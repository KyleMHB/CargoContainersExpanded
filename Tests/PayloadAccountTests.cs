using System;
using CargoContainersExpanded;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CargoContainersExpanded.Tests
{
    internal sealed class PreparedRefund
    {
        public PreparedRefund(string defName, int count)
        {
            DefName = defName;
            Count = count;
        }

        public string DefName { get; }

        public int Count { get; }

        public bool Destroyed { get; set; }
    }

    internal sealed class ProductToClamp
    {
        public ProductToClamp(string defName, int stackCount)
        {
            DefName = defName;
            StackCount = stackCount;
        }

        public string DefName { get; }

        public int StackCount { get; set; }

        public bool Destroyed { get; set; }

        public bool RotTransferred { get; set; }
    }

    [TestFixture]
    public sealed class PayloadAccountTests
    {
        [Test]
        public void OpeningFreshAccountStartsWithTheProfileMaximum()
        {
            PayloadAccount account = PayloadAccount.Open(
                new PayloadProfile("Steel", 50, 2f, null, 75),
                new PayloadSaveState(false, false, 0));

            Assert.That(account.Snapshot.Initialized, Is.True);
            Assert.That(account.Snapshot.RemainingPayloadCount, Is.EqualTo(50));
            Assert.That(account.Snapshot.DestroyWhenIterationCompletes, Is.False);
        }

        [Test]
        public void OpeningLoadedAccountClampsNegativeAndExcessiveCounts()
        {
            PayloadAccount negative = PayloadAccount.Open(
                new PayloadProfile("Steel", 50, 2f, null, 75),
                new PayloadSaveState(true, false, -4));
            PayloadAccount excessive = PayloadAccount.Open(
                new PayloadProfile("Steel", 50, 2f, null, 75),
                new PayloadSaveState(true, false, 99));

            Assert.That(negative.Snapshot.RemainingPayloadCount, Is.EqualTo(0));
            Assert.That(excessive.Snapshot.RemainingPayloadCount, Is.EqualTo(50));
        }

        [Test]
        public void WithdrawalReturnsRequestedAmountAndLeavesTheRemainder()
        {
            PayloadAccount account = PayloadAccount.Open(
                new PayloadProfile("Steel", 50, 2f, null, 75),
                new PayloadSaveState(false, false, 0));

            PayloadWithdrawal withdrawal = account.Withdraw(25);

            Assert.That(withdrawal.TakenCount, Is.EqualTo(25));
            Assert.That(account.Snapshot.RemainingPayloadCount, Is.EqualTo(25));
            Assert.That(account.Snapshot.DestroyWhenIterationCompletes, Is.False);
        }

        [Test]
        public void NonPositiveAndExcessiveWithdrawalsClampWithoutOverdrawing()
        {
            PayloadAccount account = PayloadAccount.Open(
                new PayloadProfile("Steel", 10, 2f, null, 75),
                new PayloadSaveState(false, false, 0));

            Assert.That(account.Withdraw(0).TakenCount, Is.EqualTo(0));
            Assert.That(account.Withdraw(-5).TakenCount, Is.EqualTo(0));
            Assert.That(account.Withdraw(99).TakenCount, Is.EqualTo(10));
            Assert.That(account.Snapshot.RemainingPayloadCount, Is.EqualTo(0));
            Assert.That(account.Withdraw(1).TakenCount, Is.EqualTo(0));
        }

        [Test]
        public void MultiStepWithdrawalPreservesFinalPartialAndOneTimePendingFinalization()
        {
            PayloadAccount account = PayloadAccount.Open(
                new PayloadProfile("Steel", 10, 2f, null, 75),
                new PayloadSaveState(false, false, 0));

            PayloadWithdrawal first = account.Withdraw(6);
            PayloadWithdrawal final = account.Withdraw(6);

            Assert.That(first.RequestedCount, Is.EqualTo(6));
            Assert.That(first.TakenCount, Is.EqualTo(6));
            Assert.That(first.RemainingPayloadCount, Is.EqualTo(4));
            Assert.That(final.RequestedCount, Is.EqualTo(6));
            Assert.That(final.TakenCount, Is.EqualTo(4));
            Assert.That(final.RemainingPayloadCount, Is.EqualTo(0));
            Assert.That(final.RequestedFinalization, Is.True);
            Assert.That(account.Snapshot.DestroyWhenIterationCompletes, Is.True);
            Assert.That(account.TryConsumeFinalizationRequest(false), Is.False);
            Assert.That(account.Snapshot.DestroyWhenIterationCompletes, Is.True);
            Assert.That(account.TryConsumeFinalizationRequest(true), Is.True);
            Assert.That(account.TryConsumeFinalizationRequest(true), Is.False);
        }

        [Test]
        public void FinalWithdrawalRetainsFinalizationUntilAHostCanFinalize()
        {
            PayloadAccount account = PayloadAccount.Open(
                new PayloadProfile("Steel", 7, 2f, null, 75),
                new PayloadSaveState(false, false, 0));

            PayloadWithdrawal withdrawal = account.Withdraw(25);

            Assert.That(withdrawal.TakenCount, Is.EqualTo(7));
            Assert.That(account.Snapshot.DestroyWhenIterationCompletes, Is.True);
            Assert.That(account.TryConsumeFinalizationRequest(false), Is.False);
            Assert.That(account.Snapshot.DestroyWhenIterationCompletes, Is.True);
            Assert.That(account.TryConsumeFinalizationRequest(true), Is.True);
            Assert.That(account.Snapshot.DestroyWhenIterationCompletes, Is.False);
            Assert.That(account.TryConsumeFinalizationRequest(true), Is.False);
        }

        [Test]
        public void StoredMarketValueUsesTenPercentOfTheRemainingPayloadValue()
        {
            PayloadAccount account = PayloadAccount.Open(
                new PayloadProfile("Steel", 80, 2.5f, null, 75),
                new PayloadSaveState(false, false, 0));
            account.Withdraw(30);

            float marketValue;
            Assert.That(account.TryGetStoredMarketValue(out marketValue), Is.True);
            Assert.That(marketValue, Is.EqualTo(12.5f));
        }

        [Test]
        public void StoredMarketValueAllowsEmptyPayloadAndRejectsInvalidNonEmptyValues()
        {
            PayloadAccount empty = PayloadAccount.Open(
                new PayloadProfile(null, 20, 0f, null, 75),
                new PayloadSaveState(true, false, 0));
            PayloadAccount zeroValue = PayloadAccount.Open(
                new PayloadProfile("Steel", 20, 0f, null, 75),
                new PayloadSaveState(true, false, 3));
            PayloadAccount negativeValue = PayloadAccount.Open(
                new PayloadProfile("Steel", 20, -1f, null, 75),
                new PayloadSaveState(true, false, 3));
            PayloadAccount notANumber = PayloadAccount.Open(
                new PayloadProfile("Steel", 20, float.NaN, null, 75),
                new PayloadSaveState(true, false, 3));
            PayloadAccount positiveInfinity = PayloadAccount.Open(
                new PayloadProfile("Steel", 20, float.PositiveInfinity, null, 75),
                new PayloadSaveState(true, false, 3));
            PayloadAccount negativeInfinity = PayloadAccount.Open(
                new PayloadProfile("Steel", 20, float.NegativeInfinity, null, 75),
                new PayloadSaveState(true, false, 3));

            float marketValue;
            Assert.That(empty.TryGetStoredMarketValue(out marketValue), Is.True);
            Assert.That(marketValue, Is.EqualTo(0f));
            Assert.That(zeroValue.TryGetStoredMarketValue(out marketValue), Is.False);
            Assert.That(negativeValue.TryGetStoredMarketValue(out marketValue), Is.False);
            Assert.That(notANumber.TryGetStoredMarketValue(out marketValue), Is.False);
            Assert.That(positiveInfinity.TryGetStoredMarketValue(out marketValue), Is.False);
            Assert.That(negativeInfinity.TryGetStoredMarketValue(out marketValue), Is.False);
        }

        [Test]
        public void RefundPlanExcludesFixedPayloadPreservesOrderAndSplitsStacks()
        {
            PayloadAccount account = PayloadAccount.Open(
                new PayloadProfile("RawRice", 100, 1f, "RawRice", 30),
                new PayloadSaveState(true, false, 7));

            RefundPlan plan = account.PlanRefunds(new[]
            {
                new RefundIngredientFacts("Steel", 65, 50),
                new RefundIngredientFacts("RawRice", 20, 100),
                new RefundIngredientFacts("Invalid", -2, 20),
                new RefundIngredientFacts("Plasteel", 4, 0)
            });

            Assert.That(plan.Entries.Select(entry => entry.DefName), Is.EqualTo(new[]
            {
                "Steel", "Steel", "Plasteel", "Plasteel", "Plasteel", "Plasteel", "RawRice"
            }));
            Assert.That(plan.Entries.Select(entry => entry.Count), Is.EqualTo(new[] { 50, 15, 1, 1, 1, 1, 7 }));
        }

        [Test]
        public void RefundPlanIsAnImmutableSnapshot()
        {
            var frameCosts = new[] { new RefundIngredientFacts("Steel", 2, 75) };
            PayloadAccount account = PayloadAccount.Open(
                new PayloadProfile("RawRice", 10, 1f, null, 75),
                new PayloadSaveState(true, false, 3));

            RefundPlan plan = account.PlanRefunds(frameCosts);
            frameCosts[0] = new RefundIngredientFacts("Plasteel", 99, 99);

            Assert.That(plan.Entries.Select(entry => entry.DefName), Is.EqualTo(new[] { "Steel", "RawRice" }));
            Assert.That(
                () => ((IList<RefundStackPlan>)plan.Entries).Clear(),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void RefundPlanSplitsPayloadRemainderUsingPayloadStackLimit()
        {
            PayloadAccount account = PayloadAccount.Open(
                new PayloadProfile("RawRice", 100, 1f, null, 30),
                new PayloadSaveState(true, false, 67));

            RefundPlan plan = account.PlanRefunds(Array.Empty<RefundIngredientFacts>());

            Assert.That(plan.Entries.Select(entry => entry.DefName), Is.EqualTo(new[]
            {
                "RawRice", "RawRice", "RawRice"
            }));
            Assert.That(plan.Entries.Select(entry => entry.Count), Is.EqualTo(new[] { 30, 30, 7 }));
        }

        [Test]
        public void RefundPlanKeepsRepeatedFrameDefsAsIndependentOrderedEntries()
        {
            PayloadAccount account = PayloadAccount.Open(
                new PayloadProfile("RawRice", 20, 1f, null, 75),
                new PayloadSaveState(true, false, 2));

            RefundPlan plan = account.PlanRefunds(new[]
            {
                new RefundIngredientFacts("Steel", 3, 2),
                new RefundIngredientFacts("Steel", 3, 2)
            });

            Assert.That(plan.Entries.Select(entry => entry.DefName), Is.EqualTo(new[]
            {
                "Steel", "Steel", "Steel", "Steel", "RawRice"
            }));
            Assert.That(plan.Entries.Select(entry => entry.Count), Is.EqualTo(new[] { 2, 1, 2, 1, 2 }));
        }

        [Test]
        public void RefundMaterializationFailureDestroysEveryPreparedUnspawnedThing()
        {
            RefundPlan plan = new RefundPlan(new[]
            {
                new RefundStackPlan("Steel", 2),
                new RefundStackPlan("Broken", 1),
                new RefundStackPlan("Plasteel", 3)
            });
            var prepared = new List<PreparedRefund>();
            var allCreated = new List<PreparedRefund>();
            string error;

            bool succeeded = PayloadRefundMaterialization.TryPrepare(
                plan,
                entry =>
                {
                    if (entry.DefName == "Broken")
                    {
                        throw new InvalidOperationException("cannot materialize Broken");
                    }

                    var refund = new PreparedRefund(entry.DefName, entry.Count);
                    allCreated.Add(refund);
                    return refund;
                },
                refund => refund.Destroyed = true,
                prepared,
                out error);

            Assert.That(succeeded, Is.False);
            Assert.That(error, Is.EqualTo("cannot materialize Broken"));
            Assert.That(prepared, Is.Empty);
            Assert.That(allCreated.Select(refund => refund.Destroyed), Is.EqualTo(new[] { true }));
        }

        [Test]
        public void ProductClampingLimitsBatchDestroysWrongDefsAndTransfersRotProgress()
        {
            PayloadAccount account = PayloadAccount.Open(
                new PayloadProfile("RawRice", 20, 1f, null, 75),
                new PayloadSaveState(true, false, 20));
            var products = new[]
            {
                new ProductToClamp("RawRice", 8),
                new ProductToClamp("Steel", 1),
                new ProductToClamp("RawRice", 3)
            };

            IReadOnlyList<ProductToClamp> clamped = PayloadProductClamper.Clamp(
                account,
                "RawRice",
                products,
                10,
                product => product.DefName,
                product => product.StackCount,
                (product, count) => product.StackCount = count,
                product => product.Destroyed = true,
                product => product.RotTransferred = true);

            Assert.That(clamped.Count, Is.EqualTo(2));
            Assert.That(clamped.Select(product => product.StackCount), Is.EqualTo(new[] { 8, 2 }));
            Assert.That(clamped.Select(product => product.RotTransferred), Is.EqualTo(new[] { true, true }));
            Assert.That(products[1].Destroyed, Is.True);
            Assert.That(account.Snapshot.RemainingPayloadCount, Is.EqualTo(10));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CargoContainersExpanded
{
    internal sealed class PayloadProfile
    {
        public PayloadProfile(
            string payloadDefName,
            int maxPayloadCount,
            float baseMarketValue,
            string fixedPayloadDefName,
            int payloadStackLimit)
        {
            PayloadDefName = payloadDefName;
            MaxPayloadCount = maxPayloadCount;
            BaseMarketValue = baseMarketValue;
            FixedPayloadDefName = fixedPayloadDefName;
            PayloadStackLimit = payloadStackLimit;
        }

        public string PayloadDefName { get; }

        public int MaxPayloadCount { get; }

        public float BaseMarketValue { get; }

        public string FixedPayloadDefName { get; }

        public int PayloadStackLimit { get; }
    }

    internal sealed class PayloadSaveState
    {
        public PayloadSaveState(
            bool initialized,
            bool destroyWhenIterationCompletes,
            int remainingPayloadCount)
        {
            Initialized = initialized;
            DestroyWhenIterationCompletes = destroyWhenIterationCompletes;
            RemainingPayloadCount = remainingPayloadCount;
        }

        public bool Initialized { get; }

        public bool DestroyWhenIterationCompletes { get; }

        public int RemainingPayloadCount { get; }
    }

    internal sealed class PayloadSnapshot
    {
        public PayloadSnapshot(
            bool initialized,
            bool destroyWhenIterationCompletes,
            int remainingPayloadCount)
        {
            Initialized = initialized;
            DestroyWhenIterationCompletes = destroyWhenIterationCompletes;
            RemainingPayloadCount = remainingPayloadCount;
        }

        public bool Initialized { get; }

        public bool DestroyWhenIterationCompletes { get; }

        public int RemainingPayloadCount { get; }
    }

    internal sealed class PayloadWithdrawal
    {
        public PayloadWithdrawal(int requestedCount, int takenCount, int remainingPayloadCount)
        {
            RequestedCount = requestedCount;
            TakenCount = takenCount;
            RemainingPayloadCount = remainingPayloadCount;
        }

        public int RequestedCount { get; }

        public int TakenCount { get; }

        public int RemainingPayloadCount { get; }

        public bool RequestedFinalization => RemainingPayloadCount == 0 && TakenCount > 0;
    }

    internal sealed class RefundIngredientFacts
    {
        public RefundIngredientFacts(string defName, int count, int stackLimit)
        {
            DefName = defName;
            Count = count;
            StackLimit = stackLimit;
        }

        public string DefName { get; }

        public int Count { get; }

        public int StackLimit { get; }
    }

    internal sealed class RefundStackPlan
    {
        public RefundStackPlan(string defName, int count)
        {
            DefName = defName;
            Count = count;
        }

        public string DefName { get; }

        public int Count { get; }
    }

    internal sealed class RefundPlan
    {
        public RefundPlan(IReadOnlyList<RefundStackPlan> entries)
        {
            Entries = new ReadOnlyCollection<RefundStackPlan>(
                new List<RefundStackPlan>(entries ?? Array.Empty<RefundStackPlan>()));
        }

        public IReadOnlyList<RefundStackPlan> Entries { get; }
    }

    // This helper owns only the transaction around an external materialization boundary. The
    // RimWorld adapter supplies ThingMaker and destruction delegates; deterministic account logic
    // remains independent of Verse types.
    internal static class PayloadRefundMaterialization
    {
        public static bool TryPrepare<T>(
            RefundPlan plan,
            Func<RefundStackPlan, T> materialize,
            Action<T> destroyUnspawned,
            ICollection<T> prepared,
            out string error)
            where T : class
        {
            error = null;
            try
            {
                if (plan?.Entries == null)
                {
                    return true;
                }

                foreach (RefundStackPlan entry in plan.Entries)
                {
                    T item = materialize(entry);
                    if (item == null)
                    {
                        throw new InvalidOperationException("Materialization returned no item.");
                    }

                    prepared.Add(item);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                if (prepared != null)
                {
                    foreach (T item in prepared)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        try
                        {
                            destroyUnspawned(item);
                        }
                        catch
                        {
                            // Continue cleanup for every prepared item and preserve the original
                            // materialization error for the caller.
                        }
                    }

                    prepared.Clear();
                }

                return false;
            }
        }
    }

    // This adapter-shaped helper keeps Thing mutation and rot transfer at the RimWorld boundary
    // while making the observable product-clamping transaction deterministic to characterize.
    internal static class PayloadProductClamper
    {
        public static IReadOnlyList<T> Clamp<T>(
            PayloadAccount account,
            string payloadDefName,
            IEnumerable<T> products,
            int batchCount,
            Func<T, string> getDefName,
            Func<T, int> getStackCount,
            Action<T, int> setStackCount,
            Action<T> destroy,
            Action<T> transferRotProgress)
            where T : class
        {
            var clampedProducts = new List<T>();
            int remainingBatchCount = Math.Max(batchCount, 0);
            if (account == null || products == null)
            {
                return new ReadOnlyCollection<T>(clampedProducts);
            }

            foreach (T product in products)
            {
                if (product == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(payloadDefName) ||
                    !string.Equals(getDefName(product), payloadDefName, StringComparison.Ordinal))
                {
                    destroy(product);
                    continue;
                }

                int requestedCount = Math.Min(getStackCount(product), remainingBatchCount);
                int takenCount = account.Withdraw(requestedCount).TakenCount;
                if (takenCount <= 0)
                {
                    destroy(product);
                    break;
                }

                setStackCount(product, takenCount);
                transferRotProgress(product);
                clampedProducts.Add(product);
                remainingBatchCount -= takenCount;
                if (remainingBatchCount <= 0)
                {
                    break;
                }
            }

            return new ReadOnlyCollection<T>(clampedProducts);
        }
    }

    internal sealed class PayloadAccount
    {
        private readonly PayloadProfile profile;
        private bool initialized;
        private bool destroyWhenIterationCompletes;
        private int remainingPayloadCount;

        private PayloadAccount(PayloadProfile profile, PayloadSaveState savedState)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            PayloadSaveState state = savedState ?? new PayloadSaveState(false, false, 0);
            initialized = state.Initialized;
            destroyWhenIterationCompletes = state.DestroyWhenIterationCompletes;
            remainingPayloadCount = state.RemainingPayloadCount;

            int maximum = Math.Max(profile.MaxPayloadCount, 0);
            if (!initialized)
            {
                remainingPayloadCount = maximum;
                initialized = true;
            }
            else
            {
                remainingPayloadCount = Math.Min(Math.Max(remainingPayloadCount, 0), maximum);
            }
        }

        public PayloadSnapshot Snapshot => new PayloadSnapshot(
            initialized,
            destroyWhenIterationCompletes,
            Math.Max(remainingPayloadCount, 0));

        public static PayloadAccount Open(PayloadProfile profile, PayloadSaveState savedState)
        {
            return new PayloadAccount(profile, savedState);
        }

        public PayloadWithdrawal Withdraw(int requestedCount)
        {
            if (requestedCount <= 0 || remainingPayloadCount <= 0)
            {
                return new PayloadWithdrawal(requestedCount, 0, Math.Max(remainingPayloadCount, 0));
            }

            int takenCount = Math.Min(requestedCount, remainingPayloadCount);
            remainingPayloadCount -= takenCount;
            if (remainingPayloadCount <= 0)
            {
                remainingPayloadCount = 0;
                destroyWhenIterationCompletes = true;
            }

            return new PayloadWithdrawal(requestedCount, takenCount, remainingPayloadCount);
        }

        public bool TryConsumeFinalizationRequest(bool hostCanFinalize)
        {
            if (!hostCanFinalize || !destroyWhenIterationCompletes)
            {
                return false;
            }

            destroyWhenIterationCompletes = false;
            return true;
        }

        public bool TryGetStoredMarketValue(out float marketValue)
        {
            int remainingCount = Math.Max(remainingPayloadCount, 0);
            if (remainingCount <= 0)
            {
                marketValue = 0f;
                return true;
            }

            if (string.IsNullOrEmpty(profile.PayloadDefName) ||
                profile.BaseMarketValue <= 0f ||
                float.IsNaN(profile.BaseMarketValue) ||
                float.IsInfinity(profile.BaseMarketValue))
            {
                marketValue = 0f;
                return false;
            }

            marketValue = profile.BaseMarketValue * remainingCount * 0.1f;
            return true;
        }

        public RefundPlan PlanRefunds(IReadOnlyList<RefundIngredientFacts> frameCosts)
        {
            var entries = new List<RefundStackPlan>();
            if (frameCosts != null)
            {
                foreach (RefundIngredientFacts frameCost in frameCosts)
                {
                    if (frameCost == null ||
                        string.IsNullOrEmpty(frameCost.DefName) ||
                        frameCost.Count <= 0 ||
                        string.Equals(frameCost.DefName, profile.FixedPayloadDefName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    AddSplitEntries(entries, frameCost.DefName, frameCost.Count, frameCost.StackLimit);
                }
            }

            if (!string.IsNullOrEmpty(profile.PayloadDefName) && remainingPayloadCount > 0)
            {
                AddSplitEntries(
                    entries,
                    profile.PayloadDefName,
                    remainingPayloadCount,
                    profile.PayloadStackLimit);
            }

            return new RefundPlan(entries);
        }

        private static void AddSplitEntries(
            ICollection<RefundStackPlan> entries,
            string defName,
            int count,
            int stackLimit)
        {
            int safeStackLimit = Math.Max(stackLimit, 1);
            int remainingCount = count;
            while (remainingCount > 0)
            {
                int stackCount = Math.Min(remainingCount, safeStackLimit);
                entries.Add(new RefundStackPlan(defName, stackCount));
                remainingCount -= stackCount;
            }
        }
    }
}

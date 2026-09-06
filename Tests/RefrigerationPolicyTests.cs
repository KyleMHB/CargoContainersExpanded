using System;
using System.Collections.Generic;
using CargoContainersExpanded;
using NUnit.Framework;

namespace CargoContainersExpanded.Tests
{
    [TestFixture]
    public sealed class RefrigerationPolicyTests
    {
        [Test]
        public void NormalRottableItemIsEligible()
        {
            RefrigerationEligibility result = RefrigerationPolicy.EvaluateEligibility(
                new RefrigerationDefFacts(
                    "Meal",
                    false,
                    true,
                    false,
                    false,
                    Array.Empty<string>(),
                    true,
                    false,
                    false));

            Assert.That(result.IsEligible, Is.True);
            Assert.That(result.Reason, Is.EqualTo(RefrigerationEligibilityReason.Eligible));
        }

        [TestCase(true, true, false, false, "Steel")]
        [TestCase(false, false, false, false, "NonItem")]
        [TestCase(false, true, true, false, "CorpseThingClass")]
        [TestCase(false, true, false, true, "MinifiedThingClass")]
        public void EligibilityRejectsDefinitionClassesBeforeNames(
            bool isSteel,
            bool isItem,
            bool isCorpseThingClass,
            bool isMinifiedThingClass,
            string expectedReasonName)
        {
            RefrigerationEligibility result = RefrigerationPolicy.EvaluateEligibility(
                new RefrigerationDefFacts(
                    "Blueprint_Thing",
                    isSteel,
                    isItem,
                    isCorpseThingClass,
                    isMinifiedThingClass,
                    Array.Empty<string>(),
                    true,
                    false,
                    false));

            Assert.That(result.IsEligible, Is.False);
            Assert.That(result.Reason, Is.EqualTo((RefrigerationEligibilityReason)Enum.Parse(typeof(RefrigerationEligibilityReason), expectedReasonName)));
        }

        [TestCase("bLuEpRiNt_Thing", "BlueprintName")]
        [TestCase("fRaMe_Thing", "FrameName")]
        [TestCase("mInIfIeD_Thing", "MinifiedName")]
        [TestCase("eGgThing", "EggName")]
        [TestCase("FreshCorpseThing", "CorpseName")]
        public void EligibilityRejectsGeneratedNamesCaseInsensitively(
            string defName,
            string expectedReasonName)
        {
            RefrigerationEligibility result = RefrigerationPolicy.EvaluateEligibility(
                new RefrigerationDefFacts(
                    defName,
                    false,
                    true,
                    false,
                    false,
                    Array.Empty<string>(),
                    true,
                    false,
                    false));

            Assert.That(result.IsEligible, Is.False);
            Assert.That(result.Reason, Is.EqualTo((RefrigerationEligibilityReason)Enum.Parse(typeof(RefrigerationEligibilityReason), expectedReasonName)));
        }

        [Test]
        public void EligibilityPreservesCategoryComponentOrder()
        {
            RefrigerationEligibility category = RefrigerationPolicy.EvaluateEligibility(
                new RefrigerationDefFacts(
                    "FreshThing",
                    false,
                    true,
                    false,
                    false,
                    new[] { "foodCorpseLike" },
                    true,
                    false,
                    false));
            RefrigerationEligibility missingRottable = RefrigerationPolicy.EvaluateEligibility(
                new RefrigerationDefFacts(
                    "FreshThing",
                    false,
                    true,
                    false,
                    false,
                    Array.Empty<string>(),
                    false,
                    true,
                    true));

            Assert.That(category.Reason, Is.EqualTo(RefrigerationEligibilityReason.CorpseCategory));
            Assert.That(missingRottable.Reason, Is.EqualTo(RefrigerationEligibilityReason.MissingRottable));
        }

        [Test]
        public void EggLayerAndHatcherAreRejectedAfterRottableCheck()
        {
            RefrigerationEligibility eggLayer = RefrigerationPolicy.EvaluateEligibility(
                new RefrigerationDefFacts(
                    "FreshThing",
                    false,
                    true,
                    false,
                    false,
                    Array.Empty<string>(),
                    true,
                    true,
                    true));
            RefrigerationEligibility hatcher = RefrigerationPolicy.EvaluateEligibility(
                new RefrigerationDefFacts(
                    "FreshThing",
                    false,
                    true,
                    false,
                    false,
                    Array.Empty<string>(),
                    true,
                    false,
                    true));

            Assert.That(eggLayer.Reason, Is.EqualTo(RefrigerationEligibilityReason.EggLayer));
            Assert.That(hatcher.Reason, Is.EqualTo(RefrigerationEligibilityReason.Hatcher));
        }

        [Test]
        public void NullDefinitionIsNotEligible()
        {
            RefrigerationEligibility result = RefrigerationPolicy.EvaluateEligibility(null);

            Assert.That(result.IsEligible, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RefrigerationEligibilityReason.MissingDefinition));
        }

        [Test]
        public void DefinitionFactsCopyCategoryNamesBeforeEvaluation()
        {
            var categoryNames = new List<string> { "Food" };
            RefrigerationDefFacts facts = new RefrigerationDefFacts(
                "FreshThing",
                false,
                true,
                false,
                false,
                categoryNames,
                true,
                false,
                false);
            categoryNames[0] = "FoodCorpse";

            Assert.That(RefrigerationPolicy.EvaluateEligibility(facts).IsEligible, Is.True);
            Assert.That(facts.ThingCategoryDefNames, Is.EqualTo(new[] { "Food" }));
        }

        [Test]
        public void UnpoweredDurationDoublesKnownAndMissingSourceValues()
        {
            Assert.That(RefrigerationPolicy.UnpoweredDaysToRot(3.5f), Is.EqualTo(7f));
            Assert.That(RefrigerationPolicy.UnpoweredDaysToRot(null), Is.EqualTo(2f));
        }

        [Test]
        public void PoweredRuntimeSkipsTicksAndRetainsDeterministicRotValues()
        {
            RefrigerationRuntimeDecision result = RefrigerationPolicy.EvaluateRuntime(true, 0.5f, 600);

            Assert.That(result.ShouldTick, Is.False);
            Assert.That(result.TemperatureState, Is.EqualTo(RefrigerationTemperatureState.Refrigerated));
            Assert.That(result.RotPercentPerDay, Is.EqualTo(50f));
            Assert.That(result.TicksUntilRot, Is.EqualTo(1200));
        }

        [TestCase(-0.5f, "Frozen")]
        [TestCase(0f, "Frozen")]
        [TestCase(0.999f, "Refrigerated")]
        [TestCase(1f, "Unrefrigerated")]
        [TestCase(2f, "Unrefrigerated")]
        public void RuntimeUsesTheExactTemperatureThresholds(float rotRate, string expectedStateName)
        {
            RefrigerationRuntimeDecision result = RefrigerationPolicy.EvaluateRuntime(false, rotRate, 600);

            Assert.That(result.ShouldTick, Is.True);
            Assert.That(
                result.TemperatureState,
                Is.EqualTo((RefrigerationTemperatureState)Enum.Parse(typeof(RefrigerationTemperatureState), expectedStateName)));
        }

        [Test]
        public void RuntimeClassifiesNonFiniteRatesWithoutChangingTicking()
        {
            RefrigerationRuntimeDecision notANumber = RefrigerationPolicy.EvaluateRuntime(false, float.NaN, 600);
            RefrigerationRuntimeDecision positiveInfinity = RefrigerationPolicy.EvaluateRuntime(false, float.PositiveInfinity, 600);
            RefrigerationRuntimeDecision negativeInfinity = RefrigerationPolicy.EvaluateRuntime(false, float.NegativeInfinity, 600);

            Assert.That(notANumber.TemperatureState, Is.EqualTo(RefrigerationTemperatureState.Unrefrigerated));
            Assert.That(positiveInfinity.TemperatureState, Is.EqualTo(RefrigerationTemperatureState.Unrefrigerated));
            Assert.That(negativeInfinity.TemperatureState, Is.EqualTo(RefrigerationTemperatureState.Frozen));
            Assert.That(notANumber.ShouldTick, Is.True);
            Assert.That(positiveInfinity.ShouldTick, Is.True);
            Assert.That(negativeInfinity.ShouldTick, Is.True);
        }

        [Test]
        public void RuntimeClampsMissingRotStartToZeroPercentAndZeroTicks()
        {
            RefrigerationRuntimeDecision result = RefrigerationPolicy.EvaluateRuntime(false, 0.5f, 0);
            RefrigerationRuntimeDecision negative = RefrigerationPolicy.EvaluateRuntime(false, 0.5f, -20);

            Assert.That(result.RotPercentPerDay, Is.EqualTo(0f));
            Assert.That(result.TicksUntilRot, Is.EqualTo(0));
            Assert.That(negative.RotPercentPerDay, Is.EqualTo(0f));
            Assert.That(negative.TicksUntilRot, Is.EqualTo(0));
        }

        [Test]
        public void FrozenRuntimeUsesAnUnboundedRotCountdown()
        {
            RefrigerationRuntimeDecision result = RefrigerationPolicy.EvaluateRuntime(false, 0f, 600);

            Assert.That(result.TicksUntilRot, Is.EqualTo(int.MaxValue));
            Assert.That(result.RotPercentPerDay, Is.EqualTo(0f));
        }
    }
}

using System;
using System.Collections.Generic;

namespace CargoContainersExpanded
{
    internal enum RefrigerationEligibilityReason
    {
        Eligible,
        MissingDefinition,
        Steel,
        NonItem,
        CorpseThingClass,
        MinifiedThingClass,
        BlueprintName,
        FrameName,
        MinifiedName,
        EggName,
        CorpseName,
        CorpseCategory,
        MissingRottable,
        EggLayer,
        Hatcher
    }

    internal sealed class RefrigerationDefFacts
    {
        public RefrigerationDefFacts(
            string defName,
            bool isSteel,
            bool isItem,
            bool isCorpseThingClass,
            bool isMinifiedThingClass,
            IReadOnlyList<string> thingCategoryDefNames,
            bool hasRottable,
            bool hasEggLayer,
            bool hasHatcher)
        {
            DefName = defName;
            IsSteel = isSteel;
            IsItem = isItem;
            IsCorpseThingClass = isCorpseThingClass;
            IsMinifiedThingClass = isMinifiedThingClass;
            ThingCategoryDefNames = new List<string>(thingCategoryDefNames ?? Array.Empty<string>()).AsReadOnly();
            HasRottable = hasRottable;
            HasEggLayer = hasEggLayer;
            HasHatcher = hasHatcher;
        }

        public string DefName { get; }

        public bool IsSteel { get; }

        public bool IsItem { get; }

        public bool IsCorpseThingClass { get; }

        public bool IsMinifiedThingClass { get; }

        public IReadOnlyList<string> ThingCategoryDefNames { get; }

        public bool HasRottable { get; }

        public bool HasEggLayer { get; }

        public bool HasHatcher { get; }
    }

    internal sealed class RefrigerationEligibility
    {
        internal RefrigerationEligibility(bool isEligible, RefrigerationEligibilityReason reason)
        {
            IsEligible = isEligible;
            Reason = reason;
        }

        public bool IsEligible { get; }

        public RefrigerationEligibilityReason Reason { get; }
    }

    internal enum RefrigerationTemperatureState
    {
        Frozen,
        Refrigerated,
        Unrefrigerated
    }

    internal sealed class RefrigerationRuntimeDecision
    {
        internal RefrigerationRuntimeDecision(
            bool shouldTick,
            RefrigerationTemperatureState temperatureState,
            float temperatureRotRate,
            float rotPercentPerDay,
            int ticksUntilRot)
        {
            ShouldTick = shouldTick;
            TemperatureState = temperatureState;
            TemperatureRotRate = temperatureRotRate;
            RotPercentPerDay = rotPercentPerDay;
            TicksUntilRot = ticksUntilRot;
        }

        public bool ShouldTick { get; }

        public RefrigerationTemperatureState TemperatureState { get; }

        public float TemperatureRotRate { get; }

        public float RotPercentPerDay { get; }

        public int TicksUntilRot { get; }
    }

    internal static class RefrigerationPolicy
    {
        private const float TicksPerDay = 60000f;

        public static RefrigerationEligibility EvaluateEligibility(RefrigerationDefFacts facts)
        {
            if (facts == null)
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.MissingDefinition);
            }

            if (facts.IsSteel)
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.Steel);
            }

            if (!facts.IsItem)
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.NonItem);
            }

            if (facts.IsCorpseThingClass)
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.CorpseThingClass);
            }

            if (facts.IsMinifiedThingClass)
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.MinifiedThingClass);
            }

            if (StartsWith(facts.DefName, "Blueprint_"))
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.BlueprintName);
            }

            if (StartsWith(facts.DefName, "Frame_"))
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.FrameName);
            }

            if (StartsWith(facts.DefName, "Minified_"))
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.MinifiedName);
            }

            if (StartsWith(facts.DefName, "Egg"))
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.EggName);
            }

            if (Contains(facts.DefName, "Corpse"))
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.CorpseName);
            }

            if (HasCorpseCategory(facts.ThingCategoryDefNames))
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.CorpseCategory);
            }

            if (!facts.HasRottable)
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.MissingRottable);
            }

            if (facts.HasEggLayer)
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.EggLayer);
            }

            if (facts.HasHatcher)
            {
                return new RefrigerationEligibility(false, RefrigerationEligibilityReason.Hatcher);
            }

            return new RefrigerationEligibility(true, RefrigerationEligibilityReason.Eligible);
        }

        public static float UnpoweredDaysToRot(float? sourceDaysToRot)
        {
            return (sourceDaysToRot ?? 1f) * 2f;
        }

        public static RefrigerationRuntimeDecision EvaluateRuntime(
            bool powered,
            float temperatureRotRate,
            int ticksToRotStart)
        {
            RefrigerationTemperatureState temperatureState;
            if (temperatureRotRate <= 0f)
            {
                temperatureState = RefrigerationTemperatureState.Frozen;
            }
            else if (temperatureRotRate < 1f)
            {
                temperatureState = RefrigerationTemperatureState.Refrigerated;
            }
            else
            {
                temperatureState = RefrigerationTemperatureState.Unrefrigerated;
            }

            float rotPercentPerDay = ticksToRotStart > 0
                ? temperatureRotRate * TicksPerDay / ticksToRotStart
                : 0f;

            return new RefrigerationRuntimeDecision(
                !powered,
                temperatureState,
                temperatureRotRate,
                rotPercentPerDay,
                TicksUntilRot(temperatureRotRate, ticksToRotStart));
        }

        private static bool StartsWith(string value, string prefix)
        {
            return value != null && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string value, string fragment)
        {
            return value != null && value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasCorpseCategory(IReadOnlyList<string> categoryDefNames)
        {
            if (categoryDefNames == null)
            {
                return false;
            }

            foreach (string categoryDefName in categoryDefNames)
            {
                if (Contains(categoryDefName, "Corpse"))
                {
                    return true;
                }
            }

            return false;
        }

        private static int TicksUntilRot(float temperatureRotRate, int ticksToRotStart)
        {
            if (temperatureRotRate <= 0f || float.IsNaN(temperatureRotRate))
            {
                return int.MaxValue;
            }

            if (ticksToRotStart <= 0)
            {
                return 0;
            }

            double ticks = ticksToRotStart / (double)temperatureRotRate;
            if (double.IsNaN(ticks) || ticks >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)Math.Ceiling(ticks);
        }
    }
}

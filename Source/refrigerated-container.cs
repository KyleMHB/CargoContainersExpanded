using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;
using RimWorld;

namespace CargoContainersExpanded
{
    public class CompProperties_RefrigeratedContainer : CompProperties_Rottable
    {
        public CompProperties_RefrigeratedContainer()
        {
            compClass = typeof(CompRefrigeratedContainer);
            rotDestroys = true;
            rotDamagePerDay = 0f;
            dessicatedDamagePerDay = 0f;
        }
    }

    public class CompRefrigeratedContainer : CompRottable
    {
        private CompPowerTrader powerComp;

        private ThingDef StuffDef => parent?.Stuff ?? parent?.def?.defaultStuff;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.GetComp<CompPowerTrader>();
            ConfigureRotPropertiesFromStuff();
        }

        public override void CompTickRare()
        {
            RefrigerationRuntimeDecision decision = RefrigerationPolicy.EvaluateRuntime(
                IsPowered(),
                0f,
                PropsRot?.TicksToRotStart ?? 0);
            if (!decision.ShouldTick)
            {
                return;
            }

            base.CompTickRare();
        }

        public override string CompInspectStringExtra()
        {
            if (IsPowered())
            {
                return "CCE_PoweredRefrigerationActive".Translate();
            }

            float temperature = GetCurrentTemperature();
            float rotRate = GenTemperature.RotRateAtTemperature(temperature);
            RefrigerationRuntimeDecision decision = RefrigerationPolicy.EvaluateRuntime(
                false,
                rotRate,
                PropsRot?.TicksToRotStart ?? 0);
            return GetRotInspectString(decision);
        }

        private bool IsPowered()
        {
            return powerComp != null && powerComp.PowerOn;
        }

        private void ConfigureRotPropertiesFromStuff()
        {
            var refrigeratedProps = new CompProperties_RefrigeratedContainer
            {
                daysToRotStart = GetRotDays(),
                daysToDessicated = PropsRot.daysToDessicated,
                dessicatedDamagePerDay = 0f,
                disableIfHatcher = PropsRot.disableIfHatcher,
                rotDamagePerDay = 0f,
                rotDestroys = true
            };

            props = refrigeratedProps;
        }

        private float GetRotDays()
        {
            var stuffDef = StuffDef;
            var rottableProps = stuffDef?.GetCompProperties<CompProperties_Rottable>();
            return RefrigerationPolicy.UnpoweredDaysToRot(rottableProps?.daysToRotStart);
        }

        private string GetRotInspectString(RefrigerationRuntimeDecision decision)
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine(GetRotStateLabel());
            builder.AppendLine(GetTemperatureRotLabel(decision));
            builder.Append(GetRotRateLabel(decision));
            return builder.ToString();
        }

        private string GetRotStateLabel()
        {
            switch (Stage)
            {
                case RotStage.Fresh:
                    return "RotStateFresh".Translate();
                case RotStage.Rotting:
                    return "RotStateRotting".Translate();
                case RotStage.Dessicated:
                    return "RotStateDessicated".Translate();
                default:
                    return Stage.ToString();
            }
        }

        private string GetTemperatureRotLabel(RefrigerationRuntimeDecision decision)
        {
            if (decision.TemperatureState == RefrigerationTemperatureState.Frozen)
            {
                return "CCE_CurrentlyFrozen".Translate();
            }

            string ticksUntilRot = FormatTicksUntilRot(TicksUntilRotAtCurrentTemp);
            if (decision.TemperatureState == RefrigerationTemperatureState.Refrigerated)
            {
                return "CCE_CurrentlyRefrigerated".Translate(ticksUntilRot);
            }

            return "CCE_NotRefrigerated".Translate(ticksUntilRot);
        }

        private string GetRotRateLabel(RefrigerationRuntimeDecision decision)
        {
            return "CCE_RotRate".Translate(
                decision.TemperatureRotRate.ToString("0.##", CultureInfo.InvariantCulture),
                (decision.RotPercentPerDay * 100f).ToString("0.#", CultureInfo.InvariantCulture));
        }

        private float GetCurrentTemperature()
        {
            if (parent == null || parent.Map == null)
            {
                return 21f;
            }

            if (GenTemperature.TryGetAirTemperatureAroundThing(parent, out float temperature))
            {
                return temperature;
            }

            return parent.AmbientTemperature;
        }

        private static string FormatTicksUntilRot(int ticks)
        {
            if (ticks == int.MaxValue)
            {
                return "Never".Translate();
            }

            return GenDate.ToStringTicksToPeriodVague(Mathf.Max(ticks, 0), true, false);
        }
    }

    public class Graphic_RefrigeratedContainer : Graphic_Multi
    {
        private const float IconSize = 0.78f;
        private const float IconAltitudeOffset = 0.02f;

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            base.DrawWorker(loc, rot, thingDef, thing, extraRotation);

            ThingDef stuffDef = thing?.Stuff ?? thingDef?.defaultStuff;
            Texture2D icon = stuffDef?.uiIcon;
            if (icon == null)
            {
                return;
            }

            Material material = MaterialPool.MatFrom(icon);
            Vector3 iconLoc = loc;
            iconLoc.y += IconAltitudeOffset;

            Matrix4x4 matrix = Matrix4x4.TRS(iconLoc, Quaternion.identity, new Vector3(IconSize, 1f, IconSize));
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }
    }

    public static class RefrigeratedContainerBootstrap
    {
        private const string RefrigeratedStuffCategoryDefName = "FT_CargoContainerRefrigeratedStuff";
        private static readonly string[] RefrigeratedBuildableDefNames =
        {
            "FT_RefrigeratedContainer",
            "FT_RefrigeratedContainerHalf"
        };

        public static void Initialize()
        {
            try
            {
                var refrigeratedStuffCategory = DefDatabase<StuffCategoryDef>.GetNamedSilentFail(RefrigeratedStuffCategoryDefName);
                if (refrigeratedStuffCategory == null)
                {
                    Log.Error("Cargo Containers Expanded: missing refrigerated stuff category.");
                    return;
                }

                var eligibleTargets = GetEligibleTargetDefs();
                ApplyRefrigeratedStuffCategoryToEligibleTargets(refrigeratedStuffCategory, eligibleTargets);
                ConfigureRefrigeratedBuildables(refrigeratedStuffCategory, eligibleTargets);
            }
            catch (Exception exception)
            {
                Log.Error($"Cargo Containers Expanded: failed to initialize refrigerated containers.\n{exception}");
            }
        }

        private static List<ThingDef> GetEligibleTargetDefs()
        {
            var eligibleTargets = new List<ThingDef>();
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (IsEligibleTargetDef(def))
                {
                    eligibleTargets.Add(def);
                }
            }

            return eligibleTargets;
        }

        private static void ApplyRefrigeratedStuffCategoryToEligibleTargets(StuffCategoryDef refrigeratedStuffCategory, List<ThingDef> eligibleTargets)
        {
            foreach (var target in eligibleTargets)
            {
                EnsureStuffCategory(target, refrigeratedStuffCategory);
            }
        }

        private static void ConfigureRefrigeratedBuildables(StuffCategoryDef refrigeratedStuffCategory, List<ThingDef> eligibleTargets)
        {
            if (eligibleTargets.Count == 0)
            {
                Log.Warning("Cargo Containers Expanded: no eligible refrigerated stuff was found to use as a default selection.");
                return;
            }

            foreach (var defName in RefrigeratedBuildableDefNames)
            {
                var buildable = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (buildable == null)
                {
                    Log.Error($"Cargo Containers Expanded: missing refrigerated buildable {defName}.");
                    continue;
                }

                buildable.stuffCategories ??= new List<StuffCategoryDef>();
                if (!buildable.stuffCategories.Contains(refrigeratedStuffCategory))
                {
                    buildable.stuffCategories.Add(refrigeratedStuffCategory);
                }
            }
        }

        private static void EnsureStuffCategory(ThingDef target, StuffCategoryDef refrigeratedStuffCategory)
        {
            if (target == null || refrigeratedStuffCategory == null)
            {
                return;
            }

            if (target.stuffProps == null)
            {
                target.stuffProps = new StuffProperties
                {
                    parent = target,
                    categories = new List<StuffCategoryDef>()
                };
            }

            target.stuffProps.parent = target;
            target.stuffProps.categories ??= new List<StuffCategoryDef>();

            if (!target.stuffProps.categories.Contains(refrigeratedStuffCategory))
            {
                target.stuffProps.categories.Add(refrigeratedStuffCategory);
            }
        }

        private static bool IsEligibleTargetDef(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }

            return RefrigerationPolicy.EvaluateEligibility(
                new RefrigerationDefFacts(
                    def.defName,
                    def == ThingDefOf.Steel,
                    def.category == ThingCategory.Item,
                    IsCorpseThingClass(def.thingClass),
                    IsMinifiedThingClass(def.thingClass),
                    GetThingCategoryDefNames(def),
                    def.GetCompProperties<CompProperties_Rottable>() != null,
                    def.GetCompProperties<CompProperties_EggLayer>() != null,
                    def.GetCompProperties<CompProperties_Hatcher>() != null)).IsEligible;
        }

        private static bool IsCorpseThingClass(Type thingClass)
        {
            return thingClass != null && typeof(Corpse).IsAssignableFrom(thingClass);
        }

        private static bool IsMinifiedThingClass(Type thingClass)
        {
            return thingClass != null && typeof(MinifiedThing).IsAssignableFrom(thingClass);
        }

        private static IReadOnlyList<string> GetThingCategoryDefNames(ThingDef def)
        {
            var categoryNames = new List<string>();
            if (def?.thingCategories == null)
            {
                return categoryNames;
            }

            foreach (ThingCategoryDef category in def.thingCategories)
            {
                if (category != null)
                {
                    categoryNames.Add(category.defName);
                }
            }

            return categoryNames;
        }

    }
}

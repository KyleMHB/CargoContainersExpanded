using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CargoContainersExpanded
{
    public class Building_ExtractableCargoContainer : Building_WorkTable
    {
        private bool cleanDeconstructionInProgress;
        private Rot4 cachedInteractionCellRotation = Rot4.Invalid;
        private IntVec3 cachedInteractionCell = IntVec3.Invalid;
        private List<IntVec3> cachedInteractionCells;

        public override IntVec3 InteractionCell => GetCargoInteractionCell();

        public override List<IntVec3> InteractionCells
        {
            get
            {
                IntVec3 interactionCell = InteractionCell;
                if (cachedInteractionCells == null || cachedInteractionCells.Count != 1 || cachedInteractionCells[0] != interactionCell)
                {
                    cachedInteractionCells = new List<IntVec3> { interactionCell };
                }

                return cachedInteractionCells;
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (cleanDeconstructionInProgress)
            {
                return;
            }

            if (mode != DestroyMode.Deconstruct || Map == null)
            {
                base.Destroy(mode);
                return;
            }

            CompExtractableContainer extractableComp = GetComp<CompExtractableContainer>();
            if (extractableComp == null)
            {
                base.Destroy(mode);
                return;
            }

            if (!extractableComp.TryPrepareCleanRefunds(out List<Thing> preparedRefunds, out string preparationError))
            {
                string error = $"Cargo Containers Expanded: aborted deconstruction of {def?.defName ?? "unknown container"}; refunds could not be prepared. {preparationError}";
                Log.Error(error);
                Messages.Message(error, this, MessageTypeDefOf.RejectInput, false);
                return;
            }

            Map refundMap = Map;
            IntVec3 refundPosition = Position;
            cleanDeconstructionInProgress = true;
            try
            {
                base.Destroy(mode);
                CompExtractableContainer.PlacePreparedRefunds(preparedRefunds, refundPosition, refundMap);
            }
            catch
            {
                CompExtractableContainer.DestroyPreparedRefunds(preparedRefunds);
                throw;
            }
            finally
            {
                cleanDeconstructionInProgress = false;
            }
        }

        private IntVec3 GetCargoInteractionCell()
        {
            if (cachedInteractionCellRotation == Rotation && cachedInteractionCell.IsValid)
            {
                return cachedInteractionCell;
            }

            CellRect occupiedRect = this.OccupiedRect();
            switch (Rotation.AsInt)
            {
                case 0:
                    cachedInteractionCell = new IntVec3(occupiedRect.maxX + 1, 0, occupiedRect.CenterCell.z);
                    break;
                case 1:
                    cachedInteractionCell = new IntVec3(occupiedRect.CenterCell.x, 0, occupiedRect.minZ - 1);
                    break;
                case 2:
                    cachedInteractionCell = new IntVec3(occupiedRect.minX - 1, 0, occupiedRect.CenterCell.z);
                    break;
                case 3:
                    cachedInteractionCell = new IntVec3(occupiedRect.CenterCell.x, 0, occupiedRect.maxZ + 1);
                    break;
                default:
                    cachedInteractionCell = base.InteractionCell;
                    break;
            }

            cachedInteractionCellRotation = Rotation;
            return cachedInteractionCell;
        }
    }

    public class CompProperties_ExtractableContainer : CompProperties
    {
        public ThingDef fixedPayloadDef;
        public int fixedPayloadCount;

        public CompProperties_ExtractableContainer()
        {
            compClass = typeof(CompExtractableContainer);
        }
    }

    public class CompExtractableContainer : ThingComp
    {
        // Intentional balance rule: stored payload contributes ten percent of its loose-item wealth.
        private const float StoredPayloadMarketValueFactor = 0.1f;
        private bool initialized;
        private bool destroyWhenIterationCompletes;
        private int remainingPayloadCount;

        public CompProperties_ExtractableContainer PropsExtractable => (CompProperties_ExtractableContainer)props;

        public ThingDef PayloadDef => PropsExtractable.fixedPayloadDef ?? parent?.Stuff ?? parent?.def?.defaultStuff;

        public int MaxPayloadCount
        {
            get
            {
                if (PropsExtractable.fixedPayloadCount > 0)
                {
                    return PropsExtractable.fixedPayloadCount;
                }

                return parent?.def?.costStuffCount ?? 0;
            }
        }

        public int RemainingPayloadCount => Math.Max(remainingPayloadCount, 0);

        public bool HasPayload => PayloadDef != null && RemainingPayloadCount > 0;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            InitializeIfNeeded();
            RemoveInvalidExtractionBills();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref initialized, "initialized", false);
            Scribe_Values.Look(ref destroyWhenIterationCompletes, "destroyWhenIterationCompletes", false);
            Scribe_Values.Look(ref remainingPayloadCount, "remainingPayloadCount", 0);
        }

        public override string CompInspectStringExtra()
        {
            InitializeIfNeeded();
            ThingDef payloadDef = PayloadDef;
            if (payloadDef == null || MaxPayloadCount <= 0)
            {
                return null;
            }

            return "CCE_CargoPayload".Translate(RemainingPayloadCount, MaxPayloadCount, payloadDef.label);
        }

        public bool CanRunRecipe(RecipeDef recipeDef)
        {
            InitializeIfNeeded();
            return CargoExtractionUtility.IsValidExtractionRecipeFor(parent, recipeDef) && RemainingPayloadCount > 0;
        }

        public bool HasMatchingPayloadRecipe(RecipeDef recipeDef)
        {
            return CargoExtractionUtility.IsValidExtractionRecipeFor(parent, recipeDef);
        }

        public float GetCurrentRotProgressPct()
        {
            if (parent is not ThingWithComps thingWithComps)
            {
                return 0f;
            }

            CompRottable rottableComp = thingWithComps.GetComp<CompRottable>();
            if (rottableComp == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(rottableComp.RotProgressPct);
        }

        public bool TryGetContainerMarketValue(out float marketValue)
        {
            InitializeIfNeeded();

            ThingDef payloadDef = PayloadDef;
            int remainingCount = RemainingPayloadCount;
            if (remainingCount <= 0)
            {
                marketValue = 0f;
                return true;
            }

            if (payloadDef == null)
            {
                marketValue = 0f;
                return false;
            }

            float payloadMarketValue = payloadDef.BaseMarketValue;
            if (payloadMarketValue <= 0f || float.IsNaN(payloadMarketValue) || float.IsInfinity(payloadMarketValue))
            {
                marketValue = 0f;
                return false;
            }

            marketValue = payloadMarketValue * remainingCount * StoredPayloadMarketValueFactor;
            return true;
        }

        public int TakePayload(int requestedCount)
        {
            InitializeIfNeeded();
            if (requestedCount <= 0 || remainingPayloadCount <= 0)
            {
                return 0;
            }

            int takenCount = Math.Min(requestedCount, remainingPayloadCount);
            remainingPayloadCount -= takenCount;
            if (remainingPayloadCount <= 0)
            {
                remainingPayloadCount = 0;
                destroyWhenIterationCompletes = true;
            }

            return takenCount;
        }

        public void CompleteExtractionIteration()
        {
            if (!destroyWhenIterationCompletes || parent == null || parent.Destroyed)
            {
                return;
            }

            destroyWhenIterationCompletes = false;
            parent.Destroy(DestroyMode.Deconstruct);
        }

        public bool TryPrepareCleanRefunds(out List<Thing> preparedRefunds, out string error)
        {
            preparedRefunds = new List<Thing>();
            error = null;
            try
            {
                foreach (ThingDefCountClass refund in GetFrameRefunds())
                {
                    PrepareStacks(refund.thingDef, refund.count, preparedRefunds);
                }

                ThingDef payloadDef = PayloadDef;
                if (payloadDef != null && RemainingPayloadCount > 0)
                {
                    PrepareStacks(payloadDef, RemainingPayloadCount, preparedRefunds);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                DestroyPreparedRefunds(preparedRefunds);
                preparedRefunds.Clear();
                return false;
            }
        }

        public static void PlacePreparedRefunds(List<Thing> preparedRefunds, IntVec3 position, Map map)
        {
            if (preparedRefunds == null || map == null)
            {
                return;
            }

            foreach (Thing stack in preparedRefunds)
            {
                if (stack == null || stack.Destroyed || stack.Spawned)
                {
                    continue;
                }

                if (!GenPlace.TryPlaceThing(stack, position, map, ThingPlaceMode.Near))
                {
                    GenSpawn.Spawn(stack, position, map);
                }
            }
        }

        public static void DestroyPreparedRefunds(List<Thing> preparedRefunds)
        {
            if (preparedRefunds == null)
            {
                return;
            }

            foreach (Thing stack in preparedRefunds)
            {
                if (stack != null && !stack.Destroyed && !stack.Spawned)
                {
                    stack.Destroy(DestroyMode.Vanish);
                }
            }
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
            {
                remainingPayloadCount = Math.Min(Math.Max(remainingPayloadCount, 0), Math.Max(MaxPayloadCount, 0));
                return;
            }

            remainingPayloadCount = Math.Max(MaxPayloadCount, 0);
            initialized = true;
        }

        private void RemoveInvalidExtractionBills()
        {
            if (!(parent is Building_WorkTable workTable) || workTable.BillStack?.Bills == null)
            {
                return;
            }

            List<Bill> bills = workTable.BillStack.Bills;
            for (int index = bills.Count - 1; index >= 0; index--)
            {
                Bill bill = bills[index];
                if (CargoExtractionUtility.IsExtractionRecipe(bill?.recipe) &&
                    !CargoExtractionUtility.IsValidExtractionRecipeFor((Thing)workTable, bill.recipe))
                {
                    workTable.BillStack.Delete(bill);
                }
            }
        }

        private IEnumerable<ThingDefCountClass> GetFrameRefunds()
        {
            var costs = parent?.def?.costList;
            if (costs == null)
            {
                yield break;
            }

            ThingDef fixedPayloadDef = PropsExtractable.fixedPayloadDef;
            foreach (ThingDefCountClass cost in costs)
            {
                if (cost?.thingDef == null || cost.count <= 0)
                {
                    continue;
                }

                if (fixedPayloadDef != null && cost.thingDef == fixedPayloadDef)
                {
                    continue;
                }

                yield return cost;
            }
        }

        private static void PrepareStacks(ThingDef thingDef, int count, List<Thing> preparedRefunds)
        {
            if (thingDef == null || count <= 0)
            {
                return;
            }

            int stackLimit = Math.Max(thingDef.stackLimit, 1);
            int remainingCount = count;
            while (remainingCount > 0)
            {
                int stackCount = Math.Min(remainingCount, stackLimit);
                Thing stack = ThingMaker.MakeThing(thingDef);
                stack.stackCount = stackCount;
                preparedRefunds.Add(stack);
                remainingCount -= stackCount;
            }
        }
    }

    public class RecipeWorker_ExtractCargo : RecipeWorker
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            CargoExtractionUtility.TryGetExtractableComp(thing, out CompExtractableContainer extractableComp);
            return extractableComp != null && extractableComp.CanRunRecipe(recipe);
        }

        public override AcceptanceReport AvailableReport(Thing thing, BodyPartRecord part = null)
        {
            CargoExtractionUtility.TryGetExtractableComp(thing, out CompExtractableContainer extractableComp);
            if (extractableComp == null)
            {
                return false;
            }

            return extractableComp.CanRunRecipe(recipe);
        }

        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            base.Notify_IterationCompleted(billDoer, ingredients);
            CargoExtractionUtility.TryGetExtractableComp(billDoer?.CurJob?.bill?.billStack?.billGiver?.AsThing(), out CompExtractableContainer extractableComp);
            extractableComp?.CompleteExtractionIteration();
        }
    }

    public static class CargoExtractionUtility
    {
        private const string RecipePrefix = "FT_ExtractCargo_";
        private const float WorkAmountPerItem = 180f;
        private static readonly int[] BatchSizes = { 1, 25, 100 };
        private static readonly Dictionary<int, float> WorkMultipliersByBatchSize = new Dictionary<int, float>
        {
            { 1, 1f },
            { 25, 3f },
            { 100, 5f }
        };

        private static readonly Dictionary<ThingDef, List<RecipeDef>> RecipesByPayload = new Dictionary<ThingDef, List<RecipeDef>>();
        private static readonly Dictionary<RecipeDef, ExtractionRecipeData> ExtractionRecipes = new Dictionary<RecipeDef, ExtractionRecipeData>();
        private static readonly HashSet<ThingDef> ExtractableContainerDefs = new HashSet<ThingDef>();
        private static readonly Dictionary<ThingDef, Dictionary<ThingDef, List<RecipeDef>>> FilteredRecipesByContainerAndPayload = new Dictionary<ThingDef, Dictionary<ThingDef, List<RecipeDef>>>();
        private static readonly System.Reflection.FieldInfo AllRecipesCachedField = AccessTools.Field(typeof(ThingDef), "allRecipesCached");
        private static bool missingRecipeCacheFieldLogged;

        public static bool CanFilterRecipeLists => AllRecipesCachedField != null;

        public static bool TryGetExtractableComp(Thing thing, out CompExtractableContainer extractableComp)
        {
            extractableComp = null;
            if (thing?.def == null || !ExtractableContainerDefs.Contains(thing.def))
            {
                return false;
            }

            extractableComp = thing.TryGetComp<CompExtractableContainer>();
            return extractableComp != null;
        }

        public static bool IsExtractionRecipe(RecipeDef recipeDef)
        {
            return recipeDef != null && recipeDef.defName != null && recipeDef.defName.StartsWith(RecipePrefix, StringComparison.Ordinal);
        }

        public static bool IsExtractionRecipeFor(RecipeDef recipeDef, ThingDef payloadDef)
        {
            return payloadDef != null &&
                ExtractionRecipes.TryGetValue(recipeDef, out ExtractionRecipeData recipeData) &&
                !recipeData.IsLegacy &&
                recipeData.PayloadDef == payloadDef;
        }

        public static bool IsExtractionPayloadDef(ThingDef thingDef)
        {
            return thingDef != null && RecipesByPayload.ContainsKey(thingDef);
        }

        public static List<RecipeDef> RecipesFor(ThingDef payloadDef)
        {
            if (payloadDef == null)
            {
                return null;
            }

            RecipesByPayload.TryGetValue(payloadDef, out List<RecipeDef> recipeDefs);
            return recipeDefs;
        }

        public static List<RecipeDef> GetAllowedExtractionRecipes(Thing billGiverThing)
        {
            TryGetExtractableComp(billGiverThing, out CompExtractableContainer extractableComp);
            ThingDef payloadDef = extractableComp?.PayloadDef;
            return RecipesFor(payloadDef);
        }

        public static bool IsValidExtractionRecipeFor(IBillGiver billGiver, RecipeDef recipeDef)
        {
            return IsValidExtractionRecipeFor(billGiver.AsThing(), recipeDef);
        }

        public static bool IsValidExtractionRecipeFor(Thing billGiverThing, RecipeDef recipeDef)
        {
            if (!IsExtractionRecipe(recipeDef))
            {
                return false;
            }

            List<RecipeDef> allowedRecipes = GetAllowedExtractionRecipes(billGiverThing);
            return allowedRecipes != null && allowedRecipes.Contains(recipeDef);
        }

        public static int BatchCountFor(RecipeDef recipeDef)
        {
            if (recipeDef != null &&
                ExtractionRecipes.TryGetValue(recipeDef, out ExtractionRecipeData recipeData) &&
                !recipeData.IsLegacy)
            {
                return recipeData.BatchCount;
            }

            return 0;
        }

        public static List<RecipeDef> GetRecipesForBillGiver(Thing billGiverThing)
        {
            ThingDef containerDef = billGiverThing?.def;
            List<RecipeDef> originalRecipes = containerDef?.recipes;
            if (originalRecipes == null || !TryGetExtractableComp(billGiverThing, out CompExtractableContainer extractableComp))
            {
                return null;
            }

            ThingDef payloadDef = extractableComp.PayloadDef;
            if (!FilteredRecipesByContainerAndPayload.TryGetValue(containerDef, out Dictionary<ThingDef, List<RecipeDef>> byPayload))
            {
                byPayload = new Dictionary<ThingDef, List<RecipeDef>>();
                FilteredRecipesByContainerAndPayload[containerDef] = byPayload;
            }

            if (payloadDef != null && byPayload.TryGetValue(payloadDef, out List<RecipeDef> cachedRecipes))
            {
                return cachedRecipes;
            }

            List<RecipeDef> allowedRecipes = RecipesFor(payloadDef);
            var filteredRecipes = new List<RecipeDef>(originalRecipes.Count);
            foreach (RecipeDef recipeDef in originalRecipes)
            {
                if (!IsExtractionRecipe(recipeDef) || (allowedRecipes != null && allowedRecipes.Contains(recipeDef)))
                {
                    filteredRecipes.Add(recipeDef);
                }
            }

            filteredRecipes = DistinctRecipes(filteredRecipes);
            if (payloadDef != null)
            {
                byPayload[payloadDef] = filteredRecipes;
            }

            return filteredRecipes;
        }

        public static void ConfigureExtractionDefs()
        {
            try
            {
                var extractableContainers = GetExtractableContainers();
                ExtractableContainerDefs.Clear();
                ExtractableContainerDefs.UnionWith(extractableContainers);
                FilteredRecipesByContainerAndPayload.Clear();
                VerifyRecipeCacheField();
                var payloadDefs = GetPayloadDefs(extractableContainers);
                EnsureRecipes(payloadDefs);
                AttachRecipesToContainers(extractableContainers);
                ConfigureWorkGiver(extractableContainers);
            }
            catch (Exception exception)
            {
                Log.Error($"Cargo Containers Expanded: failed to configure cargo extraction.\n{exception}");
            }
        }

        private static void VerifyRecipeCacheField()
        {
            if (AllRecipesCachedField != null || missingRecipeCacheFieldLogged)
            {
                return;
            }

            missingRecipeCacheFieldLogged = true;
            Log.Error("Cargo Containers Expanded: ThingDef.allRecipesCached is unavailable. Extraction recipe menus will remain unfiltered for compatibility, while invalid bills will still be rejected when added.");
        }

        private static List<ThingDef> GetExtractableContainers()
        {
            var containers = new List<ThingDef>();
            foreach (ThingDef containerDef in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (containerDef?.GetCompProperties<CompProperties_ExtractableContainer>() != null)
                {
                    containers.Add(containerDef);
                }
            }

            return containers;
        }

        private static HashSet<ThingDef> GetPayloadDefs(List<ThingDef> containerDefs)
        {
            var payloadDefs = new HashSet<ThingDef>();
            foreach (ThingDef containerDef in containerDefs)
            {
                var props = containerDef.GetCompProperties<CompProperties_ExtractableContainer>();
                if (props?.fixedPayloadDef != null)
                {
                    payloadDefs.Add(props.fixedPayloadDef);
                    continue;
                }

                if (containerDef.stuffCategories == null)
                {
                    continue;
                }

                foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (CanBePayloadStuff(thingDef, containerDef.stuffCategories))
                    {
                        payloadDefs.Add(thingDef);
                    }
                }
            }

            return payloadDefs;
        }

        private static bool CanBePayloadStuff(ThingDef thingDef, List<StuffCategoryDef> stuffCategories)
        {
            if (thingDef?.stuffProps?.categories == null)
            {
                return false;
            }

            foreach (StuffCategoryDef category in stuffCategories)
            {
                if (category != null && thingDef.stuffProps.categories.Contains(category))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureRecipes(HashSet<ThingDef> payloadDefs)
        {
            foreach (ThingDef payloadDef in payloadDefs)
            {
                if (payloadDef == null || RecipesByPayload.ContainsKey(payloadDef))
                {
                    continue;
                }

                var recipeDefs = new List<RecipeDef>();
                foreach (int batchSize in BatchSizes)
                {
                    string recipeDefName = RecipePrefix + payloadDef.defName + "_" + batchSize;
                    RecipeDef recipeDef = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName) ?? CreateRecipe(recipeDefName, payloadDef, batchSize, false);
                    recipeDefs.Add(recipeDef);
                    ExtractionRecipes[recipeDef] = new ExtractionRecipeData(payloadDef, batchSize, false);
                }

                string legacyRecipeDefName = RecipePrefix + payloadDef.defName;
                RecipeDef legacyRecipeDef = DefDatabase<RecipeDef>.GetNamedSilentFail(legacyRecipeDefName) ?? CreateRecipe(legacyRecipeDefName, payloadDef, 1, true);
                ExtractionRecipes[legacyRecipeDef] = new ExtractionRecipeData(payloadDef, 1, true);
                RecipesByPayload[payloadDef] = recipeDefs;
            }
        }

        private static RecipeDef CreateRecipe(string recipeDefName, ThingDef payloadDef, int batchCount, bool isLegacy)
        {
            var recipeDef = new RecipeDef
            {
                defName = recipeDefName,
                label = "CCE_ExtractRecipeLabel".Translate(batchCount, payloadDef.label),
                description = "CCE_ExtractRecipeDescription".Translate(batchCount, payloadDef.label),
                workerClass = typeof(RecipeWorker_ExtractCargo),
                workerCounterClass = typeof(RecipeWorkerCounter),
                requiredGiverWorkType = WorkTypeDefOf.Crafting,
                workAmount = WorkAmountFor(batchCount),
                workSpeedStat = StatDefOf.GeneralLaborSpeed,
                workTableSpeedStat = StatDefOf.WorkTableWorkSpeedFactor,
                workSkill = SkillDefOf.Crafting,
                ingredients = new List<IngredientCount>(),
                products = new List<ThingDefCountClass>
                {
                    new ThingDefCountClass(payloadDef, batchCount)
                },
                recipeUsers = new List<ThingDef>(),
                targetCountAdjustment = batchCount
            };

            DefDatabase<RecipeDef>.Add(recipeDef);
            recipeDef.ResolveReferences();
            return recipeDef;
        }

        private static float WorkAmountFor(int batchCount)
        {
            if (WorkMultipliersByBatchSize.TryGetValue(batchCount, out float multiplier))
            {
                return WorkAmountPerItem * multiplier;
            }

            return WorkAmountPerItem;
        }

        private static void AttachRecipesToContainers(List<ThingDef> containerDefs)
        {
            foreach (ThingDef containerDef in containerDefs)
            {
                containerDef.recipes ??= new List<RecipeDef>();
                bool changed = false;
                foreach (RecipeDef recipeDef in GetRecipesForContainer(containerDef))
                {
                    if (!containerDef.recipes.Contains(recipeDef))
                    {
                        containerDef.recipes.Add(recipeDef);
                        changed = true;
                    }

                    recipeDef.recipeUsers ??= new List<ThingDef>();
                    if (!recipeDef.recipeUsers.Contains(containerDef))
                    {
                        continue;
                    }

                    recipeDef.recipeUsers.Remove(containerDef);
                }

                if (DeduplicateRecipes(containerDef.recipes))
                {
                    changed = true;
                }

                if (changed)
                {
                    ClearAllRecipesCache(containerDef);
                }
            }
        }

        public static void ClearAllRecipesCache(ThingDef thingDef)
        {
            AllRecipesCachedField?.SetValue(thingDef, null);
        }

        private static bool DeduplicateRecipes(List<RecipeDef> recipes)
        {
            if (recipes == null || recipes.Count <= 1)
            {
                return false;
            }

            bool changed = false;
            var seenRecipes = new HashSet<RecipeDef>();
            for (int index = 0; index < recipes.Count; index++)
            {
                RecipeDef recipeDef = recipes[index];
                if (recipeDef == null)
                {
                    recipes.RemoveAt(index);
                    index--;
                    changed = true;
                    continue;
                }

                if (!seenRecipes.Add(recipeDef))
                {
                    recipes.RemoveAt(index);
                    index--;
                    changed = true;
                }
            }

            return changed;
        }

        public static List<RecipeDef> DistinctRecipes(IEnumerable<RecipeDef> recipes)
        {
            var result = new List<RecipeDef>();
            if (recipes == null)
            {
                return result;
            }

            var seenRecipes = new HashSet<RecipeDef>();
            foreach (RecipeDef recipeDef in recipes)
            {
                if (recipeDef != null && seenRecipes.Add(recipeDef))
                {
                    result.Add(recipeDef);
                }
            }

            return result;
        }

        public static List<RecipeDef> GetAllRecipesCached(ThingDef thingDef)
        {
            return AllRecipesCachedField?.GetValue(thingDef) as List<RecipeDef>;
        }

        public static void SetAllRecipesCached(ThingDef thingDef, List<RecipeDef> recipes)
        {
            AllRecipesCachedField?.SetValue(thingDef, recipes);
        }

        private static void ConfigureWorkGiver(List<ThingDef> containerDefs)
        {
            WorkGiverDef workGiverDef = DefDatabase<WorkGiverDef>.GetNamedSilentFail("FT_DoBillsExtractCargoContainers");
            if (workGiverDef == null)
            {
                Log.Error("Cargo Containers Expanded: missing extraction work giver.");
                return;
            }

            workGiverDef.fixedBillGiverDefs = containerDefs;
        }

        private static IEnumerable<RecipeDef> GetRecipesForContainer(ThingDef containerDef)
        {
            var props = containerDef.GetCompProperties<CompProperties_ExtractableContainer>();
            if (props?.fixedPayloadDef != null)
            {
                List<RecipeDef> recipeDefs = RecipesFor(props.fixedPayloadDef);
                if (recipeDefs != null)
                {
                    foreach (RecipeDef recipeDef in recipeDefs)
                    {
                        yield return recipeDef;
                    }
                }

                yield break;
            }

            if (containerDef.stuffCategories == null)
            {
                yield break;
            }

            foreach (KeyValuePair<ThingDef, List<RecipeDef>> recipeByPayload in RecipesByPayload)
            {
                if (CanBePayloadStuff(recipeByPayload.Key, containerDef.stuffCategories))
                {
                    foreach (RecipeDef recipeDef in recipeByPayload.Value)
                    {
                        yield return recipeDef;
                    }
                }
            }
        }

        private readonly struct ExtractionRecipeData
        {
            public readonly ThingDef PayloadDef;
            public readonly int BatchCount;
            public readonly bool IsLegacy;

            public ExtractionRecipeData(ThingDef payloadDef, int batchCount, bool isLegacy)
            {
                PayloadDef = payloadDef;
                BatchCount = batchCount;
                IsLegacy = isLegacy;
            }
        }
    }

    [HarmonyPatch(typeof(GenRecipe), nameof(GenRecipe.MakeRecipeProducts))]
    public static class GenRecipe_MakeRecipeProducts_ExtractCargoPatch
    {
        public static void Postfix(RecipeDef recipeDef, IBillGiver billGiver, ref IEnumerable<Thing> __result)
        {
            if (!CargoExtractionUtility.IsExtractionRecipe(recipeDef))
            {
                return;
            }

            CargoExtractionUtility.TryGetExtractableComp(billGiver.AsThing(), out CompExtractableContainer extractableComp);
            if (extractableComp == null)
            {
                __result = Enumerable.Empty<Thing>();
                return;
            }

            int batchCount = CargoExtractionUtility.BatchCountFor(recipeDef);
            if (batchCount <= 0)
            {
                __result = Enumerable.Empty<Thing>();
                return;
            }

            __result = ClampProductsToPayload(__result, extractableComp, batchCount);
        }

        private static List<Thing> ClampProductsToPayload(IEnumerable<Thing> products, CompExtractableContainer extractableComp, int batchCount)
        {
            var clampedProducts = new List<Thing>();
            ThingDef payloadDef = extractableComp.PayloadDef;
            float rotProgressPct = extractableComp.GetCurrentRotProgressPct();
            int remainingBatchCount = batchCount;
            foreach (Thing product in products)
            {
                if (product == null)
                {
                    continue;
                }

                if (payloadDef == null || product.def != payloadDef)
                {
                    product.Destroy();
                    continue;
                }

                int requestedCount = Math.Min(product.stackCount, remainingBatchCount);
                int takenCount = extractableComp.TakePayload(requestedCount);
                if (takenCount <= 0)
                {
                    product.Destroy();
                    break;
                }

                product.stackCount = takenCount;
                ApplyRotProgress(product, rotProgressPct);
                clampedProducts.Add(product);
                remainingBatchCount -= takenCount;
                if (remainingBatchCount <= 0)
                {
                    break;
                }
            }

            return clampedProducts;
        }

        private static void ApplyRotProgress(Thing product, float rotProgressPct)
        {
            if (rotProgressPct <= 0f || product is not ThingWithComps thingWithComps)
            {
                return;
            }

            CompRottable rottableComp = thingWithComps.GetComp<CompRottable>();
            if (rottableComp == null)
            {
                return;
            }

            int ticksToRotStart = rottableComp.PropsRot?.TicksToRotStart ?? 0;
            if (ticksToRotStart <= 0)
            {
                return;
            }

            rottableComp.RotProgress = ticksToRotStart * Mathf.Clamp01(rotProgressPct);
        }
    }

    [HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue))]
    public static class StatExtension_GetStatValue_ExtractCargoPatch
    {
        public static void Postfix(Thing thing, StatDef stat, ref float __result)
        {
            if (stat != StatDefOf.MarketValue && stat != StatDefOf.MarketValueIgnoreHp)
            {
                return;
            }

            if (!CargoExtractionUtility.TryGetExtractableComp(thing, out CompExtractableContainer extractableComp))
            {
                return;
            }

            if (extractableComp != null && extractableComp.TryGetContainerMarketValue(out float marketValue))
            {
                __result = marketValue;
            }
        }
    }

    public static class CargoExtractionPowerBypass
    {
        private const string ExtractionWorkGiverDefName = "FT_DoBillsExtractCargoContainers";

        [ThreadStatic]
        private static int activeExtractionWorkGiverScopes;

        public static bool IsActive => activeExtractionWorkGiverScopes > 0;

        public static bool IsExtractableContainerWithPayload(Thing thing)
        {
            CargoExtractionUtility.TryGetExtractableComp(thing, out CompExtractableContainer extractableComp);
            return extractableComp != null && extractableComp.HasPayload;
        }

        public static bool ShouldBypassPowerFor(Thing thing)
        {
            return IsActive && IsExtractableContainerWithPayload(thing);
        }

        public static bool TryEnter(WorkGiver_DoBill workGiver)
        {
            if (workGiver?.def?.defName != ExtractionWorkGiverDefName)
            {
                return false;
            }

            activeExtractionWorkGiverScopes++;
            return true;
        }

        public static void Exit(bool entered)
        {
            if (!entered)
            {
                return;
            }

            activeExtractionWorkGiverScopes = Math.Max(activeExtractionWorkGiverScopes - 1, 0);
        }
    }

    [HarmonyPatch(typeof(CompPowerTrader), nameof(CompPowerTrader.PowerOn), MethodType.Getter)]
    public static class CompPowerTrader_PowerOn_ExtractCargoPatch
    {
        public static void Postfix(CompPowerTrader __instance, ref bool __result)
        {
            if (!__result && CargoExtractionPowerBypass.ShouldBypassPowerFor(__instance?.parent))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(WorkGiver_DoBill), nameof(WorkGiver_DoBill.JobOnThing))]
    public static class WorkGiverDoBill_JobOnThing_ExtractCargoPatch
    {
        public static void Prefix(WorkGiver_DoBill __instance, ref bool __state)
        {
            __state = CargoExtractionPowerBypass.TryEnter(__instance);
        }

        public static Exception Finalizer(bool __state, Exception __exception)
        {
            CargoExtractionPowerBypass.Exit(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(BillStack), nameof(BillStack.DoListing))]
    public static class BillStack_DoListing_ExtractCargoPatch
    {
        private static readonly System.Reflection.FieldInfo BillGiverField = AccessTools.Field(typeof(BillStack), "billGiver");

        public static void Prefix(BillStack __instance, ref Func<List<FloatMenuOption>> recipeOptionsMaker)
        {
            var billGiver = GetBillGiver(__instance);
            Thing billGiverThing = billGiver?.AsThing();
            if (!CargoExtractionUtility.CanFilterRecipeLists ||
                !CargoExtractionUtility.TryGetExtractableComp(billGiverThing, out _) ||
                recipeOptionsMaker == null)
            {
                return;
            }

            Func<List<FloatMenuOption>> originalOptionsMaker = recipeOptionsMaker;
            recipeOptionsMaker = () => MakeFilteredRecipeOptions(billGiverThing, originalOptionsMaker);
        }

        private static List<FloatMenuOption> MakeFilteredRecipeOptions(Thing billGiverThing, Func<List<FloatMenuOption>> originalOptionsMaker)
        {
            return RecipeListState.WithFilteredRecipes(billGiverThing, originalOptionsMaker);
        }

        internal static IBillGiver GetBillGiver(BillStack billStack)
            => BillGiverField?.GetValue(billStack) as IBillGiver;
    }

    [HarmonyPatch(typeof(BillStack), nameof(BillStack.AddBill))]
    public static class BillStack_AddBill_ExtractCargoPatch
    {
        private static readonly HashSet<string> LoggedRejectedBills = new HashSet<string>();

        public static bool Prefix(BillStack __instance, Bill bill)
        {
            RecipeDef recipeDef = bill?.recipe;
            if (!CargoExtractionUtility.IsExtractionRecipe(recipeDef))
            {
                return true;
            }

            var billGiver = BillStack_DoListing_ExtractCargoPatch.GetBillGiver(__instance);
            Thing billGiverThing = billGiver?.AsThing();
            if (CargoExtractionUtility.IsValidExtractionRecipeFor(billGiverThing, recipeDef))
            {
                return true;
            }

            CargoExtractionUtility.TryGetExtractableComp(billGiverThing, out CompExtractableContainer extractableComp);
            ThingDef payloadDef = extractableComp?.PayloadDef;
            string rejectionKey = $"{recipeDef?.defName ?? "null"}|{billGiverThing?.def?.defName ?? "unknown"}|{payloadDef?.defName ?? "none"}";
            if (LoggedRejectedBills.Add(rejectionKey))
            {
                Log.Warning(
                    $"Cargo Containers Expanded: rejected invalid extraction bill {recipeDef?.defName ?? "null"} " +
                    $"for {billGiverThing?.def?.defName ?? "unknown bill giver"} " +
                    $"with payload {payloadDef?.defName ?? "none"}.");
            }

            return false;
        }
    }

    public class RecipeListState
    {
        [ThreadStatic]
        private static HashSet<ThingDef> activeThingDefs;

        private readonly ThingDef thingDef;
        private readonly List<RecipeDef> recipes;
        private readonly List<RecipeDef> allRecipesCached;

        public RecipeListState(ThingDef thingDef, List<RecipeDef> recipes, List<RecipeDef> allRecipesCached)
        {
            this.thingDef = thingDef;
            this.recipes = recipes;
            this.allRecipesCached = allRecipesCached;
        }

        public static T WithFilteredRecipes<T>(Thing billGiverThing, Func<T> action)
        {
            ThingDef thingDef = billGiverThing?.def;
            List<RecipeDef> originalRecipes = thingDef?.recipes;
            if (!CargoExtractionUtility.CanFilterRecipeLists || thingDef == null || originalRecipes == null)
            {
                return action();
            }

            activeThingDefs ??= new HashSet<ThingDef>();
            if (!activeThingDefs.Add(thingDef))
            {
                return action();
            }

            try
            {
                List<RecipeDef> filteredRecipes = CargoExtractionUtility.GetRecipesForBillGiver(billGiverThing);
                if (filteredRecipes == null)
                {
                    return action();
                }

                List<RecipeDef> originalAllRecipesCached = CargoExtractionUtility.GetAllRecipesCached(thingDef);
                var state = new RecipeListState(thingDef, originalRecipes, originalAllRecipesCached);
                thingDef.recipes = filteredRecipes;
                try
                {
                    CargoExtractionUtility.SetAllRecipesCached(thingDef, filteredRecipes);
                    return action();
                }
                finally
                {
                    state.Restore();
                }
            }
            finally
            {
                activeThingDefs.Remove(thingDef);
            }
        }

        public void Restore()
        {
            if (thingDef != null)
            {
                thingDef.recipes = recipes;
                CargoExtractionUtility.SetAllRecipesCached(thingDef, allRecipesCached);
            }
        }
    }

    public static class BillGiverExtensions
    {
        public static Thing AsThing(this IBillGiver billGiver)
        {
            return billGiver as Thing;
        }
    }
}

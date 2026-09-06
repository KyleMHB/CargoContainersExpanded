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
            PayloadAccount account = OpenPayloadAccount();
            ApplyPayloadSnapshot(account.Snapshot);
            return account.TryGetStoredMarketValue(out marketValue);
        }

        public int TakePayload(int requestedCount)
        {
            PayloadAccount account = OpenPayloadAccount();
            PayloadWithdrawal withdrawal = account.Withdraw(requestedCount);
            ApplyPayloadSnapshot(account.Snapshot);
            return withdrawal.TakenCount;
        }

        public void CompleteExtractionIteration()
        {
            PayloadAccount account = OpenPayloadAccount();
            bool hostCanFinalize = parent != null && !parent.Destroyed;
            if (!account.TryConsumeFinalizationRequest(hostCanFinalize))
            {
                return;
            }

            ApplyPayloadSnapshot(account.Snapshot);
            parent.Destroy(DestroyMode.Deconstruct);
        }

        public bool TryPrepareCleanRefunds(out List<Thing> preparedRefunds, out string error)
        {
            preparedRefunds = new List<Thing>();
            error = null;
            try
            {
                InitializeIfNeeded();
                PayloadAccount account = OpenPayloadAccount();
                PrepareRefundPlan(account.PlanRefunds(GetFrameRefundFacts().ToList()), preparedRefunds);

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
            PayloadAccount account = OpenPayloadAccount();
            ApplyPayloadSnapshot(account.Snapshot);
        }

        private PayloadAccount OpenPayloadAccount()
        {
            ThingDef payloadDef = PayloadDef;
            return PayloadAccount.Open(
                new PayloadProfile(
                    payloadDef?.defName,
                    MaxPayloadCount,
                    payloadDef?.BaseMarketValue ?? 0f,
                    PropsExtractable?.fixedPayloadDef?.defName,
                    payloadDef?.stackLimit ?? 1),
                new PayloadSaveState(
                    initialized,
                    destroyWhenIterationCompletes,
                    remainingPayloadCount));
        }

        private void ApplyPayloadSnapshot(PayloadSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            initialized = snapshot.Initialized;
            destroyWhenIterationCompletes = snapshot.DestroyWhenIterationCompletes;
            remainingPayloadCount = snapshot.RemainingPayloadCount;
        }

        internal PayloadAccount OpenPayloadAccountForHost()
        {
            return OpenPayloadAccount();
        }

        internal void ApplyPayloadSnapshotForHost(PayloadSnapshot snapshot)
        {
            ApplyPayloadSnapshot(snapshot);
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

        private IEnumerable<RefundIngredientFacts> GetFrameRefundFacts()
        {
            var costs = parent?.def?.costList;
            if (costs == null)
            {
                yield break;
            }

            foreach (ThingDefCountClass cost in costs)
            {
                if (cost?.thingDef == null)
                {
                    continue;
                }

                yield return new RefundIngredientFacts(
                    cost.thingDef.defName,
                    cost.count,
                    cost.thingDef.stackLimit);
            }
        }

        private static void PrepareRefundPlan(RefundPlan plan, List<Thing> preparedRefunds)
        {
            string error;
            if (!PayloadRefundMaterialization.TryPrepare(
                plan,
                MaterializeRefundStack,
                DestroyPreparedRefund,
                preparedRefunds,
                out error))
            {
                throw new InvalidOperationException(error);
            }
        }

        private static Thing MaterializeRefundStack(RefundStackPlan entry)
        {
            ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(entry.DefName);
            if (thingDef == null)
            {
                throw new InvalidOperationException(
                    "Cargo Containers Expanded: refund Def was not found: " + entry.DefName);
            }

            Thing stack = ThingMaker.MakeThing(thingDef);
            if (stack == null)
            {
                throw new InvalidOperationException(
                    "Cargo Containers Expanded: refund Thing could not be created: " + entry.DefName);
            }

            stack.stackCount = entry.Count;
            return stack;
        }

        private static void DestroyPreparedRefund(Thing stack)
        {
            if (stack != null && !stack.Destroyed && !stack.Spawned)
            {
                stack.Destroy(DestroyMode.Vanish);
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

    // The catalog consumes immutable Def-name facts. This adapter is the only place where those
    // facts are read from RimWorld's mutable DefDatabase objects.
    internal readonly struct ExtractionRecipeData
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

    internal sealed class RimWorldExtractionRecipeCatalogAdapter
    {
        private readonly Dictionary<ThingDef, List<RecipeDef>> recipesByPayload = new Dictionary<ThingDef, List<RecipeDef>>();
        private readonly Dictionary<RecipeDef, ExtractionRecipeData> extractionRecipes = new Dictionary<RecipeDef, ExtractionRecipeData>();
        private readonly Dictionary<string, RecipeDef> recipeDefsByName = new Dictionary<string, RecipeDef>(StringComparer.Ordinal);
        private readonly ExtractionRecipeMappingRegistry recipeMappings = new ExtractionRecipeMappingRegistry();
        private readonly System.Reflection.FieldInfo allRecipesCachedField = RimWorldRecipeListHost.AllRecipesCachedField;
        private bool missingRecipeCacheFieldLogged;

        public bool CanFilterRecipeLists => RimWorldRecipeListHost.CanFilterRecipeLists;

        public ExtractionRecipeCatalog BuildCatalog(
            out List<ThingDef> extractableContainers,
            out Dictionary<string, ThingDef> payloadDefsByName)
        {
            var allThingDefs = new List<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading);
            extractableContainers = new List<ThingDef>();
            foreach (ThingDef thingDef in allThingDefs)
            {
                if (thingDef?.GetCompProperties<CompProperties_ExtractableContainer>() != null)
                {
                    extractableContainers.Add(thingDef);
                }
            }

            var containerFacts = new List<ExtractionContainerFacts>();
            foreach (ThingDef containerDef in extractableContainers)
            {
                CompProperties_ExtractableContainer props = containerDef.GetCompProperties<CompProperties_ExtractableContainer>();
                containerFacts.Add(new ExtractionContainerFacts(
                    containerDef.defName,
                    props?.fixedPayloadDef?.defName,
                    CategoryNames(containerDef.stuffCategories)));
            }

            var fixedPayloadNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExtractionContainerFacts container in containerFacts)
            {
                if (!string.IsNullOrEmpty(container.FixedPayloadDefName))
                {
                    fixedPayloadNames.Add(container.FixedPayloadDefName);
                }
            }

            var payloadFacts = new List<ExtractionPayloadFacts>();
            payloadDefsByName = new Dictionary<string, ThingDef>(StringComparer.Ordinal);
            foreach (ThingDef payloadDef in allThingDefs)
            {
                if (payloadDef == null || string.IsNullOrEmpty(payloadDef.defName))
                {
                    continue;
                }

                bool isFixedPayload = fixedPayloadNames.Contains(payloadDef.defName);
                bool isStuffPayload = false;
                foreach (ExtractionContainerFacts container in containerFacts)
                {
                    if (string.IsNullOrEmpty(container.FixedPayloadDefName) &&
                        SharesCategory(payloadDef, container.StuffCategoryDefNames))
                    {
                        isStuffPayload = true;
                        break;
                    }
                }

                if (!isFixedPayload && !isStuffPayload)
                {
                    continue;
                }

                payloadDefsByName[payloadDef.defName] = payloadDef;
                payloadFacts.Add(new ExtractionPayloadFacts(
                    payloadDef.defName,
                    CategoryNames(payloadDef.stuffProps?.categories)));
            }

            var existingRecipeNames = new List<string>();
            foreach (RecipeDef recipeDef in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                existingRecipeNames.Add(recipeDef?.defName);
            }

            return ExtractionRecipeCatalog.Build(payloadFacts, containerFacts, existingRecipeNames);
        }

        public void Apply(
            ExtractionRecipeCatalog catalog,
            IReadOnlyList<ThingDef> extractableContainers,
            IReadOnlyDictionary<string, ThingDef> payloadDefsByName)
        {
            recipesByPayload.Clear();
            extractionRecipes.Clear();
            recipeDefsByName.Clear();
            recipeMappings.Clear();
            VerifyRecipeCacheField();
            EnsureRecipes(catalog, payloadDefsByName);
            AttachRecipesToContainers(catalog, extractableContainers);
            RegisterLegacyMappings(payloadDefsByName);
            ConfigureWorkGiver(extractableContainers);
        }

        public bool TryGetRecipeData(RecipeDef recipeDef, out ExtractionRecipeData recipeData)
        {
            return extractionRecipes.TryGetValue(recipeDef, out recipeData);
        }

        public List<RecipeDef> RecipesFor(ThingDef payloadDef, ExtractionRecipeCatalog catalog)
        {
            if (payloadDef == null || catalog == null)
            {
                return null;
            }

            IReadOnlyList<string> recipeNames = catalog.RecipeNamesForPayload(payloadDef.defName);
            if (recipeNames == null)
            {
                return null;
            }

            var recipeDefs = new List<RecipeDef>();
            foreach (string recipeName in recipeNames)
            {
                RecipeDef recipeDef = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeName);
                if (recipeDef != null)
                {
                    recipeDefs.Add(recipeDef);
                }
            }

            return recipeDefs;
        }

        public void ClearAllRecipesCache(ThingDef thingDef)
        {
            allRecipesCachedField?.SetValue(thingDef, null);
        }

        public List<RecipeDef> GetAllRecipesCached(ThingDef thingDef)
        {
            return allRecipesCachedField?.GetValue(thingDef) as List<RecipeDef>;
        }

        public void SetAllRecipesCached(ThingDef thingDef, List<RecipeDef> recipes)
        {
            allRecipesCachedField?.SetValue(thingDef, recipes);
        }

        private void VerifyRecipeCacheField()
        {
            if (allRecipesCachedField != null || missingRecipeCacheFieldLogged)
            {
                return;
            }

            missingRecipeCacheFieldLogged = true;
            Log.Error("Cargo Containers Expanded: ThingDef.allRecipesCached is unavailable. Extraction recipe menus will remain unfiltered for compatibility, while invalid bills will still be rejected when added.");
        }

        private void EnsureRecipes(
            ExtractionRecipeCatalog catalog,
            IReadOnlyDictionary<string, ThingDef> payloadDefsByName)
        {
            foreach (ExtractionRecipeSpec spec in catalog.RecipeSpecs)
            {
                if (spec == null || !payloadDefsByName.TryGetValue(spec.PayloadDefName, out ThingDef payloadDef) || payloadDef == null)
                {
                    continue;
                }

                if (!recipesByPayload.TryGetValue(payloadDef, out List<RecipeDef> recipeDefs))
                {
                    recipeDefs = new List<RecipeDef>();
                    recipesByPayload[payloadDef] = recipeDefs;
                }

                RecipeDef recipeDef = DefDatabase<RecipeDef>.GetNamedSilentFail(spec.RecipeDefName) ??
                    CreateRecipe(spec, payloadDef);
                if (!recipeDefs.Contains(recipeDef))
                {
                    recipeDefs.Add(recipeDef);
                }

                if (!recipeDefsByName.ContainsKey(spec.RecipeDefName))
                {
                    recipeDefsByName.Add(spec.RecipeDefName, recipeDef);
                }
            }

            foreach (KeyValuePair<string, ThingDef> payload in payloadDefsByName)
            {
                string legacyRecipeDefName = ExtractionRecipeCatalog.RecipePrefix + payload.Key;
                RecipeDef legacyRecipeDef = DefDatabase<RecipeDef>.GetNamedSilentFail(legacyRecipeDefName) ??
                    CreateRecipe(
                        new ExtractionRecipeSpec(legacyRecipeDefName, payload.Key, 1, 180f, true),
                        payload.Value);
                if (!recipeDefsByName.ContainsKey(legacyRecipeDefName))
                {
                    recipeDefsByName.Add(legacyRecipeDefName, legacyRecipeDef);
                }
            }
        }

        private void RegisterLegacyMappings(IReadOnlyDictionary<string, ThingDef> payloadDefsByName)
        {
            foreach (KeyValuePair<string, ThingDef> payload in payloadDefsByName)
            {
                string legacyRecipeDefName = ExtractionRecipeCatalog.RecipePrefix + payload.Key;
                if (!recipeDefsByName.TryGetValue(legacyRecipeDefName, out RecipeDef legacyRecipeDef))
                {
                    continue;
                }

                var legacySpec = new ExtractionRecipeSpec(
                    legacyRecipeDefName,
                    payload.Key,
                    1,
                    180f,
                    true);
                if (recipeMappings.TryRegister(legacySpec))
                {
                    extractionRecipes[legacyRecipeDef] = new ExtractionRecipeData(payload.Value, 1, true);
                }
            }
        }

        private RecipeDef CreateRecipe(ExtractionRecipeSpec spec, ThingDef payloadDef)
        {
            var recipeDef = new RecipeDef
            {
                defName = spec.RecipeDefName,
                label = "CCE_ExtractRecipeLabel".Translate(spec.BatchCount, payloadDef.label),
                description = "CCE_ExtractRecipeDescription".Translate(spec.BatchCount, payloadDef.label),
                workerClass = typeof(RecipeWorker_ExtractCargo),
                workerCounterClass = typeof(RecipeWorkerCounter),
                requiredGiverWorkType = WorkTypeDefOf.Crafting,
                workAmount = spec.WorkAmount,
                workSpeedStat = StatDefOf.GeneralLaborSpeed,
                workTableSpeedStat = StatDefOf.WorkTableWorkSpeedFactor,
                workSkill = SkillDefOf.Crafting,
                ingredients = new List<IngredientCount>(),
                products = new List<ThingDefCountClass>
                {
                    new ThingDefCountClass(payloadDef, spec.BatchCount)
                },
                recipeUsers = new List<ThingDef>(),
                targetCountAdjustment = spec.BatchCount
            };

            DefDatabase<RecipeDef>.Add(recipeDef);
            recipeDef.ResolveReferences();
            return recipeDef;
        }

        private void AttachRecipesToContainers(
            ExtractionRecipeCatalog catalog,
            IReadOnlyList<ThingDef> containerDefs)
        {
            foreach (ThingDef containerDef in containerDefs)
            {
                containerDef.recipes ??= new List<RecipeDef>();
                var existingRecipeNames = new List<string>();
                foreach (RecipeDef recipeDef in containerDef.recipes)
                {
                    existingRecipeNames.Add(recipeDef?.defName);
                }

                ExtractionCatalogContainerApplication application = ExtractionRecipeCatalogHostExecutor.Apply(
                    catalog,
                    containerDef.defName,
                    existingRecipeNames);
                bool changed = application.Changed;
                if (changed)
                {
                    containerDef.recipes.Clear();
                    foreach (string recipeName in application.RecipeDefNames)
                    {
                        RecipeDef recipeDef = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeName);
                        if (recipeDef != null)
                        {
                            containerDef.recipes.Add(recipeDef);
                        }
                    }
                }

                ContainerRecipePlan plan = catalog.ContainerPlans.FirstOrDefault(candidate =>
                    string.Equals(candidate.ContainerDefName, containerDef.defName, StringComparison.Ordinal));
                if (plan != null)
                {
                    foreach (string recipeName in plan.RecipeDefNames)
                    {
                        RecipeDef recipeDef = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeName);
                        if (recipeDef == null)
                        {
                            continue;
                        }

                        ExtractionRecipeSpec spec = application.EffectiveRecipeSpecs.FirstOrDefault(candidate =>
                            string.Equals(candidate.RecipeDefName, recipeName, StringComparison.Ordinal));
                        if (spec != null && recipeMappings.TryRegister(spec))
                        {
                            extractionRecipes[recipeDef] = new ExtractionRecipeData(
                                DefDatabase<ThingDef>.GetNamedSilentFail(spec.PayloadDefName),
                                spec.BatchCount,
                                spec.IsLegacy);
                        }

                        recipeDef.recipeUsers ??= new List<ThingDef>();
                        if (recipeDef.recipeUsers.Contains(containerDef))
                        {
                            recipeDef.recipeUsers.Remove(containerDef);
                        }
                    }
                }

                if (changed)
                {
                    ClearAllRecipesCache(containerDef);
                }
            }
        }

        private static void ConfigureWorkGiver(IReadOnlyList<ThingDef> containerDefs)
        {
            WorkGiverDef workGiverDef = DefDatabase<WorkGiverDef>.GetNamedSilentFail("FT_DoBillsExtractCargoContainers");
            if (workGiverDef == null)
            {
                Log.Error("Cargo Containers Expanded: missing extraction work giver.");
                return;
            }

            workGiverDef.fixedBillGiverDefs = new List<ThingDef>(containerDefs);
        }

        private static IEnumerable<string> CategoryNames(IEnumerable<StuffCategoryDef> categories)
        {
            if (categories == null)
            {
                yield break;
            }

            foreach (StuffCategoryDef category in categories)
            {
                yield return category?.defName;
            }
        }

        private static bool SharesCategory(ThingDef payloadDef, IReadOnlyList<string> containerCategories)
        {
            if (payloadDef?.stuffProps?.categories == null || containerCategories == null)
            {
                return false;
            }

            foreach (StuffCategoryDef payloadCategory in payloadDef.stuffProps.categories)
            {
                foreach (string containerCategory in containerCategories)
                {
                    if (string.Equals(payloadCategory?.defName, containerCategory, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public static class CargoExtractionUtility
    {
        private static readonly RimWorldExtractionRecipeCatalogAdapter catalogAdapter = new RimWorldExtractionRecipeCatalogAdapter();
        private static readonly HashSet<ThingDef> ExtractableContainerDefs = new HashSet<ThingDef>();
        private static readonly Dictionary<ThingDef, Dictionary<ThingDef, List<RecipeDef>>> FilteredRecipesByContainerAndPayload = new Dictionary<ThingDef, Dictionary<ThingDef, List<RecipeDef>>>();
        private static ExtractionRecipeCatalog extractionRecipeCatalog;

        public static bool CanFilterRecipeLists => catalogAdapter.CanFilterRecipeLists;

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
            return recipeDef != null && ExtractionRecipeCatalog.IsExtractionRecipeDefName(recipeDef.defName);
        }

        public static bool IsExtractionRecipeFor(RecipeDef recipeDef, ThingDef payloadDef)
        {
            return payloadDef != null &&
                recipeDef != null &&
                extractionRecipeCatalog != null &&
                extractionRecipeCatalog.Resolve(recipeDef.defName, payloadDef.defName).IsValid &&
                catalogAdapter.TryGetRecipeData(recipeDef, out _);
        }

        public static bool IsExtractionPayloadDef(ThingDef thingDef)
        {
            return thingDef != null && extractionRecipeCatalog?.IsKnownPayload(thingDef.defName) == true;
        }

        public static List<RecipeDef> RecipesFor(ThingDef payloadDef)
        {
            if (payloadDef == null)
            {
                return null;
            }

            return catalogAdapter.RecipesFor(payloadDef, extractionRecipeCatalog);
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
            if (recipeDef != null && catalogAdapter.TryGetRecipeData(recipeDef, out ExtractionRecipeData recipeData))
            {
                ExtractionRecipeResolution resolution = extractionRecipeCatalog?.Resolve(
                    recipeDef.defName,
                    recipeData.PayloadDef?.defName);
                return resolution != null && resolution.IsValid ? resolution.BatchCount : 0;
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
                return new List<RecipeDef>(cachedRecipes);
            }

            if (extractionRecipeCatalog == null)
            {
                // Bootstrap publishes the catalog only after a complete host application. If publication
                // has not happened yet, keep ordinary bills visible and avoid silently emptying the menu.
                return DistinctRecipes(originalRecipes.Where(recipeDef => !IsExtractionRecipe(recipeDef)));
            }

            IReadOnlyList<string> filteredRecipeNames = extractionRecipeCatalog.FilterRecipeDefNames(
                payloadDef?.defName,
                GetRecipeDefNames(originalRecipes));
            var filteredRecipes = new List<RecipeDef>();
            if (filteredRecipeNames != null)
            {
                foreach (string recipeName in filteredRecipeNames)
                {
                    RecipeDef recipeDef = originalRecipes.FirstOrDefault(candidate => candidate?.defName == recipeName);
                    if (recipeDef != null)
                    {
                        filteredRecipes.Add(recipeDef);
                    }
                }
            }

            if (payloadDef != null)
            {
                byPayload[payloadDef] = filteredRecipes;
            }

            return filteredRecipes;
        }

        private static IEnumerable<string> GetRecipeDefNames(IEnumerable<RecipeDef> recipeDefs)
        {
            if (recipeDefs == null)
            {
                yield break;
            }

            foreach (RecipeDef recipeDef in recipeDefs)
            {
                yield return recipeDef?.defName;
            }
        }

        public static void ConfigureExtractionDefs()
        {
            try
            {
                ExtractionRecipeCatalog catalog = catalogAdapter.BuildCatalog(
                    out List<ThingDef> extractableContainers,
                    out Dictionary<string, ThingDef> payloadDefsByName);
                FilteredRecipesByContainerAndPayload.Clear();
                catalogAdapter.Apply(catalog, extractableContainers, payloadDefsByName);
                ExtractableContainerDefs.Clear();
                ExtractableContainerDefs.UnionWith(extractableContainers);
                extractionRecipeCatalog = catalog;
            }
            catch (Exception exception)
            {
                Log.Error($"Cargo Containers Expanded: failed to configure cargo extraction.\n{exception}");
            }
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
            return catalogAdapter.GetAllRecipesCached(thingDef);
        }

        public static void SetAllRecipesCached(ThingDef thingDef, List<RecipeDef> recipes)
        {
            catalogAdapter.SetAllRecipesCached(thingDef, recipes);
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
            ThingDef payloadDef = extractableComp.PayloadDef;
            float rotProgressPct = extractableComp.GetCurrentRotProgressPct();
            PayloadAccount account = extractableComp.OpenPayloadAccountForHost();
            IReadOnlyList<Thing> clampedProducts = PayloadProductClamper.Clamp(
                account,
                payloadDef?.defName,
                products,
                batchCount,
                product => product?.def?.defName,
                product => product?.stackCount ?? 0,
                (product, count) => product.stackCount = count,
                product => product.Destroy(),
                product => ApplyRotProgress(product, rotProgressPct));
            extractableComp.ApplyPayloadSnapshotForHost(account.Snapshot);
            return clampedProducts.ToList();
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

    [HarmonyPatch(typeof(CompPowerTrader), nameof(CompPowerTrader.PowerOn), MethodType.Getter)]
    public static class CompPowerTrader_PowerOn_ExtractCargoPatch
    {
        public static void Postfix(CompPowerTrader __instance, ref bool __result)
        {
            CargoExtractionUtility.TryGetExtractableComp(__instance?.parent, out CompExtractableContainer extractableComp);
            if (!__result && ExtractionCompatibilityScopes.ShouldBypassPower(extractableComp?.HasPayload == true))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(WorkGiver_DoBill), nameof(WorkGiver_DoBill.JobOnThing))]
    public static class WorkGiverDoBill_JobOnThing_ExtractCargoPatch
    {
        internal static void Prefix(WorkGiver_DoBill __instance, ref ExtractionWorkScope __state)
        {
            __state = ExtractionCompatibilityScopes.EnterWorkGiver(__instance?.def?.defName);
        }

        internal static Exception Finalizer(ExtractionWorkScope __state, Exception __exception)
        {
            __state?.Dispose();
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
            if (!RimWorldRecipeListHost.CanFilterRecipeLists ||
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
            ThingDef thingDef = billGiverThing?.def;
            var host = new RimWorldRecipeListHost(thingDef);
            List<RecipeDef> filteredRecipes = CargoExtractionUtility.GetRecipesForBillGiver(billGiverThing);
            var filteredRecipeDefNames = new List<string>();
            if (filteredRecipes != null)
            {
                foreach (RecipeDef recipeDef in filteredRecipes)
                {
                    filteredRecipeDefNames.Add(recipeDef?.defName);
                }
            }

            return ExtractionCompatibilityScopes.WithFilteredRecipes(
                host,
                filteredRecipeDefNames,
                originalOptionsMaker);
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

    public static class BillGiverExtensions
    {
        public static Thing AsThing(this IBillGiver billGiver)
        {
            return billGiver as Thing;
        }
    }
}

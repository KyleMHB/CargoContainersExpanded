using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CargoContainersExpanded
{
    internal sealed class ExtractionPayloadFacts
    {
        public ExtractionPayloadFacts(string defName, IEnumerable<string> stuffCategoryDefNames = null)
        {
            DefName = defName;
            StuffCategoryDefNames = CopyNames(stuffCategoryDefNames);
        }

        public string DefName { get; }

        public IReadOnlyList<string> StuffCategoryDefNames { get; }

        private static IReadOnlyList<string> CopyNames(IEnumerable<string> names)
        {
            var copy = new List<string>();
            if (names != null)
            {
                foreach (string name in names)
                {
                    copy.Add(name);
                }
            }

            return new ReadOnlyCollection<string>(copy);
        }
    }

    internal sealed class ExtractionContainerFacts
    {
        public ExtractionContainerFacts(
            string defName,
            string fixedPayloadDefName = null,
            IEnumerable<string> stuffCategoryDefNames = null)
        {
            DefName = defName;
            FixedPayloadDefName = fixedPayloadDefName;
            StuffCategoryDefNames = CopyNames(stuffCategoryDefNames);
        }

        public string DefName { get; }

        public string FixedPayloadDefName { get; }

        public IReadOnlyList<string> StuffCategoryDefNames { get; }

        private static IReadOnlyList<string> CopyNames(IEnumerable<string> names)
        {
            var copy = new List<string>();
            if (names != null)
            {
                foreach (string name in names)
                {
                    copy.Add(name);
                }
            }

            return new ReadOnlyCollection<string>(copy);
        }
    }

    internal sealed class ExtractionRecipeSpec
    {
        public ExtractionRecipeSpec(
            string recipeDefName,
            string payloadDefName,
            int batchCount,
            float workAmount,
            bool isLegacy = false,
            bool alreadyExists = false)
        {
            RecipeDefName = recipeDefName;
            PayloadDefName = payloadDefName;
            BatchCount = batchCount;
            WorkAmount = workAmount;
            IsLegacy = isLegacy;
            AlreadyExists = alreadyExists;
        }

        public string RecipeDefName { get; }

        public string PayloadDefName { get; }

        public int BatchCount { get; }

        public float WorkAmount { get; }

        public bool IsLegacy { get; }

        public bool AlreadyExists { get; }
    }

    internal sealed class ContainerRecipePlan
    {
        public ContainerRecipePlan(string containerDefName, IReadOnlyList<string> recipeDefNames)
        {
            ContainerDefName = containerDefName;
            RecipeDefNames = new ReadOnlyCollection<string>(
                new List<string>(recipeDefNames ?? Array.Empty<string>()));
        }

        public string ContainerDefName { get; }

        public IReadOnlyList<string> RecipeDefNames { get; }
    }

    internal sealed class CatalogDiagnostic
    {
        public CatalogDiagnostic(string code, string message)
        {
            Code = code;
            Message = message;
        }

        public string Code { get; }

        public string Message { get; }
    }

    internal enum ExtractionRecipeResolutionKind
    {
        NotExtraction,
        Valid,
        Legacy,
        Unknown,
        WrongPayload
    }

    internal sealed class ExtractionRecipeResolution
    {
        private ExtractionRecipeResolution(
            string recipeDefName,
            string payloadDefName,
            ExtractionRecipeResolutionKind kind,
            int batchCount,
            float workAmount)
        {
            RecipeDefName = recipeDefName;
            PayloadDefName = payloadDefName;
            Kind = kind;
            BatchCount = batchCount;
            WorkAmount = workAmount;
        }

        public string RecipeDefName { get; }

        public string PayloadDefName { get; }

        public ExtractionRecipeResolutionKind Kind { get; }

        public int BatchCount { get; }

        public float WorkAmount { get; }

        public bool IsRecognized => Kind != ExtractionRecipeResolutionKind.NotExtraction;

        public bool IsValid => Kind == ExtractionRecipeResolutionKind.Valid;

        public bool CanRun => IsValid;

        public bool IsLegacy => Kind == ExtractionRecipeResolutionKind.Legacy;

        public bool IsUnknown => Kind == ExtractionRecipeResolutionKind.Unknown;

        public bool IsWrongPayload => Kind == ExtractionRecipeResolutionKind.WrongPayload;

        public static ExtractionRecipeResolution NotExtraction(string recipeDefName)
        {
            return new ExtractionRecipeResolution(
                recipeDefName,
                null,
                ExtractionRecipeResolutionKind.NotExtraction,
                0,
                0f);
        }

        public static ExtractionRecipeResolution For(
            string recipeDefName,
            string payloadDefName,
            ExtractionRecipeResolutionKind kind,
            int batchCount = 0,
            float workAmount = 0f)
        {
            return new ExtractionRecipeResolution(
                recipeDefName,
                payloadDefName,
                kind,
                batchCount,
                workAmount);
        }
    }

    internal sealed class ExtractionRecipeCatalog
    {
        internal const string RecipePrefix = "FT_ExtractCargo_";
        internal const string DuplicatePayloadCode = "duplicate-payload";
        internal const string DuplicateContainerCode = "duplicate-container";
        internal const string DuplicateRecipeCode = "duplicate-recipe";
        internal const string MalformedPayloadCode = "malformed-payload";
        internal const string MalformedContainerCode = "malformed-container";

        private static readonly int[] BatchSizes = { 1, 25, 100 };
        private static readonly Dictionary<int, float> WorkAmounts = new Dictionary<int, float>
        {
            { 1, 180f },
            { 25, 540f },
            { 100, 900f }
        };

        private readonly IReadOnlyList<ExtractionRecipeSpec> recipeSpecs;
        private readonly IReadOnlyList<ContainerRecipePlan> containerPlans;
        private readonly IReadOnlyList<CatalogDiagnostic> diagnostics;
        private readonly IReadOnlyDictionary<string, ExtractionRecipeSpec> specsByRecipeName;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> recipeNamesByPayload;

        private ExtractionRecipeCatalog(
            IReadOnlyList<ExtractionRecipeSpec> recipeSpecs,
            IReadOnlyList<ContainerRecipePlan> containerPlans,
            IReadOnlyList<CatalogDiagnostic> diagnostics,
            IReadOnlyDictionary<string, ExtractionRecipeSpec> specsByRecipeName,
            IReadOnlyDictionary<string, IReadOnlyList<string>> recipeNamesByPayload)
        {
            this.recipeSpecs = recipeSpecs;
            this.containerPlans = containerPlans;
            this.diagnostics = diagnostics;
            this.specsByRecipeName = specsByRecipeName;
            this.recipeNamesByPayload = recipeNamesByPayload;
        }

        public IReadOnlyList<ExtractionRecipeSpec> RecipeSpecs => recipeSpecs;

        public IReadOnlyList<ContainerRecipePlan> ContainerPlans => containerPlans;

        public IReadOnlyList<CatalogDiagnostic> Diagnostics => diagnostics;

        public static ExtractionRecipeCatalog Build(
            IReadOnlyList<ExtractionPayloadFacts> payloads,
            IReadOnlyList<ExtractionContainerFacts> containers,
            IReadOnlyCollection<string> existingRecipeDefNames)
        {
            var specs = new List<ExtractionRecipeSpec>();
            var plans = new List<ContainerRecipePlan>();
            var diagnostics = new List<CatalogDiagnostic>();
            var payloadByName = new Dictionary<string, ExtractionPayloadFacts>(StringComparer.Ordinal);
            var containerByName = new Dictionary<string, ExtractionContainerFacts>(StringComparer.Ordinal);
            var payloadFactsInOrder = new List<ExtractionPayloadFacts>();
            var containerFactsInOrder = new List<ExtractionContainerFacts>();
            var existingNames = new HashSet<string>(StringComparer.Ordinal);
            if (existingRecipeDefNames != null)
            {
                foreach (string existingName in existingRecipeDefNames)
                {
                    if (!string.IsNullOrEmpty(existingName))
                    {
                        existingNames.Add(existingName);
                    }
                }
            }

            AddPayloadFacts(payloads, payloadByName, payloadFactsInOrder, diagnostics);
            AddContainerFacts(containers, containerByName, containerFactsInOrder, diagnostics);

            var specByName = new Dictionary<string, ExtractionRecipeSpec>(StringComparer.Ordinal);
            var namesByPayload = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (ExtractionPayloadFacts payload in payloadFactsInOrder)
            {
                var payloadRecipeNames = new List<string>();
                foreach (int batchSize in BatchSizes)
                {
                    string recipeDefName = RecipePrefix + payload.DefName + "_" + batchSize;
                    if (specByName.ContainsKey(recipeDefName))
                    {
                        diagnostics.Add(new CatalogDiagnostic(
                            DuplicateRecipeCode,
                            "Duplicate extraction recipe " + recipeDefName + "."));
                        continue;
                    }

                    var spec = new ExtractionRecipeSpec(
                        recipeDefName,
                        payload.DefName,
                        batchSize,
                        WorkAmounts[batchSize],
                        false,
                        existingNames.Contains(recipeDefName));
                    specs.Add(spec);
                    specByName.Add(recipeDefName, spec);
                    payloadRecipeNames.Add(recipeDefName);
                }

                namesByPayload.Add(
                    payload.DefName,
                    new ReadOnlyCollection<string>(payloadRecipeNames));
            }

            foreach (ExtractionContainerFacts container in containerFactsInOrder)
            {
                var recipeNames = new List<string>();
                foreach (ExtractionPayloadFacts payload in payloadFactsInOrder)
                {
                    bool matches = string.IsNullOrEmpty(container.FixedPayloadDefName)
                        ? SharesCategory(payload.StuffCategoryDefNames, container.StuffCategoryDefNames)
                        : string.Equals(container.FixedPayloadDefName, payload.DefName, StringComparison.Ordinal);
                    if (!matches || !namesByPayload.TryGetValue(payload.DefName, out IReadOnlyList<string> names))
                    {
                        continue;
                    }

                    foreach (string recipeName in names)
                    {
                        AddUnique(recipeNames, recipeName, diagnostics);
                    }
                }

                plans.Add(new ContainerRecipePlan(container.DefName, recipeNames));
            }

            return new ExtractionRecipeCatalog(
                new ReadOnlyCollection<ExtractionRecipeSpec>(specs),
                new ReadOnlyCollection<ContainerRecipePlan>(plans),
                new ReadOnlyCollection<CatalogDiagnostic>(diagnostics),
                new ReadOnlyDictionary<string, ExtractionRecipeSpec>(specByName),
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(namesByPayload));
        }

        public ExtractionRecipeResolution Resolve(string recipeDefName, string payloadDefName)
        {
            if (string.IsNullOrEmpty(recipeDefName) ||
                !recipeDefName.StartsWith(RecipePrefix, StringComparison.Ordinal))
            {
                return ExtractionRecipeResolution.NotExtraction(recipeDefName);
            }

            if (specsByRecipeName.TryGetValue(recipeDefName, out ExtractionRecipeSpec spec))
            {
                if (!string.Equals(spec.PayloadDefName, payloadDefName, StringComparison.Ordinal))
                {
                    return ExtractionRecipeResolution.For(
                        recipeDefName,
                        spec.PayloadDefName,
                        ExtractionRecipeResolutionKind.WrongPayload,
                        spec.BatchCount,
                        spec.WorkAmount);
                }

                return ExtractionRecipeResolution.For(
                    recipeDefName,
                    spec.PayloadDefName,
                    ExtractionRecipeResolutionKind.Valid,
                    spec.BatchCount,
                    spec.WorkAmount);
            }

            string encodedPayloadName = recipeDefName.Substring(RecipePrefix.Length);
            if (string.IsNullOrEmpty(encodedPayloadName))
            {
                return ExtractionRecipeResolution.For(
                    recipeDefName,
                    null,
                    ExtractionRecipeResolutionKind.Unknown);
            }

            // Check the complete encoded name before looking for a batch suffix. Def names may contain
            // underscores, so splitting at the last underscore would misclassify legacy recipes.
            if (IsKnownPayload(encodedPayloadName))
            {
                return ExtractionRecipeResolution.For(
                    recipeDefName,
                    encodedPayloadName,
                    ExtractionRecipeResolutionKind.Legacy,
                    1,
                    180f);
            }

            int suffixIndex = encodedPayloadName.LastIndexOf('_');
            if (suffixIndex < 0)
            {
                if (IsKnownPayload(encodedPayloadName))
                {
                    return ExtractionRecipeResolution.For(
                        recipeDefName,
                        encodedPayloadName,
                        ExtractionRecipeResolutionKind.Legacy,
                        1,
                        180f);
                }

                return ExtractionRecipeResolution.For(
                    recipeDefName,
                    encodedPayloadName,
                    ExtractionRecipeResolutionKind.Unknown);
            }

            string suffix = encodedPayloadName.Substring(suffixIndex + 1);
            string payloadName = encodedPayloadName.Substring(0, suffixIndex);
            if (IsKnownPayload(payloadName) &&
                int.TryParse(suffix, out int batchSize) &&
                WorkAmounts.ContainsKey(batchSize))
            {
                if (!string.Equals(payloadName, payloadDefName, StringComparison.Ordinal))
                {
                    return ExtractionRecipeResolution.For(
                        recipeDefName,
                        payloadName,
                        ExtractionRecipeResolutionKind.WrongPayload,
                        batchSize,
                        WorkAmounts[batchSize]);
                }

                return ExtractionRecipeResolution.For(
                    recipeDefName,
                    payloadName,
                    ExtractionRecipeResolutionKind.Valid,
                    batchSize,
                    WorkAmounts[batchSize]);
            }

            return ExtractionRecipeResolution.For(
                recipeDefName,
                payloadName,
                ExtractionRecipeResolutionKind.Unknown);
        }

        public IReadOnlyList<string> FilterRecipeDefNames(
            string payloadDefName,
            IEnumerable<string> recipeDefNames)
        {
            var filtered = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (recipeDefNames == null)
            {
                return new ReadOnlyCollection<string>(filtered);
            }

            foreach (string recipeDefName in recipeDefNames)
            {
                if (string.IsNullOrEmpty(recipeDefName) || !seen.Add(recipeDefName))
                {
                    continue;
                }

                ExtractionRecipeResolution resolution = Resolve(recipeDefName, payloadDefName);
                if (!resolution.IsRecognized || resolution.IsValid)
                {
                    filtered.Add(recipeDefName);
                }
            }

            return new ReadOnlyCollection<string>(filtered);
        }

        public bool IsKnownPayload(string payloadDefName)
        {
            return !string.IsNullOrEmpty(payloadDefName) && recipeNamesByPayload.ContainsKey(payloadDefName);
        }

        public IReadOnlyList<string> RecipeNamesForPayload(string payloadDefName)
        {
            if (payloadDefName == null || !recipeNamesByPayload.TryGetValue(payloadDefName, out IReadOnlyList<string> names))
            {
                return null;
            }

            return names;
        }

        public static bool IsExtractionRecipeDefName(string recipeDefName)
        {
            return !string.IsNullOrEmpty(recipeDefName) &&
                recipeDefName.StartsWith(RecipePrefix, StringComparison.Ordinal);
        }

        private static void AddPayloadFacts(
            IReadOnlyList<ExtractionPayloadFacts> payloads,
            IDictionary<string, ExtractionPayloadFacts> payloadByName,
            ICollection<ExtractionPayloadFacts> payloadFactsInOrder,
            ICollection<CatalogDiagnostic> diagnostics)
        {
            if (payloads == null)
            {
                return;
            }

            foreach (ExtractionPayloadFacts payload in payloads)
            {
                if (payload == null || string.IsNullOrWhiteSpace(payload.DefName))
                {
                    diagnostics.Add(new CatalogDiagnostic(
                        MalformedPayloadCode,
                        "Extraction payload facts require a Def name."));
                    continue;
                }

                if (payloadByName.ContainsKey(payload.DefName))
                {
                    diagnostics.Add(new CatalogDiagnostic(
                        DuplicatePayloadCode,
                        "Duplicate extraction payload " + payload.DefName + ". The first fact was kept."));
                    continue;
                }

                payloadByName.Add(payload.DefName, payload);
                payloadFactsInOrder.Add(payload);
            }
        }

        private static void AddContainerFacts(
            IReadOnlyList<ExtractionContainerFacts> containers,
            IDictionary<string, ExtractionContainerFacts> containerByName,
            ICollection<ExtractionContainerFacts> containerFactsInOrder,
            ICollection<CatalogDiagnostic> diagnostics)
        {
            if (containers == null)
            {
                return;
            }

            foreach (ExtractionContainerFacts container in containers)
            {
                if (container == null || string.IsNullOrWhiteSpace(container.DefName))
                {
                    diagnostics.Add(new CatalogDiagnostic(
                        MalformedContainerCode,
                        "Extraction container facts require a Def name."));
                    continue;
                }

                if (containerByName.ContainsKey(container.DefName))
                {
                    diagnostics.Add(new CatalogDiagnostic(
                        DuplicateContainerCode,
                        "Duplicate extraction container " + container.DefName + ". The first fact was kept."));
                    continue;
                }

                containerByName.Add(container.DefName, container);
                containerFactsInOrder.Add(container);
            }
        }

        private static bool SharesCategory(
            IReadOnlyList<string> payloadCategories,
            IReadOnlyList<string> containerCategories)
        {
            if (payloadCategories == null || containerCategories == null)
            {
                return false;
            }

            foreach (string payloadCategory in payloadCategories)
            {
                if (string.IsNullOrEmpty(payloadCategory))
                {
                    continue;
                }

                foreach (string containerCategory in containerCategories)
                {
                    if (string.Equals(payloadCategory, containerCategory, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddUnique(
            ICollection<string> values,
            string value,
            ICollection<CatalogDiagnostic> diagnostics)
        {
            if (values is List<string> list && list.Contains(value))
            {
                diagnostics.Add(new CatalogDiagnostic(
                    DuplicateRecipeCode,
                    "Duplicate extraction recipe " + value + " was removed from a container plan."));
                return;
            }

            values.Add(value);
        }
    }

    internal sealed class ExtractionCatalogContainerApplication
    {
        public ExtractionCatalogContainerApplication(
            IReadOnlyList<string> recipeDefNames,
            IReadOnlyList<ExtractionRecipeSpec> effectiveRecipeSpecs,
            bool changed)
        {
            RecipeDefNames = recipeDefNames;
            EffectiveRecipeSpecs = effectiveRecipeSpecs;
            Changed = changed;
        }

        public IReadOnlyList<string> RecipeDefNames { get; }

        public IReadOnlyList<ExtractionRecipeSpec> EffectiveRecipeSpecs { get; }

        public bool Changed { get; }
    }

    internal sealed class ExtractionRecipeMappingRegistry
    {
        private readonly Dictionary<string, ExtractionRecipeSpec> mappings =
            new Dictionary<string, ExtractionRecipeSpec>(StringComparer.Ordinal);

        public bool TryRegister(ExtractionRecipeSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.RecipeDefName) || mappings.ContainsKey(spec.RecipeDefName))
            {
                return false;
            }

            mappings.Add(spec.RecipeDefName, spec);
            return true;
        }

        public void Clear()
        {
            mappings.Clear();
        }
    }

    // This host executor owns the deterministic part of applying a catalog to mutable recipe lists.
    // RimWorld adapters perform the actual DefDatabase and cache writes after receiving this result.
    internal static class ExtractionRecipeCatalogHostExecutor
    {
        public static ExtractionCatalogContainerApplication Apply(
            ExtractionRecipeCatalog catalog,
            string containerDefName,
            IEnumerable<string> existingRecipeDefNames)
        {
            var original = new List<string>();
            if (existingRecipeDefNames != null)
            {
                foreach (string recipeDefName in existingRecipeDefNames)
                {
                    original.Add(recipeDefName);
                }
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string recipeDefName in original)
            {
                if (!string.IsNullOrEmpty(recipeDefName) && seen.Add(recipeDefName))
                {
                    result.Add(recipeDefName);
                }
            }

            ContainerRecipePlan plan = catalog?.ContainerPlans == null
                ? null
                : FindPlan(catalog.ContainerPlans, containerDefName);
            if (plan != null)
            {
                foreach (string recipeDefName in plan.RecipeDefNames)
                {
                    if (!string.IsNullOrEmpty(recipeDefName) && seen.Add(recipeDefName))
                    {
                        result.Add(recipeDefName);
                    }
                }
            }

            bool changed = original.Count != result.Count;
            if (!changed)
            {
                for (int index = 0; index < result.Count; index++)
                {
                    if (!string.Equals(original[index], result[index], StringComparison.Ordinal))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            var effectiveSpecs = new List<ExtractionRecipeSpec>();
            if (catalog?.RecipeSpecs != null)
            {
                foreach (string recipeDefName in result)
                {
                    foreach (ExtractionRecipeSpec spec in catalog.RecipeSpecs)
                    {
                        if (string.Equals(spec?.RecipeDefName, recipeDefName, StringComparison.Ordinal))
                        {
                            effectiveSpecs.Add(spec);
                            break;
                        }
                    }
                }
            }

            return new ExtractionCatalogContainerApplication(
                new ReadOnlyCollection<string>(result),
                new ReadOnlyCollection<ExtractionRecipeSpec>(effectiveSpecs),
                changed);
        }

        private static ContainerRecipePlan FindPlan(
            IReadOnlyList<ContainerRecipePlan> plans,
            string containerDefName)
        {
            foreach (ContainerRecipePlan plan in plans)
            {
                if (string.Equals(plan?.ContainerDefName, containerDefName, StringComparison.Ordinal))
                {
                    return plan;
                }
            }

            return null;
        }
    }
}

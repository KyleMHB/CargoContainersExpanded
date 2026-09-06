using System;
using System.Collections.Generic;
using System.Linq;
using CargoContainersExpanded;
using NUnit.Framework;

namespace CargoContainersExpanded.Tests
{
    internal sealed class InMemoryExtractionCatalogHost
    {
        private readonly Dictionary<string, List<string>> recipesByContainer = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, ExtractionRecipeSpec> effectiveMappings = new Dictionary<string, ExtractionRecipeSpec>();

        public int CacheInvalidationCount { get; private set; }

        public void SetRecipes(string containerDefName, IEnumerable<string> recipeDefNames)
        {
            recipesByContainer[containerDefName] = new List<string>(recipeDefNames ?? Array.Empty<string>());
        }

        public IReadOnlyList<string> GetRecipes(string containerDefName)
        {
            return recipesByContainer[containerDefName];
        }

        public ExtractionRecipeSpec GetMapping(string recipeDefName)
        {
            effectiveMappings.TryGetValue(recipeDefName, out ExtractionRecipeSpec spec);
            return spec;
        }

        public void Apply(ExtractionRecipeCatalog catalog)
        {
            foreach (ContainerRecipePlan plan in catalog.ContainerPlans)
            {
                recipesByContainer.TryGetValue(plan.ContainerDefName, out List<string> existing);
                ExtractionCatalogContainerApplication result = ExtractionRecipeCatalogHostExecutor.Apply(
                    catalog,
                    plan.ContainerDefName,
                    existing);
                if (result.Changed)
                {
                    recipesByContainer[plan.ContainerDefName] = new List<string>(result.RecipeDefNames);
                    CacheInvalidationCount++;
                }

                foreach (ExtractionRecipeSpec spec in result.EffectiveRecipeSpecs)
                {
                    effectiveMappings[spec.RecipeDefName] = spec;
                }
            }
        }
    }

    [TestFixture]
    public sealed class ExtractionRecipeCatalogTests
    {
        [Test]
        public void FixedRawRicePayloadProducesTheThreeStandardExtractionRecipes()
        {
            ExtractionRecipeCatalog catalog = ExtractionRecipeCatalog.Build(
                new[] { new ExtractionPayloadFacts("RawRice") },
                new[] { new ExtractionContainerFacts("FT_RefrigeratedContainer", "RawRice") },
                Array.Empty<string>());

            Assert.That(catalog.RecipeSpecs.Select(spec => spec.RecipeDefName), Is.EqualTo(new[]
            {
                "FT_ExtractCargo_RawRice_1",
                "FT_ExtractCargo_RawRice_25",
                "FT_ExtractCargo_RawRice_100"
            }));
            Assert.That(catalog.RecipeSpecs.Select(spec => spec.BatchCount), Is.EqualTo(new[] { 1, 25, 100 }));
            Assert.That(catalog.RecipeSpecs.Select(spec => spec.WorkAmount), Is.EqualTo(new[] { 180f, 540f, 900f }));
        }

        [Test]
        public void StuffCategoryPayloadMatchesMaterialBackedContainer()
        {
            ExtractionRecipeCatalog catalog = ExtractionRecipeCatalog.Build(
                new[] { new ExtractionPayloadFacts("Steel", new[] { "Metallic" }) },
                new[] { new ExtractionContainerFacts("FT_CargoContainer", null, new[] { "Metallic", "Woody" }) },
                Array.Empty<string>());

            Assert.That(catalog.ContainerPlans.Single().RecipeDefNames, Is.EqualTo(new[]
            {
                "FT_ExtractCargo_Steel_1",
                "FT_ExtractCargo_Steel_25",
                "FT_ExtractCargo_Steel_100"
            }));
        }

        [Test]
        public void ResolutionDistinguishesModernLegacyUnknownAndWrongPayloadRecipes()
        {
            ExtractionRecipeCatalog catalog = ExtractionRecipeCatalog.Build(
                new[]
                {
                    new ExtractionPayloadFacts("RawRice"),
                    new ExtractionPayloadFacts("Steel")
                },
                Array.Empty<ExtractionContainerFacts>(),
                new[] { "FT_ExtractCargo_RawRice_25", "FT_ExtractCargo_RawRice" });

            ExtractionRecipeResolution modern = catalog.Resolve("FT_ExtractCargo_RawRice_25", "RawRice");
            ExtractionRecipeResolution legacy = catalog.Resolve("FT_ExtractCargo_RawRice", "RawRice");
            ExtractionRecipeResolution unknown = catalog.Resolve("FT_ExtractCargo_Mystery_25", "RawRice");
            ExtractionRecipeResolution wrongPayload = catalog.Resolve("FT_ExtractCargo_RawRice_25", "Steel");

            Assert.That(modern.Kind, Is.EqualTo(ExtractionRecipeResolutionKind.Valid));
            Assert.That(modern.BatchCount, Is.EqualTo(25));
            Assert.That(modern.WorkAmount, Is.EqualTo(540f));
            Assert.That(legacy.IsRecognized, Is.True);
            Assert.That(legacy.IsLegacy, Is.True);
            Assert.That(legacy.CanRun, Is.False);
            Assert.That(unknown.IsRecognized, Is.True);
            Assert.That(unknown.IsUnknown, Is.True);
            Assert.That(unknown.CanRun, Is.False);
            Assert.That(wrongPayload.IsWrongPayload, Is.True);
            Assert.That(wrongPayload.CanRun, Is.False);
        }

        [Test]
        public void LegacyRecipePreservesPayloadNamesThatContainUnderscores()
        {
            ExtractionRecipeCatalog catalog = ExtractionRecipeCatalog.Build(
                new[] { new ExtractionPayloadFacts("Raw_Rice") },
                Array.Empty<ExtractionContainerFacts>(),
                new[] { "FT_ExtractCargo_Raw_Rice" });

            ExtractionRecipeResolution resolution = catalog.Resolve("FT_ExtractCargo_Raw_Rice", "Raw_Rice");

            Assert.That(resolution.IsRecognized, Is.True);
            Assert.That(resolution.IsLegacy, Is.True);
            Assert.That(resolution.CanRun, Is.False);
        }

        [Test]
        public void ExistingRecipeSpecIsMarkedWithoutChangingGeneratedMetadata()
        {
            ExtractionRecipeCatalog catalog = ExtractionRecipeCatalog.Build(
                new[] { new ExtractionPayloadFacts("RawRice") },
                Array.Empty<ExtractionContainerFacts>(),
                new[] { "FT_ExtractCargo_RawRice_25" });

            ExtractionRecipeSpec existing = catalog.RecipeSpecs.Single(
                spec => spec.RecipeDefName == "FT_ExtractCargo_RawRice_25");
            ExtractionRecipeSpec generated = catalog.RecipeSpecs.Single(
                spec => spec.RecipeDefName == "FT_ExtractCargo_RawRice_1");

            Assert.That(existing.AlreadyExists, Is.True);
            Assert.That(existing.BatchCount, Is.EqualTo(25));
            Assert.That(existing.WorkAmount, Is.EqualTo(540f));
            Assert.That(generated.AlreadyExists, Is.False);
            Assert.That(generated.BatchCount, Is.EqualTo(1));
            Assert.That(generated.WorkAmount, Is.EqualTo(180f));
        }

        [Test]
        public void FilteringKeepsNonExtractionRecipesAndOnlyMatchingModernRecipes()
        {
            ExtractionRecipeCatalog catalog = ExtractionRecipeCatalog.Build(
                new[]
                {
                    new ExtractionPayloadFacts("RawRice"),
                    new ExtractionPayloadFacts("Steel")
                },
                Array.Empty<ExtractionContainerFacts>(),
                Array.Empty<string>());

            IReadOnlyList<string> result = catalog.FilterRecipeDefNames(
                "RawRice",
                new[]
                {
                    "Recipe_CookMeal",
                    "FT_ExtractCargo_RawRice_25",
                    "FT_ExtractCargo_RawRice",
                    "FT_ExtractCargo_Steel_1",
                    "FT_ExtractCargo_Mystery_1",
                    null,
                    "Recipe_CookMeal",
                    "FT_ExtractCargo_RawRice_25"
                });

            Assert.That(result, Is.EqualTo(new[] { "Recipe_CookMeal", "FT_ExtractCargo_RawRice_25" }));
        }

        [Test]
        public void DuplicateFactsKeepFirstOrderAndEmitDiagnostics()
        {
            ExtractionRecipeCatalog catalog = ExtractionRecipeCatalog.Build(
                new[]
                {
                    new ExtractionPayloadFacts("RawRice"),
                    new ExtractionPayloadFacts("Steel"),
                    new ExtractionPayloadFacts("RawRice")
                },
                new[]
                {
                    new ExtractionContainerFacts("FT_One", null, new[] { "Metallic" }),
                    new ExtractionContainerFacts("FT_Two", null, new[] { "Metallic" }),
                    new ExtractionContainerFacts("FT_One", null, new[] { "Metallic" })
                },
                Array.Empty<string>());

            Assert.That(catalog.RecipeSpecs.Select(spec => spec.PayloadDefName), Is.EqualTo(new[]
            {
                "RawRice", "RawRice", "RawRice", "Steel", "Steel", "Steel"
            }));
            Assert.That(catalog.ContainerPlans.Select(plan => plan.ContainerDefName), Is.EqualTo(new[] { "FT_One", "FT_Two" }));
            Assert.That(catalog.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(new[]
            {
                ExtractionRecipeCatalog.DuplicatePayloadCode,
                ExtractionRecipeCatalog.DuplicateContainerCode
            }));
        }

        [Test]
        public void MalformedFactsAreSkippedAndReported()
        {
            ExtractionRecipeCatalog catalog = ExtractionRecipeCatalog.Build(
                new[] { null, new ExtractionPayloadFacts(""), new ExtractionPayloadFacts("RawRice") },
                new[] { null, new ExtractionContainerFacts(""), new ExtractionContainerFacts("FT_Valid", "RawRice") },
                Array.Empty<string>());

            Assert.That(catalog.RecipeSpecs.Select(spec => spec.RecipeDefName), Is.EqualTo(new[]
            {
                "FT_ExtractCargo_RawRice_1",
                "FT_ExtractCargo_RawRice_25",
                "FT_ExtractCargo_RawRice_100"
            }));
            Assert.That(catalog.ContainerPlans.Select(plan => plan.ContainerDefName), Is.EqualTo(new[] { "FT_Valid" }));
            Assert.That(catalog.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(new[]
            {
                ExtractionRecipeCatalog.MalformedPayloadCode,
                ExtractionRecipeCatalog.MalformedPayloadCode,
                ExtractionRecipeCatalog.MalformedContainerCode,
                ExtractionRecipeCatalog.MalformedContainerCode
            }));
        }

        [Test]
        public void CatalogOutputsAreImmutableSnapshots()
        {
            ExtractionRecipeCatalog catalog = ExtractionRecipeCatalog.Build(
                new[] { new ExtractionPayloadFacts("RawRice") },
                new[] { new ExtractionContainerFacts("FT_Valid", "RawRice") },
                Array.Empty<string>());

            Assert.That(
                () => ((IList<ExtractionRecipeSpec>)catalog.RecipeSpecs).Clear(),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(
                () => ((IList<string>)catalog.ContainerPlans.Single().RecipeDefNames).Clear(),
                Throws.TypeOf<NotSupportedException>());

            var originalPayloads = new[] { new ExtractionPayloadFacts("RawRice") };
            ExtractionRecipeCatalog copiedCatalog = ExtractionRecipeCatalog.Build(
                originalPayloads,
                Array.Empty<ExtractionContainerFacts>(),
                Array.Empty<string>());
            originalPayloads[0] = new ExtractionPayloadFacts("Steel");

            Assert.That(copiedCatalog.RecipeSpecs.Single(spec => spec.RecipeDefName.EndsWith("_1")).PayloadDefName, Is.EqualTo("RawRice"));
        }

        [Test]
        public void ReapplyingCatalogToHostIsIdempotentAndInvalidatesChangedCaches()
        {
            ExtractionRecipeCatalog catalog = ExtractionRecipeCatalog.Build(
                new[] { new ExtractionPayloadFacts("RawRice") },
                new[] { new ExtractionContainerFacts("FT_Container", "RawRice") },
                Array.Empty<string>());
            var host = new InMemoryExtractionCatalogHost();
            host.SetRecipes("FT_Container", new[] { "Recipe_Existing", "Recipe_Existing", null });

            host.Apply(catalog);
            host.Apply(catalog);

            Assert.That(host.GetRecipes("FT_Container"), Is.EqualTo(new[]
            {
                "Recipe_Existing",
                "FT_ExtractCargo_RawRice_1",
                "FT_ExtractCargo_RawRice_25",
                "FT_ExtractCargo_RawRice_100"
            }));
            Assert.That(host.CacheInvalidationCount, Is.EqualTo(1));
        }

        [Test]
        public void ModernMappingWinsWhenLegacyNameCollidesWithPayloadSuffix()
        {
            ExtractionRecipeCatalog catalog = ExtractionRecipeCatalog.Build(
                new[]
                {
                    new ExtractionPayloadFacts("RawRice"),
                    new ExtractionPayloadFacts("RawRice_1")
                },
                new[] { new ExtractionContainerFacts("FT_Container", "RawRice") },
                new[] { "FT_ExtractCargo_RawRice_1", "FT_ExtractCargo_RawRice_1_1" });
            var host = new InMemoryExtractionCatalogHost();
            host.SetRecipes("FT_Container", new[] { "FT_ExtractCargo_RawRice_1" });

            host.Apply(catalog);

            ExtractionRecipeSpec effective = host.GetMapping("FT_ExtractCargo_RawRice_1");
            Assert.That(effective, Is.Not.Null);
            Assert.That(effective.PayloadDefName, Is.EqualTo("RawRice"));
            Assert.That(effective.BatchCount, Is.EqualTo(1));
            Assert.That(effective.WorkAmount, Is.EqualTo(180f));
            Assert.That(effective.IsLegacy, Is.False);
        }
    }
}

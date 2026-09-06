using System;
using System.Collections.Generic;
using System.Threading;
using CargoContainersExpanded;
using NUnit.Framework;

namespace CargoContainersExpanded.Tests
{
    [TestFixture]
    public sealed class ExtractionCompatibilityScopesTests
    {
        private sealed class RecipeListView
        {
            public RecipeListView(IReadOnlyList<string> defNames)
            {
                DefNames = new List<string>(defNames);
            }

            public IReadOnlyList<string> DefNames { get; }
        }

        private sealed class InMemoryRecipeListHost : IRecipeListHost
        {
            public InMemoryRecipeListHost(IReadOnlyList<string> recipes, IReadOnlyList<string> cachedRecipes)
                : this(recipes, cachedRecipes, true)
            {
            }

            public InMemoryRecipeListHost(IReadOnlyList<string> recipes, IReadOnlyList<string> cachedRecipes, bool hasRecipeCache)
            {
                Identity = new object();
                RecipeList = new RecipeListView(recipes);
                cachedRecipeList = new RecipeListView(cachedRecipes);
                HasRecipeCache = hasRecipeCache;
            }

            public object Identity { get; }

            public object RecipeList { get; set; }

            public bool HasRecipeCache { get; }

            private object cachedRecipeList;

            public Exception NextCachedSetException { get; set; }

            public int CachedRecipeSetCount { get; private set; }

            public object CachedRecipeList
            {
                get => cachedRecipeList;
                set
                {
                    CachedRecipeSetCount++;
                    if (NextCachedSetException != null)
                    {
                        Exception exception = NextCachedSetException;
                        NextCachedSetException = null;
                        throw exception;
                    }

                    cachedRecipeList = value;
                }
            }

            public object CreateRecipeList(IReadOnlyList<string> defNames)
            {
                return new RecipeListView(defNames);
            }
        }

        [Test]
        public void UnrelatedWorkGiverNeverBypassesPower()
        {
            using (ExtractionWorkScope scope = ExtractionCompatibilityScopes.EnterWorkGiver("OtherWorkGiver"))
            {
                Assert.That(ExtractionCompatibilityScopes.ShouldBypassPower(true), Is.False);
            }

            Assert.That(ExtractionCompatibilityScopes.ShouldBypassPower(true), Is.False);
        }

        [Test]
        public void NestedExtractionWorkScopesRemainActiveUntilTheOuterLeaseIsDisposed()
        {
            ExtractionWorkScope outer = ExtractionCompatibilityScopes.EnterWorkGiver("FT_DoBillsExtractCargoContainers");
            ExtractionWorkScope inner = ExtractionCompatibilityScopes.EnterWorkGiver("FT_DoBillsExtractCargoContainers");
            try
            {
                Assert.That(ExtractionCompatibilityScopes.ShouldBypassPower(true), Is.True);
                inner.Dispose();
                Assert.That(ExtractionCompatibilityScopes.ShouldBypassPower(true), Is.True);
            }
            finally
            {
                outer.Dispose();
            }

            Assert.That(ExtractionCompatibilityScopes.ShouldBypassPower(true), Is.False);
        }

        [Test]
        public void DisposingAnExtractionWorkScopeTwiceDoesNotUnderflowTheLeaseDepth()
        {
            ExtractionWorkScope scope = ExtractionCompatibilityScopes.EnterWorkGiver("FT_DoBillsExtractCargoContainers");
            scope.Dispose();
            scope.Dispose();

            Assert.That(ExtractionCompatibilityScopes.ShouldBypassPower(true), Is.False);
        }

        [Test]
        public void ExtractionWorkScopeDepthIsIsolatedPerThread()
        {
            ExtractionWorkScope scope = ExtractionCompatibilityScopes.EnterWorkGiver("FT_DoBillsExtractCargoContainers");
            bool otherThreadBypass = true;
            Exception otherThreadException = null;
            try
            {
                Thread thread = new Thread(() =>
                {
                    try
                    {
                        otherThreadBypass = ExtractionCompatibilityScopes.ShouldBypassPower(true);
                    }
                    catch (Exception exception)
                    {
                        otherThreadException = exception;
                    }
                });
                thread.Start();
                thread.Join();
            }
            finally
            {
                scope.Dispose();
            }

            Assert.That(otherThreadException, Is.Null);
            Assert.That(otherThreadBypass, Is.False);
        }

        [Test]
        public void ActiveExtractionScopeBypassesPowerOnlyForContainersWithPayload()
        {
            using (ExtractionWorkScope scope = ExtractionCompatibilityScopes.EnterWorkGiver("FT_DoBillsExtractCargoContainers"))
            {
                Assert.That(ExtractionCompatibilityScopes.ShouldBypassPower(false), Is.False);
                Assert.That(ExtractionCompatibilityScopes.ShouldBypassPower(true), Is.True);
            }
        }

        [Test]
        public void FilteredRecipeViewRestoresExactListReferencesAfterSuccess()
        {
            var host = new InMemoryRecipeListHost(new[] { "First", "Second" }, new[] { "First", "Second" });
            object originalRecipes = host.RecipeList;
            object originalCache = host.CachedRecipeList;

            string result = ExtractionCompatibilityScopes.WithFilteredRecipes(
                host,
                new[] { "Second" },
                () =>
                {
                    Assert.That(((RecipeListView)host.RecipeList).DefNames, Is.EqualTo(new[] { "Second" }));
                    Assert.That(((RecipeListView)host.CachedRecipeList).DefNames, Is.EqualTo(new[] { "Second" }));
                    return "completed";
                });

            Assert.That(result, Is.EqualTo("completed"));
            Assert.That(host.RecipeList, Is.SameAs(originalRecipes));
            Assert.That(host.CachedRecipeList, Is.SameAs(originalCache));
        }

        [Test]
        public void FilteredRecipeViewRestoresExactReferencesAndExceptionIdentityAfterFailure()
        {
            var host = new InMemoryRecipeListHost(new[] { "First", "Second" }, new[] { "First", "Second" });
            object originalRecipes = host.RecipeList;
            object originalCache = host.CachedRecipeList;
            var expected = new InvalidOperationException("menu failed");
            Exception actual = null;

            try
            {
                ExtractionCompatibilityScopes.WithFilteredRecipes<object>(
                    host,
                    new[] { "Second" },
                    () =>
                    {
                        throw expected;
                    });
            }
            catch (Exception exception)
            {
                actual = exception;
            }

            Assert.That(actual, Is.SameAs(expected));
            Assert.That(host.RecipeList, Is.SameAs(originalRecipes));
            Assert.That(host.CachedRecipeList, Is.SameAs(originalCache));
        }

        [Test]
        public void SameHostRecursionReusesTheActiveViewWhileDifferentHostsNestIndependently()
        {
            var outerHost = new InMemoryRecipeListHost(new[] { "First", "Second" }, new[] { "First", "Second" });
            var innerHost = new InMemoryRecipeListHost(new[] { "Alpha", "Beta" }, new[] { "Alpha", "Beta" });

            ExtractionCompatibilityScopes.WithFilteredRecipes(
                outerHost,
                new[] { "Second" },
                () =>
                {
                    object activeOuterRecipes = outerHost.RecipeList;
                    ExtractionCompatibilityScopes.WithFilteredRecipes(
                        outerHost,
                        new[] { "First" },
                        () =>
                        {
                            Assert.That(outerHost.RecipeList, Is.SameAs(activeOuterRecipes));
                            Assert.That(((RecipeListView)outerHost.RecipeList).DefNames, Is.EqualTo(new[] { "Second" }));
                            return 0;
                        });

                    ExtractionCompatibilityScopes.WithFilteredRecipes(
                        innerHost,
                        new[] { "Beta" },
                        () =>
                        {
                            Assert.That(((RecipeListView)outerHost.RecipeList).DefNames, Is.EqualTo(new[] { "Second" }));
                            Assert.That(((RecipeListView)innerHost.RecipeList).DefNames, Is.EqualTo(new[] { "Beta" }));
                            return 0;
                        });
                    return 0;
                });
        }

        [Test]
        public void MissingRecipeCacheLeavesTheMenuUnfilteredAndStillRunsTheAction()
        {
            var host = new InMemoryRecipeListHost(new[] { "First", "Second" }, new[] { "First", "Second" }, false);
            object originalRecipes = host.RecipeList;
            object originalCache = host.CachedRecipeList;

            string result = ExtractionCompatibilityScopes.WithFilteredRecipes(
                host,
                new[] { "Second" },
                () =>
                {
                    Assert.That(host.RecipeList, Is.SameAs(originalRecipes));
                    Assert.That(host.CachedRecipeList, Is.SameAs(originalCache));
                    return "unfiltered";
                });

            Assert.That(result, Is.EqualTo("unfiltered"));
            Assert.That(host.RecipeList, Is.SameAs(originalRecipes));
            Assert.That(host.CachedRecipeList, Is.SameAs(originalCache));
        }

        [Test]
        public void MissingRecipeListLeavesTheMenuUnfilteredAndStillRunsTheAction()
        {
            var host = new InMemoryRecipeListHost(new[] { "First" }, new[] { "First" });
            host.RecipeList = null;
            object originalCache = host.CachedRecipeList;

            string result = ExtractionCompatibilityScopes.WithFilteredRecipes(
                host,
                new[] { "First" },
                () =>
                {
                    Assert.That(host.RecipeList, Is.Null);
                    Assert.That(host.CachedRecipeList, Is.SameAs(originalCache));
                    return "unfiltered";
                });

            Assert.That(result, Is.EqualTo("unfiltered"));
            Assert.That(host.RecipeList, Is.Null);
            Assert.That(host.CachedRecipeList, Is.SameAs(originalCache));
        }

        [Test]
        public void CacheRestoreFailureStillAllowsTheSameHostToAcquireANewFilteredView()
        {
            var host = new InMemoryRecipeListHost(new[] { "First", "Second" }, new[] { "First", "Second" });
            object originalRecipes = host.RecipeList;
            object originalCache = host.CachedRecipeList;
            var expectedRestoreFailure = new InvalidOperationException("cache restore failed");
            host.NextCachedSetException = null;

            Exception actualRestoreFailure = null;
            try
            {
                ExtractionCompatibilityScopes.WithFilteredRecipes(
                    host,
                    new[] { "Second" },
                    () =>
                    {
                        Assert.That(((RecipeListView)host.RecipeList).DefNames, Is.EqualTo(new[] { "Second" }));
                        host.NextCachedSetException = expectedRestoreFailure;
                        return "first";
                    });
            }
            catch (Exception exception)
            {
                actualRestoreFailure = exception;
            }

            Assert.That(actualRestoreFailure, Is.SameAs(expectedRestoreFailure));
            Assert.That(host.RecipeList, Is.SameAs(originalRecipes));
            Assert.That(host.CachedRecipeSetCount, Is.EqualTo(2));
            Assert.That(host.CachedRecipeList, Is.Not.SameAs(originalCache));

            string secondResult = ExtractionCompatibilityScopes.WithFilteredRecipes(
                host,
                new[] { "First" },
                () =>
                {
                    Assert.That(((RecipeListView)host.RecipeList).DefNames, Is.EqualTo(new[] { "First" }));
                    return "fresh";
                });

            Assert.That(secondResult, Is.EqualTo("fresh"));
        }

        [Test]
        public void JobFinalizerReturnsTheOriginalExceptionAndDisposesTheLease()
        {
            ExtractionWorkScope scope = ExtractionCompatibilityScopes.EnterWorkGiver("FT_DoBillsExtractCargoContainers");
            var expected = new InvalidOperationException("job failed");

            Exception returned = WorkGiverDoBill_JobOnThing_ExtractCargoPatch.Finalizer(scope, expected);

            Assert.That(returned, Is.SameAs(expected));
            Assert.That(ExtractionCompatibilityScopes.ShouldBypassPower(true), Is.False);
        }
    }
}

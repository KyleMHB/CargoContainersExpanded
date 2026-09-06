using System;
using System.Collections.Generic;

namespace CargoContainersExpanded
{
    // This is the narrow seam around a third-party host whose recipe list is temporarily
    // replaced while RimWorld builds a menu. The host owns the concrete list type.
    internal interface IRecipeListHost
    {
        object Identity { get; }

        object RecipeList { get; set; }

        bool HasRecipeCache { get; }

        object CachedRecipeList { get; set; }

        object CreateRecipeList(IReadOnlyList<string> recipeDefNames);
    }

    internal sealed class ExtractionWorkScope : IDisposable
    {
        private readonly bool entered;
        private bool disposed;

        internal ExtractionWorkScope(bool entered)
        {
            this.entered = entered;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (entered)
            {
                ExtractionCompatibilityScopes.ExitWorkGiver();
            }
        }
    }

    internal static class ExtractionCompatibilityScopes
    {
        private const string ExtractionWorkGiverDefName = "FT_DoBillsExtractCargoContainers";

        [ThreadStatic]
        private static HashSet<object> activeRecipeListHosts;

        [ThreadStatic]
        private static int activeExtractionWorkGiverScopes;

        public static ExtractionWorkScope EnterWorkGiver(string workGiverDefName)
        {
            bool entered = string.Equals(
                workGiverDefName,
                ExtractionWorkGiverDefName,
                StringComparison.Ordinal);
            if (entered)
            {
                activeExtractionWorkGiverScopes++;
            }

            return new ExtractionWorkScope(entered);
        }

        public static bool ShouldBypassPower(bool containerHasPayload)
        {
            return activeExtractionWorkGiverScopes > 0 && containerHasPayload;
        }

        public static T WithFilteredRecipes<T>(
            IRecipeListHost host,
            IReadOnlyList<string> filteredRecipeDefNames,
            Func<T> action)
        {
            if (host == null || !host.HasRecipeCache)
            {
                return action();
            }

            object currentRecipeList = host.RecipeList;
            if (currentRecipeList == null)
            {
                return action();
            }

            object identity = host.Identity ?? host;
            activeRecipeListHosts ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
            if (!activeRecipeListHosts.Add(identity))
            {
                return action();
            }

            object originalRecipeList = null;
            object originalCachedRecipeList = null;
            bool capturedOriginals = false;
            try
            {
                originalRecipeList = currentRecipeList;
                originalCachedRecipeList = host.CachedRecipeList;
                capturedOriginals = true;
                object filteredRecipeList = host.CreateRecipeList(
                    filteredRecipeDefNames ?? Array.Empty<string>());
                host.RecipeList = filteredRecipeList;
                host.CachedRecipeList = filteredRecipeList;
                return action();
            }
            finally
            {
                try
                {
                    try
                    {
                        if (capturedOriginals)
                        {
                            host.RecipeList = originalRecipeList;
                        }
                    }
                    finally
                    {
                        if (capturedOriginals)
                        {
                            host.CachedRecipeList = originalCachedRecipeList;
                        }
                    }
                }
                finally
                {
                    activeRecipeListHosts.Remove(identity);
                }
            }
        }

        internal static void ExitWorkGiver()
        {
            if (activeExtractionWorkGiverScopes > 0)
            {
                activeExtractionWorkGiverScopes--;
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object left, object right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(object value)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}

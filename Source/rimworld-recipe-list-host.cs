using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace CargoContainersExpanded
{
    // Adapts ThingDef's mutable lists to the narrow third-party host seam used by
    // ExtractionCompatibilityScopes. Reflection remains isolated here.
    internal sealed class RimWorldRecipeListHost : IRecipeListHost
    {
        internal static readonly FieldInfo AllRecipesCachedField =
            AccessTools.Field(typeof(ThingDef), "allRecipesCached");

        private readonly ThingDef thingDef;

        internal RimWorldRecipeListHost(ThingDef thingDef)
        {
            this.thingDef = thingDef;
        }

        internal static bool CanFilterRecipeLists => AllRecipesCachedField != null;

        public object Identity => thingDef;

        public object RecipeList
        {
            get => thingDef?.recipes;
            set => thingDef.recipes = (List<RecipeDef>)value;
        }

        public bool HasRecipeCache => AllRecipesCachedField != null;

        public object CachedRecipeList
        {
            get => AllRecipesCachedField?.GetValue(thingDef) as List<RecipeDef>;
            set => AllRecipesCachedField?.SetValue(thingDef, value);
        }

        public object CreateRecipeList(IReadOnlyList<string> recipeDefNames)
        {
            var recipes = new List<RecipeDef>();
            if (thingDef?.recipes == null || recipeDefNames == null)
            {
                return recipes;
            }

            foreach (string recipeDefName in recipeDefNames)
            {
                foreach (RecipeDef recipeDef in thingDef.recipes)
                {
                    if (recipeDef != null && string.Equals(recipeDef.defName, recipeDefName, StringComparison.Ordinal))
                    {
                        recipes.Add(recipeDef);
                        break;
                    }
                }
            }

            return recipes;
        }
    }
}

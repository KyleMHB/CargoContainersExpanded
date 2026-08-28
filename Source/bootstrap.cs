using System;
using HarmonyLib;
using Verse;

namespace CargoContainersExpanded
{
    [StaticConstructorOnStartup]
    public static class CargoContainersBootstrap
    {
        static CargoContainersBootstrap()
        {
            try
            {
                RefrigeratedContainerBootstrap.Initialize();
                CargoExtractionUtility.ConfigureExtractionDefs();
                new Harmony("KyleMHB.CargoContainersExpanded").PatchAll();
            }
            catch (Exception exception)
            {
                Log.Error($"Cargo Containers Expanded: bootstrap failed.\n{exception}");
            }
        }
    }
}

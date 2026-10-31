#nullable enable
using System;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace SilverpineMods.CustomItemLoader;

// Unity scene/startup callbacks run on the main thread, after plugin Awakes.
// Neither callback needs a live BepInEx component or a coroutine host.
internal static class RegistrationLifecycle
{
    private static bool initialized;
    private static bool initialScanFinished;
    private static bool completing;

    internal static void Initialize()
    {
        if (initialized)
            return;
        initialized = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    internal static void MarkInitialScanFinished() => initialScanFinished = true;

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CompleteStartup();

    internal static void CompleteStartup()
    {
        if (!initialScanFinished || completing || CustomItemApi.IsRegistrationComplete)
            return;

        completing = true;
        try
        {
            // Optional art and validation must not strand integration subscribers.
            PrepareSafely(Plugin.ResolveAvailableInheritedVisuals, "inherited visuals");
            PrepareSafely(CustomAmmunitionSafety.ValidateRegisteredWeapons, "ammunition validation");
            CustomItemApi.PublishRegistrationCompleted();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Plugin.Log.LogInfo("Custom item registration complete; dependent mods notified.");
        }
        finally
        {
            completing = false;
        }
    }

    private static void PrepareSafely(Action action, string description)
    {
        try { action(); }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning($"Startup {description} failed; registration will still complete: {exception}");
        }
    }
}

// The initial menu can already be in the loaded scene when BepInEx starts.
// This is a readiness fallback only; menu injection stays with Modding Tools.
[HarmonyPatch(typeof(MainMenuUI), "Start")]
internal static class CustomItemStartupReadyPatch
{
    private static void Prefix() => RegistrationLifecycle.CompleteStartup();
}

#nullable enable
using System;
using System.Collections.Generic;

namespace SilverpineMods.CustomItemLoader;

public static partial class CustomItemApi
{
    private static bool registrationComplete;

    /// <summary>
    /// Raised once per process after the initial pack scan and plugin Awakes.
    /// Uncached GLB thumbnails may still be rendering; packs waiting for a
    /// later equipment-slot registration arrive through ItemRegistered.
    /// </summary>
    public static event EventHandler? RegistrationCompleted;

    public static bool IsRegistrationComplete
    {
        get { lock (Sync) return registrationComplete; }
    }

    /// <summary>
    /// Runs immediately if registration is complete; otherwise runs once
    /// when registration completes. The callback receives a stable snapshot.
    /// Call from Unity's main thread when accessing game objects.
    /// </summary>
    public static void WhenReady(Action<IReadOnlyList<CustomItemInfo>> callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        bool runNow;
        lock (Sync)
        {
            runNow = registrationComplete;
            if (!runNow)
            {
                EventHandler? handler = null;
                handler = (_, _) =>
                {
                    RegistrationCompleted -= handler;
                    InvokeSafely(() => callback(GetRegisteredItems()), "WhenReady callback");
                };
                RegistrationCompleted += handler;
            }
        }

        if (runNow)
            InvokeSafely(() => callback(GetRegisteredItems()), "WhenReady callback");
    }

    internal static void PublishRegistrationCompleted()
    {
        EventHandler? handlers;
        lock (Sync)
        {
            if (registrationComplete)
                return;
            registrationComplete = true;
            handlers = RegistrationCompleted;
        }

        if (handlers == null)
            return;
        foreach (EventHandler handler in handlers.GetInvocationList())
            InvokeSafely(() => handler(null, EventArgs.Empty), "RegistrationCompleted subscriber");
    }

    private static void InvokeSafely(Action callback, string description)
    {
        try { callback(); }
        catch (Exception exception)
        {
            Plugin.Log.LogError($"Custom Item API {description} failed: {exception}");
        }
    }
}

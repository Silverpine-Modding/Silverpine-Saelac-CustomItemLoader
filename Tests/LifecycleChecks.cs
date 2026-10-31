using System.Reflection;
using SilverpineMods.CustomItemLoader;
using UnityEngine.SceneManagement;

internal static class LifecycleChecks
{
    private static void Reset()
    {
        // Isolate scenarios without adding reset/hot-unload APIs to production.
        SceneManager.Reset();
        foreach (string field in new[] { "initialized", "initialScanFinished", "completing" })
            typeof(RegistrationLifecycle).GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
        typeof(CustomItemApi).GetField("registrationComplete", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
        typeof(CustomItemApi).GetField("RegistrationCompleted", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
        CustomItemApi.TestItems.Clear();
        Plugin.ResolveVisuals = () => { };
        Plugin.Log.Messages.Clear();
    }

    internal static void Run(Action<bool, string> check)
    {
        Reset();
        RegistrationLifecycle.Initialize();
        RegistrationLifecycle.Initialize();
        check(SceneManager.SubscriberCount == 1, "startup hooks initialize only once without any Unity host");
        int readyCalls = 0, eventCalls = 0, nestedCalls = 0, prepared = 0;
        var template = new CustomItemInfo();
        CustomItemApi.TestItems.Add(template);
        CustomItemApi.RegistrationCompleted += (_, _) => throw new InvalidOperationException("test subscriber failure");
        CustomItemApi.WhenReady(_ => throw new InvalidOperationException("test WhenReady failure"));
        IReadOnlyList<CustomItemInfo>? received = null;
        CustomItemApi.WhenReady(items =>
        {
            readyCalls++;
            received = items;
            foreach (var item in items) item.Template.Components.Add("planting-behavior");
            CustomItemApi.WhenReady(_ => nestedCalls++);
        });
        CustomItemApi.RegistrationCompleted += (_, _) =>
        {
            eventCalls++;
            CustomItemApi.PublishRegistrationCompleted();
            RegistrationLifecycle.CompleteStartup();
        };
        SceneManager.Load();
        RegistrationLifecycle.CompleteStartup();
        check(!CustomItemApi.IsRegistrationComplete && readyCalls == 0,
            "early scene/startup signals cannot publish before the initial scan finishes");
        RegistrationLifecycle.MarkInitialScanFinished();
        check(!CustomItemApi.IsRegistrationComplete, "finishing CIL's scan still waits for the post-Awake startup boundary");
        var lateSlotItem = new CustomItemInfo();
        CustomItemApi.TestItems.Add(lateSlotItem);
        Plugin.ResolveVisuals = () => { prepared++; RegistrationLifecycle.CompleteStartup(); };
        // Only static callbacks remain: no component, coroutine, or frame update.
        SceneManager.Load();
        check(CustomItemApi.IsRegistrationComplete && readyCalls == 1 && eventCalls == 1,
            "scene callback completes registration without a surviving plugin component");
        check(received?.Count == 2 && template.Template.Components.Count == 1 && lateSlotItem.Template.Components.Count == 1,
            "dependent planting-style subscriber sees items added during later plugin Awakes");
        check(prepared == 1 && nestedCalls == 1, "reentrant startup and ready callbacks do not repeat completion");
        check(Plugin.Log.Messages.Any(m => m.Contains("test subscriber failure")) &&
            Plugin.Log.Messages.Any(m => m.Contains("test WhenReady failure")),
            "failed integration subscribers are logged without blocking healthy subscribers");
        check(SceneManager.SubscriberCount == 0, "completed registration releases its no-longer-needed scene callback");
        CustomItemApi.TestItems.Add(new());
        SceneManager.Load();
        RegistrationLifecycle.Initialize();
        RegistrationLifecycle.CompleteStartup();
        CustomItemApi.PublishRegistrationCompleted();
        check(readyCalls == 1 && eventCalls == 1 && prepared == 1 && SceneManager.SubscriberCount == 0,
            "later scenes and duplicate initialization/publication cannot notify or prepare twice");
        int lateCount = 0;
        CustomItemApi.WhenReady(items => lateCount = items.Count);
        check(lateCount == 3 && received!.Count == 2, "late subscribers run immediately with a fresh stable snapshot");
        bool rejectedNull = false;
        try { CustomItemApi.WhenReady(null!); } catch (ArgumentNullException) { rejectedNull = true; }
        check(rejectedNull, "null readiness callbacks are rejected");

        Reset();
        RegistrationLifecycle.Initialize();
        RegistrationLifecycle.MarkInitialScanFinished();
        readyCalls = 0;
        CustomItemApi.WhenReady(_ => readyCalls++);
        Plugin.ResolveVisuals = () => throw new InvalidOperationException("test optional visual failure");
        // Invoke the actual production fallback prefix, without a scene change.
        typeof(CustomItemStartupReadyPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
        check(CustomItemApi.IsRegistrationComplete && readyCalls == 1 && SceneManager.SubscriberCount == 0,
            "initial-menu fallback completes even with no scene-loaded event and no items");
        check(Plugin.Log.Messages.Any(m => m.Contains("test optional visual failure")),
            "optional preparation failure is reported but cannot strand dependent mods");
        Plugin.ResolveVisuals = () => { };
    }
}

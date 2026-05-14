using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

public static class GameLocalization
{
    public static event Action LocaleAppliedDeferred;

    public static void RaiseLocaleAppliedDeferred()
    {
        LocaleAppliedDeferred?.Invoke();
        NotifyPassiveIncludingInactiveInstances();
    }

    public static void NotifyPassiveIncludingInactiveInstances()
    {
        SweepPassiveInclusive<TutorialUI>(t => t.ApplyPassiveLocaleRefresh());
        SweepPassiveInclusive<BuildingButton>(b => b.ApplyPassiveLocaleRefresh());
        SweepPassiveInclusive<LaunchCompleteUI>(l => l.ApplyPassiveLocaleRefresh());
        SweepPassiveInclusive<ExtractorUIManager>(e => e.ApplyPassiveLocaleRefresh());
        SweepPassiveInclusive<MainControlPanel>(m => m.ApplyPassiveLocaleRefresh());
        SweepPassiveInclusive<UIManager>(u => u.ApplyPassiveLocaleRefresh());
    }

    private static void SweepPassiveInclusive<TComponent>(System.Action<TComponent> apply)
        where TComponent : Component
    {
        TComponent[] instances = UnityEngine.Object.FindObjectsByType<TComponent>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (TComponent component in instances)
        {
            if (!ShouldPassiveSweep(component))
            {
                continue;
            }

            apply(component);
        }
    }

    private static bool ShouldPassiveSweep(Component component)
    {
        if (component == null)
        {
            return false;
        }

        HideFlags hf = component.gameObject.hideFlags;
        if ((hf & HideFlags.HideAndDontSave) != 0 || (hf & HideFlags.NotEditable) != 0)
        {
            return false;
        }

        return true;
    }

    public static string Get(string table, string key)
    {
        if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        string localized = LocalizationSettings.StringDatabase.GetLocalizedString(table, key);
        return string.IsNullOrEmpty(localized) ? key : localized;
    }

    public static string Get(string table, string key, params object[] arguments)
    {
        if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        string localized = LocalizationSettings.StringDatabase.GetLocalizedString(table, key, arguments);
        return string.IsNullOrEmpty(localized) ? key : localized;
    }

    public static string GetOrDefault(string table, string key, string fallback)
    {
        string localized = Get(table, key);
        return localized == key ? fallback : localized;
    }

    public static string GetOrDefault(string table, string key, string fallbackFormat, params object[] arguments)
    {
        string localized = Get(table, key, arguments);
        if (localized == key)
        {
            return string.Format(fallbackFormat, arguments);
        }
        return localized;
    }

    public static string GetResourceType(ResourceType resourceType)
    {
        string fallback = resourceType.ToString();
        string nameKey = $"resourceType.{resourceType}.name";
        string localized = Get("Resource", nameKey);
        if (localized != nameKey)
        {
            return localized;
        }

        string legacyKey = $"resourceType.{resourceType}";
        localized = Get("Resource", legacyKey);
        if (localized != legacyKey)
        {
            return localized;
        }

        return fallback;
    }

    public static string GetModuleType(ModuleType moduleType)
    {
        return GetOrDefault("ModuleType", $"moduleType.{moduleType}", moduleType.ToString());
    }
}

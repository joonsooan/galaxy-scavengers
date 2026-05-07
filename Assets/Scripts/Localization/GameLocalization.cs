using UnityEngine.Localization.Settings;

public static class GameLocalization
{
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
        return GetOrDefault("Resource", $"resourceType.{resourceType}", resourceType.ToString());
    }

    public static string GetModuleType(ModuleType moduleType)
    {
        return GetOrDefault("ModuleType", $"moduleType.{moduleType}", moduleType.ToString());
    }
}

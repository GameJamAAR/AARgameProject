using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Réglages du joueur sauvegardés entre deux parties (PlayerPrefs) :
/// sensibilités, vue, accroupi, champ de vision et touches personnalisées.
/// </summary>
public static class GameSettings
{
    private const string Prefix = "AAR.";
    private const string BindingsKey = Prefix + "Bindings";

    public static float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(Prefix + key, fallback);
    public static int GetInt(string key, int fallback) => PlayerPrefs.GetInt(Prefix + key, fallback);
    public static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) != 0;

    public static void SetFloat(string key, float value) { PlayerPrefs.SetFloat(Prefix + key, value); PlayerPrefs.Save(); }
    public static void SetInt(string key, int value) { PlayerPrefs.SetInt(Prefix + key, value); PlayerPrefs.Save(); }
    public static void SetBool(string key, bool value) { PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0); PlayerPrefs.Save(); }

    /// <summary>Recharge les touches personnalisées.</summary>
    public static void LoadBindings(InputActionMap map)
    {
        if (map == null) return;
        string json = PlayerPrefs.GetString(BindingsKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return;
        try { map.LoadBindingOverridesFromJson(json); }
        catch (System.Exception e) { Debug.LogWarning("[AAR] Touches sauvegardées illisibles, ignorées : " + e.Message); }
    }

    /// <summary>Sauvegarde les touches personnalisées.</summary>
    public static void SaveBindings(InputActionMap map)
    {
        if (map == null) return;
        PlayerPrefs.SetString(BindingsKey, map.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }
}

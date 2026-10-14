using UnityEngine;

/// <summary>
/// Play order: SplashScreen → Level1 → Level2 → lvl 3 from source → SplashScreen.
/// </summary>
public static class LevelChain
{
    public const string Splash = "SplashScreen";
    public const string Level1 = "Level1";
    public const string Level2 = "Level2";
    public const string Level3 = "lvl 3 from source";

    public static string NextAfter(string sceneName)
    {
        if (sceneName == Splash || sceneName == "Backstory" || sceneName == "Backstory1")
            return Level1;
        if (sceneName == Level1)
            return Level2;
        if (sceneName == Level2 || sceneName == "lvl2 trial")
            return Level3;
        if (sceneName == Level3 || sceneName == "Level3")
            return Splash;
        return null;
    }

    public static bool TryLoad(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        sceneName = sceneName.Trim();
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"Cannot load '{sceneName}'. Add it in File → Build Settings (name must match the scene file).");
            return false;
        }

        LevelLoadingScreen.ShowAndLoad(sceneName);
        return true;
    }
}

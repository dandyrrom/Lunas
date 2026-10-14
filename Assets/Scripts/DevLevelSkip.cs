using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor / development-build only. Press F7 to load the next scene in the chain
/// so continuity can be checked without finishing the level.
/// </summary>
public class DevLevelSkip : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureExists()
    {
        if (FindAnyObjectByType<DevLevelSkip>() != null)
            return;

        GameObject go = new GameObject("DevLevelSkip");
        DontDestroyOnLoad(go);
        go.AddComponent<DevLevelSkip>();
    }

    void Update()
    {
        if (!WasSkipPressed())
            return;

        string current = SceneManager.GetActiveScene().name;
        string next = LevelChain.NextAfter(current);
        if (string.IsNullOrEmpty(next))
        {
            Debug.Log($"DevLevelSkip: no next scene after '{current}'.");
            return;
        }

        Debug.Log($"DevLevelSkip: F7 {current} → {next}");
        LevelChain.TryLoad(next);
    }

    void OnGUI()
    {
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.65f, 1.5f);
        GUI.Label(
            new Rect(16f * scale, Screen.height - 36f * scale, 520f * scale, 24f * scale),
            "DEV  F7  next level");
    }

    static bool WasSkipPressed()
    {
        return Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame;
    }
#else
    void Awake()
    {
        Destroy(gameObject);
    }
#endif
}

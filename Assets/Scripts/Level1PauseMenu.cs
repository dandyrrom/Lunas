using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Pause on ESC or when the game window loses focus / cursor leaves.
/// Resume / Restart / Quit. Used on Level 1, Level 2, and the sea-port level.
/// </summary>
public class Level1PauseMenu : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("Loaded by Quit — main menu / splash.")]
    public string mainMenuSceneName = "SplashScreen";

    [Header("Audio")]
    public AudioClip buttonClickClip;
    [Range(0f, 1f)] public float buttonClickVolume = 0.85f;

    [Header("Focus / cursor")]
    [Tooltip("Pause when the Unity player / Game view loses focus (click outside).")]
    public bool pauseOnFocusLost = true;
    [Tooltip("Pause when the mouse leaves the game window while unlocked.")]
    public bool pauseWhenCursorLeavesWindow = true;

    readonly string[] menuItems = { "RESUME", "RESTART", "QUIT" };

    Texture2D darkTexture;
    Texture2D panelTexture;
    Texture2D goldTexture;
    GUIStyle titleStyle;
    GUIStyle subtitleStyle;
    GUIStyle menuStyle;
    GUIStyle selectedMenuStyle;
    GUIStyle promptStyle;
    Font menuFont;

    AudioSource uiAudio;
    int selectedItem;
    bool paused;
    bool stylesReady;

    public bool IsPaused => paused;

    void Awake()
    {
        ResolveMenuFont();

        darkTexture = MakeTexture(new Color(0.008f, 0.006f, 0.012f, 0.97f));
        panelTexture = MakeTexture(new Color(0.035f, 0.006f, 0.012f, 0.9f));
        goldTexture = MakeTexture(new Color(0.72f, 0.035f, 0.055f, 1f));

        uiAudio = gameObject.AddComponent<AudioSource>();
        uiAudio.playOnAwake = false;
        uiAudio.spatialBlend = 0f;
        uiAudio.ignoreListenerPause = true;
        uiAudio.loop = false;

    }

    void ResolveMenuFont()
    {
        if (menuFont != null)
            return;

        menuFont = Resources.Load<Font>("Blood Victim Zombie");
#if UNITY_EDITOR
        if (menuFont == null)
        {
            string[] fontGuids = UnityEditor.AssetDatabase.FindAssets("Blood Victim Zombie t:Font");
            if (fontGuids.Length > 0)
            {
                string fontPath = UnityEditor.AssetDatabase.GUIDToAssetPath(fontGuids[0]);
                menuFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            }
        }
#endif
    }

    void OnDestroy()
    {
        if (paused)
            ApplyPauseState(false);

        if (darkTexture != null)
            Destroy(darkTexture);
        if (panelTexture != null)
            Destroy(panelTexture);
        if (goldTexture != null)
            Destroy(goldTexture);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!pauseOnFocusLost || hasFocus)
            return;

        if (CanOpenPause())
            OpenPause();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseOnFocusLost || !pauseStatus)
            return;

        if (CanOpenPause())
            OpenPause();
    }

    void Update()
    {
        // End / game-over panel owns the screen — force-close pause if it opened.
        if (IsLevelEndPanelOpen())
        {
            if (paused)
                ForceCloseWithoutClick();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (paused)
                ResumeSilent();
            else if (CanOpenPause())
                OpenPause();
            return;
        }

        if (!paused && pauseWhenCursorLeavesWindow && CursorLeftGameWindow())
        {
            OpenPause();
            return;
        }

        if (!paused || keyboard == null)
            return;

        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            selectedItem = (selectedItem + menuItems.Length - 1) % menuItems.Length;
        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            selectedItem = (selectedItem + 1) % menuItems.Length;
        if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            ActivateMenuItem(selectedItem);
    }

    void ForceCloseWithoutClick()
    {
        paused = false;
        // Do not touch Time.timeScale / AudioListener — end panel owns freeze state.
    }

    bool CanOpenPause()
    {
        if (paused)
            return false;

        if (IsLevelEndPanelOpen())
            return false;

        return true;
    }

    static bool IsLevelEndPanelOpen()
    {
        Level1GameFlow level1 = Level1GameFlow.Instance;
        if (level1 != null && level1.IsPanelOpen)
            return true;

        Level2GameFlow level2 = Level2GameFlow.Instance;
        if (level2 != null && level2.IsPanelOpen)
            return true;

        Level3GameFlow level3 = Level3GameFlow.Instance;
        return level3 != null && level3.IsPanelOpen;
    }

    static bool CursorLeftGameWindow()
    {
        // Locked cursor stays centered — focus-loss handles that case.
        if (Cursor.lockState == CursorLockMode.Locked)
            return false;

        if (Mouse.current == null)
            return false;

        Vector2 pos = Mouse.current.position.ReadValue();
        return pos.x < 0f || pos.y < 0f || pos.x > Screen.width || pos.y > Screen.height;
    }

    void OpenPause()
    {
        if (paused)
            return;

        paused = true;
        selectedItem = 0;
        ApplyPauseState(true);
    }

    public void Resume()
    {
        if (!paused)
            return;

        PlayClick();
        paused = false;
        ApplyPauseState(false);
    }

    /// <summary>Resume without click SFX (used by ESC).</summary>
    public void ResumeSilent()
    {
        if (!paused)
            return;

        paused = false;
        ApplyPauseState(false);
    }

    void Restart()
    {
        PlayClick();
        StartCoroutine(LoadAfterClick(SceneManager.GetActiveScene().name));
    }

    void QuitToMainMenu()
    {
        PlayClick();
        string scene = string.IsNullOrWhiteSpace(mainMenuSceneName)
            ? "SplashScreen"
            : mainMenuSceneName;
        StartCoroutine(LoadAfterClick(scene));
    }

    void ActivateMenuItem(int index)
    {
        selectedItem = index;
        switch (index)
        {
            case 0:
                Resume();
                break;
            case 1:
                Restart();
                break;
            case 2:
                QuitToMainMenu();
                break;
        }
    }

    IEnumerator LoadAfterClick(string sceneName)
    {
        // Keep menu visible briefly so the click SFX can start.
        yield return new WaitForSecondsRealtime(0.08f);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(sceneName);
    }

    void ApplyPauseState(bool freeze)
    {
        Time.timeScale = freeze ? 0f : 1f;
        AudioListener.pause = freeze;

        ThirdPersonController player = FindFirstObjectByType<ThirdPersonController>();
        if (player != null)
            player.enabled = !freeze;

        if (freeze)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    void PlayClick()
    {
        if (uiAudio == null || buttonClickClip == null)
            return;

        uiAudio.PlayOneShot(buttonClickClip, buttonClickVolume);
    }

    void OnGUI()
    {
        if (!paused)
            return;

        EnsureStyles();
        DrawMenu();
    }

    void DrawMenu()
    {
        float width = Screen.width;
        float height = Screen.height;
        float scale = UiScale();
        float panelWidth = Mathf.Max(width * 0.46f, 520f * scale);

        // Soft full-screen dim behind the splash-style side panel.
        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, width, height), darkTexture);
        GUI.color = prev;

        GUI.DrawTexture(new Rect(0f, 0f, panelWidth, height), panelTexture);
        GUI.DrawTexture(new Rect(panelWidth, 0f, 2f * scale, height), goldTexture);

        titleStyle.fontSize = Mathf.RoundToInt(160f * scale);
        subtitleStyle.fontSize = Mathf.RoundToInt(22f * scale);
        GUI.Label(
            new Rect(62f * scale, 72f * scale, panelWidth - 100f * scale, 180f * scale),
            "LUNAS",
            LeftAligned(titleStyle));
        GUI.Label(
            new Rect(68f * scale, 248f * scale, panelWidth - 100f * scale, 40f * scale),
            "PAUSED",
            LeftAligned(subtitleStyle));

        float buttonY = height * 0.42f;
        float buttonHeight = 72f * scale;
        menuStyle.fontSize = Mathf.RoundToInt(38f * scale);
        selectedMenuStyle.fontSize = menuStyle.fontSize;

        for (int i = 0; i < menuItems.Length; i++)
        {
            Rect buttonRect = new Rect(
                68f * scale,
                buttonY + i * 92f * scale,
                panelWidth - 136f * scale,
                buttonHeight);

            if (buttonRect.Contains(Event.current.mousePosition))
                selectedItem = i;

            if (i == selectedItem)
                GUI.DrawTexture(
                    new Rect(buttonRect.x - 18f * scale, buttonRect.y + 14f * scale, 4f * scale, 42f * scale),
                    goldTexture);

            GUIStyle style = i == selectedItem ? selectedMenuStyle : menuStyle;
            if (GUI.Button(buttonRect, menuItems[i], style))
                ActivateMenuItem(i);
        }

        promptStyle.fontSize = Mathf.RoundToInt(15f * scale);
        GUI.Label(
            new Rect(68f * scale, height - 70f * scale, panelWidth - 100f * scale, 28f * scale),
            "W/S OR ↑/↓  SELECT     ENTER  CONFIRM     ESC  RESUME",
            LeftAligned(promptStyle));
    }

    void EnsureStyles()
    {
        if (stylesReady)
            return;

        titleStyle = CreateStyle(FontStyle.Normal, new Color(0.9f, 0.08f, 0.1f), TextAnchor.MiddleCenter);
        subtitleStyle = CreateStyle(FontStyle.Normal, new Color(0.92f, 0.78f, 0.7f), TextAnchor.MiddleCenter);
        menuStyle = CreateStyle(FontStyle.Normal, new Color(0.82f, 0.7f, 0.68f), TextAnchor.MiddleLeft);
        selectedMenuStyle = CreateStyle(FontStyle.Normal, new Color(1f, 0.2f, 0.22f), TextAnchor.MiddleLeft);
        promptStyle = CreateStyle(FontStyle.Normal, new Color(0.62f, 0.58f, 0.54f), TextAnchor.MiddleCenter);

        if (menuFont != null)
        {
            titleStyle.font = menuFont;
            subtitleStyle.font = menuFont;
            menuStyle.font = menuFont;
            selectedMenuStyle.font = menuFont;
        }

        stylesReady = true;
    }

    static GUIStyle CreateStyle(FontStyle fontStyle, Color color, TextAnchor alignment)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = alignment,
            fontStyle = fontStyle
        };
        style.normal.textColor = color;
        style.hover.textColor = new Color(0.94f, 0.79f, 0.5f);
        style.active.textColor = color;
        return style;
    }

    static GUIStyle LeftAligned(GUIStyle source)
    {
        GUIStyle copy = new GUIStyle(source);
        copy.alignment = TextAnchor.MiddleLeft;
        return copy;
    }

    static float UiScale()
    {
        return Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.65f, 1.5f);
    }

    static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}

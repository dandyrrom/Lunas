using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class SplashScreenController : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] string nextScene = "Backstory1";
    [SerializeField, Min(1f)] float displayDuration = 8f;
    [SerializeField, Min(0f)] float inputDelay = 1.1f;
    [SerializeField, Min(0.1f)] float fadeDuration = 1.4f;

    [Header("Camera")]
    [SerializeField] float cameraDrift = 0.35f;
    [SerializeField] float cameraDriftSpeed = 0.22f;

    [Header("Typography")]
    [SerializeField] Font bloodVictimZombieFont;

    [Header("Start menu music")]
    [Tooltip("Loops only while the start menu (and How To Play) is open — not during splash.")]
    [SerializeField] AudioClip menuMusicClip;
    [SerializeField, Range(0f, 1f)] float menuMusicVolume = 0.4f;
    [SerializeField, Min(0f)] float menuMusicFadeIn = 1.5f;
    [SerializeField, Min(0f)] float menuMusicFadeOut = 0.75f;

    enum ScreenState
    {
        Splash,
        Menu,
        HowToPlay,
        Loading
    }

    readonly string[] menuItems = { "BEGIN THE STORY", "HOW TO PLAY", "QUIT" };

    Texture2D darkTexture;
    Texture2D panelTexture;
    Texture2D goldTexture;
    Texture2D discTexture;
    Texture2D ringTexture;
    GUIStyle titleStyle;
    GUIStyle subtitleStyle;
    GUIStyle menuStyle;
    GUIStyle selectedMenuStyle;
    GUIStyle bodyStyle;
    GUIStyle promptStyle;
    GUIStyle markStyle;
    GUIStyle howToTitleStyle;
    GUIStyle sectionStyle;
    GUIStyle keyStyle;
    GUIStyle actionStyle;
    GUIStyle tipStyle;
    Vector3 cameraStart;
    ScreenState state;
    float stateStartedAt;
    int selectedItem;
    AudioSource menuMusicSource;
    Coroutine menuMusicFadeRoutine;

    void Awake()
    {
        state = ScreenState.Splash;
        stateStartedAt = Time.unscaledTime;
        ResolveMenuFont();
        if (Camera.main != null)
            cameraStart = Camera.main.transform.position;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        darkTexture = MakeTexture(new Color(0.008f, 0.006f, 0.012f, 0.97f));
        panelTexture = MakeTexture(new Color(0.035f, 0.006f, 0.012f, 0.9f));
        goldTexture = MakeTexture(new Color(0.86f, 0.16f, 0.18f, 1f));
        discTexture = MakeDisc(256, new Color(0.1f, 0.02f, 0.03f, 1f));
        ringTexture = MakeRing(256, new Color(0.92f, 0.78f, 0.55f, 1f), 0.78f, 0.97f);

        menuMusicSource = gameObject.GetComponent<AudioSource>();
        if (menuMusicSource == null)
            menuMusicSource = gameObject.AddComponent<AudioSource>();
        menuMusicSource.playOnAwake = false;
        menuMusicSource.loop = true;
        menuMusicSource.spatialBlend = 0f;
        menuMusicSource.volume = 0f;
    }

    void ResolveMenuFont()
    {
        if (bloodVictimZombieFont != null)
            return;

        bloodVictimZombieFont = Resources.Load<Font>("Blood Victim Zombie");
#if UNITY_EDITOR
        if (bloodVictimZombieFont == null)
        {
            string[] fontGuids = UnityEditor.AssetDatabase.FindAssets("Blood Victim Zombie t:Font");
            if (fontGuids.Length > 0)
            {
                string fontPath = UnityEditor.AssetDatabase.GUIDToAssetPath(fontGuids[0]);
                bloodVictimZombieFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            }
        }
#endif
    }

    void Update()
    {
        float elapsed = Time.unscaledTime - stateStartedAt;
        if (Camera.main != null)
        {
            Vector3 drift = Vector3.right * (Mathf.Sin(elapsed * cameraDriftSpeed) * cameraDrift);
            Camera.main.transform.position = cameraStart + drift;
        }

        Keyboard keyboard = Keyboard.current;
        if (state == ScreenState.Splash)
        {
            bool keyboardSkip = keyboard != null && keyboard.anyKey.wasPressedThisFrame;
            bool mouseSkip = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (elapsed >= displayDuration || (elapsed >= inputDelay && (keyboardSkip || mouseSkip)))
                ShowMenu();
            return;
        }

        if (state == ScreenState.Menu && keyboard != null)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                selectedItem = (selectedItem + menuItems.Length - 1) % menuItems.Length;
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                selectedItem = (selectedItem + 1) % menuItems.Length;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                ActivateMenuItem(selectedItem);
        }
        else if (state == ScreenState.HowToPlay && keyboard != null &&
                 (keyboard.escapeKey.wasPressedThisFrame ||
                  keyboard.enterKey.wasPressedThisFrame ||
                  keyboard.spaceKey.wasPressedThisFrame))
        {
            ShowMenu();
        }
    }

    void ShowMenu()
    {
        state = ScreenState.Menu;
        stateStartedAt = Time.unscaledTime;
        StartMenuMusic();
    }

    void ActivateMenuItem(int index)
    {
        selectedItem = index;
        switch (index)
        {
            case 0:
                StartCoroutine(BeginStory());
                break;
            case 1:
                state = ScreenState.HowToPlay;
                stateStartedAt = Time.unscaledTime;
                break;
            case 2:
                QuitGame();
                break;
        }
    }

    IEnumerator BeginStory()
    {
        if (state == ScreenState.Loading)
            yield break;

        state = ScreenState.Loading;
        stateStartedAt = Time.unscaledTime;
        CampaignScore.Reset();
        StopMenuMusic();
        yield return new WaitForSecondsRealtime(0.35f);

        if (Application.CanStreamedLevelBeLoaded(nextScene))
            SceneManager.LoadScene(nextScene);
        else
        {
            Debug.LogError($"Cannot load '{nextScene}'. Add the scene to Build Settings.");
            ShowMenu();
        }
    }

    void StartMenuMusic()
    {
        if (menuMusicSource == null || menuMusicClip == null)
            return;

        if (menuMusicSource.clip != menuMusicClip)
            menuMusicSource.clip = menuMusicClip;

        menuMusicSource.loop = true;
        if (!menuMusicSource.isPlaying)
            menuMusicSource.Play();

        float target = Mathf.Clamp01(menuMusicVolume);
        if (menuMusicFadeIn > 0f)
            StartMenuMusicFade(menuMusicSource.volume, target, menuMusicFadeIn, stopWhenDone: false);
        else
            menuMusicSource.volume = target;
    }

    void StopMenuMusic()
    {
        if (menuMusicSource == null || !menuMusicSource.isPlaying)
            return;

        if (menuMusicFadeOut > 0f)
            StartMenuMusicFade(menuMusicSource.volume, 0f, menuMusicFadeOut, stopWhenDone: true);
        else
        {
            StopMenuMusicFade();
            menuMusicSource.Stop();
            menuMusicSource.volume = 0f;
        }
    }

    void StartMenuMusicFade(float from, float to, float duration, bool stopWhenDone)
    {
        StopMenuMusicFade();
        menuMusicFadeRoutine = StartCoroutine(MenuMusicFadeRoutine(from, to, duration, stopWhenDone));
    }

    void StopMenuMusicFade()
    {
        if (menuMusicFadeRoutine == null)
            return;

        StopCoroutine(menuMusicFadeRoutine);
        menuMusicFadeRoutine = null;
    }

    IEnumerator MenuMusicFadeRoutine(float from, float to, float duration, bool stopWhenDone)
    {
        menuMusicSource.volume = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            menuMusicSource.volume = Mathf.Lerp(from, to, t);
            yield return null;
        }

        menuMusicSource.volume = to;
        menuMusicFadeRoutine = null;

        if (stopWhenDone && menuMusicSource.isPlaying)
            menuMusicSource.Stop();
    }

    static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnGUI()
    {
        EnsureStyles();
        switch (state)
        {
            case ScreenState.Splash:
                DrawSplash();
                break;
            case ScreenState.Menu:
                DrawMenu();
                break;
            case ScreenState.HowToPlay:
                DrawMenu();
                DrawHowToPlay();
                break;
            case ScreenState.Loading:
                DrawLoading();
                break;
        }
    }

    void DrawSplash()
    {
        float width = Screen.width;
        float height = Screen.height;
        float scale = UiScale();
        float elapsed = Time.unscaledTime - stateStartedAt;
        float fadeIn = Mathf.Clamp01(elapsed / fadeDuration);
        float fadeOut = Mathf.Clamp01((displayDuration - elapsed) / fadeDuration);
        float alpha = Mathf.Min(fadeIn, fadeOut);

        GUI.DrawTexture(new Rect(0f, 0f, width, height), darkTexture);

        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        titleStyle.fontSize = Mathf.RoundToInt(176f * scale);
        subtitleStyle.fontSize = Mathf.RoundToInt(28f * scale);
        promptStyle.fontSize = Mathf.RoundToInt(18f * scale);

        DrawMark(
            new Rect(width * 0.5f - 78f * scale, height * 0.07f, 156f * scale, 156f * scale),
            156f * scale);

        GUI.Label(
            new Rect(0f, height * 0.34f, width, 210f * scale),
            "LUNAS",
            titleStyle);
        GUI.DrawTexture(
            new Rect(width * 0.5f - 90f * scale, height * 0.56f, 180f * scale, 3f * scale),
            goldTexture);
        GUI.Label(
            new Rect(0f, height * 0.58f, width, 50f * scale),
            "THE WEDDING NIGHT",
            subtitleStyle);
        GUI.Label(
            new Rect(0f, height * 0.82f, width, 30f * scale),
            "BAWANG  •  KANDILA  •  PUKSAIN ANG MGA ASWANG",
            promptStyle);
        GUI.color = previous;
    }

    void DrawMenu()
    {
        float width = Screen.width;
        float height = Screen.height;
        float scale = UiScale();
        float panelWidth = Mathf.Max(width * 0.46f, 520f * scale);

        GUI.DrawTexture(new Rect(0f, 0f, panelWidth, height), panelTexture);
        GUI.DrawTexture(new Rect(panelWidth, 0f, 2f * scale, height), goldTexture);

        titleStyle.fontSize = Mathf.RoundToInt(160f * scale);
        subtitleStyle.fontSize = Mathf.RoundToInt(22f * scale);
        DrawMark(new Rect(68f * scale, 20f * scale, 70f * scale, 70f * scale), 70f * scale);

        GUI.Label(
            new Rect(62f * scale, 96f * scale, panelWidth - 100f * scale, 180f * scale),
            "LUNAS",
            LeftAligned(titleStyle));
        GUI.Label(
            new Rect(68f * scale, 268f * scale, panelWidth - 100f * scale, 40f * scale),
            "A FILIPINO ASWANG SURVIVAL STORY",
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
            "W/S OR ↑/↓  SELECT     ENTER  CONFIRM",
            LeftAligned(promptStyle));
    }

    void DrawHowToPlay()
    {
        float width = Screen.width;
        float height = Screen.height;
        float scale = UiScale();
        Rect box = new Rect(width * 0.48f, height * 0.08f, width * 0.46f, height * 0.84f);
        GUI.DrawTexture(box, darkTexture);

        float pad = 42f * scale;
        float contentLeft = box.x + pad;
        float contentWidth = box.width - pad * 2f;
        float keyWidth = contentWidth * 0.38f;
        float actionLeft = contentLeft + keyWidth + 18f * scale;
        float actionWidth = contentWidth - keyWidth - 18f * scale;
        float y = box.y + 28f * scale;

        howToTitleStyle.fontSize = Mathf.RoundToInt(44f * scale);
        sectionStyle.fontSize = Mathf.RoundToInt(20f * scale);
        keyStyle.fontSize = Mathf.RoundToInt(20f * scale);
        actionStyle.fontSize = Mathf.RoundToInt(20f * scale);
        tipStyle.fontSize = Mathf.RoundToInt(18f * scale);
        promptStyle.fontSize = Mathf.RoundToInt(15f * scale);

        GUI.Label(
            new Rect(contentLeft, y, contentWidth, 52f * scale),
            "HOW TO PLAY",
            howToTitleStyle);
        y += 54f * scale;
        GUI.DrawTexture(new Rect(contentLeft, y, 96f * scale, 3f * scale), goldTexture);
        y += 28f * scale;

        GUI.Label(new Rect(contentLeft, y, contentWidth, 28f * scale), "CONTROLS", sectionStyle);
        y += 34f * scale;

        string[] keys =
        {
            "WASD",
            "CTRL + WASD",
            "SHIFT",
            "SPACE",
            "MOUSE",
            "E",
            "F",
            "G"
        };
        string[] actions =
        {
            "Walk",
            "Sneak (silent)",
            "Run",
            "Jump",
            "Look",
            "Collect bawang / candle",
            "Throw bawang",
            "Throw candle"
        };

        float rowHeight = 30f * scale;
        for (int i = 0; i < keys.Length; i++)
        {
            GUI.Label(new Rect(contentLeft, y, keyWidth, rowHeight), keys[i], keyStyle);
            GUI.Label(new Rect(actionLeft, y, actionWidth, rowHeight), actions[i], actionStyle);
            y += rowHeight;
        }

        y += 18f * scale;
        GUI.DrawTexture(new Rect(contentLeft, y, contentWidth * 0.55f, 1.5f * scale), goldTexture);
        y += 20f * scale;

        GUI.Label(new Rect(contentLeft, y, contentWidth, 28f * scale), "SURVIVE", sectionStyle);
        y += 32f * scale;

        string[] tips =
        {
            "Aswang hunt by sound. Hold CTRL to sneak so they cannot hear you.",
            "Walking, running, and landing from a jump make noise.",
            "Bawang wounds an aswang. Candle fire burns one to death.",
            "Carry up to 5 bawang and 1 candle at a time."
        };

        float tipHeight = 48f * scale;
        for (int i = 0; i < tips.Length; i++)
        {
            GUI.Label(new Rect(contentLeft, y, contentWidth, tipHeight), tips[i], tipStyle);
            y += 40f * scale;
        }

        y += 8f * scale;
        GUI.Label(new Rect(contentLeft, y, contentWidth, 28f * scale), "GOAL", sectionStyle);
        y += 30f * scale;
        actionStyle.fontSize = Mathf.RoundToInt(24f * scale);
        actionStyle.normal.textColor = new Color(1f, 0.28f, 0.3f);
        GUI.Label(
            new Rect(contentLeft, y, contentWidth, 34f * scale),
            "KILL ALL THE ASWANG",
            actionStyle);
        actionStyle.fontSize = Mathf.RoundToInt(20f * scale);
        actionStyle.normal.textColor = new Color(0.94f, 0.91f, 0.86f);

        GUI.Label(
            new Rect(contentLeft, box.yMax - 52f * scale, contentWidth, 28f * scale),
            "ENTER / SPACE / ESC  BACK",
            LeftAligned(promptStyle));
    }

    void DrawLoading()
    {
        float scale = UiScale();
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), darkTexture);
        subtitleStyle.fontSize = Mathf.RoundToInt(40f * scale);
        GUI.Label(
            new Rect(0f, Screen.height * 0.46f, Screen.width, 70f * scale),
            "LOADING...",
            subtitleStyle);
    }

    void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = CreateStyle(FontStyle.Normal, new Color(0.9f, 0.08f, 0.1f), TextAnchor.MiddleCenter);
        subtitleStyle = CreateStyle(FontStyle.Normal, new Color(0.92f, 0.78f, 0.7f), TextAnchor.MiddleCenter);
        menuStyle = CreateStyle(FontStyle.Normal, new Color(0.82f, 0.7f, 0.68f), TextAnchor.MiddleLeft);
        selectedMenuStyle = CreateStyle(FontStyle.Normal, new Color(1f, 0.2f, 0.22f), TextAnchor.MiddleLeft);
        bodyStyle = CreateStyle(FontStyle.Normal, new Color(0.94f, 0.91f, 0.86f), TextAnchor.UpperLeft);
        bodyStyle.wordWrap = true;
        promptStyle = CreateStyle(FontStyle.Normal, new Color(0.62f, 0.58f, 0.54f), TextAnchor.MiddleCenter);
        markStyle = CreateStyle(FontStyle.Bold, new Color(0.95f, 0.12f, 0.14f), TextAnchor.MiddleCenter);
        howToTitleStyle = CreateStyle(FontStyle.Normal, new Color(0.9f, 0.08f, 0.1f), TextAnchor.MiddleLeft);
        sectionStyle = CreateStyle(FontStyle.Normal, new Color(1f, 0.28f, 0.3f), TextAnchor.MiddleLeft);
        keyStyle = CreateStyle(FontStyle.Bold, new Color(0.94f, 0.79f, 0.5f), TextAnchor.MiddleLeft);
        actionStyle = CreateStyle(FontStyle.Normal, new Color(0.94f, 0.91f, 0.86f), TextAnchor.MiddleLeft);
        tipStyle = CreateStyle(FontStyle.Normal, new Color(0.86f, 0.8f, 0.76f), TextAnchor.UpperLeft);
        tipStyle.wordWrap = true;

        if (bloodVictimZombieFont != null)
        {
            titleStyle.font = bloodVictimZombieFont;
            subtitleStyle.font = bloodVictimZombieFont;
            menuStyle.font = bloodVictimZombieFont;
            selectedMenuStyle.font = bloodVictimZombieFont;
            markStyle.font = bloodVictimZombieFont;
            howToTitleStyle.font = bloodVictimZombieFont;
            sectionStyle.font = bloodVictimZombieFont;
        }
    }

    void DrawMark(Rect area, float size)
    {
        if (discTexture != null)
            GUI.DrawTexture(area, discTexture, ScaleMode.ScaleToFit, true);
        if (ringTexture != null)
            GUI.DrawTexture(area, ringTexture, ScaleMode.ScaleToFit, true);

        float cx = area.x + area.width * 0.5f;
        float cy = area.y + area.height * 0.52f;
        GUI.DrawTexture(new Rect(cx - size * 0.018f, cy - size * 0.28f, size * 0.036f, size * 0.2f), goldTexture);
        GUI.DrawTexture(new Rect(cx - size * 0.028f, cy - size * 0.34f, size * 0.056f, size * 0.06f), goldTexture);
        if (markStyle != null)
        {
            markStyle.fontSize = Mathf.RoundToInt(size * 0.52f);
            GUI.Label(area, "L", markStyle);
        }
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

    static Texture2D MakeDisc(int size, Color color)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        float center = (size - 1) * 0.5f;
        float radius = center - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float t = dx * dx + dy * dy;
                texture.SetPixel(x, y, t <= radius * radius ? color : Color.clear);
            }
        }

        texture.Apply();
        return texture;
    }

    static Texture2D MakeRing(int size, Color color, float inner, float outer)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        float center = (size - 1) * 0.5f;
        float outerR = center * outer;
        float innerR = center * inner;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                texture.SetPixel(x, y, d <= outerR && d >= innerR ? color : Color.clear);
            }
        }

        texture.Apply();
        return texture;
    }

    void OnDestroy()
    {
        StopMenuMusicFade();
        if (menuMusicSource != null && menuMusicSource.isPlaying)
            menuMusicSource.Stop();

        if (darkTexture != null)
            Destroy(darkTexture);
        if (panelTexture != null)
            Destroy(panelTexture);
        if (goldTexture != null)
            Destroy(goldTexture);
        if (discTexture != null)
            Destroy(discTexture);
        if (ringTexture != null)
            Destroy(ringTexture);
    }
}

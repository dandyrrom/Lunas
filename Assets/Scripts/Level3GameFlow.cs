using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Level 3 (old sea port): defeat the boss to finish.
/// SPACE on a clear loads the ending shore. SPACE on game over returns to the splash menu.
/// </summary>
public class Level3GameFlow : MonoBehaviour
{
    public static Level3GameFlow Instance { get; private set; }

    [Header("Continue")]
    [Tooltip("Loaded after SPACE on a boss clear.")]
    public string continueSceneName = LevelChain.Ending;
    [Tooltip("Loaded after SPACE on game over. She starts the story again from the menu.")]
    public string gameOverSceneName = LevelChain.Splash;

    [Header("Score")]
    public int perfectScore = 250;
    public int missedThrowPenalty = 10;
    public int lifeLostPenalty = 25;

    [Tooltip("Seconds to wait after the boss death animation finishes, before the clear panel.")]
    public float victoryDelay = 3.5f;

    public bool IsPanelOpen => panelOpen;

    GameObject endPanel;
    TMP_Text endTitleText;
    TMP_Text endBodyText;
    Image endDimmer;
    bool panelOpen;
    bool waitingForSpace;
    bool endedInVictory;
    bool victoryQueued;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void Start()
    {
        BuildEndPanelIfNeeded();
    }

    void Update()
    {
        if (!waitingForSpace || !WasContinuePressed())
            return;

        waitingForSpace = false;
        StartCoroutine(ContinueRoutine());
    }

    public void NotifyBossDefeated()
    {
        if (panelOpen || victoryQueued)
            return;

        victoryQueued = true;
        StartCoroutine(VictoryAfterDelay());
    }

    public void NotifyGameOver()
    {
        if (panelOpen)
            return;

        ShowEndPanel(victory: false);
    }

    IEnumerator VictoryAfterDelay()
    {
        float wait = Mathf.Max(0f, victoryDelay);
        float elapsed = 0f;
        while (elapsed < wait)
        {
            if (panelOpen)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!panelOpen)
            ShowEndPanel(victory: true);
    }

    void ShowEndPanel(bool victory)
    {
        panelOpen = true;
        waitingForSpace = true;
        endedInVictory = victory;
        victoryQueued = false;

        PlayModeBackgroundMusic bgm = FindFirstObjectByType<PlayModeBackgroundMusic>();
        if (bgm != null)
            bgm.NotifyLevelEnded();

        FreezeGameplay(true);

        int killed = 0;
        int total = 0;
        int misses = 0;
        int livesLeft = 0;
        int maxLives = 0;
        ZombieKillScore killScore = FindFirstObjectByType<ZombieKillScore>();
        PlayerLives lives = FindFirstObjectByType<PlayerLives>();
        Level1RunStats runStats = FindFirstObjectByType<Level1RunStats>();
        if (killScore != null)
        {
            killed = killScore.Killed;
            total = killScore.Total;
        }

        if (lives != null)
        {
            livesLeft = lives.CurrentLives;
            maxLives = lives.MaxLives;
        }

        if (runStats != null)
            misses = runStats.MissedThrows;

        int totalScore = runStats != null
            ? runStats.ComputeTotalScore(
                killed, total, livesLeft, maxLives,
                perfectScore, missedThrowPenalty, lifeLostPenalty)
            : Mathf.Max(0, Mathf.RoundToInt(perfectScore * (total > 0 ? (float)killed / total : 1f))
                - misses * missedThrowPenalty
                - Mathf.Max(0, maxLives - livesLeft) * lifeLostPenalty);

        if (victory)
            CampaignScore.Record("Level 3", killed, total, misses, livesLeft, totalScore);

        if (endTitleText != null)
            endTitleText.text = victory ? "The port falls quiet" : "Taken by the night";

        if (endBodyText != null)
        {
            endBodyText.text = victory
                ? "Morning finds the shore. A boat is coming in.\n\nPress SPACE"
                : "The sea takes her before the hunt is finished.\n\nPress SPACE to return to the menu";
        }

        if (endPanel != null)
            endPanel.SetActive(true);
    }

    IEnumerator ContinueRoutine()
    {
        if (endDimmer != null)
        {
            float t = 0f;
            Color c = endDimmer.color;
            while (t < 0.6f)
            {
                t += Time.unscaledDeltaTime;
                c.a = Mathf.Lerp(0.75f, 1f, t / 0.6f);
                endDimmer.color = c;
                yield return null;
            }
        }

        string sceneName = endedInVictory
            ? (string.IsNullOrWhiteSpace(continueSceneName) ? LevelChain.Ending : continueSceneName.Trim())
            : (string.IsNullOrWhiteSpace(gameOverSceneName) ? LevelChain.Splash : gameOverSceneName.Trim());

        if (!LevelChain.TryLoad(sceneName))
            waitingForSpace = true;
    }

    void FreezeGameplay(bool freeze)
    {
        Time.timeScale = freeze ? 0f : 1f;
        AudioListener.pause = freeze;

        ThirdPersonController player = FindFirstObjectByType<ThirdPersonController>();
        if (player != null)
            player.enabled = !freeze;

        Cursor.visible = freeze;
        Cursor.lockState = freeze ? CursorLockMode.None : CursorLockMode.Locked;
    }

    static bool WasContinuePressed()
    {
        return Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame ||
             Keyboard.current.enterKey.wasPressedThisFrame);
    }

    void BuildEndPanelIfNeeded()
    {
        Canvas hudCanvas = null;
        GameObject hud = GameObject.Find("HUD");
        if (hud != null)
            hudCanvas = hud.GetComponent<Canvas>();

        if (hudCanvas == null)
        {
            GameObject canvasGo = new GameObject(
                "Level3EndCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            hudCanvas = canvasGo.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 500;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        endPanel = new GameObject("Level3EndPanel", typeof(RectTransform), typeof(Image));
        endPanel.transform.SetParent(hudCanvas.transform, false);
        RectTransform panelRt = endPanel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        endDimmer = endPanel.GetComponent<Image>();
        endDimmer.color = new Color(0f, 0f, 0f, 0.75f);
        endDimmer.raycastTarget = true;

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        GameObject titleGo = new GameObject("EndTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(endPanel.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0f, 80f);
        titleRt.sizeDelta = new Vector2(900f, 60f);
        endTitleText = titleGo.GetComponent<TextMeshProUGUI>();
        endTitleText.font = font;
        endTitleText.fontSize = 32;
        endTitleText.alignment = TextAlignmentOptions.Center;
        endTitleText.color = Color.white;
        endTitleText.fontStyle = FontStyles.Bold;

        GameObject bodyGo = new GameObject("EndBody", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyGo.transform.SetParent(endPanel.transform, false);
        RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.5f, 0.5f);
        bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRt.pivot = new Vector2(0.5f, 0.5f);
        bodyRt.anchoredPosition = new Vector2(0f, -20f);
        bodyRt.sizeDelta = new Vector2(760f, 180f);
        endBodyText = bodyGo.GetComponent<TextMeshProUGUI>();
        endBodyText.font = font;
        endBodyText.fontSize = 22;
        endBodyText.alignment = TextAlignmentOptions.Center;
        endBodyText.color = Color.white;

        endPanel.SetActive(false);
        endPanel.transform.SetAsLastSibling();
    }
}

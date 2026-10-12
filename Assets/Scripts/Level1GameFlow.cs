using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Level 1 objective flip: kill all aswangs → looping church bell → go to bloody krus → end panel.
/// Game over panel when lives run out before clearing.
/// ObjectiveText (phase A) and GoalText (phase B) are separate scene objects you can style.
/// </summary>
public class Level1GameFlow : MonoBehaviour
{
    public static Level1GameFlow Instance { get; private set; }

    [Header("Objectives")]
    [Tooltip("Phase A HUD text (Kill all aswangs). Edit font/position on this object.")]
    public TMP_Text objectiveText;
    [Tooltip("Phase B HUD text (bloody krus). Edit font/position on this object.")]
    public TMP_Text goalText;

    [Header("Objective intro (ObjectiveText)")]
    [Tooltip("Beat ObjectiveText at level start to announce the objective, then hide it.")]
    public bool beatObjectiveOnStart = true;
    [Tooltip("How many seconds ObjectiveText stays visible (beating), then disappears.")]
    public float objectiveIntroDuration = 4f;

    [Header("Goal beat (GoalText / Phase B)")]
    public bool beatGoalInPhaseB = true;
    public float beatSpeed = 6.5f;
    [Range(0f, 0.35f)] public float beatScaleAmount = 0.2f;
    [Range(0.15f, 1f)] public float beatAlphaMin = 0.35f;

    [Header("Audio")]
    public AudioClip churchBellClip;
    [Range(0f, 1f)] public float churchBellVolume = 0.85f;

    [Header("Score")]
    [Tooltip("Perfect clear score (all aswangs, 0 missed throws, all lives).")]
    public int perfectScore = 200;
    public int missedThrowPenalty = 10;
    public int lifeLostPenalty = 25;

    [Header("Continue")]
    [Tooltip("Scene loaded after SPACE on the end / game-over panel.")]
    public string continueSceneName = "Level2";

    [Header("Krus Marker")]
    public Vector3 krusTriggerSize = new Vector3(2.5f, 3f, 2.5f);

    [Header("Optional refs (auto-found if empty)")]
    public ZombieKillScore killScore;
    public PlayerLives lives;
    public Level1RunStats runStats;
    public Canvas hudCanvas;
    public Transform krusAnchor;

    GameObject endPanel;
    TMP_Text endTitleText;
    TMP_Text endBodyText;
    Image endDimmer;

    AudioSource bellSource;
    GameObject krusMarker;
    bool phaseB;
    bool panelOpen;
    bool waitingForSpace;
    bool beatingObjective;
    bool beatingGoal;
    Coroutine objectiveIntroRoutine;

    Vector3 objectiveBaseScale = Vector3.one;
    Color objectiveBaseColor = Color.white;
    Vector3 goalBaseScale = Vector3.one;
    Color goalBaseColor = Color.white;

    public bool IsPhaseB => phaseB;
    public bool IsPanelOpen => panelOpen;

    void Awake()
    {
        Instance = this;

        if (killScore == null)
            killScore = FindFirstObjectByType<ZombieKillScore>();
        if (lives == null)
            lives = FindFirstObjectByType<PlayerLives>();
        if (runStats == null)
            runStats = FindFirstObjectByType<Level1RunStats>();
        if (runStats == null)
            runStats = gameObject.AddComponent<Level1RunStats>();

        if (hudCanvas == null)
        {
            GameObject hud = GameObject.Find("HUD");
            if (hud != null)
                hudCanvas = hud.GetComponent<Canvas>();
        }

        if (objectiveText == null)
        {
            GameObject existing = GameObject.Find("ObjectiveText");
            if (existing != null)
                objectiveText = existing.GetComponent<TMP_Text>();
        }

        if (goalText == null)
        {
            GameObject existing = GameObject.Find("GoalText");
            if (existing != null)
                goalText = existing.GetComponent<TMP_Text>();
        }

        bellSource = gameObject.AddComponent<AudioSource>();
        bellSource.playOnAwake = false;
        bellSource.spatialBlend = 0f;
        bellSource.loop = true;
        bellSource.volume = churchBellVolume;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        StopChurchBell();
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void OnEnable()
    {
        if (killScore != null)
            killScore.OnScoreChanged += HandleScoreChanged;
    }

    void OnDisable()
    {
        if (killScore != null)
            killScore.OnScoreChanged -= HandleScoreChanged;
    }

    void Start()
    {
        BuildEndPanelIfNeeded();
        SetupKrusMarker();
        ShowPhaseATexts();
        CacheObjectiveVisuals();
        if (krusMarker != null)
            krusMarker.SetActive(false);

        if (objectiveIntroRoutine != null)
            StopCoroutine(objectiveIntroRoutine);
        objectiveIntroRoutine = StartCoroutine(ObjectiveIntroRoutine());
    }

    void Update()
    {
        UpdateObjectiveBeat();
        UpdateGoalBeat();

        if (!waitingForSpace || Keyboard.current == null)
            return;

        if (!Keyboard.current.spaceKey.wasPressedThisFrame)
            return;

        waitingForSpace = false;
        StartCoroutine(ContinueRoutine());
    }

    void HandleScoreChanged(int killed, int total)
    {
        if (phaseB || panelOpen)
            return;

        if (total <= 0 || killed < total)
            return;

        EnterPhaseB();
    }

    void EnterPhaseB()
    {
        if (phaseB)
            return;

        phaseB = true;
        StopObjectiveIntro();
        StartChurchBellLoop();
        ShowPhaseBTexts();
        CacheGoalVisuals();
        beatingGoal = beatGoalInPhaseB;

        if (krusMarker != null)
            krusMarker.SetActive(true);
    }

    void ShowPhaseATexts()
    {
        SetTextActive(objectiveText, true);
        SetTextActive(goalText, false);
        beatingObjective = false;
        beatingGoal = false;
    }

    void ShowPhaseBTexts()
    {
        RestoreObjectiveVisuals();
        SetTextActive(objectiveText, false);
        SetTextActive(goalText, true);
    }

    IEnumerator ObjectiveIntroRoutine()
    {
        if (objectiveText == null)
            yield break;

        SetTextActive(objectiveText, true);
        beatingObjective = beatObjectiveOnStart;

        float duration = Mathf.Max(0f, objectiveIntroDuration);
        float elapsed = 0f;
        while (elapsed < duration && !phaseB && !panelOpen)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        beatingObjective = false;
        RestoreObjectiveVisuals();

        if (!phaseB && !panelOpen)
            SetTextActive(objectiveText, false);

        objectiveIntroRoutine = null;
    }

    void StopObjectiveIntro()
    {
        beatingObjective = false;
        if (objectiveIntroRoutine != null)
        {
            StopCoroutine(objectiveIntroRoutine);
            objectiveIntroRoutine = null;
        }

        RestoreObjectiveVisuals();
    }

    static void SetTextActive(TMP_Text text, bool active)
    {
        if (text != null)
            text.gameObject.SetActive(active);
    }

    void StartChurchBellLoop()
    {
        if (bellSource == null || churchBellClip == null)
            return;

        bellSource.clip = churchBellClip;
        bellSource.loop = true;
        bellSource.volume = churchBellVolume;
        if (!bellSource.isPlaying)
            bellSource.Play();
    }

    void StopChurchBell()
    {
        if (bellSource == null)
            return;

        if (bellSource.isPlaying)
            bellSource.Stop();
        bellSource.loop = false;
    }

    public void NotifyKrusReached()
    {
        if (!phaseB || panelOpen)
            return;

        ShowEndPanel(victory: true);
    }

    public void NotifyGameOver()
    {
        if (panelOpen)
            return;

        ShowEndPanel(victory: false);
    }

    void ShowEndPanel(bool victory)
    {
        panelOpen = true;
        waitingForSpace = true;
        beatingGoal = false;
        StopObjectiveIntro();
        StopChurchBell();
        RestoreGoalVisuals();
        SetTextActive(objectiveText, false);
        SetTextActive(goalText, false);

        PlayModeBackgroundMusic bgm = FindFirstObjectByType<PlayModeBackgroundMusic>();
        if (bgm != null)
            bgm.NotifyLevelEnded();

        FreezeGameplay(true);

        int killed = killScore != null ? killScore.Killed : 0;
        int total = killScore != null ? killScore.Total : 0;
        int misses = runStats != null ? runStats.MissedThrows : 0;
        int livesLeft = lives != null ? lives.CurrentLives : 0;
        int maxLives = lives != null ? lives.MaxLives : 0;
        int totalScore = runStats != null
            ? runStats.ComputeTotalScore(
                killed, total, livesLeft, maxLives,
                perfectScore, missedThrowPenalty, lifeLostPenalty)
            : 0;

        if (endTitleText != null)
            endTitleText.text = victory ? "Level 1 Complete" : "Game Over";

        if (endBodyText != null)
        {
            if (victory)
            {
                endBodyText.text =
                    $"Aswang killed: {killed}\n" +
                    $"Missed throws: {misses}\n" +
                    $"Remaining lives: {livesLeft}\n" +
                    $"Total score: {totalScore}\n\n" +
                    "Press SPACE to continue";
            }
            else
            {
                endBodyText.text =
                    $"Aswang killed: {killed}\n" +
                    $"Missed throws: {misses}\n" +
                    $"Remaining lives: {livesLeft}\n" +
                    $"Total score: {totalScore}\n\n" +
                    "Press SPACE to continue";
            }
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

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (!string.IsNullOrWhiteSpace(continueSceneName))
            SceneManager.LoadScene(continueSceneName);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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

    void CacheObjectiveVisuals()
    {
        if (objectiveText == null)
            return;

        objectiveBaseScale = objectiveText.rectTransform.localScale;
        if (objectiveBaseScale.sqrMagnitude < 0.0001f)
            objectiveBaseScale = Vector3.one;

        objectiveBaseColor = objectiveText.color;
    }

    void RestoreObjectiveVisuals()
    {
        if (objectiveText == null || !objectiveText.gameObject.activeInHierarchy)
            return;

        objectiveText.rectTransform.localScale = objectiveBaseScale;
        objectiveText.color = objectiveBaseColor;
    }

    void CacheGoalVisuals()
    {
        if (goalText == null)
            return;

        goalBaseScale = goalText.rectTransform.localScale;
        if (goalBaseScale.sqrMagnitude < 0.0001f)
            goalBaseScale = Vector3.one;

        goalBaseColor = goalText.color;
    }

    void RestoreGoalVisuals()
    {
        if (goalText == null)
            return;

        goalText.rectTransform.localScale = goalBaseScale;
        goalText.color = goalBaseColor;
    }

    void UpdateObjectiveBeat()
    {
        if (!beatingObjective || objectiveText == null || panelOpen || phaseB)
            return;

        ApplyBeat(objectiveText, objectiveBaseScale, objectiveBaseColor);
    }

    void UpdateGoalBeat()
    {
        if (!beatingGoal || goalText == null || panelOpen)
            return;

        ApplyBeat(goalText, goalBaseScale, goalBaseColor);
    }

    void ApplyBeat(TMP_Text text, Vector3 baseScale, Color baseColor)
    {
        float wave = (Mathf.Sin(Time.time * beatSpeed) + 1f) * 0.5f;
        float scale = 1f + wave * beatScaleAmount;
        text.rectTransform.localScale = baseScale * scale;

        Color c = baseColor;
        c.a = Mathf.Lerp(beatAlphaMin, baseColor.a, wave);
        text.color = c;
    }

    void SetupKrusMarker()
    {
        Transform anchor = krusAnchor;
        if (anchor == null)
            anchor = FindBloodyKrusAnchor();

        if (anchor == null)
        {
            Debug.LogWarning("Level1GameFlow: Cross Arch not found — place krusAnchor manually.");
            return;
        }

        krusMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        krusMarker.name = "KrusObjectiveMarker";
        krusMarker.transform.SetParent(anchor, false);
        krusMarker.transform.localPosition = Vector3.zero;
        krusMarker.transform.localRotation = Quaternion.identity;

        Vector3 lossy = anchor.lossyScale;
        float sx = Mathf.Abs(lossy.x) < 0.01f ? 1f : lossy.x;
        float sy = Mathf.Abs(lossy.y) < 0.01f ? 1f : lossy.y;
        float sz = Mathf.Abs(lossy.z) < 0.01f ? 1f : lossy.z;
        krusMarker.transform.localScale = new Vector3(
            krusTriggerSize.x / Mathf.Abs(sx),
            krusTriggerSize.y / Mathf.Abs(sy),
            krusTriggerSize.z / Mathf.Abs(sz));

        Collider col = krusMarker.GetComponent<Collider>();
        col.isTrigger = true;

        MeshRenderer mr = krusMarker.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.enabled = false;

        // Drop the default mesh so Play Mode never shows a solid cube.
        MeshFilter mf = krusMarker.GetComponent<MeshFilter>();
        if (mf != null)
            Destroy(mf);

        KrusObjectiveTrigger trigger = krusMarker.AddComponent<KrusObjectiveTrigger>();
        trigger.flow = this;
    }

    Transform FindBloodyKrusAnchor()
    {
        GameObject church = GameObject.Find("Church 2 Open");
        Vector3 churchPos = church != null ? church.transform.position : Vector3.zero;

        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t == null || !t.name.StartsWith("Cross Arch"))
                continue;

            float dist = church != null
                ? (t.position - churchPos).sqrMagnitude
                : t.position.sqrMagnitude;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = t;
            }
        }

        return best;
    }

    void BuildEndPanelIfNeeded()
    {
        if (hudCanvas == null)
            return;

        TMP_FontAsset font = null;
        if (objectiveText != null && objectiveText.font != null)
            font = objectiveText.font;
        else if (goalText != null && goalText.font != null)
            font = goalText.font;
        else
            font = TMP_Settings.defaultFontAsset;

        endPanel = new GameObject("Level1EndPanel", typeof(RectTransform), typeof(Image));
        endPanel.transform.SetParent(hudCanvas.transform, false);
        RectTransform panelRt = endPanel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        endDimmer = endPanel.GetComponent<Image>();
        endDimmer.color = new Color(0f, 0f, 0f, 0.75f);
        endDimmer.raycastTarget = true;

        GameObject titleGo = new GameObject("EndTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(endPanel.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0f, 90f);
        titleRt.sizeDelta = new Vector2(600f, 50f);
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
        bodyRt.sizeDelta = new Vector2(520f, 220f);
        endBodyText = bodyGo.GetComponent<TextMeshProUGUI>();
        endBodyText.font = font;
        endBodyText.fontSize = 20;
        endBodyText.alignment = TextAlignmentOptions.Center;
        endBodyText.color = Color.white;
        endBodyText.lineSpacing = 8f;

        endPanel.SetActive(false);
        endPanel.transform.SetAsLastSibling();
    }
}

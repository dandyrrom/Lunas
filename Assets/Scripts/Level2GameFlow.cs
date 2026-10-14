using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Level 2 finals flow: escape the aswang village to the shore/boat exit → Level 3 port.
/// Default is escape mode (exit open; kills optional). Kill-all mode still available if disabled.
/// </summary>
public class Level2GameFlow : MonoBehaviour
{
    public static Level2GameFlow Instance { get; private set; }

    [Header("Objectives")]
    [Tooltip("Intro / main objective text (e.g. reach the boat / port).")]
    public TMP_Text objectiveText;
    [Tooltip("Optional secondary goal text.")]
    public TMP_Text goalText;

    [Header("Escape mode")]
    [Tooltip("If on: exit is open from the start; she does not need to kill all aswangs.")]
    public bool escapeMode = true;
    [Tooltip("Spawn a dense ambush horde at level start.")]
    public bool spawnAmbushHorde = true;

    [Header("Objective intro (ObjectiveText)")]
    [Tooltip("Beat ObjectiveText at level start to announce the objective, then fade it out.")]
    public bool beatObjectiveOnStart = true;
    [Tooltip("How many seconds ObjectiveText stays fully visible before fading.")]
    public float objectiveIntroDuration = 6.5f;
    [Tooltip("How many seconds the objective text takes to fade out.")]
    public float objectiveFadeOutDuration = 1.75f;

    [Header("Escape narration (written into ObjectiveText at start)")]
    [TextArea(2, 5)]
    public string escapeObjectiveTitle = "The wedding is gone — reach the boat";
    [TextArea(3, 10)]
    public string escapeObjectiveInstructions =
        "At the altar he said “I do”… then the aswang took the night.\n" +
        "The church grounds are behind you. This stolen village stands between you and him.\n" +
        "You don't need to kill them all — sneak, use garlic and the candle,\n" +
        "and reach the shore boat before dawn finds you first.";

    [Header("Escape timer")]
    [Tooltip("Countdown pressure to reach the shore boat. Off = no time limit.")]
    public bool useEscapeTimer = true;
    [Tooltip("Seconds to reach the boat before the night closes in.")]
    public float escapeTimeLimit = 180f;
    [Tooltip("Timer text turns warning color at/under this many seconds.")]
    public float timerWarnSeconds = 60f;
    [Tooltip("Timer text turns critical color at/under this many seconds.")]
    public float timerCriticalSeconds = 30f;
    [Tooltip("Bonus score points per whole second left when she reaches the boat.")]
    public int timeBonusPerSecond = 1;

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
    [Tooltip("Scene loaded after SPACE on a victory panel (boat reached).")]
    public string continueSceneName = "Level3";
    [Tooltip("Scene loaded after SPACE on game over. Empty = reload this Level2.")]
    public string retrySceneName = "";

    [Header("Exit Marker")]
    [Tooltip("Fallback trigger size; overridden to fit the shore boat at start.")]
    public Vector3 exitTriggerSize = new Vector3(4f, 3f, 6f);
    [Tooltip("Unused for finish now — kept for inspector compatibility. Finish is boat-hull only.")]
    public float exitProximityRadius = 2f;
    [Tooltip("Debug only: show a green cube at the exit. Keep off for the real look.")]
    public bool showExitMarkerVisually = false;
    [Tooltip("At start, move Level2ExitAnchor onto the nearest boat mesh.")]
    public bool snapExitAnchorToBoat = true;
    [Tooltip("Finish only when she stands inside the preferred shore boat hull.")]
    public bool finishWhenStandingInBoat = true;
    [Tooltip("Tiny padding around the shore boat mesh (keep small so nearby paths don't finish).")]
    public float boatBoundsPadding = 0.35f;

    [Header("Optional refs (auto-found if empty)")]
    public ZombieKillScore killScore;
    public PlayerLives lives;
    public Level1RunStats runStats;
    public Canvas hudCanvas;
    public Transform exitAnchor;

    GameObject endPanel;
    TMP_Text endTitleText;
    TMP_Text endBodyText;
    Image endDimmer;
    TMP_Text timerText;

    AudioSource bellSource;
    GameObject exitMarker;
    bool phaseB;
    bool panelOpen;
    bool waitingForSpace;
    bool lastVictory;
    bool beatingObjective;
    bool beatingGoal;
    bool timerRunning;
    float timeRemaining;
    Coroutine objectiveIntroRoutine;
    readonly System.Collections.Generic.List<Renderer> boatRenderers = new System.Collections.Generic.List<Renderer>(16);
    Transform preferredBoat;

    Vector3 objectiveBaseScale = Vector3.one;
    Color objectiveBaseColor = Color.white;
    Vector3 goalBaseScale = Vector3.one;
    Color goalBaseColor = Color.white;

    public bool IsPhaseB => phaseB;
    public bool IsPanelOpen => panelOpen;
    /// <summary>Exit is usable in escape mode from the start, or after kill-all Phase B.</summary>
    public bool IsExitOpen => escapeMode || phaseB;

    void Awake()
    {
        Instance = this;

        if (spawnAmbushHorde)
            EnsureAmbushHorde();

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

    void EnsureAmbushHorde()
    {
        Level2AmbushSpawner spawner = GetComponent<Level2AmbushSpawner>();
        if (spawner == null)
            spawner = gameObject.AddComponent<Level2AmbushSpawner>();
        spawner.SpawnHorde();
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
        BuildTimerHudIfNeeded();
        CacheBoatGoals();
        if (snapExitAnchorToBoat)
            SnapExitAnchorToNearestBoat();
        SetupExitMarker();
        ShowPhaseATexts();
        CacheObjectiveVisuals();

        if (escapeMode)
        {
            // Escape gauntlet: exit is live immediately. Kills are optional pressure/score.
            if (exitMarker != null)
                exitMarker.SetActive(true);
            phaseB = true;
            beatingGoal = false;
            CacheGoalVisuals();
            if (goalText != null)
                SetTextActive(goalText, false);
            ApplyEscapeObjectiveCopy();
        }
        else if (exitMarker != null)
        {
            exitMarker.SetActive(false);
        }

        CacheObjectiveVisuals();
        BeginEscapeTimer();

        if (objectiveIntroRoutine != null)
            StopCoroutine(objectiveIntroRoutine);
        objectiveIntroRoutine = StartCoroutine(ObjectiveIntroRoutine());
    }

    void BeginEscapeTimer()
    {
        timerRunning = useEscapeTimer && escapeMode;
        timeRemaining = Mathf.Max(1f, escapeTimeLimit);
        UpdateTimerHud();
        if (timerText != null)
            timerText.gameObject.SetActive(timerRunning);
    }

    void CacheBoatGoals()
    {
        boatRenderers.Clear();
        preferredBoat = null;
        float bestScore = float.NegativeInfinity;
        Vector3 hint = exitAnchor != null
            ? exitAnchor.position
            : new Vector3(-74f, 2.5f, 34f);

        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r == null || !IsBoatTransform(r.transform))
                continue;

            // Skip empty organizer parents named "Boats" with no real mesh volume.
            if (r.bounds.size.sqrMagnitude < 0.01f)
                continue;

            boatRenderers.Add(r);

            Transform t = r.transform;
            float dist = Vector3.Distance(r.bounds.center, hint);
            int depth = 0;
            for (Transform p = t.parent; p != null && depth < 8; p = p.parent)
                depth++;

            float score = -dist - depth * 2f;
            string n = t.name.ToLowerInvariant();
            if (n.Contains("build_boat") || n.Contains("pf_build_boat"))
                score += 12f;

            if (score > bestScore)
            {
                bestScore = score;
                preferredBoat = t;
            }
        }

        if (preferredBoat != null)
            Debug.Log($"Level2GameFlow: preferred boat '{preferredBoat.name}' ({boatRenderers.Count} boat meshes cached)");
    }

    void SnapExitAnchorToNearestBoat()
    {
        EnsureExitAnchorReference();
        if (exitAnchor == null)
            return;

        if (preferredBoat == null)
            preferredBoat = FindPreferredBoat();
        if (preferredBoat == null)
            return;

        Bounds b = GetBoatBounds(preferredBoat);
        Vector3 p = b.center;
        // Keep the trigger centered on the deck, slightly above the hull floor.
        p.y = Mathf.Max(b.min.y + 1.25f, b.center.y);
        exitAnchor.position = p;

        // Tight trigger = boat hull only (small pad). Paths beside the boat must not finish.
        float pad = Mathf.Max(0.2f, boatBoundsPadding);
        exitTriggerSize = new Vector3(
            Mathf.Clamp(b.size.x + pad * 2f, 2.5f, 8f),
            Mathf.Clamp(b.size.y + pad * 2f, 2f, 5f),
            Mathf.Clamp(b.size.z + pad * 2f, 3f, 10f));

        Debug.Log($"Level2GameFlow: exit snapped to boat '{preferredBoat.name}' at {exitAnchor.position}, trigger {exitTriggerSize}");
    }

    void EnsureExitAnchorReference()
    {
        if (exitAnchor != null)
            return;

        GameObject named = GameObject.Find("Level2ExitAnchor");
        if (named != null)
            exitAnchor = named.transform;
    }

    Transform FindPreferredBoat()
    {
        if (preferredBoat != null)
            return preferredBoat;

        CacheBoatGoals();
        return preferredBoat;
    }

    static Bounds GetBoatBounds(Transform boat)
    {
        Renderer[] renderers = boat.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return new Bounds(boat.position, Vector3.one * 4f);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                b.Encapsulate(renderers[i].bounds);
        }

        return b;
    }

    static bool IsBoatTransform(Transform t)
    {
        if (t == null)
            return false;

        string n = t.name.ToLowerInvariant();
        // Ignore organizer empties like "Boats" / "boats_group".
        if (n == "boats" || n == "boat" || n.EndsWith("_boats"))
            return false;

        return n.Contains("build_boat")
            || n.Contains("pf_build_boat")
            || n.Contains("banca")
            || n.Contains("canoe")
            || (n.Contains("boat") && t.GetComponentInChildren<Renderer>() != null);
    }

    void ApplyEscapeObjectiveCopy()
    {
        if (objectiveText == null)
            return;

        string title = string.IsNullOrWhiteSpace(escapeObjectiveTitle)
            ? "The wedding is gone — reach the boat"
            : escapeObjectiveTitle.Trim();
        string body = string.IsNullOrWhiteSpace(escapeObjectiveInstructions)
            ? string.Empty
            : escapeObjectiveInstructions.Trim();

        objectiveText.text = string.IsNullOrEmpty(body) ? title : (title + "\n\n" + body);
        objectiveText.enableWordWrapping = true;
        objectiveText.overflowMode = TextOverflowModes.Overflow;
    }

    void Update()
    {
        UpdateObjectiveBeat();
        UpdateGoalBeat();
        UpdateEscapeTimer();
        TryFinishByProximity();

        if (!waitingForSpace)
            return;

        if (!WasContinuePressed())
            return;

        waitingForSpace = false;
        StartCoroutine(ContinueRoutine());
    }

    void UpdateEscapeTimer()
    {
        if (!timerRunning || panelOpen)
            return;

        timeRemaining -= Time.deltaTime;
        UpdateTimerHud();

        if (timeRemaining > 0f)
            return;

        timeRemaining = 0f;
        timerRunning = false;
        NotifyGameOver();
    }

    void UpdateTimerHud()
    {
        if (timerText == null)
            return;

        float shown = Mathf.Max(0f, timeRemaining);
        int totalSec = Mathf.CeilToInt(shown);
        int mins = totalSec / 60;
        int secs = totalSec % 60;
        timerText.text = $"Dawn closes in  {mins:0}:{secs:00}";

        if (shown <= timerCriticalSeconds)
            timerText.color = new Color(1f, 0.35f, 0.35f, 1f);
        else if (shown <= timerWarnSeconds)
            timerText.color = new Color(1f, 0.78f, 0.35f, 1f);
        else
            timerText.color = new Color(0.92f, 0.92f, 0.88f, 1f);
    }

    static bool WasContinuePressed()
    {
        if (Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame ||
             Keyboard.current.enterKey.wasPressedThisFrame))
            return true;

        // Fallback when the Input System keyboard device is missing / Game view unfocused briefly.
        return UnityEngine.Input.GetKeyDown(KeyCode.Space) ||
               UnityEngine.Input.GetKeyDown(KeyCode.Return);
    }

    void TryFinishByProximity()
    {
        if (!IsExitOpen || panelOpen || !finishWhenStandingInBoat)
            return;

        if (!TryGetPlayerPosition(out Vector3 playerPos))
            return;

        if (preferredBoat == null)
            preferredBoat = FindPreferredBoat();
        if (preferredBoat == null)
            return;

        // Strict: only the shore goal boat hull. Nearby paths / other boats do not finish.
        Bounds b = GetBoatBounds(preferredBoat);
        b.Expand(Mathf.Max(0.05f, boatBoundsPadding) * 2f);

        Vector3 chest = playerPos + Vector3.up * 1.0f;
        if (b.Contains(playerPos) || b.Contains(chest))
        {
            NotifyExitReached();
            return;
        }

        // Slight planar forgiveness still clamped to this boat's footprint (not a village-wide radius).
        Vector3 flatPlayer = playerPos;
        Vector3 flatCenter = b.center;
        flatPlayer.y = 0f;
        flatCenter.y = 0f;
        float halfX = b.extents.x;
        float halfZ = b.extents.z;
        if (Mathf.Abs(flatPlayer.x - flatCenter.x) <= halfX &&
            Mathf.Abs(flatPlayer.z - flatCenter.z) <= halfZ &&
            playerPos.y >= b.min.y - 0.5f &&
            playerPos.y <= b.max.y + 2f)
        {
            NotifyExitReached();
        }
    }

    static bool TryGetPlayerPosition(out Vector3 playerPos)
    {
        ThirdPersonController player = FindFirstObjectByType<ThirdPersonController>();
        if (player != null)
        {
            playerPos = player.transform.position;
            return true;
        }

        CharacterController cc = FindFirstObjectByType<CharacterController>();
        if (cc != null)
        {
            playerPos = cc.transform.position;
            return true;
        }

        GameObject named = GameObject.Find("Player");
        if (named != null)
        {
            playerPos = named.transform.position;
            return true;
        }

        playerPos = default;
        return false;
    }

    void HandleScoreChanged(int killed, int total)
    {
        if (escapeMode || phaseB || panelOpen)
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

        if (exitMarker != null)
            exitMarker.SetActive(true);
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
        // Re-cache after escape copy is applied so fade restores the right base color.
        CacheObjectiveVisuals();
        Color startColor = objectiveBaseColor;
        startColor.a = objectiveBaseColor.a <= 0.01f ? 1f : objectiveBaseColor.a;
        objectiveBaseColor = startColor;
        objectiveText.color = startColor;

        // Escape instructions stay steady so they're readable; optional pulse in kill-all mode.
        beatingObjective = beatObjectiveOnStart && !escapeMode;

        // In escape mode phaseB is already true (exit open) — still show the intro.
        float hold = Mathf.Max(0f, objectiveIntroDuration);
        float elapsed = 0f;
        while (elapsed < hold && !panelOpen && (escapeMode || !phaseB))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        beatingObjective = false;
        if (objectiveText != null)
            objectiveText.rectTransform.localScale = objectiveBaseScale;

        if (panelOpen)
        {
            objectiveIntroRoutine = null;
            yield break;
        }

        // Soft fade out, then hide.
        float fade = Mathf.Max(0.05f, objectiveFadeOutDuration);
        elapsed = 0f;
        Color from = objectiveText.color;
        while (elapsed < fade && !panelOpen)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fade);
            Color c = from;
            c.a = Mathf.Lerp(from.a, 0f, t);
            objectiveText.color = c;
            yield return null;
        }

        if (!panelOpen)
        {
            SetTextActive(objectiveText, false);
            // Keep full alpha ready if something shows this text again later.
            Color restored = objectiveBaseColor;
            restored.a = startColor.a;
            objectiveBaseColor = restored;
            objectiveText.color = restored;
            objectiveText.rectTransform.localScale = objectiveBaseScale;
        }

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

    public void NotifyExitReached()
    {
        if (!IsExitOpen || panelOpen)
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
        lastVictory = victory;
        waitingForSpace = true;
        beatingGoal = false;
        timerRunning = false;
        StopObjectiveIntro();
        StopChurchBell();
        RestoreGoalVisuals();
        SetTextActive(objectiveText, false);
        SetTextActive(goalText, false);
        if (timerText != null)
            timerText.gameObject.SetActive(false);

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

        int timeBonus = 0;
        if (victory && useEscapeTimer)
        {
            timeBonus = Mathf.Max(0, Mathf.FloorToInt(timeRemaining)) * Mathf.Max(0, timeBonusPerSecond);
            totalScore += timeBonus;
        }

        if (endTitleText != null)
            endTitleText.text = victory ? "She reaches the boat" : "Dawn takes the village";

        if (endBodyText != null)
        {
            if (victory)
            {
                endBodyText.text =
                    "The vow broke at the altar — but she did not.\n" +
                    "Through the aswang village she cuts to the shore.\n" +
                    "Beyond the black water: the old sea port… and the groom\n" +
                    "the night still holds.\n\n" +
                    $"Aswangs faced down: {killed}\n" +
                    $"Missed throws: {misses}\n" +
                    $"Lives left: {livesLeft}\n" +
                    (useEscapeTimer ? $"Time left: {FormatTime(timeRemaining)}\n" : "") +
                    (timeBonus > 0 ? $"Time bonus: +{timeBonus}\n" : "") +
                    $"Score: {totalScore}\n\n" +
                    "Press SPACE — sail to the old sea port";
            }
            else
            {
                bool timedOut = useEscapeTimer && timeRemaining <= 0.05f;
                endBodyText.text =
                    (timedOut
                        ? "Dawn breaks over the stolen streets — too late.\n" +
                          "The boat waits empty while the aswang keep what they took.\n"
                        : "The aswangs close in before she can reach the shore.\n" +
                          "The boat drifts empty in the dark.\n") +
                    "\n" +
                    $"Aswangs faced down: {killed}\n" +
                    $"Missed throws: {misses}\n" +
                    $"Lives left: {livesLeft}\n" +
                    $"Score: {totalScore}\n\n" +
                    "Press SPACE to try again";
            }
        }

        if (endPanel != null)
            endPanel.SetActive(true);
    }

    static string FormatTime(float seconds)
    {
        int totalSec = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{totalSec / 60:0}:{totalSec % 60:00}";
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

        string sceneName;
        if (lastVictory)
        {
            sceneName = string.IsNullOrWhiteSpace(continueSceneName)
                ? "Level3"
                : continueSceneName.Trim();
        }
        else
        {
            sceneName = string.IsNullOrWhiteSpace(retrySceneName)
                ? SceneManager.GetActiveScene().name
                : retrySceneName.Trim();
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"Level2GameFlow: cannot load '{sceneName}'. " +
                "Add that scene in File → Build Settings (and spell the name exactly).");
            waitingForSpace = true;
            yield break;
        }

        Debug.Log($"Level2GameFlow: loading scene '{sceneName}'");
        SceneManager.LoadScene(sceneName);
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

    void SetupExitMarker()
    {
        Transform anchor = exitAnchor;
        if (anchor == null)
        {
            GameObject named = GameObject.Find("Level2ExitAnchor");
            if (named != null)
                anchor = named.transform;
        }

        if (anchor == null)
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && t.name.StartsWith("Cross Arch"))
                {
                    anchor = t;
                    break;
                }
            }
        }

        if (anchor == null)
        {
            Debug.LogWarning("Level2GameFlow: exitAnchor / Cross Arch not found — place Level2ExitAnchor manually.");
            return;
        }

        exitMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        exitMarker.name = "Level2ExitMarker";
        exitMarker.transform.SetParent(anchor, false);
        exitMarker.transform.localPosition = Vector3.zero;
        exitMarker.transform.localRotation = Quaternion.identity;

        Vector3 lossy = anchor.lossyScale;
        float sx = Mathf.Abs(lossy.x) < 0.01f ? 1f : lossy.x;
        float sy = Mathf.Abs(lossy.y) < 0.01f ? 1f : lossy.y;
        float sz = Mathf.Abs(lossy.z) < 0.01f ? 1f : lossy.z;
        exitMarker.transform.localScale = new Vector3(
            exitTriggerSize.x / Mathf.Abs(sx),
            exitTriggerSize.y / Mathf.Abs(sy),
            exitTriggerSize.z / Mathf.Abs(sz));

        Collider col = exitMarker.GetComponent<Collider>();
        col.isTrigger = true;

        MeshRenderer mr = exitMarker.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.enabled = showExitMarkerVisually;
            if (showExitMarkerVisually)
            {
                // Unlit-ish translucent green so the verify exit is obvious.
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader != null)
                {
                    Material mat = new Material(shader);
                    Color green = new Color(0.2f, 1f, 0.35f, 0.35f);
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", green);
                    if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", green);
                    mat.renderQueue = 3000;
                    mr.sharedMaterial = mat;
                }
            }
        }

        Level2ExitTrigger trigger = exitMarker.AddComponent<Level2ExitTrigger>();
        trigger.flow = this;
    }

    void EnsureHudCanvas()
    {
        if (hudCanvas == null)
        {
            GameObject hud = GameObject.Find("HUD");
            if (hud != null)
                hudCanvas = hud.GetComponent<Canvas>();
        }

        if (hudCanvas != null)
            return;

        GameObject canvasGo = new GameObject(
            "Level2EndCanvas",
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

    TMP_FontAsset ResolveHudFont()
    {
        if (objectiveText != null && objectiveText.font != null)
            return objectiveText.font;
        if (goalText != null && goalText.font != null)
            return goalText.font;
        return TMP_Settings.defaultFontAsset;
    }

    void BuildTimerHudIfNeeded()
    {
        if (timerText != null)
            return;

        EnsureHudCanvas();
        if (hudCanvas == null)
            return;

        GameObject timerGo = new GameObject("Level2EscapeTimer", typeof(RectTransform), typeof(TextMeshProUGUI));
        timerGo.transform.SetParent(hudCanvas.transform, false);
        RectTransform rt = timerGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -18f);
        rt.sizeDelta = new Vector2(520f, 40f);

        timerText = timerGo.GetComponent<TextMeshProUGUI>();
        timerText.font = ResolveHudFont();
        timerText.fontSize = 26;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.fontStyle = FontStyles.Bold;
        timerText.color = new Color(0.92f, 0.92f, 0.88f, 1f);
        timerText.text = "Dawn closes in  3:00";
        timerText.gameObject.SetActive(false);
        timerGo.transform.SetAsLastSibling();
    }

    void BuildEndPanelIfNeeded()
    {
        EnsureHudCanvas();
        if (hudCanvas == null)
            return;

        TMP_FontAsset font = ResolveHudFont();

        endPanel = new GameObject("Level2EndPanel", typeof(RectTransform), typeof(Image));
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
        titleRt.anchoredPosition = new Vector2(0f, 110f);
        titleRt.sizeDelta = new Vector2(700f, 50f);
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
        bodyRt.anchoredPosition = new Vector2(0f, -30f);
        bodyRt.sizeDelta = new Vector2(620f, 280f);
        endBodyText = bodyGo.GetComponent<TextMeshProUGUI>();
        endBodyText.font = font;
        endBodyText.fontSize = 18;
        endBodyText.alignment = TextAlignmentOptions.Center;
        endBodyText.color = Color.white;
        endBodyText.lineSpacing = 6f;

        endPanel.SetActive(false);
        endPanel.transform.SetAsLastSibling();
    }
}

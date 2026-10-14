using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// After Level 3: morning on the old port. The groom's boat comes in to the shore
/// marker while he waves and calls out. The run scoreboard stays up over the shot.
/// SPACE after the boat lands returns to the splash menu.
/// </summary>
public class EndingCutscene : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("The grouped boat with groom. Moved as one object. The groom is not reparented.")]
    public Transform boat;
    public Transform groom;
    public Transform shoreMarker;
    public Camera shotCamera;
    public Light sun;

    [Header("Approach")]
    [Tooltip("Seconds for the boat to reach the shore marker.")]
    public float approachSeconds = 16f;

    [Header("Song")]
    [Tooltip("Plays over the ending until SPACE.")]
    public AudioClip endingSong;
    [Range(0f, 1f)] public float endingSongVolume = 0.75f;

    [Header("Groom")]
    public AnimationClip wavingClip;
    public AudioClip[] shouts;
    [Range(0f, 1f)] public float shoutVolume = 1f;
    [Tooltip("Quiet gap between each shout.")]
    public float shoutGap = 0.4f;

    [Header("Morning")]
    public bool brightenEnvironment = true;
    public float sunIntensity = 2.35f;
    public Color sunColor = new Color(1f, 0.96f, 0.88f, 1f);
    public Color ambientColor = new Color(0.82f, 0.88f, 0.95f, 1f);

    PlayableGraph graph;
    AnimationClipPlayable wavePlayable;
    AudioSource shoutSource;
    AudioSource songSource;
    TMP_Text promptText;
    Vector3 boatStart;
    bool arrived;
    bool leaving;

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        ResolveRefs();
        if (brightenEnvironment)
            MakeMorning();

        CacheApproach();
        PlayEndingSong();
        PlayWaving();
        StartCoroutine(ShoutLoop());
        BuildScoreboard();
        FrameCamera(1f);
    }

    void Update()
    {
        LoopWave();
        MoveBoat();
        FrameCamera(Time.deltaTime);

        if (!arrived || leaving || !WasContinuePressed())
            return;

        leaving = true;
        if (songSource != null)
            songSource.Stop();
        LevelChain.TryLoad(LevelChain.Splash);
    }

    void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }

    void ResolveRefs()
    {
        if (boat == null)
        {
            GameObject group = GameObject.Find("boat with groom");
            if (group != null)
                boat = group.transform;
        }

        if (groom == null)
        {
            GameObject found = GameObject.Find("Groom");
            if (found != null)
                groom = found.transform;
        }

        if (shoreMarker == null)
        {
            GameObject found = GameObject.Find("marker");
            if (found != null)
                shoreMarker = found.transform;
        }

        if (shotCamera == null)
            shotCamera = Camera.main;

        if (sun == null)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    sun = lights[i];
                    break;
                }
            }
        }
    }

    void MakeMorning()
    {
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.ambientIntensity = 1.2f;

        if (sun != null)
        {
            sun.color = sunColor;
            sun.intensity = sunIntensity;
        }

        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            if (volumes[i] != null)
                volumes[i].weight = 0f;
        }
    }

    void CacheApproach()
    {
        if (boat == null)
            return;

        boatStart = boat.position;
    }

    void MoveBoat()
    {
        if (boat == null || shoreMarker == null || arrived)
            return;

        float duration = Mathf.Max(0.1f, approachSeconds);
        float t = Mathf.Clamp01(Time.timeSinceLevelLoad / duration);
        float eased = t * t * (3f - 2f * t);

        Vector3 end = shoreMarker.position;
        end.y = boatStart.y;
        boat.position = Vector3.Lerp(boatStart, end, eased);

        if (t < 1f)
            return;

        arrived = true;
        boat.position = end;
        if (promptText != null)
            promptText.text = "Press SPACE to return to the menu";
    }

    void PlayEndingSong()
    {
        EnsureEndingSong();
        if (endingSong == null)
            return;

        songSource = gameObject.AddComponent<AudioSource>();
        songSource.playOnAwake = false;
        songSource.loop = true;
        songSource.spatialBlend = 0f;
        songSource.volume = endingSongVolume;
        songSource.clip = endingSong;
        songSource.Play();
    }

    void EnsureEndingSong()
    {
#if UNITY_EDITOR
        if (endingSong != null)
            return;

        endingSong = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/ending sound.wav");
#endif
    }

    void PlayWaving()
    {
        EnsureWavingClip();
        if (groom == null || wavingClip == null)
            return;

        Animator animator = groom.GetComponentInChildren<Animator>();
        if (animator == null)
            animator = groom.gameObject.AddComponent<Animator>();

        animator.applyRootMotion = false;
        animator.runtimeAnimatorController = null;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (graph.IsValid())
            graph.Destroy();

        graph = PlayableGraph.Create("EndingGroomWave");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Wave", animator);
        wavePlayable = AnimationClipPlayable.Create(graph, wavingClip);
        wavePlayable.SetApplyFootIK(false);
        output.SetSourcePlayable(wavePlayable);
        graph.Play();
    }

    void EnsureWavingClip()
    {
#if UNITY_EDITOR
        if (wavingClip != null)
            return;

        UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
            "Assets/assets-people/c-anims/Waving.fbx");
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview"))
            {
                wavingClip = clip;
                return;
            }
        }
#endif
    }

    void LoopWave()
    {
        if (!wavePlayable.IsValid() || wavingClip == null || wavingClip.length <= 0.01f)
            return;

        double time = wavePlayable.GetTime();
        if (time >= wavingClip.length)
            wavePlayable.SetTime(time % wavingClip.length);
    }

    IEnumerator ShoutLoop()
    {
        if (shouts == null || shouts.Length == 0)
            yield break;

        shoutSource = groom != null
            ? groom.gameObject.AddComponent<AudioSource>()
            : gameObject.AddComponent<AudioSource>();
        shoutSource.playOnAwake = false;
        shoutSource.loop = false;
        shoutSource.spatialBlend = groom != null ? 1f : 0f;
        shoutSource.minDistance = 8f;
        shoutSource.maxDistance = 90f;
        shoutSource.rolloffMode = AudioRolloffMode.Linear;
        shoutSource.volume = shoutVolume;

        while (true)
        {
            bool played = false;
            for (int i = 0; i < shouts.Length; i++)
            {
                AudioClip clip = shouts[i];
                if (clip == null)
                    continue;

                played = true;
                shoutSource.clip = clip;
                shoutSource.Play();
                yield return new WaitForSeconds(clip.length + Mathf.Max(0f, shoutGap));
            }

            if (!played)
                yield break;
        }
    }

    void FrameCamera(float deltaTime)
    {
        if (shotCamera == null || boat == null)
            return;

        Animator cameraAnimator = shotCamera.GetComponent<Animator>();
        if (cameraAnimator != null)
            cameraAnimator.enabled = false;

        Vector3 focus = boat.position + Vector3.up * 1.8f;
        Vector3 dir = focus - shotCamera.transform.position;
        if (dir.sqrMagnitude < 0.01f)
            return;

        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        float blend = 1f - Mathf.Exp(-2.2f * Mathf.Max(0f, deltaTime));
        shotCamera.transform.rotation = Quaternion.Slerp(shotCamera.transform.rotation, look, blend);
    }

    void BuildScoreboard()
    {
        GameObject canvasGo = new GameObject(
            "EndingScoreboard",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 0.5f);
        panelRt.anchorMax = new Vector2(1f, 0.5f);
        panelRt.pivot = new Vector2(1f, 0.5f);
        panelRt.anchoredPosition = new Vector2(-48f, 0f);
        panelRt.sizeDelta = new Vector2(560f, 640f);
        panel.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.1f, 0.72f);

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        AddLabel(panel.transform, font, "Score", 40, FontStyles.Bold, new Vector2(0f, 260f), new Vector2(500f, 64f));

        var lines = CampaignScore.Lines;
        if (lines.Count == 0)
        {
            AddLabel(
                panel.transform,
                font,
                "Play from the splash menu\nto fill this board.",
                26,
                FontStyles.Normal,
                new Vector2(0f, 40f),
                new Vector2(500f, 180f));
        }
        else
        {
            float y = 160f;
            for (int i = 0; i < lines.Count; i++)
            {
                CampaignScore.Line line = lines[i];
                string body =
                    $"{line.levelName}    {line.score}\n" +
                    $"<size=18>aswang {line.killed}/{line.total}   misses {line.missedThrows}   lives {line.livesLeft}</size>";
                AddLabel(panel.transform, font, body, 28, FontStyles.Normal, new Vector2(0f, y), new Vector2(500f, 100f));
                y -= 120f;
            }

            AddLabel(
                panel.transform,
                font,
                $"Total    {CampaignScore.Total}",
                32,
                FontStyles.Bold,
                new Vector2(0f, y + 20f),
                new Vector2(500f, 56f));
        }

        promptText = AddLabel(
            panel.transform,
            font,
            "The boat is coming in",
            22,
            FontStyles.Italic,
            new Vector2(0f, -270f),
            new Vector2(500f, 48f));
    }

    static TMP_Text AddLabel(
        Transform parent,
        TMP_FontAsset font,
        string text,
        float size,
        FontStyles style,
        Vector2 position,
        Vector2 sizeDelta)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = sizeDelta;

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.text = text;
        label.richText = true;
        return label;
    }

    static bool WasContinuePressed()
    {
        return Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame ||
             Keyboard.current.enterKey.wasPressedThisFrame);
    }
}

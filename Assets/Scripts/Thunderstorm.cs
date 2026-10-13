using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// After the level clear, the sun, sky, and baked light go out.
/// Street lights and other local lights stay on.
/// Lightning brings the saved scene brightness back, then it drops again.
/// Put this on the scene Directional Light.
/// </summary>
[DisallowMultipleComponent]
public class Thunderstorm : MonoBehaviour
{
    [Header("Darkness")]
    [Tooltip("How bright the storm stays between flashes. 0 leaves only street lights and other local lights. 1 is the scene as you saved it.")]
    [Range(0f, 1f)]
    public float darkLevel = 0f;

    [Header("Lightning")]
    [Tooltip("How bright the scene gets during a flash. 1 matches the saved scene.")]
    [Range(0.5f, 3f)]
    public float flashLevel = 1f;

    [Tooltip("Seconds the scene stays at flash brightness.")]
    [UnityEngine.Min(0f)]
    public float brightHold = 0.5f;

    [Tooltip("Seconds to ease from the flash back down to the dark level.")]
    [UnityEngine.Min(0.01f)]
    public float fadeBack = 0.4f;

    [Tooltip("Shortest wait between flashes.")]
    [UnityEngine.Min(0.1f)]
    public float minGap = 3f;

    [Tooltip("Longest wait between flashes.")]
    [UnityEngine.Min(0.1f)]
    public float maxGap = 8f;

    [Header("Thunder")]
    public AudioClip thunderClip;
    [Range(0f, 1f)]
    public float thunderVolume = 1f;

    Light sun;
    Material skyCopy;
    Material originalSky;
    float baseSunIntensity = 1f;
    float baseAmbientIntensity = 1f;
    float baseSkyExposure = 1f;
    float baseReflectionIntensity = 1f;
    SphericalHarmonicsL2 baseAmbientProbe;
    LightmapData[] savedLightmaps;
    bool hasSkyExposure;
    bool blackoutActive;
    bool openingPlayed;
    bool bakedLightingOn = true;

    float level = 1f;
    float nextFlashTime;
    float phaseStart;
    Phase phase = Phase.Waiting;
    AudioSource thunderSource;

    enum Phase
    {
        Waiting,
        Holding,
        Fading
    }

    void OnValidate()
    {
        if (flashLevel < darkLevel)
            flashLevel = darkLevel;
        if (maxGap < minGap)
            maxGap = minGap;
    }

    void Awake()
    {
        sun = GetComponent<Light>();
        if (sun == null || sun.type != LightType.Directional)
            sun = FindDirectionalLight();

        if (sun != null)
            baseSunIntensity = sun.intensity;

        thunderSource = GetComponent<AudioSource>();
        if (thunderSource == null)
            thunderSource = gameObject.AddComponent<AudioSource>();

        thunderSource.playOnAwake = false;
        thunderSource.loop = false;
        thunderSource.spatialBlend = 0f;
        thunderSource.volume = thunderVolume;

        baseAmbientIntensity = RenderSettings.ambientIntensity;
        baseReflectionIntensity = RenderSettings.reflectionIntensity;
        baseAmbientProbe = RenderSettings.ambientProbe;
        savedLightmaps = LightmapSettings.lightmaps;

        originalSky = RenderSettings.skybox;
        if (originalSky != null && originalSky.HasProperty("_Exposure"))
        {
            baseSkyExposure = originalSky.GetFloat("_Exposure");
            skyCopy = new Material(originalSky);
            RenderSettings.skybox = skyCopy;
            hasSkyExposure = true;
        }
    }

    public void BeginBlackout()
    {
        if (blackoutActive)
            return;

        blackoutActive = true;
        DisableAutoExposure();
        phase = Phase.Waiting;
        level = darkLevel;
        Apply(level);
        ScheduleNextFlash();
    }

    /// <summary>
    /// One lightning flash with thunder, then fade into the blackout.
    /// onBlackout runs when the scene has finished fading dark.
    /// </summary>
    public void PlayStrikeThenBlackout(System.Action onBlackout)
    {
        if (openingPlayed)
        {
            onBlackout?.Invoke();
            return;
        }

        openingPlayed = true;
        StartCoroutine(OpeningStrike(onBlackout));
    }

    IEnumerator OpeningStrike(System.Action onBlackout)
    {
        DisableAutoExposure();
        PlayThunder();

        level = flashLevel;
        Apply(level);

        if (brightHold > 0f)
            yield return new WaitForSeconds(brightHold);

        float fade = Mathf.Max(0.01f, fadeBack);
        float elapsed = 0f;
        while (elapsed < fade)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fade);
            level = Mathf.Lerp(flashLevel, darkLevel, t);
            Apply(level);
            yield return null;
        }

        blackoutActive = true;
        phase = Phase.Waiting;
        level = darkLevel;
        Apply(level);
        ScheduleNextFlash();
        onBlackout?.Invoke();
    }

    public void ClearAfter(float delay)
    {
        StartCoroutine(ClearRoutine(delay));
    }

    IEnumerator ClearRoutine(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        EndStorm();
    }

    public void EndStorm()
    {
        blackoutActive = false;

        if (thunderSource != null)
            thunderSource.Stop();

        level = 1f;
        bakedLightingOn = false;
        Apply(1f);

        GameObject rain = GameObject.Find("PF_RainVFX_Group");
        if (rain != null)
            rain.SetActive(false);
    }

    void Update()
    {
        if (!blackoutActive)
            return;

        if (phase == Phase.Waiting)
        {
            level = darkLevel;
            if (Time.time >= nextFlashTime)
            {
                phase = Phase.Holding;
                phaseStart = Time.time;
                level = flashLevel;
                PlayThunder();
            }
        }
        else if (phase == Phase.Holding)
        {
            level = flashLevel;
            if (Time.time >= phaseStart + brightHold)
            {
                phase = Phase.Fading;
                phaseStart = Time.time;
            }
        }
        else
        {
            float t = Mathf.Clamp01((Time.time - phaseStart) / fadeBack);
            level = Mathf.Lerp(flashLevel, darkLevel, t);
            if (t >= 1f)
            {
                phase = Phase.Waiting;
                level = darkLevel;
                ScheduleNextFlash();
            }
        }

        Apply(level);
    }

    void OnDisable()
    {
        if (sun != null)
            sun.intensity = baseSunIntensity;

        RenderSettings.ambientIntensity = baseAmbientIntensity;
        RenderSettings.reflectionIntensity = baseReflectionIntensity;
        RenderSettings.ambientProbe = baseAmbientProbe;

        if (savedLightmaps != null)
            LightmapSettings.lightmaps = savedLightmaps;

        if (originalSky != null)
            RenderSettings.skybox = originalSky;

        if (skyCopy != null)
        {
            if (Application.isPlaying)
                Destroy(skyCopy);
            else
                DestroyImmediate(skyCopy);
            skyCopy = null;
        }
    }

    void ScheduleNextFlash()
    {
        nextFlashTime = Time.time + Random.Range(minGap, maxGap);
    }

    void PlayThunder()
    {
        if (thunderClip == null || thunderSource == null)
            return;

        thunderSource.volume = thunderVolume;
        thunderSource.clip = thunderClip;
        thunderSource.Play();
    }

    void Apply(float brightness)
    {
        if (sun != null)
            sun.intensity = baseSunIntensity * brightness;

        RenderSettings.ambientIntensity = baseAmbientIntensity * brightness;
        RenderSettings.reflectionIntensity = baseReflectionIntensity * brightness;

        if (hasSkyExposure && skyCopy != null)
            skyCopy.SetFloat("_Exposure", baseSkyExposure * brightness);

        bool showBaked = brightness > 0.85f;
        if (showBaked == bakedLightingOn)
            return;

        bakedLightingOn = showBaked;
        if (showBaked)
        {
            if (savedLightmaps != null)
                LightmapSettings.lightmaps = savedLightmaps;
            RenderSettings.ambientProbe = baseAmbientProbe;
        }
        else
        {
            LightmapSettings.lightmaps = System.Array.Empty<LightmapData>();
            RenderSettings.ambientProbe = new SphericalHarmonicsL2();
        }
    }

    static void DisableAutoExposure()
    {
        UnityEngine.Rendering.PostProcessing.PostProcessVolume volume =
            FindFirstObjectByType<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
        if (volume == null)
            return;

        UnityEngine.Rendering.PostProcessing.PostProcessProfile profile = volume.profile;
        if (profile != null && profile.TryGetSettings(out UnityEngine.Rendering.PostProcessing.AutoExposure exposure))
            exposure.enabled.value = false;
    }

    static Light FindDirectionalLight()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Directional)
                return lights[i];
        }

        return null;
    }
}

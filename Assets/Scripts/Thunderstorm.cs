using UnityEngine;

/// <summary>
/// Dims the sun, ambient light, and sky, then holds a brighter lightning flash.
/// Put this on the scene Directional Light.
/// </summary>
[DisallowMultipleComponent]
public class Thunderstorm : MonoBehaviour
{
    [Header("Darkness")]
    [Tooltip("How bright the storm stays between flashes. 1 is the scene as you saved it. Lower is darker.")]
    [Range(0.2f, 1f)]
    public float darkLevel = 0.28f;

    [Header("Lightning")]
    [Tooltip("How bright the scene gets during a flash. 1 matches the saved scene. Higher is a stronger strike.")]
    [Range(0.5f, 3f)]
    public float flashLevel = 1.4f;

    [Tooltip("Seconds the scene stays at flash brightness.")]
    [Min(0f)]
    public float brightHold = 0.5f;

    [Tooltip("Seconds to ease from the flash back down to the dark level.")]
    [Min(0.01f)]
    public float fadeBack = 0.4f;

    [Tooltip("Shortest wait between flashes.")]
    [Min(0.1f)]
    public float minGap = 3f;

    [Tooltip("Longest wait between flashes.")]
    [Min(0.1f)]
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
    bool hasSkyExposure;

    float level;
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

        originalSky = RenderSettings.skybox;
        if (originalSky != null && originalSky.HasProperty("_Exposure"))
        {
            baseSkyExposure = originalSky.GetFloat("_Exposure");
            skyCopy = new Material(originalSky);
            RenderSettings.skybox = skyCopy;
            hasSkyExposure = true;
        }
    }

    void Start()
    {
        level = darkLevel;
        Apply(level);
        ScheduleNextFlash();
    }

    void Update()
    {
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

        if (hasSkyExposure && skyCopy != null)
            skyCopy.SetFloat("_Exposure", baseSkyExposure * brightness);
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

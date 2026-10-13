using System.Collections.Generic;
using UnityEngine;

public class AnglerfishAttack : MonoBehaviour
{
    [Header("Fish")]
    public Transform fish;
    [Tooltip("Empty markers over the water beside the pier. One fish leaves from each marker.")]
    public Transform[] launchPoints;
    [Tooltip("Longest side of each fish while it is in the air, in meters.")]
    public float fishLength = 3.5f;
    [Tooltip("Extra spin if the model nose does not point along the flight.")]
    public Vector3 modelEulerOffset;

    [Header("Flight")]
    [Tooltip("How fast each arc covers the distance, in units per second.")]
    public float flySpeed = 28f;
    [Tooltip("How high each leap climbs before it dives onto her.")]
    public float arcHeight = 8f;
    [Tooltip("Chest height added to her position. The dive ends here.")]
    public float aimHeight = 1.2f;
    [Tooltip("Seconds a fish keeps flying after its hit, along the same dive.")]
    public float flyThroughTime = 0.35f;

    [Header("Hit")]
    [Tooltip("Extra reach past the mesh surface. Damage happens only when the body itself meets her.")]
    public float contactSlack = 0.35f;
    public int damage = 60;

    [Header("Audio")]
    public AudioClip flyClip;
    [Range(0f, 1f)]
    public float flyVolume = 1f;

    public bool IsFlying => flights.Count > 0;

    readonly List<Flight> flights = new List<Flight>();
    bool scaled;
    bool meshCenterReady;
    Vector3 meshCenterLocal;

    class Flight
    {
        public Transform body;
        public Transform marker;
        public Light lure;
        public Vector3 start;
        public Vector3 control;
        public Vector3 end;
        public Vector3 previous;
        public float duration;
        public float elapsed;
        public float delay;
        public bool launched;
        public bool hit;
        public AudioSource flySource;
    }

    void Awake()
    {
        ResolveFish();
        if (fish != null)
            fish.gameObject.SetActive(false);
    }

    public void Launch(float firstInterval, float intervalStep, float minimumInterval)
    {
        if (flights.Count > 0)
            return;

        ResolveFish();
        if (fish == null || launchPoints == null || launchPoints.Length == 0)
        {
            Debug.LogWarning("Anglerfish launch skipped. Fish or launch points are missing.");
            return;
        }

        if (FindAnyObjectByType<ThirdPersonController>() == null)
        {
            Debug.LogWarning("Anglerfish launch skipped. No player in the scene.");
            return;
        }

        PrepareTemplate();

        float interval = Mathf.Max(minimumInterval, firstInterval);
        float delay = 0f;
        int order = 0;
        for (int i = 0; i < launchPoints.Length; i++)
        {
            if (launchPoints[i] == null)
                continue;

            GameObject copy = Instantiate(fish.gameObject);
            copy.name = "anglerfish " + (order + 1);
            copy.SetActive(false);

            flights.Add(new Flight
            {
                body = copy.transform,
                marker = launchPoints[i],
                delay = delay
            });

            delay += Mathf.Max(0.1f, interval);
            interval = Mathf.Max(minimumInterval, interval - intervalStep);
            order++;
        }
    }

    public void LaunchOne()
    {
        ResolveFish();
        if (fish == null || launchPoints == null || launchPoints.Length == 0)
            return;

        if (FindAnyObjectByType<ThirdPersonController>() == null)
            return;

        PrepareTemplate();

        Transform marker = null;
        int offset = Random.Range(0, launchPoints.Length);
        for (int i = 0; i < launchPoints.Length; i++)
        {
            Transform candidate = launchPoints[(offset + i) % launchPoints.Length];
            if (candidate == null)
                continue;

            marker = candidate;
            break;
        }

        if (marker == null)
            return;

        GameObject copy = Instantiate(fish.gameObject);
        copy.name = "anglerfish minion";
        copy.SetActive(false);

        flights.Add(new Flight
        {
            body = copy.transform,
            marker = marker,
            delay = 0f
        });
    }

    void Update()
    {
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight flight = flights[i];
            flight.elapsed += Time.deltaTime;

            if (!flight.launched)
            {
                if (flight.elapsed < flight.delay)
                    continue;

                BeginFlight(flight);
            }

            float travel = flight.elapsed - flight.delay;
            float t = Mathf.Clamp01(travel / flight.duration);
            Vector3 pos = t < 1f
                ? Bezier(flight, t)
                : flight.end + ExitDirection(flight) * ((travel - flight.duration) * flySpeed);

            Quaternion rotation = flight.body.rotation;
            Vector3 tangent = pos - flight.previous;
            if (tangent.sqrMagnitude > 0.0001f)
            {
                rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up)
                    * Quaternion.Euler(modelEulerOffset);
            }

            PlaceBody(flight.body, pos, rotation);
            if (flight.lure != null)
                flight.lure.transform.position = pos;

            if (!flight.hit && BodyTouchesHer(flight.body, flight.previous, pos))
                Strike(flight);

            flight.previous = pos;

            if (travel < flight.duration + flyThroughTime)
                continue;

            if (flight.lure != null)
                Destroy(flight.lure.gameObject);
            Destroy(flight.body.gameObject);
            flights.RemoveAt(i);
            if (flights.Count == 0 && !bossRevealed)
            {
                bossRevealed = true;
                RevealBoss();
            }
        }
    }

    bool bossRevealed;

    void RevealBoss()
    {
        ZombieBoss boss = FindAnyObjectByType<ZombieBoss>(FindObjectsInactive.Include);
        boss?.Appear();
    }

    void BeginFlight(Flight flight)
    {
        ThirdPersonController player = FindAnyObjectByType<ThirdPersonController>();
        Vector3 aim = player != null
            ? player.transform.position + Vector3.up * aimHeight
            : flight.body.position + Vector3.forward * 8f;

        int index = flights.IndexOf(flight);
        flight.start = flight.marker != null ? flight.marker.position : flight.body.position;
        flight.end = aim;

        Vector3 flat = flight.end - flight.start;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.25f)
            flat = Vector3.forward * 8f;

        Vector3 side = Vector3.Cross(flat.normalized, Vector3.up);
        float spread = (index - (flights.Count - 1) * 0.5f) * 3.5f;
        flight.control = flight.start
            + flat.normalized * (flat.magnitude * 0.35f)
            + Vector3.up * arcHeight
            + side * spread;

        float distance = Vector3.Distance(flight.start, flight.control) + Vector3.Distance(flight.control, flight.end);
        flight.duration = Mathf.Clamp(distance / Mathf.Max(1f, flySpeed), 0.55f, 1.1f);

        flight.body.gameObject.SetActive(true);
        foreach (Renderer renderer in flight.body.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = true;

        flight.lure = CreateLure();
        PlaceBody(flight.body, flight.start, Quaternion.identity);
        flight.lure.transform.position = flight.start;
        flight.previous = flight.start;
        flight.launched = true;
        StartFlySound(flight);
    }

    void StartFlySound(Flight flight)
    {
        if (flyClip == null || flight.body == null)
            return;

        AudioSource source = flight.body.gameObject.AddComponent<AudioSource>();
        source.clip = flyClip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0.15f;
        source.volume = 1f;
        source.minDistance = 30f;
        source.maxDistance = 100f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.dopplerLevel = 0f;
        source.Play();
        flight.flySource = source;
    }

    void ResolveFish()
    {
        if (fish != null)
            return;

        GameObject named = GameObject.Find("anglerfish");
        if (named != null)
            fish = named.transform;
    }

    void PrepareTemplate()
    {
        fish.gameObject.SetActive(true);
        foreach (Renderer renderer in fish.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = true;

        if (!scaled && fishLength > 0.1f)
        {
            fish.localScale = Vector3.one;
            Renderer[] renderers = fish.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        bounds.Encapsulate(renderers[i].bounds);
                }

                float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (size >= 0.001f)
                {
                    fish.localScale = Vector3.one * (fishLength / size);
                    scaled = true;
                }
            }
        }

        CaptureMeshCenter();
        fish.gameObject.SetActive(false);
    }

    void CaptureMeshCenter()
    {
        if (meshCenterReady)
            return;

        Quaternion saved = fish.rotation;
        fish.rotation = Quaternion.identity;
        Bounds bounds = CombinedBounds(fish);
        meshCenterLocal = fish.InverseTransformPoint(bounds.center);
        fish.rotation = saved;
        meshCenterReady = true;

        float offset = fish.TransformVector(meshCenterLocal).magnitude;
        if (offset > 0.2f)
            Debug.Log("Anglerfish mesh sits " + offset.ToString("0.0") + " m off its move gizmo. Flight uses the mesh.");
    }

    void PlaceBody(Transform body, Vector3 meshCenter, Quaternion rotation)
    {
        body.rotation = rotation;
        body.position = meshCenter - body.TransformVector(meshCenterLocal);
    }

    static Bounds CombinedBounds(Transform body)
    {
        Renderer[] renderers = body.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(body.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    Light CreateLure()
    {
        GameObject bulb = new GameObject("Lure");
        Light lure = bulb.AddComponent<Light>();
        lure.type = LightType.Point;
        lure.color = new Color(1f, 0.82f, 0.35f);
        lure.intensity = 12f;
        lure.range = 18f;
        lure.shadows = LightShadows.None;
        return lure;
    }

    static Vector3 Bezier(Flight flight, float t)
    {
        float u = 1f - t;
        return u * u * flight.start + 2f * u * t * flight.control + t * t * flight.end;
    }

    static Vector3 ExitDirection(Flight flight)
    {
        Vector3 dir = flight.end - flight.control;
        if (dir.sqrMagnitude < 0.001f)
            dir = flight.end - flight.start;
        return dir.sqrMagnitude < 0.001f ? Vector3.forward : dir.normalized;
    }

    bool BodyTouchesHer(Transform fishBody, Vector3 fromCenter, Vector3 toCenter)
    {
        ThirdPersonController player = FindAnyObjectByType<ThirdPersonController>();
        if (player == null || fishBody == null)
            return false;

        Vector3 chest = player.transform.position + Vector3.up * aimHeight;
        Bounds mesh = CombinedBounds(fishBody);
        if (Vector3.Distance(mesh.ClosestPoint(chest), chest) <= contactSlack)
            return true;

        Vector3 extents = mesh.extents;
        float longest = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
        float thickness = (extents.x + extents.y + extents.z - longest) * 0.5f;
        thickness = Mathf.Max(thickness, 0.35f);

        Vector3 segment = toCenter - fromCenter;
        float lengthSqr = segment.sqrMagnitude;
        float along = lengthSqr < 0.0001f
            ? 0f
            : Mathf.Clamp01(Vector3.Dot(chest - fromCenter, segment) / lengthSqr);
        return Vector3.Distance(fromCenter + segment * along, chest) <= thickness;
    }

    void Strike(Flight flight)
    {
        flight.hit = true;
        ThirdPersonController player = FindAnyObjectByType<ThirdPersonController>();
        if (player != null)
            player.TakeHit(damage);
    }
}

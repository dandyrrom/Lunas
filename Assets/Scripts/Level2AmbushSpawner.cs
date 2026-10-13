using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Level2 escape gauntlet: pack aswangs across every useful path so she can't slip through clean.
/// Spawns extras at runtime from existing ZombieRoam templates (NavMesh-sampled).
/// </summary>
public class Level2AmbushSpawner : MonoBehaviour
{
    [Header("Horde")]
    [Tooltip("How many extra aswangs to spawn beyond whatever is already in the scene.")]
    public int extraCount = 55;

    [Tooltip("Re-enable any disabled aswangs already placed in the scene.")]
    public bool reactivatePlacedAswangs = true;

    [Header("Spawn area (world)")]
    public Vector3 areaMin = new Vector3(-120f, 2f, -65f);
    public Vector3 areaMax = new Vector3(28f, 8f, 40f);

    [Tooltip("Keep this radius around PlayerSpawn clear.")]
    public float clearRadiusAroundSpawn = 8f;

    [Tooltip("Minimum spacing between spawned aswangs.")]
    public float minSpacing = 2.75f;

    [Header("Pressure (applied to spawned + reactivated)")]
    public float chaseSpeed = 0.72f;
    public float hitRunSpeed = 0.8f;
    public float screamAlertRadius = 34f;
    public float breathingRange = 1.4f;
    public int attackDamage = 12;

    /// <summary>
    /// Authored choke / path seeds so side routes and shore approaches aren't empty.
    /// Covers main west push, north ridge, south shore, east flank, and cross alleys.
    /// </summary>
    static readonly Vector3[] PathCorridorSpots =
    {
        // Leave-spawn approaches (east village edge)
        new Vector3(12f, 6f, -22f),
        new Vector3(8f, 6f, -16f),
        new Vector3(5f, 5.5f, -30f),
        new Vector3(0f, 5.5f, -20f),
        new Vector3(-5f, 5.5f, -12f),
        new Vector3(-8f, 5.5f, -28f),
        new Vector3(-10f, 6f, -8f),
        new Vector3(-12f, 6f, 2f),

        // Central boardwalk / mid village
        new Vector3(-20f, 5.5f, -18f),
        new Vector3(-25f, 5f, -10f),
        new Vector3(-28f, 5f, -25f),
        new Vector3(-32f, 5f, -5f),
        new Vector3(-35f, 5f, -32f),
        new Vector3(-38f, 5f, 0f),
        new Vector3(-40f, 4.5f, -15f),
        new Vector3(-42f, 5f, 8f),
        new Vector3(-45f, 4.5f, -22f),
        new Vector3(-48f, 4f, -8f),
        new Vector3(-50f, 4f, 5f),
        new Vector3(-52f, 4f, -35f),
        new Vector3(-55f, 3.5f, -18f),
        new Vector3(-58f, 3.5f, 0f),

        // North path / ridge flank
        new Vector3(-15f, 7f, 10f),
        new Vector3(-25f, 6.5f, 15f),
        new Vector3(-35f, 6f, 18f),
        new Vector3(-45f, 5f, 20f),
        new Vector3(-55f, 4f, 16f),
        new Vector3(-65f, 3.5f, 12f),
        new Vector3(-75f, 3f, 14f),
        new Vector3(-85f, 3f, 10f),
        new Vector3(-95f, 3f, 8f),
        new Vector3(-22f, 6f, 6f),
        new Vector3(-48f, 4.5f, 12f),
        new Vector3(-68f, 3.5f, 6f),

        // South path / shore approach
        new Vector3(-18f, 5.5f, -38f),
        new Vector3(-28f, 5f, -42f),
        new Vector3(-38f, 4.5f, -45f),
        new Vector3(-48f, 4f, -48f),
        new Vector3(-58f, 3.5f, -50f),
        new Vector3(-68f, 3.5f, -46f),
        new Vector3(-78f, 3f, -42f),
        new Vector3(-88f, 3f, -38f),
        new Vector3(-98f, 3f, -32f),
        new Vector3(-108f, 3f, -24f),
        new Vector3(-30f, 5f, -36f),
        new Vector3(-60f, 3.5f, -40f),
        new Vector3(-90f, 3f, -28f),

        // West deep village / toward exit-boat side
        new Vector3(-70f, 3.5f, -20f),
        new Vector3(-75f, 3f, -8f),
        new Vector3(-80f, 3f, -25f),
        new Vector3(-85f, 3f, 0f),
        new Vector3(-90f, 3f, -15f),
        new Vector3(-95f, 3.5f, -5f),
        new Vector3(-100f, 3.3f, -20f),
        new Vector3(-105f, 3.3f, -10f),
        new Vector3(-110f, 3f, -18f),
        new Vector3(-115f, 3f, -8f),
        new Vector3(-72f, 3.5f, -32f),
        new Vector3(-82f, 3f, 6f),
        new Vector3(-92f, 3f, -35f),

        // Cross alleys / cut-throughs
        new Vector3(-33f, 5f, -18f),
        new Vector3(-43f, 4.5f, -28f),
        new Vector3(-53f, 4f, 8f),
        new Vector3(-63f, 3.5f, -5f),
        new Vector3(-73f, 3.5f, 10f),
        new Vector3(-63f, 3.5f, -25f),
        new Vector3(-43f, 4.5f, 4f),
        new Vector3(-23f, 5.5f, -4f),
        new Vector3(-16f, 6f, -24f),
        new Vector3(-36f, 5f, -40f),
    };

    public void SpawnHorde()
    {
        if (reactivatePlacedAswangs)
            ReactivatePlaced();

        ZombieRoam[] templates = FindObjectsByType<ZombieRoam>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (templates == null || templates.Length == 0)
        {
            Debug.LogWarning("Level2AmbushSpawner: no active ZombieRoam templates to clone.");
            return;
        }

        Vector3 spawnCenter = ResolveSpawnCenter();
        List<Vector3> used = new List<Vector3>();
        foreach (ZombieRoam z in templates)
        {
            if (z != null)
                used.Add(z.transform.position);
        }

        int created = 0;

        // 1) Seed every corridor / side path first so no route is empty.
        created += SpawnFromSpotList(PathCorridorSpots, templates, used, spawnCenter, ref created, preferExact: true);

        // 2) Fill remaining quota with random NavMesh points across the whole area.
        int attempts = 0;
        int maxAttempts = Mathf.Max(extraCount * 20, 200);
        while (created < extraCount && attempts < maxAttempts)
        {
            attempts++;
            Vector3 candidate = RandomPointInArea();
            if (!TryPlace(candidate, templates, used, spawnCenter, created, out _))
                continue;
            created++;
        }

        // 3) Last resort: densify by jittering corridor spots that failed earlier.
        if (created < extraCount)
        {
            for (int i = 0; created < extraCount && i < PathCorridorSpots.Length * 2; i++)
            {
                Vector3 baseSpot = PathCorridorSpots[i % PathCorridorSpots.Length];
                Vector3 jitter = baseSpot + new Vector3(
                    Random.Range(-4f, 4f),
                    0f,
                    Random.Range(-4f, 4f));
                if (!TryPlace(jitter, templates, used, spawnCenter, created, out _))
                    continue;
                created++;
            }
        }

        Debug.Log($"Level2AmbushSpawner: spawned {created} extra aswangs across paths (requested {extraCount}).");
    }

    int SpawnFromSpotList(
        Vector3[] spots,
        ZombieRoam[] templates,
        List<Vector3> used,
        Vector3 spawnCenter,
        ref int created,
        bool preferExact)
    {
        int added = 0;
        for (int i = 0; i < spots.Length && created < extraCount; i++)
        {
            Vector3 spot = spots[i];
            if (!TryPlace(spot, templates, used, spawnCenter, created, out _, allowLooserSpacing: preferExact))
                continue;
            created++;
            added++;
        }

        return added;
    }

    bool TryPlace(
        Vector3 approx,
        ZombieRoam[] templates,
        List<Vector3> used,
        Vector3 spawnCenter,
        int createdIndex,
        out Vector3 placed,
        bool allowLooserSpacing = false)
    {
        placed = Vector3.zero;

        if (!TrySampleNavMesh(approx, out Vector3 onMesh))
            return false;

        if (Vector3.Distance(onMesh, spawnCenter) < clearRadiusAroundSpawn)
            return false;

        float spacing = allowLooserSpacing ? minSpacing * 0.85f : minSpacing;
        for (int i = 0; i < used.Count; i++)
        {
            if (Vector3.Distance(onMesh, used[i]) < spacing)
                return false;
        }

        ZombieRoam template = templates[createdIndex % templates.Length];
        GameObject clone = Instantiate(
            template.gameObject,
            onMesh,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        clone.name = template.name + $" (Ambush {createdIndex + 1})";
        Tune(clone.GetComponent<ZombieRoam>());
        used.Add(onMesh);
        placed = onMesh;
        return true;
    }

    void ReactivatePlaced()
    {
        ZombieRoam[] all = FindObjectsByType<ZombieRoam>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int n = 0;
        foreach (ZombieRoam z in all)
        {
            if (z == null)
                continue;
            if (!z.gameObject.activeSelf)
            {
                z.gameObject.SetActive(true);
                n++;
            }

            Tune(z);
        }

        if (n > 0)
            Debug.Log($"Level2AmbushSpawner: reactivated {n} placed aswangs.");
    }

    void Tune(ZombieRoam roam)
    {
        if (roam == null)
            return;

        roam.chaseSpeed = chaseSpeed;
        roam.hitRunSpeed = hitRunSpeed;
        roam.screamAlertRadius = screamAlertRadius;
        roam.breathingRange = breathingRange;
        roam.attackDamage = attackDamage;
        // Wider roam so they cover alleys instead of camping one point.
        roam.roamRadius = Mathf.Max(roam.roamRadius, 22f);
    }

    Vector3 RandomPointInArea()
    {
        return new Vector3(
            Random.Range(areaMin.x, areaMax.x),
            Random.Range(areaMin.y, areaMax.y),
            Random.Range(areaMin.z, areaMax.z));
    }

    static bool TrySampleNavMesh(Vector3 approx, out Vector3 onMesh)
    {
        if (NavMesh.SamplePosition(approx, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            onMesh = hit.position;
            return true;
        }

        onMesh = Vector3.zero;
        return false;
    }

    static Vector3 ResolveSpawnCenter()
    {
        GameObject spawn = GameObject.Find("PlayerSpawn");
        if (spawn != null)
            return spawn.transform.position;

        ThirdPersonController player = Object.FindFirstObjectByType<ThirdPersonController>();
        if (player != null)
            return player.transform.position;

        return new Vector3(20.5f, 6f, -28f);
    }
}

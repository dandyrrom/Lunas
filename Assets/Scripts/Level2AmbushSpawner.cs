using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Level2 escape gauntlet: multiply aswangs across the village so the path is hard to cross.
/// Spawns extras at runtime from existing ZombieRoam templates (NavMesh-sampled).
/// </summary>
public class Level2AmbushSpawner : MonoBehaviour
{
    [Header("Horde")]
    [Tooltip("How many extra aswangs to spawn beyond whatever is already in the scene.")]
    public int extraCount = 30;

    [Tooltip("Re-enable any disabled aswangs already placed in the scene.")]
    public bool reactivatePlacedAswangs = true;

    [Header("Spawn area (world)")]
    public Vector3 areaMin = new Vector3(-110f, 2f, -55f);
    public Vector3 areaMax = new Vector3(10f, 7f, 25f);

    [Tooltip("Keep this radius around PlayerSpawn clear.")]
    public float clearRadiusAroundSpawn = 10f;

    [Tooltip("Minimum spacing between spawned aswangs.")]
    public float minSpacing = 3.5f;

    [Header("Pressure (applied to spawned + reactivated)")]
    public float chaseSpeed = 0.7f;
    public float hitRunSpeed = 0.75f;
    public float screamAlertRadius = 32f;
    public float breathingRange = 1.35f;
    public int attackDamage = 12;

    static readonly Vector3[] FallbackSpots =
    {
        new Vector3(-25f, 5f, -20f),
        new Vector3(-35f, 5f, -10f),
        new Vector3(-45f, 4f, -5f),
        new Vector3(-55f, 3.5f, -15f),
        new Vector3(-65f, 3.5f, -30f),
        new Vector3(-75f, 3f, -20f),
        new Vector3(-85f, 3f, -5f),
        new Vector3(-95f, 3f, -25f),
        new Vector3(-40f, 5f, 5f),
        new Vector3(-50f, 4f, 12f),
        new Vector3(-60f, 3.5f, 8f),
        new Vector3(-70f, 3f, 0f),
        new Vector3(-30f, 5.5f, 0f),
        new Vector3(-20f, 5.5f, -8f),
        new Vector3(-15f, 6f, 8f),
        new Vector3(-48f, 4f, -40f),
        new Vector3(-58f, 3.5f, -45f),
        new Vector3(-80f, 3f, -35f),
        new Vector3(-90f, 3f, -10f),
        new Vector3(-100f, 3f, -5f),
        new Vector3(-38f, 5f, -28f),
        new Vector3(-52f, 4f, -22f),
        new Vector3(-67f, 3.5f, -12f),
        new Vector3(-78f, 3f, 10f),
        new Vector3(-42f, 5f, 15f),
        new Vector3(-28f, 5.5f, 12f),
        new Vector3(-33f, 5f, -35f),
        new Vector3(-88f, 3f, -40f),
        new Vector3(-62f, 3.5f, 18f),
        new Vector3(-18f, 6f, -15f),
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
        int attempts = 0;
        int maxAttempts = Mathf.Max(extraCount * 12, 80);

        while (created < extraCount && attempts < maxAttempts)
        {
            attempts++;
            Vector3 candidate = RandomPointInArea();
            if (!TrySampleNavMesh(candidate, out Vector3 onMesh))
                continue;

            if (Vector3.Distance(onMesh, spawnCenter) < clearRadiusAroundSpawn)
                continue;

            bool tooClose = false;
            for (int i = 0; i < used.Count; i++)
            {
                if (Vector3.Distance(onMesh, used[i]) < minSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            ZombieRoam template = templates[created % templates.Length];
            GameObject clone = Instantiate(template.gameObject, onMesh, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            clone.name = template.name + $" (Ambush {created + 1})";
            Tune(clone.GetComponent<ZombieRoam>());
            used.Add(onMesh);
            created++;
        }

        // If NavMesh sampling failed a lot, drop extras on authored fallback spots.
        for (int i = 0; created < extraCount && i < FallbackSpots.Length; i++)
        {
            Vector3 spot = FallbackSpots[i];
            if (Vector3.Distance(spot, spawnCenter) < clearRadiusAroundSpawn)
                continue;

            TrySampleNavMesh(spot, out Vector3 onMesh);
            if (onMesh == Vector3.zero)
                onMesh = spot;

            bool tooClose = false;
            foreach (Vector3 u in used)
            {
                if (Vector3.Distance(onMesh, u) < minSpacing * 0.75f)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            ZombieRoam template = templates[created % templates.Length];
            GameObject clone = Instantiate(template.gameObject, onMesh, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            clone.name = template.name + $" (Ambush {created + 1})";
            Tune(clone.GetComponent<ZombieRoam>());
            used.Add(onMesh);
            created++;
        }

        Debug.Log($"Level2AmbushSpawner: spawned {created} extra aswangs (requested {extraCount}).");
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
        if (NavMesh.SamplePosition(approx, out NavMeshHit hit, 8f, NavMesh.AllAreas))
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

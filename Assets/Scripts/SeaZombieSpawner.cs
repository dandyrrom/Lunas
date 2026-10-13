using System.Collections;
using UnityEngine;

public class SeaZombieSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject waterZombiePrefab;

    [Header("Markers")]
    [Tooltip("Used in order. The next zombie always comes from the next marker.")]
    public Transform[] spawnPoints;

    [Header("Timing")]
    [Tooltip("Seconds after Play before the first zombie.")]
    public float startDelay = 5f;
    [Tooltip("Seconds after the first zombie before the second one.")]
    public float firstInterval = 18f;
    [Tooltip("How many seconds shorter each following wait is.")]
    public float intervalStep = 2f;
    [Tooltip("The shortest wait. Spawning never gets faster than this.")]
    public float minimumInterval = 6f;

    [Header("Clear")]
    [Tooltip("Spawning stops after this many zombie deaths. Anyone still alive catches fire.")]
    public int killsToStop = 20;
    public GameObject clearFireVfx;
    [Tooltip("Launches after the burn, the lightning, the blackout, and the wait below.")]
    public AnglerfishAttack anglerfish;
    [Tooltip("Seconds after the lightning has faded into the blackout before the first anglerfish leaves.")]
    public float secondsAfterBlackout = 3f;

    int kills;
    int burnsRemaining;
    bool cleared;

    void OnEnable()
    {
        ZombieHealth.OnDied += HandleZombieDied;
    }

    void OnDisable()
    {
        ZombieHealth.OnDied -= HandleZombieDied;
        ZombieHealth.OnDied -= HandleBurnedZombieDied;
    }

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    void HandleZombieDied()
    {
        if (cleared)
            return;

        kills++;
        if (kills >= killsToStop)
            StopAndBurnTheRest();
    }

    void StopAndBurnTheRest()
    {
        cleared = true;
        StopAllCoroutines();

        ZombieHealth.OnDied += HandleBurnedZombieDied;

        ZombieHealth[] zombies = FindObjectsByType<ZombieHealth>(FindObjectsSortMode.None);
        foreach (ZombieHealth zombie in zombies)
        {
            if (zombie == null || zombie.IsDead)
                continue;

            burnsRemaining++;
            zombie.KillByCandleFire(clearFireVfx);
        }

        StartCoroutine(AfterTheBurn());
    }

    void HandleBurnedZombieDied()
    {
        if (burnsRemaining <= 0)
            return;

        burnsRemaining--;
    }

    IEnumerator AfterTheBurn()
    {
        float waited = 0f;
        while (burnsRemaining > 0 && waited < 12f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        ZombieHealth.OnDied -= HandleBurnedZombieDied;
        burnsRemaining = 0;

        Thunderstorm storm = FindAnyObjectByType<Thunderstorm>();
        bool blackoutDone = storm == null;
        if (storm != null)
            storm.PlayStrikeThenBlackout(() => blackoutDone = true);

        while (!blackoutDone)
            yield return null;

        if (secondsAfterBlackout > 0f)
            yield return new WaitForSeconds(secondsAfterBlackout);

        if (anglerfish == null)
            anglerfish = FindAnyObjectByType<AnglerfishAttack>();

        anglerfish?.Launch(firstInterval, intervalStep, minimumInterval);
    }

    IEnumerator SpawnLoop()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        if (waterZombiePrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            yield break;

        int index = 0;
        float interval = Mathf.Max(minimumInterval, firstInterval);

        while (!cleared)
        {
            Transform marker = spawnPoints[index % spawnPoints.Length];
            index++;

            if (marker != null)
            {
                GameObject zombie = Instantiate(waterZombiePrefab);
                zombie.SetActive(true);
                zombie.transform.SetPositionAndRotation(marker.position, marker.rotation);
            }

            float wait = Mathf.Max(0.1f, interval);
            yield return new WaitForSeconds(wait);
            interval = Mathf.Max(minimumInterval, interval - intervalStep);
        }
    }
}

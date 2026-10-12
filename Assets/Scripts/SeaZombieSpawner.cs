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

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        if (waterZombiePrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            yield break;

        int index = 0;
        float interval = Mathf.Max(minimumInterval, firstInterval);

        while (true)
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

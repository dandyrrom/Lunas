using System;
using UnityEngine;

public class ZombieKillScore : MonoBehaviour
{
    [Tooltip("Used when this scene has no Sea Zombie Spawner. Zero counts the zombies already in the scene. A Sea Zombie Spawner overrides this with Kills To Stop.")]
    public int goal;

    public int Killed { get; private set; }
    public int Total { get; private set; }

    public event Action<int, int> OnScoreChanged;

    void Start()
    {
        SeaZombieSpawner spawner = FindAnyObjectByType<SeaZombieSpawner>();
        if (spawner != null && spawner.killsToStop > 0)
            Total = spawner.killsToStop;
        else
            Total = goal > 0
                ? goal
                : FindObjectsByType<ZombieHealth>(FindObjectsSortMode.None).Length;

        Killed = 0;
        OnScoreChanged?.Invoke(Killed, Total);
    }

    public void RegisterKill()
    {
        Killed = Mathf.Min(Killed + 1, Total > 0 ? Total : Killed + 1);
        OnScoreChanged?.Invoke(Killed, Total);
    }
}

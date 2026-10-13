using System;
using UnityEngine;

public class ZombieKillScore : MonoBehaviour
{
    [Tooltip("If greater than 0, the counter shows kills out of this number. Zero keeps Level 1's count of zombies in the scene.")]
    public int goal;

    public int Killed { get; private set; }
    public int Total { get; private set; }

    public event Action<int, int> OnScoreChanged;

    void Start()
    {
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

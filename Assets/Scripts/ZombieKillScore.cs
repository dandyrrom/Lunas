using System;
using UnityEngine;

public class ZombieKillScore : MonoBehaviour
{
    public int Killed { get; private set; }
    public int Total { get; private set; }

    public event Action<int, int> OnScoreChanged;

    void Start()
    {
        // Only count aswangs that are active in the scene (disabled ones stay for later).
        Total = 0;
        foreach (ZombieHealth z in FindObjectsByType<ZombieHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (z != null && z.gameObject.activeInHierarchy)
                Total++;
        }

        Killed = 0;
        OnScoreChanged?.Invoke(Killed, Total);
    }

    public void RegisterKill()
    {
        Killed = Mathf.Min(Killed + 1, Total > 0 ? Total : Killed + 1);
        OnScoreChanged?.Invoke(Killed, Total);
    }
}

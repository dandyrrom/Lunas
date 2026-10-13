using UnityEngine;

/// <summary>
/// Trigger volume at the Level 2 exit / nest marker. Only active after all aswangs are killed.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Level2ExitTrigger : MonoBehaviour
{
    public Level2GameFlow flow;

    bool fired;

    void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (fired || flow == null || !flow.IsPhaseB)
            return;

        if (other.GetComponentInParent<ThirdPersonController>() == null)
            return;

        fired = true;
        flow.NotifyExitReached();
    }
}

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RespawnCheckpoint : MonoBehaviour
{
    [Tooltip("Where she teleports. Defaults to this transform.")]
    public Transform spawnPoint;

    bool activated;

    void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        if (spawnPoint == null)
            spawnPoint = transform;
    }

    void OnTriggerEnter(Collider other)
    {
        if (activated)
            return;

        if (other.GetComponentInParent<ThirdPersonController>() == null)
            return;

        activated = true;

        RespawnManager manager = FindFirstObjectByType<RespawnManager>();
        manager?.SetCheckpoint(spawnPoint);

        // First time she finds this pad: save spawn + refill escape timer.
        Level2GameFlow.Instance?.ResetEscapeTimer();
    }
}
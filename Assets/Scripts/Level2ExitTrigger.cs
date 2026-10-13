using UnityEngine;

/// <summary>
/// Trigger volume at the Level 2 exit marker (boat / shore / gate).
/// Open in escape mode from the start, or after kill-all Phase B.
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

        // CharacterController + trigger is more reliable with a kinematic body on the volume.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void OnTriggerEnter(Collider other) => TryFinish(other);

    void OnTriggerStay(Collider other) => TryFinish(other);

    void TryFinish(Collider other)
    {
        if (fired || flow == null || !flow.IsExitOpen)
            return;

        if (other.GetComponentInParent<ThirdPersonController>() == null &&
            other.GetComponentInParent<CharacterController>() == null)
            return;

        fired = true;
        flow.NotifyExitReached();
    }
}

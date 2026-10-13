using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple third-person follow camera for Level 2.
/// Orbits with mouse and stays behind the player.
/// </summary>
public class PlayerFollowCamera : MonoBehaviour
{
    [Tooltip("Usually the Player root with ThirdPersonController.")]
    public Transform target;

    [Tooltip("Look point height above the player feet.")]
    public float lookHeight = 1.55f;

    [Tooltip("Distance behind the player.")]
    public float distance = 4.2f;

    [Tooltip("Extra height of the camera above the look point.")]
    public float heightOffset = 0.35f;

    public float mouseSensitivity = 0.12f;
    public float minPitch = -25f;
    public float maxPitch = 55f;
    public float followSmooth = 18f;

    float yaw;
    float pitch = 12f;

    void Start()
    {
        if (target == null)
        {
            ThirdPersonController player = FindFirstObjectByType<ThirdPersonController>();
            if (player != null)
                target = player.transform;
        }

        if (target != null)
            yaw = target.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * mouseSensitivity;
            pitch -= delta.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        Vector3 desired = lookPoint - orbit * Vector3.forward * distance + Vector3.up * heightOffset;

        float t = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, orbit, t);
    }
}

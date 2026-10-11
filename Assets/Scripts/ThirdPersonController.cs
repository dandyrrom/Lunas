using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(Animator), typeof(PlayerInput))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Locomotion")]
    [Tooltip("On: Mixamo root motion moves the body. NavMeshAgent only follows the path. Use on Warzombie.")]
    public bool useRootMotion = false;

    [Header("Movement Speeds")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 5.0f;
    [Tooltip("Hold Ctrl to sneak. Slower than walk; zombies cannot hear these steps.")]
    public float sneakSpeed = 1.2f;
    public float rotationSmoothTime = 0.1f;

    [Header("Jump")]
    public float jumpHeight = 1.2f;
    [Tooltip("How long she crouches in place before the hop. Raise this for a longer pause.")]
    public float jumpTakeoffDelay = 0.4f;
    [Tooltip("If on, WASD is ignored during the crouch pause. After she leaves the ground she can move again.")]
    public bool pauseDuringCrouch = true;
    [Tooltip("How long she stands still after landing.")]
    public float landPause = 0.25f;
    [Tooltip("If on, WASD is ignored for Land Pause seconds after she hits the ground.")]
    public bool pauseOnLanding = true;
    [Tooltip("How long she stands still after Hit (H).")]
    public float hitPause = 0.7f;
    [Tooltip("If Space is pressed a moment before she is counted as grounded, still accept the jump.")]
    public float jumpBufferTime = 0.2f;
    [Tooltip("How much WASD steers her in the air. 1 = same as walking.")]
    [Range(0f, 1f)]
    public float airControl = 1f;

    [Header("Jump Noise")]
    [Tooltip("Played when she lands after a Space jump.")]
    public AudioClip jumpLandClip;
    [Tooltip("How far the landing noise can travel for hearing AI.")]
    public float jumpLandNoiseRadius = 15f;

    [Header("Walk Footsteps")]
    [Tooltip("One-shot clip for a single walk step.")]
    public AudioClip walkFootstepClip;
    [Tooltip("Seconds between walk steps.")]
    public float walkStepInterval = 0.5f;
    [Tooltip("How far a walk step can be heard by zombies.")]
    public float walkNoiseRadius = 5f;

    [Header("Run Footsteps")]
    [Tooltip("One-shot clip for a single run step.")]
    public AudioClip runFootstepClip;
    [Tooltip("Seconds between run steps.")]
    public float runStepInterval = 0.32f;
    [Tooltip("How far a run step can be heard by zombies.")]
    public float runNoiseRadius = 14f;

    [Header("Sneak Footsteps")]
    [Tooltip("Optional soft one-shot for the player only. Leave empty for fully silent sneak.")]
    public AudioClip sneakFootstepClip;
    [Tooltip("Seconds between sneak steps (player-facing audio only).")]
    public float sneakStepInterval = 0.65f;

    [Header("Health")]
    [Tooltip("Damage used when testing hits with H.")]
    public int debugHitDamage = 5;

    [Header("Hurt Audio")]
    [Tooltip("Played when she takes damage (H or zombie mid-attack).")]
    public AudioClip hitClip;
    [Tooltip("How far her hurt cry can be heard by zombies.")]
    public float hurtNoiseRadius = 12f;

    [Header("Ladder Climb")]
    [Tooltip("Meters per second along ladder_7, ladder_8, and ladder_9.")]
    public float climbSpeed = 1.35f;
    [Tooltip("How far in front of the steps she can be and still grab with W. Raise this if she has to stand on the ladder. Lower it if she grabs too early.")]
    public float climbFrontReach = 0.85f;
    [Tooltip("How far left or right of the middle of the steps still counts. The side rails are outside this. Lower it if the sides still grab her.")]
    public float climbStepHalfWidth = 0.4f;

    CharacterController controller;
    PlayerHealth health;
    Animator animator;
    PlayerInput playerInput;
    AudioSource audioSource;
    Transform mainCameraTransform;

    InputAction moveAction;
    InputAction sprintAction;
    InputAction sneakAction;
    InputAction jumpAction;

    float currentAngle;
    float currentSpeed;
    float verticalVelocity;
    readonly float gravity = -9.81f;
    readonly float groundedStick = -2f;
    bool isDead;
    float hitUntil;
    bool jumpPending;
    float jumpTakeoffAt;
    float jumpBufferUntil;
    bool jumpAirborne;
    bool jumpedFromHop;
    float landLockedUntil;
    float nextWalkStepTime;
    float nextRunStepTime;
    float nextSneakStepTime;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int SneakHash = Animator.StringToHash("Sneak");
    static readonly int JumpHash = Animator.StringToHash("Jump");
    static readonly int HitHash = Animator.StringToHash("Hit");
    static readonly int DieHash = Animator.StringToHash("Die");
    static readonly int ClimbHash = Animator.StringToHash("Climb");
    static readonly int ClimbSpeedHash = Animator.StringToHash("ClimbSpeed");

    bool isClimbing;
    Vector3 climbOrigin;
    Vector3 climbAxis;
    Vector3 climbFaceDir;
    float climbLength;
    float climbProgress;
    float climbStandDistance;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        playerInput = GetComponent<PlayerInput>();
        audioSource = GetComponent<AudioSource>();

        health = GetComponent<PlayerHealth>();

        if (health != null)
            health.OnDied += HandleDeath;

        if (Camera.main != null)
            mainCameraTransform = Camera.main.transform;

        moveAction = playerInput.actions["Move"];
        sprintAction = playerInput.actions["Sprint"];
        sneakAction = playerInput.actions.FindAction("Sneak");
        jumpAction = playerInput.actions["Jump"];

        Cursor.lockState = CursorLockMode.Locked;

    }


    void OnDestroy()
    {
        if (health != null)
            health.OnDied -= HandleDeath;
    }

    void Update()
    {
        if (Keyboard.current != null && !isDead)
        {
            if (Keyboard.current.hKey.wasPressedThisFrame)
                TakeHit(debugHitDamage);
            if (Keyboard.current.kKey.wasPressedThisFrame)
                health?.Kill();
        }

        CalculateMovement();
    }

    public void TakeHit(int damage)
    {
        if (isDead || health == null)
            return;

        // Optional i-frames between hits. Use 0 if every mid-attack must always land.
        if (Time.time < hitUntil)
            return;

        if (!health.TakeDamage(damage))
            return; // died: OnDied handles death anim

        hitUntil = Time.time + hitPause;

        if (audioSource != null && hitClip != null)
            audioSource.PlayOneShot(hitClip);

        if (hurtNoiseRadius > 0f)
            NoiseEvents.Emit(transform.position, hurtNoiseRadius);

        ExitClimb();

        if (animator != null)
        {
            animator.ResetTrigger(HitHash);
            animator.SetTrigger(HitHash); // interrupt/replay react immediately
        }
    }

    void HandleDeath()
    {
        if (isDead)
            return;
        isDead = true;
        ExitClimb();
        if (animator != null)
            animator.SetTrigger(DieHash);
    }

    /// <summary>
    /// Soft respawn: move to checkpoint and unlock controls after death anim.
    /// </summary>
    public void RespawnAt(Vector3 position, Quaternion rotation)
    {
        isDead = false;
        hitUntil = 0f;
        jumpPending = false;
        jumpAirborne = false;
        jumpedFromHop = false;
        landLockedUntil = 0f;
        jumpBufferUntil = 0f;
        verticalVelocity = groundedStick;
        currentSpeed = 0f;
        ExitClimb();

        if (controller != null)
            controller.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (controller != null)
            controller.enabled = true;

        if (animator != null)
        {
            animator.ResetTrigger(DieHash);
            animator.ResetTrigger(HitHash);
            animator.ResetTrigger(JumpHash);
            animator.SetFloat(SpeedHash, 0f);
            animator.SetBool(SneakHash, false);
            animator.Play("Blend Tree", 0, 0f);
        }
    }

    void CalculateMovement()
    {
        if (isDead)
        {
            if (controller.isGrounded)
                verticalVelocity = groundedStick;
            else
                verticalVelocity += gravity * Time.deltaTime;

            controller.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
            if (animator != null)
            {
                animator.SetFloat(SpeedHash, 0f);
                animator.SetBool(SneakHash, false);
            }
            return;
        }

        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;
        bool wantsSneak = sneakAction != null && sneakAction.IsPressed();
        if (!wantsSneak && Keyboard.current != null)
            wantsSneak = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
        // Ctrl wins over Shift so she stays quiet if both are held.
        bool isSneaking = wantsSneak;
        bool isSprinting = !isSneaking && sprintAction != null && sprintAction.IsPressed();
        bool isHitLocked = Time.time < hitUntil;
        bool grounded = controller.isGrounded;

        if (!isHitLocked && UpdateLadderClimb(input, grounded))
            return;

        if (jumpAction != null && jumpAction.WasPressedThisFrame() && !isHitLocked)
            jumpBufferUntil = Time.time + jumpBufferTime;

        bool wantsJump = Time.time < jumpBufferUntil;
        if (!jumpPending && !jumpAirborne && wantsJump && grounded && !isHitLocked && Time.time >= landLockedUntil)
        {
            jumpPending = true;
            jumpTakeoffAt = Time.time + jumpTakeoffDelay;
            jumpBufferUntil = 0f;
            if (animator != null)
            {
                animator.ResetTrigger(JumpHash);
                animator.SetTrigger(JumpHash);
            }
        }

        if (jumpPending && Time.time >= jumpTakeoffAt)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpPending = false;
            jumpAirborne = true;
            jumpedFromHop = true;
        }
        else if (grounded && !jumpAirborne)
        {
            verticalVelocity = groundedStick;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        if (jumpAirborne && grounded && verticalVelocity <= 0f && !jumpPending)
        {
            jumpAirborne = false;
            if (jumpedFromHop)
            {
                if (pauseOnLanding)
                    landLockedUntil = Time.time + landPause;
                EmitJumpLandNoise();
            }
            jumpedFromHop = false;
        }

        bool crouchLocked = pauseDuringCrouch && jumpPending;
        bool landLocked = pauseOnLanding && Time.time < landLockedUntil;

        float targetSpeed = isSneaking ? sneakSpeed : (isSprinting ? runSpeed : walkSpeed);
        if (input.magnitude < 0.1f || isHitLocked || crouchLocked || landLocked)
            targetSpeed = 0f;
        else if (jumpAirborne)
            targetSpeed *= airControl;

        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 10f);

        bool isMoving = input.magnitude >= 0.1f && targetSpeed > 0.1f;
        bool blocked = isHitLocked || crouchLocked || landLocked;
        bool playSneakAnim = isSneaking && isMoving && grounded && !blocked && !jumpAirborne;

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, runSpeed > 0f ? currentSpeed / runSpeed : 0f);
            animator.SetBool(SneakHash, playSneakAnim);
        }

        Vector3 moveDir = Vector3.zero;
        Transform cam = mainCameraTransform != null ? mainCameraTransform : (Camera.main != null ? Camera.main.transform : null);

        if (!isHitLocked && !crouchLocked && !landLocked && direction.magnitude >= 0.1f)
        {
            float cameraYaw = cam != null ? cam.eulerAngles.y : transform.eulerAngles.y;
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraYaw;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref currentAngle, rotationSmoothTime);

            transform.rotation = Quaternion.Euler(0f, angle, 0f);
            moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        }

        Vector3 finalMovement = moveDir * targetSpeed + new Vector3(0f, verticalVelocity, 0f);
        controller.Move(finalMovement * Time.deltaTime);
        UpdateWalkFootsteps(grounded, isMoving, isSprinting, isSneaking, blocked);
        UpdateRunFootsteps(grounded, isMoving, isSprinting, blocked);
        UpdateSneakFootsteps(grounded, isMoving, isSneaking, blocked);
    }

    void EmitJumpLandNoise()
    {
        if (audioSource != null && jumpLandClip != null)
            audioSource.PlayOneShot(jumpLandClip);

        NoiseEvents.Emit(transform.position, jumpLandNoiseRadius);
    }

    void UpdateWalkFootsteps(bool grounded, bool isMoving, bool isSprinting, bool isSneaking, bool blocked)
    {
        if (!grounded || !isMoving || isSprinting || isSneaking || blocked)
            return;

        if (Time.time < nextWalkStepTime)
            return;

        nextWalkStepTime = Time.time + walkStepInterval;

        if (audioSource != null && walkFootstepClip != null)
            audioSource.PlayOneShot(walkFootstepClip);

        NoiseEvents.Emit(transform.position, walkNoiseRadius);
    }

    void UpdateRunFootsteps(bool grounded, bool isMoving, bool isSprinting, bool blocked)
    {
        if (!grounded || !isMoving || !isSprinting || blocked)
            return;

        if (Time.time < nextRunStepTime)
            return;

        nextRunStepTime = Time.time + runStepInterval;

        if (audioSource != null && runFootstepClip != null)
            audioSource.PlayOneShot(runFootstepClip);

        NoiseEvents.Emit(transform.position, runNoiseRadius);
    }

    void UpdateSneakFootsteps(bool grounded, bool isMoving, bool isSneaking, bool blocked)
    {
        // Player-facing only — never emits NoiseEvents (zombies cannot hear sneak).
        if (!grounded || !isMoving || !isSneaking || blocked)
            return;

        if (sneakFootstepClip == null)
            return;

        if (Time.time < nextSneakStepTime)
            return;

        nextSneakStepTime = Time.time + sneakStepInterval;

        if (audioSource != null)
            audioSource.PlayOneShot(sneakFootstepClip, 0.35f);
    }

    bool UpdateLadderClimb(Vector2 input, bool grounded)
    {
        if (!isClimbing)
        {
            if (!grounded || input.y < 0.5f || !TryMountLadder())
                return false;
        }

        if (jumpAction != null && jumpAction.WasPressedThisFrame())
        {
            ExitClimb();
            verticalVelocity = 0f;
            return true;
        }

        float axis = input.y;
        if (axis > 0.15f && climbProgress >= climbLength - 0.15f)
        {
            DismountLadderTop();
            return true;
        }

        if (axis < -0.15f && climbProgress <= 0.08f)
        {
            ExitClimb();
            verticalVelocity = groundedStick;
            return true;
        }

        if (Mathf.Abs(axis) < 0.15f)
            axis = 0f;
        else
            axis = Mathf.Sign(axis);

        climbProgress = Mathf.Clamp(climbProgress + axis * climbSpeed * Time.deltaTime, 0f, climbLength);
        Vector3 stand = climbOrigin + climbAxis * climbProgress + climbFaceDir * climbStandDistance;
        controller.Move(stand - transform.position);

        if (climbFaceDir.sqrMagnitude > 0.0001f)
        {
            Quaternion face = Quaternion.LookRotation(-climbFaceDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }

        verticalVelocity = 0f;
        currentSpeed = 0f;
        if (animator != null)
        {
            animator.SetBool(ClimbHash, true);
            animator.SetBool(SneakHash, false);
            animator.SetFloat(SpeedHash, 0f);
            animator.SetFloat(ClimbSpeedHash, axis);
        }

        return true;
    }

    static Vector3 LadderFront(Collider col)
    {
        Vector3 front = -col.transform.up;
        front.y = 0f;
        if (front.sqrMagnitude < 0.0001f)
            front = Vector3.forward;
        return front.normalized;
    }

    static bool IsShipLadder(Collider col)
    {
        if (col == null)
            return false;

        string name = col.name;
        return name == "ladder_7" || name == "ladder_8" || name == "ladder_9";
    }

    bool TryMountLadder()
    {
        Vector3 origin = transform.position + Vector3.up * controller.center.y;
        Collider[] hits = Physics.OverlapSphere(origin, 0.75f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        Collider best = null;
        float bestDistance = climbFrontReach;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (!IsShipLadder(col) || col.transform.IsChildOf(transform))
                continue;

            Vector3 front = LadderFront(col);
            Vector3 right = Vector3.Cross(Vector3.up, front);
            Vector3 fromCenter = origin - col.bounds.center;
            float inFront = Vector3.Dot(fromCenter, front);
            float lateral = Vector3.Dot(fromCenter, right);
            if (inFront < 0.05f || inFront > climbFrontReach)
                continue;
            if (Mathf.Abs(lateral) > climbStepHalfWidth)
                continue;
            if (Vector3.Dot(transform.forward, -front) < 0.35f)
                continue;

            if (inFront < bestDistance)
            {
                best = col;
                bestDistance = inFront;
            }
        }

        if (best == null || !BuildLongLadder(best))
            return false;

        float along = Mathf.Clamp(Vector3.Dot(transform.position - climbOrigin, climbAxis), 0f, climbLength);
        if (along > climbLength - 0.7f)
            return false;

        climbFaceDir = LadderFront(best);
        climbStandDistance = controller.radius + 0.22f;
        climbProgress = along;
        isClimbing = true;
        verticalVelocity = 0f;
        jumpPending = false;
        jumpAirborne = false;
        return true;
    }

    bool BuildLongLadder(Collider seed)
    {
        Vector3 axis = seed.transform.forward;
        if (Vector3.Dot(axis, Vector3.up) < 0f)
            axis = -axis;
        if (axis.sqrMagnitude < 0.0001f)
            return false;
        axis.Normalize();

        Collider[] near = Physics.OverlapSphere(seed.bounds.center, 8f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        Vector3 sample = seed.bounds.center;
        bool any = false;
        float minT = 0f;
        float maxT = 0f;

        for (int i = 0; i < near.Length; i++)
        {
            Collider col = near[i];
            if (!IsShipLadder(col))
                continue;

            BoxCollider box = col as BoxCollider;
            Vector3 center = box != null ? col.transform.TransformPoint(box.center) : col.bounds.center;
            Vector3 halfAlong = box != null
                ? col.transform.TransformVector(new Vector3(0f, 0f, box.size.z * 0.5f))
                : axis * 1.23f;
            float start = Vector3.Dot(center - halfAlong - sample, axis);
            float end = Vector3.Dot(center + halfAlong - sample, axis);
            float low = Mathf.Min(start, end);
            float high = Mathf.Max(start, end);

            if (!any)
            {
                minT = low;
                maxT = high;
                any = true;
            }
            else
            {
                minT = Mathf.Min(minT, low);
                maxT = Mathf.Max(maxT, high);
            }
        }

        if (!any || maxT - minT < 0.5f)
            return false;

        climbOrigin = sample + axis * minT;
        climbAxis = axis;
        climbLength = maxT - minT;
        return true;
    }

    void DismountLadderTop()
    {
        Vector3 land = climbOrigin + climbAxis * (climbLength + 0.55f) + climbFaceDir * 0.35f;
        land.y += 0.08f;

        ExitClimb();
        controller.enabled = false;
        transform.position = land;
        controller.enabled = true;
        verticalVelocity = groundedStick;
    }

    void ExitClimb()
    {
        if (!isClimbing)
        {
            if (animator != null)
                animator.SetBool(ClimbHash, false);
            return;
        }

        isClimbing = false;
        if (animator != null)
        {
            animator.SetBool(ClimbHash, false);
            animator.SetFloat(ClimbSpeedHash, 0f);
        }
    }
}

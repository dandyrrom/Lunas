using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class ZombieWaterHunter : MonoBehaviour
{
    [Header("After Climb")]
    [Tooltip("How far to search for the dock NavMesh when the climb ends.")]
    public float landSampleRadius = 4f;

    [Header("Roam")]
    public float roamRadius = 8f;
    public float minIdleTime = 2f;
    public float maxIdleTime = 5f;
    [Tooltip("NavMesh speed while roaming. Keep this between 0.1 and 0.25 so the walk clip plays.")]
    public float roamSpeed = 0.2f;

    [Header("Sight")]
    public float sightRange = 12f;
    [Range(10f, 180f)]
    public float sightAngle = 120f;
    public float eyeHeight = 1.5f;
    public float playerEyeHeight = 1.2f;
    public LayerMask sightBlockers = ~0;

    [Header("Chase")]
    [Tooltip("NavMesh speed while chasing. Above 0.25 plays the run clip.")]
    public float chaseSpeed = 0.8f;

    [Header("Candle")]
    [Tooltip("Seconds to hold still after a candle hit before the death anim.")]
    public float candleFireHoldDuration = 3.5f;

    [Header("Attack")]
    public float attackRange = 0.8f;
    public float attackReachPadding = 0.8f;
    public int attackDamage = 10;
    public float attackDuration = 1.8f;
    [Range(0.1f, 0.9f)]
    public float attackHitNormalized = 0.45f;

    [Header("Target")]
    public Transform player;

    [Header("Audio")]
    [Tooltip("Looped while idle or walking on the dock.")]
    public AudioClip roamClip;
    [Tooltip("Looped while chasing MC.")]
    public AudioClip runClip;
    [Tooltip("Played each time an attack swing starts.")]
    public AudioClip attackClip;
    [Tooltip("Played once when this zombie dies.")]
    public AudioClip deathClip;

    NavMeshAgent agent;
    Animator animator;
    AudioSource audioSource;
    ThirdPersonController playerController;
    ZombieHealth zombieHealth;
    bool deathAudioPlayed;

    bool onLand;
    bool stoppedForDeath;
    bool candleBurning;
    bool isChasing;
    bool isAttacking;
    bool hasDestination;
    float idleUntil;
    float attackStartTime;
    float attackUntil;
    bool hasDealtDamageThisSwing;
    Vector3 attackAnchorPosition;
    Vector3 lastSeenPosition;
    bool hasLastSeen;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int SwimEdgeHash = Animator.StringToHash("swimedge");
    static readonly int ClimbHash = Animator.StringToHash("zclimb");
    static readonly int IdleHash = Animator.StringToHash("breathidle");

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        agent.enabled = false;
        EnsureHittable();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.volume = 0.7f;
        audioSource.minDistance = 1.5f;
        audioSource.maxDistance = 9f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.dopplerLevel = 0f;
    }

    void Start()
    {
        if (player == null)
        {
            ThirdPersonController mc = FindAnyObjectByType<ThirdPersonController>();
            if (mc != null)
                player = mc.transform;
        }

        if (player != null)
            playerController = player.GetComponent<ThirdPersonController>();

        idleUntil = Time.time + Random.Range(minIdleTime, maxIdleTime);
    }

    void EnsureHittable()
    {
        zombieHealth = GetComponent<ZombieHealth>();
        if (zombieHealth == null)
            zombieHealth = gameObject.AddComponent<ZombieHealth>();

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = gameObject.AddComponent<CapsuleCollider>();

        capsule.isTrigger = false;
        capsule.radius = 0.25f;
        capsule.height = 2f;
        capsule.center = new Vector3(0f, 1f, 0f);
        capsule.direction = 1;
    }

    void Update()
    {
        if (stoppedForDeath)
            return;

        if (candleBurning)
            return;

        if (zombieHealth != null && zombieHealth.IsDead)
        {
            StopForDeath();
            return;
        }

        if (!onLand)
        {
            TryLand();
            return;
        }

        if (animator != null && !isAttacking)
            animator.SetFloat(SpeedHash, agent.velocity.magnitude);

        if (isAttacking)
        {
            UpdateAttack();
            return;
        }

        if (isChasing)
        {
            UpdateChase();
            return;
        }

        UpdateRoam();
    }

    void TryLand()
    {
        if (animator == null)
            return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash == SwimEdgeHash || state.shortNameHash == ClimbHash)
            return;
        if (state.shortNameHash != IdleHash)
            return;
        if (animator.IsInTransition(0))
            return;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, landSampleRadius, NavMesh.AllAreas))
            return;

        animator.applyRootMotion = false;
        agent.enabled = true;
        agent.Warp(hit.position);
        agent.isStopped = false;
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.speed = chaseSpeed;
        onLand = true;
        BeginChase();
    }

    void UpdateRoam()
    {
        if (CanSeePlayer())
        {
            BeginChase();
            return;
        }

        if (Time.time < idleUntil)
            return;

        if (!hasDestination)
        {
            if (TryPickDestination(out Vector3 destination))
            {
                agent.isStopped = false;
                agent.SetDestination(destination);
                hasDestination = true;
            }
            else
            {
                idleUntil = Time.time + Random.Range(minIdleTime, maxIdleTime);
            }

            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            hasDestination = false;
            idleUntil = Time.time + Random.Range(minIdleTime, maxIdleTime);
        }
    }

    void BeginChase()
    {
        isChasing = true;
        hasDestination = false;
        idleUntil = 0f;
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        StopRoamAudio();
        StopAttackAudio();
        PlayRunAudio();
        RememberPlayer();
        SetChaseDestination();
    }

    void UpdateChase()
    {
        if (player == null)
            return;

        if (IsPlayerInAttackRange())
        {
            StartAttack();
            return;
        }

        RememberPlayer();
        SetChaseDestination();
    }

    void EndChase()
    {
        isChasing = false;
        hasLastSeen = false;
        hasDestination = false;
        agent.speed = roamSpeed;
        agent.ResetPath();
        StopRunAudio();
        StopAttackAudio();
        PlayRoamAudio();
        idleUntil = Time.time + Random.Range(minIdleTime, maxIdleTime);
    }

    void SetChaseDestination()
    {
        if (!hasLastSeen)
            return;

        if (!NavMesh.SamplePosition(lastSeenPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return;

        agent.isStopped = false;
        agent.SetDestination(hit.position);
    }

    void RememberPlayer()
    {
        if (player == null)
            return;

        lastSeenPosition = player.position;
        hasLastSeen = true;
    }

    void StartAttack()
    {
        if (isAttacking)
            return;

        isAttacking = true;
        hasDealtDamageThisSwing = false;
        hasDestination = false;

        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;
        attackAnchorPosition = transform.position;

        FacePlayer();

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, 0f);
            animator.ResetTrigger(AttackHash);
            animator.SetTrigger(AttackHash);
        }

        StopRunAudio();
        PlayAttackAudio();

        attackStartTime = Time.time;
        attackUntil = Time.time + attackDuration;
    }

    void UpdateAttack()
    {
        transform.position = attackAnchorPosition;

        if (player != null && IsPlayerInAttackRange())
            FacePlayerSmooth();

        if (!hasDealtDamageThisSwing &&
            Time.time >= attackStartTime + attackDuration * attackHitNormalized)
        {
            if (IsPlayerInAttackRange())
            {
                if (playerController != null)
                    playerController.TakeHit(attackDamage);
                hasDealtDamageThisSwing = true;
            }
        }

        if (Time.time < attackUntil)
            return;

        if (!hasDealtDamageThisSwing)
            hasDealtDamageThisSwing = true;

        if (IsPlayerInAttackRange())
        {
            hasDealtDamageThisSwing = false;
            attackStartTime = Time.time;
            attackUntil = Time.time + attackDuration;
            if (animator != null)
                animator.SetTrigger(AttackHash);
            PlayAttackAudio();
            return;
        }

        isAttacking = false;
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.isStopped = false;
        BeginChase();
    }

    public void ReactToBawangHit()
    {
        if (stoppedForDeath || candleBurning || !onLand || isAttacking)
            return;

        BeginChase();
    }

    public void BeginCandleBurn()
    {
        candleBurning = true;
        isAttacking = false;
        isChasing = false;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (animator != null)
            animator.SetFloat(SpeedHash, 0f);

        StopRoamAudio();
        StopRunAudio();
        StopAttackAudio();
    }

    public void StopForDeath()
    {
        if (stoppedForDeath)
            return;

        stoppedForDeath = true;
        candleBurning = false;
        isAttacking = false;
        isChasing = false;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false;
        }

        if (animator != null)
            animator.SetFloat(SpeedHash, 0f);

        PlayDeathAudio();
    }

    bool CanSeePlayer()
    {
        if (player == null)
            return false;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * playerEyeHeight;
        Vector3 to = target - origin;
        float distance = to.magnitude;
        if (distance > sightRange || distance < 0.001f)
            return false;

        Vector3 flat = to;
        flat.y = 0f;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (flat.sqrMagnitude < 0.001f || forward.sqrMagnitude < 0.001f)
            return false;
        if (Vector3.Angle(forward, flat) > sightAngle * 0.5f)
            return false;

        if (Physics.Raycast(origin, to / distance, out RaycastHit hit, distance, sightBlockers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == player || hit.transform.IsChildOf(player) || player.IsChildOf(hit.transform))
                return true;
            return false;
        }

        return true;
    }

    bool IsPlayerInAttackRange()
    {
        return HorizontalDistanceToPlayer() <= attackRange + attackReachPadding;
    }

    float HorizontalDistanceToPlayer()
    {
        if (player == null)
            return float.MaxValue;

        Vector3 zombiePos = transform.position;
        Vector3 playerPos = player.position;
        zombiePos.y = 0f;
        playerPos.y = 0f;
        return Vector3.Distance(zombiePos, playerPos);
    }

    void FacePlayer()
    {
        if (player == null)
            return;

        Vector3 look = player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(look);
    }

    void FacePlayerSmooth()
    {
        if (player == null)
            return;

        Vector3 look = player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(look),
            Time.deltaTime * 5f);
    }

    bool TryPickDestination(out Vector3 destination)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = Random.insideUnitCircle * roamRadius;
            Vector3 randomPoint = transform.position + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, roamRadius, NavMesh.AllAreas))
            {
                destination = hit.position;
                return true;
            }
        }

        destination = transform.position;
        return false;
    }

    void PlayRoamAudio()
    {
        if (audioSource == null || roamClip == null)
            return;

        if (audioSource.isPlaying && audioSource.clip == roamClip)
            return;

        audioSource.clip = roamClip;
        audioSource.loop = true;
        audioSource.Play();
    }

    void StopRoamAudio()
    {
        if (audioSource == null)
            return;

        if (audioSource.clip == roamClip && audioSource.isPlaying)
            audioSource.Stop();
    }

    void PlayRunAudio()
    {
        if (audioSource == null || runClip == null)
            return;

        if (audioSource.isPlaying && audioSource.clip == runClip)
            return;

        audioSource.clip = runClip;
        audioSource.loop = true;
        audioSource.Play();
    }

    void StopRunAudio()
    {
        if (audioSource == null)
            return;

        if (audioSource.clip == runClip && audioSource.isPlaying)
            audioSource.Stop();

        audioSource.loop = false;
    }

    void PlayAttackAudio()
    {
        if (audioSource == null || attackClip == null)
            return;

        audioSource.clip = attackClip;
        audioSource.loop = false;
        audioSource.Play();
    }

    void StopAttackAudio()
    {
        if (audioSource == null)
            return;

        if (audioSource.clip == attackClip && audioSource.isPlaying)
            audioSource.Stop();
    }

    void PlayDeathAudio()
    {
        if (deathAudioPlayed || audioSource == null || deathClip == null)
            return;

        deathAudioPlayed = true;
        StopRoamAudio();
        StopRunAudio();
        StopAttackAudio();
        audioSource.PlayOneShot(deathClip);
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class ZombieBoss : MonoBehaviour
{
    [Header("Clips")]
    public AnimationClip breathingIdle;
    public AnimationClip scream;
    public AnimationClip agonizing;
    public AnimationClip deathClip;

    [Header("Timing")]
    [Tooltip("Seconds he holds Breathing Idle between Scream and Agonizing.")]
    public float idleGap = 1.2f;

    [Header("Health")]
    [Tooltip("Sea zombies have 2. Bawang deals 1. A candle deals Candle Damage and does not kill him outright.")]
    public int maxHealth = 20;
    public int candleDamage = 4;
    [Tooltip("Height of the health bar above his pivot. 5 or higher sits over his head.")]
    public float barHeight = 4.92f;

    [Header("Relocate")]
    [Tooltip("Seconds between each jump to a new spot on the pier.")]
    public float relocateEvery = 7f;
    [Tooltip("How far from where he first appeared he may jump, in meters. Spots over the water are skipped.")]
    public float relocateRadius = 12f;
    [Tooltip("He will not pick a spot closer than this to where he is standing.")]
    public float minMoveDistance = 5f;
    [Tooltip("A landing counts as the pier when the ground under it is this close to his starting height.")]
    public float pierHeightSlack = 0.8f;

    [Header("Defeat")]
    public AudioClip hitClip;
    [Tooltip("Seconds after his health hits zero, while the scream plays, before the storm and rain stop.")]
    public float clearDelay = 3f;

    [Header("Minions")]
    [Tooltip("Seconds after he appears before the first minion, and the first gap between them.")]
    public float minionFirstInterval = 5f;
    [Tooltip("How many seconds shorter each following gap is.")]
    public float minionIntervalStep = 1.5f;
    [Tooltip("The shortest gap. Minions never come faster than this.")]
    public float minionMinimumInterval = 4.5f;

    Animator animator;
    PlayableGraph graph;
    AnimationClipPlayable currentPlayable;
    ZombieHealth health;
    Vector3 home;
    bool appeared;
    bool performing;
    bool defeated;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        EnsureCollider();

        ZombieHealth bossHealth = GetComponent<ZombieHealth>();
        if (bossHealth == null)
            bossHealth = gameObject.AddComponent<ZombieHealth>();
        bossHealth.ConfigureAsBoss(maxHealth, candleDamage);
        health = bossHealth;
        if (GetComponent<BossHealthBar>() == null)
            gameObject.AddComponent<BossHealthBar>();

        if (!appeared)
            gameObject.SetActive(false);
    }

    public void Appear()
    {
        if (appeared)
            return;

        appeared = true;
        gameObject.SetActive(true);

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
            Debug.LogWarning("Zombie Boss has no Animator.");

        home = transform.position;
        StartCoroutine(Perform());
        StartCoroutine(Relocate());
        StartCoroutine(Minions());
    }

    IEnumerator Minions()
    {
        yield return new WaitForSeconds(2f);
        if (!defeated)
            FindAnyObjectByType<SeaZombieSpawner>()?.SpawnOne();

        float interval = Mathf.Max(minionMinimumInterval, minionFirstInterval);

        while (!defeated)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, interval));
            if (defeated)
                yield break;

            if (Random.value < 0.5f)
                FindAnyObjectByType<SeaZombieSpawner>()?.SpawnOne();
            else
                FindAnyObjectByType<AnglerfishAttack>()?.LaunchOne();

            interval = Mathf.Max(minionMinimumInterval, interval - minionIntervalStep);
        }
    }

    IEnumerator Relocate()
    {
        while (!defeated && (health == null || !health.IsDead))
        {
            float wait = Mathf.Max(0.5f, relocateEvery);
            yield return new WaitForSeconds(wait);

            if (defeated || (health != null && health.IsDead))
                yield break;

            transform.position = PickSpot();
        }
    }

    public void OnDefeated()
    {
        if (defeated)
            return;

        defeated = true;
        performing = false;
        StopAllCoroutines();
        FindAnyObjectByType<SeaZombieSpawner>()?.BurnRemaining();

        if (scream != null)
            Play(scream);

        if (hitClip != null)
        {
            AudioSource source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.playOnAwake = false;
            source.PlayOneShot(hitClip);
        }

        StartCoroutine(PlayDeathAfterHit());
        FindAnyObjectByType<Thunderstorm>()?.ClearAfter(clearDelay);
    }

    IEnumerator PlayDeathAfterHit()
    {
        float wait = 0.1f;
        if (scream != null)
            wait = Mathf.Max(wait, scream.length);
        if (hitClip != null)
            wait = Mathf.Max(wait, hitClip.length);

        yield return new WaitForSeconds(wait);

        if (deathClip == null)
        {
            Level3GameFlow.Instance?.NotifyBossDefeated();
            yield break;
        }

        Play(deathClip);
        yield return new WaitForSeconds(Mathf.Max(0.1f, deathClip.length));
        HoldLastFrame();
        Level3GameFlow.Instance?.NotifyBossDefeated();
    }

    void HoldLastFrame()
    {
        if (!currentPlayable.IsValid())
            return;

        currentPlayable.SetTime(currentPlayable.GetDuration());
        currentPlayable.Pause();
    }

    Vector3 PickSpot()
    {
        Vector3 current = transform.position;
        float radius = Mathf.Max(minMoveDistance, relocateRadius);
        for (int i = 0; i < 24; i++)
        {
            Vector2 circle = Random.insideUnitCircle * radius;
            if (circle.magnitude < minMoveDistance)
                circle = circle.normalized * minMoveDistance;
            if (circle.magnitude > relocateRadius)
                continue;

            Vector3 candidate = new Vector3(home.x + circle.x, home.y, home.z + circle.y);
            if (Vector3.Distance(candidate, current) < minMoveDistance)
                continue;
            if (!HasPierUnder(candidate))
                continue;

            return candidate;
        }

        return current;
    }

    bool HasPierUnder(Vector3 spot)
    {
        Vector3[] feet =
        {
            Vector3.zero,
            new Vector3(0.7f, 0f, 0f),
            new Vector3(-0.7f, 0f, 0f),
            new Vector3(0f, 0f, 0.7f),
            new Vector3(0f, 0f, -0.7f)
        };

        for (int i = 0; i < feet.Length; i++)
        {
            if (!PierAt(spot + feet[i]))
                return false;
        }

        return true;
    }

    bool PierAt(Vector3 spot)
    {
        Vector3 origin = new Vector3(spot.x, home.y + 4f, spot.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || col.transform.IsChildOf(transform))
                continue;
            if (Mathf.Abs(hits[i].point.y - home.y) > pierHeightSlack)
                continue;
            if (IsWater(col.gameObject.name))
                continue;

            return true;
        }

        return false;
    }

    static bool IsWater(string objectName)
    {
        string name = objectName.ToLowerInvariant();
        return name.Contains("ocean") || name.Contains("water") || name.Contains("boat");
    }

    void EnsureCollider()
    {
        if (GetComponentInChildren<Collider>() != null)
            return;

        CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.radius = 0.55f;
        capsule.height = 2.2f;
        capsule.center = new Vector3(0f, 1.1f, 0f);

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
        float height = Mathf.Max(1.2f, bounds.size.y);
        capsule.center = localCenter;
        capsule.height = height;
        capsule.radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.55f, 0.35f, 0.9f);
    }

    IEnumerator Perform()
    {
        performing = true;

        if (scream != null)
            yield return PlayOnce(scream);

        while (performing)
        {
            if (breathingIdle != null)
                Play(breathingIdle);

            if (idleGap > 0f)
                yield return new WaitForSeconds(idleGap);

            AnimationClip next = PickPerformance();
            if (next == null)
                yield break;

            yield return PlayOnce(next);
        }
    }

    AnimationClip PickPerformance()
    {
        bool canScream = scream != null;
        bool canAgonize = agonizing != null;
        if (canScream && canAgonize)
            return Random.value < 0.5f ? scream : agonizing;
        if (canScream)
            return scream;
        return agonizing;
    }

    IEnumerator PlayOnce(AnimationClip clip)
    {
        Play(clip);
        float length = Mathf.Max(0.1f, clip.length);
        yield return new WaitForSeconds(length);
    }

    void Play(AnimationClip clip)
    {
        if (clip == null || animator == null)
            return;

        if (graph.IsValid())
            graph.Destroy();

        graph = PlayableGraph.Create("ZombieBoss");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Boss", animator);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        currentPlayable = playable;
        output.SetSourcePlayable(playable);
        graph.Play();
    }

    void OnDisable()
    {
        performing = false;
        if (graph.IsValid())
            graph.Destroy();
    }
}

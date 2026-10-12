using System.Collections;
using UnityEngine;

public class ZombieHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 2;

    [Header("Death")]
    [Tooltip("Seconds before the corpse is destroyed. Match death clip length.")]
    public float deathDestroyDelay = 3f;

    [Header("Candle Fire")]
    [Tooltip("Local offset for fire VFX on the zombie.")]
    public Vector3 fireVfxLocalOffset = new Vector3(0f, 1f, 0f);
    public Vector3 fireVfxLocalScale = Vector3.one;
    [Tooltip("How long the fire fades out before the death anim starts.")]
    public float fireFadeDuration = 0.65f;

    int currentHealth;
    ZombieHitIndicator indicator;
    bool candleBurning;
    bool deathStarted;
    GameObject activeCandleFire;

    public int CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0;

    static readonly int DieHash = Animator.StringToHash("Die");

    void Awake()
    {
        currentHealth = maxHealth;
        indicator = GetComponent<ZombieHitIndicator>();
        if (indicator == null)
            indicator = gameObject.AddComponent<ZombieHitIndicator>();
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || candleBurning || amount <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        indicator?.Flash();

        if (IsDead)
        {
            Die();
            return;
        }

        GetComponent<ZombieRoam>()?.ReactToBawangHit();
        GetComponent<ZombieWaterHunter>()?.ReactToBawangHit();
    }

    /// <summary>
    /// Candle hit: spawn fire, scream, fade fire, then smooth Scream→Death.
    /// </summary>
    public void KillByCandleFire(GameObject fireVfxPrefab)
    {
        if (IsDead || candleBurning)
            return;

        candleBurning = true;
        currentHealth = 0;

        SpawnFireVfx(fireVfxPrefab);

        ZombieRoam roam = GetComponent<ZombieRoam>();
        roam?.BeginCandleBurn();
        GetComponent<ZombieWaterHunter>()?.BeginCandleBurn();

        StartCoroutine(CandleFireDeathRoutine(roam));
    }

    void SpawnFireVfx(GameObject fireVfxPrefab)
    {
        if (fireVfxPrefab == null)
            return;

        activeCandleFire = Instantiate(fireVfxPrefab, transform);
        activeCandleFire.transform.localPosition = fireVfxLocalOffset;
        activeCandleFire.transform.localRotation = Quaternion.identity;
        activeCandleFire.transform.localScale = fireVfxLocalScale;
    }

    void ExtinguishCandleFire()
    {
        if (activeCandleFire == null)
            return;

        Destroy(activeCandleFire);
        activeCandleFire = null;
    }

    IEnumerator CandleFireDeathRoutine(ZombieRoam roam)
    {
        ZombieWaterHunter hunter = GetComponent<ZombieWaterHunter>();
        float total = roam != null
            ? roam.candleFireScreamHoldDuration
            : hunter != null ? hunter.candleFireHoldDuration : 3.5f;
        total = Mathf.Max(0.1f, total);
        float fade = Mathf.Clamp(fireFadeDuration, 0.05f, total * 0.85f);
        float hold = Mathf.Max(0f, total - fade);

        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        yield return FadeOutCandleFire(fade);
        Die();
    }

    IEnumerator FadeOutCandleFire(float duration)
    {
        if (activeCandleFire == null || duration <= 0f)
        {
            ExtinguishCandleFire();
            yield break;
        }

        ParticleSystem[] systems = activeCandleFire.GetComponentsInChildren<ParticleSystem>(true);
        ParticleSystemRenderer[] renderers = activeCandleFire.GetComponentsInChildren<ParticleSystemRenderer>(true);

        foreach (ParticleSystem ps in systems)
        {
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        Color[] startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                startColors[i] = renderers[i].material != null
                    ? renderers[i].material.color
                    : Color.white;
        }

        Vector3 startScale = activeCandleFire.transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration && activeCandleFire != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float k = 1f - (t * t);

            activeCandleFire.transform.localScale = startScale * Mathf.Lerp(1f, 0.15f, t);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color c = startColors[i];
                c.a = startColors[i].a * k;
                if (renderers[i].material != null)
                    renderers[i].material.color = c;
            }

            yield return null;
        }

        ExtinguishCandleFire();
    }

    void Die()
    {
        if (deathStarted)
            return;

        deathStarted = true;

        ExtinguishCandleFire();

        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        ZombieRoam roam = GetComponent<ZombieRoam>();

        Animator anim = GetComponent<Animator>();
        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        // Fire Die while still in Scream + OnFire so Scream→Death blends smoothly.
        if (anim != null && HasTrigger(anim, DieHash))
        {
            anim.ResetTrigger(DieHash);
            anim.SetTrigger(DieHash);
        }

        if (roam != null)
            roam.StopForDeath();

        GetComponent<ZombieWaterHunter>()?.StopForDeath();

        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;

        ZombieKillScore score = FindFirstObjectByType<ZombieKillScore>();
        score?.RegisterKill();

        Destroy(gameObject, deathDestroyDelay);
    }

    static bool HasTrigger(Animator anim, int hash)
    {
        foreach (AnimatorControllerParameter parameter in anim.parameters)
        {
            if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Trigger)
                return true;
        }

        return false;
    }
}

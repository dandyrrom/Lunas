using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RespawnManager : MonoBehaviour
{
    [Header("Checkpoints")]
    [Tooltip("Used until she walks into another pad (usually MC start).")]
    public Transform defaultCheckpoint;

    [Header("Timing")]
    [Tooltip("Wait for death anim before teleporting.")]
    public float respawnDelay = 2.5f;

    Transform currentCheckpoint;
    PlayerLives lives;
    PlayerHealth health;
    ThirdPersonController player;
    bool handlingDeath;

    void Awake()
    {
        currentCheckpoint = defaultCheckpoint;
        lives = FindFirstObjectByType<PlayerLives>();
        health = FindFirstObjectByType<PlayerHealth>();
        player = FindFirstObjectByType<ThirdPersonController>();
    }

    void OnEnable()
    {
        if (health != null)
            health.OnDied += HandleDied;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDied -= HandleDied;
    }

    void Start()
    {
        if (currentCheckpoint == null && player != null)
            currentCheckpoint = player.transform;
    }

    public void SetCheckpoint(Transform point)
    {
        if (point == null)
            return;

        currentCheckpoint = point;
    }

    void HandleDied()
    {
        if (handlingDeath)
            return;

        handlingDeath = true;
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        bool canRespawn = lives != null && lives.TrySpendLife();

        yield return new WaitForSeconds(respawnDelay);

        if (!canRespawn)
        {
            Level1GameFlow level1 = Level1GameFlow.Instance != null
                ? Level1GameFlow.Instance
                : FindFirstObjectByType<Level1GameFlow>();
            if (level1 != null)
            {
                level1.NotifyGameOver();
                handlingDeath = false;
                yield break;
            }

            Level2GameFlow level2 = Level2GameFlow.Instance != null
                ? Level2GameFlow.Instance
                : FindFirstObjectByType<Level2GameFlow>();
            if (level2 != null)
            {
                level2.NotifyGameOver();
                handlingDeath = false;
                yield break;
            }

            Level3GameFlow level3 = Level3GameFlow.Instance != null
                ? Level3GameFlow.Instance
                : FindFirstObjectByType<Level3GameFlow>();
            if (level3 != null)
            {
                level3.NotifyGameOver();
                handlingDeath = false;
                yield break;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            yield break;
        }

        Transform point = currentCheckpoint != null ? currentCheckpoint : defaultCheckpoint;
        if (point == null && player != null)
            point = player.transform;

        if (player != null && point != null)
            player.RespawnAt(point.position, point.rotation);

        if (health != null)
            health.ReviveFull();

        handlingDeath = false;
    }
}

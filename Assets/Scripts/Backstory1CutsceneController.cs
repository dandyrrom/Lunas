using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class Backstory1CutsceneController : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] string gameplayScene = "Level1";
    [SerializeField, Min(0.1f)] float transitionDuration = 0.8f;

    [Header("Cast")]
    [SerializeField] Transform sherall;
    [SerializeField] Transform groom;
    [SerializeField] Transform priest;
    [SerializeField] Transform elder;
    [SerializeField] Transform aswangGuest;

    [Header("Control")]
    [SerializeField] Transform mainCamera;
    [SerializeField] Behaviour cinemachineBrain;
    [SerializeField] Behaviour[] playerControls;

    [Header("Bridal Entrance")]
    [SerializeField] bool allowBrideControlBeforeCeremony = true;
    [SerializeField] Vector3 brideEntryPosition = new Vector3(303.46f, 2.53f, 58f);
    [SerializeField] Vector3 brideCeremonyPosition = new Vector3(296.72f, 3.025f, 39.454f);
    [SerializeField] Vector3 brideCeremonyEuler = new Vector3(0f, 124.775f, 0f);
    [SerializeField, Min(0.1f)] float ceremonyFadeDuration = 0.45f;
    [SerializeField, Min(0.5f)] float ceremonyTriggerDistance = 2.2f;

    [Header("Wedding Candlelight")]
    [SerializeField] bool createWarmCandleGlow = true;
    [SerializeField] Color candleGlowColor = new Color(1f, 0.58f, 0.24f, 1f);
    [SerializeField, Min(0f)] float candleGlowIntensity = 1.6f;
    [SerializeField, Min(0.1f)] float candleGlowRange = 5f;

    [Header("Lively Wedding Lighting")]
    [SerializeField] bool createCeilingWeddingLights = true;
    [SerializeField] Color ceilingLightColor = new Color(1f, 0.88f, 0.68f, 1f);
    [SerializeField, Min(0f)] float ceilingLightIntensity = 4.5f;
    [SerializeField, Min(0.1f)] float ceilingLightRange = 12f;

    [Header("Blocking")]
    [SerializeField] Vector3 groomDisturbedPosition = new Vector3(298.5f, 3.07f, 37.8f);
    [SerializeField] Vector3 elderStartPosition = new Vector3(303.46f, 2.53f, 58.2f);
    [SerializeField] Vector3 elderRearAisleWaypoint = new Vector3(303.46f, 2.53f, 52f);
    [SerializeField] Vector3 elderFrontAisleWaypoint = new Vector3(303.46f, 2.53f, 45.5f);
    [SerializeField] Vector3 elderDestination = new Vector3(300.2f, 2.53f, 41.1f);
    [SerializeField] Vector3 elderWatchCameraPosition = new Vector3(298.6f, 4.15f, 37.4f);
    [SerializeField] Vector3 aswangDestination = new Vector3(309f, 2.53f, 48f);
    [SerializeField, Min(0.1f)] float elderWalkDuration = 7.2f;
    [SerializeField, Min(0.1f)] float aswangWalkDuration = 3f;

    [Header("Church Ground")]
    [SerializeField] float churchFloorY = 2.53f;
    [SerializeField] float altarFloorY = 3.03f;
    [SerializeField] float sittingRootY = 2.94f;
    [SerializeField] float sittingHipY = 3.16f;
    [SerializeField] float sittingSeatClearance = 0.08f;
    [SerializeField] float sittingThighPad = 0.13f;
    [SerializeField] float standingHipHeight = 0.9f;
    [SerializeField] float altarFrontZ = 41.4f;
    [SerializeField] float pewSeatOffset = 0.08f;
    [SerializeField] float pewBackOffset = 0.06f;
    [SerializeField] float sitForwardOffset = -0.12f;

    [Header("Infection Reactions")]
    [SerializeField, Min(0.1f)] float groomHitHoldDuration = 2.35f;
    [SerializeField, Min(0.1f)] float brideShockStepDuration = 3f;
    [SerializeField, Min(0.2f)] float brideShockStepDistance = 1.15f;

    [Header("Guests")]
    [SerializeField] Transform[] sittingGuests;
    [SerializeField] Transform[] guestSources;
    [SerializeField] Transform[] infectedRearGuests;
    [SerializeField] RuntimeAnimatorController guestSitController;
    [SerializeField] RuntimeAnimatorController guestStandController;
    [SerializeField] RuntimeAnimatorController cutsceneActorController;
    [SerializeField] bool spawnSittingGuests = true;
    [SerializeField] bool spawnInfectedRearGuests = false;

    [Header("Dialogue Babble")]
    [SerializeField] bool playDialogueBabble = true;
    [SerializeField, Range(0f, 1f)] float dialogueBabbleVolume = 0.55f;
    [SerializeField] AudioClip maleBabbleClip;
    [SerializeField] AudioClip femaleBabbleClip;
    [SerializeField] Vector3[] infectedRearStartPositions =
    {
        new Vector3(300.4f, 2.53f, 57.2f),
        new Vector3(306.3f, 2.53f, 56.8f)
    };
    [SerializeField] Vector3[] infectedRearDestinations =
    {
        new Vector3(301f, 2.53f, 49.6f),
        new Vector3(306.9f, 2.53f, 50.1f)
    };

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int HitHash = Animator.StringToHash("Hit");
    const string GettingHitState = "getting-hit";
    const string WalkBackState = "walking-backwards";
    const string ZombieScreamState = "zombie-scream";
    const string ZombieAttackState = "zombie-attack";
    const string IdleState = "idle";
    static readonly string[] SittingStates =
    {
        "female-sitting-pose",
        "sitting-and-pointing",
        "sitting-rubbing-arm",
        "sitting-and-pointing",
        "female-sitting-pose",
        "sitting-rubbing-arm"
    };
    static readonly string[] ForbiddenCoupleSitStates =
    {
        "female-sitting-pose",
        "sitting-and-pointing",
        "sitting-rubbing-arm",
        "sitting-and-pointing",
        "female-sitting-pose",
        "sitting-rubbing-arm",
        "kick-to-the-groin"
    };

    Animator sherallAnimator;
    Animator groomAnimator;
    Animator priestAnimator;
    Animator elderAnimator;
    Animator aswangAnimator;
    Animator[] infectedRearAnimators;
    Vector3[] seatedGuestPositions;
    float[] seatedGuestYaws;
    float[] seatedGuestSeatYs;
    Camera sceneCamera;
    float gameplayFieldOfView;
    Texture2D panelTexture;
    Texture2D accentTexture;
    GUIStyle speakerStyle;
    GUIStyle dialogueStyle;
    GUIStyle promptStyle;
    GUIStyle loadingStyle;
    Font loadingFont;
    string currentSpeaker = "";
    string currentDialogue = "";
    float dialogueAlpha;
    float screenFade;
    bool isTransitioning;
    bool sequenceComplete;
    bool awaitingCeremonyStart;
    bool guestsAreSeated;
    bool faceBrideTowardElder;
    AudioSource dialogueAudio;
    Coroutine dialogueBabbleRoutine;

    void Awake()
    {
        awaitingCeremonyStart = allowBrideControlBeforeCeremony;
        SetPlayerControl(awaitingCeremonyStart);
        if (cinemachineBrain != null)
            cinemachineBrain.enabled = awaitingCeremonyStart;

        if (awaitingCeremonyStart && sherall != null)
        {
            sherall.position = SnapToChurchFloor(brideEntryPosition);
            sherall.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        sherallAnimator = FindAnimator(sherall);
        groomAnimator = FindAnimator(groom);
        priestAnimator = FindAnimator(priest);
        elderAnimator = FindAnimator(elder);
        aswangAnimator = FindAnimator(aswangGuest);
        EnsureEnvironmentColliders();
        EnsurePewSeatColliders();
        EnsureChurchFloor();
        EnsureCharacterColliders();
        OpenExteriorChurchOpenings();

        if (elder != null)
            elder.gameObject.SetActive(false);

        if (mainCamera != null)
        {
            sceneCamera = mainCamera.GetComponent<Camera>();
            if (sceneCamera != null)
                gameplayFieldOfView = sceneCamera.fieldOfView;
        }

        SetSpeed(sherallAnimator, 0f);
        SetSpeed(groomAnimator, 0f);
        SetSpeed(priestAnimator, 0f);
        SetSpeed(elderAnimator, 0f);
        SetSpeed(aswangAnimator, 0f);

        panelTexture = MakeTexture(new Color(0.015f, 0.012f, 0.018f, 0.92f));
        accentTexture = MakeTexture(new Color(0.78f, 0.58f, 0.24f, 1f));
        ResolveLoadingFont();

        if (createWarmCandleGlow)
            CreateCandleGlow();
        if (createCeilingWeddingLights)
            CreateCeilingLights();

        SpawnSittingGuests();
        SpawnInfectedRearGuests();
        HideInfectedRearGuests();
        if (aswangGuest != null)
            aswangGuest.gameObject.SetActive(false);
        OpenAisleForBride();
        LockAllAnimatorsToGround();
        SetupDialogueAudio();
    }

    IEnumerator Start()
    {
        if (awaitingCeremonyStart)
            yield return WaitForBrideToBeginCeremony();
        else
            TakeCinematicControl();

        FaceEachOther(sherall, groom);
        ForceBrideStandingPose();
        ForbidCoupleSitAnimation(groomAnimator);
        SetShot(
            Midpoint(sherall, groom, 0f) + new Vector3(0f, 2.4f, 4.8f),
            Midpoint(sherall, groom, 1.35f),
            42f);

        // Guests stand during the bridal entrance; sit once the vows begin.
        SeatGuestsInPlaceForVows();

        yield return ShowLine(
            "NARRATION",
            "Before the altar, Sherall and her groom stood one vow away from becoming husband and wife.",
            4.5f);
        yield return ShowCharacterLine(
            "OFFICIANT",
            "Sherall, do you take him as your husband—in joy, in hardship, and for all your days?",
            4.8f,
            priest,
            1f);
        yield return ShowCharacterLine(
            "SHERALL",
            "I do. Buong puso at buong buhay.",
            3.2f,
            sherall,
            -1f);

        yield return ShowCharacterLine(
            "OFFICIANT",
            "And do you take Sherall as your wife?",
            3.3f,
            priest,
            -1f);
        yield return ShowCharacterLine("GROOM", "I... do.", 2.8f, groom, 1f);

        Coroutine groomActing = StartCoroutine(ActStrangely());
        yield return MoveCamera(
            groom.position + new Vector3(2.4f, 1.65f, 3f),
            groom.position + Vector3.up * 1.45f,
            0.8f,
            36f);
        yield return ShowLine(
            "NARRATION",
            "His hand tightened around hers. His breathing changed, and his eyes followed a sound no one else could hear.",
            5.2f);
        yield return ShowLine("GROOM", "The bells... make them stop. They can hear us.", 3.8f);
        yield return groomActing;

        yield return ShowCharacterLine(
            "GROOM",
            "Sherall... get away from me.",
            3.2f,
            groom,
            1f);
        Coroutine shockCamera = StartCoroutine(WatchBrideShock(Mathf.Max(brideShockStepDuration, 3f)));
        Coroutine brideShock = StartCoroutine(StepBackInShock());
        yield return ShowCharacterLine(
            "SHERALL",
            "What is happening to you?",
            3f,
            sherall,
            -1f,
            false);
        yield return brideShock;
        yield return shockCamera;
        ForceBrideStandingPose();
        ForbidCoupleSitAnimation(groomAnimator);

        PlaceElderAtAisleStart();
        if (elder != null)
            elder.gameObject.SetActive(true);

        if (elderAnimator != null)
        {
            elderAnimator.Rebind();
            elderAnimator.Update(0f);
            SetSpeed(elderAnimator, 0f);
        }

        yield return MoveCamera(
            elderWatchCameraPosition,
            ElderApproachLookTarget(),
            0.85f,
            48f);
        Coroutine elderWalk = StartCoroutine(
            MoveCharacterAlongPath(
                elder,
                new[] { elderRearAisleWaypoint, elderFrontAisleWaypoint, elderDestination },
                elderWalkDuration,
                elderAnimator,
                0.85f,
                false,
                CoupleLookPoint()));
        // Front/altar POV watching the elder walk toward the bride and groom.
        Coroutine elderCamera = StartCoroutine(
            WatchApproachFromAltar(elder, elderWalkDuration, 48f));
        yield return ShowLine(
            "NARRATION",
            "The church doors opened. An elder hurried down the aisle as the guests began to turn.",
            4.4f);
        yield return elderWalk;
        yield return elderCamera;
        faceBrideTowardElder = true;
        FaceBrideAndElder();
        yield return MoveCamera(
            ElderCounselCameraPosition(),
            ElderCounselLookTarget(),
            0.7f,
            42f);
        FaceBrideAndElder();

        yield return ShowCharacterLine(
            "ELDER",
            "Sherall! Huwag mong tapusin ang seremonya!",
            3.2f,
            elder,
            1f,
            false);
        yield return ShowCharacterLine(
            "SHERALL",
            "Lolo, please—what is happening to him?",
            3.4f,
            sherall,
            -1f,
            false);
        yield return ShowCharacterLine(
            "ELDER",
            "Hindi na sila ang mga bisita ninyo. The aswang hunt by sound—keep your voice low.",
            5.2f,
            elder,
            1f,
            false);
        yield return ShowCharacterLine(
            "ELDER",
            "Find bawang and a candle. Their smoke and flame can destroy an aswang.",
            5f,
            elder,
            -1f,
            false);
        yield return ShowCharacterLine(
            "ELDER",
            "Use both against every creature on these grounds. Do not leave a single aswang alive.",
            5.2f,
            elder,
            1f,
            false);
        yield return ShowCharacterLine(
            "SHERALL",
            "Then I will find them and kill every aswang before they hurt anyone else.",
            3.4f,
            sherall,
            -1f,
            false);
        yield return ShowCharacterLine(
            "ELDER",
            "Move quietly, gather the bawang and candle, and strike before they surround you.",
            4.5f,
            elder,
            1f,
            false);

        // Keep the camera on elder / bride / groom — no rear zombie walk cutaway.
        if (aswangGuest != null)
            aswangGuest.gameObject.SetActive(false);
        HideInfectedRearGuests();

        yield return MoveCamera(
            ElderCounselCameraPosition(),
            ElderCounselLookTarget(),
            0.55f,
            42f);
        faceBrideTowardElder = true;
        FaceBrideAndElder();
        yield return ShowCharacterLine(
            "ELDER",
            "Go. Find the bawang and candle. Kill every last aswang.",
            3.8f,
            elder,
            -1f,
            false);
        yield return ShowLine(
            "OBJECTIVE",
            "Collect bawang and a candle. Kill all aswangs.",
            4.5f);
        BeginGameplayTransition();
    }

    IEnumerator WaitForBrideToBeginCeremony()
    {
        while (!BrideReachedGroom())
            yield return null;

        awaitingCeremonyStart = false;
        TakeCinematicControl();
        yield return FadeScreen(1f, ceremonyFadeDuration);

        if (sherall != null)
        {
            CharacterController controller = sherall.GetComponent<CharacterController>();
            bool controllerWasEnabled = controller != null && controller.enabled;
            if (controllerWasEnabled)
                controller.enabled = false;

            sherall.position = SnapToChurchFloor(brideCeremonyPosition);
            sherall.rotation = Quaternion.Euler(brideCeremonyEuler);

            if (controllerWasEnabled)
                controller.enabled = true;
        }

        FaceEachOther(sherall, groom);
        SetShot(
            Midpoint(sherall, groom, 0f) + new Vector3(0f, 2.4f, 4.8f),
            Midpoint(sherall, groom, 1.35f),
            42f);
        yield return WaitUnscaled(0.15f);
        yield return FadeScreen(0f, ceremonyFadeDuration);
    }

    bool BrideReachedGroom()
    {
        if (sherall == null)
            return false;

        Vector3 bride = sherall.position;
        if (groom != null && HorizontalDistanceSq(bride, groom.position) <=
            ceremonyTriggerDistance * ceremonyTriggerDistance)
            return true;

        if (HorizontalDistanceSq(bride, brideCeremonyPosition) <=
            ceremonyTriggerDistance * ceremonyTriggerDistance)
            return true;

        bool inAisle = Mathf.Abs(bride.x - brideEntryPosition.x) <= 3.6f;
        bool atFront = bride.z <= altarFrontZ + 2.4f && bride.z >= altarFrontZ - 5.5f;
        return inAisle && atFront;
    }

    void TakeCinematicControl()
    {
        SetPlayerControl(false);
        SetSpeed(sherallAnimator, 0f);
        if (cinemachineBrain != null)
            cinemachineBrain.enabled = false;
    }

    IEnumerator FadeScreen(float target, float duration)
    {
        float start = screenFade;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            screenFade = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        screenFade = target;
    }

    IEnumerator ShowCharacterLine(
        string speaker,
        string dialogue,
        float duration,
        Transform subject,
        float side,
        bool cutToSpeaker = true)
    {
        if (cutToSpeaker && subject != null)
        {
            yield return MoveCamera(
                subject.position + new Vector3(2.35f * side, 1.7f, 3f),
                subject.position + Vector3.up * 1.4f,
                0.65f,
                36f);
        }

        yield return ShowLine(speaker, dialogue, duration);
    }

    IEnumerator ShowLine(string speaker, string dialogue, float duration)
    {
        currentSpeaker = speaker;
        currentDialogue = dialogue;
        dialogueAlpha = 0f;
        StartDialogueBabble(speaker, dialogue, duration);

        float started = Time.unscaledTime;
        while (Time.unscaledTime - started < duration)
        {
            float elapsed = Time.unscaledTime - started;
            float remaining = duration - elapsed;
            dialogueAlpha = Mathf.Min(
                Mathf.Clamp01(elapsed / 0.25f),
                Mathf.Clamp01(remaining / 0.35f));

            if (SkipSequencePressed())
            {
                StopDialogueBabble();
                BeginGameplayTransition();
                yield break;
            }

            if (elapsed > 0.35f && AdvancePressed())
            {
                StopDialogueBabble();
                yield return FadeDialogueOut(0.18f);
                break;
            }

            yield return null;
        }

        StopDialogueBabble();
        dialogueAlpha = 0f;
        currentSpeaker = "";
        currentDialogue = "";
    }

    IEnumerator FadeDialogueOut(float duration)
    {
        float startAlpha = dialogueAlpha;
        float elapsed = 0f;
        while (elapsed < duration && !sequenceComplete)
        {
            elapsed += Time.unscaledDeltaTime;
            dialogueAlpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
    }

    IEnumerator ActStrangely()
    {
        if (groom == null)
            yield break;

        FaceTarget(groom, sherall != null ? sherall.position : groom.position + groom.forward);
        CharacterController controller = groom.GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        EnsureCutsceneAnimator(groomAnimator);
        ForbidCoupleSitAnimation(groomAnimator);

        Vector3 bridePoint = sherall != null ? sherall.position : groom.position + groom.forward;
        FaceTarget(groom, bridePoint);

        // Subtle infection: a short standing hit spasm while still facing Sherall.
        // No big turn, scream, kick, or sit pose.
        float hold = Mathf.Clamp(groomHitHoldDuration, 1.1f, 1.8f);
        yield return PlayStandingClip(groom, groomAnimator, GettingHitState, hold);

        FaceTarget(groom, bridePoint);
        KeepStandingOnFloor(groom, standingHipHeight);
        RestoreStandingIdle(groomAnimator);
        ForbidCoupleSitAnimation(groomAnimator);

        if (controllerWasEnabled)
            controller.enabled = true;
    }

    IEnumerator StepBackInShock()
    {
        if (sherall == null || groom == null)
            yield break;

        Vector3 start = SnapToChurchFloor(sherall.position);
        Vector3 away = Horizontal(start - groom.position);
        if (away.sqrMagnitude < 0.01f)
            away = -Horizontal(sherall.forward);
        away.Normalize();

        float stepDistance = Mathf.Max(1.35f, brideShockStepDistance);
        Vector3 destination = SnapToChurchFloor(start + away * stepDistance);
        FaceTarget(sherall, groom.position);

        CharacterController controller = sherall.GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        EnsureCutsceneAnimator(sherallAnimator);
        ForbidCoupleSitAnimation(sherallAnimator);
        // Hard-clear any sit controller/pose before she steps back.
        ForceBrideStandingPose();

        float elapsed = 0f;
        float duration = Mathf.Max(2.4f, brideShockStepDuration);
        while (elapsed < duration && !sequenceComplete)
        {
            HoldLoopingState(sherallAnimator, WalkBackState);
            ForbidCoupleSitAnimation(sherallAnimator);
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector3 next = Vector3.Lerp(start, destination, t);
            sherall.position = SnapToChurchFloor(next);
            FaceTarget(sherall, groom.position);
            KeepStandingOnFloor(sherall, standingHipHeight);
            yield return null;
        }

        sherall.position = destination;
        KeepStandingOnFloor(sherall, standingHipHeight);
        FaceTarget(sherall, groom.position);
        ForceBrideStandingPose();

        if (controllerWasEnabled)
            controller.enabled = true;
    }

    void ForceBrideStandingPose()
    {
        if (sherall == null)
            return;

        EnsureCutsceneAnimator(sherallAnimator);
        ForbidCoupleSitAnimation(sherallAnimator);
        if (guestSitController != null &&
            sherallAnimator != null &&
            sherallAnimator.runtimeAnimatorController == guestSitController)
        {
            if (cutsceneActorController != null)
                sherallAnimator.runtimeAnimatorController = cutsceneActorController;
            else if (guestStandController != null)
                sherallAnimator.runtimeAnimatorController = guestStandController;
        }

        RestoreStandingIdle(sherallAnimator);
        KeepStandingOnFloor(sherall, standingHipHeight);
        ForbidCoupleSitAnimation(sherallAnimator);
    }

    void HideInfectedRearGuests()
    {
        if (infectedRearGuests == null)
            return;

        for (int i = 0; i < infectedRearGuests.Length; i++)
        {
            if (infectedRearGuests[i] != null)
                infectedRearGuests[i].gameObject.SetActive(false);
        }
    }

    IEnumerator PlayStandingClip(Transform body, Animator animator, string stateName, float duration)
    {
        EnsureCutsceneAnimator(animator);
        ForbidCoupleSitAnimation(animator);
        float elapsed = 0f;
        while (elapsed < duration && !sequenceComplete)
        {
            HoldLoopingState(animator, stateName);
            ForbidCoupleSitAnimation(animator);
            KeepStandingOnFloor(body, standingHipHeight);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    static void HoldAnimatorState(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        animator.applyRootMotion = false;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName(stateName))
        {
            animator.speed = 1f;
            animator.Play(stateName, 0, 0f);
            animator.Update(0f);
            return;
        }

        if (info.normalizedTime >= 0.92f && !info.loop)
            animator.speed = 0f;
        else
            animator.speed = 1f;
    }

    static void HoldLoopingState(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        animator.applyRootMotion = false;
        animator.speed = 1f;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName(stateName) || info.normalizedTime >= 0.98f)
        {
            animator.Play(stateName, 0, 0f);
            animator.Update(0f);
        }
    }

    void ForbidCoupleSitAnimation(Animator animator)
    {
        if (animator == null)
            return;

        if (guestSitController != null && animator.runtimeAnimatorController == guestSitController)
            EnsureCutsceneAnimator(animator);

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        for (int i = 0; i < ForbiddenCoupleSitStates.Length; i++)
        {
            if (info.IsName(ForbiddenCoupleSitStates[i]))
            {
                RestoreStandingIdle(animator);
                return;
            }
        }
    }

    static void RestoreStandingIdle(Animator animator)
    {
        if (animator == null)
            return;

        animator.applyRootMotion = false;
        animator.speed = 1f;
        animator.SetFloat(SpeedHash, 0f);
        // Prefer dedicated idle; fall back to blend tree controllers.
        if (animator.HasState(0, Animator.StringToHash("idle")))
            animator.Play("idle", 0, 0f);
        else
            animator.Play("Blend Tree", 0, 0f);
        animator.Update(0f);
    }

    IEnumerator WatchBrideShock(float duration)
    {
        if (mainCamera == null || sherall == null || groom == null)
            yield break;

        Vector3 across = Horizontal(sherall.position - groom.position);
        Vector3 side = Vector3.Cross(Vector3.up, across.sqrMagnitude > 0.01f ? across.normalized : Vector3.forward);
        if (side.sqrMagnitude < 0.01f)
            side = Vector3.right;
        side.Normalize();

        Vector3 startLook = Vector3.Lerp(groom.position, sherall.position, 0.55f) + Vector3.up * 1.35f;
        Vector3 cameraPosition = startLook + side * 3.3f + new Vector3(0f, 0.45f, 3.4f);

        float elapsed = 0f;
        while (elapsed < duration && !sequenceComplete)
        {
            Vector3 brideChest = sherall.position + Vector3.up * 1.35f;
            Vector3 look = Vector3.Lerp(groom.position + Vector3.up * 1.4f, brideChest, 0.62f);
            SetShot(cameraPosition, look, 46f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    IEnumerator RotateCharacter(Transform character, Quaternion destination, float duration)
    {
        if (character == null)
            yield break;

        Quaternion start = character.rotation;
        float elapsed = 0f;
        while (elapsed < duration && !sequenceComplete)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            character.rotation = Quaternion.Slerp(start, destination, t);
            yield return null;
        }
    }

    IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !sequenceComplete)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    IEnumerator MoveCharacter(
        Transform character,
        Vector3 destination,
        float duration,
        Animator animator,
        float speed)
    {
        if (character == null)
            yield break;

        Vector3 start = character.position;
        Vector3 direction = destination - start;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            character.rotation = Quaternion.LookRotation(direction);

        SetSpeed(animator, speed);
        float elapsed = 0f;
        CharacterController characterController = character.GetComponent<CharacterController>();
        while (elapsed < duration && !sequenceComplete)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector3 nextPosition = SnapToChurchFloor(Vector3.Lerp(start, destination, t));
            MoveWithCollision(character, characterController, nextPosition);
            yield return null;
        }

        MoveWithCollision(character, characterController, SnapToChurchFloor(destination));
        SetSpeed(animator, 0f);
    }

    IEnumerator MoveCharacterAlongPath(
        Transform character,
        Vector3[] waypoints,
        float duration,
        Animator animator,
        float speed,
        bool ignoreCollision = false,
        Vector3 faceWorldTarget = default)
    {
        if (character == null || waypoints == null || waypoints.Length == 0)
            yield break;

        float totalDistance = 0f;
        Vector3 previous = character.position;
        foreach (Vector3 waypoint in waypoints)
        {
            totalDistance += Vector3.Distance(previous, waypoint);
            previous = waypoint;
        }

        if (totalDistance <= 0.001f)
            yield break;

        CharacterController characterController = character.GetComponent<CharacterController>();
        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (ignoreCollision && controllerWasEnabled)
            characterController.enabled = false;

        bool lockFacing = faceWorldTarget.sqrMagnitude > 0.01f;
        SetSpeed(animator, speed);

        foreach (Vector3 waypoint in waypoints)
        {
            Vector3 start = character.position;
            Vector3 direction = waypoint - start;
            direction.y = 0f;
            float segmentDistance = Vector3.Distance(start, waypoint);
            float segmentDuration = duration * segmentDistance / totalDistance;
            if (lockFacing)
                FaceTarget(character, faceWorldTarget);
            else if (direction.sqrMagnitude > 0.001f)
                character.rotation = Quaternion.LookRotation(direction);

            float elapsed = 0f;
            while (elapsed < segmentDuration && !sequenceComplete)
            {
                elapsed += Time.unscaledDeltaTime;
                float linearT = Mathf.Clamp01(elapsed / Mathf.Max(segmentDuration, 0.01f));
                float smoothT = Mathf.SmoothStep(0f, 1f, linearT);
                if (lockFacing)
                    FaceTarget(character, faceWorldTarget);
                Vector3 nextPosition = SnapToChurchFloor(Vector3.Lerp(start, waypoint, smoothT));
                if (ignoreCollision)
                    character.position = nextPosition;
                else
                    MoveWithCollision(character, characterController, nextPosition);
                yield return null;
            }

            Vector3 groundedWaypoint = SnapToChurchFloor(waypoint);
            if (ignoreCollision)
                character.position = groundedWaypoint;
            else
                MoveWithCollision(character, characterController, groundedWaypoint);
            if (lockFacing)
                FaceTarget(character, faceWorldTarget);
        }

        SetSpeed(animator, 0f);
        if (ignoreCollision && characterController != null && controllerWasEnabled)
            characterController.enabled = true;
    }

    void MoveWithCollision(
        Transform character,
        CharacterController characterController,
        Vector3 destination)
    {
        if (characterController != null && characterController.enabled)
            characterController.Move(destination - character.position);
        else
            character.position = destination;

        if (character != null)
        {
            Vector3 grounded = SnapToChurchFloor(character.position);
            if (Mathf.Abs(character.position.y - grounded.y) > 0.001f)
                character.position = grounded;
        }
    }

    IEnumerator MoveCamera(
        Vector3 destination,
        Vector3 lookTarget,
        float duration,
        float fieldOfView)
    {
        if (mainCamera == null)
            yield break;

        Vector3 startPosition = mainCamera.position;
        Quaternion startRotation = mainCamera.rotation;
        Quaternion endRotation = Quaternion.LookRotation(lookTarget - destination);
        float startFieldOfView = sceneCamera != null ? sceneCamera.fieldOfView : fieldOfView;
        float elapsed = 0f;

        while (elapsed < duration && !sequenceComplete)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            mainCamera.position = Vector3.Lerp(startPosition, destination, t);
            mainCamera.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            if (sceneCamera != null)
                sceneCamera.fieldOfView = Mathf.Lerp(startFieldOfView, fieldOfView, t);
            yield return null;
        }
    }

    IEnumerator WatchApproachFromAltar(Transform subject, float duration, float fieldOfView)
    {
        if (mainCamera == null || subject == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration && !sequenceComplete)
        {
            elapsed += Time.unscaledDeltaTime;
            SetShot(
                elderWatchCameraPosition,
                Vector3.Lerp(subject.position + Vector3.up * 1.45f, ElderApproachLookTarget(), 0.35f),
                fieldOfView);
            yield return null;
        }
    }

    Vector3 ElderApproachLookTarget()
    {
        if (elder == null)
            return elderWatchCameraPosition + Vector3.forward;

        Vector3 elderChest = elder.position + Vector3.up * 1.45f;
        Vector3 aisleAhead = new Vector3(elderStartPosition.x, elderChest.y, elder.position.z);
        return Vector3.Lerp(elderChest, aisleAhead, 0.12f);
    }

    void PlaceElderAtAisleStart()
    {
        if (elder == null)
            return;

        CharacterController controller = elder.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        elder.position = SnapToChurchFloor(elderStartPosition);
        FaceTarget(elder, CoupleLookPoint());
    }

    Vector3 CoupleLookPoint()
    {
        if (sherall != null && groom != null)
            return Midpoint(sherall, groom, 0f);
        if (groom != null)
            return groom.position;
        if (sherall != null)
            return sherall.position;
        return elderDestination;
    }

    Vector3 ElderCounselCameraPosition()
    {
        Vector3 anchor = sherall != null && elder != null
            ? Midpoint(sherall, elder, 0f)
            : CoupleLookPoint();
        return anchor + new Vector3(0f, 2.35f, 5f);
    }

    Vector3 ElderCounselLookTarget()
    {
        Vector3 couple = CoupleLookPoint() + Vector3.up * 1.2f;
        if (elder == null)
            return couple;
        return Vector3.Lerp(couple, elder.position + Vector3.up * 1.4f, 0.3f);
    }

    void SetShot(Vector3 position, Vector3 lookTarget, float fieldOfView)
    {
        if (mainCamera == null)
            return;

        mainCamera.position = position;
        mainCamera.rotation = Quaternion.LookRotation(lookTarget - position);
        if (sceneCamera != null)
            sceneCamera.fieldOfView = fieldOfView;
    }

    void FinishSequence()
    {
        if (sequenceComplete)
            return;

        sequenceComplete = true;
        StopAllCoroutines();
        currentSpeaker = "";
        currentDialogue = "";
        dialogueAlpha = 0f;
        RestoreAnimatorPlayback(sherallAnimator);
        RestoreAnimatorPlayback(groomAnimator);
        RestoreAnimatorPlayback(priestAnimator);
        RestoreAnimatorPlayback(elderAnimator);
        RestoreAnimatorPlayback(aswangAnimator);
        if (infectedRearAnimators != null)
        {
            for (int i = 0; i < infectedRearAnimators.Length; i++)
                RestoreAnimatorPlayback(infectedRearAnimators[i]);
        }

        if (sceneCamera != null)
            sceneCamera.fieldOfView = gameplayFieldOfView;
        if (cinemachineBrain != null)
            cinemachineBrain.enabled = true;
        SetPlayerControl(true);
    }

    void BeginGameplayTransition()
    {
        StopDialogueBabble();
        if (isTransitioning)
            return;

        isTransitioning = true;
        sequenceComplete = true;
        currentSpeaker = "";
        currentDialogue = "";
        dialogueAlpha = 0f;
        SetPlayerControl(false);
        StopAllCoroutines();
        StartCoroutine(LoadGameplay());
    }

    IEnumerator LoadGameplay()
    {
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            screenFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transitionDuration));
            yield return null;
        }

        screenFade = 1f;
        // Hold so LOADING... is readable before Level1 starts.
        yield return new WaitForSecondsRealtime(0.85f);

        if (Application.CanStreamedLevelBeLoaded(gameplayScene))
        {
            SceneManager.LoadScene(gameplayScene);
            yield break;
        }

        Debug.LogError($"Cannot load '{gameplayScene}'. Add the scene to Build Settings.");
        screenFade = 0f;
        isTransitioning = false;
        sequenceComplete = false;
        FinishSequence();
    }

    void SetPlayerControl(bool enabled)
    {
        if (playerControls == null)
            return;

        foreach (Behaviour control in playerControls)
        {
            if (control != null)
                control.enabled = enabled;
        }
    }

    void EnsureEnvironmentColliders()
    {
        MeshFilter[] meshFilters = FindObjectsByType<MeshFilter>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            if (!NeedsSolidEnvironmentCollider(meshFilter.gameObject.name))
                continue;

            Collider existing = meshFilter.GetComponent<Collider>();
            if (existing != null)
            {
                // Keep church props solid so the elder cannot walk through them.
                existing.isTrigger = false;
                continue;
            }

            MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;
            meshCollider.isTrigger = false;
        }

        EnsurePewBlockingColliders();
        Physics.SyncTransforms();
    }

    static bool NeedsSolidEnvironmentCollider(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        string name = objectName.ToLowerInvariant();
        return name.Contains("bench") ||
               name.Contains("pew") ||
               name.Contains("chair") ||
               name.Contains("altar") ||
               name.Contains("pillar") ||
               name.Contains("column") ||
               name.Contains("rail") ||
               name.Contains("wall") ||
               name.Contains("door") ||
               name.Contains("owenground") ||
               name.Contains("ground") ||
               name.Contains("floor") ||
               name.Contains("flowerstand") ||
               name.Contains("foliageplant") ||
               name.Contains("sm_flowers") ||
               name.Contains("flower") ||
               name.Contains("candle") ||
               name.Contains("stand") ||
               name.Contains("fence") ||
               name.Contains("barrier");
    }

    void EnsurePewBlockingColliders()
    {
        Transform[] pews = FindOriginalWeddingPews();
        for (int i = 0; i < pews.Length; i++)
        {
            Transform pew = pews[i];
            if (pew == null)
                continue;

            // Existing mesh colliders stay solid.
            Collider[] existing = pew.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < existing.Length; c++)
            {
                if (existing[c] == null)
                    continue;
                if (existing[c].name == "PewSeatCollider")
                    continue;
                existing[c].isTrigger = false;
            }

            if (pew.Find("PewBlockCollider") != null)
                continue;

            // Extra solid body so CharacterControllers cannot clip through thin bench meshes.
            GameObject block = new GameObject("PewBlockCollider");
            block.transform.SetParent(pew, false);
            block.transform.localPosition = Vector3.zero;
            block.transform.localRotation = Quaternion.identity;
            block.transform.localScale = Vector3.one;
            BoxCollider box = block.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.38f, 0f);
            box.size = new Vector3(1.85f, 0.9f, 0.52f);
            box.isTrigger = false;
        }
    }

    void EnsureCharacterColliders()
    {
        EnsureCharacterController(sherall);
        EnsureCharacterController(groom);
        EnsureCharacterController(priest);
        EnsureCharacterController(elder);
        EnsureCharacterController(aswangGuest);
    }

    void EnsurePewSeatColliders()
    {
        Transform[] pews = FindOriginalWeddingPews();
        for (int i = 0; i < pews.Length; i++)
        {
            Transform pew = pews[i];
            if (pew == null)
                continue;

            Transform existingSeat = pew.Find("PewSeatCollider");
            if (existingSeat != null)
            {
                BoxCollider existingBox = existingSeat.GetComponent<BoxCollider>();
                if (existingBox != null)
                {
                    // Seat volume is solid enough to stop bodies, thin enough for sit poses.
                    existingBox.isTrigger = false;
                    existingBox.center = new Vector3(0f, 0.28f, 0.02f);
                    existingBox.size = new Vector3(1.7f, 0.55f, 0.42f);
                }
                continue;
            }

            GameObject seat = new GameObject("PewSeatCollider");
            seat.transform.SetParent(pew, false);
            seat.transform.localPosition = Vector3.zero;
            seat.transform.localRotation = Quaternion.identity;
            BoxCollider box = seat.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.28f, 0.02f);
            box.size = new Vector3(1.7f, 0.55f, 0.42f);
            box.isTrigger = false;
        }

        Physics.SyncTransforms();
    }

    static void AttachSittingCollider(Transform guest)
    {
        if (guest == null)
            return;

        DisableCharacterController(guest);
        CapsuleCollider capsule = guest.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = guest.gameObject.AddComponent<CapsuleCollider>();

        capsule.direction = 1;
        capsule.center = new Vector3(0f, 0.92f, 0.02f);
        capsule.height = 0.95f;
        capsule.radius = 0.2f;
        capsule.isTrigger = true;
        capsule.enabled = true;
    }

    void SpawnSittingGuests()
    {
        if (!spawnSittingGuests)
            return;

        GuestSeat[] seats = BuildPewSeats();
        if (sittingGuests != null && sittingGuests.Length > 0)
        {
            // Keep guests at their scene placements. Stand first; sit at vows.
            seatedGuestPositions = new Vector3[sittingGuests.Length];
            seatedGuestYaws = new float[sittingGuests.Length];
            seatedGuestSeatYs = new float[sittingGuests.Length];
            guestsAreSeated = false;
            for (int i = 0; i < sittingGuests.Length; i++)
            {
                Transform guest = sittingGuests[i];
                if (guest == null || IsWeddingCastMember(guest))
                    continue;

                CaptureGuestHomePose(guest, i);
                ApplyStandingPoseInPlace(guest, i);
            }

            return;
        }

        if (guestSources == null || guestSources.Length == 0)
            return;

        for (int i = 0; i < seats.Length; i++)
        {
            Transform source = guestSources[i % guestSources.Length];
            if (IsForbiddenGuestSource(source))
                continue;

            Transform guest = InstantiateGuest(source, $"Wedding Guest {i + 1}", WeddingGuestScale());
            if (guest == null)
                continue;

            ApplySittingPose(guest, seats[i], i);
        }
    }

    GuestSeat[] BuildPewSeats()
    {
        Physics.SyncTransforms();
        var seats = new List<GuestSeat>(40);
        Transform[] pews = FindOriginalWeddingPews();
        int sitIndex = 0;
        for (int p = 0; p < pews.Length && seats.Count < 40; p++)
        {
            Transform pew = pews[p];
            Vector3 along = Horizontal(pew.right);
            Vector3 face = Horizontal(pew.up);
            if (face.sqrMagnitude < 0.0001f)
                face = Horizontal(-pew.forward);
            along.Normalize();
            face.Normalize();
            Vector3 origin = PewSeatOrigin(pew, face);
            bool longPew = pew.lossyScale.x >= 1.05f && pew.position.z >= 43.2f;
            int guestCount = longPew ? 2 : 1;
            if (seats.Count + guestCount > 40)
                guestCount = 40 - seats.Count;

            for (int i = 0; i < guestCount; i++)
            {
                float slot = guestCount == 1 ? 0f : (i - (guestCount - 1) * 0.5f) * 0.38f;
                Vector3 position = origin + along * slot;
                if (Mathf.Abs(position.x - brideEntryPosition.x) < 1.2f)
                    continue;

                Vector3 seatPoint;
                float seatY = MeasurePewSeatY(pew, position, out seatPoint);
                position.x = seatPoint.x;
                position.z = seatPoint.z;
                position.y = seatY;
                float yaw = YawTowardCouple(position);
                seats.Add(new GuestSeat(position, yaw, seatY, sitIndex++));
            }
        }

        return seats.ToArray();
    }

    Vector3 PewSeatOrigin(Transform pew, Vector3 face)
    {
        Renderer renderer = pew.GetComponent<Renderer>();
        if (renderer == null)
            renderer = pew.GetComponentInChildren<Renderer>();
        if (renderer == null)
            return pew.position + face * pewSeatOffset - face * pewBackOffset;

        Vector3 center = renderer.bounds.center;
        float depth = Vector3.Dot(Horizontal(center - pew.position), face);
        return pew.position + face * depth + face * pewSeatOffset - face * pewBackOffset;
    }

    float MeasurePewSeatY(Transform pew, Vector3 sitPoint, out Vector3 seatPoint)
    {
        seatPoint = sitPoint;
        Renderer renderer = pew.GetComponent<Renderer>();
        if (renderer == null)
            renderer = pew.GetComponentInChildren<Renderer>();

        float minY = pew.position.y - 0.2f;
        float maxY = pew.position.y + 0.8f;
        if (renderer != null)
        {
            minY = renderer.bounds.min.y;
            maxY = renderer.bounds.max.y;
        }

        float fallbackY = Mathf.Lerp(minY, maxY, 0.36f);
        Vector3 origin = new Vector3(sitPoint.x, maxY + 0.08f, sitPoint.z);
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            maxY - minY + 0.35f,
            ~0,
            QueryTriggerInteraction.Ignore);

        float bestY = float.MinValue;
        bool found = false;
        float backrestCut = maxY - 0.14f;
        float legCut = minY + 0.12f;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || !IsPartOfPew(pew, hit.collider.transform))
                continue;
            if (hit.normal.y < 0.45f)
                continue;
            if (hit.point.y < legCut || hit.point.y > backrestCut)
                continue;
            if (hit.point.y < churchFloorY + 0.2f)
                continue;
            if (hit.point.y > bestY)
            {
                bestY = hit.point.y;
                seatPoint = hit.point;
                found = true;
            }
        }

        if (!found)
        {
            seatPoint = sitPoint;
            seatPoint.y = fallbackY;
            return fallbackY;
        }

        return bestY;
    }

    static bool IsPartOfPew(Transform pew, Transform hit)
    {
        return hit == pew || hit.IsChildOf(pew);
    }

    Transform[] FindOriginalWeddingPews()
    {
        var pews = new List<Transform>();
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate == null || !candidate.name.StartsWith("Bench 2"))
                continue;

            Vector3 position = candidate.position;
            if (position.x < 290f || position.x > 320f || position.z < 35f || position.z > 65f || position.y < 2f)
                continue;

            pews.Add(candidate);
        }

        pews.Sort((a, b) =>
        {
            int z = a.position.z.CompareTo(b.position.z);
            return z != 0 ? z : a.position.x.CompareTo(b.position.x);
        });
        return pews.ToArray();
    }

    float WeddingGuestScale()
    {
        float scale = 0f;
        int count = 0;
        if (sherall != null)
        {
            scale += sherall.localScale.y;
            count++;
        }

        if (groom != null)
        {
            scale += groom.localScale.y;
            count++;
        }

        return count > 0 ? scale / count : 1.25f;
    }

    static Vector3 Horizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    bool IsWeddingCastMember(Transform guest)
    {
        if (guest == null)
            return false;
        return guest == sherall || guest == groom || guest == priest || guest == elder || guest == aswangGuest;
    }

    void CaptureGuestHomePose(Transform guest, int index)
    {
        if (guest == null || seatedGuestPositions == null || index < 0 || index >= seatedGuestPositions.Length)
            return;

        seatedGuestPositions[index] = guest.position;
        seatedGuestYaws[index] = guest.eulerAngles.y;
        seatedGuestSeatYs[index] = guest.position.y;
    }

    void ApplyStandingPoseInPlace(Transform guest, int index)
    {
        if (guest == null)
            return;
        if (IsWeddingCastMember(guest))
            return;

        guest.gameObject.SetActive(true);
        DisableCharacterController(guest);
        KeepGuestAtHomePose(guest, index);

        Animator animator = FindAnimator(guest);
        if (animator == null)
            return;

        RuntimeAnimatorController controller = guestStandController;
        if (controller == null && guestSitController == null && sherallAnimator != null)
            controller = sherallAnimator.runtimeAnimatorController;
        if (controller == null)
            controller = guestSitController;
        if (controller != null)
            animator.runtimeAnimatorController = controller;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = 1f;
        if (guestStandController != null)
            animator.Play("idle", 0, (index * 0.13f) % 1f);
        else
        {
            animator.SetFloat(SpeedHash, 0f);
            animator.Play("Blend Tree", 0, 0f);
        }
        animator.Update(0f);
        KeepGuestAtHomePose(guest, index);
    }

    void SeatGuestsInPlaceForVows()
    {
        if (sittingGuests == null || sittingGuests.Length == 0)
            return;

        if (seatedGuestPositions == null || seatedGuestPositions.Length != sittingGuests.Length)
        {
            seatedGuestPositions = new Vector3[sittingGuests.Length];
            seatedGuestYaws = new float[sittingGuests.Length];
            seatedGuestSeatYs = new float[sittingGuests.Length];
        }

        for (int i = 0; i < sittingGuests.Length; i++)
        {
            Transform guest = sittingGuests[i];
            if (guest == null || IsWeddingCastMember(guest))
                continue;

            if (seatedGuestPositions[i] == Vector3.zero)
                CaptureGuestHomePose(guest, i);
            ApplySittingPoseInPlace(guest, i);
        }

        guestsAreSeated = true;
    }

    void ApplySittingPoseInPlace(Transform guest, int index)
    {
        if (guest == null)
            return;
        if (IsWeddingCastMember(guest))
            return;

        guest.gameObject.SetActive(true);
        DisableCharacterController(guest);
        AttachSittingCollider(guest);

        Vector3 home = seatedGuestPositions != null && index < seatedGuestPositions.Length
            ? seatedGuestPositions[index]
            : guest.position;

        // Seat depth uses the pew's aisle-facing direction so butts land on the plank.
        Vector3 aisleFace = Horizontal(CoupleFocusPoint() - home);
        if (aisleFace.sqrMagnitude < 0.0001f)
            aisleFace = Vector3.forward;
        aisleFace.Normalize();

        Vector3 sitPos = home + aisleFace * sitForwardOffset;
        float seatSurfaceY = EstimateInPlaceSeatHeight(home.y);
        Transform pew = FindNearestWeddingPew(home);
        if (pew != null)
            sitPos = BuildForwardSitPosition(pew, home, aisleFace, out seatSurfaceY);

        // During vows, every seated guest looks toward the bride and groom.
        float yaw = YawTowardCouple(sitPos);

        guest.position = new Vector3(sitPos.x, home.y, sitPos.z);
        guest.rotation = Quaternion.Euler(0f, yaw, 0f);

        Animator animator = FindAnimator(guest);
        if (animator == null)
            return;

        RuntimeAnimatorController controller = guestSitController;
        if (controller == null && sherallAnimator != null)
            controller = sherallAnimator.runtimeAnimatorController;
        if (controller != null)
            animator.runtimeAnimatorController = controller;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = 0.9f + (index % 4) * 0.05f;
        animator.Play(SittingStates[index % SittingStates.Length], 0, 0.2f + (index * 0.07f) % 0.6f);
        animator.Update(0f);

        SettleSittingGuest(guest, seatSurfaceY);
        guest.rotation = Quaternion.Euler(0f, yaw, 0f);
        CaptureGuestHomePose(guest, index);
        if (seatedGuestSeatYs != null && index < seatedGuestSeatYs.Length)
            seatedGuestSeatYs[index] = seatSurfaceY;
    }

    Vector3 CoupleFocusPoint()
    {
        if (sherall != null && groom != null)
            return Midpoint(sherall, groom, 0f);
        if (sherall != null)
            return sherall.position;
        if (groom != null)
            return groom.position;
        return new Vector3(303.46f, churchFloorY, 39f);
    }

    float YawTowardCouple(Vector3 fromPosition)
    {
        Vector3 toCouple = Horizontal(CoupleFocusPoint() - fromPosition);
        if (toCouple.sqrMagnitude < 0.0001f)
            return 0f;
        return Quaternion.LookRotation(toCouple).eulerAngles.y;
    }

    Vector3 BuildForwardSitPosition(Transform pew, Vector3 home, Vector3 face, out float seatY)
    {
        Vector3 pewFace = Horizontal(pew.up);
        if (pewFace.sqrMagnitude < 0.0001f)
            pewFace = Horizontal(-pew.forward);
        pewFace.Normalize();
        if (Vector3.Dot(pewFace, face) < 0f)
            pewFace = -pewFace;

        Vector3 along = Horizontal(pew.right);
        if (along.sqrMagnitude < 0.0001f)
            along = Vector3.Cross(Vector3.up, pewFace);
        along.Normalize();

        Vector3 origin = PewSeatOrigin(pew, pewFace);
        float lateral = Vector3.Dot(home - origin, along);
        // Seat origin is already on the plank; only nudge a little along face.
        // Negative sitForwardOffset pulls toward the backrest (fixes floating in the aisle).
        Vector3 sitPos = origin + along * lateral + pewFace * sitForwardOffset;

        Vector3 seatPoint;
        seatY = MeasurePewSeatY(pew, sitPos, out seatPoint);
        sitPos.x = seatPoint.x;
        sitPos.z = seatPoint.z;

        // If the measured point still sits ahead of the plank, ease back onto the seat.
        Vector3 fromOrigin = Horizontal(sitPos - origin);
        float depth = Vector3.Dot(fromOrigin, pewFace);
        if (depth > 0.06f)
            sitPos -= pewFace * (depth - 0.06f);
        else if (depth < -0.04f)
            sitPos -= pewFace * (depth + 0.04f);

        return sitPos;
    }

    Transform FindNearestWeddingPew(Vector3 position)
    {
        Transform[] pews = FindOriginalWeddingPews();
        Transform best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < pews.Length; i++)
        {
            Transform pew = pews[i];
            if (pew == null)
                continue;

            float dist = HorizontalDistanceSq(position, pew.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = pew;
            }
        }

        return best;
    }

    float EstimateInPlaceSeatHeight(float standingRootY)
    {
        // Mixamo sit clips drop the hips a lot. Raise them onto a seat-like height
        // without changing the guest's placed XZ position.
        float fromStanding = standingRootY + 0.55f;
        float fromChurch = churchFloorY + 0.55f;
        return Mathf.Max(fromStanding, fromChurch, sittingRootY);
    }

    static void SitGuestOnSurface(Transform guest, Animator animator, float seatSurfaceY)
    {
        if (guest == null)
            return;

        Transform leftThigh = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg) : null;
        Transform rightThigh = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightUpperLeg) : null;
        Transform hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;

        float contactY;
        if (leftThigh != null && rightThigh != null)
            contactY = (leftThigh.position.y + rightThigh.position.y) * 0.5f;
        else if (hips != null)
            contactY = hips.position.y - 0.12f;
        else
        {
            // Fallback: hard lift so they cannot remain buried in the floor.
            guest.position += Vector3.up * 0.6f;
            return;
        }

        float lift = seatSurfaceY - contactY;
        if (Mathf.Abs(lift) > 0.001f)
            guest.position += Vector3.up * lift;

        // Second pass after moving root, because humanoid bones move with it.
        if (leftThigh != null && rightThigh != null)
        {
            contactY = (leftThigh.position.y + rightThigh.position.y) * 0.5f;
            lift = seatSurfaceY - contactY;
            if (Mathf.Abs(lift) > 0.001f)
                guest.position += Vector3.up * lift;
        }
    }

    void KeepGuestAtHomePose(Transform character, int index)
    {
        if (character == null || !character.gameObject.activeInHierarchy)
            return;
        if (seatedGuestPositions == null || index < 0 || index >= seatedGuestPositions.Length)
            return;
        if (seatedGuestPositions[index] == Vector3.zero)
            return;

        DisableCharacterController(character);
        character.position = seatedGuestPositions[index];
        character.rotation = Quaternion.Euler(0f, seatedGuestYaws[index], 0f);
    }

    void ApplySittingPose(Transform guest, GuestSeat seat, int index)
    {
        guest.gameObject.SetActive(true);
        DisableCharacterController(guest);
        float weddingScale = WeddingGuestScale();
        guest.localScale = new Vector3(weddingScale, weddingScale, weddingScale);
        Vector3 seated = new Vector3(seat.position.x, seat.seatY, seat.position.z);
        float yaw = YawTowardCouple(seated);
        guest.position = seated;
        guest.rotation = Quaternion.Euler(0f, yaw, 0f);
        AttachSittingCollider(guest);
        if (seatedGuestPositions != null && index < seatedGuestPositions.Length)
        {
            seatedGuestPositions[index] = seated;
            seatedGuestYaws[index] = yaw;
            seatedGuestSeatYs[index] = seat.seatY;
        }

        Animator animator = FindAnimator(guest);
        if (animator == null)
            return;

        RuntimeAnimatorController controller = guestSitController;
        if (controller == null && sherallAnimator != null)
            controller = sherallAnimator.runtimeAnimatorController;
        if (controller != null)
            animator.runtimeAnimatorController = controller;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = 0.9f + (index % 4) * 0.05f;
        animator.Play(SittingStates[seat.sitIndex % SittingStates.Length], 0, 0.2f + (index * 0.07f) % 0.6f);
        animator.Update(0f);
        SettleSittingGuest(guest, seat.seatY);
        if (seatedGuestPositions != null && index < seatedGuestPositions.Length)
            seatedGuestPositions[index] = guest.position;
    }

    void SpawnInfectedRearGuests()
    {
        if (!spawnInfectedRearGuests)
            return;

        if (infectedRearGuests != null && infectedRearGuests.Length >= 2)
        {
            infectedRearAnimators = new Animator[infectedRearGuests.Length];
            for (int i = 0; i < infectedRearGuests.Length; i++)
            {
                Transform guest = infectedRearGuests[i];
                if (guest == null)
                    continue;

                guest.gameObject.SetActive(false);
                infectedRearAnimators[i] = FindAnimator(guest);
                SetSpeed(infectedRearAnimators[i], 0f);
            }

            return;
        }

        if (aswangGuest == null)
            return;

        int startCount = infectedRearStartPositions != null ? infectedRearStartPositions.Length : 0;
        int destinationCount = infectedRearDestinations != null ? infectedRearDestinations.Length : 0;
        int count = Mathf.Min(startCount, destinationCount);
        if (count < 2)
            return;
        infectedRearGuests = new Transform[count];
        infectedRearAnimators = new Animator[count];

        for (int i = 0; i < count; i++)
        {
            Transform guest = InstantiateGuest(aswangGuest, $"Infected Rear Guest {i + 1}", 1f);
            if (guest == null)
                continue;

            guest.position = infectedRearStartPositions[i];
            Vector3 look = infectedRearDestinations[i] - infectedRearStartPositions[i];
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                guest.rotation = Quaternion.LookRotation(look);

            infectedRearGuests[i] = guest;
            infectedRearAnimators[i] = FindAnimator(guest);
            SetSpeed(infectedRearAnimators[i], 0f);
            guest.gameObject.SetActive(false);
        }
    }

    void RevealInfectedRearGuests()
    {
        if (infectedRearGuests == null)
            return;

        for (int i = 0; i < infectedRearGuests.Length; i++)
        {
            if (infectedRearGuests[i] != null)
                infectedRearGuests[i].gameObject.SetActive(true);
        }
    }

    IEnumerator WalkInfectedRearGuests()
    {
        if (infectedRearGuests == null)
            yield break;

        List<Coroutine> walks = new List<Coroutine>();
        for (int i = 0; i < infectedRearGuests.Length; i++)
        {
            if (infectedRearGuests[i] == null)
                continue;

            walks.Add(StartCoroutine(
                MoveCharacter(
                    infectedRearGuests[i],
                    infectedRearDestinations[i],
                    aswangWalkDuration + 0.8f + i * 0.35f,
                    infectedRearAnimators[i],
                    0.55f)));
        }

        for (int i = 0; i < walks.Count; i++)
            yield return walks[i];
    }

    Transform InstantiateGuest(Transform source, string guestName, float uniformScale)
    {
        if (source == null)
            return null;

        GameObject clone = Instantiate(source.gameObject);
        clone.name = guestName;
        clone.tag = "Untagged";
        clone.SetActive(true);
        StripGameplayFromGuest(clone);

        Transform guest = clone.transform;
        guest.SetParent(transform, true);
        guest.localScale = Vector3.one * uniformScale;
        return guest;
    }

    static void StripGameplayFromGuest(GameObject guest)
    {
        foreach (CharacterController controller in guest.GetComponentsInChildren<CharacterController>(true))
            Destroy(controller);

        foreach (Collider collider in guest.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null || collider is CharacterController)
                continue;
            collider.isTrigger = true;
        }

        foreach (Camera camera in guest.GetComponentsInChildren<Camera>(true))
            Destroy(camera);

        foreach (AudioListener listener in guest.GetComponentsInChildren<AudioListener>(true))
            Destroy(listener);

        Behaviour[] behaviours = guest.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour is Animator)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName.Contains("Player") ||
                typeName.Contains("Input") ||
                typeName.Contains("Cinemachine") ||
                typeName.Contains("Starter") ||
                typeName.Contains("Brain"))
            {
                behaviour.enabled = false;
            }
        }
    }

    bool IsForbiddenGuestSource(Transform source)
    {
        if (source == null)
            return true;

        return source == sherall ||
            source == groom ||
            source == elder ||
            source == aswangGuest ||
            source == priest;
    }

    struct GuestSeat
    {
        public Vector3 position;
        public float yaw;
        public float seatY;
        public int sitIndex;

        public GuestSeat(Vector3 position, float yaw, float seatY, int sitIndex)
        {
            this.position = position;
            this.yaw = yaw;
            this.seatY = seatY;
            this.sitIndex = sitIndex;
        }
    }

    void LateUpdate()
    {
        if (sequenceComplete && !awaitingCeremonyStart)
            return;

        if (!awaitingCeremonyStart)
            KeepStandingOnFloor(sherall, standingHipHeight);
        KeepStandingOnFloor(groom, standingHipHeight);
        KeepStandingOnFloor(priest, standingHipHeight);
        KeepStandingOnFloor(elder, standingHipHeight);
        KeepStandingOnFloor(aswangGuest, standingHipHeight);
        ForbidCoupleSitAnimation(sherallAnimator);
        ForbidCoupleSitAnimation(groomAnimator);
        if (faceBrideTowardElder)
            FaceBrideAndElder();
        if (sittingGuests != null)
        {
            for (int i = 0; i < sittingGuests.Length; i++)
            {
                if (guestsAreSeated)
                    KeepSittingOnBench(sittingGuests[i], i);
                else
                    KeepGuestAtHomePose(sittingGuests[i], i);
            }
        }

        if (infectedRearGuests != null)
        {
            for (int i = 0; i < infectedRearGuests.Length; i++)
                KeepStandingOnFloor(infectedRearGuests[i], standingHipHeight);
        }
    }

    void EnsureChurchFloor()
    {
        CreateChurchGround(
            "Church Ceremony Floor",
            new Vector3(303.46f, churchFloorY, 47f),
            new Vector3(22f, 0.25f, 32f));
    }

    void OpenAisleForBride()
    {
        if (sherall == null)
            return;

        CharacterController bride = sherall.GetComponent<CharacterController>();
        if (bride == null)
            return;

        float aisleX = brideEntryPosition.x;
        Transform[] pews = FindOriginalWeddingPews();
        for (int i = 0; i < pews.Length; i++)
        {
            Transform pew = pews[i];
            if (pew == null)
                continue;

            // Only the pews sitting in the flower lane stay passable so she
            // can walk to the altar. Side benches and flowers stay solid.
            if (Mathf.Abs(pew.position.x - aisleX) <= 1.15f)
                IgnoreColliders(bride, pew);
        }
    }

    static void IgnoreColliders(CharacterController bride, Transform root)
    {
        if (bride == null || root == null)
            return;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider other = colliders[i];
            if (other == null || other == bride)
                continue;
            Physics.IgnoreCollision(bride, other, true);
        }
    }

    static float HorizontalDistanceSq(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    void CreateChurchGround(string objectName, Vector3 position, Vector3 size)
    {
        GameObject ground = new GameObject(objectName);
        ground.layer = 0;
        ground.transform.SetParent(transform, false);
        ground.transform.position = position;
        BoxCollider collider = ground.AddComponent<BoxCollider>();
        collider.size = size;
        collider.center = new Vector3(0f, -size.y * 0.5f + 0.01f, 0f);
    }

    void LockAllAnimatorsToGround()
    {
        LockAnimator(sherallAnimator);
        LockAnimator(groomAnimator);
        LockAnimator(priestAnimator);
        LockAnimator(elderAnimator);
        LockAnimator(aswangAnimator);
        if (sittingGuests != null)
        {
            for (int i = 0; i < sittingGuests.Length; i++)
                LockAnimator(FindAnimator(sittingGuests[i]));
        }

        if (infectedRearGuests != null)
        {
            for (int i = 0; i < infectedRearGuests.Length; i++)
                LockAnimator(FindAnimator(infectedRearGuests[i]));
        }
    }

    static void LockAnimator(Animator animator)
    {
        if (animator == null)
            return;

        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
    }

    float GetChurchFloorY(Vector3 position)
    {
        bool onAltarPlatform = position.z <= 40f && position.x >= 294f && position.x <= 301.2f;
        return onAltarPlatform ? altarFloorY : churchFloorY;
    }

    Vector3 SnapToChurchFloor(Vector3 position)
    {
        position.y = GetChurchFloorY(position);
        return position;
    }

    void KeepStandingOnFloor(Transform character, float hipHeight)
    {
        if (character == null || !character.gameObject.activeInHierarchy)
            return;

        GroundFeetToFloor(character, GetChurchFloorY(character.position));
    }

    static void GroundFeetToFloor(Transform character, float floorY)
    {
        if (character == null)
            return;

        Animator animator = FindAnimator(character);
        Transform leftFoot = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
        Transform rightFoot = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
        Transform leftToes = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftToes) : null;
        Transform rightToes = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightToes) : null;
        if (leftFoot == null && rightFoot == null)
        {
            Vector3 position = character.position;
            position.y = floorY;
            character.position = position;
            return;
        }

        float lowest = float.PositiveInfinity;
        if (leftFoot != null)
            lowest = Mathf.Min(lowest, leftFoot.position.y);
        if (rightFoot != null)
            lowest = Mathf.Min(lowest, rightFoot.position.y);
        if (leftToes != null)
            lowest = Mathf.Min(lowest, leftToes.position.y);
        if (rightToes != null)
            lowest = Mathf.Min(lowest, rightToes.position.y);

        float lift = floorY - lowest;
        if (Mathf.Abs(lift) > 0.004f)
            character.position += Vector3.up * lift;
    }

    void KeepSittingOnBench(Transform character, int index)
    {
        if (character == null || !character.gameObject.activeInHierarchy)
            return;

        DisableCharacterController(character);
        Vector3 seated = character.position;
        float yaw = character.eulerAngles.y;
        float seatY = sittingHipY;
        if (seatedGuestPositions != null && index < seatedGuestPositions.Length && seatedGuestPositions[index] != Vector3.zero)
        {
            seated.x = seatedGuestPositions[index].x;
            seated.z = seatedGuestPositions[index].z;
            yaw = seatedGuestYaws[index];
        }

        if (seatedGuestSeatYs != null && index < seatedGuestSeatYs.Length && seatedGuestSeatYs[index] > 0.1f)
            seatY = seatedGuestSeatYs[index];

        character.position = seated;
        character.rotation = Quaternion.Euler(0f, yaw, 0f);
        SettleSittingGuest(character, seatY);
    }

    void SettleSittingGuest(Transform character, float seatY)
    {
        if (character == null)
            return;

        Animator animator = FindAnimator(character);
        float scale = Mathf.Max(character.lossyScale.y, 0.25f);
        Transform leftThigh = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg) : null;
        Transform rightThigh = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightUpperLeg) : null;
        Transform hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;

        float referenceY;
        if (leftThigh != null && rightThigh != null)
            referenceY = (leftThigh.position.y + rightThigh.position.y) * 0.5f;
        else if (hips != null)
            referenceY = hips.position.y;
        else
            return;

        float contactY = referenceY - sittingThighPad * scale;
        float targetY = seatY + sittingSeatClearance;
        character.position += Vector3.up * (targetY - contactY);
    }

    static void LiftHipsToMinimum(Transform character, float minHipY)
    {
        Transform hips = GetHips(character);
        if (hips != null)
        {
            float lift = minHipY - hips.position.y;
            if (lift > 0.01f)
                character.position += Vector3.up * lift;
            return;
        }

        LiftMeshAbove(character, minHipY - 0.9f);
    }

    static void PinHipsTo(Transform character, float hipY)
    {
        Transform hips = GetHips(character);
        if (hips != null)
        {
            character.position += Vector3.up * (hipY - hips.position.y);
            return;
        }

        LiftMeshAbove(character, hipY - 0.45f);
    }

    static void LiftMeshAbove(Transform character, float minY)
    {
        SkinnedMeshRenderer renderer = character.GetComponentInChildren<SkinnedMeshRenderer>();
        if (renderer == null)
            return;

        float lift = minY - renderer.bounds.min.y;
        if (lift > 0.01f)
            character.position += Vector3.up * lift;
    }

    static Transform GetHips(Transform character)
    {
        Animator animator = FindAnimator(character);
        if (animator == null)
            return null;

        animator.applyRootMotion = false;
        return animator.GetBoneTransform(HumanBodyBones.Hips);
    }

    static void DisableCharacterController(Transform character)
    {
        if (character == null)
            return;

        CharacterController controller = character.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;
    }

    static void EnsureCharacterController(Transform character)
    {
        if (character == null)
            return;

        CharacterController controller = character.GetComponent<CharacterController>();
        if (controller == null)
            controller = character.gameObject.AddComponent<CharacterController>();

        controller.center = new Vector3(0f, 1f, 0f);
        controller.height = 2f;
        controller.radius = 0.28f;
        controller.slopeLimit = 45f;
        controller.stepOffset = 0.25f;
        controller.skinWidth = 0.06f;
        controller.minMoveDistance = 0.001f;
        controller.enabled = true;
    }

    static Animator FindAnimator(Transform character)
    {
        return character != null ? character.GetComponentInChildren<Animator>() : null;
    }

    static void SetSpeed(Animator animator, float speed)
    {
        if (animator != null)
            animator.SetFloat(SpeedHash, speed);
    }

    static void PlayState(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        animator.applyRootMotion = false;
        animator.speed = 1f;
        animator.Play(stateName, 0, 0f);
        animator.Update(0f);
    }

    static void RestoreAnimatorPlayback(Animator animator)
    {
        if (animator == null)
            return;

        animator.speed = 1f;
        animator.SetFloat(SpeedHash, 0f);
        animator.Play("Blend Tree", 0, 0f);
    }

    void FaceBrideAndElder()
    {
        if (sherall != null && elder != null)
        {
            FaceTarget(sherall, elder.position);
            FaceTarget(elder, sherall.position);
        }
    }

    void EnsureCutsceneAnimator(Animator animator)
    {
        if (animator == null)
            return;

        // Never force the guest sit controller onto bride/groom/cast.
        RuntimeAnimatorController controller = cutsceneActorController;
        if (controller == null)
            controller = animator.runtimeAnimatorController;
        if (guestSitController != null && controller == guestSitController)
            controller = cutsceneActorController;
        if (controller == null && sherallAnimator != null &&
            sherallAnimator.runtimeAnimatorController != guestSitController)
            controller = sherallAnimator.runtimeAnimatorController;
        if (controller != null && animator.runtimeAnimatorController != controller)
            animator.runtimeAnimatorController = controller;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = 1f;
        ForbidCoupleSitAnimation(animator);
    }

    static void FaceTarget(Transform character, Vector3 worldTarget)
    {
        if (character == null)
            return;

        Vector3 direction = worldTarget - character.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            character.rotation = Quaternion.LookRotation(direction);
    }

    static void TriggerReaction(Animator animator)
    {
        if (animator != null)
            animator.SetTrigger(HitHash);
    }

    static void FaceEachOther(Transform first, Transform second)
    {
        if (first == null || second == null)
            return;

        Vector3 firstDirection = second.position - first.position;
        firstDirection.y = 0f;
        if (firstDirection.sqrMagnitude > 0.001f)
            first.rotation = Quaternion.LookRotation(firstDirection);

        Vector3 secondDirection = first.position - second.position;
        secondDirection.y = 0f;
        if (secondDirection.sqrMagnitude > 0.001f)
            second.rotation = Quaternion.LookRotation(secondDirection);
    }

    static Vector3 Midpoint(Transform first, Transform second, float height)
    {
        if (first == null || second == null)
            return Vector3.up * height;
        return (first.position + second.position) * 0.5f + Vector3.up * height;
    }

    static bool AdvancePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null &&
            (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
    }

    static bool SkipSequencePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
    }

    void OnGUI()
    {
        if (screenFade > 0f)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, screenFade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), panelTexture);
            GUI.color = previous;

            if (isTransitioning && screenFade > 0.45f)
                DrawLoadingText(Mathf.Clamp01((screenFade - 0.45f) / 0.55f));
        }

        if (awaitingCeremonyStart)
        {
            DrawCeremonyPrompt();
            return;
        }

        if (sequenceComplete || string.IsNullOrEmpty(currentDialogue))
            return;

        EnsureStyles();
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.7f, 1.4f);
        float panelWidth = Mathf.Min(Screen.width * 0.78f, 1380f * scale);
        float panelHeight = 220f * scale;
        float panelX = (Screen.width - panelWidth) * 0.5f;
        float panelY = Screen.height - panelHeight - 42f * scale;

        Color previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, dialogueAlpha);
        GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, panelHeight), panelTexture);
        GUI.DrawTexture(new Rect(panelX, panelY, 6f * scale, panelHeight), accentTexture);

        speakerStyle.fontSize = Mathf.RoundToInt(22f * scale);
        dialogueStyle.fontSize = Mathf.RoundToInt(26f * scale);
        promptStyle.fontSize = Mathf.RoundToInt(16f * scale);

        float left = panelX + 38f * scale;
        GUI.Label(
            new Rect(left, panelY + 18f * scale, panelWidth - 70f * scale, 32f * scale),
            currentSpeaker,
            speakerStyle);
        GUI.Label(
            new Rect(left, panelY + 54f * scale, panelWidth - 76f * scale, 122f * scale),
            currentDialogue,
            dialogueStyle);
        GUI.Label(
            new Rect(left, panelY + 182f * scale, panelWidth - 76f * scale, 24f * scale),
            "SPACE / ENTER  Continue     ESC  Skip",
            promptStyle);
        GUI.color = previousColor;
    }

    void DrawLoadingText(float alpha)
    {
        EnsureStyles();
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.7f, 1.4f);
        loadingStyle.fontSize = Mathf.RoundToInt(40f * scale);

        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(
            new Rect(0f, Screen.height * 0.46f, Screen.width, 70f * scale),
            "LOADING...",
            loadingStyle);
        GUI.color = previous;
    }

    void DrawCeremonyPrompt()
    {
        EnsureStyles();
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.7f, 1.4f);
        float panelWidth = Mathf.Min(Screen.width * 0.72f, 1100f * scale);
        float panelHeight = 168f * scale;
        float panelX = (Screen.width - panelWidth) * 0.5f;
        float panelY = Screen.height - panelHeight - 48f * scale;

        GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, panelHeight), panelTexture);
        GUI.DrawTexture(new Rect(panelX, panelY, 6f * scale, panelHeight), accentTexture);

        speakerStyle.fontSize = Mathf.RoundToInt(20f * scale);
        dialogueStyle.fontSize = Mathf.RoundToInt(22f * scale);
        float left = panelX + 34f * scale;
        GUI.Label(
            new Rect(left, panelY + 14f * scale, panelWidth - 68f * scale, 28f * scale),
            "BRIDAL ENTRANCE",
            speakerStyle);
        GUI.Label(
            new Rect(left, panelY + 46f * scale, panelWidth - 68f * scale, 108f * scale),
            "Use WASD to walk Sherall down the flower aisle. The ceremony begins when you reach the front, before the groom.",
            dialogueStyle);
    }


    void SetupDialogueAudio()
    {
        dialogueAudio = gameObject.GetComponent<AudioSource>();
        if (dialogueAudio == null)
            dialogueAudio = gameObject.AddComponent<AudioSource>();

        dialogueAudio.playOnAwake = false;
        dialogueAudio.loop = false;
        dialogueAudio.spatialBlend = 0f;
        dialogueAudio.volume = dialogueBabbleVolume;
        ResolveFreesoundBabbleClips();
    }

    void ResolveFreesoundBabbleClips()
    {
        if (maleBabbleClip != null && femaleBabbleClip != null)
            return;

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("freesound t:AudioClip", new[] { "Assets/Audio" });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            AudioClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null)
                continue;

            string name = clip.name.ToLowerInvariant();
            if (maleBabbleClip == null && name.Contains("male"))
                maleBabbleClip = clip;
            else if (femaleBabbleClip == null && name.Contains("female"))
                femaleBabbleClip = clip;
        }

        // If names are generic freesound_* without male/female, take first two.
        if ((maleBabbleClip == null || femaleBabbleClip == null) && guids.Length > 0)
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                AudioClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip == null)
                    continue;
                if (maleBabbleClip == null)
                    maleBabbleClip = clip;
                else if (femaleBabbleClip == null && clip != maleBabbleClip)
                    femaleBabbleClip = clip;
            }
        }
#endif

        if (maleBabbleClip == null)
            maleBabbleClip = Resources.Load<AudioClip>("freesound-male-babble");
        if (femaleBabbleClip == null)
            femaleBabbleClip = Resources.Load<AudioClip>("freesound-female-babble");
    }

    void StartDialogueBabble(string speaker, string dialogue, float duration)
    {
        StopDialogueBabble();
        if (!playDialogueBabble || dialogueAudio == null)
            return;

        dialogueBabbleRoutine = StartCoroutine(PlayDialogueBabble(speaker, duration));
    }

    void StopDialogueBabble()
    {
        if (dialogueBabbleRoutine != null)
        {
            StopCoroutine(dialogueBabbleRoutine);
            dialogueBabbleRoutine = null;
        }

        if (dialogueAudio != null && dialogueAudio.isPlaying)
            dialogueAudio.Stop();
        if (dialogueAudio != null)
        {
            dialogueAudio.clip = null;
            dialogueAudio.pitch = 1f;
            dialogueAudio.loop = false;
        }
    }

    IEnumerator PlayDialogueBabble(string speaker, float duration)
    {
        AudioClip clip = GetBabbleClipForSpeaker(speaker);
        if (clip == null)
            yield break;

        float pitch = GetBabblePitchForSpeaker(speaker);
        dialogueAudio.clip = clip;
        dialogueAudio.pitch = pitch;
        dialogueAudio.volume = dialogueBabbleVolume;
        dialogueAudio.loop = clip.length + 0.05f < Mathf.Max(0.4f, duration - 0.2f);
        dialogueAudio.time = 0f;
        dialogueAudio.Play();

        float endAt = Time.unscaledTime + Mathf.Max(0.25f, duration - 0.15f);
        while (Time.unscaledTime < endAt && !sequenceComplete)
        {
            if (!dialogueAudio.isPlaying && dialogueAudio.loop)
                dialogueAudio.Play();
            yield return null;
        }

        if (dialogueAudio != null)
            dialogueAudio.Stop();
    }

    AudioClip GetBabbleClipForSpeaker(string speaker)
    {
        string key = NormalizeSpeakerKey(speaker);
        bool female = key == "SHERALL";
        AudioClip preferred = female ? femaleBabbleClip : maleBabbleClip;
        if (preferred != null)
            return preferred;
        return femaleBabbleClip != null ? femaleBabbleClip : maleBabbleClip;
    }

    static float GetBabblePitchForSpeaker(string speaker)
    {
        switch (NormalizeSpeakerKey(speaker))
        {
            case "SHERALL":
                return 1.12f;
            case "GROOM":
                return 0.96f;
            case "OFFICIANT":
                return 1.0f;
            case "ELDER":
                return 0.84f;
            default:
                return 0.92f;
        }
    }

    static string NormalizeSpeakerKey(string speaker)
    {
        if (string.IsNullOrWhiteSpace(speaker))
            return "NARRATION";

        string key = speaker.Trim().ToUpperInvariant();
        if (key.Contains("SHER") || key.Contains("BRIDE"))
            return "SHERALL";
        if (key.Contains("GROOM"))
            return "GROOM";
        if (key.Contains("PRIEST") || key.Contains("OFFICIANT"))
            return "OFFICIANT";
        if (key.Contains("ELDER"))
            return "ELDER";
        if (key.Contains("NARR"))
            return "NARRATION";
        return key;
    }

    void EnsureStyles()
    {
        if (speakerStyle != null)
            return;

        speakerStyle = CreateStyle(FontStyle.Bold, new Color(0.9f, 0.7f, 0.36f));
        dialogueStyle = CreateStyle(FontStyle.Normal, new Color(0.96f, 0.94f, 0.9f));
        dialogueStyle.wordWrap = true;
        dialogueStyle.alignment = TextAnchor.UpperLeft;
        promptStyle = CreateStyle(FontStyle.Normal, new Color(0.62f, 0.58f, 0.54f));
        loadingStyle = CreateStyle(FontStyle.Normal, new Color(0.92f, 0.78f, 0.7f));
        loadingStyle.alignment = TextAnchor.MiddleCenter;
        if (loadingFont != null)
            loadingStyle.font = loadingFont;
    }

    void ResolveLoadingFont()
    {
        if (loadingFont != null)
            return;

        loadingFont = Resources.Load<Font>("Blood Victim Zombie");
#if UNITY_EDITOR
        if (loadingFont == null)
        {
            string[] fontGuids = UnityEditor.AssetDatabase.FindAssets("Blood Victim Zombie t:Font");
            if (fontGuids.Length > 0)
            {
                string fontPath = UnityEditor.AssetDatabase.GUIDToAssetPath(fontGuids[0]);
                loadingFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            }
        }
#endif
    }

    static GUIStyle CreateStyle(FontStyle fontStyle, Color color)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = fontStyle
        };
        style.normal.textColor = color;
        return style;
    }

    static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    void OpenExteriorChurchOpenings()
    {
        Transform[] sceneTransforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (Transform sceneTransform in sceneTransforms)
        {
            if (sceneTransform == null)
                continue;

            string objectName = sceneTransform.name;
            bool isExteriorDoorLeaf =
                objectName == "Front_Door_L" ||
                objectName == "Front_Door_R" ||
                objectName == "Front_Door_L_1" ||
                objectName == "Front_Door_R_1";
            bool isExteriorWindow =
                objectName == "OwenDoor_Bigwindow" ||
                objectName == "Graveyard From";
            if (!isExteriorDoorLeaf && !isExteriorWindow)
                continue;

            sceneTransform.gameObject.SetActive(false);
        }
    }

    void CreateCandleGlow()
    {
        Vector3[] positions =
        {
            new Vector3(303.46f, 4.15f, 36.1f),
            new Vector3(303.46f, 3.65f, 42f),
            new Vector3(303.46f, 3.65f, 47.5f),
            new Vector3(303.46f, 3.65f, 53.5f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject glow = new GameObject($"Wedding Candle Glow {i + 1}");
            glow.transform.SetParent(transform, false);
            glow.transform.position = positions[i];

            Light candleLight = glow.AddComponent<Light>();
            candleLight.type = LightType.Point;
            candleLight.color = candleGlowColor;
            candleLight.intensity = candleGlowIntensity;
            candleLight.range = candleGlowRange;
            candleLight.shadows = LightShadows.None;
        }
    }

    void CreateCeilingLights()
    {
        Transform ceilingFixture = null;
        Transform[] sceneTransforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (Transform sceneTransform in sceneTransforms)
        {
            if (!sceneTransform.name.Contains("Ceiling_lamp"))
                continue;

            ceilingFixture = sceneTransform;
            Renderer fixtureRenderer = sceneTransform.GetComponent<Renderer>();
            if (fixtureRenderer != null)
                fixtureRenderer.enabled = true;
            break;
        }

        Vector3[] positions =
        {
            new Vector3(298.5f, 7.2f, 39f),
            new Vector3(300f, 7.2f, 44f),
            new Vector3(302f, 7.2f, 49f),
            new Vector3(304f, 7.2f, 54f),
            new Vector3(305.5f, 7.2f, 58f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject ceilingLightObject = new GameObject($"Wedding Ceiling Light {i + 1}");
            Transform lightTransform = ceilingLightObject.transform;
            lightTransform.SetPositionAndRotation(positions[i], Quaternion.Euler(90f, 0f, 0f));
            lightTransform.SetParent(ceilingFixture != null ? ceilingFixture : transform, true);

            Light ceilingLight = ceilingLightObject.AddComponent<Light>();
            ceilingLight.type = LightType.Spot;
            ceilingLight.color = ceilingLightColor;
            ceilingLight.intensity = ceilingLightIntensity;
            ceilingLight.range = ceilingLightRange;
            ceilingLight.spotAngle = 75f;
            ceilingLight.innerSpotAngle = 48f;
            ceilingLight.shadows = LightShadows.None;
        }
    }

    void OnDisable()
    {
        if (!sequenceComplete && !isTransitioning)
            FinishSequence();
    }

    void OnDestroy()
    {
        if (panelTexture != null)
            Destroy(panelTexture);
        if (accentTexture != null)
            Destroy(accentTexture);
    }
}

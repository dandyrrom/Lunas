#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Modest Level2 difficulty / polish pass. Safe to re-run; skips work that is already done.
/// Menu: LUNAS / Level 2 / Apply Harder Pass (Modest)
/// </summary>
public static class Level2HarderPass
{
    const string Level2Path = "Assets/Scenes/Level2.unity";
    const string BellClipPath = "Assets/SFX"; // resolved by guid / name search

    static readonly Vector3 ExitNearLightedBowls = new Vector3(-37f, 6f, 48f);

    // Slightly tougher than Level1 defaults (0.5 / 0.5 / 20 / 1 / 10).
    const float ChaseSpeed = 0.65f;
    const float HitRunSpeed = 0.7f;
    const float ScreamAlertRadius = 28f;
    const float BreathingRange = 1.25f;
    const int AttackDamage = 12;

    static readonly Vector3[] ExtraZombieOffsets =
    {
        new Vector3(-42f, 5.5f, 18f),   // path toward lighted bowls
        new Vector3(-72f, 3.2f, -42f),  // south cluster pressure
    };

    [MenuItem("LUNAS/Level 2/Apply Harder Pass (Modest)")]
    public static void Apply()
    {
        Scene scene = EnsureLevel2Open();
        if (!scene.IsValid())
            return;

        int changes = 0;
        changes += ApplyNightLook();
        changes += TuneAswangs();
        // Do not auto-spawn extras — scene aswang count is authored in Level2.
        changes += FixRespawnPads();
        changes += WireFlowAndTexts();
        changes += PlaceExitAnchor();

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Level2 harder pass: applied {changes} change groups. Save the scene (Ctrl/Cmd+S).");
    }

    static Scene EnsureLevel2Open()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path == Level2Path)
            return active;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return default;

        return EditorSceneManager.OpenScene(Level2Path);
    }

    static int ApplyNightLook()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.12f, 1f);
        RenderSettings.fogStartDistance = 18f;
        RenderSettings.fogEndDistance = 140f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientSkyColor = new Color(0.02f, 0.03f, 0.08f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.015f, 0.018f, 0.03f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.008f, 0.008f, 0.012f, 1f);
        RenderSettings.ambientIntensity = 0.32f;
        RenderSettings.reflectionIntensity = 0.4f;
        RenderSettings.subtractiveShadowColor = new Color(0.05f, 0.06f, 0.1f, 1f);

        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light == null || light.type != LightType.Directional)
                continue;

            // Keep moonlight cool; clamp so we don't nuke intentional prop lights.
            Color c = light.color;
            if (c.r > 0.45f || c.g > 0.45f || c.b > 0.55f)
                light.color = new Color(0.16f, 0.22f, 0.30f, 1f);
            if (light.intensity > 0.55f)
                light.intensity = 0.4f;
            EditorUtility.SetDirty(light);
        }

        return 1;
    }

    static int TuneAswangs()
    {
        int n = 0;
        foreach (ZombieRoam roam in Object.FindObjectsByType<ZombieRoam>(FindObjectsSortMode.None))
        {
            roam.chaseSpeed = ChaseSpeed;
            roam.hitRunSpeed = HitRunSpeed;
            roam.screamAlertRadius = ScreamAlertRadius;
            roam.breathingRange = BreathingRange;
            roam.attackDamage = AttackDamage;
            EditorUtility.SetDirty(roam);
            n++;
        }

        Debug.Log($"Level2 harder pass: tuned {n} aswangs.");
        return n > 0 ? 1 : 0;
    }

    static int EnsureExtraAswangs()
    {
        ZombieRoam[] existing = Object.FindObjectsByType<ZombieRoam>(FindObjectsSortMode.None);
        if (existing.Length >= 9)
        {
            Debug.Log($"Level2 harder pass: already have {existing.Length} aswangs — skip extras.");
            return 0;
        }

        if (existing.Length == 0)
        {
            Debug.LogWarning("Level2 harder pass: no aswang template found.");
            return 0;
        }

        // Prefer distinct templates when available.
        List<ZombieRoam> templates = new List<ZombieRoam>();
        foreach (ZombieRoam z in existing)
        {
            if (z != null && !templates.Exists(t => t.name == z.name))
                templates.Add(z);
        }

        int need = Mathf.Min(ExtraZombieOffsets.Length, 9 - existing.Length);
        int created = 0;
        for (int i = 0; i < need; i++)
        {
            ZombieRoam template = templates[i % templates.Count];
            GameObject clone = Object.Instantiate(template.gameObject);
            clone.name = template.name + $" (L2Harder {i + 1})";
            clone.transform.position = ExtraZombieOffsets[i];
            clone.transform.rotation = template.transform.rotation;
            SceneManager.MoveGameObjectToScene(clone, template.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(clone, "Level2 harder aswang");

            ZombieRoam roam = clone.GetComponent<ZombieRoam>();
            if (roam != null)
            {
                roam.chaseSpeed = ChaseSpeed;
                roam.hitRunSpeed = HitRunSpeed;
                roam.screamAlertRadius = ScreamAlertRadius;
                roam.breathingRange = BreathingRange;
                roam.attackDamage = AttackDamage;
            }

            created++;
        }

        Debug.Log($"Level2 harder pass: spawned {created} extra aswangs (target ~9).");
        return created > 0 ? 1 : 0;
    }

    static int FixRespawnPads()
    {
        // Keep pads parented under lighted bowls; disable Level1 leftover pads under RespawnPads.
        GameObject padsRoot = GameObject.Find("RespawnPads");
        int disabled = 0;
        if (padsRoot != null)
        {
            foreach (Transform child in padsRoot.transform)
            {
                if (child == null)
                    continue;
                if (!child.gameObject.activeSelf)
                    continue;
                child.gameObject.SetActive(false);
                EditorUtility.SetDirty(child.gameObject);
                disabled++;
            }
        }

        // Ensure default checkpoint is PlayerSpawn.
        RespawnManager manager = Object.FindFirstObjectByType<RespawnManager>();
        GameObject spawn = GameObject.Find("PlayerSpawn");
        if (manager != null && spawn != null)
        {
            manager.defaultCheckpoint = spawn.transform;
            EditorUtility.SetDirty(manager);
        }

        // Lighted bowls should already own RespawnCheckpoint pads; log how many are active.
        int activePads = 0;
        foreach (RespawnCheckpoint pad in Object.FindObjectsByType<RespawnCheckpoint>(FindObjectsSortMode.None))
        {
            if (pad != null && pad.gameObject.activeInHierarchy)
                activePads++;
        }

        Debug.Log($"Level2 harder pass: disabled {disabled} leftover pads under RespawnPads; {activePads} active bowl pads remain.");
        return 1;
    }

    static int WireFlowAndTexts()
    {
        Level2GameFlow flow = Object.FindFirstObjectByType<Level2GameFlow>();
        if (flow == null)
        {
            Debug.LogWarning("Level2 harder pass: Level2GameFlow missing.");
            return 0;
        }

        GameObject objective = GameObject.Find("ObjectiveText");
        GameObject goal = GameObject.Find("GoalText");
        if (objective != null)
            flow.objectiveText = objective.GetComponent<TMP_Text>();
        if (goal != null)
        {
            flow.goalText = goal.GetComponent<TMP_Text>();
            TMP_Text goalTmp = goal.GetComponent<TMP_Text>();
            if (goalTmp != null)
                goalTmp.text = "Reach the village gate";
        }

        GameObject hud = GameObject.Find("HUD");
        if (hud != null)
            flow.hudCanvas = hud.GetComponent<Canvas>();

        GameObject exit = GameObject.Find("Level2ExitAnchor");
        if (exit != null)
            flow.exitAnchor = exit.transform;

        if (flow.churchBellClip == null)
        {
            AudioClip clip = FindChurchBellClip();
            if (clip != null)
                flow.churchBellClip = clip;
            else
                Debug.LogWarning("Level2 harder pass: church bell clip not found — assign manually on Level2GameFlow.");
        }

        flow.perfectScore = 275;
        flow.continueSceneName = "SplashScreen";
        EditorUtility.SetDirty(flow);
        if (goal != null)
            EditorUtility.SetDirty(goal);
        return 1;
    }

    static AudioClip FindChurchBellClip()
    {
        // Prefer the same clip Level1 uses (guid from Level1 scene).
        string guid = "d66c4cb893be06f46980d641154f032d";
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (!string.IsNullOrEmpty(path))
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
                return clip;
        }

        string[] hits = AssetDatabase.FindAssets("bell t:AudioClip");
        foreach (string g in hits)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g));
            if (clip != null && clip.name.ToLowerInvariant().Contains("bell"))
                return clip;
        }

        return null;
    }

    static int PlaceExitAnchor()
    {
        GameObject exit = GameObject.Find("Level2ExitAnchor");
        if (exit == null)
        {
            exit = new GameObject("Level2ExitAnchor");
            Undo.RegisterCreatedObjectUndo(exit, "Level2 exit anchor");
        }

        // Only move if still on the old placeholder.
        Vector3 p = exit.transform.position;
        bool placeholder = Vector3.Distance(p, new Vector3(0f, 1.5f, 78f)) < 0.05f
                           || Vector3.Distance(p, Vector3.zero) < 0.05f;
        if (placeholder)
        {
            exit.transform.position = ExitNearLightedBowls;
            EditorUtility.SetDirty(exit);
            Debug.Log($"Level2 harder pass: moved Level2ExitAnchor to {ExitNearLightedBowls} (past lighted bowls).");
            return 1;
        }

        Debug.Log($"Level2 harder pass: exit anchor left at {p} (already placed).");
        return 0;
    }
}
#endif

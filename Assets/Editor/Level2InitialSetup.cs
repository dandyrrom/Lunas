#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-click helpers to finish Level 2 after the cemetery blockout lands in the repo.
/// Use while Level2.unity is open (or let the menu open it).
/// </summary>
public static class Level2InitialSetup
{
    const string Level2Path = "Assets/Scenes/Level2.unity";
    const string Level1Path = "Assets/Scenes/Level1.unity";

    static readonly string[] CopyRootNames =
    {
        "HUD",
        "zombies",
        "RespawnManager",
        "RespawnPads",
        "ZombieKillScore",
        "ZombieKillHUD",
        "PlayModeBackgroundMusic",
        "BawangSpawn",
        "Ground Fog",
        "Level1PauseMenu"
    };

    [MenuItem("LUNAS/Level 2/Open Level2 Scene")]
    public static void OpenLevel2()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(Level2Path);
    }

    [MenuItem("LUNAS/Level 2/Ensure Flow + HUD + Anchors")]
    public static void EnsureFlowHudAnchors()
    {
        Scene scene = EnsureLevel2Open();
        if (!scene.IsValid())
            return;

        EnsureAnchors();
        EnsureHud();
        EnsureGameFlow();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Level2: flow, HUD, and anchors ready.");
    }

    [MenuItem("LUNAS/Level 2/Create NavMesh Surface (Safe)")]
    public static void CreateNavMeshSurfaceSafe()
    {
        Scene scene = EnsureLevel2Open();
        if (!scene.IsValid())
            return;

        NavMeshSurface existing = Object.FindFirstObjectByType<NavMeshSurface>();
        GameObject go;
        NavMeshSurface surface;

        if (existing != null)
        {
            go = existing.gameObject;
            surface = existing;
        }
        else
        {
            go = new GameObject("NavMesh");
            SceneManager.MoveGameObjectToScene(go, scene);
            surface = go.AddComponent<NavMeshSurface>();
            Undo.RegisterCreatedObjectUndo(go, "Create Level2 NavMesh");
        }

        surface.collectObjects = CollectObjects.All;
        surface.BuildNavMesh();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = go;
        Debug.Log("Level2: NavMesh Surface ready and baked. Save the scene.");
    }

    [MenuItem("LUNAS/Level 2/Import Gameplay Roots From Level1")]
    public static void ImportGameplayFromLevel1()
    {
        Scene level2 = EnsureLevel2Open();
        if (!level2.IsValid())
            return;

        Scene level1 = EditorSceneManager.OpenScene(Level1Path, OpenSceneMode.Additive);
        try
        {
            Dictionary<string, GameObject> level1Roots = new Dictionary<string, GameObject>();
            foreach (GameObject root in level1.GetRootGameObjects())
            {
                if (root != null && !level1Roots.ContainsKey(root.name))
                    level1Roots[root.name] = root;
            }

            // Player is whichever root has ThirdPersonController.
            GameObject playerSource = null;
            foreach (GameObject root in level1.GetRootGameObjects())
            {
                if (root != null && root.GetComponentInChildren<ThirdPersonController>(true) != null)
                {
                    playerSource = root;
                    break;
                }
            }

            int copied = 0;
            if (playerSource != null)
            {
                copied += CopyOrReplaceRoot(playerSource, level2, "Player") ? 1 : 0;
                PlaceAtSpawn(GameObject.Find("Player") ?? FindPlayerInScene(level2));
            }
            else
            {
                Debug.LogWarning("Level2 import: no ThirdPersonController found in Level1.");
            }

            foreach (string name in CopyRootNames)
            {
                if (!level1Roots.TryGetValue(name, out GameObject source) || source == null)
                    continue;
                if (CopyOrReplaceRoot(source, level2, name))
                    copied++;
            }

            // Candle / garlic bowls are often nested; also copy any root with those names.
            foreach (GameObject root in level1.GetRootGameObjects())
            {
                if (root == null)
                    continue;
                string n = root.name;
                if (n.Contains("bowl") || n.Contains("Candle") || n.Contains("garlic") || n.Contains("Bawang"))
                {
                    if (CopyOrReplaceRoot(root, level2, n))
                        copied++;
                }
            }

            EnsureFlowHudAnchors();
            RetargetPauseMenuName();
            Debug.Log($"Level2 import: copied/replaced {copied} roots from Level1. Bake NavMesh next, then place aswangs along the path.");
        }
        finally
        {
            EditorSceneManager.CloseScene(level1, true);
            EditorSceneManager.MarkSceneDirty(level2);
        }
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

    static bool CopyOrReplaceRoot(GameObject source, Scene targetScene, string desiredName)
    {
        GameObject existing = null;
        foreach (GameObject root in targetScene.GetRootGameObjects())
        {
            if (root != null && root.name == desiredName)
            {
                existing = root;
                break;
            }
        }

        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject clone = Object.Instantiate(source);
        clone.name = desiredName;
        SceneManager.MoveGameObjectToScene(clone, targetScene);
        Undo.RegisterCreatedObjectUndo(clone, "Import Level2 root");
        return true;
    }

    static GameObject FindPlayerInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root != null && root.GetComponentInChildren<ThirdPersonController>(true) != null)
                return root;
        }
        return null;
    }

    static void PlaceAtSpawn(GameObject player)
    {
        if (player == null)
            return;

        GameObject spawn = GameObject.Find("PlayerSpawn");
        if (spawn == null)
            return;

        player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
    }

    static void EnsureAnchors()
    {
        if (GameObject.Find("PlayerSpawn") == null)
        {
            GameObject spawn = new GameObject("PlayerSpawn");
            spawn.transform.position = new Vector3(0f, 0.1f, 2f);
            spawn.transform.rotation = Quaternion.identity;
        }

        if (GameObject.Find("Level2ExitAnchor") == null)
        {
            GameObject exit = new GameObject("Level2ExitAnchor");
            exit.transform.position = new Vector3(-37f, 6f, 48f);
        }
    }

    static void EnsureHud()
    {
        Canvas canvas = null;
        GameObject hud = GameObject.Find("HUD");
        if (hud != null)
            canvas = hud.GetComponent<Canvas>();

        if (canvas == null)
        {
            hud = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        EnsureHudText(hud.transform, "ObjectiveText", "Kill all aswang!!!!", new Vector2(0f, 220f), new Color(1f, 0.25f, 0.28f));
        EnsureHudText(hud.transform, "GoalText", "Reach the village gate", new Vector2(0f, 220f), new Color(1f, 0.85f, 0.35f));
        GameObject goal = GameObject.Find("GoalText");
        if (goal != null)
            goal.SetActive(false);
    }

    static void EnsureHudText(Transform parent, string name, string text, Vector2 anchoredPos, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        if (existing == null)
            go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(900f, 80f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 42;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        if (tmp.font == null)
            tmp.font = TMP_Settings.defaultFontAsset;
    }

    static void EnsureGameFlow()
    {
        Level2GameFlow flow = Object.FindFirstObjectByType<Level2GameFlow>();
        if (flow == null)
        {
            GameObject go = new GameObject("Level2GameFlow");
            flow = go.AddComponent<Level2GameFlow>();
        }

        flow.objectiveText = GameObject.Find("ObjectiveText")?.GetComponent<TMP_Text>();
        flow.goalText = GameObject.Find("GoalText")?.GetComponent<TMP_Text>();
        GameObject hud = GameObject.Find("HUD");
        if (hud != null)
            flow.hudCanvas = hud.GetComponent<Canvas>();

        GameObject exit = GameObject.Find("Level2ExitAnchor");
        if (exit != null)
            flow.exitAnchor = exit.transform;

        flow.continueSceneName = LevelChain.Level3;
        EditorUtility.SetDirty(flow);
    }

    static void RetargetPauseMenuName()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root != null && root.name == "Level1PauseMenu")
                root.name = "Level2PauseMenu";
        }
    }
}
#endif

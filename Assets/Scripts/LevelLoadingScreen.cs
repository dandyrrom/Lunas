using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Full-screen dark LOADING... hold, matching the splash / backstory handoff, then loads the scene.
/// </summary>
public class LevelLoadingScreen : MonoBehaviour
{
    const float HoldSeconds = 0.85f;

    static LevelLoadingScreen instance;
    bool loading;

    Canvas canvas;
    Text label;

    public static void ShowAndLoad(string sceneName)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("LevelLoadingScreen");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LevelLoadingScreen>();
        }

        instance.Begin(sceneName);
    }

    void Begin(string sceneName)
    {
        if (loading)
            return;

        loading = true;
        EnsureUi();
        canvas.gameObject.SetActive(true);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        StartCoroutine(LoadAfterHold(sceneName));
    }

    IEnumerator LoadAfterHold(string sceneName)
    {
        yield return new WaitForSecondsRealtime(HoldSeconds);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (load == null)
        {
            Debug.LogError($"Cannot load '{sceneName}'.");
            loading = false;
            if (canvas != null)
                canvas.gameObject.SetActive(false);
            yield break;
        }

        while (!load.isDone)
            yield return null;

        loading = false;
        if (canvas != null)
            canvas.gameObject.SetActive(false);
    }

    void EnsureUi()
    {
        if (canvas != null)
            return;

        GameObject canvasGo = new GameObject("LoadingCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(canvasGo.transform, false);
        RectTransform dimRt = dim.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        Image image = dim.GetComponent<Image>();
        image.color = new Color(0.008f, 0.006f, 0.012f, 1f);
        image.raycastTarget = true;

        GameObject textGo = new GameObject("LoadingLabel", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(canvasGo.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0.46f);
        textRt.anchorMax = new Vector2(1f, 0.46f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.sizeDelta = new Vector2(0f, 70f);
        label = textGo.GetComponent<Text>();
        label.text = "LOADING...";
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.92f, 0.78f, 0.7f);
        label.fontSize = 40;
        label.raycastTarget = false;
        Font font = ResolveFont();
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
            label.font = font;
    }

    static Font ResolveFont()
    {
        Font font = Resources.Load<Font>("Blood Victim Zombie");
#if UNITY_EDITOR
        if (font == null)
        {
            string[] fontGuids = UnityEditor.AssetDatabase.FindAssets("Blood Victim Zombie t:Font");
            if (fontGuids.Length > 0)
            {
                string fontPath = UnityEditor.AssetDatabase.GUIDToAssetPath(fontGuids[0]);
                font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            }
        }
#endif
        return font;
    }
}

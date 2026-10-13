using UnityEngine;
using UnityEngine.UI;

public class BossHealthBar : MonoBehaviour
{
    ZombieHealth health;
    Image fill;
    Transform bar;
    Sprite white;

    void Awake()
    {
        health = GetComponent<ZombieHealth>();
        Build();
    }

    void OnEnable()
    {
        if (health != null)
            health.OnHealthChanged += HandleHealthChanged;
        Refresh();
    }

    void OnDisable()
    {
        if (health != null)
            health.OnHealthChanged -= HandleHealthChanged;
    }

    void LateUpdate()
    {
        if (bar == null || health == null)
            return;

        ZombieBoss boss = GetComponent<ZombieBoss>();
        float height = boss != null ? boss.barHeight : 4.92f;
        bar.localPosition = new Vector3(0f, height, 0f);
        bar.localRotation = Quaternion.identity;

        ApplyFill();

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 toCamera = bar.position - cam.transform.position;
        if (toCamera.sqrMagnitude > 0.001f)
            bar.rotation = Quaternion.LookRotation(toCamera);
    }

    void HandleHealthChanged(int current, int max)
    {
        ApplyFill();
    }

    void Refresh()
    {
        ApplyFill();
    }

    void ApplyFill()
    {
        if (health == null || fill == null || health.MaxHealth <= 0)
            return;

        float ratio = Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);
        fill.fillOrigin = (int)Image.OriginHorizontal.Right;
        fill.fillAmount = ratio;
        RectTransform rect = fill.rectTransform;
        rect.anchorMin = new Vector2(1f - ratio, 0f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(ratio <= 0.001f ? -2f : 2f, 2f);
        rect.offsetMax = new Vector2(-2f, -2f);
    }

    void Build()
    {
        white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));

        GameObject root = new GameObject("Boss Health Bar");
        root.transform.SetParent(transform, false);
        bar = root.transform;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(160f, 18f);
        canvasRect.localScale = Vector3.one * 0.01f;

        Image background = CreateImage("Background", canvasRect, new Color(0.08f, 0.02f, 0.02f, 0.9f));
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;

        fill = CreateImage("Fill", background.rectTransform, new Color(0.75f, 0.08f, 0.08f, 1f));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(2f, 2f);
        fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Right;
        fill.fillAmount = 1f;
    }

    Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = white;
        image.color = color;
        return image;
    }
}

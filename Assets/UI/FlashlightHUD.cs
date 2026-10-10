using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Indicateur de batterie de la lampe torche (en bas à gauche).
/// Visible quand la lampe est allumée, que la batterie est faible ou vient de changer ;
/// s'efface tout seul sinon. Clignote en rouge quand la batterie est faible.
/// Créé automatiquement par PlayerFlashlight.
/// </summary>
public class FlashlightHUD : MonoBehaviour
{
    private static readonly Color PanelCol = new Color32(14, 17, 22, 200);
    private static readonly Color Accent   = new Color32(60, 242, 163, 255);
    private static readonly Color Warning  = new Color32(255, 196, 77, 255);
    private static readonly Color Danger   = new Color32(255, 107, 107, 255);
    private static readonly Color Empty    = new Color32(255, 255, 255, 28);
    private static readonly Color Outline  = new Color32(200, 210, 220, 200);

    private const int Segments = 5;
    private const float HideDelay = 2.5f;

    private PlayerFlashlight flashlight;
    private CanvasGroup group;
    private Image[] segments;
    private Image body, nub;
    private TextMeshProUGUI percent;
    private float visibleUntil;

    public static void Ensure(PlayerFlashlight flashlight)
    {
        var hud = FindAnyObjectByType<FlashlightHUD>();
        if (hud == null) hud = new GameObject("FlashlightHUD").AddComponent<FlashlightHUD>();
        hud.Bind(flashlight);
    }

    private void Bind(PlayerFlashlight f)
    {
        if (flashlight != null) flashlight.Changed -= Ping;
        flashlight = f;
        flashlight.Changed += Ping;
        Ping();
    }

    private void OnDestroy()
    {
        if (flashlight != null) flashlight.Changed -= Ping;
    }

    private void Ping() => visibleUntil = Time.unscaledTime + HideDelay;

    private void Awake()
    {
        Sprite rounded = SettingsMenu.MakeRoundedSprite(64, 10);
        Sprite small = SettingsMenu.MakeRoundedSprite(16, 3);

        var canvasGO = new GameObject("Canvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // au-dessus de la brume, sous le menu
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;

        // Pastille de fond
        var panel = NewRect("Battery", canvasGO.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
        panel.anchoredPosition = new Vector2(32, 32);
        panel.sizeDelta = new Vector2(176, 56);
        Img(panel, PanelCol, rounded);

        // Corps de la pile
        var bodyRT = NewRect("Body", panel);
        bodyRT.anchorMin = bodyRT.anchorMax = bodyRT.pivot = new Vector2(0, 0.5f);
        bodyRT.anchoredPosition = new Vector2(18, 0);
        bodyRT.sizeDelta = new Vector2(66, 30);
        body = Img(bodyRT, Outline, small);

        var inner = NewRect("Inner", bodyRT);
        inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(2, 2); inner.offsetMax = new Vector2(-2, -2);
        Img(inner, PanelCol, small);

        var nubRT = NewRect("Nub", panel);
        nubRT.anchorMin = nubRT.anchorMax = nubRT.pivot = new Vector2(0, 0.5f);
        nubRT.anchoredPosition = new Vector2(18 + 66 + 1, 0);
        nubRT.sizeDelta = new Vector2(5, 12);
        nub = Img(nubRT, Outline, small);

        segments = new Image[Segments];
        float segW = (66f - 4f - 4f - (Segments - 1) * 2f) / Segments;
        for (int i = 0; i < Segments; i++)
        {
            var s = NewRect("Seg" + i, bodyRT);
            s.anchorMin = s.anchorMax = s.pivot = new Vector2(0, 0.5f);
            s.anchoredPosition = new Vector2(4 + i * (segW + 2f), 0);
            s.sizeDelta = new Vector2(segW, 30 - 8);
            segments[i] = Img(s, Accent, small);
        }

        // Pourcentage
        var textRT = NewRect("Percent", panel);
        textRT.anchorMin = new Vector2(0, 0); textRT.anchorMax = new Vector2(1, 1);
        textRT.offsetMin = new Vector2(98, 0); textRT.offsetMax = new Vector2(-14, 0);
        percent = textRT.gameObject.AddComponent<TextMeshProUGUI>();
        percent.fontSize = 22;
        percent.fontStyle = FontStyles.Bold;
        percent.alignment = TextAlignmentOptions.MidlineRight;
        percent.textWrappingMode = TextWrappingModes.NoWrap;
        percent.raycastTarget = false;
    }

    private void Update()
    {
        if (flashlight == null) { group.alpha = 0f; return; }

        float c = flashlight.Charge;
        bool low = flashlight.IsLow;
        bool show = flashlight.IsOn || low || Time.unscaledTime < visibleUntil || SettingsMenu.IsOpen;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 3f);

        // Couleur selon le niveau ; clignote quand la batterie est faible
        Color col = c > 0.5f ? Accent : (low ? Danger : Warning);
        if (low && flashlight.IsOn)
            col.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (c <= 0.05f ? 9f : 4f)));

        float filled = c * Segments;
        for (int i = 0; i < Segments; i++)
        {
            float f = Mathf.Clamp01(filled - i); // remplissage partiel du dernier segment
            segments[i].color = f > 0f ? Color.Lerp(Empty, col, 0.35f + 0.65f * f) : Empty;
            segments[i].rectTransform.localScale = new Vector3(1f, f > 0f ? Mathf.Lerp(0.55f, 1f, f) : 1f, 1f);
        }

        Color outline = flashlight.IsEmpty ? Danger : Outline;
        body.color = outline;
        nub.color = outline;
        percent.text = flashlight.IsEmpty ? "VIDE" : Mathf.CeilToInt(c * 100f) + " %";
        percent.color = low ? Danger : new Color32(230, 237, 243, 255);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image Img(RectTransform rt, Color color, Sprite sprite)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}

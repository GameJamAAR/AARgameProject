using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ambiance horreur :
///  - brouillard de distance (le décor disparaît au loin) ;
///  - brume plus épaisse sur les bords de l'écran, qui « respire » lentement ;
///  - scène assombrie pour que la lampe torche compte.
/// Créé automatiquement par PlayerController s'il n'y en a pas dans la scène.
/// Pour régler les valeurs dans l'inspecteur, ajoute ce composant à un objet de la scène.
/// </summary>
public class AtmosphereFog : MonoBehaviour
{
    [Header("Brouillard de distance")]
    [SerializeField] private Color fogColor = new Color(0.035f, 0.045f, 0.055f);
    [Tooltip("Densité (mode exponentiel²). 0,08 ≈ on ne voit plus rien au-delà de 20 m.")]
    [SerializeField, Range(0f, 0.3f)] private float fogDensity = 0.20f;
    [Tooltip("Remplit le fond de la caméra avec la couleur du brouillard (pas de ciel visible).")]
    [SerializeField] private bool cameraBackgroundIsFog = true;

    [Header("Brume sur les bords de l'écran")]
    [SerializeField, Range(0f, 1f)] private float edgeOpacity = 0.85f;
    [Tooltip("Rayon (0-1,4) où la brume commence. Plus petit = brume plus envahissante.")]
    [SerializeField, Range(0.2f, 1.2f)] private float edgeStart = 0.55f;
    [Tooltip("Rayon où la brume est la plus épaisse (coins de l'écran ≈ 1,41).")]
    [SerializeField, Range(0.6f, 1.6f)] private float edgeEnd = 1.3f;
    [Tooltip("Amplitude et vitesse de la « respiration » de la brume.")]
    [SerializeField, Range(0f, 0.3f)] private float breatheAmount = 0.08f;
    [SerializeField] private float breatheSpeed = 0.35f;

    [Header("Obscurité")]
    [Tooltip("0 = éclairage d'origine, 1 = noir complet (hors lampe torche).")]
    [SerializeField, Range(0f, 1f)] private float sceneDarkness = 0.75f;
    [SerializeField] private Color ambientColor = new Color(0.05f, 0.06f, 0.075f);

    private static AtmosphereFog instance;
    private RawImage edgeImage;
    private Texture2D edgeTexture;
    private float builtStart = -1f, builtEnd = -1f;

    /// <summary>Crée l'ambiance si la scène n'en a pas encore.</summary>
    public static void Ensure()
    {
        if (instance != null) return;
        instance = FindAnyObjectByType<AtmosphereFog>();
        if (instance == null) new GameObject("AtmosphereFog").AddComponent<AtmosphereFog>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        ApplyFog();
        ApplyDarkness();
        BuildEdgeOverlay();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (edgeTexture != null) Destroy(edgeTexture);
    }

    private void Update()
    {
        // Les valeurs restent réglables en direct dans l'inspecteur
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        if (cameraBackgroundIsFog && Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = fogColor;
        }

        if (!Mathf.Approximately(builtStart, edgeStart) || !Mathf.Approximately(builtEnd, edgeEnd))
            RebuildEdgeTexture();

        float breathe = 1f + Mathf.Sin(Time.unscaledTime * breatheSpeed * Mathf.PI * 2f) * breatheAmount
                           + (Mathf.PerlinNoise(Time.unscaledTime * 0.23f, 0.5f) - 0.5f) * breatheAmount;
        Color c = fogColor;
        c.a = Mathf.Clamp01(edgeOpacity * breathe);
        edgeImage.color = c;
        // Légère dérive pour que la brume ne paraisse pas figée
        float drift = 0.012f;
        edgeImage.uvRect = new Rect(
            Mathf.Sin(Time.unscaledTime * 0.17f) * drift, Mathf.Cos(Time.unscaledTime * 0.13f) * drift, 1f, 1f);
    }

    private void ApplyFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    private void ApplyDarkness()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, ambientColor, sceneDarkness);
        RenderSettings.reflectionIntensity *= 1f - sceneDarkness * 0.8f;

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Light>())
                    if (light.type == LightType.Directional)
                        light.intensity *= 1f - sceneDarkness;
        }
    }

    // ---------- Brume d'écran ----------
    private void BuildEdgeOverlay()
    {
        var canvasGO = new GameObject("EdgeFogCanvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // sous le menu Paramètres (500)

        var imgGO = new GameObject("EdgeFog", typeof(RectTransform));
        imgGO.transform.SetParent(canvasGO.transform, false);
        var rt = (RectTransform)imgGO.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        // Un peu plus grand que l'écran pour que la dérive ne laisse pas voir de bord net
        rt.offsetMin = new Vector2(-40, -40); rt.offsetMax = new Vector2(40, 40);

        edgeImage = imgGO.AddComponent<RawImage>();
        edgeImage.raycastTarget = false;
        RebuildEdgeTexture();
    }

    private void RebuildEdgeTexture()
    {
        builtStart = edgeStart;
        builtEnd = Mathf.Max(edgeEnd, edgeStart + 0.05f);
        const int size = 256;
        if (edgeTexture == null)
        {
            edgeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "EdgeFog"
            };
        }

        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size * 2f - 1f;
            float v = (y + 0.5f) / size * 2f - 1f;
            float d = Mathf.Sqrt(u * u + v * v);
            // Volutes irrégulières pour éviter un vignettage trop parfait
            float n = Mathf.PerlinNoise(x * 0.025f, y * 0.025f) * 0.35f + Mathf.PerlinNoise(x * 0.07f + 13f, y * 0.07f) * 0.15f;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(builtStart, builtEnd, d + (n - 0.25f) * 0.35f));
            a = Mathf.Pow(a, 1.4f);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        edgeTexture.SetPixels32(px);
        edgeTexture.Apply(false);
        edgeImage.texture = edgeTexture;
    }
}

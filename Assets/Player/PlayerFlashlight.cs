using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

/// <summary>
/// Lampe torche du joueur, allumée/éteinte avec l'action "Flashlight" (F / croix haut).
/// La lumière suit le regard (pivot caméra) en 1re comme en 3e personne.
///
/// Batterie : se vide quand la lampe est allumée. Sous le seuil "faible", la lumière
/// baisse et grésille ; à 0 elle s'éteint et ne se rallume plus. On la recharge en
/// ramassant des piles (BatteryPickup) et, si activé, doucement quand la lampe est éteinte.
/// Ajoutée automatiquement par PlayerController si absente.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerFlashlight : MonoBehaviour
{
    [Header("Lumière")]
    [SerializeField] private bool startOn = false;
    [SerializeField] private Color color = new Color(1f, 0.93f, 0.8f);
    [SerializeField] private float intensity = 4f;
    [SerializeField] private float range = 22f;
    [SerializeField, Range(10f, 120f)] private float spotAngle = 55f;
    [SerializeField, Range(5f, 110f)] private float innerSpotAngle = 28f;
    [Tooltip("Décalage par rapport aux yeux (droite, haut, avant), comme une lampe tenue en main. Assez en avant pour sortir du casque (sinon son ombre bouche la lumière).")]
    [SerializeField] private Vector3 offset = new Vector3(0.2f, -0.25f, 0.55f);
    [Tooltip("Vitesse à laquelle la lampe rattrape le regard (un léger retard la rend plus vivante).")]
    [SerializeField] private float followSharpness = 18f;

    [Header("Batterie")]
    [Tooltip("Durée d'une batterie pleine, lampe allumée en continu (secondes).")]
    [SerializeField] private float batteryLifeSeconds = 120f;
    [Tooltip("Charge au début de la partie (0 à 1).")]
    [SerializeField, Range(0f, 1f)] private float startCharge = 1f;
    [Tooltip("Recharge par seconde quand la lampe est éteinte (0 = uniquement avec des piles).")]
    [SerializeField, Range(0f, 0.05f)] private float rechargePerSecondWhenOff = 0.008f;
    [Tooltip("En dessous de ce niveau, la lumière faiblit et grésille.")]
    [SerializeField, Range(0f, 0.5f)] private float lowThreshold = 0.2f;
    [Tooltip("Intensité minimale atteinte juste avant que la batterie soit vide (en % de l'intensité normale).")]
    [SerializeField, Range(0f, 1f)] private float lowIntensityFactor = 0.35f;

    [Header("Ambiance")]
    [Tooltip("Petits vacillements aléatoires de la lumière.")]
    [SerializeField] private bool flicker = true;
    [SerializeField, Range(0f, 1f)] private float flickerChancePerSecond = 0.08f;

    private PlayerController player;
    private Light spot;
    private bool isOn;
    private float charge;
    private float flickerTimer;
    private float deadBlinkTimer;

    public bool IsOn => isOn;
    /// <summary>Charge de 0 (vide) à 1 (pleine).</summary>
    public float Charge => charge;
    public bool IsLow => charge <= lowThreshold;
    public bool IsEmpty => charge <= 0f;

    /// <summary>Appelé à chaque changement notable (allumage, pile ramassée, batterie vide…).</summary>
    public event Action Changed;

    private void Awake()
    {
        player = GetComponent<PlayerController>();

        var go = new GameObject("Flashlight");
        go.transform.SetParent(transform, false);
        spot = go.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = color;
        spot.intensity = intensity;
        spot.range = range;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = Mathf.Min(innerSpotAngle, spotAngle - 1f);
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = 0.9f;
        spot.renderMode = LightRenderMode.ForcePixel;

        charge = startCharge;
        isOn = startOn && charge > 0f;
        spot.enabled = isOn;
        SnapToView();

        FlashlightHUD.Ensure(this);
    }

    private void Update()
    {
        var action = player.FlashlightAction;
        if (player.InputEnabled && action != null && action.WasPressedThisFrame())
            TryToggle();

        UpdateBattery();
        UpdateLight();
    }

    private void LateUpdate()
    {
        // Après le PlayerController : suit le pivot caméra avec un léger retard
        Transform pivot = player.CameraPivot;
        if (pivot == null) return;
        Vector3 targetPos = pivot.TransformPoint(offset);
        Quaternion targetRot = pivot.rotation;
        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        spot.transform.SetPositionAndRotation(
            Vector3.Lerp(spot.transform.position, targetPos, t),
            Quaternion.Slerp(spot.transform.rotation, targetRot, t));
    }

    // ---------- API ----------
    public void TryToggle()
    {
        if (!isOn && IsEmpty)
        {
            // Batterie vide : petit sursaut de lumière qui meurt aussitôt
            deadBlinkTimer = 0.12f;
            spot.enabled = true;
            Changed?.Invoke();
            return;
        }
        SetOn(!isOn);
    }

    public void SetOn(bool on)
    {
        isOn = on && !IsEmpty;
        spot.enabled = isOn;
        if (isOn) SnapToView();
        Changed?.Invoke();
    }

    /// <summary>Ajoute de la charge (0 à 1). Renvoie faux si la batterie était déjà pleine.</summary>
    public bool AddCharge(float amount)
    {
        if (charge >= 1f) return false;
        charge = Mathf.Clamp01(charge + amount);
        Changed?.Invoke();
        return true;
    }

    // ---------- Batterie ----------
    private void UpdateBattery()
    {
        if (isOn)
        {
            charge -= Time.deltaTime / Mathf.Max(1f, batteryLifeSeconds);
            if (charge <= 0f)
            {
                charge = 0f;
                isOn = false;
                spot.enabled = false;
                Changed?.Invoke();
            }
        }
        else if (rechargePerSecondWhenOff > 0f && charge < 1f)
        {
            charge = Mathf.Min(1f, charge + rechargePerSecondWhenOff * Time.deltaTime);
        }
    }

    private void UpdateLight()
    {
        if (deadBlinkTimer > 0f)
        {
            deadBlinkTimer -= Time.deltaTime;
            spot.intensity = intensity * 0.15f * Random.value;
            if (deadBlinkTimer <= 0f && !isOn) spot.enabled = false;
            return;
        }
        if (!isOn) return;

        // Batterie faible : la lumière baisse et grésille de plus en plus
        float level = 1f;
        float chance = flicker ? flickerChancePerSecond : 0f;
        if (IsLow && lowThreshold > 0f)
        {
            float k = charge / lowThreshold; // 1 → 0
            level = Mathf.Lerp(lowIntensityFactor, 1f, k);
            chance = Mathf.Lerp(1.5f, flickerChancePerSecond, k);
        }

        if (flickerTimer > 0f)
        {
            flickerTimer -= Time.deltaTime;
            level *= Random.value < 0.5f ? Random.Range(0.05f, 0.4f) : Random.Range(0.8f, 1f);
        }
        else if (Random.value < chance * Time.deltaTime)
        {
            flickerTimer = Random.Range(0.08f, 0.35f);
        }
        spot.intensity = intensity * level;
    }

    private void SnapToView()
    {
        Transform pivot = player != null ? player.CameraPivot : null;
        if (pivot != null) spot.transform.SetPositionAndRotation(pivot.TransformPoint(offset), pivot.rotation);
    }
}

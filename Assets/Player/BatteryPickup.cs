using UnityEngine;

/// <summary>
/// Pile à ramasser : recharge la lampe torche quand le joueur passe dessus.
/// Flotte et tourne doucement, avec une petite lueur pour être repérable dans le noir.
/// Menu "AAR > Ajouter une pile" pour en poser une dans la scène.
/// </summary>
public class BatteryPickup : MonoBehaviour
{
    [Tooltip("Charge rendue (1 = batterie pleine).")]
    [SerializeField, Range(0.05f, 1f)] private float amount = 0.5f;
    [Tooltip("Ramassée même si la batterie est déjà pleine.")]
    [SerializeField] private bool pickUpWhenFull = false;

    [Header("Animation")]
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float spinSpeed = 60f;
    [SerializeField] private bool glow = true;
    [SerializeField] private Color glowColor = new Color32(60, 242, 163, 255);

    private Vector3 basePos;
    private Light glowLight;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.45f;
            col = sphere;
        }
        col.isTrigger = true;
        basePos = transform.position;

        if (glow)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(transform, false);
            glowLight = go.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = glowColor;
            glowLight.range = 1.6f;
            glowLight.intensity = 1.2f;
            glowLight.shadows = LightShadows.None;
        }
    }

    private void Update()
    {
        float t = Time.time;
        transform.position = basePos + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobHeight);
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        if (glowLight != null) glowLight.intensity = 1.0f + 0.4f * Mathf.Sin(t * 3.1f);
    }

    private void OnTriggerEnter(Collider other)
    {
        var flashlight = other.GetComponentInParent<PlayerFlashlight>();
        if (flashlight == null) return;
        if (!flashlight.AddCharge(amount) && !pickUpWhenFull) return;
        Destroy(gameObject);
    }
}

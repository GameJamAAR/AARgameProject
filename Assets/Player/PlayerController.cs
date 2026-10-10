using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Contrôleur de base du joueur : déplacement, course, saut, accroupi, regard.
/// À placer sur l'objet racine "Player" ; le modèle (Assets/Models/Character.fbx) est un enfant.
///
/// Les commandes viennent de Assets/InputSystem_Actions.inputactions (map "Player") :
///   Move  : WASD (= ZQSD sur clavier AZERTY) / flèches / stick gauche
///   Look  : souris / stick droit
///   Jump  : Espace / A (Croix)
///   Crouch: C / B (Rond)
///   Sprint: Shift gauche / L3
///   ToggleView : V / R3        (1re ⇄ 3e personne)
///   Pause      : Échap / Start (menu Paramètres, géré par SettingsMenu)
///   Flashlight : F / croix haut (lampe torche, gérée par PlayerFlashlight)
///
/// Menu "AAR > Créer le joueur dans la scène" pour tout monter automatiquement.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public enum ViewMode { FirstPerson, ThirdPerson }

    [Header("Références")]
    [Tooltip("Fichier d'actions du projet (InputSystem_Actions). Map utilisée : \"Player\".")]
    [SerializeField] private InputActionAsset inputActions;
    [Tooltip("Pivot de la caméra (enfant du joueur, à hauteur des yeux). Créé automatiquement si vide.")]
    [SerializeField] private Transform cameraPivot;
    [Tooltip("Caméra du joueur. Prend Camera.main si vide.")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Objet qui contient le modèle (Character). Il se tourne vers la direction de marche et s'écrase en accroupi.")]
    [SerializeField] private Transform model;
    [Tooltip("Animator du rig (généré par AAR > Générer le squelette et les animations du joueur). Paramètres : Speed (float), Grounded (bool), Crouch (bool), Jump (trigger).")]
    [SerializeField] private Animator animator;

    [Header("Vue")]
    [SerializeField] private ViewMode viewMode = ViewMode.ThirdPerson;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float gamepadSensitivity = 150f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -70f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private float thirdPersonDistance = 3.5f;
    [Tooltip("Décalage latéral de la caméra en 3e personne (épaule).")]
    [SerializeField] private float shoulderOffset = 0.4f;
    [SerializeField] private LayerMask cameraCollisionMask = ~0;

    [Header("Déplacement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField, Range(0f, 1f)] private float airControl = 0.4f;
    [SerializeField] private float modelTurnSpeed = 12f;

    [Header("Saut et gravité")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;
    [Tooltip("Délai pendant lequel on peut encore sauter après avoir quitté le sol.")]
    [SerializeField] private float coyoteTime = 0.12f;
    [Tooltip("Un appui sur saut juste avant d'atterrir est quand même pris en compte.")]
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Accroupi")]
    [Tooltip("Hauteur du personnage debout (Character.fbx ≈ 1,9 m).")]
    [SerializeField] private float standingHeight = 1.9f;
    [SerializeField] private float crouchHeight = 1.1f;
    [SerializeField] private float crouchTransitionSpeed = 10f;
    [Tooltip("Si vrai, une pression bascule accroupi/debout. Sinon il faut maintenir la touche.")]
    [SerializeField] private bool crouchToggle = false;
    [Tooltip("Pour un modèle sans animation : on l'écrase verticalement quand on s'accroupit. Désactivé automatiquement avec le rig animé.")]
    [SerializeField] private bool squashModelWhenCrouching = true;

    // Entrées
    private InputActionMap playerMap;
    private InputAction moveAction, lookAction, jumpAction, crouchAction, sprintAction;
    private InputAction toggleViewAction, pauseAction, flashlightAction;
    private bool ownsActions;       // vrai si la map a été créée par code (pas d'asset)
    private InputActionMap extraMap; // ToggleView/Pause créées par code si absentes de l'asset
    private bool inputEnabled = true;

    // État
    private CharacterController controller;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float yaw, pitch;
    private float lastGroundedTime = -10f;
    private float lastJumpPressedTime = -10f;
    private bool wantsCrouch;
    private bool isCrouching;
    private float eyeHeightRatio;
    private Vector3 modelBaseScale = Vector3.one;
    private Renderer[] modelRenderers;

    public bool IsGrounded => controller.isGrounded;
    public bool IsCrouching => isCrouching;
    public bool IsSprinting { get; private set; }
    public Vector3 Velocity => controller.velocity;
    public ViewMode CurrentViewMode => viewMode;

    // ---------- Accès pour le menu Paramètres ----------
    public InputActionMap PlayerMap => playerMap;
    public InputAction PauseAction => pauseAction;
    public InputAction FlashlightAction => flashlightAction;
    public Transform CameraPivot => cameraPivot;
    public bool InputEnabled => inputEnabled;

    public float MouseSensitivity
    {
        get => mouseSensitivity;
        set { mouseSensitivity = value; GameSettings.SetFloat("MouseSensitivity", value); }
    }
    public float GamepadSensitivity
    {
        get => gamepadSensitivity;
        set { gamepadSensitivity = value; GameSettings.SetFloat("GamepadSensitivity", value); }
    }
    public bool InvertY
    {
        get => invertY;
        set { invertY = value; GameSettings.SetBool("InvertY", value); }
    }
    public bool CrouchToggle
    {
        get => crouchToggle;
        set { crouchToggle = value; wantsCrouch = false; GameSettings.SetBool("CrouchToggle", value); }
    }
    public float FieldOfView
    {
        get => playerCamera != null ? playerCamera.fieldOfView : 60f;
        set { if (playerCamera != null) playerCamera.fieldOfView = value; GameSettings.SetFloat("FieldOfView", value); }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.height = standingHeight;
        controller.center = new Vector3(0f, standingHeight / 2f, 0f);

        if (playerCamera == null) playerCamera = Camera.main;
        if (cameraPivot == null)
        {
            cameraPivot = new GameObject("CameraPivot").transform;
            cameraPivot.SetParent(transform, false);
            cameraPivot.localPosition = new Vector3(0f, standingHeight * 0.9f, 0f);
        }
        eyeHeightRatio = cameraPivot.localPosition.y / standingHeight;

        if (model != null)
        {
            modelBaseScale = model.localScale;
            modelRenderers = model.GetComponentsInChildren<Renderer>();
        }
        if (animator == null) animator = GetComponentInChildren<Animator>();

        yaw = transform.eulerAngles.y;
        LoadSettings();
        SetupInput();
        ApplyViewMode();

        // Menu Paramètres (Échap), lampe torche (F) et brouillard, créés automatiquement s'ils manquent
        SettingsMenu.Ensure(this);
        if (!TryGetComponent(out PlayerFlashlight _)) gameObject.AddComponent<PlayerFlashlight>();
        AtmosphereFog.Ensure();
    }

    private void LoadSettings()
    {
        mouseSensitivity = GameSettings.GetFloat("MouseSensitivity", mouseSensitivity);
        gamepadSensitivity = GameSettings.GetFloat("GamepadSensitivity", gamepadSensitivity);
        invertY = GameSettings.GetBool("InvertY", invertY);
        crouchToggle = GameSettings.GetBool("CrouchToggle", crouchToggle);
        viewMode = (ViewMode)GameSettings.GetInt("ViewMode", (int)viewMode);
        if (playerCamera != null)
            playerCamera.fieldOfView = GameSettings.GetFloat("FieldOfView", playerCamera.fieldOfView);
    }

    // ---------- Entrées ----------
    private void SetupInput()
    {
        if (inputActions != null)
        {
            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: false);
            if (playerMap != null)
            {
                moveAction = playerMap.FindAction("Move");
                lookAction = playerMap.FindAction("Look");
                jumpAction = playerMap.FindAction("Jump");
                crouchAction = playerMap.FindAction("Crouch");
                sprintAction = playerMap.FindAction("Sprint");
                toggleViewAction = playerMap.FindAction("ToggleView");
                pauseAction = playerMap.FindAction("Pause");
                flashlightAction = playerMap.FindAction("Flashlight");
            }
        }

        if (moveAction != null && lookAction != null && jumpAction != null && crouchAction != null && sprintAction != null)
        {
            CreateMissingExtraActions();
            GameSettings.LoadBindings(playerMap);
            return;
        }

        // Secours si l'asset n'est pas assigné : mêmes touches, créées par code.
        Debug.LogWarning("[PlayerController] InputSystem_Actions non assigné : utilisation des touches par défaut.", this);
        ownsActions = true;
        playerMap = new InputActionMap("Player");

        moveAction = playerMap.AddAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");

        lookAction = playerMap.AddAction("Look", InputActionType.Value);
        lookAction.AddBinding("<Pointer>/delta");
        lookAction.AddBinding("<Gamepad>/rightStick");

        jumpAction = playerMap.AddAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        crouchAction = playerMap.AddAction("Crouch", InputActionType.Button);
        crouchAction.AddBinding("<Keyboard>/c");
        crouchAction.AddBinding("<Keyboard>/leftCtrl");
        crouchAction.AddBinding("<Gamepad>/buttonEast");

        sprintAction = playerMap.AddAction("Sprint", InputActionType.Button);
        sprintAction.AddBinding("<Keyboard>/leftShift");
        sprintAction.AddBinding("<Gamepad>/leftStickPress");

        toggleViewAction = playerMap.AddAction("ToggleView", InputActionType.Button);
        toggleViewAction.AddBinding("<Keyboard>/v").WithGroup("Keyboard&Mouse");
        toggleViewAction.AddBinding("<Gamepad>/rightStickPress").WithGroup("Gamepad");

        pauseAction = playerMap.AddAction("Pause", InputActionType.Button);
        pauseAction.AddBinding("<Keyboard>/escape").WithGroup("Keyboard&Mouse");
        pauseAction.AddBinding("<Gamepad>/start").WithGroup("Gamepad");

        flashlightAction = playerMap.AddAction("Flashlight", InputActionType.Button);
        flashlightAction.AddBinding("<Keyboard>/f").WithGroup("Keyboard&Mouse");
        flashlightAction.AddBinding("<Gamepad>/dpad/up").WithGroup("Gamepad");

        GameSettings.LoadBindings(playerMap);
    }

    // Si l'asset d'un coéquipier n'a pas encore ToggleView / Pause
    private void CreateMissingExtraActions()
    {
        if (toggleViewAction != null && pauseAction != null && flashlightAction != null) return;
        extraMap = new InputActionMap("PlayerExtra");
        if (toggleViewAction == null)
        {
            toggleViewAction = extraMap.AddAction("ToggleView", InputActionType.Button);
            toggleViewAction.AddBinding("<Keyboard>/v");
            toggleViewAction.AddBinding("<Gamepad>/rightStickPress");
        }
        if (pauseAction == null)
        {
            pauseAction = extraMap.AddAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");
        }
        if (flashlightAction == null)
        {
            flashlightAction = extraMap.AddAction("Flashlight", InputActionType.Button);
            flashlightAction.AddBinding("<Keyboard>/f");
            flashlightAction.AddBinding("<Gamepad>/dpad/up");
        }
    }

    private void OnEnable()
    {
        SetInputEnabled(inputEnabled);
    }

    private void OnDisable()
    {
        playerMap?.Disable();
        extraMap?.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (ownsActions) playerMap?.Dispose();
        extraMap?.Dispose();
    }

    /// <summary>
    /// Active/désactive les commandes du joueur (utilisé par le menu Paramètres).
    /// L'action Pause reste active pour pouvoir refermer le menu.
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (enabled)
        {
            playerMap?.Enable();
            extraMap?.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            playerMap?.Disable();
            extraMap?.Disable();
            pauseAction?.Enable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        if (inputEnabled)
        {
            // Si la souris a été libérée (Alt-Tab…), un clic la reprend
            if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Bascule 1re ⇄ 3e personne
            if (toggleViewAction != null && toggleViewAction.WasPressedThisFrame())
                SetViewMode(viewMode == ViewMode.FirstPerson ? ViewMode.ThirdPerson : ViewMode.FirstPerson);
        }

        HandleLook();
        HandleCrouch();
        HandleMovement();
        UpdateAnimator();
    }

    private void LateUpdate()
    {
        UpdateCamera();
    }

    // ---------- Regard ----------
    private void HandleLook()
    {
        if (!inputEnabled) return;
        if (Cursor.lockState != CursorLockMode.Locked && !IsGamepadActive(lookAction)) return;

        Vector2 look = lookAction.ReadValue<Vector2>();
        float sens = IsGamepadActive(lookAction) ? gamepadSensitivity * Time.deltaTime : mouseSensitivity;

        yaw += look.x * sens;
        pitch = Mathf.Clamp(pitch - look.y * sens * (invertY ? -1f : 1f), minPitch, maxPitch);

        // En 1re personne, le corps suit la caméra horizontalement
        if (viewMode == ViewMode.FirstPerson)
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private static bool IsGamepadActive(InputAction action) =>
        action.activeControl != null && action.activeControl.device is Gamepad;

    // ---------- Accroupi ----------
    private void HandleCrouch()
    {
        if (crouchToggle)
        {
            if (crouchAction.WasPressedThisFrame()) wantsCrouch = !wantsCrouch;
        }
        else
        {
            wantsCrouch = crouchAction.IsPressed();
        }

        // On ne se relève que s'il y a la place au-dessus
        if (wantsCrouch) isCrouching = true;
        else if (isCrouching && CanStandUp()) isCrouching = false;

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        float newHeight = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        if (Mathf.Abs(newHeight - targetHeight) < 0.01f) newHeight = targetHeight;

        controller.height = newHeight;
        controller.center = new Vector3(0f, newHeight / 2f, 0f);

        Vector3 pivotPos = cameraPivot.localPosition;
        pivotPos.y = newHeight * eyeHeightRatio;
        cameraPivot.localPosition = pivotPos;

        // Le modèle est statique : on l'écrase pour montrer qu'on est accroupi
        if (model != null && squashModelWhenCrouching)
        {
            float ratio = newHeight / standingHeight;
            model.localScale = new Vector3(modelBaseScale.x, modelBaseScale.y * ratio, modelBaseScale.z);
        }
    }

    private bool CanStandUp()
    {
        float radius = controller.radius * 0.95f;
        Vector3 bottom = transform.position + Vector3.up * (controller.height - radius);
        Vector3 top = transform.position + Vector3.up * (standingHeight - radius);
        Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider c in hits)
            if (!c.transform.IsChildOf(transform)) return false; // obstacle au-dessus
        return true;
    }

    // ---------- Déplacement, saut, gravité ----------
    private void HandleMovement()
    {
        bool grounded = controller.isGrounded;
        if (grounded) lastGroundedTime = Time.time;
        if (jumpAction.WasPressedThisFrame()) lastJumpPressedTime = Time.time;

        // Direction relative à la caméra
        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 wishDir = yawRot * new Vector3(input.x, 0f, input.y);

        // En 1re personne on ne court qu'en avançant ; en 3e personne dans toutes les directions
        bool movingForward = viewMode == ViewMode.ThirdPerson ? input.sqrMagnitude > 0.01f : input.y > 0.1f;
        IsSprinting = !isCrouching && sprintAction.IsPressed() && movingForward;
        float targetSpeed = isCrouching ? crouchSpeed : IsSprinting ? runSpeed : walkSpeed;

        float accel = grounded ? acceleration : acceleration * airControl;
        horizontalVelocity = Vector3.Lerp(horizontalVelocity, wishDir * targetSpeed, accel * Time.deltaTime);

        // Saut (coyote time + buffer)
        bool canJump = Time.time - lastGroundedTime <= coyoteTime && !isCrouching;
        bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
        if (canJump && jumpBuffered)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpPressedTime = -10f;
            lastGroundedTime = -10f;
            SetAnimTrigger("Jump");
        }

        // Gravité
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f; // colle au sol / pentes
        else verticalVelocity += gravity * Time.deltaTime;

        // On arrête de monter si on se cogne la tête
        if ((controller.collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;

        controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

        // En 3e personne, le modèle se tourne vers la direction de marche
        if (viewMode == ViewMode.ThirdPerson && model != null && wishDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(wishDir, Vector3.up);
            model.rotation = Quaternion.Slerp(model.rotation, targetRot, modelTurnSpeed * Time.deltaTime);
        }
    }

    // ---------- Caméra ----------
    private void UpdateCamera()
    {
        if (playerCamera == null) return;

        if (viewMode == ViewMode.FirstPerson)
        {
            playerCamera.transform.SetPositionAndRotation(cameraPivot.position, cameraPivot.rotation);
            return;
        }

        // 3e personne : caméra derrière l'épaule, sans traverser les murs
        Vector3 origin = cameraPivot.position + cameraPivot.right * shoulderOffset;
        Vector3 back = -cameraPivot.forward;
        float distance = thirdPersonDistance;
        RaycastHit[] hits = Physics.SphereCastAll(origin, 0.2f, back, thirdPersonDistance,
            cameraCollisionMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            distance = Mathf.Min(distance, Mathf.Max(0.3f, hit.distance - 0.1f));
        }
        playerCamera.transform.SetPositionAndRotation(origin + back * distance, cameraPivot.rotation);
    }

    /// <summary>Bascule 1re / 3e personne (utilisable par le système des 3 visions).</summary>
    public void SetViewMode(ViewMode mode)
    {
        viewMode = mode;
        GameSettings.SetInt("ViewMode", (int)mode);
        ApplyViewMode();
    }

    private void ApplyViewMode()
    {
        if (viewMode == ViewMode.FirstPerson)
        {
            // Le corps s'aligne sur le regard
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (model != null) model.localRotation = Quaternion.identity;
        }
        else if (model != null)
        {
            // Le modèle garde l'orientation qu'il avait (tourné en espace monde par HandleMovement)
            Quaternion keep = model.rotation;
            transform.rotation = Quaternion.identity;
            model.rotation = keep;
        }

        // En 1re personne on cache le modèle mais on garde son ombre
        if (modelRenderers == null) return;
        foreach (Renderer r in modelRenderers)
            r.shadowCastingMode = viewMode == ViewMode.FirstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
    }

    private void OnValidate()
    {
        crouchHeight = Mathf.Clamp(crouchHeight, 0.5f, standingHeight);
        if (Application.isPlaying && controller != null) ApplyViewMode();
    }

    // ---------- Animator (optionnel) ----------
    private void UpdateAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        Vector3 flat = controller.velocity; flat.y = 0f;
        SetAnimFloat("Speed", flat.magnitude);
        // Petite tolérance pour ne pas basculer en chute à chaque marche d'escalier
        SetAnimBool("Grounded", controller.isGrounded || Time.time - lastGroundedTime < 0.15f);
        SetAnimBool("Crouch", isCrouching);
    }

    private bool HasParam(string n, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        foreach (var p in animator.parameters)
            if (p.name == n && p.type == type) return true;
        return false;
    }

    private void SetAnimFloat(string n, float v) { if (HasParam(n, AnimatorControllerParameterType.Float)) animator.SetFloat(n, v); }
    private void SetAnimBool(string n, bool v) { if (HasParam(n, AnimatorControllerParameterType.Bool)) animator.SetBool(n, v); }
    private void SetAnimTrigger(string n) { if (HasParam(n, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(n); }
}

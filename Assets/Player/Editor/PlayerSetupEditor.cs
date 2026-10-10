using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Menu "AAR > Créer le joueur dans la scène" : monte le joueur complet dans la scène ouverte
/// (Character.fbx + CharacterController + PlayerController + caméra + sol si besoin)
/// et l'enregistre en prefab dans Assets/Player/Player.prefab.
/// </summary>
public static class PlayerSetupEditor
{
    private const string ModelPath = "Assets/Models/Character.fbx";
    private const string InputPath = "Assets/InputSystem_Actions.inputactions";
    private const string PrefabPath = "Assets/Player/Player.prefab";

    [MenuItem("AAR/Créer le joueur dans la scène")]
    public static void CreatePlayer()
    {
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            EditorUtility.DisplayDialog("AAR", "Modèle introuvable : " + ModelPath, "OK");
            return;
        }

        var existing = Object.FindAnyObjectByType<PlayerController>();
        if (existing != null &&
            !EditorUtility.DisplayDialog("AAR", "Un joueur existe déjà dans la scène. En créer un autre ?", "Oui", "Annuler"))
        {
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // --- Placement (calculé avant de créer le joueur pour ne pas toucher son propre collider) : au point visé dans la vue Scène, sinon à l'origine ---
        Vector3 spawn = Vector3.zero;
        var sv = SceneView.lastActiveSceneView;
        if (sv != null && Physics.Raycast(sv.camera.transform.position, sv.camera.transform.forward, out RaycastHit hitView, 200f))
            spawn = hitView.point;

        // --- Sol de test s'il n'y a rien sous le joueur ---
        bool needsGround = !Physics.Raycast(spawn + Vector3.up * 1f, Vector3.down, 50f);
        if (needsGround)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(ground, "Créer le sol");
            ground.name = "Ground (test)";
            ground.transform.position = spawn;
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
        }

        // --- Racine du joueur ---
        var player = new GameObject("Player");
        Undo.RegisterCreatedObjectUndo(player, "Créer le joueur");
        player.tag = "Player";

        // --- Modèle : un conteneur aux pieds, le FBX dedans recalé pour que les pieds soient à Y = 0 ---
        var modelRoot = new GameObject("Model").transform;
        modelRoot.SetParent(player.transform, false);

        var character = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        character.transform.SetParent(modelRoot, false);
        foreach (var col in character.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(col);

        Bounds b = ComputeBounds(character);
        float height = Mathf.Max(0.5f, b.size.y);
        float radius = Mathf.Clamp(height * 0.16f, 0.15f, 0.5f); // ≈ largeur du buste
        // Recentre le mesh sur l'axe du joueur, pieds au sol
        character.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);

        // --- Physique ---
        var cc = player.AddComponent<CharacterController>();
        cc.height = height;
        cc.radius = radius;
        cc.center = new Vector3(0f, height / 2f, 0f);
        cc.stepOffset = Mathf.Min(0.3f, height * 0.2f);
        cc.skinWidth = Mathf.Max(0.01f, height * 0.01f);
        modelRoot.localPosition = new Vector3(0f, -cc.skinWidth, 0f);
        cc.slopeLimit = 45f;

        // --- Pivot caméra à hauteur des yeux ---
        var pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(player.transform, false);
        pivot.localPosition = new Vector3(0f, height * 0.9f, 0f);

        // --- Caméra ---
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            Undo.RegisterCreatedObjectUndo(camGO, "Créer la caméra");
            camGO.tag = "MainCamera";
            cam = camGO.GetComponent<Camera>();
        }
        cam.nearClipPlane = 0.05f;

        // --- Contrôleur ---
        var pc = player.AddComponent<PlayerController>();
        var so = new SerializedObject(pc);
        so.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
        so.FindProperty("cameraPivot").objectReferenceValue = pivot;
        so.FindProperty("playerCamera").objectReferenceValue = cam;
        so.FindProperty("model").objectReferenceValue = modelRoot;
        so.FindProperty("standingHeight").floatValue = height;
        so.FindProperty("crouchHeight").floatValue = height * 0.58f;
        so.ApplyModifiedPropertiesWithoutUndo();

        player.transform.position = spawn + Vector3.up * 0.05f;

        // --- Rig animé s'il a déjà été généré (AAR > Générer le squelette et les animations du joueur) ---
        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRigGenerator.RigPrefabPath);
        if (rigPrefab != null)
        {
            height = CharacterRigGenerator.RigHeight(rigPrefab);
            CharacterRigGenerator.ConfigurePlayer(pc, rigPrefab, height);
        }

        // --- Prefab ---
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            PrefabUtility.SaveAsPrefabAssetAndConnect(player, PrefabPath, InteractionMode.UserAction);

        EditorSceneManager.MarkSceneDirty(player.scene);
        Selection.activeGameObject = player;
        if (rigPrefab == null)
            Debug.Log("[AAR] Astuce : AAR > Générer le squelette et les animations du joueur pour l'animer.");
        Debug.Log($"[AAR] Joueur créé (hauteur {height:0.00} m, rayon {radius:0.00} m). " +
                  "Si le personnage regarde vers l'arrière, tourne l'objet Player/Model/Character de 180° en Y.", player);
    }

    private static Bounds ComputeBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));

        // Bornes exprimées dans l'espace du parent (le conteneur Model, à l'origine)
        Transform parent = go.transform.parent;
        Bounds b = default;
        bool first = true;
        foreach (var r in renderers)
        {
            Bounds wb = r.bounds;
            Vector3 min = parent.InverseTransformPoint(wb.min);
            Vector3 max = parent.InverseTransformPoint(wb.max);
            var lb = new Bounds((min + max) / 2f, Vector3.zero);
            lb.Encapsulate(min); lb.Encapsulate(max);
            if (first) { b = lb; first = false; } else b.Encapsulate(lb);
        }
        return b;
    }
}

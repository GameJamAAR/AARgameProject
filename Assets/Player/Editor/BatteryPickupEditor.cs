using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Menu "AAR > Ajouter une pile" : pose une pile à ramasser devant la vue Scène.</summary>
public static class BatteryPickupEditor
{
    private const string PrefabPath = "Assets/Player/BatteryPickup.prefab";

    [MenuItem("AAR/Ajouter une pile")]
    public static void AddBattery()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = CreatePrefab();

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Ajouter une pile");

        // Au point visé dans la vue Scène, posée au sol
        Vector3 pos = Vector3.zero;
        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            var cam = sv.camera.transform;
            pos = Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 200f) ? hit.point : sv.pivot;
        }
        go.transform.position = pos + Vector3.up * 0.6f;

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    private static GameObject CreatePrefab()
    {
        // Pile simple : corps + borne, dans un objet racine qui porte le trigger
        var root = new GameObject("BatteryPickup");
        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.45f;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.14f, 0.12f, 0.14f);
        body.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);

        var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.name = "Cap";
        Object.DestroyImmediate(cap.GetComponent<Collider>());
        cap.transform.SetParent(body.transform, false);
        cap.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        cap.transform.localScale = new Vector3(0.45f, 0.12f, 0.45f);

        root.AddComponent<BatteryPickup>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu "AAR > Générer le squelette et les animations du joueur".
///
/// À partir du mesh statique Assets/Models/Character.fbx, génère dans Assets/Player/Generated :
///  - CharacterRigged.asset      : copie du mesh avec poids de skinning (2 os max par sommet)
///  - Anim_*.anim                : Idle, Walk, Run, CrouchIdle, CrouchWalk, Jump, Fall (procédurales)
///  - CharacterRig.controller    : Animator (paramètres Speed, Grounded, Crouch, Jump)
///  - CharacterRig.prefab        : squelette + SkinnedMeshRenderer + Animator
/// puis remplace le modèle statique du joueur (prefab Player et/ou objet dans la scène).
///
/// Le squelette et les poses ont été calés sur les proportions de Character.fbx (≈ 1,9 m, gros casque).
/// Si le modèle change de proportions, il faudra ajuster la table JOINTS ci-dessous.
/// </summary>
public static class CharacterRigGenerator
{
    private const string ModelPath = "Assets/Models/Character.fbx";
    private const string OutDir = "Assets/Player/Generated";
    private const string PlayerPrefabPath = "Assets/Player/Player.prefab";
    public const string RigPrefabPath = OutDir + "/CharacterRig.prefab";

    // ------------------------------------------------------------------
    // Squelette de référence, dans le repère d'origine du modèle :
    // x = côté gauche du personnage (+), y = haut (centre du mesh à 0, pieds à -0.953),
    // z = arrière du personnage (+). Converti plus bas vers le repère Unity
    // (x droite, y haut depuis les pieds, z avant).
    // ------------------------------------------------------------------
    private const float RefHeight = 1.9021526f;
    private const float RefFeetY = -0.9530333f;
    private const float RefCenterZ = 0.0136987f;

    private struct JointDef
    {
        public string name, parent; public Vector3 pos;
        public JointDef(string n, string p, float x, float y, float z) { name = n; parent = p; pos = new Vector3(x, y, z); }
    }

    private static readonly JointDef[] BaseJoints =
    {
        new JointDef("Hips",       null,         0.00f, -0.30f,  0.00f),
        new JointDef("Spine",      "Hips",       0.00f, -0.12f,  0.00f),
        new JointDef("Chest",      "Spine",      0.00f,  0.08f,  0.00f),
        new JointDef("Neck",       "Chest",      0.00f,  0.28f,  0.00f),
        new JointDef("Head",       "Neck",       0.00f,  0.36f,  0.00f),
        new JointDef("HeadTop",    "Head",       0.00f,  0.95f,  0.00f),
        new JointDef("L_Shoulder", "Chest",      0.14f,  0.18f,  0.00f),
        new JointDef("L_UpperArm", "L_Shoulder", 0.30f,  0.10f,  0.00f),
        new JointDef("L_LowerArm", "L_UpperArm", 0.42f, -0.08f,  0.00f),
        new JointDef("L_Hand",     "L_LowerArm", 0.47f, -0.24f, -0.03f),
        new JointDef("L_HandEnd",  "L_Hand",     0.48f, -0.40f, -0.05f),
        new JointDef("L_UpperLeg", "Hips",       0.17f, -0.38f,  0.00f),
        new JointDef("L_LowerLeg", "L_UpperLeg", 0.19f, -0.62f,  0.00f),
        new JointDef("L_Foot",     "L_LowerLeg", 0.21f, -0.84f,  0.00f),
        new JointDef("L_Toes",     "L_Foot",     0.22f, -0.92f, -0.22f),
    };

    // Os qui déforment le mesh -> articulation qui termine leur segment
    private static readonly Dictionary<string, string> SegmentEnd = new Dictionary<string, string>
    {
        { "Hips", "Spine" }, { "Spine", "Chest" }, { "Chest", "Neck" }, { "Neck", "Head" }, { "Head", "HeadTop" },
        { "L_Shoulder", "L_UpperArm" }, { "L_UpperArm", "L_LowerArm" }, { "L_LowerArm", "L_Hand" }, { "L_Hand", "L_HandEnd" },
        { "L_UpperLeg", "L_LowerLeg" }, { "L_LowerLeg", "L_Foot" }, { "L_Foot", "L_Toes" },
        { "R_Shoulder", "R_UpperArm" }, { "R_UpperArm", "R_LowerArm" }, { "R_LowerArm", "R_Hand" }, { "R_Hand", "R_HandEnd" },
        { "R_UpperLeg", "R_LowerLeg" }, { "R_LowerLeg", "R_Foot" }, { "R_Foot", "R_Toes" },
    };

    private static List<JointDef> AllJoints()
    {
        var list = new List<JointDef>(BaseJoints);
        foreach (var j in BaseJoints)
            if (j.name.StartsWith("L_"))
                list.Add(new JointDef("R_" + j.name.Substring(2),
                    j.parent.StartsWith("L_") ? "R_" + j.parent.Substring(2) : j.parent,
                    -j.pos.x, j.pos.y, j.pos.z));
        return list;
    }

    // Repère de référence -> repère Unity du rig (pieds à y = 0, regard vers +Z, gauche = -X)
    private static Vector3 RefToRig(Vector3 p, float s) => new Vector3(-p.x * s, (p.y - RefFeetY) * s, -p.z * s);
    private static Vector3 RigToRef(Vector3 v, float s) => new Vector3(-v.x / s, v.y / s + RefFeetY, -v.z / s);

    // ==================================================================
    [MenuItem("AAR/Générer le squelette et les animations du joueur")]
    public static void Generate()
    {
        EnsureReadable();
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (fbx == null) { EditorUtility.DisplayDialog("AAR", "Modèle introuvable : " + ModelPath, "OK"); return; }

        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/Player", "Generated");

        // ---------- 1. Mesh cuit dans le repère du rig ----------
        // On garde la rotation/échelle de la racine du FBX (ici -90° en X et ×100) : elles font partie du modèle
        var temp = UnityEngine.Object.Instantiate(fbx);
        temp.transform.position = Vector3.zero;
        var mf = temp.GetComponentInChildren<MeshFilter>();
        var mr = temp.GetComponentInChildren<MeshRenderer>();
        if (mf == null || mf.sharedMesh == null)
        {
            UnityEngine.Object.DestroyImmediate(temp);
            EditorUtility.DisplayDialog("AAR", "Aucun MeshFilter trouvé dans " + ModelPath, "OK");
            return;
        }
        Mesh src = mf.sharedMesh;
        Material[] materials = mr != null ? mr.sharedMaterials : new Material[0];
        Matrix4x4 toWorld = mf.transform.localToWorldMatrix;
        UnityEngine.Object.DestroyImmediate(temp);

        Vector3[] verts = src.vertices;
        Vector3[] normals = src.normals;
        Vector4[] tangents = src.tangents;
        for (int i = 0; i < verts.Length; i++) verts[i] = toWorld.MultiplyPoint3x4(verts[i]);
        for (int i = 0; i < normals.Length; i++) normals[i] = toWorld.MultiplyVector(normals[i]).normalized;
        for (int i = 0; i < tangents.Length; i++)
        {
            Vector3 t = toWorld.MultiplyVector(tangents[i]).normalized;
            tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
        }

        Bounds b = BoundsOf(verts);
        float height = b.size.y;
        if (height < 0.2f || height > 20f)
            Debug.LogWarning($"[AAR] Hauteur du modèle inattendue : {height:0.###} m. Vérifie l'échelle d'import de {ModelPath}.");
        float s = height / RefHeight;

        // Sens du regard : les pointes de pieds sont à l'avant, le sac à dos à l'arrière
        float feetZ = MeanZ(verts, b.min.y, b.min.y + 0.05f * height);
        float torsoZ = MeanZ(verts, b.min.y + 0.45f * height, b.min.y + 0.62f * height);
        bool facesBackward = feetZ - torsoZ < 0f;
        if (facesBackward)
        {
            Quaternion flip = Quaternion.Euler(0f, 180f, 0f);
            for (int i = 0; i < verts.Length; i++) verts[i] = flip * verts[i];
            for (int i = 0; i < normals.Length; i++) normals[i] = flip * normals[i];
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 t = flip * (Vector3)tangents[i];
                tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
            }
            b = BoundsOf(verts);
        }

        // Origine : pieds au sol, centré comme le modèle de référence
        Vector3 origin = new Vector3(b.center.x, b.min.y, b.center.z + RefCenterZ * s);
        for (int i = 0; i < verts.Length; i++) verts[i] -= origin;

        // ---------- 2. Squelette ----------
        List<JointDef> joints = AllJoints();
        var root = new GameObject("CharacterRig");
        var boneByName = new Dictionary<string, Transform>();
        var bones = new Transform[joints.Count];
        for (int i = 0; i < joints.Count; i++)
        {
            var j = joints[i];
            var t = new GameObject(j.name).transform;
            t.SetParent(j.parent == null ? root.transform : boneByName[j.parent], false);
            t.position = RefToRig(j.pos, s);   // racine à l'origine, rotations identité
            t.localRotation = Quaternion.identity;
            boneByName[j.name] = t;
            bones[i] = t;
        }

        // ---------- 3. Poids de skinning ----------
        var boneIndex = new Dictionary<string, int>();
        for (int i = 0; i < joints.Count; i++) boneIndex[joints[i].name] = i;
        var refPos = new Dictionary<string, Vector3>();
        foreach (var j in joints) refPos[j.name] = j.pos;

        var weights = new BoneWeight[verts.Length];
        for (int i = 0; i < verts.Length; i++)
            weights[i] = ComputeWeight(RigToRef(verts[i], s), refPos, boneIndex);

        var bindposes = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            bindposes[i] = bones[i].worldToLocalMatrix * root.transform.localToWorldMatrix;

        var mesh = new Mesh { name = "CharacterRigged" };
        mesh.indexFormat = src.indexFormat;
        mesh.vertices = verts;
        if (normals.Length == verts.Length) mesh.normals = normals;
        if (tangents.Length == verts.Length) mesh.tangents = tangents;
        var uv = new List<Vector2>();
        for (int ch = 0; ch < 4; ch++)
        {
            src.GetUVs(ch, uv);
            if (uv.Count == verts.Length) mesh.SetUVs(ch, uv);
        }
        if (src.colors.Length == verts.Length) mesh.colors = src.colors;
        mesh.subMeshCount = src.subMeshCount;
        for (int sm = 0; sm < src.subMeshCount; sm++)
            mesh.SetTriangles(src.GetTriangles(sm), sm);
        mesh.boneWeights = weights;
        mesh.bindposes = bindposes;
        mesh.RecalculateBounds();
        if (normals.Length != verts.Length) mesh.RecalculateNormals();

        string meshPath = OutDir + "/CharacterRigged.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        var body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        var smr = body.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.bones = bones;
        smr.rootBone = boneByName["Hips"];
        smr.sharedMaterials = materials;
        Bounds lb = mesh.bounds;
        lb.center -= boneByName["Hips"].position;
        lb.Expand(0.6f * s);
        smr.localBounds = lb;

        // ---------- 4. Animations ----------
        var clips = new Dictionary<string, AnimationClip>
        {
            { "Idle",       BakeClip("Idle",       3.0f, true,  Idle,                         root.transform, boneByName, s) },
            { "Walk",       BakeClip("Walk",       0.6f, true,  u => Walk(u, false),          root.transform, boneByName, s) },
            { "Run",        BakeClip("Run",        0.45f, true, u => Walk(u, true),           root.transform, boneByName, s) },
            { "CrouchIdle", BakeClip("CrouchIdle", 3.0f, true,  u => Crouch(u, false),        root.transform, boneByName, s) },
            { "CrouchWalk", BakeClip("CrouchWalk", 0.8f, true,  u => Crouch(u, true),         root.transform, boneByName, s) },
            { "Jump",       BakeClip("Jump",       0.25f, false, Jump,                        root.transform, boneByName, s) },
            { "Fall",       BakeClip("Fall",       1.0f, true,  Fall,                         root.transform, boneByName, s) },
        };
        foreach (var kv in clips)
        {
            string p = $"{OutDir}/Anim_{kv.Key}.anim";
            AssetDatabase.DeleteAsset(p);
            AssetDatabase.CreateAsset(kv.Value, p);
        }

        // Vitesses du joueur pour les seuils des blend trees
        float walkSpeed = 4f, runSpeed = 7f, crouchSpeed = 2f;
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        PlayerController pcRef = playerPrefab != null ? playerPrefab.GetComponent<PlayerController>() : null;
        if (pcRef == null) { var found = ScenePlayers(); if (found.Count > 0) pcRef = found[0]; }
        if (pcRef != null)
        {
            var so = new SerializedObject(pcRef);
            walkSpeed = so.FindProperty("walkSpeed").floatValue;
            runSpeed = so.FindProperty("runSpeed").floatValue;
            crouchSpeed = so.FindProperty("crouchSpeed").floatValue;
        }

        var controller = BuildController(clips, walkSpeed, runSpeed, crouchSpeed);

        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // ---------- 5. Prefab ----------
        var rigPrefab = PrefabUtility.SaveAsPrefabAsset(root, RigPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        // ---------- 6. Brancher sur le joueur ----------
        string where = InstallOnPlayer(rigPrefab, height);

        Debug.Log($"[AAR] Squelette ({joints.Count} os) et 7 animations générés dans {OutDir}. " +
                  (facesBackward ? "Le modèle regardait vers -Z : il a été retourné. " : "") + where);
        EditorUtility.DisplayDialog("AAR", "Squelette et animations générés.\n\n" + where, "OK");
    }

    // ==================================================================
    // Poids : chaque zone du corps n'est influencée que par ses os, puis
    // poids inverse à la distance au segment de l'os (puissance 4), 2 os max.
    // ==================================================================
    private static BoneWeight ComputeWeight(Vector3 p, Dictionary<string, Vector3> J, Dictionary<string, int> index)
    {
        float ax = Mathf.Abs(p.x), y = p.y;
        string sd = p.x >= 0f ? "L" : "R";
        string[] cand;
        if (y > 0.33f) cand = new[] { "Head" };
        else if (y > 0.26f && ax < 0.27f) cand = new[] { "Head", "Neck", "Chest" };
        else if ((ax > 0.31f && y > -0.5f && y < 0.22f) || (ax > 0.26f && y > -0.3f && y < 0.2f))
            cand = new[] { sd + "_Shoulder", sd + "_UpperArm", sd + "_LowerArm", sd + "_Hand" };
        else if (y < -0.40f) cand = new[] { sd + "_UpperLeg", sd + "_LowerLeg", sd + "_Foot" };
        else if (y < -0.30f) cand = new[] { "Hips", sd + "_UpperLeg" };
        else cand = new[] { "Hips", "Spine", "Chest", sd + "_Shoulder" };

        int best0 = -1, best1 = -1; float w0 = 0f, w1 = 0f;
        foreach (string c in cand)
        {
            float d = Mathf.Max(SegmentDistance(p, J[c], J[SegmentEnd[c]]), 0.01f);
            float w = 1f / (d * d * d * d);
            if (w > w0) { best1 = best0; w1 = w0; best0 = index[c]; w0 = w; }
            else if (w > w1) { best1 = index[c]; w1 = w; }
        }
        var bw = new BoneWeight { boneIndex0 = best0, weight0 = 1f };
        if (best1 >= 0)
        {
            float sum = w0 + w1;
            bw.weight0 = w0 / sum;
            bw.boneIndex1 = best1;
            bw.weight1 = w1 / sum;
        }
        return bw;
    }

    private static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Vector3.Dot(ab, ab));
        return Vector3.Distance(p, a + t * ab);
    }

    // ==================================================================
    // Poses (degrés, rotations locales par rapport à la pose de repos).
    // X+ : jambe/bras vers l'arrière, buste penché vers l'avant. t = phase 0..1.
    // ==================================================================
    private class RigPose
    {
        public readonly Dictionary<string, Vector3> rot = new Dictionary<string, Vector3>();
        public Vector3 hipOffset;
        public RigPose Set(string bone, float x, float y, float z) { rot[bone] = new Vector3(x, y, z); return this; }
    }

    private const float TwoPi = Mathf.PI * 2f;

    private static RigPose Idle(float t)
    {
        float s = Mathf.Sin(TwoPi * t);
        var P = new RigPose().Set("Chest", -2f * s, 0, 0).Set("Head", 1.5f * s, 0, 0);
        foreach (var (sd, sg) in Sides)
        {
            P.Set(sd + "_UpperArm", 0, 0, sg * (-3f - 2f * s));
            P.Set(sd + "_LowerArm", -8f, 0, 0);
        }
        P.hipOffset = new Vector3(0, -0.008f * (1f + s) / 2f, 0);
        return P;
    }

    private static RigPose Walk(float t, bool run)
    {
        float ph = TwoPi * t, s = Mathf.Sin(ph), c = Mathf.Cos(ph);
        float A = run ? 1.6f : 1f, lean = run ? 12f : 4f;
        var P = new RigPose()
            .Set("Spine", lean * 0.5f, 6f * s * A, 0)
            .Set("Chest", lean * 0.5f, 6f * s * A, 0)
            .Set("Head", -lean * 0.8f, -10f * s * A, 0);
        foreach (var (sd, sg) in Sides)
        {
            float ls = s * sg;
            float thigh = -28f * ls * A;
            float knee = Mathf.Max(0f, 40f * A * Mathf.Sin(ph + (sg > 0 ? 0f : Mathf.PI) + 1.4f)) + (run ? 10f : 4f);
            P.Set(sd + "_UpperLeg", thigh - (run ? 8f : 0f), 0, 0);
            P.Set(sd + "_LowerLeg", knee, 0, 0);
            P.Set(sd + "_Foot", -thigh * 0.35f - knee * 0.4f, 0, 0);
            float arm = 28f * ls * A;
            P.Set(sd + "_UpperArm", arm, 0, sg * (run ? -12f : -8f));
            P.Set(sd + "_LowerArm", -(run ? 50f : 15f) - Mathf.Max(0f, -arm) * 0.6f, 0, 0);
        }
        P.hipOffset = new Vector3(0, (run ? 0.035f : 0.025f) * Mathf.Abs(c) - (run ? 0.05f : 0.02f), 0);
        return P;
    }

    private static RigPose Crouch(float t, bool moving)
    {
        float ph = TwoPi * t, s = Mathf.Sin(ph), c = Mathf.Cos(ph);
        const float drop = 0.25f, hip = 20f;
        var P = new RigPose().Set("Hips", hip, 0, 0).Set("Spine", 6, 0, 0).Set("Chest", 6, 0, 0).Set("Head", -15, 0, 0);
        foreach (var (sd, sg) in Sides)
        {
            float ls = moving ? s * sg : 0f;
            float lift = moving ? Mathf.Max(0f, Mathf.Sin(ph + (sg > 0 ? 0f : Mathf.PI) + 1.4f)) : 0f;
            P.Set(sd + "_UpperLeg", -90f - hip - 14f * ls - 10f * lift, 0, sg * 6f);
            P.Set(sd + "_LowerLeg", 90f + 14f * lift, 0, 0);
            P.Set(sd + "_Foot", -10f + 14f * ls * 0.5f, 0, 0);
            P.Set(sd + "_UpperArm", -20f + (moving ? 14f * ls : 0f), 0, sg * -10f);
            P.Set(sd + "_LowerArm", -50f, 0, 0);
        }
        float breathe = moving ? 0f : 0.006f * s;
        P.hipOffset = new Vector3(0, -drop + (moving ? 0.012f * Mathf.Abs(c) : breathe), 0);
        return P;
    }

    private static RigPose Jump(float t)
    {
        var P = new RigPose().Set("Spine", -5, 0, 0).Set("Head", -5, 0, 0);
        foreach (var (sd, sg) in Sides)
        {
            bool left = sd == "L";
            P.Set(sd + "_UpperLeg", left ? -35f : -10f, 0, 0);
            P.Set(sd + "_LowerLeg", left ? 55f : 25f, 0, 0);
            P.Set(sd + "_Foot", -10f, 0, 0);
            P.Set(sd + "_UpperArm", -25f, 0, sg * -12f);
            P.Set(sd + "_LowerArm", -30f, 0, 0);
        }
        return P;
    }

    private static RigPose Fall(float t)
    {
        float s = Mathf.Sin(TwoPi * t);
        var P = new RigPose().Set("Spine", 5, 0, 0).Set("Head", 8, 0, 0);
        foreach (var (sd, sg) in Sides)
        {
            P.Set(sd + "_UpperLeg", -15f - 5f * s * sg, 0, sg * 4f);
            P.Set(sd + "_LowerLeg", 30f, 0, 0);
            P.Set(sd + "_UpperArm", -5f, 0, sg * (-35f - 8f * s));
            P.Set(sd + "_LowerArm", -20f, 0, 0);
        }
        return P;
    }

    private static readonly (string, float)[] Sides = { ("L", 1f), ("R", -1f) };

    // ==================================================================
    private static AnimationClip BakeClip(string name, float length, bool loop, Func<float, RigPose> poseAt,
        Transform root, Dictionary<string, Transform> bones, float scale)
    {
        int fps = length >= 2f ? 10 : 30;   // les poses lentes (repos) n'ont pas besoin de 30 images/s
        int frames = loop ? Mathf.Max(2, Mathf.RoundToInt(length * fps)) : 1;
        var clip = new AnimationClip { name = "Anim_" + name, frameRate = fps };

        var samples = new RigPose[frames + 1];
        for (int f = 0; f <= frames; f++) samples[f] = poseAt(loop ? (float)f / frames : 0f);
        float dt = loop ? length / frames : length;

        foreach (var kv in bones)
        {
            string path = AnimationUtility.CalculateTransformPath(kv.Value, root);
            var cx = new float[frames + 1]; var cy = new float[frames + 1];
            var cz = new float[frames + 1]; var cw = new float[frames + 1];
            Quaternion prev = Quaternion.identity;
            for (int f = 0; f <= frames; f++)
            {
                Vector3 e = samples[f].rot.TryGetValue(kv.Key, out var r) ? r : Vector3.zero;
                Quaternion q = Quaternion.Euler(e);
                if (f > 0 && Quaternion.Dot(prev, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                prev = q;
                cx[f] = q.x; cy[f] = q.y; cz[f] = q.z; cw[f] = q.w;
            }
            // Os immobile en pose de repos : pas de courbe (l'Animator remet la valeur par défaut)
            bool animated = !(IsConstant(cx) && IsConstant(cy) && IsConstant(cz) && IsConstant(cw));
            bool atRest = Mathf.Abs(cx[0]) < 1e-5f && Mathf.Abs(cy[0]) < 1e-5f && Mathf.Abs(cz[0]) < 1e-5f;
            if (animated || !atRest)
            {
                SetCurve(clip, path, "m_LocalRotation.x", cx, dt, loop);
                SetCurve(clip, path, "m_LocalRotation.y", cy, dt, loop);
                SetCurve(clip, path, "m_LocalRotation.z", cz, dt, loop);
                SetCurve(clip, path, "m_LocalRotation.w", cw, dt, loop);
            }

            if (kv.Key == "Hips")
            {
                Vector3 bind = kv.Value.localPosition;
                var px = new float[frames + 1]; var py = new float[frames + 1]; var pz = new float[frames + 1];
                for (int f = 0; f <= frames; f++)
                {
                    Vector3 p = bind + samples[f].hipOffset * scale;
                    px[f] = p.x; py[f] = p.y; pz[f] = p.z;
                }
                SetCurve(clip, path, "m_LocalPosition.x", px, dt, loop);
                SetCurve(clip, path, "m_LocalPosition.y", py, dt, loop);
                SetCurve(clip, path, "m_LocalPosition.z", pz, dt, loop);
            }
        }

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static bool IsConstant(float[] v)
    {
        for (int i = 1; i < v.Length; i++) if (Mathf.Abs(v[i] - v[0]) > 1e-5f) return false;
        return true;
    }

    // Courbe échantillonnée avec tangentes calculées (bouclage pris en compte)
    private static void SetCurve(AnimationClip clip, string path, string prop, float[] v, float dt, bool loop)
    {
        if (IsConstant(v))
        {
            float end = (v.Length - 1) * dt;
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop),
                AnimationCurve.Constant(0f, end, v[0]));
            return;
        }
        int n = v.Length;
        var keys = new Keyframe[n];
        for (int i = 0; i < n; i++)
        {
            float tangent;
            if (n < 3 || !loop && (i == 0 || i == n - 1)) tangent = 0f;
            else
            {
                // v[0] == v[n-1] en boucle : on saute le doublon
                float prev = i == 0 ? v[n - 2] : v[i - 1];
                float next = i == n - 1 ? v[1] : v[i + 1];
                tangent = (next - prev) / (2f * dt);
            }
            keys[i] = new Keyframe(i * dt, v[i], tangent, tangent);
        }
        var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), prop);
        AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
    }

    // ==================================================================
    private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips,
        float walkSpeed, float runSpeed, float crouchSpeed)
    {
        string path = OutDir + "/CharacterRig.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
        ctrl.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;

        var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree locoTree, 0);
        locoTree.blendType = BlendTreeType.Simple1D;
        locoTree.blendParameter = "Speed";
        locoTree.useAutomaticThresholds = false;
        locoTree.AddChild(clips["Idle"], 0f);
        locoTree.AddChild(clips["Walk"], walkSpeed);
        locoTree.AddChild(clips["Run"], runSpeed);

        var crouch = ctrl.CreateBlendTreeInController("Crouch", out BlendTree crouchTree, 0);
        crouchTree.blendType = BlendTreeType.Simple1D;
        crouchTree.blendParameter = "Speed";
        crouchTree.useAutomaticThresholds = false;
        crouchTree.AddChild(clips["CrouchIdle"], 0f);
        crouchTree.AddChild(clips["CrouchWalk"], crouchSpeed);

        var jump = sm.AddState("Jump");
        jump.motion = clips["Jump"];
        var fall = sm.AddState("Fall");
        fall.motion = clips["Fall"];

        sm.defaultState = loco;

        // L'ordre compte : le saut est testé avant la chute
        AddTransition(loco, jump, 0.08f).AddCondition(AnimatorConditionMode.If, 0, "Jump");
        AddTransition(loco, crouch, 0.2f).AddCondition(AnimatorConditionMode.If, 0, "Crouch");
        AddTransition(loco, fall, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");

        AddTransition(crouch, loco, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0, "Crouch");
        AddTransition(crouch, fall, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");

        var jumpToFall = AddTransition(jump, fall, 0.25f);
        jumpToFall.hasExitTime = true;
        jumpToFall.exitTime = 0.9f;
        AddTransition(jump, loco, 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Grounded");

        AddTransition(fall, loco, 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Grounded");

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    private static AnimatorStateTransition AddTransition(AnimatorState from, AnimatorState to, float duration)
    {
        var tr = from.AddTransition(to);
        tr.hasExitTime = false;
        tr.duration = duration;
        tr.hasFixedDuration = true;
        tr.canTransitionToSelf = false;
        return tr;
    }

    // ==================================================================
    // Remplace le modèle statique par le rig dans le prefab Player et/ou la scène
    // ==================================================================
    private static string InstallOnPlayer(GameObject rigPrefab, float height)
    {
        var report = new List<string>();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            var pc = contents.GetComponent<PlayerController>();
            if (pc != null && ConfigurePlayer(pc, rigPrefab, height))
            {
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                report.Add("Prefab Player mis à jour.");
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }

        foreach (var pc in ScenePlayers())
        {
            // Instance du prefab Player : on retire les anciennes valeurs locales (taille, rig…)
            // pour qu'elle reprenne celles du prefab mis à jour.
            if (PrefabUtility.IsPartOfPrefabInstance(pc))
            {
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(pc) == PlayerPrefabPath)
                {
                    RevertRigOverrides(pc);
                    EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
                    report.Add($"Joueur « {pc.name} » de la scène resynchronisé avec le prefab.");
                }
                continue;
            }
            // Joueur qui n'est pas une instance du prefab : on le modifie directement
            if (ConfigurePlayer(pc, rigPrefab, height))
            {
                EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
                report.Add($"Joueur « {pc.name} » de la scène mis à jour.");
            }
        }

        return report.Count > 0
            ? string.Join("\n", report)
            : "Aucun joueur trouvé : utilise AAR > Créer le joueur dans la scène (il utilisera le rig).";
    }

    /// <summary>Met le rig animé dans le conteneur "Model" du joueur et règle le contrôleur.</summary>
    public static bool ConfigurePlayer(PlayerController pc, GameObject rigPrefab, float height)
    {
        var so = new SerializedObject(pc);
        var modelProp = so.FindProperty("model");
        var modelRoot = modelProp.objectReferenceValue as Transform;
        if (modelRoot == null)
        {
            modelRoot = new GameObject("Model").transform;
            modelRoot.SetParent(pc.transform, false);
            modelProp.objectReferenceValue = modelRoot;
        }

        for (int i = modelRoot.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(modelRoot.GetChild(i).gameObject);

        var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, modelRoot);
        rig.transform.localPosition = Vector3.zero;
        rig.transform.localRotation = Quaternion.identity;
        rig.transform.localScale = Vector3.one;
        modelRoot.localPosition = Vector3.zero;
        modelRoot.localScale = Vector3.one;

        so.FindProperty("animator").objectReferenceValue = rig.GetComponent<Animator>();
        so.FindProperty("squashModelWhenCrouching").boolValue = false;
        so.FindProperty("standingHeight").floatValue = height;
        // Hauteur réelle de la pose accroupie générée (≈ 84 % de la hauteur debout)
        so.FindProperty("crouchHeight").floatValue = height * 0.84f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Le joueur doit rester à l'échelle 1 : c'est le rig qui porte la taille du personnage
        pc.transform.localScale = Vector3.one;

        var cc = pc.GetComponent<CharacterController>();
        if (cc != null)
        {
            float s = height / RefHeight;
            cc.height = height;
            cc.center = new Vector3(0f, height / 2f, 0f);
            cc.radius = 0.3f * s;          // largeur du buste, sans les bras ni le casque
            cc.stepOffset = 0.3f * s;
            cc.skinWidth = 0.02f * s;
            // Le CharacterController flotte de skinWidth au-dessus du sol : on descend le modèle d'autant
            modelRoot.localPosition = new Vector3(0f, -cc.skinWidth, 0f);
        }
        var pivot = so.FindProperty("cameraPivot").objectReferenceValue as Transform;
        if (pivot != null) pivot.localPosition = new Vector3(0f, height * 0.9f, 0f);
        return true;
    }

    private static void RevertRigOverrides(PlayerController pc)
    {
        var so = new SerializedObject(pc);
        foreach (string prop in new[] { "model", "animator", "standingHeight", "crouchHeight", "squashModelWhenCrouching", "cameraPivot" })
        {
            var sp = so.FindProperty(prop);
            if (sp != null && sp.prefabOverride) PrefabUtility.RevertPropertyOverride(sp, InteractionMode.AutomatedAction);
        }
        var cc = pc.GetComponent<CharacterController>();
        if (cc != null && PrefabUtility.IsPartOfPrefabInstance(cc)) PrefabUtility.RevertObjectOverride(cc, InteractionMode.AutomatedAction);
        var scale = new SerializedObject(pc.transform).FindProperty("m_LocalScale");
        if (scale != null && scale.prefabOverride) PrefabUtility.RevertPropertyOverride(scale, InteractionMode.AutomatedAction);
        var pivot = new SerializedObject(pc).FindProperty("cameraPivot").objectReferenceValue as Transform;
        if (pivot != null && PrefabUtility.IsPartOfPrefabInstance(pivot)) PrefabUtility.RevertObjectOverride(pivot, InteractionMode.AutomatedAction);
        var model = new SerializedObject(pc).FindProperty("model").objectReferenceValue as Transform;
        if (model != null && PrefabUtility.IsPartOfPrefabInstance(model)) PrefabUtility.RevertObjectOverride(model, InteractionMode.AutomatedAction);
    }

    private static List<PlayerController> ScenePlayers()
    {
        var list = new List<PlayerController>();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var go in scene.GetRootGameObjects())
                list.AddRange(go.GetComponentsInChildren<PlayerController>(true));
        }
        return list;
    }

    /// <summary>Hauteur du personnage riggé (taille de son mesh).</summary>
    public static float RigHeight(GameObject rigPrefab)
    {
        var smr = rigPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
        return smr != null && smr.sharedMesh != null ? smr.sharedMesh.bounds.size.y : RefHeight;
    }

    // ==================================================================
    private static void EnsureReadable()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    private static Bounds BoundsOf(Vector3[] v)
    {
        var b = new Bounds(v[0], Vector3.zero);
        for (int i = 1; i < v.Length; i++) b.Encapsulate(v[i]);
        return b;
    }

    private static float MeanZ(Vector3[] v, float yMin, float yMax)
    {
        float sum = 0f; int n = 0;
        foreach (var p in v) if (p.y >= yMin && p.y <= yMax) { sum += p.z; n++; }
        return n > 0 ? sum / n : 0f;
    }
}

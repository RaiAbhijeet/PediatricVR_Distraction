#if UNITY_EDITOR
// ============================================================
// PediatricVR — FishSetupTool.cs  [EDITOR ONLY]
//
// One-click tool to:
//   1. Create prefabs from fish FBX and turtle GLB models
//   2. If the model has embedded animation clips, wire them
//      into an AnimatorController automatically
//   3. Assign Fish1/2/3 prefabs to FishSchool_01/02/03
//   4. Instantiate SeaTurtle prefab as a child of GuidedCreature
//
// Usage: Unity menu bar → PediatricVR → Setup Fish & Turtle Prefabs
//
// Requires: Main.unity must be open in the Editor.
// ============================================================

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PediatricVR.Editor
{
    public static class FishSetupTool
    {
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string AnimsFolder   = "Assets/Animations";

        // ── Entry point ───────────────────────────────────────────

        [MenuItem("PediatricVR/Setup Fish & Turtle Prefabs", false, 10)]
        static void SetupAll()
        {
            AssetDatabase.Refresh();

            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets", "Animations");

            // Create prefabs from models.
            // Turtle uses sea_turtle2.glb (4.4 MB) — sea_turtle1.glb is 16 MB and too heavy.
            // If the SeaTurtle prefab was built from the heavy model, delete and recreate it.
            ForceDeletePrefabIfHeavy("SeaTurtle", "Assets/Models/Turtle/sea_turtle1.glb");

            GameObject fish1  = CreatePrefab("Assets/Models/Fish/Fish1.fbx",         "Fish1");
            GameObject fish2  = CreatePrefab("Assets/Models/Fish/Fish2.fbx",         "Fish2");
            GameObject fish3  = CreatePrefab("Assets/Models/Fish/Fish3.fbx",         "Fish3");
            GameObject turtle = CreatePrefab("Assets/Models/Turtle/sea_turtle2.glb", "SeaTurtle");

            // Wire into scene
            AssignToFishSchool("FishSchool_01", fish1);
            AssignToFishSchool("FishSchool_02", fish2);
            AssignToFishSchool("FishSchool_03", fish3);
            PlaceTurtleInScene(turtle);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Setup Complete",
                "Prefabs created and scene references wired.\n\n" +
                "Check the Console for details.\n\n" +
                "NOTE: If fish appear too large or small in Play mode,\n" +
                "select the prefab → Inspector → Transform → Scale.",
                "OK");
        }

        // ── Heavy-prefab cleanup ──────────────────────────────────

        // Deletes an existing prefab so it can be rebuilt from a lighter source model.
        // Only deletes when the prefab's source model matches heavyModelPath.
        static void ForceDeletePrefabIfHeavy(string prefabName, string heavyModelPath)
        {
            string prefabPath = $"{PrefabsFolder}/{prefabName}.prefab";
            if (!File.Exists(prefabPath)) return;

            // Check if the prefab was derived from the heavy model
            string[] deps = AssetDatabase.GetDependencies(prefabPath, recursive: false);
            bool usesHeavyModel = deps.Any(d =>
                d.Replace('\\', '/').ToLower() == heavyModelPath.ToLower());

            if (!usesHeavyModel) return;

            AssetDatabase.DeleteAsset(prefabPath);
            Debug.Log($"[FishSetup] Deleted heavy prefab {prefabPath} — will rebuild from lighter model.");
        }

        // ── Folder helper ─────────────────────────────────────────

        static void EnsureFolder(string parent, string folderName)
        {
            string fullPath = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(fullPath))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        // ── Prefab creation ───────────────────────────────────────

        static GameObject CreatePrefab(string modelPath, string prefabName)
        {
            string prefabPath = $"{PrefabsFolder}/{prefabName}.prefab";

            // Return existing prefab without overwriting it
            if (File.Exists(prefabPath))
            {
                Debug.Log($"[FishSetup] Prefab already exists, reusing: {prefabPath}");
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                Debug.LogWarning($"[FishSetup] Model not found at: {modelPath} — skipping.");
                return null;
            }

            // Find embedded animation clips (Unity internal clips start with __)
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__"))
                .ToArray();

            // Instantiate temporarily to build the prefab
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            instance.name = prefabName;

            if (clips.Length > 0)
            {
                // Find existing Animator on the model (most rigged FBX have one)
                Animator animator = instance.GetComponentInChildren<Animator>();
                if (animator == null)
                    animator = instance.AddComponent<Animator>();

                string controllerPath = $"{AnimsFolder}/{prefabName}_Swim.controller";
                animator.runtimeAnimatorController = BuildController(controllerPath, clips[0]);
                Debug.Log($"[FishSetup] AnimatorController → {controllerPath} (clip: {clips[0].name})");
            }
            else
            {
                Debug.Log($"[FishSetup] No embedded animations in {Path.GetFileName(modelPath)} — FishSchool code bobbing only.");
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);

            Debug.Log($"[FishSetup] ✓ Created prefab: {prefabPath}");
            return prefab;
        }

        // ── AnimatorController builder ────────────────────────────

        static RuntimeAnimatorController BuildController(string path, AnimationClip clip)
        {
            // Return existing controller if already built
            if (File.Exists(path))
                return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);

            var controller  = AnimatorController.CreateAnimatorControllerAtPath(path);
            var stateMachine = controller.layers[0].stateMachine;
            AnimatorState state = stateMachine.AddState("Swim");
            state.motion = clip;
            stateMachine.defaultState = state;

            // Loop setting lives on the clip itself (set via FBX import Animation tab).
            // If the clip does not loop, open the model in Project → Animation tab
            // and enable "Loop Time" on the clip.

            return controller;
        }

        // ── Scene wiring ──────────────────────────────────────────

        static void AssignToFishSchool(string schoolName, GameObject prefab)
        {
            if (prefab == null) return;

            FishSchool school = Object.FindObjectsByType<FishSchool>(FindObjectsSortMode.None)
                .FirstOrDefault(s => s.gameObject.name == schoolName);

            if (school == null)
            {
                Debug.LogWarning($"[FishSetup] '{schoolName}' not found — open Main.unity before running this tool.");
                return;
            }

            var so = new SerializedObject(school);
            so.FindProperty("fishPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(school);

            Debug.Log($"[FishSetup] ✓ {prefab.name} → {schoolName}");
        }

        static void PlaceTurtleInScene(GameObject turtlePrefab)
        {
            if (turtlePrefab == null) return;

            GameObject guidedCreature = GameObject.Find("GuidedCreature");
            if (guidedCreature == null)
            {
                Debug.LogWarning("[FishSetup] 'GuidedCreature' not found in open scene — place turtle manually later.");
                return;
            }

            // Remove any previously placed turtle to avoid duplicates
            Transform existing = guidedCreature.transform.Find("SeaTurtle");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            GameObject turtleInScene = (GameObject)PrefabUtility.InstantiatePrefab(turtlePrefab, guidedCreature.transform);
            turtleInScene.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            EditorUtility.SetDirty(guidedCreature);

            Debug.Log("[FishSetup] ✓ SeaTurtle placed inside GuidedCreature.");
        }
    }
}
#endif

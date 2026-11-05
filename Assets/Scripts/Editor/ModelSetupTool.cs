#if UNITY_EDITOR
// ============================================================
// PediatricVR — ModelSetupTool.cs  [EDITOR ONLY]
//
// Populates the underwater scene with optimised creature and
// environment objects for Quest 3S (mobile GPU budget).
//
// Excluded from scene (too heavy for Quest 3S):
//   alien_flowers.glb  46 MB — removed
//   Starfish.fbx        7.7 MB — removed
//
// Usage: PediatricVR → Setup Underwater Scene (New Models)
// Requires: Main.unity must be open in the Editor.
// ============================================================

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PediatricVR.Editor
{
    public static class ModelSetupTool
    {
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string AnimsFolder   = "Assets/Animations";

        [MenuItem("PediatricVR/Setup Underwater Scene (New Models)", false, 11)]
        static void SetupAll()
        {
            AssetDatabase.Refresh();
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets", "Animations");

            // ── Create prefabs (lightweight models only) ────────
            // Heavy models excluded: AlienFlowers (46 MB), Starfish (7.7 MB)
            var dolphin   = MakePrefab("Assets/Models/Fish/Dolphin.fbx",                          "Dolphin");
            var shark     = MakePrefab("Assets/Models/Fish/Shark.fbx",                            "Shark");
            var whale     = MakePrefab("Assets/Models/Fish/Whale.fbx",                            "Whale");
            var manta     = MakePrefab("Assets/Models/Fish/Manta ray.fbx",                        "MantaRay");
            var alienEggs = MakePrefab("Assets/Models/Environment/AlienEggs/source/fun 01.fbx",   "AlienEggs");
            var iceberg   = MakePrefab("Assets/Models/Environment/Iceberg/Iceberg.fbx",           "Iceberg");

            // ── Remove previously placed heavy props if present ─
            CleanupHeavyProps();

            // ── Locate underwaterRoot ───────────────────────────
            GameObject underwaterRoot = FindUnderwaterRoot();
            if (underwaterRoot == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Could not find 'underwaterRoot' in the scene.\n" +
                    "Open Main.unity and try again.", "OK");
                return;
            }

            // ── Swimming creature schools ───────────────────────
            // Fish count kept low (1–2 per species) for Quest 3S GPU budget.
            var dolphinSchool = MakeFishSchool("Dolphin_School", dolphin, underwaterRoot.transform,
                pos:          new Vector3( 5f, 0f,  6f),
                count:        2,
                speed:        0.50f,
                schoolRadius: 2.0f,
                wanderBounds: new Vector3(14f, 4f, 14f));

            var sharkSchool = MakeFishSchool("Shark_School", shark, underwaterRoot.transform,
                pos:          new Vector3(-8f, -1f, 10f),
                count:        1,
                speed:        0.35f,
                schoolRadius: 1.0f,
                wanderBounds: new Vector3(16f, 3f, 16f));

            var whaleSchool = MakeFishSchool("Whale_School", whale, underwaterRoot.transform,
                pos:          new Vector3( 0f, -1f, 13f),
                count:        1,
                speed:        0.20f,
                schoolRadius: 1.0f,
                wanderBounds: new Vector3(18f, 3f, 18f));

            var mantaSchool = MakeFishSchool("MantaRay_School", manta, underwaterRoot.transform,
                pos:          new Vector3( 4f, 2f, -5f),
                count:        2,
                speed:        0.40f,
                schoolRadius: 2.5f,
                wanderBounds: new Vector3(14f, 3f, 14f));

            RegisterSchoolsWithController(dolphinSchool, sharkSchool, whaleSchool, mantaSchool);

            // ── Static environment props (minimal count) ────────
            Transform propRoot = GetOrCreateChild(underwaterRoot.transform, "EnvironmentProps");

            // 1 alien egg cluster — 4 MB but only 1 instance
            PlaceProps(alienEggs, propRoot, new[]
            {
                new Vector3(-2f, -4f, 5f),
            });

            // 1 iceberg in background — 4.5 MB, 1 instance
            PlaceProps(iceberg, propRoot, new[]
            {
                new Vector3(0f, -3f, -10f),
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Scene Optimised for Quest 3S",
                "Swimming creatures added:\n" +
                "  Dolphin ×2\n" +
                "  Shark ×1  (far left-front)\n" +
                "  Whale ×1  (directly ahead, far)\n" +
                "  Manta ray ×2\n\n" +
                "Minimal props added:\n" +
                "  Alien egg cluster ×1\n" +
                "  Iceberg ×1  (background)\n\n" +
                "Removed from scene (were too heavy):\n" +
                "  AlienFlowers  (46 MB — removed)\n" +
                "  Starfish       (7.7 MB — removed)\n\n" +
                "If creatures still look too large, select the prefab in\n" +
                "Assets/Prefabs and reduce its Scale in the Inspector.",
                "OK");
        }

        // ── Remove previously-placed heavy props from the scene ─

        static void CleanupHeavyProps()
        {
            // Names placed by the previous (unoptimised) run of this tool
            string[] heavyNames =
            {
                "AlienFlowers_01", "AlienFlowers_02", "AlienFlowers_03",
                "AlienFlowers_04", "AlienFlowers_05",
                "Starfish_01", "Starfish_02", "Starfish_03", "Starfish_04",
                // Extra AlienEggs and Iceberg instances beyond what we keep
                "AlienEggs_02", "AlienEggs_03", "AlienEggs_04",
                "Iceberg_02",
            };

            int removed = 0;
            foreach (string name in heavyNames)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                Object.DestroyImmediate(go);
                removed++;
                Debug.Log($"[ModelSetup] Removed heavy prop: {name}");
            }

            if (removed > 0)
                Debug.Log($"[ModelSetup] Cleaned up {removed} heavy props from previous tool run.");
        }

        // ═══════════════════════════════════════════════════════
        // Prefab creation
        // ═══════════════════════════════════════════════════════

        static GameObject MakePrefab(string modelPath, string prefabName)
        {
            string prefabPath = $"{PrefabsFolder}/{prefabName}.prefab";

            if (File.Exists(prefabPath))
            {
                Debug.Log($"[ModelSetup] Reusing existing prefab: {prefabPath}");
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                Debug.LogWarning($"[ModelSetup] Model not found: {modelPath} — skipping.");
                return null;
            }

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__"))
                .ToArray();

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            instance.name = prefabName;

            if (clips.Length > 0)
            {
                Animator animator = instance.GetComponentInChildren<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();

                string ctrlPath = $"{AnimsFolder}/{prefabName}_Anim.controller";
                animator.runtimeAnimatorController = BuildController(ctrlPath, clips[0]);
                Debug.Log($"[ModelSetup] Wired animation '{clips[0].name}' → {ctrlPath}");
            }
            else
            {
                Debug.Log($"[ModelSetup] No embedded animations in {prefabName} — bob-only movement.");
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            Debug.Log($"[ModelSetup] Created prefab: {prefabPath}");
            return prefab;
        }

        static RuntimeAnimatorController BuildController(string path, AnimationClip clip)
        {
            if (File.Exists(path))
                return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorState state = ctrl.layers[0].stateMachine.AddState(clip.name);
            state.motion = clip;
            ctrl.layers[0].stateMachine.defaultState = state;
            return ctrl;
        }

        // ═══════════════════════════════════════════════════════
        // FishSchool creation
        // ═══════════════════════════════════════════════════════

        static FishSchool MakeFishSchool(string name, GameObject prefab, Transform parent,
            Vector3 pos, int count, float speed, float schoolRadius, Vector3 wanderBounds)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[ModelSetup] Skipping {name} — prefab missing.");
                return null;
            }

            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Debug.Log($"[ModelSetup] {name} already in scene — reusing.");
                return existing.GetComponent<FishSchool>();
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;

            var school = go.AddComponent<FishSchool>();
            var so = new SerializedObject(school);
            SetProp(so, "fishPrefab",    prefab);
            SetProp(so, "fishCount",     count);
            SetProp(so, "swimSpeed",     speed);
            SetProp(so, "schoolRadius",  schoolRadius);
            SetProp(so, "wanderBounds",  wanderBounds);
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(go);
            return school;
        }

        static void RegisterSchoolsWithController(params FishSchool[] schools)
        {
            UnderwaterController ctrl = Object.FindFirstObjectByType<UnderwaterController>();
            if (ctrl == null)
            {
                Debug.LogWarning("[ModelSetup] UnderwaterController not found — new FishSchools won't auto-hide.");
                return;
            }

            var so   = new SerializedObject(ctrl);
            var list = so.FindProperty("fishSchools");
            if (list == null || !list.isArray) return;

            foreach (var school in schools)
            {
                if (school == null) continue;

                bool alreadyIn = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == school)
                    { alreadyIn = true; break; }
                }
                if (alreadyIn) continue;

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = school;
                Debug.Log($"[ModelSetup] Registered {school.name} with UnderwaterController.");
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ctrl);
        }

        // ═══════════════════════════════════════════════════════
        // Static props placement
        // ═══════════════════════════════════════════════════════

        static void PlaceProps(GameObject prefab, Transform parent, Vector3[] positions)
        {
            if (prefab == null) return;

            for (int i = 0; i < positions.Length; i++)
            {
                string propName = $"{prefab.name}_{i + 1:00}";
                if (parent.Find(propName) != null) continue;

                GameObject prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                prop.name = propName;
                prop.transform.localPosition = positions[i];
                prop.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                EditorUtility.SetDirty(prop);
            }
        }

        // ═══════════════════════════════════════════════════════
        // Utilities
        // ═══════════════════════════════════════════════════════

        static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            return go.transform;
        }

        static GameObject FindUnderwaterRoot()
        {
            var byName = GameObject.Find("underwaterRoot");
            if (byName != null) return byName;

            var ctrl = Object.FindFirstObjectByType<UnderwaterController>();
            if (ctrl == null) return null;
            var so   = new SerializedObject(ctrl);
            var prop = so.FindProperty("underwaterRoot");
            return prop?.objectReferenceValue as GameObject;
        }

        static void SetProp(SerializedObject so, string field, object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) return;
            switch (value)
            {
                case GameObject go: p.objectReferenceValue = go;  break;
                case int     i:     p.intValue    = i;            break;
                case float   f:     p.floatValue  = f;            break;
                case Vector3 v:     p.vector3Value = v;           break;
            }
        }
    }
}
#endif

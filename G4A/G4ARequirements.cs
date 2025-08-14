using System;
using System.Linq;
using UnityEngine;

#if ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

namespace G4A
{
    [CreateAssetMenu(fileName = "G4ARequirements", menuName = "G4A/Requirements Config", order = 1)]
    public class G4ARequirements : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Runtime type name (e.g., Game4AutomationController)")]
            public string requiredTypeName;

            [Tooltip("Addressables key (preferred when available)")]
            public string addressKey;

            [Tooltip("Resources.Load path (e.g., Prefabs/Controllers/MainController)")]
            public string resourcesPath;

            [Tooltip("Editor fallback: Asset path (e.g., Assets/game4automation/Prefabs/MainController.prefab)")]
            public string editorAssetPath;

            [Tooltip("Spawn inactive first to avoid Awake/OnEnable timing issues")]
            public bool spawnInactive = true;
        }

        public Entry[] entries = Array.Empty<Entry>();
        public Transform parentForSpawned;

        public void EnsureAllSync()
        {
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.requiredTypeName))
                    continue;

                var type = FindType(e.requiredTypeName);
                if (type != null && FindObjectOfType(type) != null)
                    continue; // already present

                var go = TrySpawnSync(e);
                if (go == null)
                {
                    Debug.LogError($"[G4ARequirements] Failed to spawn '{e.requiredTypeName}' " +
                                   $"(addr='{e.addressKey}', res='{e.resourcesPath}', editor='{e.editorAssetPath}')");
                }
            }

            // Activate all spawned inactive objects last
            foreach (var e in entries)
            {
                if (e == null || !e.spawnInactive) continue;
                var type = FindType(e.requiredTypeName);
                if (type == null) continue;
                foreach (var comp in FindObjectsOfType(type, includeInactive: true))
                {
                    var c = comp as Component;
                    if (c != null && c.gameObject != null && !c.gameObject.activeSelf)
                        c.gameObject.SetActive(true);
                }
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        private GameObject TrySpawnSync(Entry e)
        {
            GameObject prefab = null;

#if ADDRESSABLES
            if (!string.IsNullOrWhiteSpace(e.addressKey))
            {
                try
                {
                    var h = Addressables.LoadAssetAsync<GameObject>(e.addressKey);
                    prefab = h.WaitForCompletion();
                    Addressables.Release(h);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[G4ARequirements] Addressables load failed key='{e.addressKey}': {ex.Message}");
                }
            }
#endif
            if (prefab == null && !string.IsNullOrWhiteSpace(e.resourcesPath))
            {
                try { prefab = Resources.Load<GameObject>(e.resourcesPath); }
                catch (Exception ex) { Debug.LogWarning($"[G4ARequirements] Resources load failed '{e.resourcesPath}': {ex.Message}"); }
            }

#if UNITY_EDITOR
            if (prefab == null && !string.IsNullOrWhiteSpace(e.editorAssetPath))
            {
                try { prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(e.editorAssetPath); }
                catch (Exception ex) { Debug.LogWarning($"[G4ARequirements] AssetDatabase load failed '{e.editorAssetPath}': {ex.Message}"); }
            }
#endif
            if (prefab == null) return null;

            var go = Instantiate(prefab);
            go.name = $"[G4A]{prefab.name}";
            if (e.spawnInactive) go.SetActive(false);
            if (parentForSpawned != null) go.transform.SetParent(parentForSpawned, true);
            return go;
        }
    }
}

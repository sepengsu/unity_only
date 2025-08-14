using System;
using System.Linq;
using UnityEngine;

#if ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace Helpers
{
    /// <summary>
    /// Game4Automation 컨트롤러 자동 보장 유틸.
    /// - 프리팹(Addressables/Editor/Registry) 우선 시도.
    /// - 실패 시 AddComponent 폴백으로 컨트롤러 생성 (안전 조치 포함).
    /// </summary>
    public static class G4AHelper
    {
        private const string ControllerTypeName = "game4automation.Game4AutomationController";

        private static readonly string[] AddressKeys =
        {
            "Game4AutomationController",
            "game4automation/Game4AutomationController", 
            "G4A/Game4AutomationController"
        };

        private static readonly string[] EditorControllerPathCandidates =
        {
            "Assets/game4automation/Prefabs/Game4AutomationController.prefab",
            "Assets/game4automation/private/Prefabs/Game4AutomationController.prefab",
            "Assets/game4automation/Prefabs/G4AController.prefab", 
            "Assets/game4automation/Prefabs/Controller.prefab",
            // 추가 후보 경로들
            "Assets/game4automation/Game4AutomationController.prefab",
            "Assets/game4automation/Controller.prefab"
        };

        /// <summary>
        /// 프리팹/키워드로 보장 필요 여부 감지해서 보장 시도.
        /// </summary>
        public static void AutoEnsureFor(string prefabPathOrNull, string addressKeyOrNull)
        {
            if (!LooksLikeG4A(prefabPathOrNull) && !LooksLikeG4A(addressKeyOrNull))
                return;

            bool success = EnsureControllerPresent(out string source, out string message);
            
            if (success)
            {
                Debug.Log($"[G4AHelper] Controller ensured via: {source}");
            }
            else
            {
                Debug.LogWarning($"[G4AHelper] Controller ensure failed: {message}");
            }
        }

        /// <summary>
        /// 컨트롤러 보장. 성공 시 true, 실패 시 false.
        /// source: "Addressables" | "Registry-Editor" | "EditorPath" | "AddComponent"
        /// </summary>
        public static bool EnsureControllerPresent(out string source, out string message)
        {
            source = null;
            message = null;

            if (IsControllerPresent())
            {
                source = "Exists";
                return true;
            }

            // 1) Addressables
#if ADDRESSABLES
            foreach (var key in AddressKeys)
            {
                if (TryInstantiateAddressable(key, out var go))
                {
                    go.name = $"[G4A]{go.name}";
                    source = "Addressables";
                    return true;
                }
            }
#endif

            // 2) Registry → EditorPath
            PrefabRegistry.EnsureLoaded();
            var reg = PrefabRegistry.FindByName("Game4AutomationController")
                   ?? PrefabRegistry.FindByAddress("Game4AutomationController")
                   ?? PrefabRegistry.FindByAddress("G4AController");
#if UNITY_EDITOR
            if (reg != null && !string.IsNullOrEmpty(reg.assetPath))
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(reg.assetPath);
                if (prefab != null)
                {
                    var g = UnityEngine.Object.Instantiate(prefab);
                    g.name = $"[G4A]{prefab.name}";
                    source = "Registry-Editor";
                    return true;
                }
            }
#endif

            // 3) Editor 후보 경로 (verbose 로그 제거)
#if UNITY_EDITOR
            foreach (var path in EditorControllerPathCandidates)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var g = UnityEngine.Object.Instantiate(prefab);
                    g.name = $"[G4A]{prefab.name}";
                    source = "EditorPath";
                    return true;
                }
            }
#endif

            // 4) AddComponent 폴백 (안전 조치 포함)
            var ctrlType = FindType(ControllerTypeName);
            if (ctrlType != null)
            {
                try
                {
                    var controllerGO = new GameObject("[G4A]Game4AutomationController");
                    
                    // GameObject를 비활성화하고 컴포넌트 추가
                    controllerGO.SetActive(false);
                    var component = controllerGO.AddComponent(ctrlType);
                    
                    // 안전 조치: 필요한 필드들 초기화
                    InitializeControllerSafely(component);
                    
                    // 컴포넌트 추가 완료 후 활성화 시도
                    try
                    {
                        controllerGO.SetActive(true);
                    }
                    catch (Exception activateEx)
                    {
                        Debug.LogWarning($"[G4AHelper] Controller created but activation failed: {activateEx.Message}");
                        // 활성화 실패해도 컨트롤러는 존재하므로 성공으로 간주
                    }
                    
                    source = "AddComponent";
                    message = "Controller created via AddComponent with safety initialization.";
                    return true;
                }
                catch (Exception ex)
                {
                    message = $"AddComponent fallback failed: {ex.Message}";
                    return false;
                }
            }

            message = "Could not auto-insert Game4AutomationController (type not found).";
            return false;
        }

        /// <summary>
        /// 실제 Game4Automation 프리팹 경로 찾기 (디버깅용)
        /// </summary>
        public static void FindActualControllerPath()
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("Game4AutomationController t:GameObject");
            Debug.Log($"[G4AHelper] Found {guids.Length} Game4AutomationController assets:");
            
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var hasController = asset.GetComponent(FindType(ControllerTypeName)) != null;
                Debug.Log($"[G4AHelper] Path: {path}, HasController: {hasController}");
            }
#else
            Debug.Log("[G4AHelper] FindActualControllerPath only works in editor.");
#endif
        }

        /// <summary>
        /// AddComponent로 생성된 컨트롤러의 안전 초기화
        /// </summary>
        private static void InitializeControllerSafely(Component controller)
        {
            try
            {
                var type = controller.GetType();
                
                // 기본적으로 null이 될 수 있는 필드들을 빈 배열로 초기화
                var fields = type.GetFields(System.Reflection.BindingFlags.Public | 
                                          System.Reflection.BindingFlags.NonPublic | 
                                          System.Reflection.BindingFlags.Instance);
                
                foreach (var field in fields)
                {
                    if (field.FieldType.IsArray && field.GetValue(controller) == null)
                    {
                        var emptyArray = Array.CreateInstance(field.FieldType.GetElementType(), 0);
                        field.SetValue(controller, emptyArray);
                    }
                    else if (field.FieldType.IsGenericType && 
                             field.FieldType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>) &&
                             field.GetValue(controller) == null)
                    {
                        var listType = field.FieldType;
                        var emptyList = Activator.CreateInstance(listType);
                        field.SetValue(controller, emptyList);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[G4AHelper] Safety initialization failed (non-critical): {ex.Message}");
            }
        }

        // ---------- helpers ----------
        private static bool LooksLikeG4A(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            s = s.ToLowerInvariant();
            if (s.Contains("game4automation") || s.Contains("realvirtual") || s.Contains("real-virtual"))
                return true;
            string[] robotHints = { "irb", "kuka", "fanuc", "ur", "abb", "yaskawa", "mitsubishi", "staubli" };
            return robotHints.Any(h => s.Contains(h));
        }

        private static bool IsControllerPresent()
        {
            var t = FindType(ControllerTypeName);
            return t != null && UnityEngine.Object.FindObjectOfType(t) != null;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

#if ADDRESSABLES
        private static bool TryInstantiateAddressable(string key, out GameObject go)
        {
            go = null;
            try
            {
                var locH = Addressables.LoadResourceLocationsAsync(key, typeof(GameObject));
                var locs = locH.WaitForCompletion();
                Addressables.Release(locH);
                if (locs == null || locs.Count == 0) return false;

                var inst = Addressables.InstantiateAsync(key);
                var _ = inst.WaitForCompletion();
                if (inst.Status == AsyncOperationStatus.Succeeded)
                {
                    go = inst.Result;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[G4AHelper] Addressables ensure failed '{key}': {ex.Message}");
            }
            return false;
        }
#endif
    }
}
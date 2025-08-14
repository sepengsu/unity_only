using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Networking;
#endif

[Serializable] public class PrefabItem
{
    public string name;
    public string addressKey;
    public string assetPath;
    public string guid;
    public string category;
    public string description;
    public string[] tags;
}

[Serializable] public class PrefabCatalog
{
    public PrefabItem[] items;
}

public static class PrefabRegistry
{
    private static bool _loaded;
    private static Dictionary<string, PrefabItem> _byAssetPath; // key: normalized assetPath
    private static Dictionary<string, PrefabItem> _byName;
    private static Dictionary<string, PrefabItem> _byAddress;

    /// <summary>
    /// 경로 정규화: 슬래시 통일 + 소문자화
    /// </summary>
    private static string NormalizePath(string p)
    {
        if (string.IsNullOrEmpty(p)) return p;
        p = p.Replace('\\', '/');
        return p.ToLowerInvariant();
    }

    /// <summary>
    /// 카탈로그 로드(한 번만). 실패 시에도 내부 딕셔너리는 빈 상태로 초기화.
    /// </summary>
    public static void EnsureLoaded()
    {
        if (_loaded) return;

        try
        {
            string path = Path.Combine(Application.streamingAssetsPath, "prefabs.json");
            string json = LoadTextSync(path);

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning($"[PrefabRegistry] prefabs.json not found or empty at: {path}");
                InitEmpty();
                return;
            }

            var catalog = JsonUtility.FromJson<PrefabCatalog>(json);
            var items = catalog?.items ?? Array.Empty<PrefabItem>();

            _byAssetPath = items
                .Where(i => !string.IsNullOrEmpty(i.assetPath))
                .GroupBy(i => NormalizePath(i.assetPath))
                .ToDictionary(g => g.Key, g => g.First());

            _byName = items
                .Where(i => !string.IsNullOrEmpty(i.name))
                .GroupBy(i => i.name)
                .ToDictionary(g => g.Key, g => g.First());

            _byAddress = items
                .Where(i => !string.IsNullOrEmpty(i.addressKey))
                .GroupBy(i => i.addressKey)
                .ToDictionary(g => g.Key, g => g.First());

            _loaded = true;
            Debug.Log($"[PrefabRegistry] Loaded {items.Length} entries from prefabs.json");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PrefabRegistry] Load failed: {e}");
            InitEmpty();
        }
    }

    /// <summary>
    /// 강제 재로드 (개발 중 prefabs.json 갱신 즉시 반영)
    /// </summary>
    public static void Reload()
    {
        _loaded = false;
        EnsureLoaded();
    }

    public static PrefabItem FindByAssetPath(string assetPath)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(assetPath)) return null;
        var key = NormalizePath(assetPath);
        return _byAssetPath != null && _byAssetPath.TryGetValue(key, out var it) ? it : null;
    }

    public static PrefabItem FindByName(string name)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(name)) return null;
        return _byName != null && _byName.TryGetValue(name, out var it) ? it : null;
    }

    public static PrefabItem FindByAddress(string addressKey)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(addressKey)) return null;
        return _byAddress != null && _byAddress.TryGetValue(addressKey, out var it) ? it : null;
    }

    private static void InitEmpty()
    {
        _byAssetPath = new Dictionary<string, PrefabItem>();
        _byName = new Dictionary<string, PrefabItem>();
        _byAddress = new Dictionary<string, PrefabItem>();
        _loaded = true;
    }

    /// <summary>
    /// 플랫폼별 StreamingAssets 동기 로더
    /// </summary>
    private static string LoadTextSync(string path)
    {
#if UNITY_ANDROID
        // Android에선 StreamingAssets가 APK 내부라 File.ReadAllText 불가 → UnityWebRequest 사용
        using (var req = UnityWebRequest.Get(path))
        {
            var op = req.SendWebRequest();
            while (!op.isDone) { } // 메인 스레드에서 호출 권장
#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success) return null;
#else
            if (req.isNetworkError || req.isHttpError) return null;
#endif
            return req.downloadHandler.text;
        }
#else
        return File.Exists(path) ? File.ReadAllText(path) : null;
#endif
    }
}

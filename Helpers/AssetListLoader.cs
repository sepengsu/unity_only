using UnityEngine;
using System.Collections.Generic;

public class AssetListLoader : MonoBehaviour
{
    // JSON 구조는 BuildTimeAssetListGenerator의 것과 반드시 일치해야 합니다.
    [System.Serializable]
    public class AssetInfo { public string path; public string type; public string guid; }

    [System.Serializable]
    private class AssetListWrapper { public List<AssetInfo> assets; }

    public List<AssetInfo> AllAssets { get; private set; }
    
    // 싱글톤 패턴: 다른 스크립트에서 쉽게 접근하기 위함
    public static AssetListLoader Instance { get; private set; }

    void Awake()
    {
        // 싱글톤 인스턴스 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 파괴되지 않음
            LoadAssetList();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void LoadAssetList()
    {
        TextAsset listAsset = Resources.Load<TextAsset>("asset_list");
        if (listAsset == null)
        {
            Debug.LogError("Resources 폴더에서 asset_list.txt를 찾을 수 없습니다!");
            return;
        }

        string jsonString = listAsset.text;
        AssetListWrapper wrapper = JsonUtility.FromJson<AssetListWrapper>(jsonString);
        AllAssets = wrapper.assets;

        Debug.Log($"빌드 내에서 {AllAssets.Count}개의 에셋 정보를 성공적으로 로드했습니다.");
    }
}
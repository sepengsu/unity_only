# Unity MCP 기능 분류표

각 기능에 대해 Unity C#만으로 구현 가능한지, UnityEditor API가 필수인지 구분하고  
직접 구현 가능성도 함께 명시했습니다.

## ✅ Unity C# 만으로 구현 가능한 기능 (에디터 필요 없음)

| 기능 | 설명 | 직접 구현 가능 여부 |
|------|------|-------------------|
| GameObject 생성/삭제 | `Instantiate()`, `Destroy()` 사용 | 🛠 직접 구현 가능 |
| Transform 제어 | 위치, 회전, 크기 등 속성 설정 | 🛠 직접 구현 가능 |
| 컴포넌트 조작 | `AddComponent`, `GetComponent`, `.enabled` 등 | 🛠 직접 구현 가능 |
| Runtime 에셋 로딩 | `Resources.Load()`, `Addressables` | 🛠 직접 구현 가능 |
| UI 제어 | Button, Text, Canvas 조작 | 🛠 직접 구현 가능 |
| 물리/렌더링 설정 | Rigidbody, Collider, Material 등 | 🛠 직접 구현 가능 |

---

## ⚠️ UnityEditor API가 **필수**인 기능 (런타임에서는 불가)

| 기능 | 설명 | 직접 구현 가능 여부 |
|------|------|-------------------|
| 에디터 콘솔 제어 | 로그 메시지 읽기/삭제 | 🔧 구현 시 주의 필요 (`LogEntries`) |
| 씬 저장/로드 (편집 시) | `EditorSceneManager.SaveScene()` 등 | 🔧 구현 시 주의 필요 |
| 에셋 생성/삭제 | `AssetDatabase.CreateAsset`, `DeleteAsset` | 🔧 구현 시 주의 필요 |
| 메뉴 항목 실행 | `EditorApplication.ExecuteMenuItem("File/Save")` | 🔧 구현 시 주의 필요 |
| 코드 파일 생성/수정 | `.cs` 파일 생성 후 `AssetDatabase.Refresh()` | 🔧 구현 시 주의 필요 |
| 플레이 모드 제어 | `EditorApplication.isPlaying = true` | 🔧 구현 시 주의 필요 |
| 커스텀 에디터 UI | `EditorWindow`, `CustomEditor` | 🔧 구현 시 주의 필요 |
| GameObject 생성 (에디터 히에라키 반영 포함) | `Undo.RegisterCreatedObjectUndo()` 등 | 🔧 구현 시 주의 필요 |

---

## 📝 구현 전략

- `Runtime`에서 실행되어야 하는 로직은 **에디터 API를 전혀 포함하지 않도록** 분리
- `Editor/` 폴더 또는 `#if UNITY_EDITOR` 전처리기를 활용해 **에디터 전용 기능**과 구분
- LLM 연결 시에는 에디터 도구를 사용하는 요청과 런타임 제어 요청을 분리하여 처리

---

## 📂 추천 폴더 구조


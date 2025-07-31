# Unity MCP 기반 UnityEngeine 제어 시스템
---

## 🔁 시스템 구조 개요

| 구성요소 | 설명 | 입출력 |
|----------|------|--------|
| **Interface** | 외부 요청 수신 및 응답 전달 (TCP/HTTP 등) | `Json <-> Json` |
| **Parser** | 요청 Json 파싱, Handler로 전달, 결과 응답 생성 | `Json -> (Handler, Params)` |
| **Handler** | 요청 유형별 함수 분기 및 실행 전달 | `(함수, Params) -> 결과 전달` |
| **Functions** | 실제 Unity API 호출. GameObject, Asset 등 생성/제어 | 실행 및 결과 생성 |

---

## 🎮 GameObject 제어 기능 (Functions.GameObjectManager)

### 📂 관련 파일: `ManageGameObject.cs`

Unity 내에서 동적으로 GameObject를 생성/삭제/수정하고 컴포넌트를 추가하는 기능을 제공합니다.

### ✅ 기능 목록

| 기능 | 설명 | JSON `action` |
|------|------|----------------|
| GameObject 생성 | 프리팹/프리미티브/빈 객체 생성 및 부모/위치/컴포넌트 설정 | `create` |
| GameObject 수정 | 이름, 위치, 회전, 부모, 태그, 레이어, 컴포넌트 수정 | `modify` |
| GameObject 삭제 | 이름/ID/Tag 기반 오브젝트 삭제 | `delete` |
| 오브젝트 검색 | 이름/ID/Tag/Layer/Path/Component 기반 검색 | `find` |
| 컴포넌트 조회 | 대상 오브젝트에 포함된 컴포넌트 리스트 조회 | `get_components` |
| 컴포넌트 추가 | 지정된 타입의 컴포넌트를 추가하고 속성도 설정 가능 | `add_component` |
| 컴포넌트 제거 | 지정된 타입의 컴포넌트를 제거 | `remove_component` |
| 컴포넌트 속성 수정 | 특정 컴포넌트의 속성을 직접 설정 | `set_component_property` |

---

## 🧱 Asset 제어 기능 (Functions.ManageAsset)

### 📂 관련 파일: `ManageAsset.cs`

Assets 폴더 내의 리소스를 생성, 이동, 복사, 수정, 삭제할 수 있습니다.

| 기능 | 설명 | JSON `action` |
|------|------|----------------|
| Asset 생성 | Material, ScriptableObject, 폴더 등 생성 | `create` |
| Asset 수정 | Material 속성, ScriptableObject 값, Prefab 컴포넌트 수정 | `modify` |
| Asset 삭제 | 파일, 폴더 등 삭제 | `delete` |
| Asset 이동/이름변경 | 경로 이동, 이름 변경 | `move`, `rename` |
| Asset 복사 | 동일한 asset을 새 경로로 복제 | `duplicate` |
| 정보 조회 | 메타 정보 및 컴포넌트 확인 | `get_info`, `get_components` |

---

## 📝 Script 관리 기능 (Functions.ManageScript)

### 📂 관련 파일: `ManageScript.cs`

Unity C# 스크립트 파일을 생성, 읽기, 수정, 삭제합니다.

| 기능 | 설명 | JSON `action` |
|------|------|----------------|
| 스크립트 생성 | 기본 템플릿 또는 사용자 정의 코드 삽입 | `create` |
| 스크립트 읽기 | 파일 내용 반환 (Base64도 지원) | `read` |
| 스크립트 수정 | 전체 코드 수정 | `update` |
| 스크립트 삭제 | 해당 스크립트 삭제 | `delete` |

---

## 🎨 Shader 관리 기능 (Functions.ManageShader)

### 📂 관련 파일: `ManageShader.cs`

Unity의 .shader 파일을 관리할 수 있습니다.

| 기능 | 설명 | JSON `action` |
|------|------|----------------|
| 셰이더 생성 | 기본 셰이더 템플릿 또는 사용자 정의 내용 저장 | `create` |
| 셰이더 읽기 | Shader 코드 반환 | `read` |
| 셰이더 수정 | 내용 업데이트 | `update` |
| 셰이더 삭제 | 파일 삭제 | `delete` |

---

## ✅ 체크리스트 (진행 현황 관리용)
[체크리스트로 이동하기](./Checklist.md)
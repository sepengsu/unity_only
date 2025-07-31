# ✅ Unity MCP 전체 기능 체크리스트

## 🎮 1. GameObject

- [x] create: GameObject 생성 (Primitive, Empty, Prefab)
- [x] modify: 위치/회전/스케일/부모/이름/태그/레이어 수정
- [x] delete: GameObject 삭제
- [x] find: ID, name, tag, layer, path 등으로 검색
- [x] get_components: 포함된 컴포넌트 조회
- [x] add_component: 컴포넌트 추가
- [x] remove_component: 컴포넌트 제거
- [x] set_component_property: 컴포넌트 속성 변경
- [ ] prefab 수정 위임: prefab일 경우 ManageAsset으로 위임

---

## 🧱 2. Asset

- [ ] create: Material, ScriptableObject, 폴더 등 생성
- [ ] modify: Material/ScriptableObject/Prefab 수정
- [ ] delete: 에셋 제거
- [ ] move/rename: 경로 이동 및 이름 변경
- [ ] duplicate: 복제
- [ ] get_info: 에셋 정보 확인
- [ ] get_components: Prefab 컴포넌트 확인
- [ ] create_folder: 폴더 생성

---

## 📝 3. Script

- [ ] create: 템플릿 기반 C# 스크립트 생성
- [ ] read: Base64 지원 포함 읽기
- [ ] update: 코드 전체 수정
- [ ] delete: 스크립트 제거
- [ ] validate: 유효성 검사 (괄호 쌍 등)

---

## 🎨 4. Shader

- [ ] create: 기본 템플릿 생성
- [ ] read: 대용량 지원 포함 읽기
- [ ] update: 수정 반영
- [ ] delete: 삭제
- [ ] name check: 이름 중복 방지

---
   
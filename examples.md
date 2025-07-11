### 1. 오브젝트 생성 (Create)
"이름이 MyCube인 큐브를 (0, 1, 2) 위치에 생성해줘."
```json
{
  "action": "create",
  "name": "MyCube",
  "primitiveType": "Cube",
  "position": [0, 5, 2]
}
```
### 2. 오브젝트 이동(Move)
"MyCube를 x=3, y=1, z=0 위치로 옮겨줘."
```json
{
  "action": "move",
  "target": "MyCube",
  "position": [5, 1, 0]
}
```

### 3. 오브젝트 삭제 (Remove)
```json
{
  "action": "remove",
  "target": "MyCube"
}
```

### 4. 환경 설명 (Describe)
"씬정보"
```json
{
  "action": "describe"
}
```

### 5. 물체 회전 (Rotate)
"물건 돌게 해줘"
```json
{
  "action": "rotate",
  "target": "MyCube",
  "rotation": [0, 90, 45]
}
```

### 6. 물체 속도 이동 (제한시간 설정)
```json
{
  "action": "move_with_speed",
  "target": "MyCube",
  "speed": [1, 0, 0],
  "duration": 3.0
}
```
### 7. 물체 속도 이동 (제한시간 설정 X)
```json
{
  "action": "move_with_speed",
  "target": "MyCube",
  "speed": [0, -0.2, 0]
}
```
### 8. 무제한 회전 
```json
{
  "action": "rotate_with_speed",
  "target": "MyCube",
  "speed": [0, 45, 45]
}
```
### 9. 일정 시간 회전 
```json
{
  "action": "rotate_with_speed",
  "target": "MyCube",
  "speed": [45, 45, 0],
  "duration": 3.0
}
```

### 10. 멈추게 하기 
```json
{
  "action": "stop",
  "target": "MyCube"
}
```

### 11. 물체 색상 변경 
```json
{
  "action": "color",
  "target": "MyCube",
  "color": [1.0, 0.0, 0.0]
}
```
만약에 투명도를 원한다면 
```json
{
  "action": "color",
  "target": "MyCube",
  "color": [0.2, 0.4, 0.8, 0.5] 
}
```

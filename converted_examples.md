### 1. 오브젝트 생성 (Create)
```json
{
  "type": "manage_gameobject",
  "params": {
    "action": "create",
    "name": "MyCube",
    "primitiveType": "Cube",
    "position": [5, 5, 2]
  }
}
```

### 2. 오브젝트 이동 (Modfiy)
```json
{
  "type": "manage_gameobject",
  "params": {
    "action": "modify",
    "target": "MyCube",
    "position": [1.5, 2.0, -3.0]
  }
}
```
```json
{
  "type": "manage_gameobject",
  "params": {
    "action": "modify",
    "target": "MyCube"
  }
}
```
```json 
{
  "type": "manage_gameobject",
  "params": {
    "action": "modify",
    "target": "MyCube",
    "scale": [1.5, 2.0, -3.0]
  }
}
```

### 3. FInd
```json
{
  "type": "manage_gameobject",
  "params": {
    "action": "find",
    "target": "MyCube",
    "searchMethod": "by_name"
  }
}
```
# 4. Delete 
```json
{
  "type": "manage_gameobject",
  "params": {
    "action": "delete",
    "target": "Cube",
    "searchMethod": "by_tag",
    "findAll": true
  }
}
```
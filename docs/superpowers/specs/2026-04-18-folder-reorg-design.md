# 프로젝트 폴더 구조 재정리 설계

**Date:** 2026-04-18
**Project:** Modak-Golf (Unity 6 / URP, 모바일 캐주얼 미니골프)
**Status:** Approved

## 배경

URP 템플릿으로 생성된 신규 Unity 프로젝트에 에셋스토어 패키지(Nature Forest)가 import되면서 다음 문제가 발생:

- `Assets/Assets/Environment/Nature Forest/` — 이중 `Assets/` 경로로 중첩됨
- URP 템플릿 샘플(`SampleScene`, `TutorialInfo/`, `Readme.asset`)이 본 게임과 무관하게 남아있음
- 게임 자산용 표준 폴더(Scripts, Prefabs, Art, Audio 등) 부재
- 커스텀 코드·씬이 없는 상태 — 본격 개발 착수 전 구조 재정비 적기

## 목표

본격 개발 전에 폴더 구조를 정리하여:

1. 프로젝트 자산과 외부(에셋스토어) 자산을 시각적으로 분리
2. 모바일 캐주얼 미니골프에 적합한 Feature 중심 스크립트 구조 확립
3. URP 템플릿 잔재 제거
4. 이후 추가될 자산·코드의 목적지를 명확히 함

## 최상위 레이아웃

```
Assets/
├── _Project/            ← 우리 게임 자산 (언더스코어 prefix로 Project 창 상단 고정)
├── ThirdParty/          ← 에셋스토어/외부 패키지
├── Plugins/             ← 기존 Plugins (Roslyn 등)
└── Settings/            ← URP Renderer/Profile (엔진 설정, 루트 유지)
```

**설계 이유:**
- `_Project/` 접두 컨벤션은 Unity 커뮤니티에서 가장 널리 쓰이며 외부 에셋과 섞임을 방지
- `Settings/`(URP)는 엔진 기대 위치이자 템플릿 관례이므로 이동하지 않음 (GUID 참조라 이동해도 깨지지 않지만 굳이 건드릴 필요 없음)

## `_Project/` 내부 구조

```
_Project/
├── Art/
│   ├── Materials/
│   ├── Models/
│   ├── Textures/
│   ├── VFX/
│   └── UI/                 (UI용 스프라이트·아이콘)
├── Audio/
│   ├── BGM/
│   └── SFX/
├── Prefabs/
│   ├── Gameplay/           (Ball, Club, Hole 등)
│   ├── Stages/
│   └── UI/
├── Scenes/
│   ├── Boot.unity          (진입/초기화) — 추후 생성
│   ├── MainMenu.unity      — 추후 생성
│   └── Gameplay.unity      (퍼팅 씬) — 추후 생성
├── ScriptableObjects/
│   ├── Stages/             (스테이지 데이터)
│   └── Settings/           (난이도·튜닝값 등)
├── Scripts/                 (→ 다음 섹션 참조)
├── Input/
│   └── InputSystem_Actions.inputactions
└── Settings/                (프로젝트 공통 데이터 — 필요시 추가)
```

**일부러 만들지 않는 폴더:**
- `Resources/` — 빌드 사이즈·초기 로드 비용이 모바일에 불리. 런타임 로드는 직접 참조 또는 추후 Addressables로 처리.
- `Editor/` — 지금 커스텀 에디터 스크립트 없음. 필요 시 `_Project/Scripts/Editor/`로 추가.

## `Scripts/` 내부 구조 (Feature 중심)

```
_Project/Scripts/
├── Core/            (GameManager, SceneLoader, SaveSystem, EventBus)
├── Gameplay/
│   ├── Ball/        (공 물리·상태)
│   ├── Club/        (퍼팅 입력·샷 처리)
│   ├── Hole/        (홀 감지·클리어)
│   └── Camera/      (카메라 추적·조준)
├── Stages/          (StageLoader, StageData 참조)
├── UI/              (메뉴·HUD·결과창)
├── Audio/           (AudioManager, SFX 트리거)
├── Input/           (Input System 래퍼)
└── Utils/           (확장 메서드·헬퍼)
```

**Assembly Definition (asmdef):** 현 시점에서는 추가하지 않음. 초기 프로젝트에 오버엔지니어링. 스크립트 수가 늘어 컴파일 시간이 체감될 때 `Gameplay/`, `UI/` 단위로 분리 도입.

## 이관 동작 (Migration)

### 이동
| 원본 | 대상 |
|---|---|
| `Assets/Assets/Environment/Nature Forest/` | `Assets/ThirdParty/NatureForest/` |
| `Assets/InputSystem_Actions.inputactions` (+ .meta) | `Assets/_Project/Input/InputSystem_Actions.inputactions` |

### 삭제 (.meta 포함)
- `Assets/Scenes/SampleScene.unity`
- `Assets/Scenes/` (비게 되면 폴더째 삭제)
- `Assets/TutorialInfo/` (전체)
- `Assets/Readme.asset`
- `Assets/Assets/` (NatureForest 이동 후 비면 삭제)

### 신규 생성 (빈 폴더)
- `Assets/_Project/` 및 하위 구조 전체
- `Assets/ThirdParty/`

### 유지 (손대지 않음)
- `Assets/Settings/` (URP 설정)
- `Assets/Plugins/Roslyn/`
- `Packages/`, `ProjectSettings/`

## 제약 & 주의사항

- **Unity 에디터 종료 상태에서 이동 권장**: 에디터 실행 중 파일 시스템에서 이동하면 GUID 참조가 꼬일 수 있음. 또는 Unity 에디터 내 Project 창에서 드래그 이동 필요.
- **.meta 파일 동반 이동 필수**: 모든 폴더·파일은 동일 이름의 `.meta`와 함께 이동. 누락 시 GUID 재생성되어 참조 깨짐.
- **Build Settings 씬 목록**: 현재 `SampleScene`만 등록돼 있고 삭제 예정이라 특별 조치 불필요. 빌드 씬은 추후 신규 씬 생성 시 재등록.
- **빈 폴더 커밋**: Unity는 빈 폴더에 `.meta`를 만들지 않음. 플레이스홀더 `.gitkeep`는 git에 빈 폴더를 커밋하기 위함일 뿐 Unity에는 영향 없음. `_Project/` 하위 빈 폴더는 구조 가시성을 위해 유지.

## 검증 기준 (Acceptance)

- [ ] `Assets/Assets/` 경로 존재하지 않음
- [ ] `Assets/_Project/` 아래에 설계된 하위 폴더 모두 존재
- [ ] `Assets/ThirdParty/NatureForest/` 에 기존 Nature Forest 하위(Demo/Materials/Models/Prefabs/Textures) 전부 이관
- [ ] URP 샘플(`SampleScene`, `TutorialInfo/`, `Readme.asset`) 자취 없음
- [ ] Unity 에디터를 열었을 때 콘솔에 참조 깨짐/누락 에러 0건
- [ ] `InputSystem_Actions.inputactions`가 `_Project/Input/` 에서 정상 인식
- [ ] 프로젝트가 기존과 동일하게 재생(play)·빌드 가능 (기준: URP 파이프라인 동작)

## 미적용 / 후속

- 신규 씬(`Boot`, `MainMenu`, `Gameplay`) 실제 생성은 다음 단계
- 첫 게임플레이 스크립트 스켈레톤 작성은 다음 단계
- Addressables·asmdef·Editor 툴링은 필요 시점 도입

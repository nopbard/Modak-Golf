# Modak Putt — 게임 개발 명세서

> **이 문서의 정체성**: 본 문서는 모닥게임즈의 Unity 기반 미니골프 게임 `Modak Putt`의
> 개발 명세서입니다. Cursor / Claude 등 AI 코딩 도구의 컨텍스트로 사용됩니다.
> 사업계획서가 아닌 **개발 직결 명세서**입니다.

---

## 0. 이 문서 사용법 (AI에게 주는 지시)

당신(AI 코딩 도구)이 이 프로젝트의 코드를 작성하거나 수정할 때 반드시 따라야 할 규칙:

1. **`2. 절대 불변 원칙` 섹션은 어떤 경우에도 위반하지 않는다.** 제안조차 하지 않는다.
2. **`3. 기술 스택 결정`은 사용자의 명시적 변경 지시 없이는 바꾸지 않는다.**
3. 명세에 없는 디테일은 임의로 결정하지 말고 **사용자에게 묻는다.**
4. 코드 작성 시 **인스펙터에서 튜닝 가능한 값은 반드시 `[SerializeField]` private 또는 public 필드로 노출**한다. 사용자는 개발자가 아니므로 코드 수정 없이 인스펙터에서만 조정할 수 있어야 한다.
5. 공개 필드/메서드에는 한국어 주석을 붙인다. 클래스/메서드 이름은 영문 PascalCase/camelCase.
6. 새 시스템을 추가할 때는 **확장 가능한 구조**로 만든다 (인터페이스, 추상 클래스, ScriptableObject 활용).
7. 사용자가 "기획서에 없다"고 지적하면, 임의 구현하지 말고 명세에 추가할지부터 묻는다.

---

## 1. 게임 한 줄 요약

**Modak Putt**는 Unity로 개발되는 **3D 캐주얼 미니골프 게임**이다.
탑다운 isometric 시점에서 공을 드래그-릴리즈로 발사해 다양한 기믹이 가득한 코스의 홀에 넣는다.
싱글 플레이와 2인 로컬 턴제 모드를 제공하며, **벤츠 차량 인포테인먼트(MBUX)와 캠프파이어 웹 플랫폼**의 두 환경을 타겟으로 한다.

레퍼런스: **Wonderputt Forever**의 디오라마 스타일과 다이나믹한 코스 연출.

---

## 2. 절대 불변 원칙 (PROJECT INVARIANTS)

이 항목들은 **모든 설계 결정의 상위 제약**이다. 위반하는 코드/디자인은 즉시 거부한다.

### 2.1 성능
- **60fps 고정**. 최저 사양 환경에서도 60fps를 목표로 함.
- WebGL 빌드 크기: **압축 후 30MB 이하 목표**.
- 메모리 사용량: **512MB 이하**.
- 첫 로딩: **WebGL 환경에서 10초 이내**.

### 2.2 호환성
- **Unity WebGL 빌드와 완전히 동일하게 동작**해야 한다. 즉, WebGL에서 작동하지 않는 기능은 사용 금지.
  - 사용 금지 예: 멀티스레딩(`System.Threading`), `System.IO.File` 직접 접근, 동적 어셈블리 로딩
  - WebGL에서 동작 검증된 API만 사용
- Chromium 기반 브라우저 호환 (Chrome, Edge, MBUX 내장 브라우저).

### 2.3 입력
- **터치스크린 only**로 모든 조작이 가능해야 한다.
- 마우스 입력은 터치를 시뮬레이트하는 방식으로 동시 지원 (개발 편의 + 데스크탑 호환).
- 키보드, 게임패드, 마이크, 카메라 입력 사용 금지.

### 2.4 인터럽트 대응
- **언제든지 게임을 중단해도 사용자가 손해를 보지 않아야 한다.**
- 일시정지 시 즉시 게임 정지, 재개 시 중단 시점에서 정확히 복원.
- 앱이 강제 종료되어도 다음 실행 시 마지막 상태(현재 홀, 타수, 공 위치)에서 재개.

### 2.5 콘텐츠
- 가족 친화적. 폭력, 선정성, 정치/종교/논쟁적 주제 금지.
- 텍스트와 튜토리얼은 최소화. 모든 조작은 직관적이어야 함.

### 2.6 다국어
- 모든 사용자 노출 텍스트는 **i18n 키-값 구조로 분리**한다. 코드에 하드코딩 금지.
- 지원 언어: 한국어 / 영어 / 중국어 간체 / 독일어 (현재는 키 분리만 해두고 KO/EN 우선 작성)

---

## 3. 기술 스택 결정 (확정)

| 항목 | 결정 | 비고 |
|---|---|---|
| 엔진 | Unity 6.4 (6000.4 LTS) | |
| 렌더 파이프라인 | URP (Universal Render Pipeline) | |
| 입력 시스템 | **레거시 Input Manager** | `Input.GetMouseButton`, `Input.touches` 사용 |
| 물리 엔진 | Unity 내장 PhysX (3D) | Rigidbody + Collider |
| 좌표계 | 풀 3D | XZ 평면 + Y축 (점프, 낙하, 공중 회전) |
| Fixed Timestep | **0.01666 (60Hz)** | Project Settings > Time |
| UI | Unity UI (uGUI) + TextMeshPro | |
| 게임 상태 관리 | **클래스 기반 State Pattern** | enum 사용 금지 |
| 데이터 | ScriptableObject 적극 활용 | 사용자가 인스펙터로 튜닝 |
| 빌드 타겟 | WebGL (1순위), Android APK (2순위) | |

### 3.1 사용 금지 / 비권장 패키지
- Unity 신규 Input System: 사용 금지 (WebGL 터치 안정성 이슈)
- DOTS / ECS: 사용 금지 (오버킬, WebGL 호환 검증 부담)
- Burst Compiler: 사용 금지 (WebGL 미지원)
- Job System: 사용 금지

---

## 4. Unity 프로젝트 구조

### 4.1 폴더 구조
```
Assets/
├── _Project/                    # 본 프로젝트 전용 에셋 (외부 에셋과 분리)
│   ├── Art/
│   │   ├── Materials/
│   │   ├── Models/
│   │   ├── Textures/
│   │   ├── UI/
│   │   └── VFX/
│   ├── Audio/
│   │   ├── BGM/
│   │   └── SFX/
│   ├── Input/                   # InputSystem_Actions 등 입력 에셋
│   ├── Prefabs/
│   │   ├── Gameplay/
│   │   ├── Stages/
│   │   └── UI/
│   ├── Scenes/
│   ├── ScriptableObjects/       # 데이터 (스테이지, 기믹 파라미터, 스킨)
│   │   ├── Settings/
│   │   └── Stages/
│   ├── Scripts/
│   │   ├── Audio/               # BGM/SFX 매니저
│   │   ├── Core/                # 게임 코어 (상태, 매니저, 세이브 등)
│   │   ├── Gameplay/
│   │   │   ├── Ball/            # 공 물리, 입력, 트레일
│   │   │   ├── Camera/          # 카메라 시스템
│   │   │   ├── Club/            # 퍼터/샷 로직
│   │   │   └── Hole/            # 홀 트리거
│   │   ├── Input/               # 드래그-릴리즈 입력 처리
│   │   ├── Stages/              # 스테이지(코스) 로드, 기믹 시스템, 턴 매니저
│   │   ├── UI/                  # HUD, 메뉴, i18n
│   │   └── Utils/               # 유틸리티
│   └── Settings/                # URP/Quality 등 프로젝트 설정
├── ThirdParty/                  # 외부 에셋 (스토어에서 받은 것)
└── Plugins/
```

> **참고**: 명세 본문은 "코스(Course)" 용어를 사용하지만, 실제 폴더/프리팹/SO는
> **Stages** 네이밍을 사용한다. `Course` ↔ `Stage`는 동의어로 취급.
> 멀티플레이어/세이브/로컬라이제이션/셰이더는 전용 폴더 없이 각각
> `Stages/` (턴매니저), `Core/` (세이브), `UI/` (i18n), `Art/Materials/` 하위에 둔다.
> 확장이 필요해지면 그때 폴더를 분리한다.

### 4.2 씬 구성
- `Boot.unity` — 첫 실행, 초기화 후 Title로 이동
- `Title.unity` — 메인 메뉴
- `Game.unity` — 실제 게임 플레이 (모든 코스 공유)
- (선택) `Loading.unity` — 씬 전환 중 로딩

코스는 별도 씬이 아닌 **`Game.unity` 내에서 코스 프리팹을 동적 로드**하는 방식.
이유: 씬 전환 비용 절감, 카메라 패닝 연출 자유.

### 4.3 네이밍 컨벤션
- 클래스: `PascalCase` (예: `BallController`)
- 메서드: `PascalCase` (예: `Shoot()`)
- 필드 (private): `_camelCase` (예: `_velocity`)
- 필드 (public/SerializeField): `camelCase` (예: `maxPower`)
- 상수: `UPPER_SNAKE_CASE` (예: `MAX_DRAG_PIXELS`)
- 인터페이스: `I` 접두사 (예: `IGimmick`)
- ScriptableObject: `SO` 접미사 (예: `CourseDataSO`)

---

## 5. 코어 게임플레이 명세

### 5.1 드래그 & 샷 동작

**입력 시퀀스:**
1. **TouchDown / MouseDown**
   - 터치 위치를 월드 좌표로 변환
   - 공의 콜라이더 반경 × `touchRadiusMultiplier` (기본 2.5) 내인지 체크
   - 내부면 → `Aiming` 상태 진입, 시작 위치 저장
   - 외부면 → 무시
2. **TouchDrag / MouseDrag** (매 프레임)
   - `dragVector = currentPos - startPos` (스크린 픽셀 기준)
   - `power = clamp(|dragVector|, 0, MAX_DRAG_PIXELS) / MAX_DRAG_PIXELS` (0~1 정규화)
   - `direction = -dragVector.normalized` (새총: 반대 방향)
   - 인디케이터 갱신: 방향 화살표 + 파워 게이지 + 짧은 궤적 예측선 (길이 = `power × maxTrajectoryLength`)
3. **TouchUp / MouseUp**
   - 즉시 발사 (취소 없음)
   - 공에 `velocity = direction × power × maxShotSpeed` 적용
   - 인디케이터 숨김
   - 상태: `Rolling`

**인스펙터 노출 파라미터:**
- `touchRadiusMultiplier` (float, 기본 2.5)
- `maxDragPixels` (float, 기본 200)
- `maxShotSpeed` (float, 기본 25)
- `maxTrajectoryLength` (float, 기본 3)
- `trajectorySegments` (int, 기본 20)

### 5.2 게임 상태 머신

State Pattern으로 구현. 각 상태는 `IGameState` 구현체.

| 상태 | 진입 조건 | 동작 | 다음 상태 |
|---|---|---|---|
| `IdleState` | 공이 정지, 입력 대기 | 인디케이터 숨김, 입력 대기 | TouchDown 시 → `AimingState` |
| `AimingState` | 공 터치 시작 | 드래그 추적, 인디케이터 표시 | TouchUp 시 → `RollingState` |
| `RollingState` | 공 발사됨 | 물리 시뮬레이션, 정지 감지 | 공 정지 시 → `IdleState` 또는 `HoleInState` 또는 `NextTurnState` |
| `HoleInState` | 공이 홀에 들어감 | 클리어 연출, 별점 계산 | 다음 홀 또는 결과 화면 |
| `NextTurnState` (2P) | 한 플레이어 정지 | 턴 교체 | `IdleState` |
| `PausedState` | 일시정지 트리거 | 모든 시뮬레이션 정지 | 재개 시 이전 상태 복귀 |

**전이 규칙:**
- `PausedState`는 어떤 상태에서든 진입 가능, 재개 시 이전 상태로 복귀
- 상태 변경은 `GameStateMachine.ChangeState(newState)` 단일 진입점 사용

### 5.3 카메라 시스템

- **Orthographic 카메라** 1대
- 고정 회전: X축 30°, Y축 45° (정통 isometric)
- 코스 변경 시 코스 전체가 화면에 들어오도록 사이즈 자동 조절
- 코스 전환: 카메라가 다음 코스 중심으로 부드럽게 이동 (1~2초, ease-in-out)
- 주행 중 인터럽트나 일시정지 시 카메라 정지

**인스펙터 노출:**
- `cameraAngleX` (기본 30)
- `cameraAngleY` (기본 45)
- `transitionDuration` (기본 1.5)
- `easingType` (enum: Linear, EaseInOut, EaseOut)

### 5.4 코스 / 홀 단위

- **Course (코스)** = 하나의 플레이 단위. 1~N개의 Hole로 구성.
- **Hole (홀)** = 공이 들어가야 할 컵. 한 Course에 1개가 일반적.
- MVP는 **1 Course = 1 Hole**로 시작. 추후 Wonderputt식 연결형으로 확장.

**Course 프리팹 구성:**
- `CourseRoot` (빈 GameObject)
  - `Terrain` (지형, 정적 메쉬 + Mesh Collider)
  - `Walls` (벽, 정적)
  - `BallSpawnPoint` (공 시작 위치, Transform)
  - `Hole` (홀 위치, 트리거 콜라이더)
  - `Gimmicks` (이 코스에 배치된 기믹들)
  - `Decorations` (장식, 콜라이더 없음)

---

## 6. 시스템 아키텍처 (클래스 계층)

### 6.1 핵심 클래스 (의무 구현)

| 클래스 | 책임 |
|---|---|
| `GameManager` | 게임 전체 흐름 (싱글톤). 코스 로드, 모드 전환, 결과 저장 |
| `GameStateMachine` | 현재 상태 보유 및 전이 |
| `IGameState` | 상태 인터페이스 (`Enter`, `Update`, `Exit`) |
| `BallController` | 공 1개의 제어 (입력 수신, 물리 적용) |
| `BallPhysicsConfig` (SO) | 공의 물리 파라미터 (반발, 마찰, 회전 등) |
| `ShotInputHandler` | 드래그 입력 → 발사 벡터 계산 |
| `ShotIndicator` | 인디케이터 시각 표시 (화살표, 파워, 궤적) |
| `CameraController` | isometric 카메라 제어 |
| `CourseManager` | 현재 코스 로드/언로드, 다음 코스 진행 |
| `Hole` | 홀 트리거. 공 진입 감지 |
| `IGimmick` | 기믹 인터페이스 |
| `BaseGimmick` | 기믹 추상 클래스 (모든 기믹 상속) |
| `TurnManager` | 2P 턴 관리 |
| `SaveSystem` | 진행 저장/로드 (PlayerPrefs 또는 JSON) |
| `AudioManager` | BGM/SFX 재생 |
| `UIManager` | UI 전환, HUD 갱신 |
| `LocalizationManager` | i18n 키 → 현재 언어 텍스트 변환 |
| `PauseManager` | 일시정지 트리거 통합 |

### 6.2 핵심 인터페이스

```csharp
// 게임 상태
public interface IGameState
{
    void Enter();
    void Tick(float deltaTime);
    void FixedTick(float fixedDeltaTime);
    void Exit();
}

// 기믹
public interface IGimmick
{
    void OnBallEnter(BallController ball);
    void OnBallStay(BallController ball);
    void OnBallExit(BallController ball);
    void ResetGimmick();  // 코스 리셋 시 초기 상태로
}

// 일시정지 가능한 객체 (애니메이션, 파티클, 물리 등)
public interface IPausable
{
    void OnPause();
    void OnResume();
}
```

### 6.3 ScriptableObject 데이터 구조

사용자(비개발자)가 인스펙터에서 튜닝하는 모든 데이터는 SO로 분리.

| SO | 내용 |
|---|---|
| `BallPhysicsConfigSO` | 반발계수, 마찰계수, 회전 감쇠, 정지 임계값 |
| `ShotConfigSO` | 최대 파워, 드래그 픽셀, 인디케이터 길이 등 |
| `CourseDataSO` | 코스 메타데이터 (이름, 난이도, 별 기준 타수, 프리팹 참조) |
| `GimmickConfigSO` (각 기믹별) | 기믹 파라미터 (폭발 반경, 중력 강도 등) |
| `SkinConfigSO` | 공/퍼터 스킨 (모델, 컬러, 가격) |
| `LocalizationDataSO` | 언어별 키-값 |
| `AudioConfigSO` | BGM/SFX 클립 매핑 |

---

## 7. 기믹 시스템 (확장 구조)

### 7.1 설계 원칙
- 모든 기믹은 `BaseGimmick` 상속.
- 기믹 추가 시 코드 수정은 새 클래스 1개 + SO 1개 작성으로 끝.
- 기믹 파라미터는 SO에서 인스펙터 튜닝.

### 7.2 영상 데모용 초기 기믹 (구현 우선순위)
1. 경사로 (지형의 일부, 별도 클래스 불필요)
2. 폭탄 (`BombGimmick`)
3. 도미노 (`DominoGimmick`)
4. 중력구 (`GravityWellGimmick`)
5. 트램펄린 (`TrampolineGimmick`)

### 7.3 향후 확장 예정 (사용자가 추가)
- 텔레포트, 컨베이어 벨트, 회전 디스크, 강풍, 자석, 스위치, 점프대 등
- **추가 시 사용자가 AI에 별도 명세를 줄 예정.**

---

## 8. 2P 모드 명세 (단순 버전)

### 8.1 흐름
1. 메인 메뉴에서 "2P" 선택
2. 1P / 2P가 각각 공 색상 선택 (기본 빨강/파랑)
3. 코스 선택
4. **턴제 진행:**
   - 1P 샷 → 공 정지 대기 → 2P 턴
   - 2P 샷 → 공 정지 대기 → 1P 턴
   - 누군가 홀인 시 그 플레이어 즉시 마무리, 다른 플레이어는 계속 (또는 종료 — 추후 결정)
5. 결과: 적은 타수가 승

### 8.2 두 공의 상호작용
- 두 공 모두 일반 Rigidbody. **서로 충돌 가능** (PhysX 기본 동작).
- 충돌 시 운동량 보존으로 서로 튕김.
- → **상대 공을 맞춰 방해하는 플레이가 자연스럽게 발생.**

### 8.3 인터랙션 확장 (V2 — 영상엔 안 들어감)
- 특정 영역 통과 시 상대 코스에 장애물 생성
- 폭탄 폭발 시 상대 공도 휘말림
- 협동 모드 (같은 공을 번갈아 치기)

### 8.4 화면 표시
- 한 화면 공유. 분할 화면 X.
- 현재 턴 플레이어를 화면 상단에 큼지막하게 표시 (색상 + "P1/P2").
- 각 공 위에 작은 P1/P2 라벨 (선택).

---

## 9. UI 구조 (와이어프레임 수준)

### 9.1 화면 목록
| 화면 | 용도 |
|---|---|
| Title | 게임 시작, 모드 선택 (1P/2P), 설정 |
| ModeSelect | 1P / 2P 선택 |
| PlayerSelect (2P 시) | 1P, 2P 공 색 선택 |
| CourseSelect | 코스 목록, 별점 표시 |
| InGameHUD | 타수, 별, 일시정지 버튼, 현재 턴(2P) |
| Pause | 재개, 재시작, 메뉴로 |
| Result | 별점, 다음 코스, 메뉴로 |
| Settings | 음량, 언어 |

### 9.2 HUD 레이아웃 원칙
- 화면 모서리에 배치 (게임 영역 침범 최소화)
- 일시정지 버튼은 우측 상단 항상 노출
- 가로/세로 디스플레이 모두 대응 (Anchor 기반 레이아웃)

### 9.3 가로/세로 반응형
- Canvas Scaler: `Scale With Screen Size`
- Reference Resolution: `1920 × 1080`
- Match: 가로형은 `Width`, 세로형은 `Height` (Aspect Ratio 감지로 자동 전환)

---

## 10. 셰이더 / 비주얼 가이드

### 10.1 룩앤필
- **Wonderputt Forever 레퍼런스**: 디오라마 / 미니어처 / 로우폴
- 따뜻하고 채도 적당한 컬러
- 단순한 면 셰이딩, 복잡한 노멀맵 X
- 외곽선(아웃라인) 사용은 보류 (성능 비용)

### 10.2 셰이더 전략
| 용도 | 셰이더 |
|---|---|
| 지형/벽/장식 (대다수) | URP **Simple Lit** + 베이크된 라이트맵 |
| 공 | URP Lit (메탈릭/스무스니스 약간) |
| 중력구 | 커스텀 셰이더 (소용돌이 UV 회전 + 디졸브) |
| 폭탄 폭발 | URP Particle Unlit + VFX |
| 트레일 | URP Trail Renderer |
| UI | URP UI 기본 |

### 10.3 라이팅
- 실시간 라이트 1개 (Directional, 그림자 OFF)
- 베이크된 라이트맵 1장 (코스당, 1024×1024 이하)
- 동적 그림자는 **Blob Shadow** (공 아래 검은 원판) — 성능 절약 + 일관된 룩

### 10.4 컬러 팔레트
- 팔레트 텍스처 1장 (256×16) — 모든 머티리얼이 이 텍스처를 UV 좌표로 참조
- 머티리얼 종류 5개 이하로 압축 (드로우콜 절감)

---

## 11. 성능 예산

| 항목 | 한계 |
|---|---|
| FPS | 60 고정 |
| 빌드 크기 (WebGL, Brotli 후) | 30MB 이하 |
| 메모리 | 512MB 이하 |
| 첫 로딩 | 10초 이하 |
| 드로우콜 (한 화면) | 50 이하 |
| 폴리 카운트 (한 코스) | 50,000 tri 이하 |
| 활성 Rigidbody | 100개 이하 |
| 머티리얼 종류 | 5개 이하 |
| 라이트맵 크기 (코스당) | 1024×1024 이하, 1MB 이하 |
| 텍스처 (총합) | 5MB 이하 |
| 오디오 (총합) | 3MB 이하 (압축) |

---

## 12. WebGL 빌드 설정 체크리스트

Build Settings → Player Settings → WebGL:

- [ ] **Compression Format**: Brotli
- [ ] **Code Optimization**: Master (Size and Speed)
- [ ] **Strip Engine Code**: ON
- [ ] **Managed Stripping Level**: High
- [ ] **IL2CPP Code Generation**: Faster runtime
- [ ] **Decompression Fallback**: OFF (서버에 압축 헤더 설정 가능 시)
- [ ] **Memory Size**: 512 MB
- [ ] **Color Space**: Linear (URP는 Linear 권장, 단 WebGL 1.0 호환 위해 Gamma도 검토)
- [ ] **Auto Graphics API**: WebGL 2.0만 체크
- [ ] **Splash Screen**: 비활성 (Pro 라이선스 필요)
- [ ] **Texture Compression**: ASTC 또는 ETC2

URP Asset 설정:
- [ ] HDR: OFF
- [ ] MSAA: 2x 또는 OFF (성능 우선 시 OFF)
- [ ] Shadow Distance: 짧게 (또는 그림자 OFF + Blob shadow 사용)
- [ ] Render Scale: 1.0

---

## 13. Open Decisions (미결 사항)

향후 결정이 필요한 항목들. **AI는 임의로 결정하지 말고 사용자에게 물을 것.**

| 항목 | 메모 |
|---|---|
| 메타 시스템 상세 | 별점을 재화로 어떤 스킨을 살 수 있는가? 가격 책정? |
| 코스 진행 구조 | 챕터제 vs Wonderputt식 연결형 — 데모 후 결정 |
| 2P 홀인 처리 | 한 명 홀인 시 다른 사람 계속 vs 즉시 종료 |
| 사운드 디테일 | 공 굴러가는 소리 루프 처리, 충돌음 다양화 |
| 차량 데이터 연동 | MBUX에서 받을 수 있는 데이터 (속도, 충전 상태 등)와 게임 연동 — PoC 단계에서 결정 |
| 결과 화면 디테일 | 별점 외 통계 (홀인원, 최단 타수 등) 표시 여부 |
| 인앱결제 / 광고 | 수익 모델 구체화 — 별도 논의 |

---

## 부록 A. 영상 제출용 데모 시나리오 (목요일 마감)

5일 내 완성 목표. 영상 1~2분 분량.

### 데모에 반드시 포함
1. 게임 시작 화면 (3초)
2. **튜토리얼 코스 (가장 쉬움)**: 직선 페어웨이, 1~2번 샷으로 클리어 (10초)
3. **기믹 종합 코스 (가장 화려)**: 경사 + 폭탄 + 도미노 + 중력구 + 트램펄린 시연 (30초)
4. **2P 모드**: 두 공이 같은 코스에서 턴제로 플레이, 충돌 장면 포함 (20초)
5. 결과 화면 (별점) (5초)

### 영상 어필 포인트
- 카타르시스 모먼트 3개 이상 (도미노 연쇄, 폭탄 폭발, 홀인 슛)
- 디오라마 룩의 첫 인상
- 차량 디스플레이 목업에 합성 (선택)

---

## 부록 B. 용어집

| 용어 | 의미 |
|---|---|
| Course | 하나의 플레이 단위 (1개 이상 홀 포함) |
| Hole | 공이 들어가야 할 컵 |
| Shot | 1회 발사 |
| Stroke | 타수 (몇 번 쳤는가) |
| Gimmick | 코스에 배치된 인터랙티브 요소 (폭탄, 도미노 등) |
| MBUX | Mercedes-Benz User Experience (벤츠 차량 인포테인먼트 시스템) |
| 캠프파이어 | 모닥게임즈 자체 웹게임 플랫폼 |
| PoC | Proof of Concept (개념 검증) |

---

**문서 버전**: v0.1 (2026-04-19)
**작성**: 모닥게임즈 + AI 협업
**다음 업데이트**: 영상 제출 후 PoC 본 개발 진입 시 v1.0으로 정식화

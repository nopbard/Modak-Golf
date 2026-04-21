# WebGL 개발 가이드라인

이 프로젝트는 WebGL 빌드를 타겟으로 합니다.
코드 작성 시 아래 사항을 항상 염두하세요.

## 피해야 할 것

| 항목 | 이유 |
|------|------|
| `FindObjectOfType` / `FindObjectsOfType` in Update | 매 프레임 씬 전체 탐색 → 성능 저하 |
| 매 프레임 `Instantiate` | GC 압박, 스파이크 유발 → 오브젝트 풀링 고려 |
| LINQ, 임시 리스트/배열 남발 | GC 할당 → WebGL에서 GC 멈춤 체감됨 |
| `Compute Shader`, `Geometry Shader` | WebGL2 미지원 |
| `Thread`, `Task` | WebGL은 단일 쓰레드 |
| DBuffer 데칼 | OpenGL/WebGL 미지원 |
| `#pragma multi_compile` 남발 | 셰이더 변형 수 증가 → 빌드 용량 / 로딩 시간 증가 |

## 권장 패턴

- **오브젝트 풀링** — 자주 생성/삭제되는 오브젝트(파티클 제외)는 풀 사용
- **캐싱** — `GetComponent`, `Camera.main` 등은 `Awake`/`Start`에서 캐싱
- **셰이더** — `shader_feature` vs `multi_compile` 구분해서 변형 수 최소화
- **그림자** — PCF 소프트 섀도우 사용, Screen Space 방식 권장
- **Update 단순화** — bool 비교, 간단한 수식은 부담 없음. 무거운 탐색/할당만 주의

## 적용된 사례

- `ToonLit.shader` — `multi_compile` 3줄로 최소화 (FlatKit 대비 대폭 감소)
- `BallWaitingIndicator` — 이벤트 대신 단순 bool 폴링
- `Bomb` — `AudioSource.PlayClipAtPoint` 사용 (오브젝트 파괴 후에도 재생)
- 그림자 — DBuffer 제거, PCF HIGH + Screen Space 적용

# ProjectM — 그린 궤적이 검술이 되는 무협 자동 전투

화면에 손으로 그린 궤적이 무공(검술)이 되어 자동으로 싸우는 모바일 게임입니다. 2026.09부터 혼자 개발하고 있습니다.

| 항목 | 내용 |
| --- | --- |
| 엔진 | Unity 6.3 (6000.3.24f1), URP |
| 언어 | C# |
| 플랫폼 | 모바일 (Android 우선) |
| 형태 | 1인 개발 (설계는 직접 하고, 구현에는 Claude Code와 Unity MCP 활용) |
| 상태 | 개발 중 |

## 핵심 루프

```
무공 제작(궤적 그리기) → 저장 → 1분 자동 전투 → PvE 스테이지 진행 → PvP 랭킹
```

## 설계: 같은 입력이면 어느 기기에서나 같은 결과

비동기 PvP는 상대 데이터를 받아 각자 기기에서 전투를 재현합니다. 결과가 기기마다 조금이라도 다르면 승패가 뒤바뀌기 때문에, 처음부터 결정론적인 구조로 설계했습니다.

| 어셈블리 | 역할 |
| --- | --- |
| `Game.sim` | 궤적 분석, 무공 조합, 전투 시뮬레이션, 진행·PvP 규칙. `noEngineReferences: true`로 UnityEngine을 참조할 수 없게 분리 |
| `Game.Client` | 궤적 입력, 전투 재생·연출, UI, 화면 흐름 |
| `Game.Tests` | EditMode 유닛 테스트 |

- float 대신 고정소수점(`Fixed`, `IntegerMath`)을, 시스템 난수 대신 시드 난수(`DeterministicRng`)를 씁니다.
- 저장 데이터는 외부 라이브러리 없이 직접 만든 JSON 직렬화기로 처리합니다.
- PvP 백엔드는 `IPvpBackend` 인터페이스로 분리했습니다. 지금은 로컬 가짜 구현(`LocalFakePvpBackend`)을 쓰고 있고, 서버 구현으로 바꿔도 UI는 건드리지 않도록 했습니다.

## 시뮬레이션으로 잡은 밸런스 문제

큰 원을 그린 찌르기가 AI 상대를 일방적으로 이기는 문제가 있었습니다. 데미지가 원의 면적에 비례해 선형으로 커지는 공식이 원인이었습니다.

그래서 면적의 제곱근을 쓰도록 바꿨습니다(`ThrustDamage = IntegerSqrt(Area) / 26`). 나누는 값 26은 반지름 150인 원이 기본 베기 데미지(10)와 같아지도록 정했습니다. → `Assets/Game.sim/Battle/BattleConstants.cs`

## 폴더 구성

```
Assets/
├── Game.sim/      Ai · Battle · Common · Cultivation · Progress · Pvp · Stroke · Technique
├── Game.Client/   Battle · Cultivation · Flow · Progress · Pvp · Stage · Stroke · Technique · Ui
├── Game.Tests/    전투·궤적·직렬화·랭킹 등 유닛 테스트
└── CharacterArt/  캐릭터 아트 리소스
```

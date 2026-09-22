## 프로젝트 개요
무협 검로 자동전투 게임. Unity 6.3 LTS, URP, Android 우선 타겟.
화면에 선을 그으면(자세: 앉기/서기/전진/후퇴 포함) 검로가 되고,
이를 조합해 무공을 만들어 AI 또는 다른 플레이어와 자동 전투를 벌이는 게임.

## 아키텍처 규칙 (반드시 지킬 것)
- Assets/Game.sim은 절대 UnityEngine을 참조하지 않는다. (No Engine References 설정됨)
- Game.sim 내부에서는 float 대신 고정소수점 또는 정수 연산을 사용한다.
- Game.sim 내부에서 System.Random, DateTime.Now, Time.* 사용 금지.
  결정론적 시뮬레이션을 위해 시드 기반 커스텀 RNG만 사용한다.
- 무공/초식 데이터는 버전 필드(version)가 포함된 JSON 스키마로 저장한다.
- 입력(Game.Client)과 시뮬레이션(Game.sim)의 경계에서 float를 정수로 변환한다.

## 작업 규칙
- 한 번에 기능 하나만 작업한다. 여러 기능을 한 프롬프트에 섞지 않는다.
- 새 로직을 Game.sim에 추가하면 반드시 Game.Tests에 대응하는 EditMode 테스트를 함께 작성한다.
- 작업이 끝나면 테스트를 실행하고, 콘솔 에러 유무를 확인한 뒤 결과를 요약해서 보고한다.
- 기존 씬(.unity), 프리팹(.prefab)을 삭제하거나 대량으로 수정하기 전에는 먼저 확인을 구한다.
- ProjectSettings 폴더의 파일은 명시적으로 요청받은 경우가 아니면 수정하지 않는다.

## 테스트 실행 방법
- 에디터 안에서: Window > General > Test Runner > EditMode 탭 > Run All
- Unity RunCommand로 테스트를 실행할 수 있다면 그 방법을 사용해도 된다.

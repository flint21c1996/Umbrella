# NPC Behavior 초기 설계

## 왜 하는가

NPC는 플레이어가 특정 조건을 만족했을 때 특정 위치로 이동하거나, 애니메이션/대사/오브젝트 활성화 같은 행동을 해야 한다. NPC 종류가 많아도 10~15종 정도라서 큰 자체 BT 프레임워크를 새로 만들기보다는, Unity Behavior 패키지를 기준으로 공통 조건과 보조 컴포넌트만 얇게 추가한다.

## 이번 작업

- Unity Behavior의 기본 노드를 우선 사용한다.
- 이동은 `Navigate To Target` / `Navigate To Location` 기본 노드와 `NavMeshAgent`를 사용한다.
- 대기, 애니메이션 파라미터, 오브젝트 활성화도 기본 노드를 우선 사용한다.
- 프로젝트 전용으로 필요한 조건만 추가한다.
- 버튼/퍼즐/트리거 결과를 Behavior Graph가 읽을 수 있게 보조 컴포넌트를 둔다.

## 추가한 요소

- `NPCBehaviorSignal`: UnityEvent나 Trigger에서 켜고 끌 수 있는 단순 신호.
- `NPCTriggerSignalZone`: 플레이어가 특정 구역에 들어오면 신호를 켠다.
- `NPCBehaviorBlackboardBinder`: Behavior Graph의 `Agent`, `Player`, `PlayerTransform` 변수를 자동 연결한다.
- `NPC Signal Is Set` 조건: `NPCBehaviorSignal` 상태를 Behavior Graph에서 검사한다.
- `NPC Puzzle Condition Is Satisfied` 조건: `PuzzleConditionSource` 또는 `PuzzleConditionGroup` 상태를 Behavior Graph에서 검사한다.

## 1차 사용 예

플레이어가 특정 버튼을 누르면 NPC가 특정 위치로 이동하는 경우:

1. NPC에 `BehaviorGraphAgent`, `NavMeshAgent`, `NPCBehaviorBlackboardBinder`를 붙인다.
2. Behavior Graph 블랙보드에 `Agent` GameObject, 목적지 GameObject 또는 Vector3를 만든다.
3. 버튼이 기존 퍼즐 조건이면 `NPC Puzzle Condition Is Satisfied` 조건에서 해당 버튼/ConditionGroup 오브젝트를 참조한다.
4. 조건이 참이면 Sequence에서 `Navigate To Target` 또는 `Navigate To Location`을 실행한다.
5. 이후 필요한 경우 기본 노드로 Animator Trigger, Wait, Set Object Active 등을 이어 붙인다.

조건이 깨졌을 때 중단/복귀/계속 진행 여부는 회의 후 Observer Abort 설정이나 별도 조건 구조로 정한다.

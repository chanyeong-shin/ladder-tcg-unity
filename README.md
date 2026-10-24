# 침투 프로토콜 · BREACH PROTOCOL

| 항목 | 내용 |
|---|---|
| 갱신 | 2026-10-25 재구현 |
| 환경 | Unity 6000.3.22f1 · UGUI · Windows · 싱글플레이 봇 대전 |
| 규칙 | 상위 사다리전투_TCG_기획서.md 1~8절 |
| 화면 | 2026-10-25 Figma Week08_Proposals 스냅샷 기반 |
| 이전 구현 | 033e03e 커밋. 현재 브랜치 rebuild/2026-10-25 |

## 1. 플레이

`Build/BreachProtocol.exe`를 실행한다. 실행 파일만 옮기지 않는다. 같은 폴더의 데이터·DLL·MonoBleedingEdge도 필요하다.

Unity에서는 이 `unity` 폴더를 열고 `Assets/Scenes/Game.unity`에서 Play를 누른다.

| 조작 | 결과 |
|---|---|
| 침투 개시 또는 Enter | 새 대전 시작 |
| 손패에 마우스 올리기 | 카드 1.6배 확대, 다른 카드는 간격 유지 |
| 카드 선택 후 대상 클릭 | 카드 사용 |
| 카드를 판에 끌어서 놓기 | 카드 사용. 잘못된 위치·판 밖은 손패로 복귀 |
| 라우팅 변경 | 빈 가로줄 자리에 건설, 기존 줄은 파괴 |
| 버퍼 적재·버퍼 오버플로 | 카드 선택 후 판 클릭 또는 판/소켓으로 드롭 |
| 장전된 서버에 마우스 올리기 | 해당 패킷 경로 표시 |
| 행동 취소 | 자기 행동 시작 시점으로 손패·판·난수 복원 |
| 행동 종료 또는 Enter | 다음 단계로 진행 |
| Escape | 카드 선택 해제 |
| 기록 보기 | 전투 기록 열기·닫기 |
| 결과 화면 다시 시작 | 새 대전 |

카드 6종이 들어 있다. 강화 카드는 동시 발동 때 공개된다. 상대 방어 행동은 해결 전까지 손패 숫자·연출·기록으로 노출하지 않는다. 전멸까지 턴 제한 없이 진행한다.

## 2. 수정할 곳

| 파일·폴더 | 역할 |
|---|---|
| Assets/Scenes/Game.unity | 실제 Canvas·화면·서버·버튼과 참조 연결 |
| Assets/Prefabs | 카드 6종·공통 뒷면·양측 서버 원본 |
| Assets/Scripts/Core/Battle.cs | 규칙·상태·턴·합법성·되돌리기·봇 판단 |
| Assets/Scripts/UI/BattleController.cs | 입력·화면 갱신·전송/발사 연출 |
| Assets/Scripts/UI/CardView.cs | 카드 호버·클릭·드래그 이벤트 |
| Assets/Scripts/UI/ServerView.cs | null·장전·OFFLINE 표시와 경로 호버 |
| Assets/Scripts/UI/Shape.cs | 테두리·선·육각형 UI 도형 |
| Assets/Editor/SceneBuilder.cs | 씬·프리팹 생성, Windows 빌드 |
| Assets/Figma | Figma 좌표·색·텍스트 스냅샷 |
| Assets/Art/Figma | 복잡한 원본 벡터의 부품별 이미지 |
| Tests | 규칙 사례 검증 |

문구·버튼·회로·서버·카드 구조는 편집할 수 있다. 복잡한 카드·표지 그림 일부는 이미지 부품이다. 전체 화면을 이미지 하나로 대체하지 않았다.

## 3. 빌드와 재생성

`Breach > 저장된 씬으로 Windows 빌드`는 현재 저장된 씬을 빌드한다. Inspector에서 수정한 씬·프리팹을 재생성하지 않는다.

`Breach > 승인 화면으로 새 씬 생성`은 씬·프리팹을 다시 만든다. 수동 편집을 덮어쓰므로 재생성이 필요할 때만 사용한다. 검증 스크립트의 `SceneBuilder.Rebuild`도 재생성을 포함한다.

## 4. 검증과 한계

규칙 검사: `dotnet run --project Tests/CoreTests.csproj -p:BaseOutputPath=bin3/`.

실행 검증은 상위 `_검증/Unity_재구현/smoke/`에 기록한다. 고정된 한 판의 시작·선택·사용·잘못된 드롭·취소·장전·발동·승리·재시작을 검사한다. 밸런스 시뮬레이션이나 모든 랜덤 조합의 완전 검사는 아니다.

사운드와 튜토리얼은 기획서에서 미정이므로 추가하지 않았다. 연출 속도·강도와 실제 마우스 조작감은 사용자 플레이로 확인한다. 봇의 장기 대전 균형도 이번 검증 범위 밖이다.

화면 비율은 [Unity Canvas Scaler 공식 설명](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Documentation~/script-CanvasScaler.md)의 Expand 방식을 사용해 1920×1080 영역을 유지하고, 비율 차이는 바깥 여백으로 처리한다.

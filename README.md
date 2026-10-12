# 사다리 전투 TCG — 유니티

규칙 v1.0 · Unity 6000.3.22f1 · 1920x1080

---

## 여는 법

Unity Hub → **Add project from disk** → 이 `unity` 폴더 → `Assets/Scenes/Game.unity` → **Play**.

시작 화면이 뜨고 `EXEC >> 접속` 을 누르면 판이 시작된다.

**카드는 끌어서 낸다** (기획서 10.1). 손패 카드를 누른 채 끌면 커지면서 떠오르고,
판 테두리 전체가 빛나며 놓을 수 있는 자리만 한 단계 더 밝아진다.
판 안에서 손을 떼면 배치되고, **판 밖에서 떼면 손패로 돌아간다** (취소).
`행동 종료` 를 누르면 차례가 넘어간다.

낸 카드는 왼쪽 `CARD LOG` 소켓에 칩처럼 꽂혔다가, 기판 회로를 타고 판 테두리로
빨려 들어간다. 봇이 낸 것도 같은 일이 빨강으로 일어난다.

---

## 무엇이 어디에 있나

```
Assets/
  Scenes/Game.unity        화면 전체. 23,000줄짜리 실제 오브젝트다
  Prefabs/                 실행 중에 개수가 바뀌는 것만 프리팹으로 둔다
    카드.prefab              손패 카드 한 장
    가로줄.prefab            연결 회선 한 줄
    봇카드뒷면.prefab         봇 손패 뒷면
  Scripts/
    Core/                  규칙. 유니티를 하나도 쓰지 않는 순수 C#
      Rules.cs               고정값과 카드 6종
      GameState.cs           판의 상태
      GameEngine.cs          규칙 그 자체. 턴 흐름은 EndAction 한곳에서만 바뀐다
      Bot.cs                 봇. 카드만 내고 차례는 건드리지 않는다
    View/                  화면. 전부 MonoBehaviour 다
      GameDirector.cs        엔진과 화면을 잇는다. 씬의 GameDirector 에 붙어 있다
      BoardView.cs           사다리판 — 서버 6개, 가로줄, 누를 자리
      ServerView.cs          서버 하나 (육각형 · 보안도 · OFFLINE · 패킷)
      RungView.cs            가로줄 하나
      HandView.cs            손패 — 카드를 만들어 늘어놓는다. 적 배치 예고도 여기서
      CardView.cs            카드 한 장. 끌어서 내는 길이 여기 있다
      HudView.cs             윗줄 · SEQUENCE · INTEL · 버튼
      SocketView.cs          CARD LOG 의 CPU 소켓과 기판 회로 (칩이 꽂혔다 빨려 들어간다)
      BoardFxView.cs         판 연출 — 턴 색 · 빛줄기 · 섬광 · 피해 숫자 · 폭발 · 흔들림
      TransitionView.cs      턴 전환 명판 (가림막 · 코드 레인 · 명판)
      DecorView.cs           장식 — 이진수 뒤집힘 · 바코드 흔들림 · 타자 효과
      ScreenFlow.cs          시작 화면 → 플레이 → 종료 화면
      Palette.cs             시안의 색 (소켓 잔색 · 턴별 판 색 포함)
      AutoPlay.cs            검사용. 사람 없이 한 판을 끌어서 돌려 본다
    Editor/                에디터에서만 도는 도구. 빌드에는 안 들어간다
      FigmaImport.cs         피그마 배치표를 게임오브젝트로 세운다
      WireGame.cs            세운 화면을 컴포넌트에 물리고 프리팹을 뽑는다
      BuildScreen.cs         Tools 메뉴
      UiSprites.cs           육각형 · 원 · 테두리 · 번짐을 코드로 그린다
      FontAssets.cs          TMP 폰트 에셋을 만든다
      Screenshot.cs          창 없이 화면을 찍는다
      Probe.cs               한 점 위에 무엇이 겹쳐 있는지 알려 준다
      PlayTest.cs            창 없이 Play 를 눌러 한 판을 돌린다
  Figma/배치표/             피그마에서 받아 온 좌표표 7개
  Fonts/                   Inter · Rajdhani · 본고딕 (전부 OFL)
  Art/Generated/           UiSprites 가 만든 그림. 지워도 다시 만들어진다
  TextMesh Pro/            TMP 필수 리소스
```

---

## 화면은 피그마에서 가져온다

`Assets/Scenes/Game.unity` 는 **손으로 만들지 않았다.** 피그마 시안의 좌표·색·글자를
그대로 받아 와 게임오브젝트로 세운 것이다. 그래서 시안과 어긋날 수가 없다.

```
피그마  ──(1)──▶  _검증/figma_raw/*.json   원본 덤프
        ──(2)──▶  Assets/Figma/배치표/*.json   유니티가 읽는 납작한 표
        ──(3)──▶  Game.unity                실제 게임오브젝트 172개
```

1. `node _검증/figma_dump.js json _검증/figma_raw 17:170`
2. `python _검증/figma_to_unity.py _검증/figma_raw/17_170.json Assets/Figma/배치표/17_170.layout.json`
3. 유니티에서 **Tools > 사다리전투 > 화면 만들기**

전환 명판은 전환 화면 시안(`13_44`)에서 **오버레이 묶음(`13:204`)만** 가져온다.
전환 화면 시안은 플레이 화면을 통째로 품고 있어서, 그대로 세우면 같은 화면이 두 겹이 된다.

> **3번을 누르면 씬이 처음부터 다시 만들어진다.** 손으로 고쳐 둔 것은 사라진다.
> 시안이 바뀌었을 때만 누르고, 평소에는 Inspector 에서 고친다.

시안과 얼마나 같은지는 숫자로 잰다.

```
Unity.exe -quit -batchmode -projectPath . \
  -executeMethod LadderBattle.EditorTools.BuildScreen.CLI_Mock -shotPath 시안만.png
python _검증/화면대조.py 시안.png 시안만.png 결과폴더
```

**2026-10-12 기준 평균 차이 2.21 / 255.** 남은 차이는 거의 한글 글꼴이다 —
시안은 Inter 를 쓰는데 Inter 에 한글이 없어 피그마가 윈도우 글꼴로 대신 그렸고,
유니티는 본고딕으로 그린다. 글자 모양이 다르니 이 차이는 더 줄일 수 없다.

---

## 명령줄에서 돌리기

유니티 허브에 로그인돼 있으면 창 없이 다 된다.

| 할 일 | 명령 |
|---|---|
| 화면 다시 세우기 | `-quit -batchmode -executeMethod LadderBattle.EditorTools.BuildScreen.CLI_Build` |
| 시안만 세워 사진 | `-quit -batchmode -executeMethod ...BuildScreen.CLI_Mock -shotPath <경로>` |
| 지금 씬 사진 | `-quit -batchmode -executeMethod ...Screenshot.CLI_Shot -shotPath <경로>` |
| 한 점 위에 뭐가 있나 | `-quit -batchmode -executeMethod ...Probe.CLI_Probe -probe 960,200` |
| **한 판 돌려 보기** | `-batchmode -executeMethod ...PlayTest.CLI_Play` |
| 연출 끄고 숫자만 | 위에 `-noFx` 를 더한다 |
| 옛 누르기 길로 | 위에 `-byClick` 를 더한다 |

앞에 `"C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Unity.exe" -projectPath .` 를 붙인다.

> **한 판 돌려 보기에는 `-quit` 를 붙이면 안 된다.** Play 에 들어가기 전에 닫힌다.
> 검사가 스스로 닫는다 — 성공 0, 실패 1.
> `-nographics` 도 붙이면 안 된다. 그리질 못해서 사진이 비어 나온다.

`PlayTest` 는 사람 없이 **카드를 집어 끌어다 놓고** 턴을 넘겨 판이 끝날 때까지 간다.
연출이 실제로 화면에 떴을 때마다 사진을 남기고, 하나라도 안 떴으면 실패로 끝낸다 —
숫자만 맞고 연출이 안 돌면 "돌았다"고 보고하는 셈이 되기 때문이다.
사진과 보고서가 `Logs/자동플레이/` 에 남는다.

> **검사기의 "걸음"은 프레임이 아니다.** 배치모드는 초당 1000프레임 가까이 돈다.
> 프레임으로 상한을 걸면 연출이 몇 번 돌기도 전에 걸려서 "판이 안 끝났다"가 된다.
> `최대결정`(낼지 넘길지 정한 횟수)과 `최대시간`(초)으로 센다.

---

## 규칙은 유니티 밖에서도 돈다

`Scripts/Core` 는 `using UnityEngine` 이 한 줄도 없다. 그래서 유니티 없이 수천 판을 돌려
숫자를 볼 수 있다. 화면을 고치다 규칙이 틀어졌는지 여기서 바로 잡힌다.

```
cd _검증/csharp && dotnet run -c Release -- 3000
```

| 2026-10-12 · 3000판 | 실행판 HTML | 유니티 Core (C#) |
|---|---:|---:|
| 전멸 결착 | 18.1% | 17.6% |
| 무승부 | 7.0% | 7.6% |
| 평균 턴 | 9.10 | 9.13 |
| 규칙 위반 | **0** | **0** |

---

## 알아 둘 것

- **턴 흐름을 바꾸는 코드는 `GameEngine.EndAction` 한곳뿐이다.** 10-11 에 봇이 스스로
  차례를 넘기는 바람에 방어 턴이 통째로 사라진 적이 있다. 화면 쪽에서 차례를 직접 넘기지 않는다.
- **번짐은 형제로 둔다.** 자식으로 두면 칠 위에 덮인다. 조각을 옮길 때 번짐도 같이 옮겨야 한다
  (안 그러면 옛 자리에 남아 엉뚱한 것을 가린다).
- **9분할 스프라이트는 가운데를 안 그린다**(`fillCenter = false`). 가운데가 투명해도
  늘리면 양옆 픽셀이 번져 들어와 틀 안쪽이 테두리 색으로 물든다.
- **한글 줄바꿈**은 TMP Settings 의 `Use Modern Hangul Line Breaking Rules` 가 켜져 있어야
  단어 중간에서 안 끊긴다.
- **`??` 로 유니티 컴포넌트를 받지 않는다.** `GetComponent<T>() ?? AddComponent<T>()` 는
  없는 컴포넌트를 그냥 통과시킨다 — 유니티의 빈 컴포넌트는 `==` 를 재정의한 가짜 null 이라
  널 병합 연산자가 못 알아본다. `TryGetComponent` 를 쓴다.
- **연출은 `GameState.Fx` 만 보고 그린다.** 어느 칸에서 상쇄됐는지는 `Resolve()` 안에서만
  알 수 있다. 화면 쪽에서 다시 계산하면 규칙이 두 군데로 갈라진다. `Fx` 는 적어 두기만
  하고 판정에는 쓰지 않으므로 창 없는 검증에서도 숫자가 그대로다.
- `Assets/Art/Generated` 와 `Library` 는 지워도 된다. 다시 만들어진다.

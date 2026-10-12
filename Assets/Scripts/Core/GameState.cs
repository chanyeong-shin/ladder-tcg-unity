using System.Collections.Generic;

namespace LadderBattle.Core
{
    /// <summary>어느 쪽인지. P = 나, B = 봇.</summary>
    public enum Side { P, B }

    /// <summary>한 턴의 어느 대목인지.</summary>
    public enum Step { Idle, Attack, Defend, Resolve, End }

    public enum Result { None, PlayerWin, BotWin, Draw }

    /// <summary>판에 놓인 패킷. 같은 채널에 겹쳐 내면 하나로 합쳐지고 위력만 올라간다.</summary>
    public sealed class Shell
    {
        public int Id;
        public Side Owner;
        public int Lane;
        public int Power;
        public bool Pierce;     // 제로데이로 주입한 것. 상쇄되지 않는다
    }

    /// <summary>뒷면으로 깔린 보안 등급 카드. 깐 턴의 동시 발동에 공개된다.</summary>
    public sealed class SecCard
    {
        public int Id;
        public Rules.CardId Card;
        public int Lane;        // 대상이 없는 카드는 -1
    }

    /// <summary>연결 회선 한 줄. 단계(tier)마다 하나까지만 놓을 수 있다.</summary>
    public sealed class Rung
    {
        public int Id;
        public int Tier;        // 1..Tiers
        public int Left;        // 0..Lanes-2 (왼쪽 채널 번호)
        public Side Owner;
    }

    public sealed class SideState
    {
        public readonly List<Rules.CardId> Hand = new List<Rules.CardId>();
        public readonly int[] Ends = { Rules.EndHp, Rules.EndHp, Rules.EndHp };
        public readonly List<Shell> Shells = new List<Shell>();
        public readonly List<SecCard> Sec = new List<SecCard>();      // 아직 뒷면인 것
        public readonly List<SecCard> SecOpen = new List<SecCard>();  // 이번 턴 공개된 것 (화면용)
        public bool Done;

        public int AliveServers
        {
            get
            {
                int n = 0;
                foreach (var hp in Ends) if (hp > 0) n++;
                return n;
            }
        }

        public int SecurityLeft
        {
            get
            {
                int n = 0;
                foreach (var hp in Ends) n += hp;
                return n;
            }
        }
    }

    /// <summary>
    /// 판 전체의 상태. HTML 쪽 전역 `S` 하나를 그대로 옮긴 것이다.
    /// 화면은 이 값을 읽기만 하고, 바꾸는 것은 <see cref="GameEngine"/> 뿐이다.
    /// </summary>
    /// <summary>동시 발동에서 일어난 일 한 가지. 연출은 이 기록만 보고 그린다.</summary>
    public enum FxKind { 발사, 차단, 관통, 상쇄, 착탄, 파괴, 헛방, 번짐 }

    /// <summary>
    /// 연출용 기록. **판정에는 쓰지 않는다** — 적어 두기만 한다.
    ///
    /// 왜 규칙 쪽에 두나 — 무엇이 어느 칸에서 상쇄됐는지는 <see cref="GameEngine.Resolve"/>
    /// 안에서만 알 수 있다. 화면 쪽에서 다시 계산하면 규칙이 두 군데로 갈라진다.
    /// 유니티를 쓰지 않으므로 창 없는 검증(`_검증/csharp`)에서도 그대로 돈다.
    /// </summary>
    public sealed class Fx
    {
        public FxKind Kind;
        public Side By;          // 누가 쏜 것인가 (착탄은 쏜 쪽, 맞은 쪽은 반대)
        public int Lane;         // 발사는 출발 채널, 착탄은 맞은 채널
        public int EndLane;      // 발사가 도착하는 채널
        public int Power;
        public int Power2;       // 상쇄에서 상대 위력
        public bool Pierce;
        public int Y = -1;       // 상쇄가 일어난 단계 (0..Goal)
        public readonly List<(int y, int lane)> Cells = new List<(int, int)>();
    }

    public sealed class GameState
    {
        public int Turn;
        public readonly List<Rung> Rungs = new List<Rung>();
        public int RungId = 1, ShellId = 1, SecId = 1;

        public Side First;              // 이번 턴 공격 턴을 맡은 쪽
        public Side Acting;             // 지금 행동하는 쪽
        public Step Step = Step.Idle;
        public bool Ended;
        public Result Result = Result.None;

        public readonly List<int> Blocked = new List<int>();   // 방화벽이 막은 채널
        public bool Reveal;                                    // 동시 발동에 들어갔나
        public readonly List<string> Log = new List<string>();
        /// <summary>이번 동시 발동의 연출 기록. 발동이 시작될 때 비워진다.</summary>
        public readonly List<Fx> Fx = new List<Fx>();

        public readonly SideState P = new SideState();
        public readonly SideState B = new SideState();

        public SideState Of(Side s) => s == Side.P ? P : B;
        public static Side Other(Side s) => s == Side.P ? Side.B : Side.P;
        public SideState Opponent(Side s) => Of(Other(s));

        public void Say(string line) => Log.Insert(0, line);
    }
}

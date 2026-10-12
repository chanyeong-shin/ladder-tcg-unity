using System;
using System.Collections.Generic;

namespace LadderBattle.Core
{
    /// <summary>카드 한 장을 내는 행동.</summary>
    public struct Act
    {
        public Rules.CardId Card;
        public int Lane;    // Target.Lane 일 때
        public int Tier;    // Target.Slot 일 때
        public int Left;    // Target.Slot 일 때

        public static Act Lane_(Rules.CardId c, int lane) => new Act { Card = c, Lane = lane, Tier = -1, Left = -1 };
        public static Act Slot_(Rules.CardId c, int tier, int left) => new Act { Card = c, Lane = -1, Tier = tier, Left = left };
        public static Act None_(Rules.CardId c) => new Act { Card = c, Lane = -1, Tier = -1, Left = -1 };
    }

    /// <summary>경로 한 줄. cells[k] 는 k 걸음째의 자리다.</summary>
    public sealed class Trace
    {
        public int EndLane;
        public readonly List<(int y, int lane)> Cells = new List<(int, int)>();
    }

    /// <summary>
    /// 규칙 v1.0 엔진. `ladder-tcg-prototype.html` 의 규칙 부분을 그대로 옮겼다.
    ///
    /// **턴 흐름을 바꾸는 책임은 <see cref="EndAction"/> 한곳뿐이다.**
    /// 10-11 에 봇 함수가 끝에서 직접 차례를 넘기는 바람에 방어 턴이 통째로 사라진 버그를 겪었다.
    /// 그래서 <see cref="Bot"/> 은 카드만 내고 흐름은 건드리지 않는다.
    /// </summary>
    public sealed class GameEngine
    {
        public readonly GameState S = new GameState();
        readonly Random _rng;

        /// <summary>연출이 없는 자리(시험·검증)에서는 true. 턴이 끊김 없이 이어진다.</summary>
        public bool Headless;

        /// <summary>동시 발동이 끝났을 때 불린다. 화면이 연출을 끼워 넣을 자리다.</summary>
        public event Action OnResolved;
        /// <summary>새 턴이 시작됐을 때.</summary>
        public event Action OnTurnStart;

        public GameEngine(int seed = 0)
        {
            _rng = seed == 0 ? new Random() : new Random(seed);
        }

        // ── 덱 ────────────────────────────────────────────────
        /// <summary>덱은 무한이다. 뽑을 때마다 가중치대로 한 장을 고른다 (본인 10-11).</summary>
        Rules.CardId PickCard()
        {
            int r = _rng.Next(Rules.WeightSum);
            foreach (var c in Rules.Cards)
            {
                r -= c.Weight;
                if (r < 0) return c.Id;
            }
            return Rules.CardId.Inject;
        }

        bool Draw(Side side)
        {
            var me = S.Of(side);
            if (me.Hand.Count >= Rules.HandMax) return false;   // 손패 상한이 유일한 제동이다
            me.Hand.Add(PickCard());
            return true;
        }

        // ── 판 시작 ───────────────────────────────────────────
        public void NewGame()
        {
            S.Turn = 0;
            S.Rungs.Clear();
            S.RungId = S.ShellId = S.SecId = 1;
            S.First = _rng.Next(2) == 0 ? Side.P : Side.B;
            S.Step = Step.Idle;
            S.Ended = false;
            S.Result = Result.None;
            S.Blocked.Clear();
            S.Reveal = false;
            S.Log.Clear();
            ResetSide(S.P);
            ResetSide(S.B);

            for (int i = 0; i < 3; i++) { Draw(Side.P); Draw(Side.B); }
            S.Say($"새 판. 양쪽 같은 무한 덱 · 1턴 공격은 {Who(S.First)} (랜덤)");
            StartTurn();
        }

        static void ResetSide(SideState s)
        {
            s.Hand.Clear();
            for (int i = 0; i < Rules.Lanes; i++) s.Ends[i] = Rules.EndHp;
            s.Shells.Clear(); s.Sec.Clear(); s.SecOpen.Clear();
            s.Done = false;
        }

        static string Who(Side s) => s == Side.P ? "나" : "봇";

        public void StartTurn()
        {
            S.Turn++;
            if (S.Turn > Rules.MaxTurn) { EndGame(Judge(), $"{Rules.MaxTurn}턴 종료"); return; }
            if (S.Turn > 1) S.First = GameState.Other(S.First);

            foreach (var side in new[] { Side.P, Side.B })
            {
                var s = S.Of(side);
                s.Shells.Clear(); s.Sec.Clear(); s.SecOpen.Clear(); s.Done = false;
                // 서버가 터질수록 더 뽑는다 — 길이 막힌 쪽이 라우팅 변경을 더 자주 쥔다
                int n = Rules.DrawCount(s.AliveServers);
                for (int i = 0; i < n; i++) Draw(side);
            }
            S.Blocked.Clear();
            S.Reveal = false;

            S.Say($"{S.Turn}턴 · 공격 {Who(S.First)}");
            S.Acting = S.First;
            S.Step = Step.Attack;
            OnTurnStart?.Invoke();
        }

        // ── 카드 사용 ─────────────────────────────────────────
        public bool CanUse(Side side, Rules.CardId card)
        {
            var me = S.Of(side);
            if (!me.Hand.Contains(card)) return false;
            var def = Rules.Of(card);

            switch (card)
            {
                case Rules.CardId.Inject:
                case Rules.CardId.ZeroDay:
                    for (int l = 0; l < Rules.Lanes; l++) if (LaneAlive(side, l)) return true;
                    return false;   // 내 서버가 다 부서졌다 — 놓을 채널이 없다 (송신 노드 규칙)
                case Rules.CardId.Firewall:
                    for (int l = 0; l < Rules.Lanes; l++) if (!S.Blocked.Contains(l)) return true;
                    return false;
                case Rules.CardId.Buffer:
                case Rules.CardId.Overflow:
                    return me.Hand.Count < Rules.HandMax;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 카드 한 장이 실제로 나갔을 때 (편 · 무엇 · 손패에서 몇 번째였나 · 장수 전 · 장수 후).
        ///
        /// 봇은 한 차례에 여러 장을 낸다. 연출이 한 장씩 따라가려면 어느 자리에서
        /// 무엇이 빠졌는지를 알아야 한다. **판정에는 쓰지 않는다.**
        /// </summary>
        public event Action<Side, Act, int, int, int> OnActApplied;

        public bool ApplyAct(Side side, Act act)
        {
            var hand = S.Of(side).Hand;
            int 번호 = hand.IndexOf(act.Card);
            int 장수전 = hand.Count;
            bool 됐다 = ApplyActCore(side, act);
            if (됐다) OnActApplied?.Invoke(side, act, 번호, 장수전, hand.Count);
            return 됐다;
        }

        bool ApplyActCore(Side side, Act act)
        {
            var me = S.Of(side);
            var def = Rules.Of(act.Card);
            int hi = me.Hand.IndexOf(act.Card);
            if (hi < 0) return false;

            // 보안 등급은 효과를 지금 쓰지 않는다. 뒷면으로 깔아 두고 동시 발동에 공개한다.
            // 로그에도 무슨 카드인지 적지 않는다 — "깔렸다"는 사실만 양쪽이 안다.
            if (def.Grade == Rules.Grade.Security)
            {
                if (def.Need == Rules.Target.Lane)
                {
                    if (act.Lane < 0 || act.Lane >= Rules.Lanes) return false;
                    if (act.Card == Rules.CardId.ZeroDay && !LaneAlive(side, act.Lane)) return false;
                    if (act.Card == Rules.CardId.Firewall && S.Blocked.Contains(act.Lane)) return false;
                }
                me.Sec.Add(new SecCard { Id = S.SecId++, Card = act.Card, Lane = def.Need == Rules.Target.Lane ? act.Lane : -1 });
                me.Hand.RemoveAt(hi);
                S.Say($"{Who(side)} 보안 등급 카드를 뒷면으로 깔았다 · 동시 발동에 공개된다");
                return true;
            }

            switch (act.Card)
            {
                case Rules.CardId.Inject:
                {
                    if (!LaneAlive(side, act.Lane)) return false;
                    var sh = AddShell(side, act.Lane, 1);
                    S.Say($"{Who(side)} {def.Label} → 채널 {act.Lane + 1}"
                          + (sh.Power > 1 ? $" · 겹쳐 주입하여 위력 {sh.Power}" : " · 위력 1"));
                    break;
                }
                case Rules.CardId.Reroute:
                {
                    var r = Rewire(side, act.Tier, act.Left);
                    if (r == null) return false;
                    S.Say($"{Who(side)} {def.Label} · {act.Tier}단 {act.Left + 1}-{act.Left + 2} {r}");
                    break;
                }
                case Rules.CardId.Buffer:
                {
                    me.Hand.RemoveAt(hi);           // 먼저 버려야 손패 상한에 걸리지 않는다
                    int n = 0;
                    for (int i = 0; i < 2; i++) if (Draw(side)) n++;
                    S.Say($"{Who(side)} {def.Label} · {n}장 보충");
                    return true;
                }
                default: return false;
            }
            me.Hand.RemoveAt(hi);
            return true;
        }

        // ── 패킷 ──────────────────────────────────────────────
        public bool LaneAlive(Side side, int lane)
            => lane >= 0 && lane < Rules.Lanes && S.Of(side).Ends[lane] > 0;

        Shell AddShell(Side side, int lane, int power)
        {
            var s = S.Of(side);
            var ex = s.Shells.Find(x => x.Lane == lane);
            if (ex != null) { ex.Power += power; return ex; }   // 같은 채널이면 합산한다
            var sh = new Shell { Id = S.ShellId++, Owner = side, Lane = lane, Power = power };
            s.Shells.Add(sh);
            return sh;
        }

        // ── 연결 회선 ─────────────────────────────────────────
        public static bool SlotValid(int tier, int left)
            => tier >= 1 && tier <= Rules.Tiers && left >= 0 && left <= Rules.Lanes - 2;

        /// <summary>빈 자리면 설치, 그 자리에 줄이 있으면 파괴, 같은 단의 다른 자리가 찼으면 불가.</summary>
        public Rung RewirePlan(List<Rung> rungs, int tier, int left, out bool breaking)
        {
            breaking = false;
            if (!SlotValid(tier, left)) return null;
            var here = rungs.Find(r => r.Tier == tier && r.Left == left);
            if (here != null) { breaking = true; return here; }
            if (rungs.Exists(r => r.Tier == tier)) return null;
            return null;    // 설치 가능 (대상 노드 없음)
        }

        public bool CanRewire(List<Rung> rungs, int tier, int left)
        {
            if (!SlotValid(tier, left)) return false;
            if (rungs.Exists(r => r.Tier == tier && r.Left == left)) return true;   // 파괴
            return !rungs.Exists(r => r.Tier == tier);                               // 설치
        }

        string Rewire(Side side, int tier, int left)
        {
            if (!CanRewire(S.Rungs, tier, left)) return null;
            var here = S.Rungs.Find(r => r.Tier == tier && r.Left == left);
            if (here != null) { S.Rungs.Remove(here); return "파괴"; }
            S.Rungs.Add(new Rung { Id = S.RungId++, Tier = tier, Left = left, Owner = side });
            return "설치";
        }

        // ── 경로 ──────────────────────────────────────────────
        public static Trace TraceFrom(Side side, int lane, List<Rung> rungs)
        {
            int dir = side == Side.P ? 1 : -1;
            int y = side == Side.P ? 0 : Rules.Goal;
            var t = new Trace();
            t.Cells.Add((y, lane));
            for (int k = 0; k < Rules.Goal; k++)
            {
                y += dir;
                if (y >= 1 && y <= Rules.Tiers)
                {
                    var r = rungs.Find(x => x.Tier == y && (x.Left == lane || x.Left == lane - 1));
                    if (r != null) lane = (r.Left == lane) ? lane + 1 : lane - 1;
                }
                t.Cells.Add((y, lane));
            }
            t.EndLane = lane;
            return t;
        }

        /// <summary>경로가 한 칸이라도 겹치면 상쇄한다 (기획서 4.2).</summary>
        public static bool CellsHit(Trace a, Trace b)
        {
            foreach (var c in a.Cells)
                foreach (var d in b.Cells)
                    if (c.y == d.y && c.lane == d.lane) return true;
            return false;
        }

        /// <summary>
        /// <see cref="CellsHit"/> 와 판정이 같고, 겹친 **첫 칸**을 함께 준다.
        /// 도는 순서가 같으므로 참·거짓도 늘 같다 — 연출이 쓸 자리를 얻으려고 더한 것이다.
        /// </summary>
        public static bool FirstHit(Trace a, Trace b, out int y, out int lane)
        {
            foreach (var c in a.Cells)
                foreach (var d in b.Cells)
                    if (c.y == d.y && c.lane == d.lane) { y = c.y; lane = c.lane; return true; }
            y = -1; lane = -1;
            return false;
        }

        // ── 연출 기록 ─────────────────────────────────────────
        // 적어 두기만 한다. 여기서 판정을 바꾸면 창 없는 검증과 숫자가 갈라진다

        Fx Fx기록(FxKind kind, Side by, int lane)
        {
            var fx = new Fx { Kind = kind, By = by, Lane = lane, EndLane = lane };
            S.Fx.Add(fx);
            return fx;
        }

        void Fx겹침(FxKind kind, Side 이긴쪽, int y, int lane, int 내위력, int 상대위력)
        {
            var fx = Fx기록(kind, 이긴쪽, lane);
            fx.Y = y; fx.Power = 내위력; fx.Power2 = 상대위력;
        }

        // ── 턴 진행 ───────────────────────────────────────────
        public void EndAction(Side side)
        {
            S.Of(side).Done = true;
            if (S.Step == Step.Attack)
            {
                S.Step = Step.Defend;
                S.Acting = GameState.Other(side);
            }
            else
            {
                Resolve();
            }
        }

        // ── 동시 발동 ─────────────────────────────────────────
        public void Resolve()
        {
            S.Reveal = true;
            S.Step = Step.Resolve;
            S.Fx.Clear();

            // 0) 보안 카드 공개 — 경로 계산보다 먼저 해야 제로데이가 그 턴에 날아가고
            //    방화벽이 그 턴에 막는다
            foreach (var side in new[] { Side.P, Side.B })
            {
                var s = S.Of(side);
                s.SecOpen.Clear();
                if (s.Sec.Count == 0) continue;
                s.SecOpen.AddRange(s.Sec);
                foreach (var c in s.Sec)
                {
                    var def = Rules.Of(c.Card);
                    S.Say($"{Who(side)} 보안 등급 공개 · {def.Label}");
                    switch (c.Card)
                    {
                        case Rules.CardId.Firewall:
                            if (!S.Blocked.Contains(c.Lane)) S.Blocked.Add(c.Lane);
                            break;
                        case Rules.CardId.ZeroDay:
                            if (!LaneAlive(side, c.Lane)) { S.Say($"  · 채널 {c.Lane + 1} 서버가 부서져 발사 불가"); break; }
                            var sh = AddShell(side, c.Lane, 1);
                            sh.Pierce = true;
                            S.Say($"  · 채널 {c.Lane + 1} 위력 {sh.Power} · 상쇄되지 않고 통과한다");
                            break;
                        case Rules.CardId.Overflow:
                            int n = 0;
                            for (int i = 0; i < 4; i++) if (Draw(side)) n++;
                            S.Say($"  · {n}장 보충");
                            break;
                    }
                }
                s.Sec.Clear();
            }

            // 1) 경로 계산
            var all = new List<(Shell sh, Side side, Trace tr, bool dead)>();
            foreach (var side in new[] { Side.P, Side.B })
                foreach (var sh in S.Of(side).Shells)
                    all.Add((sh, side, TraceFrom(side, sh.Lane, S.Rungs), false));

            // 연출 기록 — 어느 패킷이 어느 길로 가는지. 화면은 이것만 보고 빛줄기를 달린다
            foreach (var a in all)
            {
                var fx = Fx기록(FxKind.발사, a.side, a.sh.Lane);
                fx.EndLane = a.tr.EndLane;
                fx.Power = a.sh.Power;
                fx.Pierce = a.sh.Pierce;
                fx.Cells.AddRange(a.tr.Cells);
            }

            var dead = new HashSet<int>();   // shell id

            // 2) 방화벽 — 지나는 채널이 차단되어 있으면 소멸
            int cut = 0;
            foreach (var a in all)
            {
                foreach (var c in a.tr.Cells)
                    if (S.Blocked.Contains(c.lane))
                    {
                        dead.Add(a.sh.Id); cut++;
                        var fx = Fx기록(FxKind.차단, a.side, a.sh.Lane);
                        fx.Y = c.y; fx.EndLane = c.lane;
                        break;
                    }
            }
            if (cut > 0) S.Say($"방화벽 · 패킷 {cut}개 DROP");

            // 3) 상쇄 — 경로가 겹치는 양측 패킷끼리 위력 차감
            var ps = all.FindAll(a => a.side == Side.P && !dead.Contains(a.sh.Id));
            var bs = all.FindAll(a => a.side == Side.B && !dead.Contains(a.sh.Id));
            foreach (var p in ps)
            {
                if (dead.Contains(p.sh.Id)) continue;
                foreach (var b in bs)
                {
                    if (dead.Contains(p.sh.Id) || dead.Contains(b.sh.Id)) continue;
                    // 겹치는 첫 칸을 같이 받는다. 섬광을 어디에 띄울지는 그 칸이 정한다
                    if (!FirstHit(p.tr, b.tr, out int hy, out int hlane)) continue;
                    if (p.sh.Pierce && !b.sh.Pierce)
                    {
                        dead.Add(b.sh.Id); S.Say("제로데이 · 나의 패킷이 상대 패킷을 뚫었다");
                        Fx겹침(FxKind.관통, Side.P, hy, hlane, p.sh.Power, b.sh.Power); continue;
                    }
                    if (b.sh.Pierce && !p.sh.Pierce)
                    {
                        dead.Add(p.sh.Id); S.Say("제로데이 · 상대 패킷이 내 패킷을 뚫었다");
                        Fx겹침(FxKind.관통, Side.B, hy, hlane, b.sh.Power, p.sh.Power); continue;
                    }
                    int pw = p.sh.Power, bw = b.sh.Power;
                    if (pw == bw) { dead.Add(p.sh.Id); dead.Add(b.sh.Id); S.Say($"상쇄 · {pw} 대 {bw}, 둘 다 소멸"); }
                    else if (pw > bw) { p.sh.Power = pw - bw; dead.Add(b.sh.Id); S.Say($"상쇄 · {pw} 대 {bw} → 내 패킷 {p.sh.Power} 남아 진행"); }
                    else { b.sh.Power = bw - pw; dead.Add(p.sh.Id); S.Say($"상쇄 · {pw} 대 {bw} → 상대 패킷 {b.sh.Power} 남아 진행"); }
                    Fx겹침(FxKind.상쇄, pw >= bw ? Side.P : Side.B, hy, hlane, pw, bw);
                }
            }

            // 4) 착탄
            foreach (var a in all)
                if (!dead.Contains(a.sh.Id)) Impact(a.sh, a.side, a.tr.EndLane);

            // 5) 뒷정리
            S.P.Shells.Clear(); S.B.Shells.Clear();
            if (!S.Ended) CheckWin();

            OnResolved?.Invoke();
            if (Headless && !S.Ended) StartTurn();
        }

        void Impact(Shell sh, Side side, int lane)
        {
            var tg = GameState.Other(side);
            var T = S.Of(tg);
            if (T.Ends[lane] <= 0)
            {
                S.Say($"{Who(side)} 패킷이 이미 부서진 채널 {lane + 1}에 헛방");
                Fx기록(FxKind.헛방, side, lane);
                return;
            }
            HitCore(tg, lane, sh.Power, true);
        }

        void HitCore(Side tg, int lane, int dmg, bool first)
        {
            var T = S.Of(tg);
            if (lane < 0 || lane >= Rules.Lanes || T.Ends[lane] <= 0 || dmg <= 0) return;
            int before = T.Ends[lane];
            T.Ends[lane] = Math.Max(0, before - dmg);
            S.Say($"{Who(GameState.Other(tg))} 착탄 · 채널 {lane + 1} 서버 {dmg} 피해"
                  + (T.Ends[lane] == 0 ? " 파괴" : $" (남은 보안도 {T.Ends[lane]})"));

            // 맞은 쪽이 아니라 **쏜 쪽**을 By 에 적는다. 화면은 색을 쏜 쪽으로 칠한다
            var hit = Fx기록(first ? (T.Ends[lane] == 0 ? FxKind.파괴 : FxKind.착탄) : FxKind.번짐,
                             GameState.Other(tg), lane);
            hit.Power = dmg;
            hit.Pierce = T.Ends[lane] == 0;   // 이 한 방으로 떨어졌나
            if (first && dmg > before)
            {
                int half = (dmg - before) / 2;
                if (half > 0)
                {
                    HitCore(tg, lane - 1, half, false);
                    HitCore(tg, lane + 1, half, false);
                    S.Say($"넘친 피해 {half}씩이 양옆으로 번졌다");
                }
            }
        }

        // ── 승패 ──────────────────────────────────────────────
        void CheckWin()
        {
            bool pd = S.P.AliveServers == 0, bd = S.B.AliveServers == 0;
            if (pd && bd) EndGame(Result.Draw, "양쪽 전멸");
            else if (bd) EndGame(Result.PlayerWin, "상대 서버 전부 파괴");
            else if (pd) EndGame(Result.BotWin, "내 서버 전부 파괴");
        }

        /// <summary>턴 제한 판정 — 기획서 §5: 부순 서버 수 → 같으면 남은 보안도 합 → 같으면 무승부.</summary>
        public Result Judge()
        {
            int pa = S.P.AliveServers, ba = S.B.AliveServers;
            if (ba != pa) return ba < pa ? Result.PlayerWin : Result.BotWin;
            int pSum = S.P.SecurityLeft, bSum = S.B.SecurityLeft;
            if (pSum == bSum) return Result.Draw;
            return pSum > bSum ? Result.PlayerWin : Result.BotWin;
        }

        void EndGame(Result res, string why)
        {
            S.Ended = true;
            S.Result = res;
            S.Step = Step.End;
            string name = res == Result.Draw ? "무승부" : res == Result.PlayerWin ? "내 승리" : "봇 승리";
            S.Say($"{name} · {why}");
            OnResolved?.Invoke();
        }
    }
}

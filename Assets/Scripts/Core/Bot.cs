using System;
using System.Collections.Generic;

namespace LadderBattle.Core
{
    /// <summary>
    /// 봇. 덱이 하나뿐이므로 **이 판단기가 양쪽을 돌린다**.
    /// 그래서 봇 대 봇 측정의 승률 기준선이 50%이고, 벗어난 만큼이 규칙의 편향이다.
    ///
    /// **턴 흐름은 건드리지 않는다.** 카드만 내고 끝낸다.
    /// (10-11에 봇이 직접 차례를 넘기는 바람에 방어 턴이 사라지는 버그를 겪었다.)
    /// </summary>
    public sealed class Bot
    {
        readonly GameEngine _e;
        readonly Random _rng;

        public Bot(GameEngine engine, int seed = 0)
        {
            _e = engine;
            _rng = seed == 0 ? new Random() : new Random(seed);
        }

        struct Plan { public int Lane; public int Need; }

        public void TakeTurn(Side side)
        {
            var S = _e.S;
            var me = S.Of(side);
            var op = S.Opponent(side);
            var opSide = GameState.Other(side);
            bool defending = S.Step == Step.Defend;

            int Left(Rules.CardId c) { int n = 0; foreach (var x in me.Hand) if (x == c) n++; return n; }
            bool Has(Rules.CardId c) => Left(c) > 0;
            bool Act(Act a) => _e.ApplyAct(side, a);

            // ① 손패 보충 — 버퍼는 비용이 없다. 얇을 때만 쓴다
            if (Has(Rules.CardId.Overflow) && me.Hand.Count <= 2 && _e.CanUse(side, Rules.CardId.Overflow))
                Act(Core.Act.None_(Rules.CardId.Overflow));
            while (Has(Rules.CardId.Buffer) && me.Hand.Count <= 3 && _e.CanUse(side, Rules.CardId.Buffer))
                if (!Act(Core.Act.None_(Rules.CardId.Buffer))) break;

            // 어느 채널에서 가장 싸게 서버를 깨나. rungs 가 바뀌면 값이 달라지므로 쓸 때마다 다시 센다
            List<Plan> MakePlan()
            {
                var outp = new List<Plan>();
                for (int l = 0; l < Rules.Lanes; l++)
                {
                    if (!_e.LaneAlive(side, l)) continue;           // 송신 노드 규칙
                    int e = GameEngine.TraceFrom(side, l, S.Rungs).EndLane;
                    int hp = op.Ends[e];
                    if (hp <= 0) continue;                          // 이미 부서진 서버 — 헛방이다
                    outp.Add(new Plan { Lane = l, Need = hp });
                }
                // need 가 같은 채널끼리는 무작위로 섞는다.
                // 섞지 않으면 양쪽 봇이 늘 채널 1을 골라 매 턴 정면으로 박고 전부 상쇄된다.
                for (int i = outp.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    (outp[i], outp[j]) = (outp[j], outp[i]);
                }
                outp.Sort((a, b) => a.Need.CompareTo(b.Need));
                return outp;
            }

            // ② 방어 턴 — 내 서버를 깰 상대 패킷부터 막는다
            if (defending)
            {
                var kill = new List<(Shell sh, int end)>();
                foreach (var sh in op.Shells)
                {
                    int e = GameEngine.TraceFrom(opSide, sh.Lane, S.Rungs).EndLane;
                    if (me.Ends[e] > 0 && sh.Power >= me.Ends[e]) kill.Add((sh, e));
                }
                kill.Sort((a, b) => b.sh.Power.CompareTo(a.sh.Power));

                if (kill.Count > 0)
                {
                    bool fixedIt = false;

                    // ②-1 라우팅 변경으로 경로를 튼다 — 위협이 전부 빗나가는 자리를 찾는다
                    if (Has(Rules.CardId.Reroute))
                    {
                        for (int t = 1; t <= Rules.Tiers && !fixedIt; t++)
                        for (int lf = 0; lf <= Rules.Lanes - 2 && !fixedIt; lf++)
                        {
                            if (!_e.CanRewire(S.Rungs, t, lf)) continue;
                            var test = new List<Rung>(S.Rungs);
                            var here = test.Find(r => r.Tier == t && r.Left == lf);
                            if (here != null) test.Remove(here);
                            else test.Add(new Rung { Id = -1, Tier = t, Left = lf, Owner = side });

                            bool still = false;
                            foreach (var x in kill)
                            {
                                int e = GameEngine.TraceFrom(opSide, x.sh.Lane, test).EndLane;
                                if (me.Ends[e] > 0 && x.sh.Power >= me.Ends[e]) { still = true; break; }
                            }
                            if (!still) fixedIt = Act(Core.Act.Slot_(Rules.CardId.Reroute, t, lf));
                        }
                    }

                    // ②-2 못 틀면 방화벽으로 그 경로를 끊는다. 내가 쏠 채널은 피한다
                    if (!fixedIt && Has(Rules.CardId.Firewall))
                    {
                        var mine = MakePlan();
                        var path = GameEngine.TraceFrom(opSide, kill[0].sh.Lane, S.Rungs).Cells;
                        foreach (var c in path)
                        {
                            if (S.Blocked.Contains(c.lane)) continue;
                            if (mine.Exists(p => p.Lane == c.lane)) continue;
                            fixedIt = Act(Core.Act.Lane_(Rules.CardId.Firewall, c.lane));
                            if (fixedIt) break;
                        }
                    }

                    // ②-3 그래도 못 막으면 같은 경로에 코드 주입을 겹쳐 상쇄한다.
                    //      공격 카드가 곧 방패다 — 그게 이 게임의 중심이다
                    if (!fixedIt && Has(Rules.CardId.Inject))
                    {
                        var tgtTr = GameEngine.TraceFrom(opSide, kill[0].sh.Lane, S.Rungs);
                        for (int l = 0; l < Rules.Lanes && !fixedIt; l++)
                        {
                            if (!_e.LaneAlive(side, l)) continue;
                            if (!GameEngine.CellsHit(GameEngine.TraceFrom(side, l, S.Rungs), tgtTr)) continue;
                            var have = me.Shells.Find(x => x.Lane == l);
                            int pw = have?.Power ?? 0;
                            while (Left(Rules.CardId.Inject) > 0 && pw < kill[0].sh.Power)
                            {
                                if (!Act(Core.Act.Lane_(Rules.CardId.Inject, l))) break;
                                pw++;
                            }
                            fixedIt = pw >= kill[0].sh.Power;
                        }
                    }
                }
            }

            // ③ 공격 — 가장 싸게 깰 수 있는 채널에 필요한 만큼 겹쳐 낸다
            {
                var plan = MakePlan();
                if (plan.Count > 0 && Has(Rules.CardId.Inject))
                {
                    var tg = plan[0];
                    var have = me.Shells.Find(x => x.Lane == tg.Lane);
                    int pw = have?.Power ?? 0;
                    while (Left(Rules.CardId.Inject) > 0 && pw < tg.Need)
                    {
                        if (!Act(Core.Act.Lane_(Rules.CardId.Inject, tg.Lane))) break;
                        pw++;
                    }
                }
            }

            // ④ 제로데이 — 상쇄를 뚫는다. 공격 턴에 깔면 상대가 어느 채널인지 모른다
            if (Has(Rules.CardId.ZeroDay))
            {
                var plan = MakePlan();
                if (plan.Count > 0) Act(Core.Act.Lane_(Rules.CardId.ZeroDay, plan[0].Lane));
            }

            // ⑤ 남은 라우팅 변경 — 내 패킷이 더 아프게 되는 자리만
            if (Has(Rules.CardId.Reroute) && me.Shells.Count > 0)
            {
                int Gain(List<Rung> rungs)
                {
                    int sum = 0;
                    foreach (var sh in me.Shells)
                    {
                        int e = GameEngine.TraceFrom(side, sh.Lane, rungs).EndLane;
                        int hp = op.Ends[e];
                        if (hp > 0) sum += Math.Min(sh.Power, hp);
                    }
                    return sum;
                }
                int now = Gain(S.Rungs);
                bool moved = false;
                for (int t = 1; t <= Rules.Tiers && !moved; t++)
                for (int lf = 0; lf <= Rules.Lanes - 2 && !moved; lf++)
                {
                    if (!_e.CanRewire(S.Rungs, t, lf)) continue;
                    var test = new List<Rung>(S.Rungs);
                    var here = test.Find(r => r.Tier == t && r.Left == lf);
                    if (here != null) test.Remove(here);
                    else test.Add(new Rung { Id = -1, Tier = t, Left = lf, Owner = side });
                    if (Gain(test) > now) moved = Act(Core.Act.Slot_(Rules.CardId.Reroute, t, lf));
                }
            }

            me.Done = true;
        }
    }
}

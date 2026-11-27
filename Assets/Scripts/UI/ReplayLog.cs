using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Breach.UI {
 // Week 12 item 10 (10_resultlog.js "replay"): the battle record with the target of every action (channel, rung row/edge/rider),
 // and the board state after each line, so the result screen can replay it on the real board pieces.
 // Kinds as in the HTML LOG: round · me · enemy (actions) · reveal · event · hit (simultaneous activation).
 // Action and round lines copy the live Battle state; reveal/event/hit lines are rebuilt from the state before Battle.Resolve with
 // Battle.Route and the Resolve collision order (like the HTML RES), and every event/hit sentence is compared with Battle.Resolve's own.
 public sealed class ReplayLog {
  public sealed class Shot {
   public int O, S, End, Initial, Power; public bool Pierce, Blocked; public List<Cell> Cells; public List<Vector2> Pts; public float Len, Split; public Vector2? Contact;
   public int EventIdx = -1, HitIdx = -1;
  }
  public sealed class Ev { public string T, Cap; public List<Shot> Shots = new List<Shot>(); public bool Hit; }
  public sealed class Snap {
   public int[,] H = new int[2, 3], P = new int[2, 3]; public bool[,] Z = new bool[2, 3]; public int[] Rungs = new int[3], Riders = new int[3]; public bool[] Walls = new bool[3];
   public int[] Pend = new int[2]; public List<Shot> Shots = new List<Shot>(); public List<Vector2> Gates = new List<Vector2>();
   public Snap Clone() { var s = (Snap)MemberwiseClone(); s.H = (int[,])H.Clone(); s.P = (int[,])P.Clone(); s.Z = (bool[,])Z.Clone(); s.Rungs = (int[])Rungs.Clone(); s.Riders = (int[])Riders.Clone(); s.Walls = (bool[])Walls.Clone(); s.Pend = (int[])Pend.Clone(); return s; }
  }
  public sealed class Step {
   public int R; public string K, T; public int C = -1, Ch = -1, Row = -1, Edge = -1, Rider = -1, Who = -1, Att = -1; public bool Removed; public char G = ' ';
   public Snap S; public Ev E;
  }
  public readonly List<Step> Steps = new List<Step>();
  public readonly List<string> Mismatch = new List<string>();
  int round = 1; Snap before; List<Secret> hidden;
  public void Clear() { Steps.Clear(); Mismatch.Clear(); round = 1; }

  static Snap Capture(Battle b) {
   var s = new Snap(); s.H = (int[,])b.Health.Clone(); s.P = (int[,])b.Power.Clone(); s.Z = (bool[,])b.Pierce.Clone(); s.Rungs = (int[])b.Rungs.Clone(); s.Riders = (int[])b.RungRiders.Clone(); s.Walls = (bool[])b.Walls.Clone();
   s.Pend[0] = b.Hidden.Count(h => h.Owner == 0); s.Pend[1] = b.Hidden.Count(h => h.Owner == 1); s.Gates = Gates(s); return s;
  }
  static Battle Board(Snap s) { var b = new Battle(1, 0); b.Rungs = (int[])s.Rungs.Clone(); b.RungRiders = (int[])s.Riders.Clone(); b.Walls = (bool[])s.Walls.Clone(); b.Health = (int[,])s.H.Clone(); return b; }
  static List<Vector2> Gates(Snap s) { return CombatPresentation.FirewallEndpoints(Board(s)).ToList(); }

  public void Round(Battle b, string text) { round = b.Round; Steps.Add(new Step { R = round, K = "round", T = text, Att = b.Attacker, S = Capture(b) }); }
  public void Action(Battle b, int owner, Card card, int target, string text) {
   var st = new Step { R = round, K = owner == 0 ? "me" : "enemy", T = text, C = (int)card, Who = owner, S = Capture(b) };
   if (card == Card.Inject || card == Card.ZeroDay || card == Card.Firewall) st.Ch = target + 1;
   else if (card == Card.Reroute) { int slot = Battle.Slot(target); st.Row = slot / 2; st.Edge = slot % 2; st.Removed = b.Rungs[st.Row] < 0; st.Rider = st.Removed ? -1 : b.RungRiders[st.Row]; }
   Steps.Add(st);
  }
  public void UndoLast() { for (int i = Steps.Count - 1; i >= 0; i--) if (Steps[i].K == "me") { Steps.RemoveAt(i); return; } }
  public void BeforeResolve(Battle b) { before = Capture(b); hidden = new List<Secret>(b.Hidden); }

  // 10_resultlog.js RES: reveal lines apply their card, then resolve() on that state; event/hit lines follow its order
  public void AfterResolve(Battle b, Resolution r) {
   if (before == null) return; var st = before.Clone(); st.Shots = new List<Shot>();
   foreach (var s in hidden) {
    st.Pend[s.Owner] = Mathf.Max(0, st.Pend[s.Owner] - 1);
    if (s.Card == Card.ZeroDay) { st.P[s.Owner, s.Target]++; st.Z[s.Owner, s.Target] = true; } else if (s.Card == Card.Firewall) st.Walls[s.Target] = true;
    st.Gates = Gates(st);
    Steps.Add(new Step { R = round, K = "reveal", T = (s.Owner == 0 ? "나" : "상대") + " · " + Cards.Names[(int)s.Card] + " 공개", C = (int)s.Card, Who = s.Owner, Ch = s.Card == Card.Overflow ? -1 : s.Target + 1, S = st.Clone() });
   }
   var shots = new List<Shot>(); var events = Resolve(st, shots); st.Shots = shots;
   var real = r.Events.Where(e => !e.EndsWith(" 공개", System.StringComparison.Ordinal)).ToList();
   if (real.Count != events.Count) Mismatch.Add("round " + round + ": " + events.Count + " lines rebuilt, Battle.Resolve " + real.Count);
   for (int i = 0; i < Mathf.Min(real.Count, events.Count); i++) if (real[i] != events[i].T) Mismatch.Add("round " + round + ": '" + events[i].T + "' vs '" + real[i] + "'");
   foreach (var ev in events) {
    int idx = Steps.Count; foreach (var s in ev.Shots) { if (ev.Hit) s.HitIdx = idx; else if (s.EventIdx < 0) s.EventIdx = idx; }
    if (ev.Hit) st.H[1 - ev.Shots[0].O, ev.Shots[0].End] = 0;
    var step = new Step { R = round, K = ev.Hit ? "hit" : "event", T = ev.T, E = ev, S = st.Clone() };
    if (ev.Hit) { step.Who = 1 - ev.Shots[0].O; step.Ch = ev.Shots[0].End + 1; } else step.G = ev.T.StartsWith("공격 상쇄") ? '=' : ev.T.StartsWith("방화벽") ? '×' : '»';
    Steps.Add(step);
   }
   before = null;
  }
  static List<Ev> Resolve(Snap st, List<Shot> shots) {
   var events = new List<Ev>(); var board = Board(st);
   for (int p = 0; p < 2; p++) for (int c = 0; c < 3; c++) if (st.P[p, c] > 0) {
    var pk = board.Route(p, c); var s = new Shot { O = p, S = c, End = pk.End, Cells = pk.Cells, Initial = st.P[p, c], Power = st.P[p, c], Pierce = st.Z[p, c] }; s.Pts = BattleController.Points(pk);
    if (s.Cells.Any(x => st.Walls[x.Lane])) { s.Blocked = true; s.Power = 0; var e = new Ev { T = "방화벽 · 패킷 소멸", Cap = "서버 앞 차단" }; e.Shots.Add(s); events.Add(e); }
    shots.Add(s);
   }
   foreach (var a in shots.Where(x => x.O == 0).ToList()) foreach (var bb in shots.Where(x => x.O == 1).ToList()) {
    if (a.Power <= 0 || bb.Power <= 0 || !a.Cells.Any(x => bb.Cells.Any(y => x.Row == y.Row && x.Lane == y.Lane))) continue; string t, cap;
    if (a.Pierce && !bb.Pierce) { bb.Power = 0; t = "제로데이 · 상대 패킷 관통"; cap = "관통"; }
    else if (bb.Pierce && !a.Pierce) { a.Power = 0; t = "제로데이 · 내 패킷 관통"; cap = "관통"; }
    else { int n = Mathf.Min(a.Power, bb.Power); a.Power -= n; bb.Power -= n; t = "공격 상쇄 · " + n; cap = "공격 상쇄"; }
    var cell = a.Cells.Where(x => bb.Cells.Any(y => x.Row == y.Row && x.Lane == y.Lane)).OrderBy(x => Mathf.Abs(x.Row - 1)).ThenBy(x => x.Lane).First();
    a.Contact = bb.Contact = new Vector2(BattleController.LaneX[cell.Lane], cell.Row < 0 ? 201 : cell.Row > 2 ? 777 : BattleController.RowY[cell.Row]);
    var ev = new Ev { T = t, Cap = cap }; ev.Shots.Add(a); ev.Shots.Add(bb); events.Add(ev);
   }
   foreach (var s in shots) { if (s.Power <= 0) continue; int tg = 1 - s.O; if (st.H[tg, s.End] <= 0) continue; var ev = new Ev { T = (tg == 0 ? "내" : "상대") + " 서버 " + (s.End + 1) + " · 피해 " + s.Power, Hit = true }; ev.Shots.Add(s); events.Add(ev); }
   foreach (var s in shots) { if (s.Blocked) { int rc = 1 - s.O; s.Contact = new Vector2(BattleController.LaneX[s.End], (rc == 0 ? 686 : 292) + (rc == 0 ? -26.4f : 26.4f)); } s.Split = s.Contact.HasValue ? Fraction(s.Pts, s.Contact.Value) : 0; s.Len = PathMath.Length(s.Pts); }
   return events;
  }
  static float Fraction(List<Vector2> pts, Vector2 p) { float L = PathMath.Length(pts), done = 0; for (int j = 1; j < pts.Count; j++) { float n = Vector2.Distance(pts[j - 1], pts[j]); if (Mathf.Abs(Vector2.Distance(p, pts[j - 1]) + Vector2.Distance(pts[j], p) - n) < .1f) return (done + Vector2.Distance(p, pts[j - 1])) / Mathf.Max(1, L); done += n; } return 1; }

  // Week12Check: the HTML LOG (3 rounds, win) played on a real Battle. Hands are set to the card before each play.
  public static Battle Script(ReplayLog log, out Resolution last) {
   var b = new Battle(7, 0); log.Clear(); Resolution final = null;
   foreach (var h in b.Hands) h.Clear(); b.Drawn[0].Clear(); b.Drawn[1].Clear();
   log.Round(b, "접속 완료 · 내 공격 턴");
   void Play(int o, Card c, int target) { b.Hands[o].Clear(); b.Hands[o].Add(c); b.DrawUsed = false; if (!b.Play(o, 0, target)) throw new System.Exception("script play failed " + c); log.Action(b, o, c, target, o == 0 ? "나 · " + Cards.Names[(int)c] : "상대 · " + Cards.Names[(int)c] + (Cards.Enhanced(c) ? " (발동 때 적용)" : "")); }
   int Rung(int row, int edge, int rider, int o) { return Battle.RungTarget(row * 2 + edge, rider < 0 ? Battle.Both : rider == o ? Battle.Mine : Battle.Theirs); }
   void Resolve() { b.EndAction(); log.BeforeResolve(b); final = b.Resolve(); log.AfterResolve(b, final); }
   void Next() { b.NextRound(); log.Round(b, "ROUND " + b.Round + " · " + (b.Attacker == 0 ? "내" : "상대") + " 공격 턴"); }
   // round 1 (me attack)
   Play(0, Card.Inject, 1); Play(0, Card.Inject, 1); Play(0, Card.Reroute, Rung(2, 0, 1, 0)); Play(0, Card.Buffer, 0); b.EndAction();
   Play(1, Card.Inject, 0); Play(1, Card.Firewall, 2); Resolve(); Next();
   // round 2 (enemy attack)
   Play(1, Card.Inject, 2); Play(1, Card.Reroute, Rung(1, 0, 1, 1)); Play(1, Card.ZeroDay, 0); b.EndAction();
   Play(0, Card.Firewall, 2); Play(0, Card.Inject, 0); Resolve(); Next();
   // round 3 (me attack)
   Play(0, Card.Overflow, 0); Play(0, Card.Inject, 1); Play(0, Card.Reroute, Rung(0, 0, 0, 0)); Play(0, Card.ZeroDay, 2); Play(0, Card.Inject, 2); b.EndAction();
   Play(1, Card.Inject, 2); Resolve();
   foreach (var h in b.Hands) h.Clear(); last = final;
   return b;
  }
 }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 // Week 13 (2026-11-27) drop target hint while a card is dragged. Source: _도구/시안13주차/tabs/05_drop.js, variant mkMix('badge')
 // ("4+5 합침" + "DELETE E 커서 배지"). Only attack cards (Inject / ZeroDay) and rung cards (Reroute) use it; other cards keep the
 // old hints in BattleController.RefreshTargets.
 //  - pick-up (.25 s): the board (582,129,800,716) darkens 62% except round holes (r 12) over the legal targets
 //    (attack = lane column (cx-34, 175, 68, 640), rung = the 260 x 84 slot rect; the existing rung's own slot is legal = remove).
 //  - a circuit trace grows from the card (.35 s, .06 s stagger) to every legal target; the one nearest the pointer is 2.5 px 85%
 //    with 4 fast dots, the others 1.4 px 22% with 2 slow dots; 8 px pad at the target end.
 //  - hover: white hole outline 2.5 px 90% + 10% fill; attack = 6 pads around the server, cyan glow, gold route;
 //    empty slot = gold 6 px + glowing end pads; existing rung = red 6 px blinking 12 Hz + DELETE cursor badge at the card's
 //    bottom-right corner; illegal slot = red X and the card tinted red 35%.
 // Times are unscaled seconds since the pick-up (not FxClock), like the other UI hints. Nothing is drawn while no card is dragged.
 public sealed class DropTargetFx : MonoBehaviour {
  static readonly Color Cyan = C(56, 217, 255), Red = C(255, 59, 87), Gold = C(255, 196, 56), White = Color.white;
  static Color C(int r, int g, int b, float a = 1) { return new Color(r / 255f, g / 255f, b / 255f, a); }
  static Color A(Color c, float a) { c.a = a; return c; }
  static float EOut(float t) { t = Mathf.Clamp01(t); return 1 - (1 - t) * (1 - t) * (1 - t); }
  const float ServerY = 777; // my server row (02_shot.js YM)

  BattleController host; FxPainter under, over; RectTransform overRoot; TextMeshProUGUI deleteText;
  CardView lastCard; float t; bool drawn;

  struct Target { public int i; public bool legal, remove, attack, wall; public Rect hole; public Vector2 center, a, b; }
  readonly List<Target> targets = new List<Target>();

  public void Initialize(BattleController c) {
   host = c;
   // under the dragged card: above the servers (so they darken too) and the path preview
   under = FxPainter.Create("놓는 곳 표시 - 판 어둡게와 회로", c.game.transform);
   int above = 0; foreach (var s in c.servers) if (s) above = Mathf.Max(above, s.transform.GetSiblingIndex());
   under.transform.SetSiblingIndex(above + 1);
   // above the dragged card (it is moved to the stage's last sibling in BeginDrag): DELETE badge and the red tint
   overRoot = Ui.Rect("놓는 곳 표시 - 카드 위 배지", c.stage, 0, 0, 1920, 1080);
   over = FxPainter.Create("배지 그림", overRoot);
   deleteText = Ui.Text("DELETE", overRoot, 0, 0, 82, 24, "DELETE", c.font, 16, Color.Lerp(Red, White, .35f));
   deleteText.fontStyle = FontStyles.Bold; deleteText.alignment = TextAlignmentOptions.Center; deleteText.textWrappingMode = TextWrappingModes.NoWrap;
   deleteText.rectTransform.pivot = new Vector2(.5f, .5f); deleteText.gameObject.SetActive(false);
  }

  void LateUpdate() {
   if (!host || !under) return;
   var card = host.DraggedView; int index = host.DragIndex; var st = host.State;
   bool on = card && st != null && index >= 0 && index < st.Hands[0].Count && host.game.activeSelf;
   Card kind = on ? st.Hands[0][index] : Card.Buffer;
   // 11-28 사용자 "방화벽 카드도 들면 공격 카드 든것처럼 연출있으면 좋겠음": 방화벽도 채널 기둥 구멍·회로선, 올리면 차단선과 양쪽 서버 문 자리
   bool wall = kind == Card.Firewall, attack = kind == Card.Inject || kind == Card.ZeroDay, rung = kind == Card.Reroute;
   if (!on || !(attack || rung || wall)) { Hide(); lastCard = null; return; }
   if (card != lastCard) { lastCard = card; t = 0; } else t += Time.unscaledDeltaTime;
   Draw(st, index, attack, wall, card);
  }

  void Hide() {
   if (!drawn) return; drawn = false;
   under.Begin(); under.End(); over.Begin(); over.End(); deleteText.gameObject.SetActive(false);
  }

  void Draw(Battle st, int index, bool attack, bool wall, CardView card) {
   drawn = true; float lift = Mathf.Clamp01(t / .25f);
   Vector2 p = host.DragStagePoint; Rect board = BattleController.BoardRect; bool overBoard = board.Contains(p);
   Rect cardRect = StageRect((RectTransform)card.transform);
   // targets (same geometry as Drop / AmmoDropTarget)
   targets.Clear();
   if (attack) for (int i = 0; i < 3; i++) { float cx = BattleController.LaneX[i]; targets.Add(new Target { i = i, attack = true, legal = st.CanPlay(0, index, i), hole = new Rect(cx - 34, 175, 68, 640), center = new Vector2(cx, ServerY) }); }
   else if (wall) for (int i = 0; i < 3; i++) { float cx = BattleController.LaneX[i]; targets.Add(new Target { i = i, wall = true, legal = st.CanPlay(0, index, i), hole = new Rect(cx - 34, 175, 68, 640), center = new Vector2(cx, 487), a = CombatPresentation.FirewallPoint(1, i), b = CombatPresentation.FirewallPoint(0, i) }); }
   else for (int s = 0; s < 6; s++) {
    float x = BattleController.LaneX[s % 2], y = BattleController.RowY[s / 2]; bool legal = st.CanPlay(0, index, s);
    targets.Add(new Target { i = s, legal = legal, remove = legal && st.Rungs[s / 2] == s % 2, hole = new Rect(x + 10, y - 42, 260, 84), center = new Vector2(x + 140, y), a = new Vector2(x, y), b = new Vector2(BattleController.LaneX[s % 2 + 1], y) });
   }
   int hover = -1;
   if (attack) { if (p.y >= 231 && p.y <= 832) { int lane = 0; for (int i = 1; i < 3; i++) if (Mathf.Abs(p.x - BattleController.LaneX[i]) < Mathf.Abs(p.x - BattleController.LaneX[lane])) lane = i; if (Mathf.Abs(p.x - BattleController.LaneX[lane]) <= (p.y >= 722 ? 50 : 28)) hover = lane; } }
   else if (wall) { if (p.y >= 175 && p.y <= 815) { int lane = 0; for (int i = 1; i < 3; i++) if (Mathf.Abs(p.x - BattleController.LaneX[i]) < Mathf.Abs(p.x - BattleController.LaneX[lane])) lane = i; if (Mathf.Abs(p.x - BattleController.LaneX[lane]) <= 40) hover = lane; } }
   else for (int s = 0; s < 6; s++) if (targets[s].hole.Contains(p)) { hover = s; break; }
   Target? hv = hover >= 0 ? targets[hover] : (Target?)null;

   under.Begin();
   // 1. dark board with holes over the legal targets
   var holes = new List<Rect>(); foreach (var tg in targets) if (tg.legal) holes.Add(tg.hole);
   FillOutsideRound(under, board, holes, 12, new Color(0, 0, 0, .62f * lift));
   // nearest legal target to the pointer
   int near = -1; float nd = float.MaxValue; foreach (var tg in targets) if (tg.legal) { float d = Vector2.Distance(p, tg.center); if (d < nd) { nd = d; near = tg.i; } }
   Vector2 from = cardRect.center - new Vector2(0, overBoard ? 50 : 90);
   int k = 0; float fl = Mathf.FloorToInt(t * 12) % 2 == 1 ? 1 : .35f;
   foreach (var tg in targets) {
    if (!tg.legal) continue;
    Color col = tg.attack ? Cyan : tg.wall ? Gold : tg.remove ? Red : Gold; bool h = hover == tg.i, isN = near == tg.i; var b = tg.hole;
    // 2. hole outline
    under.StrokeRoundRect(b.x, b.y, b.width, b.height, 12, h ? 2.5f : 1.2f, h ? A(White, .9f) : A(col, .4f * lift));
    if (h) under.FillRoundRect(b.x, b.y, b.width, b.height, 12, A(col, .1f));
    // 3. circuit trace from the card
    Vector2 end = new Vector2(tg.center.x, tg.center.y + (tg.attack ? 30 : 0)); var pts = Trace(from, end, k);
    float grow = EOut((t - k * .06f) / .35f), len = PathMath.Length(pts);
    if (grow >= 1) {
     under.Stroke(pts, isN ? 2.5f : 1.4f, A(col, (isN ? .85f : .22f) * lift), true);
     int n = isN ? 4 : 2; for (int j = 0; j < n; j++) { float u = Mathf.Repeat(t * (isN ? .9f : .4f) + j / (float)n, 1); under.Disc(PathMath.At(pts, u * len), isN ? 3 : 2, A(Color.Lerp(col, White, .6f), isN ? .95f : .5f), 12); }
     under.FillRect(end.x - 4, end.y - 4, 8, 8, A(col, (isN ? .9f : .35f) * lift));
    } else under.Stroke(new[] { pts[0], PathMath.At(pts, grow * len) }, 1.5f, A(col, .3f * lift), true);
    // 4. target marks
    if (tg.attack) {
     if (h) {
      for (int q = 0; q < 6; q++) { float ang = q / 6f * Mathf.PI * 2; under.FillRect(tg.center.x + Mathf.Cos(ang) * 36 - 3, tg.center.y + Mathf.Sin(ang) * 36 - 3, 6, 6, A(White, .9f)); }
      under.Glow(tg.center, 50, Cyan, .6f);
      // the public route (same as BattleController.Preview, drawn again above the dark layer)
      under.Stroke(BattleController.Points(st.Route(0, tg.i)), 3, A(Gold, .85f), true);
     }
    } else if (tg.wall) {
     // 차단선(채널 따라 금색, 올리면 굵게 깜빡임 없이) + 양쪽 서버 문 자리(가로 막대 70 px, 올리면 빛)
     Vector2 top = new Vector2(tg.center.x, 231), bottom = new Vector2(tg.center.x, 763);
     if (h) under.Stroke(new[] { top, bottom }, 4, A(Gold, .9f), true); else Dashed(under, top, bottom, 8, 8, 2, A(Gold, .4f * lift));
     foreach (var g in new[] { tg.a, tg.b }) { under.Stroke(new[] { g + new Vector2(-35, 0), g + new Vector2(35, 0) }, h ? 6 : 3, A(Gold, (h ? 1 : .45f) * lift), true); if (h) { under.Glow(g, 40, Gold, .8f); under.FillRect(g.x - 4, g.y - 4, 8, 8, A(White, .9f)); } }
    } else if (tg.remove) {
     if (h) under.Stroke(new[] { tg.a, tg.b }, 6, A(Red, .9f * fl), true);
     else Dashed(under, tg.a, tg.b, 6, 6, 2, A(Red, .5f * lift));
    } else {
     under.Stroke(new[] { tg.a, tg.b }, h ? 6 : 2, A(Gold, (h ? 1 : .35f) * lift), true);
     if (h) foreach (var e in new[] { tg.a, tg.b }) { under.Glow(e, 26, Gold, .9f); under.FillRect(e.x - 4, e.y - 4, 8, 8, A(White, .9f)); }
    }
    k++;
   }
   // 5. illegal target under the pointer: red X
   if (hv.HasValue && !hv.Value.legal) { var c = hv.Value.center; under.Stroke(new[] { c + new Vector2(-14, -14), c + new Vector2(14, 14) }, 4, A(Red, .95f), true); under.Stroke(new[] { c + new Vector2(14, -14), c + new Vector2(-14, 14) }, 4, A(Red, .95f), true); }
   under.End();

   // above the card: red tint (illegal) and the DELETE cursor badge (existing rung)
   if (overRoot.GetSiblingIndex() != overRoot.parent.childCount - 1) overRoot.SetAsLastSibling();
   over.Begin();
   var group = card.GetComponent<CanvasGroup>(); float cardAlpha = group ? group.alpha : 1;
   if (hv.HasValue && !hv.Value.legal) over.FillRect(cardRect.x, cardRect.y, cardRect.width, cardRect.height, A(Red, .35f * cardAlpha));
   bool badge = hv.HasValue && hv.Value.remove;
   if (badge) {
    Vector2 bc = new Vector2(cardRect.xMax, cardRect.yMax);
    over.Disc(bc, 16, Red, 32); over.Ring(bc, 16, 2, A(White, .95f), 32); over.Stroke(new[] { bc + new Vector2(-7, 0), bc + new Vector2(7, 0) }, 3.5f, White, true);
    over.FillRect(bc.x + 20, bc.y - 12, 82, 24, C(40, 4, 10, .9f));
    deleteText.rectTransform.anchoredPosition = new Vector2(bc.x + 61, -(bc.y + 1));
   }
   deleteText.gameObject.SetActive(badge);
   over.End();
  }

  // 05_drop.js trace(): orthogonal route with 12 px chamfers, the horizontal run at 45% of the height (+10 px per target)
  static Vector2[] Trace(Vector2 c, Vector2 e, int k) {
   float ym = Mathf.Lerp(c.y, e.y, .45f) + k * 10, ch = 12, dx = e.x - c.x;
   float sx = dx > 0 ? 1 : dx < 0 ? -1 : 1, dy = e.y - ym, sy = dy > 0 ? 1 : dy < 0 ? -1 : -1, cut = Mathf.Min(ch, Mathf.Abs(dx));
   return new[] { c, new Vector2(c.x, ym + ch * (c.y > ym ? 1 : -1)), new Vector2(c.x + sx * cut, ym), new Vector2(e.x - sx * cut, ym), new Vector2(e.x, ym + sy * ch), e };
  }

  static void Dashed(FxPainter g, Vector2 a, Vector2 b, float on, float off, float w, Color c) {
   float len = Vector2.Distance(a, b); if (len <= 0) return; var d = (b - a) / len;
   for (float s = 0; s < len; s += on + off) g.Stroke(new[] { a + d * s, a + d * Mathf.Min(len, s + on) }, w, c, true);
  }

  Rect StageRect(RectTransform r) {
   var w = new Vector3[4]; r.GetWorldCorners(w); Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
   foreach (var q in w) { var l = host.stage.InverseTransformPoint(q); var s = new Vector2(l.x, -l.y); min = Vector2.Min(min, s); max = Vector2.Max(max, s); }
   return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
  }

  // ctx.rect(area); ctx.roundRect(hole...) x n; fill('evenodd') for holes that do not overlap: horizontal strips whose
  // breakpoints follow each hole's corner arcs (6 steps), filled as quads between the holes.
  static void FillOutsideRound(FxPainter g, Rect area, List<Rect> holes, float r, Color c) {
   if (c.a <= 0) return;
   var ys = new List<float> { area.yMin, area.yMax };
   foreach (var h in holes) { float rr = Mathf.Min(r, h.width / 2, h.height / 2); for (int i = 0; i <= 6; i++) { float d = rr * (1 - Mathf.Cos(i / 6f * Mathf.PI / 2)); ys.Add(h.yMin + d); ys.Add(h.yMax - d); } }
   ys.Sort();
   var active = new List<Rect>();
   for (int n = 1; n < ys.Count; n++) {
    float y0 = Mathf.Max(area.yMin, ys[n - 1]), y1 = Mathf.Min(area.yMax, ys[n]); if (y1 - y0 < 1e-3f) continue;
    active.Clear(); float ym = (y0 + y1) / 2; foreach (var h in holes) if (h.yMin < ym && ym < h.yMax) active.Add(h);
    active.Sort((p, q) => p.xMin.CompareTo(q.xMin));
    float px0 = area.xMin, px1 = area.xMin;
    foreach (var h in active) {
     float i0 = Inset(h, y0, r), i1 = Inset(h, y1, r), l0 = Mathf.Max(area.xMin, h.xMin + i0), l1 = Mathf.Max(area.xMin, h.xMin + i1);
     g.FillPoly(new[] { new Vector2(px0, y0), new Vector2(l0, y0), new Vector2(l1, y1), new Vector2(px1, y1) }, c);
     px0 = Mathf.Min(area.xMax, h.xMax - i0); px1 = Mathf.Min(area.xMax, h.xMax - i1);
    }
    g.FillPoly(new[] { new Vector2(px0, y0), new Vector2(area.xMax, y0), new Vector2(area.xMax, y1), new Vector2(px1, y1) }, c);
   }
  }
  // horizontal inset of a rounded rect's edge at height y
  static float Inset(Rect h, float y, float r) {
   r = Mathf.Min(r, h.width / 2, h.height / 2); float d = Mathf.Min(y - h.yMin, h.yMax - y); if (d >= r) return 0; d = Mathf.Max(0, d);
   float u = r - d; return r - Mathf.Sqrt(Mathf.Max(0, r * r - u * u));
  }

  void OnDisable() { Hide(); }
 }
}

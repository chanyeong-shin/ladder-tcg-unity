using TMPro;
using UnityEngine;

namespace Breach.UI {
 // Week 12 item 4 (2026-11-26): card socket "회로 무늬 → 스캔 중 흐르는 스캔라인", line-for-line port of _도구/시안12주차/tabs/06_socket.js
 // comboMount(): the gap between the socket (CardTransferFx.Socket 223,381,228x236) and the card-size slot (DockRect 137.5x220)
 // shows a circuit pattern while the socket waits and flowing scanlines while a card is docked and scanned.
 // Timing comes from CardTransferFx (move .35, dock .25, scan .8, settle .22, absorb .7); after the last card leaves the pattern
 // returns in .3 s. The card that last docked owns the socket (week 12 item 5 lets the next card dock while the previous one absorbs).
 public sealed class SocketFx : MonoBehaviour {
  static readonly Rect S = CardTransferFx.Socket;
  static Rect D { get { return CardTransferFx.DockRect; } }
  static readonly Color SocketBg = new Color(17 / 255f, 11 / 255f, 13 / 255f), SlotBg = new Color(6 / 255f, 8 / 255f, 11 / 255f);
  // 내 턴 청록 (56,217,255) · 상대 턴 빨강 (255,59,87) · 동시 발동 노랑 (255,196,56)
  static readonly Color[] TurnColor = { new Color(56 / 255f, 217 / 255f, 1), new Color(1, 59 / 255f, 87 / 255f), new Color(1, 196 / 255f, 56 / 255f) };
  BattleController host; FxPainter g; TextMeshProUGUI markKo, markEn;
  Color col = TurnColor[0]; float clock, tau = 99;
  // m: 0 circuit pattern, 1 scanlines (eased value drawn); back: seconds into the .3 s return
  float m, back = 9, mark = 1, clampA, moveMark = 1; Color clampColor; object owner;
  // wake: 0 빈 소켓, 1 카드를 집었거나 소켓에 들어오는 중 (0.3초에 걸쳐 오르내림, 그릴 때 Ease)
  float wake;
  public float Mix { get { return m; } }
  public float Clock { get { return clock; } set { clock = value; } }
  public Color TurnInk { get { return col; } }
  // 지금 턴의 목표 색 (스캔 회로 색: CardTransferFx)
  public Color TargetInk { get { if (host == null || host.State == null) return TurnColor[0]; return TurnColor[host.State.Phase == 2 ? 2 : host.State.Actor == 0 ? 0 : 1]; } }

  public void Initialize(BattleController c, Transform parent, int siblingIndex, TMP_FontAsset font) {
   host = c; var root = Ui.Rect("카드 실행 소켓 - 회로 무늬와 스캔라인", parent, 0, 0, 1920, 1080); root.SetSiblingIndex(siblingIndex);
   g = FxPainter.Create("소켓 채움", root);
   var d = D;
   markKo = Ui.Text("빈 소켓 글자", root, d.x, d.center.y - 12 - 20, d.width, 40, "카드 실행", font, 20, Color.clear); markKo.alignment = TextAlignmentOptions.Midline; markKo.textWrappingMode = TextWrappingModes.NoWrap;
   markEn = Ui.Text("빈 소켓 영문", root, d.x, d.center.y + 12 - 20, d.width, 40, "EXECUTE", font, 13, Color.clear); markEn.alignment = TextAlignmentOptions.Midline; markEn.textWrappingMode = TextWrappingModes.NoWrap;
  }
  static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
  static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, Mathf.Clamp01(a)); }
  static Color White(Color c, float k) { return Color.Lerp(c, Color.white, k); }

  // ---- driven by CardTransferFx.Play ----
  public void Move(object card, float k) { moveMark = 1 - Ease(k / .6f); }
  public void Dock(object card, float k) { owner = card; moveMark = 1; back = 9; m = Mathf.Max(m, Ease(k)); mark = 0; clampA = 1; clampColor = Color.Lerp(Color.white, col, k); tau = -(1 - k) * .25f; }
  public void Scan(object card, float k) { if (owner != card) return; m = 1; mark = 0; clampA = 1; clampColor = col; tau = k * .8f; }
  public void Hold(object card) { if (owner != card) return; m = 1; mark = 0; clampA = 1; clampColor = col; }
  public void Absorb(object card, float k) { if (owner != card) return; m = 1; mark = 0; clampA = 1 - Ease((k - .5f) / .5f); clampColor = col; }
  public void Release(object card) { if (owner != card) return; owner = null; back = 0; clampA = 0; }
  public void ResetAll() { owner = null; m = 0; back = 9; mark = 1; moveMark = 1; clampA = 0; tau = 99; wake = 0; }

  void LateUpdate() {
   if (!g || host == null || host.State == null) { if (g) { g.Begin(); g.End(); } return; }
   float dt = Time.unscaledDeltaTime * FxClock.Scale, rate = host.feedback ? host.feedback.EffectRate : 1;
   clock += dt; tau += Time.deltaTime * FxClock.Scale * rate;
   int phase = host.State.Phase == 2 ? 2 : host.State.Actor == 0 ? 0 : 1;
   // st.col = mix(st.col, TURN[st.turn].rgb, Math.min(1, dt / .08))
   col = Color.Lerp(col, TurnColor[phase], Mathf.Min(1, dt / .08f));
   if (owner == null && back < .3f) { back += Time.deltaTime * FxClock.Scale * rate; float k = Mathf.Clamp01(back / .3f); m = 1 - Ease(k); mark = Ease(k); if (back >= .3f) { m = 0; mark = 1; } }
   if (owner != null && clampColor != col && clampA > 0 && tau > 0) clampColor = col;
   bool awake = host.Dragging || owner != null || moveMark < 1 || (back < .3f && m > 0);
   wake = Mathf.Clamp01(wake + (awake ? 1 : -1) * Time.unscaledDeltaTime * FxClock.Scale / .3f);
   Draw();
  }

  void Draw() {
   var d = D; g.Begin();
   // cover(g, S, 4)
   g.FillRect(S.x - 4, S.y - 4, S.width + 8, S.height + 8, SocketBg);
   if (m < 1) CircuitFill(1 - m);
   if (m > 0) ScanFill(m);
   float w = Ease(wake), wm = Mathf.Max(w, m);
   // CALM: 소켓 테두리 .45→.9, 글자 .28→.5, 슬롯 테두리 .3→.55
   g.StrokeRect(S.x + .5f, S.y + .5f, S.width - 1, S.height - 1, 1.5f, A(col, Mathf.Lerp(.45f, .9f, wm)));
   g.FillRect(d.x, d.y, d.width, d.height, SlotBg);
   float markA = owner != null ? 0 : Mathf.Min(mark, moveMark);
   markKo.color = A(col, Mathf.Lerp(.28f, .5f, w) * markA); markEn.color = A(col, Mathf.Lerp(.28f, .5f, w) * markA);
   g.StrokeRect(d.x - 1, d.y - 1, d.width + 2, d.height + 2, 1.5f, A(col, Mathf.Lerp(.3f, .55f, wm)));
   if (clampA > 0) g.StrokeRect(d.x - 3, d.y - 3, d.width + 6, d.height + 6, 2, A(clampColor, clampA));
   g.End();
  }
  // gap = socket inner rect minus the slot (ctx.rect(S); ctx.rect(D); evenodd)
  static Rect[] Gap() { var d = D; return new[] { Rect.MinMaxRect(S.xMin, S.yMin, d.xMin, S.yMax), Rect.MinMaxRect(d.xMax, S.yMin, S.xMax, S.yMax), Rect.MinMaxRect(d.xMin, S.yMin, d.xMax, d.yMin), Rect.MinMaxRect(d.xMin, d.yMax, d.xMax, S.yMax) }; }
  // fillRect clipped to the gap; top/bottom colors give a vertical gradient
  void GapRect(float x, float y, float w, float h, Color top, Color bottom) {
   foreach (var r in Gap()) {
    float x0 = Mathf.Max(x, r.xMin), x1 = Mathf.Min(x + w, r.xMax), y0 = Mathf.Max(y, r.yMin), y1 = Mathf.Min(y + h, r.yMax); if (x1 <= x0 || y1 <= y0) continue;
    Color c0 = Color.Lerp(top, bottom, (y0 - y) / h), c1 = Color.Lerp(top, bottom, (y1 - y) / h);
    g.Line(new Vector2((x0 + x1) / 2, y0), new Vector2((x0 + x1) / 2, y1), x1 - x0, c0, c1);
   }
  }
  // 06_socket.js circuitFill: 16% fill, 11 lanes a side (45 degree jog, end pad), light dots flowing toward the card
  readonly Vector2[][] lanePts = new Vector2[22][];
  void CircuitFill(float alpha) {
   var d = D;
   float w = Ease(wake);
   foreach (var r in Gap()) g.FillRect(r.x, r.y, r.width, r.height, A(col, Mathf.Lerp(.07f, .16f, w) * alpha));
   const int lanes = 11; float pitch = (d.height - 20) / (lanes - 1);
   for (int side = 0; side < 2; side++) for (int i = 0; i < lanes; i++) {
    float dir = side == 0 ? -1 : 1, x0 = side == 0 ? d.x - 2 : d.xMax + 2, x1 = side == 0 ? S.x + 6 : S.xMax - 6;
    float y = d.y + 10 + i * pitch, jog = i % 3 == 0 ? 0 : (i < lanes / 2f ? -1 : 1) * 6, mid = x0 + dir * (10 + (i % 4) * 4);
    var pts = new[] { new Vector2(x0, y), new Vector2(mid, y), new Vector2(mid + dir * Mathf.Abs(jog), y + jog), new Vector2(x1, y + jog) };
    g.Poly(pts, 1.6f, A(col, Mathf.Lerp(.34f, .7f, w) * alpha));
    g.FillRect(x1 - 2.5f, y + jog - 2.5f, 5, 5, A(col, Mathf.Lerp(.45f, .95f, w) * alpha));
    lanePts[side * lanes + i] = pts;
    float k = ((clock * .9f + i * .23f + side * .5f) % 1.6f) / 1.6f;
    // 11-27 fix (user: "회로의 점 움직이는게 회로를 정확히 안따라가던데"): the old mock formula moved x linearly and y only in the
    // second half, off the 45-degree jog. Now the dot walks the polyline by arc length from the socket end (x1) to the slot (x0).
    if (k < 1 && w > 0) g.Disc(Along(pts, 1 - k), 2.4f, A(White(col, .7f), (1 - k * .6f) * alpha * w), 16);
   }
   // 드문 점: 양쪽 하나씩, 3.2초마다(1.6초 엇갈림) 2.4초에 걸쳐 천천히 카드 쪽으로, 최대 55%
   for (int side = 0; side < 2; side++) {
    float cyc = (clock + side * 1.6f) / 3.2f; int n = Mathf.FloorToInt(cyc); float k = (cyc - n) * 3.2f / 2.4f; if (k >= 1) continue;
    var lp = lanePts[side * lanes + ((n * 7 + side * 4) % lanes + lanes) % lanes];
    g.Disc(Along(lp, 1 - Ease(k)), 2, A(White(col, .5f), Mathf.Sin(k * Mathf.PI) * .55f * alpha), 16);
   }
   g.Line(new Vector2(d.x, S.y + 4), new Vector2(d.xMax, S.y + 4), 1, A(col, Mathf.Lerp(.24f, .55f, w) * alpha));
   g.Line(new Vector2(d.x, S.yMax - 4), new Vector2(d.xMax, S.yMax - 4), 1, A(col, Mathf.Lerp(.24f, .55f, w) * alpha));
  }
  // point at fraction f (0 = first point, 1 = last) of a polyline's length
  static Vector2 Along(Vector2[] p, float f) {
   float total = 0; for (int i = 1; i < p.Length; i++) total += Vector2.Distance(p[i - 1], p[i]);
   float d = Mathf.Clamp01(f) * total;
   for (int i = 1; i < p.Length; i++) { float s = Vector2.Distance(p[i - 1], p[i]); if (d <= s || i == p.Length - 1) return s > 0 ? Vector2.Lerp(p[i - 1], p[i], Mathf.Min(1, d / s)) : p[i]; d -= s; }
   return p[p.Length - 1];
  }
  // 06_socket.js scanFill: 30% fill, 38% lines every 5 px moving down 30 px/s, a bright band (by) tied to the card's scan bar
  void ScanFill(float alpha) {
   var d = D;
   GapRect(S.x, S.y, S.width, S.height, A(col, .3f * alpha), A(col, .3f * alpha));
   float off = (clock * 30) % 5;
   for (float y = S.y - 5 + off; y < S.yMax; y += 5) GapRect(S.x, y, S.width, 2, A(col, .38f * alpha), A(col, .38f * alpha));
   // rel = D.y + (D.h / .8) * tau - (S.y - 40); by = S.y - 40 + (rel < 0 ? rel : rel % (S.h + 80))
   float rel = d.y + (d.height / .8f) * tau - (S.y - 40), L = S.height + 80, by = S.y - 40 + (rel < 0 ? rel : rel % L);
   Color mid = A(White(col, .5f), .75f * alpha), edge = A(col, 0);
   GapRect(S.x, by - 36, S.width, 36, edge, mid); GapRect(S.x, by, S.width, 36, mid, edge);
  }
 }
}

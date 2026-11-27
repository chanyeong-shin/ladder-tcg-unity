using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 // Week 12 hand items (계획/Unity_12주차_적용목록.md 7, 12):
 //  - 07_handlimit.js "segments" + dissolve: MY HAND 5-segment bar left of the hand (174,905, 400 wide). A draw that finds the hand full
 //    flies from the deck to 922,640, shakes red (.4-.75 s), then breaks into 6 x 10 tiles from the bottom row up, each running off along
 //    a circuit trace (.6 s per tile); the warning sits in the status label's place (1459,795, 287 wide) for 2.6 s.
 //    The red card is the HTML's tint canvas (sepia(1) saturate(6) hue-rotate(-50deg) brightness(.9)) exported once from Chrome
 //    (Resources/CardFx_BackRed.png), and the 60 tiles are one mesh, so no frame pays for a filter (the HTML's 7 fps bug).
 //  - 12_drawlimit.js "lock": rules v1.2 (Battle.DrawUsed). A draw card I cannot play this turn: grayscale(.6) brightness(.6) (40% darker),
 //    a gold hex lock badge and "다음 턴" in the middle. Dragging it onto the socket/board: the socket shows a glitching "ERROR" and
 //    "DRAW 1 / 1" for .8 s and the card snaps back to the hand (BattleController.RejectCard); the status place shows the reason.
 //    The badge goes when my next action turn starts (no text; user 11-26), with one cyan outline pulse (.8 s) like the HTML.
 public sealed class HandFx : MonoBehaviour {
  BattleController c; float now; RectTransform under, over;
  Shape box, boxLine; TMP_Text label; readonly Shape[] segs = new Shape[5], segLines = new Shape[5]; FxPainter segGlow;
  Shape cover; TMP_Text warnText; Image ghost; TileMesh tiles; FxPainter traces, err, pulse; TMP_Text errMain, errCyan, errRed, errSub;
  Material glowMat; Sprite red; float lostT = -99, msgT = -99, rejectT = -99, lockT = -99, unlockT = -99; int lostN; bool locked;
  public float LockAmount { get; private set; }
  public float LostTime { get { return now - lostT; } }
  public float RejectTime { get { return now - rejectT; } }
  public bool WarningShown { get { return cover && cover.gameObject.activeSelf; } }
  public string WarningText { get { return warnText ? warnText.text : ""; } }
  public string HandLabel { get { return label ? label.text : ""; } }
  public bool DrawLocked { get { return locked; } }
  public static readonly Rect Deck = new Rect(1542, 390, 120, 192), Lost = new Rect(922, 640, 126, 201.6f);
  public static readonly Color Red = C(255, 59, 87), Gold = C(255, 196, 56), Cyan = C(56, 217, 255), Dim = C(110, 150, 165), Line = C(51, 102, 115);
  static Color C(float r, float g, float b, float a = 1) { return new Color(r / 255, g / 255, b / 255, a); }
  static Color A(Color col, float a) { return new Color(col.r, col.g, col.b, a); }
  static float Clamp(float v) { return Mathf.Clamp01(v); }
  static float OutQ(float v) { v = Clamp(v); return 1 - Mathf.Pow(1 - v, 3); }

  // 6 x 10 tiles with the HTML's Park-Miller sequence (seed 7): trace leaves through the nearer side, bends 45 degrees, runs out
  public sealed class Tile { public float x, y, len, td; public int col, row; public Vector2[] pts; }
  public static readonly List<Tile> Tiles = MakeTiles();
  static List<Tile> MakeTiles() {
   var list = new List<Tile>(); double seed = 7; System.Func<double> rnd = () => { seed = (seed * 16807) % 2147483647; return seed / 2147483647; };
   const int C6 = 6, R10 = 10; float tw = Lost.width / C6, th = Lost.height / R10;
   for (int r = 0; r < R10; r++) for (int cc = 0; cc < C6; cc++) {
    float x = Lost.x + cc * tw, y = Lost.y + r * th, cx = x + tw / 2, cy = y + th / 2; int sx = cc < C6 / 2 ? -1 : 1; float edgeX = sx < 0 ? Lost.x - 6 : Lost.x + Lost.width + 6;
    float run = (float)(30 + rnd() * 70); float sign = rnd() < .5 ? -1 : 1; float dy = sign * (float)(14 + rnd() * 30); float far = (float)(40 + rnd() * 90);
    var pts = new[] { new Vector2(cx, cy), new Vector2(edgeX, cy), new Vector2(edgeX + sx * run, cy), new Vector2(edgeX + sx * (run + Mathf.Abs(dy)), cy + dy), new Vector2(edgeX + sx * (run + Mathf.Abs(dy) + far), cy + dy) };
    list.Add(new Tile { x = x, y = y, col = cc, row = r, pts = pts, len = PathMath.Length(pts), td = (float)(.76 + (R10 - 1 - r) / (double)(R10 - 1) * .8 + rnd() * .1) });
   }
   return list;
  }

  public void Initialize(BattleController controller) {
   c = controller; red = Resources.Load<Sprite>("CardFx_BackRed");
   under = Ui.Rect("손패 상한 - MY HAND", c.hand.parent, 0, 0, 1920, 1080); under.SetSiblingIndex(c.hand.GetSiblingIndex());
   box = Ui.Box("MY HAND 바탕", under, 174, 905, 400, 78, C(10, 20, 27, .92f)); boxLine = Ui.Box("MY HAND 테두리", under, 174, 905, 400, 78, Line, false, 1);
   segGlow = FxPainter.Create("칸 빛", under);
   for (int k = 0; k < 5; k++) { segs[k] = Ui.Box("칸 " + (k + 1), under, 191 + k * 74.797f, 956, 66.797f, 10, Color.clear); segLines[k] = Ui.Box("칸 테두리 " + (k + 1), under, 191 + k * 74.797f, 956, 66.797f, 10, Line, false, 1); }
   label = Txt(under, "MY HAND 글", 191, 937, 600, 19, Cyan, "", 0, .5f / 19 * 100);
   over = Ui.Rect("손패 연출 - 잠금·분해", c.game.transform, 0, 0, 1920, 1080); over.SetAsLastSibling();
   cover = Ui.Box("상태 글 가림", over, 1455, 790, 300, 70, C(11, 12, 16)); warnText = Txt(cover.transform.parent, "상태 경고 글", 1459, 814, 287, 19, Red, "", (27.55f - 1.448f * 19) / 19 * 100, 0); warnText.transform.SetParent(cover.transform, true);
   cover.gameObject.SetActive(false);
   ghost = Ui.Rect("받지 못한 드로우", over, 0, 0, 126, 201.6f).gameObject.AddComponent<Image>(); ghost.raycastTarget = false; ghost.gameObject.SetActive(false);
   tiles = Ui.Rect("분해 조각", over, 0, 0, 1920, 1080).gameObject.AddComponent<TileMesh>(); tiles.raycastTarget = false; tiles.sprite = red;
   traces = FxPainter.Create("조각 회로선", over); pulse = FxPainter.Create("잠금 풀림 테두리", over); err = FxPainter.Create("소켓 ERROR 테두리", over);
   errCyan = Mid(over, "ERROR 청록 잔상", 52); errRed = Mid(over, "ERROR 빨강 잔상", 52); errMain = Mid(over, "ERROR", 52); errSub = Mid(over, "DRAW 1 / 1", 20); errSub.text = "DRAW 1 / 1";
   foreach (var t in new[] { errCyan, errRed, errMain }) t.text = "ERROR";
   // shadowBlur 12 red behind the main ERROR: a soft TMP underlay
   glowMat = errMain.fontMaterial; glowMat.EnableKeyword("UNDERLAY_ON"); glowMat.SetFloat("_UnderlaySoftness", .8f); glowMat.SetFloat("_UnderlayDilate", .4f); glowMat.SetFloat("_UnderlayOffsetX", 0); glowMat.SetFloat("_UnderlayOffsetY", 0);
  }
  TMP_Text Txt(Transform parent, string name, float x, float baseline, float w, float size, Color col, string text, float lineSpacing, float charSpacing) {
   var t = Ui.Text(name, parent, x, baseline - 1.16f * size, w, 200, text, c.font, size, col); t.textWrappingMode = TextWrappingModes.NoWrap; t.lineSpacing = lineSpacing; t.characterSpacing = charSpacing; t.richText = true; return t;
  }
  // canvas textAlign center + textBaseline middle: Chrome puts the em-box middle on y, i.e. baseline = y + (.8 - .2)/2 em for Do Hyeon
  TMP_Text Mid(Transform parent, string name, float size) { var t = Ui.Text(name, parent, 0, 0, 600, 200, "", c.font, size, Color.white); t.alignment = TextAlignmentOptions.Top; t.textWrappingMode = TextWrappingModes.NoWrap; t.gameObject.SetActive(false); return t; }
  static void At(TMP_Text t, float cx, float y, float size) { t.rectTransform.anchoredPosition = new Vector2(cx - 300, -(y + .3f * size - 1.16f * size)); }

  public void LostDraw(int count) { lostT = now; lostN = Mathf.Max(1, count); }
  public void LockedDrag() { msgT = now; }
  public void Reject() { rejectT = now; }
  public void ResetAll() { lostT = msgT = rejectT = lockT = unlockT = -99; locked = false; LockAmount = 0; if (ghost) ghost.gameObject.SetActive(false); if (cover) cover.gameObject.SetActive(false); }
  public bool Flash { get { float r = now - rejectT; return r >= 0 && r < .6f && Mathf.FloorToInt(r * 10) % 2 == 0; } }

  void Update() {
   now += Time.unscaledDeltaTime * FxClock.Scale;
   var s = c.State; if (s == null) return;
   // lock: follows Battle.DrawUsed during my action turn, kept until my next action turn
   if (s.Actor == 0 && s.Phase < 2 && s.Winner == -1 && s.DrawUsed != locked) { if (!s.DrawUsed && locked) unlockT = now; locked = s.DrawUsed; lockT = now; }
   LockAmount = locked ? OutQ((now - lockT) / .3f) : (lockT < 0 ? 0 : 1 - OutQ((now - lockT) / .3f));
   ApplyLocks();
   ApplyHand(s);
   ApplyLost();
   ApplyReject();
  }

  void ApplyHand(Battle s) {
   int n = c.VisibleHandCount; float lk = now - lostT; bool warn = lk >= .4f && lk < 3; float w = warn ? Clamp(1 - (lk - .4f) / 2.6f) : 0;
   bool flash = w > 0 && Mathf.FloorToInt(lk * 8 + 5.2f * 8) % 2 == 0; /* HTML redFlash(w, t): t is the demo clock, whose only lost draw is at 5.2 s */ Color col = flash ? Red : n >= 5 ? Gold : Cyan;
   // HTML: while the red "FULL · 드로우 못 받음" is shown the label line grows from 24 to 28 px (the '·' falls back to another font) and the box with it
   bool tall = w > 0; float labBase = tall ? 941 : 937, segY = tall ? 960 : 956, boxH = tall ? 82 : 78;
   Size(box, 400, boxH); Size(boxLine, 400, boxH);
   label.rectTransform.anchoredPosition = new Vector2(191, -(labBase - 1.16f * 19));
   label.text = "<color=#" + ColorUtility.ToHtmlStringRGB(col) + ">MY HAND   " + n.ToString("00") + "</color><color=#6E96A5>  /  05</color>" + (w > 0 ? "<color=#FF3B57>   FULL · 드로우 못 받음</color>" : n >= 5 ? "<color=#FFC438>   FULL</color>" : "");
   segGlow.Begin();
   for (int k = 0; k < 5; k++) {
    float x = 191 + k * 74.797f; segs[k].rectTransform.anchoredPosition = segLines[k].rectTransform.anchoredPosition = new Vector2(x, -segY);
    segs[k].color = k < n ? col : C(56, 217, 255, .06f); if (k < n) segGlow.SoftRect(x, segY, 66.797f, 10, 8, A(col, .5f));
   }
   segGlow.End();
   // status place: draw-lock reason (from the drag start + .2 s, 2.4 s, fade .15 / .3) or the full-hand warning (lk .4 - 3)
   float m = now - msgT, msg = m >= .2f && m < 2.6f ? Mathf.Min(1, (m - .2f) / .15f, (2.6f - m) / .3f) : 0;
   bool lockMsg = msg > 0 && msgT >= lostT;
   bool show = lockMsg || warn; if (cover.gameObject.activeSelf != show) cover.gameObject.SetActive(show);
   if (lockMsg) { warnText.text = "드로우 카드는 한 턴에 1장만 낼 수 있습니\n다"; warnText.alpha = msg; }
   else if (warn) { warnText.text = "손패가 가득 차(5/5) 이번 드로우 " + lostN + "장을 받\n지 못했습니다"; warnText.alpha = 1; }
  }
  static void Size(Graphic g, float w, float h) { g.rectTransform.sizeDelta = new Vector2(w, h); g.SetVerticesDirty(); }

  void ApplyLost() {
   float lk = now - lostT;
   bool fly = lk >= 0 && lk < .75f; if (ghost.gameObject.activeSelf != fly) ghost.gameObject.SetActive(fly);
   if (fly) {
    float e = OutQ(lk / .4f), sh = lk > .4f && lk < .75f ? Mathf.Sin(lk * 70) * 10 : 0;
    var r = ghost.rectTransform; r.anchoredPosition = new Vector2(Mathf.Lerp(Deck.x, Lost.x, e) + sh, -Mathf.Lerp(Deck.y, Lost.y, e)); r.sizeDelta = new Vector2(Mathf.Lerp(Deck.width, Lost.width, e), Mathf.Lerp(Deck.height, Lost.height, e));
    ghost.sprite = lk > .4f ? red : c.feedback.backImage;
   }
   bool dz = lk >= .75f && lk < 2.75f;
   tiles.Draw(dz ? lk : -1);
   traces.Begin();
   if (dz) foreach (var tl in Tiles) {
    float k = (lk - tl.td) / .6f; if (k < 0 || k > 1.6f) continue;
    float head = tl.len * OutQ(Mathf.Min(1, k)), fade = k > 1 ? 1 - (k - 1) / .6f : 1;
    traces.Poly(PathMath.Slice(tl.pts, 0, head), 1.5f, C(255, 59, 87, .28f * fade));
    var tail = PathMath.Slice(tl.pts, Mathf.Max(0, head - 26), head);
    traces.Poly(tail, 9, C(255, 59, 87, .3f * fade)); RoundCaps(traces, tail, 9, C(255, 59, 87, .3f * fade));
    traces.Poly(tail, 2.5f, C(255, 140, 150, .9f * fade));
    if (k < 1) { var p = PathMath.At(tl.pts, head); traces.FillRect(p.x - 2, p.y - 2, 4, 4, C(255, 220, 225, fade)); }
    else traces.Disc(tl.pts[tl.pts.Length - 1], 3, C(255, 59, 87, fade), 16);
   }
   traces.End();
  }
  // canvas lineCap 'round': half discs beyond both ends of the stroke
  static void RoundCaps(FxPainter p, List<Vector2> pts, float w, Color col) {
   if (pts.Count < 2) return;
   void Cap(Vector2 at, Vector2 dir) { var poly = new List<Vector2> { at }; float a0 = Mathf.Atan2(dir.y, dir.x) - Mathf.PI / 2; for (int i = 0; i <= 8; i++) { float a = a0 + Mathf.PI * i / 8; poly.Add(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * w / 2); } p.FillPoly(poly, col); }
   if ((pts[1] - pts[0]).sqrMagnitude > 1e-6f) Cap(pts[0], (pts[0] - pts[1]).normalized);
   int n = pts.Count; if ((pts[n - 1] - pts[n - 2]).sqrMagnitude > 1e-6f) Cap(pts[n - 1], (pts[n - 1] - pts[n - 2]).normalized);
  }

  void ApplyReject() {
   float rej = now - rejectT; bool on = rej >= 0 && rej < .8f; float a = on ? 1 - Clamp(rej / .8f) : 0;
   foreach (var t in new[] { errMain, errCyan, errRed, errSub }) if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
   err.Begin();
   if (on) {
    const float sx = 223 + 114, sy = 381 + 118;
    // strokeRect(S.x + 6, S.y + 6, S.w - 12, S.h - 12) 4 px red with shadowBlur 14 (two wider faint strokes stand in for the blur)
    err.StrokeRect(229, 387, 216, 224, 12, C(255, 59, 87, .12f * a)); err.StrokeRect(229, 387, 216, 224, 8, C(255, 59, 87, .2f * a)); err.StrokeRect(229, 387, 216, 224, 4, C(255, 59, 87, a));
    float j = rej < .3f ? Mathf.Sin(rej * 90) * 7 : 0; bool lit = rej < .3f && Mathf.FloorToInt(rej * 30) % 2 == 1;
    errCyan.gameObject.SetActive(j != 0); errRed.gameObject.SetActive(j != 0);
    At(errCyan, sx + j + 3, sy - 8, 52); errCyan.color = C(56, 217, 255, .6f * a);
    At(errRed, sx - j - 3, sy - 8, 52); errRed.color = C(255, 59, 87, .6f * a);
    At(errMain, sx, sy - 8, 52); errMain.color = lit ? C(255, 220, 225, a) : C(255, 90, 105, a); if (glowMat) glowMat.SetColor("_UnderlayColor", C(255, 59, 87, .8f * a));
    At(errSub, sx, sy + 34, 20); errSub.color = C(255, 190, 198, a);
   }
   err.End();
  }

  void ApplyLocks() {
   float u = now - unlockT; pulse.Begin();
   foreach (var v in c.HandViews) {
    if (!v || !Battle.IsDraw(v.card)) continue;
    var lockView = v.GetComponent<DrawLockView>(); if (!lockView) { lockView = v.gameObject.AddComponent<DrawLockView>(); lockView.Build(c, v.card); }
    lockView.Set(LockAmount, Flash);
    if (u >= 0 && u < .8f) { // HTML: strokeRect(r.x - 4 - 10k, r.y - 4 - 10k, r.w + 8 + 20k, r.h + 8 + 20k), 3 px cyan 1 - k, shadowBlur 16
     float k = u / .8f; var r = StageRect((RectTransform)v.transform); Color col = C(56, 217, 255, 1 - k);
     float x = r.x - 4 - 10 * k, y = r.y - 4 - 10 * k, w = r.width + 8 + 20 * k, h = r.height + 8 + 20 * k;
     pulse.StrokeRect(x, y, w, h, 14, A(col, .1f * (1 - k))); pulse.StrokeRect(x, y, w, h, 8, A(col, .2f * (1 - k))); pulse.StrokeRect(x, y, w, h, 3, col);
    }
   }
   pulse.End();
  }
  Rect StageRect(RectTransform t) { var corners = new Vector3[4]; t.GetWorldCorners(corners); Vector3 a = c.stage.InverseTransformPoint(corners[1]), b = c.stage.InverseTransformPoint(corners[3]); var st = c.stage.rect; return new Rect(a.x - st.xMin, st.yMax - a.y, b.x - a.x, a.y - b.y); }
 }

 // the 6 x 10 red card tiles as one mesh (one draw call, no per-frame filter)
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class TileMesh : MaskableGraphic {
  public Sprite sprite; float lk = -1;
  public override Texture mainTexture { get { return sprite ? sprite.texture : s_WhiteTexture; } }
  public void Draw(float time) { if (time < 0 && lk < 0) return; lk = time; SetVerticesDirty(); }
  protected override void OnPopulateMesh(VertexHelper vh) {
   vh.Clear(); if (lk < 0 || !sprite) return;
   var tr = sprite.textureRect; float W = sprite.texture.width, H = sprite.texture.height; float tw = HandFx.Lost.width / 6, th = HandFx.Lost.height / 10;
   float sh = Mathf.Sin(lk * 70) * 10 * Mathf.Max(.3f, 1 - (lk - .75f) / .8f);
   foreach (var t in HandFx.Tiles) {
    if (lk >= t.td) continue; float jx = lk > t.td - .12f ? (Random.value - .5f) * 4 : 0; float x = t.x + sh + jx, y = t.y;
    float u0 = (tr.x + t.col * tr.width / 6) / W, u1 = (tr.x + (t.col + 1) * tr.width / 6) / W, v1 = (tr.y + tr.height - t.row * tr.height / 10) / H, v0 = (tr.y + tr.height - (t.row + 1) * tr.height / 10) / H;
    int i = vh.currentVertCount; Color32 col = color;
    vh.AddVert(new Vector3(x, -y), col, new Vector2(u0, v1)); vh.AddVert(new Vector3(x + tw + .5f, -y), col, new Vector2(u1, v1));
    vh.AddVert(new Vector3(x + tw + .5f, -(y + th + .5f)), col, new Vector2(u1, v0)); vh.AddVert(new Vector3(x, -(y + th + .5f)), col, new Vector2(u0, v0));
    vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
   }
  }
 }

 // Per draw card in the hand: the gray copy (CSS grayscale(.6k) brightness(1 - .4k)) and the hex lock badge with "다음 턴".
 public sealed class DrawLockView : MonoBehaviour {
  Image gray; Material mat; RectTransform deco; CanvasGroup group; float last = -1; bool lastFlash;
  public void Build(BattleController c, Card card) {
   var root = (RectTransform)transform;
   gray = Ui.Rect("잠금 - 회색 카드", root, 0, 0, 126, 201.6f).gameObject.AddComponent<Image>(); gray.sprite = c.feedback.cardImages[(int)card]; gray.raycastTarget = false;
   var sh = Resources.Load<Shader>("UIGray"); if (sh) { mat = new Material(sh); gray.material = mat; }
   deco = Ui.Rect("잠금 배지", root, 0, 0, 126, 201.6f); deco.pivot = new Vector2(.5f, .5f); deco.anchoredPosition = new Vector2(63, -100.8f);
   group = deco.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
   // svg 64 x 72 at (31, 52), viewBox -32 -36: hexagon center (63, 88)
   var p = FxPainter.Create("육각 자물쇠", deco); p.Begin(); var o = new Vector2(63, 88);
   var hex = new[] { new Vector2(0, -32), new Vector2(27.7f, -16), new Vector2(27.7f, 16), new Vector2(0, 32), new Vector2(-27.7f, 16), new Vector2(-27.7f, -16) };
   for (int i = 0; i < 6; i++) hex[i] += o;
   p.FillPoly(hex, new Color(5 / 255f, 8 / 255f, 13 / 255f, .88f)); p.StrokeClosed(hex, 2.5f, HandFx.Gold);
   // lock body: rect -10,-3 20 x 15 rx 2; shackle M-6.5 -3 V-8 A6.5 6.5 0 0 1 6.5 -8 V-3, stroke 3
   p.FillRect(o.x - 10, o.y - 3 + 2, 20, 11, HandFx.Gold); p.FillRect(o.x - 8, o.y - 3, 16, 15, HandFx.Gold);
   foreach (var cc in new[] { new Vector2(-8, -1), new Vector2(8, -1), new Vector2(-8, 10), new Vector2(8, 10) }) p.Disc(o + cc, 2, HandFx.Gold, 8);
   var arc = new List<Vector2> { o + new Vector2(-6.5f, -3), o + new Vector2(-6.5f, -8) };
   for (int i = 1; i <= 12; i++) { float a = Mathf.PI + Mathf.PI * i / 12; arc.Add(o + new Vector2(0, -8) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6.5f); }
   arc.Add(o + new Vector2(6.5f, -3)); p.Poly(arc, 3, HandFx.Gold); p.End();
   var t = Ui.Text("다음 턴", deco, 0, 144 - 1.16f * 18, 126, 40, "다음 턴", c.font, 18, HandFx.Gold); t.alignment = TMPro.TextAlignmentOptions.Top; t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
   Set(0, false);
  }
  public void Set(float k, bool flash) {
   if (Mathf.Approximately(k, last) && flash == lastFlash) return; last = k; lastFlash = flash;
   bool on = k > 0; if (gray.gameObject.activeSelf != on) gray.gameObject.SetActive(on); if (deco.gameObject.activeSelf != on) deco.gameObject.SetActive(on);
   if (mat) { mat.SetFloat("_Gray", .6f * k); mat.SetFloat("_Bright", 1 - .4f * k); }
   group.alpha = k; deco.localScale = Vector3.one * (flash ? 1.12f : 1);
  }
  void OnDestroy() { if (mat) Destroy(mat); }
 }
}

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 // Week 12 item 1 (2026-11-26, 계획/Unity_12주차_적용목록.md): card activation effect chosen per action turn.
 // Line-for-line port of _도구/시안12주차/tabs/01_activate.js, variant "random":
 //  POOL = [지금 빛 띠(now, no dim), 패킷 버스트, 글리치 RGB 분리, 헥스 격자 파문, 데이터 스트림]; one pick per action turn, every activation in the
 //  turn uses it, never the previous turn's pick. u = seconds since the activation (the HTML's T0 = .3 s into each 3.2 s loop).
 // The HTML scene is a straight CH2 line from my server (982,777) to the enemy server (982,201). Here the same drawing follows the
 // packet's real route (ladder turns included); for a straight route the numbers are the HTML's exactly.
 // Everything draws in the event-light layer under the servers (CombatPresentation.BoardEffectUnderServers), so the server hexes stay on top
 // like the HTML's server overlay. The "outside the board 50% dark" layer draws on top of the whole screen like the HTML canvas.
 public static class ActivateFx {
  public static readonly string[] Names = { "지금 빛 띠", "패킷 버스트", "글리치 RGB 분리", "헥스 격자 파문", "데이터 스트림" };
  public const int Now = 0, Packets = 1, Glitch = 2, Hexes = 3, Stream = 4;
  public const float Length = 2.7f;
  static readonly Rect B = new Rect(582, 129, 800, 716);
  const string HEX = "0123456789ABCDEF";
  // const choose = turn => { let k = Math.floor(hash(turn * 7919 + 13) * POOL.length); if (k === pick && turn > 0) k = (k + 1 + Math.floor(hash(turn * 31) * (POOL.length - 1))) % POOL.length; return k; };
  // turn 0 always starts with 패킷 버스트 (pick = 1)
  // 11-26 fix (same as the mock): the JS double hash above lost precision and returned ~0, so every other turn was 'now'.
  // Integer hash (Math.imul equivalent) and pick one of the 4 effects other than the previous one.
  static double IntHash(int n) { uint x = (uint)n; x = (x ^ (x >> 16)) * 0x45d9f3bu; x = (x ^ (x >> 16)) * 0x45d9f3bu; x ^= x >> 16; return x / 4294967296.0; }
  public static int Choose(int turn, int previous) {
   if (turn <= 0) return Packets;
   return (previous + 1 + (int)System.Math.Floor(IntHash(turn * 7919 + 13) * 4)) % 5;
  }
  static float Clamp(float v) { return Mathf.Clamp01(v); }
  static float Ease3(float v) { return 1 - Mathf.Pow(1 - Clamp(v), 3); }
  static float Smooth(float v) { v = Clamp(v); return v * v * (3 - 2 * v); }
  static float Blink(float t) { return EventFx.Blink(t); }
  static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, Mathf.Clamp01(a)); }
  static readonly Color Core = new Color(233 / 255f, 251 / 255f, 1);
  static readonly Color RedSplit = new Color(1, 59 / 255f, 87 / 255f);
  static readonly Color CyanHtml = new Color(56 / 255f, 217 / 255f, 1);

  // 판 밖을 어둡게(0.25초에 50%, 2.3초부터 0.4초에 걸쳐 걷힘)
  public static float DimAlpha(float u) { return .5f * (u < 0 ? 0 : u < .25f ? Smooth(u / .25f) : u < 2.3f ? 1 : 1 - Smooth((u - 2.3f) / .4f)); }

  sealed class Ctx {
   public FxPainter under, clipped, dim; public RectTransform clipRoot, glyphRoot, textRoot; public List<Vector2> path; public float total; public Color cy; public Vector2 start, end; public int endLane;
   public TMP_FontAsset font; public Texture board; public RectTransform glitchClip; public RawImage red, cyan, redAdd; public RawImage[] strips;
   public readonly List<TextMeshProUGUI> glyphs = new List<TextMeshProUGUI>(); public int used; public TextMeshProUGUI[] breach;
   public List<float> nodes; public List<(float x, float y, int code)> cells;
  }

  // the running effect's clock (checks read it while FxClock.Scale is 0)
  public static float LastU { get; private set; } = -1;
  public static int LastPick { get; private set; } = -1;
  // root: a layer under the servers (owned, destroyed at the end); topRoot: a layer over the whole screen for the dim (owned)
  public static IEnumerator Run(int pick, RectTransform root, RectTransform topRoot, List<Vector2> route, int owner, int endLane, Texture boardShot, TMP_FontAsset font, Func<float> delta, Action<float> progress = null) {
   if (pick == Now || route == null || route.Count < 2) { if (root) UnityEngine.Object.Destroy(root.gameObject); if (topRoot) UnityEngine.Object.Destroy(topRoot.gameObject); yield break; }
   root.name = "카드 발동 이펙트 - " + Names[pick];
   var x = new Ctx { path = route, total = PathMath.Length(route), cy = owner == 0 ? CyanHtml : EventFx.Enemy, start = route[0], end = route[route.Count - 1], endLane = endLane, font = font, board = boardShot };
   x.under = FxPainter.Create("발동 그림", root);
   // the HTML clips hexes / stream / glitch to the board: ctx.rect(B.x, B.y, B.w, B.h); ctx.clip()
   x.clipRoot = Ui.Rect("판 안 자르기", root, B.x, B.y, B.width, B.height); x.clipRoot.gameObject.AddComponent<RectMask2D>();
   var inner = Ui.Rect("판 안 그림", x.clipRoot, -B.x, -B.y, 1920, 1080); x.clipped = FxPainter.Create("판 안 발동 그림", inner); x.glyphRoot = Ui.Rect("판 안 글자", inner, 0, 0, 1920, 1080);
   x.textRoot = Ui.Rect("발동 글자", root, 0, 0, 1920, 1080);
   x.dim = FxPainter.Create("판 밖 어둡게", topRoot);
   if (pick == Glitch) GlitchInit(x, root);
   if (pick == Packets) x.nodes = RowNodes(route);
   if (pick == Hexes) x.cells = Cells();
   for (float u = 0; u <= Length; u += delta()) {
    if (!root || !topRoot) yield break;
    progress?.Invoke(u / Length); LastU = u; LastPick = pick;
    Frame(x, pick, u);
    yield return null;
   }
   if (root) UnityEngine.Object.Destroy(root.gameObject); if (topRoot) UnityEngine.Object.Destroy(topRoot.gameObject); LastU = -1;
  }
  // Draw one moment (also used by checks to freeze a frame).
  static void Frame(Ctx x, int pick, float u) {
   x.under.Begin(); x.clipped.Begin(); x.dim.Begin(); x.used = 0;
   float a = DimAlpha(u); if (a > 0) x.dim.FillOutside(B, new Color(0, 0, 0, a));
   float t = u + .3f; // the HTML variant clock: t % 3.2 - .3 = u on the first loop
   if (pick == Packets) DrawPackets(x, u);
   else if (pick == Glitch) DrawGlitch(x, u, t);
   else if (pick == Hexes) DrawHexes(x, u);
   else if (pick == Stream) DrawStream(x, u, t);
   for (int i = x.used; i < x.glyphs.Count; i++) if (x.glyphs[i].gameObject.activeSelf) x.glyphs[i].gameObject.SetActive(false);
   x.under.End(); x.clipped.End(); x.dim.End();
  }

  /* ---------- text (ctx.fillText with textAlign center, y = baseline) ---------- */
  static TextMeshProUGUI Glyph(Ctx x, string s, float size, Vector2 p, Color c) {
   TextMeshProUGUI t;
   if (x.used < x.glyphs.Count) t = x.glyphs[x.used]; else { t = MakeText(x.glyphRoot, x.font, size); x.glyphs.Add(t); }
   x.used++; if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
   if (t.text != s) t.text = s; if (t.fontSize != size) t.fontSize = size; t.color = c; t.rectTransform.anchoredPosition = new Vector2(p.x - 40, -(p.y - 20)); return t;
  }
  static TextMeshProUGUI MakeText(Transform parent, TMP_FontAsset font, float size) {
   var t = Ui.Text("발동 글자", parent, 0, 0, 80, 40, "", font, size, Color.clear); t.alignment = TextAlignmentOptions.Baseline; t.textWrappingMode = TextWrappingModes.NoWrap; return t;
  }

  // 11_queue.js hexGlitch (week 12 item 5): a pointy-top hexagon (r 44, like the servers) cut into 7 horizontal bands that jump left/right
  // for .28 s with red/cyan copies 4 px aside, then settle, flash (fill .25 for .12 s) and fade by .6 s.
  // The canvas shadowBlur (6 while jittering, 16 when settled) is drawn as a wider faint stroke under the line.
  public static void HexGlitch(FxPainter g, Vector2 c, float v, Color side) {
   const float r = 44; const int BANDS = 7; float settle = Mathf.Min(1, v / .28f), amp = 18 * (1 - settle) * (1 - settle);
   float a = v < .28f ? (Mathf.FloorToInt(v * 40) % 3 == 0 ? .35f : 1) : 1 - Mathf.Max(0, (v - .28f) / .32f);
   if (a <= 0) return; var cy = side.r < .5f ? CyanHtml : side;
   for (int b = 0; b < BANDS; b++) {
    float y0 = c.y - r + b * 2 * r / BANDS, h = 2 * r / BANDS; double n = Math.Sin((b + 1) * 12.9898 + Mathf.Floor(v * 30) * 78.233) * 43758.5453; float dx = (float)((n - Math.Floor(n) - .5) * 2 * amp);
    if (amp > 1) { g.StrokeClosedClipY(FxPainter.HexPoints(new Vector2(c.x + dx - 4, c.y), r, Mathf.PI / 2), 3, A(RedSplit, .7f * a), y0, y0 + h + .5f); g.StrokeClosedClipY(FxPainter.HexPoints(new Vector2(c.x + dx + 4, c.y), r, Mathf.PI / 2), 3, A(CyanHtml, .7f * a), y0, y0 + h + .5f); }
    var hex = FxPainter.HexPoints(new Vector2(c.x + dx, c.y), r, Mathf.PI / 2);
    g.StrokeClosedClipY(hex, settle >= 1 ? 4 + 16 : 4 + 6, A(CyanHtml, (settle >= 1 ? .18f : .22f) * a), y0, y0 + h + .5f);
    g.StrokeClosedClipY(hex, 4, settle >= 1 ? A(Core, a) : A(cy, a), y0, y0 + h + .5f);
   }
   if (v >= .28f && v < .4f) g.FillPoly(FxPainter.HexPoints(c, r, Mathf.PI / 2), A(CyanHtml, .25f * (1 - (v - .28f) / .12f)));
  }
  static void HexPath(FxPainter g, Vector2 c, float r, Color col, float w) { g.StrokeClosed(FxPainter.HexPoints(c, r, Mathf.PI / 6), w, col); }

  /* ---------- 1. 패킷 버스트 ---------- */
  // rung rows the path passes (HTML: U.RowY on the straight CH2 line)
  static List<float> RowNodes(List<Vector2> p) {
   var list = new List<float>(); float acc = 0;
   for (int i = 1; i < p.Count; i++) {
    Vector2 a = p[i - 1], b = p[i]; float l = Vector2.Distance(a, b);
    foreach (float ry in BattleController.RowY) {
     if (Mathf.Abs(a.x - b.x) < .5f && (ry - a.y) * (ry - b.y) <= 0 && Mathf.Abs(b.y - a.y) > .5f) { float d = acc + Mathf.Abs(ry - a.y); if (!list.Exists(v => Mathf.Abs(v - d) < 1)) list.Add(d); }
     else if (Mathf.Abs(a.y - b.y) < .5f && Mathf.Abs(a.y - ry) < .5f) { foreach (float d in new[] { acc, acc + l }) if (!list.Exists(v => Mathf.Abs(v - d) < 1)) list.Add(d); }
    }
    acc += l;
   }
   return list;
  }
  static void DrawPackets(Ctx x, float u) {
   if (u < 0) return; var g = x.under; var cy = x.cy;
   float fade = u < 2.2f ? 1 : 1 - Clamp((u - 2.2f) / .4f);
   // 세로선 자체를 6px 로 밝힘
   g.Poly(x.path, 6, A(cy, .35f * fade * Clamp(u / .1f)));
   g.Ring(x.start, 30 + 40 * Ease3(u / .4f), 3, A(cy, Clamp(1 - u / .4f)));
   const int N = 5;
   for (int k = 0; k < N; k++) {
    float v = (u - k * .1f) / .55f; if (v < 0) continue;
    if (v < 1) {
     float d = v * v * x.total; Vector2 head = PathMath.At(x.path, d); // 점점 빨라짐
     // gradient head core(.9) -> cy(0) over 90 px behind the head (clipped at the start)
     DrawTrail(g, x.path, d, cy);
     // shadowBlur 22 #38d9ff under the 14 px #e9fbff square (soft edge stand-in for the blur)
     g.SoftRect(head.x - 7, head.y - 7, 14, 14, 22, A(CyanHtml, .2f));
     g.FillRect(head.x - 7, head.y - 7, 14, 14, new Color(233 / 255f, 251 / 255f, 1));
    }
    // 지나간 마디(가로 단 자리) 점등
    foreach (float nd in x.nodes) { float pass = nd / x.total, at = Mathf.Sqrt(pass); float w0 = v - at; if (w0 > 0 && w0 < .5f) { float a = 1 - w0 / .5f, s = 18 + 26 * w0; var p = PathMath.At(x.path, nd); g.StrokeRect(p.x - s / 2, p.y - s / 2, s, s, 2, A(cy, a)); } }
    // 도착: 조각 10개가 사방으로
    float w = (u - k * .1f - .55f) / .45f;
    if (w > 0 && w < 1) {
     for (int i = 0; i < 10; i++) { float ang = Mathf.PI * 2 * (i / 10f + k * .037f), r = 30 + 80 * Ease3(w); g.FillRect(x.end.x + Mathf.Cos(ang) * r - 3, x.end.y + Mathf.Sin(ang) * r - 3, 6, 6, A(Core, 1 - w)); }
     HexPath(g, x.end, 40 + 26 * w, A(cy, 1 - w), 3);
    }
   }
   // (사용자 11-26: 마지막 글자는 넣을 필요 없음 — 도착 글자 없음)
  }
  // ctx.createLinearGradient(0, y, 0, y + 90): core(.9) at the head, cy(0) 90 px behind; lineWidth 4
  static void DrawTrail(FxPainter g, List<Vector2> path, float d, Color cy) {
   float from = Mathf.Max(0, d - 90); if (d - from < .01f) return;
   float acc = 0;
   for (int i = 1; i < path.Count; i++) {
    float l = Vector2.Distance(path[i - 1], path[i]); float s0 = Mathf.Max(from, acc), s1 = Mathf.Min(d, acc + l);
    if (s1 > s0) {
     Vector2 a = Vector2.Lerp(path[i - 1], path[i], (s0 - acc) / l), b = Vector2.Lerp(path[i - 1], path[i], (s1 - acc) / l);
     Color ca = Color.Lerp(A(Core, .9f), A(cy, 0), (d - s0) / 90), cb = Color.Lerp(A(Core, .9f), A(cy, 0), (d - s1) / 90);
     g.Line(a, b, 4, ca, cb);
    }
    acc += l;
   }
  }

  /* ---------- 2. 글리치 RGB 분리 ---------- */
  // The HTML draws both tinted copies with 'screen' onto its transparent effect canvas and lays that canvas over the board:
  // per pixel F = base(1-s)^2 + R s + C s - s^2 R C  (s = .75k, R / C = board x tint). Unity: R over (alpha s), C over (alpha s),
  // then R s^2 added (Resources/UIAddTint.shader); the remaining -s^2 R C term (< 4% of a channel here) is dropped.
  static Material add;
  static Material Add { get { if (!add) { var sh = Resources.Load<Shader>("UIAddTint"); if (sh) add = new Material(sh); } return add; } }
  static void GlitchInit(Ctx x, RectTransform root) {
   x.glitchClip = Ui.Rect("글리치 자르기", root, B.x, B.y, B.width, B.height); x.glitchClip.gameObject.AddComponent<RectMask2D>();
   x.glitchClip.SetSiblingIndex(0);
   RawImage Copy(string name, Color c) { var r = Ui.Rect(name, x.glitchClip, 0, 0, B.width, B.height); var im = r.gameObject.AddComponent<RawImage>(); im.texture = x.board; im.color = c; im.raycastTarget = false; im.uvRect = new Rect(B.x / 1920f, 1 - B.yMax / 1080f, B.width / 1920f, B.height / 1080f); im.enabled = false; return im; }
   // red / cyan: drawImage(board) then multiply by the tint, composited with 'screen' at globalAlpha .75k
   x.red = Copy("글리치 빨강 겹", new Color(1, 40 / 255f, 70 / 255f, 0)); x.cyan = Copy("글리치 청록 겹", new Color(40 / 255f, 230 / 255f, 1, 0));
   x.redAdd = Copy("글리치 빨강 겹 - 화면 혼합 보정", new Color(1, 40 / 255f, 70 / 255f, 0)); if (Add) x.redAdd.material = Add;
   x.strips = new RawImage[7]; for (int i = 0; i < 7; i++) x.strips[i] = Copy("글리치 찢김 띠 " + i, Color.white);
   x.breach = new TextMeshProUGUI[3]; for (int i = 0; i < 3; i++) { x.breach[i] = MakeText(x.textRoot, x.font, 24); x.breach[i].text = "BREACH  CH " + (x.endLane + 1); }
  }
  static void DrawGlitch(Ctx x, float u, float t) {
   foreach (var tx in x.breach) tx.gameObject.SetActive(false);
   x.red.enabled = x.cyan.enabled = x.redAdd.enabled = false; foreach (var s in x.strips) s.enabled = false;
   if (u < 0) return; var g = x.under;
   int tick = Mathf.FloorToInt(t * 30);
   bool Burst(float u0, float len, float amp, float y0, float y1) {
    float v = (u - u0) / len; if (v < 0 || v > 1) return false; float k = 1 - Mathf.Abs(v * 2 - 1);
    if (!x.board) return true;
    y0 = Mathf.Max(y0, B.y); y1 = Mathf.Min(y1, B.yMax);
    x.glitchClip.anchoredPosition = new Vector2(B.x, -y0); x.glitchClip.sizeDelta = new Vector2(B.width, y1 - y0);
    // copies are positioned in stage pixels relative to the clip rect
    void Place(RawImage im, float px, float py, float h, float srcY) { var r = im.rectTransform; r.anchoredPosition = new Vector2(px - B.x, -(py - y0)); r.sizeDelta = new Vector2(B.width, h); im.uvRect = new Rect(B.x / 1920f, 1 - (srcY + h) / 1080f, B.width / 1920f, h / 1080f); }
    Place(x.red, B.x - amp * k, B.y, B.height, B.y); x.red.color = new Color(1, 40 / 255f, 70 / 255f, .75f * k); x.red.enabled = true;
    Place(x.cyan, B.x + amp * k, B.y, B.height, B.y); x.cyan.color = new Color(40 / 255f, 230 / 255f, 1, .75f * k); x.cyan.enabled = true;
    if (Add) { Place(x.redAdd, B.x - amp * k, B.y, B.height, B.y); x.redAdd.color = new Color(1, 40 / 255f, 70 / 255f, .75f * k * .75f * k); x.redAdd.enabled = true; }
    for (int i = 0; i < 7; i++) {
     float sy = y0 + JsMath.Hash(tick * 31 + i * 7) * (y1 - y0), sh = 6 + JsMath.Hash(tick * 17 + i) * 44, dx = (JsMath.Hash(tick * 13 + i * 3) - .5f) * amp * 2.6f * k;
     if (k > .15f) { Place(x.strips[i], B.x + dx, sy, sh, sy); x.strips[i].enabled = true; }
    }
    return true;
   }
   if (!Burst(0, .42f, 26, B.y, B.yMax))                       // 발동: 판 전체가 찢김
    Burst(.62f, .25f, 18, x.end.y - 70, x.end.y + 70);        // 도착: 상대 서버 줄만
   // 세로선: 계단처럼 끊기며 올라가는 RGB 3겹 선
   float v = u - .28f;
   if (v > 0) {
    float a = Blink(v), reach = Mathf.Min(1, Mathf.Floor(v / .04f) * .12f); var part = PathMath.Slice(x.path, 0, reach * x.total);
    float jx = v < .4f ? (JsMath.Hash(tick) - .5f) * 10 : 0;
    if (part.Count >= 2) foreach (var (dx, col, w) in new[] { (-4f, A(RedSplit, .8f * a), 3f), (4f, A(CyanHtml, .8f * a), 3f), (0f, A(Core, a), 4f) }) {
     var moved = new List<Vector2>(); foreach (var p in part) moved.Add(p + new Vector2(dx + jx, 0)); g.Poly(moved, w, col);
    }
   }
   if (u > .62f && u < 2.3f) {
    float a = Clamp((u - .62f) / .1f) * (u > 2 ? 1 - (u - 2) / .3f : 1), j = u < .9f ? (JsMath.Hash(tick + 5) - .5f) * 8 : 0;
    // HTML: baseline EN + 96 (toward the board middle from the arrival server)
    float by = x.end.y + (x.end.y < 489 ? 96 : -78);
    var cols = new[] { A(RedSplit, .7f * a), A(CyanHtml, .7f * a), A(Core, a) }; var xs = new[] { x.end.x - 3 + j, x.end.x + 3 - j, x.end.x };
    for (int i = 0; i < 3; i++) { var tx = x.breach[i]; tx.gameObject.SetActive(true); tx.color = cols[i]; tx.rectTransform.anchoredPosition = new Vector2(xs[i] - 40, -(by - 20)); tx.rectTransform.sizeDelta = new Vector2(80, 40); }
   }
  }

  /* ---------- 3. 헥스 격자 파문 ---------- */
  const float R = 24;
  static List<(float, float, int)> Cells() {
   var cells = new List<(float, float, int)>();
   for (int row = 0; ; row++) { float y = B.y + R + row * 1.5f * R; if (y >= B.yMax) break;
    for (float cx = B.x + (row % 2 == 1 ? Mathf.Sqrt(3) * R / 2 : 0); cx < B.xMax + R; cx += Mathf.Sqrt(3) * R) cells.Add((cx, y, Mathf.FloorToInt(JsMath.Hash(cells.Count + 3) * 256))); }
   return cells;
  }
  // HTML: Math.abs(x - X) < 30 && y > along(reveal) - 10 — within 30 px across the revealed part of the path (and 10 px past its head;
  // the first segment reaches back past the start, as the HTML's line has no lower end)
  static bool NearRevealed(List<Vector2> path, float reveal, Vector2 c) {
   float acc = 0;
   for (int i = 1; i < path.Count; i++) {
    Vector2 a = path[i - 1], b = path[i]; float l = Vector2.Distance(a, b); if (l < .01f) continue; var dir = (b - a) / l;
    float along = Vector2.Dot(c - a, dir), across = Mathf.Abs(dir.x * (c - a).y - dir.y * (c - a).x);
    float lo = i == 1 ? float.NegativeInfinity : 0, hi = Mathf.Min(l, reveal - acc) + 10;
    if (reveal - acc > 0 && across < 30 && along > lo && along < hi) return true;
    acc += l;
   }
   return false;
  }
  static void DrawHexes(Ctx x, float u) {
   if (u < 0) return; var g = x.clipped; var cy = x.cy;
   float r1 = 1100 * u, r2 = 950 * (u - .55f), fade = u < 1.9f ? 1 : 1 - Clamp((u - 1.9f) / .6f);
   float reveal = Ease3((u - .1f) / .5f) * x.total;
   foreach (var (cx, cyy, code) in x.cells) {
    var c = new Vector2(cx, cyy); float d1 = Vector2.Distance(c, x.start), d2 = Vector2.Distance(c, x.end);
    float I = Mathf.Exp(-Mathf.Pow((d1 - r1) / 46, 2)) * Clamp(1 - u / 1.3f);
    if (u > .55f) I = Mathf.Max(I, Mathf.Exp(-Mathf.Pow((d2 - r2) / 40, 2)) * Clamp(1 - (u - .55f) / 1.1f));
    if (NearRevealed(x.path, reveal, c)) I = Mathf.Max(I, .55f * Blink(Mathf.Max(0, u - .1f)));
    I *= fade; if (I < .03f) continue;
    var hex = FxPainter.HexPoints(c, R - 2, Mathf.PI / 6); g.FillPoly(hex, A(cy, .12f * I)); g.StrokeClosed(hex, 1.5f, A(cy, .7f * I));
    if (code % 4 == 0) Glyph(x, HEX[code >> 4].ToString() + HEX[code & 15], 13, new Vector2(cx, cyy + 5), A(Core, .9f * I));
   }
   if (u > .55f && u < 1.1f) x.under.Ring(x.end, 30 + 40 * Ease3((u - .55f) / .55f), 4, A(Core, 1 - (u - .55f) / .55f));
  }

  /* ---------- 4. 데이터 스트림 ---------- */
  static void DrawStream(Ctx x, float u, float t) {
   if (u < 0) return; var cy = x.cy;
   float fade = u < 2.1f ? 1 : 1 - Clamp((u - 2.1f) / .5f); int tick = Mathf.FloorToInt(t * 20);
   // 판 전체의 희미한 비: 40px 간격 열이 천천히 내려옴 (the route's vertical lines are kept clear: |x - X| < 50)
   for (int c = 0; c < 20; c++) {
    float cx = B.x + 20 + c * 40; bool skip = false;
    for (int i = 1; i < x.path.Count; i++) if (Mathf.Abs(x.path[i].x - x.path[i - 1].x) < .5f && Mathf.Abs(cx - x.path[i].x) < 50) skip = true;
    if (skip) continue;
    float off = (JsMath.Hash(c) * 700 + u * 160) % 760;
    for (int k = 0; k < 9; k++) { float y = B.y + off - k * 24; if (y < B.y || y > B.yMax) continue; Glyph(x, HEX[Mathf.FloorToInt(JsMath.Hash(c * 97 + k + tick) * 16)].ToString(), 20, new Vector2(cx, y), A(cy, .10f * (1 - k / 9f) * fade)); }
   }
   // CH 다섯 줄: 경로를 따라 솟는 헥스 문자, 머리는 흰색
   float[] dxs = { -36, -18, 0, 18, 36 };
   for (int c = 0; c < 5; c++) {
    float delay = JsMath.Hash(c + 40) * .22f, speed = 1000 + JsMath.Hash(c + 9) * 500; if (u < delay) continue; float head = (u - delay) * speed;
    for (int k = 0; k < 16; k++) {
     float d = head - k * 24; if (d < 0 || d > x.total + 10) continue; float a = (k == 0 ? 1 : .85f * (1 - k / 16f)) * fade;
     Vector2 dir = PathMath.Dir(x.path, Mathf.Min(d, x.total)), p = PathMath.At(x.path, Mathf.Min(d, x.total)) + dir * Mathf.Max(0, d - x.total), n = new Vector2(-dir.y, dir.x);
     Glyph(x, HEX[Mathf.FloorToInt(JsMath.Hash(c * 131 + k * 7 + tick) * 16)].ToString(), 20, p + n * dxs[c], k == 0 ? A(Core, a) : A(cy, a));
    }
   }
   float v = u - .55f; if (v > 0 && v < .6f) { float k = v / .6f; x.under.FillPoly(FxPainter.HexPoints(x.end, 36 + 10 * k, Mathf.PI / 6), A(Core, .5f * (1 - k))); x.under.Ring(x.end, 40 + 40 * Ease3(k), 3, A(cy, 1 - k)); }
  }
 }
}

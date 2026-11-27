using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 // Card activation effect. Week 12 item 1 port of _도구/시안12주차/tabs/01_activate.js, generalized in week 13 to
 // _도구/시안13주차/tabs/11_fxpick.js: every effect follows ANY polyline (mkPath: at / tan / nor / trace / near), and it plays for
 // loading (lane route), rung place / remove (the rung segment) and the resolve fire (the firing route, CombatPresentation.OnFireRoute).
 //  Selectable pool (11-27 user "패킷 버스트 빼고 5. 락온 괄호 + 터미널 입력 넣고 싶음"): 지금 빛 띠 · 글리치 RGB 분리 · 헥스 격자 파문 ·
 //  데이터 스트림 · 락온 괄호 + 터미널, plus "무작위" (EffectPicker). 패킷 버스트 stays in code (checks may force it) but is not offered.
 //  u = seconds since the activation (the HTML's T0 = .3 s into each 3.2 s loop).
 // Everything draws in the event-light layer under the servers (CombatPresentation.BoardEffectUnderServers), so the server hexes stay on top
 // like the HTML's server overlay. The "outside the board 50% dark" layer, the terminal window and the LOCK / word texts draw on the top layer.
 public static class ActivateFx {
  public static readonly string[] Names = { "지금 빛 띠", "패킷 버스트", "글리치 RGB 분리", "헥스 격자 파문", "데이터 스트림", "락온 괄호 + 터미널" };
  public const int Now = 0, Packets = 1, Glitch = 2, Hexes = 3, Stream = 4, Lockon = 5;
  // 11_fxpick.js FX order (the picker tiles and the per-turn random use this order)
  public static readonly int[] Pool = { Now, Glitch, Hexes, Stream, Lockon };
  public const float Length = 2.7f;
  static readonly Rect B = new Rect(582, 129, 800, 716);
  const string HEX = "0123456789ABCDEF";
  const float S = 1.33f;
  // 11_fxpick.js / 11-26 fix: integer hash (Math.imul equivalent). Every 13th-week effect uses this hash.
  static float IntHash(int n) { uint x = (uint)n; x = (x ^ (x >> 16)) * 0x45d9f3bu; x = (x ^ (x >> 16)) * 0x45d9f3bu; x ^= x >> 16; return (float)(x / 4294967296.0); }
  // per-turn random ("무작위"): st.turnPick = (turnPick + 1 + floor(hash(cycle * 7919 + 13) * 4)) % 5 over the pool — never the previous pick.
  // turn 0 starts with the mockup's first value (st.turnPick = 4 = 락온).
  public static int Choose(int turn, int previous) {
   if (turn <= 0) return Lockon;
   int at = Array.IndexOf(Pool, previous); if (at < 0) at = 0;
   return Pool[(at + 1 + (int)Math.Floor(IntHash(turn * 7919 + 13) * 4)) % Pool.Length];
  }
  static float Clamp(float v) { return Mathf.Clamp01(v); }
  static float Ease3(float v) { return 1 - Mathf.Pow(1 - Clamp(v), 3); }
  static float Smooth(float v) { v = Clamp(v); return v * v * (3 - 2 * v); }
  static float Blink(float t) { return EventFx.Blink(t); }
  static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, Mathf.Clamp01(a)); }
  static readonly Color Core = new Color(233 / 255f, 251 / 255f, 1);
  static readonly Color RedSplit = new Color(1, 59 / 255f, 87 / 255f);
  static readonly Color CyanHtml = new Color(56 / 255f, 217 / 255f, 1);
  static readonly Color LockGold = new Color(1, 204 / 255f, 51 / 255f);
  static readonly Color TermOk = new Color(75 / 255f, 227 / 255f, 138 / 255f);

  // 판 밖을 어둡게(0.25초에 50%, 2.3초부터 0.4초에 걸쳐 걷힘)
  public static float DimAlpha(float u) { return .5f * (u < 0 ? 0 : u < .25f ? Smooth(u / .25f) : u < 2.3f ? 1 : 1 - Smooth((u - 2.3f) / .4f)); }

  /* ---------- scenes (11_fxpick.js SC / 05_drop.js mountDrop) ---------- */
  public sealed class Scene {
   public List<Vector2> Path; public int Owner;
   public string Word; public Vector2 WordAt;           // glitch word (BREACH / LINK / CUT / FIRE)
   public string[] Term; public Vector2 TermAt;         // lockon terminal (null = no window)
   public bool Dim = true;                              // the outside-the-board dark (one per batch)
   public bool Board = true;                            // board-wide parts: glitch board copies, stream rain (one per batch)
   public List<float> Nodes;                            // packet burst: rung rows passed
  }
  static Vector2 WordNear(Vector2 e) { return new Vector2(e.x, e.y + (e.y < 489 ? 96 : -78)); }
  static int LaneOf(float x) { int best = 0; for (int i = 1; i < 3; i++) if (Mathf.Abs(x - BattleController.LaneX[i]) < Mathf.Abs(x - BattleController.LaneX[best])) best = i; return best; }
  // load: my server -> the arrival server along the packet's real route (05_drop.js: word BREACH CH n, termAt [lane 3 ? 1000 : 1040, 400])
  public static Scene LoadScene(List<Vector2> route, int owner, int startLane, int endLane, bool zeroDay) {
   var e = route[route.Count - 1];
   return new Scene { Path = route, Owner = owner, Word = "BREACH  CH " + (endLane + 1), WordAt = WordNear(e), Nodes = RowNodes(route),
    Term = new[] { (zeroDay ? "> exec zeroday --ch " : "> exec inject --ch ") + (startLane + 1), "  route  CH" + (startLane + 1) + " → " + (owner == 0 ? "ENEMY" : "MY") + " CH" + (endLane + 1), zeroDay ? "  pass  1  loaded" : "  packet  +1  loaded", "  [ OK ]" },
    TermAt = new Vector2(startLane == 2 ? 1000 : 1040, 400) };
  }
  // rung place / remove: left node -> right node of that row (05_drop.js: LINK / CUT ROW n, termAt [1000, min(690, cy + 40)])
  public static Scene RungScene(int slot, bool removed, int owner) {
   int row = slot / 2, edge = slot % 2; float y = BattleController.RowY[row];
   Vector2 a = new Vector2(BattleController.LaneX[edge], y), b = new Vector2(BattleController.LaneX[edge + 1], y);
   return new Scene { Path = new List<Vector2> { a, b }, Owner = owner, Word = (removed ? "CUT  ROW " : "LINK  ROW ") + (row + 1), WordAt = new Vector2((a.x + b.x) / 2, y + 64), Nodes = new List<float>(),
    Term = new[] { "> exec reroute --row " + (row + 1), (removed ? "  unlink  CH" : "  link  CH") + (edge + 1) + " ⇄ CH" + (edge + 2), removed ? "  rung  removed" : "  rung  placed", "  [ OK ]" },
    TermAt = new Vector2(1000, Mathf.Min(690, y + 40)) };
  }
  // fire: the firing route (11_fxpick.js 'fire': word FIRE CH n, terminal "> fire --ch n"). The terminal lines are set by the caller per volley.
  public static Scene FireScene(List<Vector2> route, int owner) {
   var e = route[route.Count - 1]; int s = LaneOf(route[0].x);
   return new Scene { Path = route, Owner = owner, Word = "FIRE  CH " + (s + 1), WordAt = WordNear(e), Nodes = RowNodes(route), TermAt = new Vector2(1040, 400) };
  }
  // 지금 빛 띠 = the event light (EventFx) along the scene path: ring at 26S from the start (10S -> 34S), the path glow from .15 s,
  // the arrival ring (28 -> 52) from .6 s — 11_fxpick.js now() without the "+1 장전" box (the result shows it).
  public static EventFx.Plan NowPlan(Scene sc) {
   var plan = new EventFx.Plan(); var col = EventFx.Side(sc.Owner); var p = sc.Path;
   plan.Rings.Add((PathMath.At(p, 26 * S), col, 10 * S, 34 * S, 0)); plan.Halos.Add((p, col, .15f)); plan.Rings.Add((p[p.Count - 1], col, 28, 52, .6f));
   return plan;
  }

  internal sealed class Ctx {
   public FxPainter under, clipped, dim, top; public RectTransform glyphRoot, textRoot; public List<Vector2> path; public float total; public Color cy; public Vector2 start, end;
   public Scene sc; public bool mini;
   public TMP_FontAsset font; public Texture board; public RectTransform glitchClip; public RawImage red, cyan, redAdd; public RawImage[] strips;
   public readonly List<TextMeshProUGUI> glyphs = new List<TextMeshProUGUI>(); public int used; public TextMeshProUGUI[] words; public TextMeshProUGUI lockText; public TextMeshProUGUI[] term;
   public List<float> nodes;
  }

  // the running effect's clock (checks read it while FxClock.Scale is 0)
  public static float LastU { get; private set; } = -1;
  public static int LastPick { get; private set; } = -1;
  // 11-28 사용자 "터미널 발동 효과에 배속이 안되는 부분이있음": 효과는 카드 처리와 따로 돌아 처리가 먼저 끝나면 배속 조건이 꺼졌다. 도는 동안 배속을 받게 알린다
  static int ActiveFrame = -10;
  public static bool Running { get { return Time.frameCount - ActiveFrame <= 1; } }
  // Week 12 signature (load along the packet route) — kept for callers that do not build a scene.
  public static IEnumerator Run(int pick, RectTransform root, RectTransform topRoot, List<Vector2> route, int owner, int endLane, Texture boardShot, TMP_FontAsset font, Func<float> delta, Action<float> progress = null) {
   int startLane = route != null && route.Count > 0 ? LaneOf(route[0].x) : 0;
   return Run(pick, root, topRoot, route == null || route.Count < 2 ? null : LoadScene(route, owner, startLane, endLane, false), boardShot, font, delta, 0, progress);
  }
  // root: a layer under the servers (owned, destroyed at the end); topRoot: a layer over the board for the dim, words and terminal (owned).
  // delay: seconds (of delta) before u = 0 (the fire effect waits for the charge).
  public static IEnumerator Run(int pick, RectTransform root, RectTransform topRoot, Scene sc, Texture boardShot, TMP_FontAsset font, Func<float> delta, float delay = 0, Action<float> progress = null) {
   if (pick == Now || sc == null || sc.Path == null || sc.Path.Count < 2) { if (root) UnityEngine.Object.Destroy(root.gameObject); if (topRoot) UnityEngine.Object.Destroy(topRoot.gameObject); yield break; }
   for (float w = 0; w < delay; w += delta()) { if (!root || !topRoot) yield break; yield return null; }
   if (!root || !topRoot) yield break;
   root.name = "카드 발동 이펙트 - " + Names[pick];
   var x = new Ctx { sc = sc, path = sc.Path, total = PathMath.Length(sc.Path), cy = sc.Owner == 0 ? CyanHtml : EventFx.Enemy, start = sc.Path[0], end = sc.Path[sc.Path.Count - 1], font = font, board = boardShot, nodes = sc.Nodes ?? new List<float>() };
   x.under = FxPainter.Create("발동 그림", root);
   // the HTML clips hexes / stream / glitch to the board: ctx.rect(B.x, B.y, B.w, B.h); ctx.clip()
   var clipRoot = Ui.Rect("판 안 자르기", root, B.x, B.y, B.width, B.height); clipRoot.gameObject.AddComponent<RectMask2D>();
   var inner = Ui.Rect("판 안 그림", clipRoot, -B.x, -B.y, 1920, 1080); x.clipped = FxPainter.Create("판 안 발동 그림", inner); x.glyphRoot = Ui.Rect("판 안 글자", inner, 0, 0, 1920, 1080);
   x.dim = FxPainter.Create("판 밖 어둡게", topRoot);
   x.top = FxPainter.Create("발동 창 바탕", topRoot);
   x.textRoot = Ui.Rect("발동 글자", topRoot, 0, 0, 1920, 1080);
   if (pick == Glitch) GlitchInit(x, root);
   if (pick == Lockon) LockInit(x);
   for (float u = 0; u <= Length; u += delta()) {
    if (!root || !topRoot) yield break;
    progress?.Invoke(u / Length); LastU = u; LastPick = pick; ActiveFrame = Time.frameCount;
    Frame(x, pick, u);
    yield return null;
   }
   if (root) UnityEngine.Object.Destroy(root.gameObject); if (topRoot) UnityEngine.Object.Destroy(topRoot.gameObject); LastU = -1;
  }
  // Draw one moment.
  static void Frame(Ctx x, int pick, float u) {
   x.under.Begin(); x.clipped.Begin(); if (x.dim) x.dim.Begin(); if (x.top) x.top.Begin(); x.used = 0;
   if (x.mini) Lanes(x.under);
   if (x.dim && x.sc.Dim) { float a = DimAlpha(u); if (a > 0) x.dim.FillOutside(B, new Color(0, 0, 0, a)); }
   float t = u + .3f; // the HTML variant clock: t % 3.2 - .3 = u on the first loop
   if (pick == Now) DrawNow(x, u);
   else if (pick == Packets) DrawPackets(x, u);
   else if (pick == Glitch) DrawGlitch(x, u, t);
   else if (pick == Hexes) DrawHexes(x, u);
   else if (pick == Stream) DrawStream(x, u, t);
   else if (pick == Lockon) DrawLockon(x, u);
   for (int i = x.used; i < x.glyphs.Count; i++) if (x.glyphs[i].gameObject.activeSelf) x.glyphs[i].gameObject.SetActive(false);
   x.under.End(); if (x.clipped != x.under) x.clipped.End(); if (x.dim) x.dim.End(); if (x.top) x.top.End();
  }

  /* ---------- picker previews (11_fxpick.js drawMini) ---------- */
  // The mini board: the board scaled to the tile (k = min(w / B.w, h / B.h)), CH lines rgba(56,217,255,.35) 6 px, the effect on the
  // straight CH2 line 777 -> 201 with mini = true (no glitch board copies, no hex codes, no terminal or words).
  public sealed class Preview { internal Ctx x; }
  public static Preview CreatePreview(FxPainter g) {
   var path = new List<Vector2> { new Vector2(982, 777), new Vector2(982, 201) };
   return new Preview { x = new Ctx { mini = true, under = g, clipped = g, path = path, total = PathMath.Length(path), cy = CyanHtml, start = path[0], end = path[1], sc = new Scene { Path = path, Dim = false, Board = true, Nodes = RowNodes(path) }, nodes = RowNodes(path) } };
  }
  // t: the tile clock; u = (t % 2.6) - .2 like drawMini
  public static void DrawPreview(Preview p, int pick, float t) { float u = (t % 2.6f) - .2f; Frame(p.x, pick, u); }
  static void Lanes(FxPainter g) { foreach (float lx in BattleController.LaneX) g.Line(new Vector2(lx, 201), new Vector2(lx, 777), 6, A(CyanHtml, .35f)); }

  /* ---------- path helpers (mkPath) ---------- */
  static Vector2 Nor(List<Vector2> p, float d) { var t = PathMath.Dir(p, Mathf.Clamp(d, 0, PathMath.Length(p))); return new Vector2(-t.y, t.x); }
  // P.trace(d0, d1, off): the path between d0 and d1 offset by 'off' along each segment's normal (corners get both offsets)
  static List<Vector2> Trace(List<Vector2> p, float d0, float d1, float off) {
   var list = new List<Vector2>(); float L = PathMath.Length(p); d0 = Mathf.Max(0, d0); d1 = Mathf.Min(L, d1); if (d1 <= d0) return list;
   void Put(float d) { list.Add(PathMath.At(p, d) + Nor(p, d) * off); }
   Put(d0); float acc = 0; for (int i = 1; i < p.Count - 1; i++) { acc += Vector2.Distance(p[i - 1], p[i]); if (acc > d0 && acc < d1) { Put(acc - .001f); Put(acc + .001f); } }
   Put(d1); return list;
  }
  // P.near(x, y): distance to the path and the path position of the nearest point
  static (float d, float at) Near(List<Vector2> p, Vector2 q) {
   float best = 1e9f, at = 0, acc = 0;
   for (int i = 1; i < p.Count; i++) { Vector2 a = p[i - 1], b = p[i], d = b - a; float l2 = d.sqrMagnitude; if (l2 < 1e-6f) l2 = 1; float k = Clamp(Vector2.Dot(q - a, d) / l2); var pp = a + d * k; float dist = Vector2.Distance(q, pp); float len = Vector2.Distance(a, b); if (dist < best) { best = dist; at = acc + k * len; } acc += len; }
   return (best, at);
  }

  /* ---------- text (ctx.fillText with textAlign center, y = baseline) ---------- */
  static void Glyph(Ctx x, string s, float size, Vector2 p, Color c) {
   if (x.mini) { x.clipped.FillRect(p.x - size * .3f, p.y - size * .7f, size * .6f, size * .7f, c); return; } // a glyph is ~2 px in a tile
   TextMeshProUGUI t;
   if (x.used < x.glyphs.Count) t = x.glyphs[x.used]; else { t = MakeText(x.glyphRoot, x.font, size); x.glyphs.Add(t); }
   x.used++; if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
   if (t.text != s) t.text = s; if (t.fontSize != size) t.fontSize = size; t.color = c; t.rectTransform.anchoredPosition = new Vector2(p.x - 40, -(p.y - 20));
  }
  static TextMeshProUGUI MakeText(Transform parent, TMP_FontAsset font, float size) {
   var t = Ui.Text("발동 글자", parent, 0, 0, 80, 40, "", font, size, Color.clear); t.alignment = TextAlignmentOptions.Baseline; t.textWrappingMode = TextWrappingModes.NoWrap; return t;
  }
  static void Place(TextMeshProUGUI t, string s, Vector2 baseline, float w, Color c) { if (t.text != s) t.text = s; t.color = c; t.rectTransform.sizeDelta = new Vector2(w, 40); t.rectTransform.anchoredPosition = new Vector2(baseline.x - w / 2, -(baseline.y - 20)); t.gameObject.SetActive(true); }

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

  /* ---------- 0. 지금 빛 띠 (previews only; the game uses NowPlan / EventFx) ---------- */
  static void DrawNow(Ctx x, float u) {
   if (u < 0) return; var g = x.under; var cy = x.cy;
   { float k = Clamp(u / .5f); var p = PathMath.At(x.path, 26 * S); if (k < 1) g.Ring(p, 10 * S + 24 * S * Ease3(k), 3, A(cy, 1 - k)); }
   { float v = u - .15f; if (v > 0) { float a = Blink(v), d = Ease3(v / .45f) * x.total; var part = PathMath.Slice(x.path, 0, d);
     // blur(9S) under a 30S stroke at .55: a wide faint band and a narrower one
     if (part.Count >= 2) { g.Stroke(part, 30 * S + 18 * S, A(cy, .2f * a), true); g.Stroke(part, 30 * S, A(cy, .3f * a), true); g.Stroke(part, 3, A(Core, .9f * a)); } } }
   { float v = u - .6f; if (v > 0 && v < .5f) { float k = v / .5f; g.Ring(x.end, 28 + 24 * Ease3(k), 3, A(cy, 1 - k)); } }
  }

  /* ---------- 1. 패킷 버스트 (not offered since 11-27; kept for checks) ---------- */
  // rung rows the path passes (HTML: U.RowY on the straight CH2 line); a short path (a rung) has none (11_fxpick.js: P.L > 400)
  static List<float> RowNodes(List<Vector2> p) {
   var list = new List<float>(); float acc = 0; if (PathMath.Length(p) <= 400) return list;
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
   g.Poly(x.path, 6, A(cy, .35f * fade * Clamp(u / .1f)));
   g.Ring(x.start, 30 + 40 * Ease3(u / .4f), 3, A(cy, Clamp(1 - u / .4f)));
   const int N = 5;
   for (int k = 0; k < N; k++) {
    float v = (u - k * .1f) / .55f; if (v < 0) continue;
    if (v < 1) {
     float d = v * v * x.total; Vector2 head = PathMath.At(x.path, d);
     DrawTrail(g, x.path, d, cy);
     g.SoftRect(head.x - 7, head.y - 7, 14, 14, 22, A(CyanHtml, .2f));
     g.FillRect(head.x - 7, head.y - 7, 14, 14, new Color(233 / 255f, 251 / 255f, 1));
    }
    foreach (float nd in x.nodes) { float pass = nd / x.total, at = Mathf.Sqrt(pass); float w0 = v - at; if (w0 > 0 && w0 < .5f) { float a = 1 - w0 / .5f, s = 18 + 26 * w0; var p = PathMath.At(x.path, nd); g.StrokeRect(p.x - s / 2, p.y - s / 2, s, s, 2, A(cy, a)); } }
    float w = (u - k * .1f - .55f) / .45f;
    if (w > 0 && w < 1) {
     for (int i = 0; i < 10; i++) { float ang = Mathf.PI * 2 * (i / 10f + k * .037f), r = 30 + 80 * Ease3(w); g.FillRect(x.end.x + Mathf.Cos(ang) * r - 3, x.end.y + Mathf.Sin(ang) * r - 3, 6, 6, A(Core, 1 - w)); }
     HexPath(g, x.end, 40 + 26 * w, A(cy, 1 - w), 3);
    }
   }
  }
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
   x.words = new TextMeshProUGUI[3]; for (int i = 0; i < 3; i++) { x.words[i] = MakeText(x.textRoot, x.font, 24); x.words[i].gameObject.SetActive(false); }
   if (!x.board || !x.sc.Board) return;
   x.glitchClip = Ui.Rect("글리치 자르기", root, B.x, B.y, B.width, B.height); x.glitchClip.gameObject.AddComponent<RectMask2D>();
   x.glitchClip.SetSiblingIndex(0);
   RawImage Copy(string name, Color c) { var r = Ui.Rect(name, x.glitchClip, 0, 0, B.width, B.height); var im = r.gameObject.AddComponent<RawImage>(); im.texture = x.board; im.color = c; im.raycastTarget = false; im.uvRect = new Rect(B.x / 1920f, 1 - B.yMax / 1080f, B.width / 1920f, B.height / 1080f); im.enabled = false; return im; }
   x.red = Copy("글리치 빨강 겹", new Color(1, 40 / 255f, 70 / 255f, 0)); x.cyan = Copy("글리치 청록 겹", new Color(40 / 255f, 230 / 255f, 1, 0));
   x.redAdd = Copy("글리치 빨강 겹 - 화면 혼합 보정", new Color(1, 40 / 255f, 70 / 255f, 0)); if (Add) x.redAdd.material = Add;
   x.strips = new RawImage[7]; for (int i = 0; i < 7; i++) x.strips[i] = Copy("글리치 찢김 띠 " + i, Color.white);
  }
  static void DrawGlitch(Ctx x, float u, float t) {
   if (x.words != null) foreach (var tx in x.words) tx.gameObject.SetActive(false);
   if (x.red) { x.red.enabled = x.cyan.enabled = x.redAdd.enabled = false; foreach (var s in x.strips) s.enabled = false; }
   if (u < 0) return; var g = x.under;
   int tick = Mathf.FloorToInt(t * 30);
   void Burst(float u0, float len, float amp, float y0, float y1) {
    float v = (u - u0) / len; if (v < 0 || v > 1 || !x.red) return; float k = 1 - Mathf.Abs(v * 2 - 1);
    y0 = Mathf.Max(y0, B.y); y1 = Mathf.Min(y1, B.yMax); if (y1 <= y0) return;
    x.glitchClip.anchoredPosition = new Vector2(B.x, -y0); x.glitchClip.sizeDelta = new Vector2(B.width, y1 - y0);
    void Put(RawImage im, float px, float py, float h, float srcY) { var r = im.rectTransform; r.anchoredPosition = new Vector2(px - B.x, -(py - y0)); r.sizeDelta = new Vector2(B.width, h); im.uvRect = new Rect(B.x / 1920f, 1 - (srcY + h) / 1080f, B.width / 1920f, h / 1080f); }
    Put(x.red, B.x - amp * k, B.y, B.height, B.y); x.red.color = new Color(1, 40 / 255f, 70 / 255f, .75f * k); x.red.enabled = true;
    Put(x.cyan, B.x + amp * k, B.y, B.height, B.y); x.cyan.color = new Color(40 / 255f, 230 / 255f, 1, .75f * k); x.cyan.enabled = true;
    if (Add) { Put(x.redAdd, B.x - amp * k, B.y, B.height, B.y); x.redAdd.color = new Color(1, 40 / 255f, 70 / 255f, .75f * k * .75f * k); x.redAdd.enabled = true; }
    for (int i = 0; i < 7; i++) {
     float sy = y0 + IntHash(tick * 31 + i * 7) * (y1 - y0), sh = 6 + IntHash(tick * 17 + i) * 44, dx = (IntHash(tick * 13 + i * 3) - .5f) * amp * 2.6f * k;
     if (k > .15f) { Put(x.strips[i], B.x + dx, sy, sh, sy); x.strips[i].enabled = true; }
    }
   }
   if (!x.mini) { Burst(0, .42f, 26, B.y, B.yMax); Burst(.62f, .25f, 18, x.end.y - 70, x.end.y + 70); } // 발동: 판 전체가 찢김 / 도착: 도착점 줄만
   // 경로: 계단처럼 끊기며 나아가는 RGB 3겹 선 (법선 방향으로 -4 / +4 / 0, 흔들림 j)
   float v2 = u - .28f;
   if (v2 > 0) {
    float a = Blink(v2), reach = Mathf.Min(1, Mathf.Floor(v2 / .04f) * .12f) * x.total, j = v2 < .4f ? (IntHash(tick) - .5f) * 10 : 0;
    foreach (var (dx, col, w) in new[] { (-4f, A(RedSplit, .8f * a), 3f), (4f, A(CyanHtml, .8f * a), 3f), (0f, A(Core, a), 4f) }) { var line = Trace(x.path, 0, reach, dx + j); if (line.Count >= 2) g.Poly(line, w, col); }
   }
   if (!x.mini && x.words != null && u > .62f && u < 2.3f) {
    float a = Clamp((u - .62f) / .1f) * (u > 2 ? 1 - (u - 2) / .3f : 1), j = u < .9f ? (IntHash(tick + 5) - .5f) * 8 : 0; var w = x.sc.WordAt;
    var cols = new[] { A(RedSplit, .7f * a), A(CyanHtml, .7f * a), A(Core, a) }; var xs = new[] { w.x - 3 + j, w.x + 3 - j, w.x };
    for (int i = 0; i < 3; i++) Place(x.words[i], x.sc.Word ?? "", new Vector2(xs[i], w.y), 300, cols[i]);
   }
  }

  /* ---------- 3. 헥스 격자 파문 ---------- */
  const float R = 24;
  static List<(float x, float y, int code)> cells;
  static List<(float x, float y, int code)> Cells() {
   if (cells != null) return cells; cells = new List<(float, float, int)>();
   for (int row = 0; ; row++) { float y = B.y + R + row * 1.5f * R; if (y >= B.yMax) break;
    for (float cx = B.x + (row % 2 == 1 ? Mathf.Sqrt(3) * R / 2 : 0); cx < B.xMax + R; cx += Mathf.Sqrt(3) * R) cells.Add((cx, y, Mathf.FloorToInt(IntHash(cells.Count + 3) * 256))); }
   return cells;
  }
  static void DrawHexes(Ctx x, float u) {
   if (u < 0) return; var g = x.clipped; var cy = x.cy;
   float r1 = 1100 * u, r2 = 950 * (u - .55f), fade = u < 1.9f ? 1 : 1 - Clamp((u - 1.9f) / .6f);
   float reach = Ease3((u - .1f) / .5f) * x.total;
   foreach (var (cx, cyy, code) in Cells()) {
    var c = new Vector2(cx, cyy); float d1 = Vector2.Distance(c, x.start), d2 = Vector2.Distance(c, x.end);
    float I = Mathf.Exp(-Mathf.Pow((d1 - r1) / 46, 2)) * Clamp(1 - u / 1.3f);
    if (u > .55f) I = Mathf.Max(I, Mathf.Exp(-Mathf.Pow((d2 - r2) / 40, 2)) * Clamp(1 - (u - .55f) / 1.1f));
    var (dn, at) = Near(x.path, c); if (dn < 30 && at < reach + 10) I = Mathf.Max(I, .55f * Blink(Mathf.Max(0, u - .1f)));
    I *= fade; if (I < .03f) continue;
    var hex = FxPainter.HexPoints(c, R - 2, Mathf.PI / 6); g.FillPoly(hex, A(cy, .12f * I)); g.StrokeClosed(hex, 1.5f, A(cy, .7f * I));
    if (!x.mini && code % 4 == 0) Glyph(x, HEX[code >> 4].ToString() + HEX[code & 15], 13, new Vector2(cx, cyy + 5), A(Core, .9f * I));
   }
   if (u > .55f && u < 1.1f) x.under.Ring(x.end, 30 + 40 * Ease3((u - .55f) / .55f), 4, A(Core, 1 - (u - .55f) / .55f));
  }

  /* ---------- 4. 데이터 스트림 ---------- */
  // 11_fxpick.js stream: speed and the arrival time scale with the path length (sp = L / 576; the straight lane is 576 px)
  static void DrawStream(Ctx x, float u, float t) {
   if (u < 0) return; var cy = x.cy;
   float fade = u < 2.1f ? 1 : 1 - Clamp((u - 2.1f) / .5f), sp = x.total / 576; int tick = Mathf.FloorToInt(t * 20);
   // 판 전체의 희미한 비: 40px 간격 열이 천천히 내려옴 (경로 50px 안은 비움). textBaseline 'middle' -> baseline = y + .35 em
   if (x.sc.Board) for (int c = 0; c < 20; c++) {
    float cx = B.x + 20 + c * 40, off = (IntHash(c + 1) * 700 + u * 160) % 760;
    for (int k = 0; k < 9; k++) { float y = B.y + off - k * 24; if (y < B.y || y > B.yMax || Near(x.path, new Vector2(cx, y)).d < 50) continue; Glyph(x, HEX[Mathf.FloorToInt(IntHash(c * 97 + k + tick) * 16)].ToString(), 20, new Vector2(cx, y + 7), A(cy, .10f * (1 - k / 9f) * fade)); }
   }
   // 다섯 줄: 경로를 따라 흐르는 헥스 문자, 머리는 흰색
   float[] dxs = { -36, -18, 0, 18, 36 };
   for (int c = 0; c < 5; c++) {
    float delay = IntHash(c + 40) * .22f, speed = (1000 + IntHash(c + 9) * 500) * sp; if (u < delay) continue; float head = (u - delay) * speed;
    for (int k = 0; k < 16; k++) {
     float d = head - k * 24; if (d < 0 || d > x.total + 10) continue; float a = (k == 0 ? 1 : .85f * (1 - k / 16f)) * fade;
     Vector2 p = PathMath.At(x.path, Mathf.Min(d, x.total)), n = Nor(x.path, d);
     Glyph(x, HEX[Mathf.FloorToInt(IntHash(c * 131 + k * 7 + tick) * 16)].ToString(), 20, p + n * dxs[c] + new Vector2(0, 7), k == 0 ? A(Core, a) : A(cy, a));
    }
   }
   float v = u - .55f * sp; if (v > 0 && v < .6f) { float k = v / .6f; x.under.FillPoly(FxPainter.HexPoints(x.end, 36 + 10 * k, Mathf.PI / 6), A(Core, .5f * (1 - k))); x.under.Ring(x.end, 40 + 40 * Ease3(k), 3, A(cy, 1 - k)); }
  }

  /* ---------- 5. 락온 괄호 + 터미널 ---------- */
  // 11_fxpick.js lockon: the board's four corner brackets close to the box around the path in .35 s (ease3), hold to .45, then lock onto the
  // arrival point (+-52) by .7 (white 6 px flash .7-.85, "LOCK" 64 px above, four ticks 60-90 px out); from .35 a gold dash [16, 10] flows
  // along the path at 120 px/s; a 320 x 150 window types the scene's command at 45 characters/s (cursor blinks at 2 Hz).
  static void LockInit(Ctx x) {
   x.lockText = MakeText(x.textRoot, x.font, 22); x.lockText.gameObject.SetActive(false);
   if (x.sc.Term == null) return;
   x.term = new TextMeshProUGUI[x.sc.Term.Length];
   for (int i = 0; i < x.term.Length; i++) { var t = Ui.Text("터미널 " + i, x.textRoot, 0, 0, 300, 40, "", x.font, 20, Color.clear); t.alignment = TextAlignmentOptions.BaselineLeft; t.textWrappingMode = TextWrappingModes.NoWrap; x.term[i] = t; }
  }
  static void Brackets(FxPainter g, float x0, float y0, float x1, float y1, Color c, float w) {
   float L = Mathf.Min(42, (x1 - x0) / 3, (y1 - y0) / 3);
   foreach (var (cx, cy, sx, sy) in new[] { (x0, y0, 1f, 1f), (x1, y0, -1f, 1f), (x0, y1, 1f, -1f), (x1, y1, -1f, -1f) })
    g.Stroke(new[] { new Vector2(cx + sx * L, cy), new Vector2(cx, cy), new Vector2(cx, cy + sy * L) }, w, c);
  }
  static void DrawLockon(Ctx x, float u) {
   if (x.lockText) x.lockText.gameObject.SetActive(false); if (x.term != null) foreach (var t in x.term) t.gameObject.SetActive(false);
   if (u < 0) return; var g = x.under; var cy = x.cy; var E = x.end;
   float mx0 = float.MaxValue, my0 = float.MaxValue, mx1 = float.MinValue, my1 = float.MinValue; foreach (var q in x.path) { mx0 = Mathf.Min(mx0, q.x); my0 = Mathf.Min(my0, q.y); mx1 = Mathf.Max(mx1, q.x); my1 = Mathf.Max(my1, q.y); }
   Vector4 R0 = new Vector4(B.x, B.y, B.xMax, B.yMax), R1 = new Vector4(mx0 - 60, my0 - 40, mx1 + 60, my1 + 40), R2 = new Vector4(E.x - 52, E.y - 52, E.x + 52, E.y + 52);
   float fade = u < 2.2f ? 1 : 1 - Clamp((u - 2.2f) / .4f);
   var r = u < .35f ? Vector4.Lerp(R0, R1, Ease3(u / .35f)) : u < .45f ? R1 : Vector4.Lerp(R1, R2, Ease3((u - .45f) / .25f)); bool locked = u > .7f, flash = locked && u < .85f;
   Brackets(g, r.x, r.y, r.z, r.w, flash ? Core : A(cy, fade), flash ? 6 : 4);
   if (locked) {
    if (!x.mini && x.lockText) Place(x.lockText, "LOCK", new Vector2(E.x, E.y - 64), 120, A(flash ? Core : cy, fade));
    foreach (var d in new[] { Vector2.left, Vector2.right, Vector2.down, Vector2.up }) g.Line(E + d * 60, E + d * 90, 1.5f, A(cy, .6f * fade));
   }
   if (u > .35f) { // setLineDash([16, 10]); lineDashOffset = -u * 120: a dash starts at u * 120 + 26 k
    var gold = A(LockGold, .9f * fade);
    for (float s0 = (u * 120) % 26 - 26; s0 < x.total; s0 += 26) { float a0 = Mathf.Max(0, s0), a1 = Mathf.Min(x.total, s0 + 16); if (a1 > a0) { var dash = PathMath.Slice(x.path, a0, a1); if (dash.Count >= 2) g.Stroke(dash, 4, gold); } }
   }
   if (x.mini || x.term == null || !x.top) return;
   float al = Clamp(u / .12f) * fade; Vector2 T = x.sc.TermAt;
   x.top.FillRect(T.x, T.y, 320, 150, new Color(4 / 255f, 10 / 255f, 16 / 255f, .88f * al)); x.top.StrokeRect(T.x, T.y, 320, 150, 1.5f, A(CyanHtml, .6f * al));
   int budget = Mathf.FloorToInt(u * 45);
   for (int i = 0; i < x.term.Length; i++) {
    string ln = x.sc.Term[i]; string s2 = ln.Substring(0, Mathf.Clamp(budget, 0, ln.Length)); budget -= ln.Length;
    var t = x.term[i]; if (t.text != s2) t.text = s2; t.color = i == 3 ? A(TermOk, al) : A(Core, al); t.rectTransform.anchoredPosition = new Vector2(T.x + 14, -(T.y + 34 + i * 32 - 20)); t.gameObject.SetActive(true);
    if (budget < 0 && budget > -ln.Length - 1 && Mathf.FloorToInt(u * 4) % 2 == 0) { float w = s2.Length > 0 ? t.GetPreferredValues(s2).x : 0; x.top.FillRect(T.x + 14 + w + 2, T.y + 18 + i * 32, 10, 20, i == 3 ? A(TermOk, al) : A(Core, al)); }
   }
  }

  /* ---------- rung removal pieces (11_fxpick.js rungDraw 'cut') ---------- */
  // The removed rung stays solid (5 px, lanes +-16) until u .45, then 8 pieces (28 x 5) scatter and fade over .6 s.
  public static IEnumerator RungCut(RectTransform root, Vector2 a, Vector2 b, Color col, Func<float> delta) {
   var g = FxPainter.Create("가로선 제거 조각", root);
   for (float u = 0; u < 1.05f; u += delta()) {
    if (!root) yield break; g.Begin();
    if (u < .45f) g.Line(new Vector2(a.x + 16, a.y), new Vector2(b.x - 16, b.y), 5, col);
    else {
     float k = Clamp((u - .45f) / .6f), step = (b.x - a.x - 32) / 8;
     for (int i = 0; i < 8; i++) {
      float x0 = a.x + 16 + i * step, dx = (IntHash(i * 7 + 1) - .5f) * 60 * k, dy = (IntHash(i * 13 + 2) - .2f) * 80 * k * k, rot = (IntHash(i * 3) - .5f) * 2 * k;
      Vector2 c = new Vector2(x0 + 14 + dx, a.y + dy), ex = new Vector2(Mathf.Cos(rot), Mathf.Sin(rot)), ey = new Vector2(-ex.y, ex.x);
      g.FillPoly(new[] { c - ex * 14 - ey * 2.5f, c + ex * 14 - ey * 2.5f, c + ex * 14 + ey * 2.5f, c - ex * 14 + ey * 2.5f }, A(col, 1 - k));
     }
    }
    g.End(); yield return null;
   }
   if (root) UnityEngine.Object.Destroy(root.gameObject);
  }
 }
}

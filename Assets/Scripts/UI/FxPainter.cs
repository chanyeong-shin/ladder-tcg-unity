using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 // Week 12 (2026-11-26): an immediate-mode 2D painter so the HTML canvas effects (조사_시안/2026-11-26_시안_12주차_회의반영.html,
 // _도구/시안12주차/tabs/*.js) can be ported call by call. Coordinates are 1920 x 1080 stage pixels, y down; the rect must sit at (0,0).
 // Canvas strokes are centered on the path like ctx.stroke(); lineWidth is the full width.
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class FxPainter : MaskableGraphic {
  readonly List<UIVertex> verts = new List<UIVertex>(); readonly List<int> tris = new List<int>();
  public static FxPainter Create(string name, Transform parent) { var r = Ui.Rect(name, parent, 0, 0, 1920, 1080); var p = r.gameObject.AddComponent<FxPainter>(); p.raycastTarget = false; return p; }
  public void Begin() { verts.Clear(); tris.Clear(); }
  public void End() { SetVerticesDirty(); }
  protected override void OnPopulateMesh(VertexHelper vh) { vh.Clear(); if (verts.Count == 0) return; vh.AddUIVertexStream(verts, tris); }
  // vh.AddUIVertexStream takes a vertex list and a triangle index list
  int V(Vector2 p, Color c) { var v = UIVertex.simpleVert; v.position = new Vector3(p.x, -p.y); v.color = c; verts.Add(v); return verts.Count - 1; }
  void Tri(int a, int b, int c) { tris.Add(a); tris.Add(b); tris.Add(c); }
  void QuadV(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd) { int i = V(a, ca), j = V(b, cb), k = V(c, cc), l = V(d, cd); Tri(i, j, k); Tri(i, k, l); }
  // Segment a->b of width w, color ca at a and cb at b (canvas linear gradient along the segment)
  public void Line(Vector2 a, Vector2 b, float w, Color ca, Color cb) {
   var d = b - a; if (d.sqrMagnitude < 1e-6f || w <= 0) return; var n = new Vector2(-d.y, d.x).normalized * w / 2;
   QuadV(a + n, b + n, b - n, a - n, ca, cb, cb, ca);
  }
  public void Line(Vector2 a, Vector2 b, float w, Color c) { Line(a, b, w, c, c); }
  // Polyline stroke (butt ends; joins overlap like separate subpaths)
  public void Poly(IList<Vector2> pts, float w, Color c) { for (int i = 1; i < pts.Count; i++) Line(pts[i - 1], pts[i], w, c); }
  public void FillRect(float x, float y, float w, float h, Color c) { if (w <= 0 || h <= 0) return; QuadV(new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h), c, c, c, c); }
  // ctx.strokeRect: the stroke straddles the rectangle edge
  public void StrokeRect(float x, float y, float w, float h, float lw, Color c) {
   float o = lw / 2; FillRect(x - o, y - o, w + lw, lw, c); FillRect(x - o, y + h - o, w + lw, lw, c); FillRect(x - o, y + o, lw, h - lw, c); FillRect(x + w - o, y + o, lw, h - lw, c);
  }
  // Filled rect with a soft edge that fades to 0 over 'soft' px on every side (stand-in for canvas shadowBlur)
  public void SoftRect(float x, float y, float w, float h, float soft, Color c) {
   var clear = new Color(c.r, c.g, c.b, 0); FillRect(x, y, w, h, c);
   Vector2 a = new Vector2(x, y), b = new Vector2(x + w, y), cc = new Vector2(x + w, y + h), d = new Vector2(x, y + h);
   Vector2 A = a + new Vector2(-soft, -soft), B = b + new Vector2(soft, -soft), C = cc + new Vector2(soft, soft), D = d + new Vector2(-soft, soft);
   QuadV(A, B, b, a, clear, clear, c, c); QuadV(b, B, C, cc, c, clear, clear, c); QuadV(d, cc, C, D, c, c, clear, clear); QuadV(A, a, d, D, clear, c, c, clear);
  }
  public static Vector2[] HexPoints(Vector2 c, float r, float start) { var p = new Vector2[6]; for (int i = 0; i < 6; i++) { float a = start + i * Mathf.PI / 3; p[i] = c + new Vector2(r * Mathf.Cos(a), r * Mathf.Sin(a)); } return p; }
  public void FillPoly(IList<Vector2> p, Color c) { int first = verts.Count; foreach (var q in p) V(q, c); for (int i = 1; i < p.Count - 1; i++) Tri(first, first + i, first + i + 1); }
  // Closed path stroke centered on the outline, mitred corners
  public void StrokeClosed(IList<Vector2> p, float w, Color c) {
   int n = p.Count; if (n < 3) return; var outer = new Vector2[n]; var inner = new Vector2[n];
   for (int i = 0; i < n; i++) {
    Vector2 prev = p[(i - 1 + n) % n], cur = p[i], next = p[(i + 1) % n];
    Vector2 n0 = new Vector2(-(cur - prev).y, (cur - prev).x).normalized, n1 = new Vector2(-(next - cur).y, (next - cur).x).normalized;
    var m = (n0 + n1).normalized; float k = Vector2.Dot(m, n0); if (Mathf.Abs(k) < .2f) k = .2f * Mathf.Sign(k == 0 ? 1 : k);
    outer[i] = cur + m * (w / 2 / k); inner[i] = cur - m * (w / 2 / k);
   }
   for (int i = 0; i < n; i++) { int j = (i + 1) % n; QuadV(outer[i], outer[j], inner[j], inner[i], c, c, c, c); }
  }
  // StrokeClosed clipped to the horizontal band y0..y1 (ctx.save(); ctx.rect(...); ctx.clip(); stroke; ctx.restore())
  public void StrokeClosedClipY(IList<Vector2> p, float w, Color c, float y0, float y1) {
   int n = p.Count; if (n < 3) return; var outer = new Vector2[n]; var inner = new Vector2[n];
   for (int i = 0; i < n; i++) {
    Vector2 prev = p[(i - 1 + n) % n], cur = p[i], next = p[(i + 1) % n];
    Vector2 n0 = new Vector2(-(cur - prev).y, (cur - prev).x).normalized, n1 = new Vector2(-(next - cur).y, (next - cur).x).normalized;
    var m = (n0 + n1).normalized; float k = Vector2.Dot(m, n0); if (Mathf.Abs(k) < .2f) k = .2f * Mathf.Sign(k == 0 ? 1 : k);
    outer[i] = cur + m * (w / 2 / k); inner[i] = cur - m * (w / 2 / k);
   }
   for (int i = 0; i < n; i++) { int j = (i + 1) % n; FillClipY(new List<Vector2> { outer[i], outer[j], inner[j], inner[i] }, c, y0, y1); }
  }
  // convex polygon clipped to y0 <= y <= y1 (Sutherland-Hodgman against two horizontal lines)
  public void FillClipY(List<Vector2> poly, Color c, float y0, float y1) {
   List<Vector2> Cut(List<Vector2> pts, float y, bool keepBelow) {
    var res = new List<Vector2>(); for (int i = 0; i < pts.Count; i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Count]; bool ina = keepBelow ? a.y >= y : a.y <= y, inb = keepBelow ? b.y >= y : b.y <= y;
     if (ina) res.Add(a); if (ina != inb) res.Add(Vector2.Lerp(a, b, (y - a.y) / (b.y - a.y))); }
    return res;
   }
   var q = Cut(Cut(poly, y0, true), y1, false); if (q.Count >= 3) FillPoly(q, c);
  }
  public void Ring(Vector2 c, float r, float w, Color col, int segments = 64) { if (r <= 0) return; var p = new Vector2[segments]; for (int i = 0; i < segments; i++) { float a = i * Mathf.PI * 2 / segments; p[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; } StrokeClosed(p, w, col); }
  public void Disc(Vector2 c, float r, Color col, int segments = 32) { var p = new Vector2[segments]; for (int i = 0; i < segments; i++) { float a = i * Mathf.PI * 2 / segments; p[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; } FillPoly(p, col); }
  // 13주차: 02_shot.js glow() — createRadialGradient 0: mix(col, 흰색, .55) a / .35: col a*.45 / 1: col 0
  public void Glow(Vector2 c, float r, Color col, float a, int segments = 40) {
   if (a <= 0 || r <= 0) return; a = Mathf.Clamp01(a);
   RadialFan(c, r, r, new[] { 0f, .35f, 1f }, new[] { WithA(Color.Lerp(col, Color.white, .55f), a), WithA(col, a * .45f), WithA(col, 0) }, segments);
  }
  // 02_shot.js flare(): 가로 3.4s x 세로 s 로 늘린 반지름 28 빛(0: mix(col, 흰색, .5) .7a / .4: col .32a / 1: 0) + 가로 160s x 1.5 선(mix(col, 흰색, .6) .55a)
  public void Flare(Vector2 c, Color col, float s, float a) {
   if (a <= 0 || s <= 0) return; a = Mathf.Clamp01(a);
   RadialFan(c, 28 * 3.4f * s, 28 * s, new[] { 0f, .4f, 1f }, new[] { WithA(Color.Lerp(col, Color.white, .5f), .7f * a), WithA(col, .32f * a), WithA(col, 0) }, 48);
   FillRect(c.x - 80 * s, c.y - .75f, 160 * s, 1.5f, WithA(Color.Lerp(col, Color.white, .6f), .55f * a));
  }
  void RadialFan(Vector2 c, float rx, float ry, float[] stops, Color[] cols, int segments) {
   int center = V(c, cols[0]); int prev = -1;
   for (int s = 1; s < stops.Length; s++) {
    int first = verts.Count;
    for (int i = 0; i < segments; i++) { float a = i * Mathf.PI * 2 / segments; V(c + new Vector2(Mathf.Cos(a) * rx * stops[s], Mathf.Sin(a) * ry * stops[s]), cols[s]); }
    for (int i = 0; i < segments; i++) { int j = (i + 1) % segments; if (prev < 0) Tri(center, first + i, first + j); else { Tri(prev + i, first + i, first + j); Tri(prev + i, first + j, prev + j); } }
    prev = first;
   }
  }
  static Color WithA(Color c, float a) { c.a = a; return c; }
  // ctx.stroke() of an open polyline: miter joins (canvas default lineJoin), butt or round caps. Overlap-free so a
  // translucent stroke does not darken at its joins.
  public void Stroke(IList<Vector2> p, float w, Color c, bool roundCaps = false) {
   int n = p.Count; if (n < 2 || w <= 0 || c.a <= 0) return; float h = w / 2;
   var pts = new List<Vector2>(n); foreach (var q in p) if (pts.Count == 0 || (q - pts[pts.Count - 1]).sqrMagnitude > 1e-4f) pts.Add(q); n = pts.Count; if (n < 2) return;
   var L = new Vector2[n]; var R = new Vector2[n];
   for (int i = 0; i < n; i++) {
    Vector2 d0 = i > 0 ? (pts[i] - pts[i - 1]).normalized : (pts[1] - pts[0]).normalized, d1 = i < n - 1 ? (pts[i + 1] - pts[i]).normalized : d0;
    Vector2 n0 = new Vector2(-d0.y, d0.x), n1 = new Vector2(-d1.y, d1.x), m = (n0 + n1); if (m.sqrMagnitude < 1e-6f) m = n0; m.Normalize();
    float k = Vector2.Dot(m, n0); if (k < .1f) k = .1f; // canvas miterLimit 10
    L[i] = pts[i] + m * (h / k); R[i] = pts[i] - m * (h / k);
   }
   for (int i = 1; i < n; i++) QuadV(L[i - 1], L[i], R[i], R[i - 1], c, c, c, c);
   if (roundCaps) { Cap(pts[0], (pts[0] - pts[1]).normalized, h, c); Cap(pts[n - 1], (pts[n - 1] - pts[n - 2]).normalized, h, c); }
  }
  void Cap(Vector2 at, Vector2 outward, float h, Color c) {
   const int seg = 10; var nrm = new Vector2(-outward.y, outward.x); int center = V(at, c), first = verts.Count;
   for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI / seg; V(at + (nrm * Mathf.Cos(a) + outward * Mathf.Sin(a)) * h, c); }
   for (int i = 0; i < seg; i++) Tri(center, first + i, first + i + 1);
  }
  // ctx.roundRect(x, y, w, h, r) fill
  public void FillRoundRect(float x, float y, float w, float h, float r, Color c) {
   if (w <= 0 || h <= 0 || c.a <= 0) return; r = Mathf.Min(r, w / 2, h / 2); var p = new List<Vector2>(); const int seg = 6;
   Vector2[] centers = { new Vector2(x + w - r, y + r), new Vector2(x + w - r, y + h - r), new Vector2(x + r, y + h - r), new Vector2(x + r, y + r) };
   for (int k = 0; k < 4; k++) for (int i = 0; i <= seg; i++) { float a = -Mathf.PI / 2 + (k + i / (float)seg) * Mathf.PI / 2; p.Add(centers[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
   FillPoly(p, c);
  }
  // rounded rect outline (stroke centered on the edge)
  public void StrokeRoundRect(float x, float y, float w, float h, float r, float lw, Color c) {
   if (w <= 0 || h <= 0 || c.a <= 0) return; r = Mathf.Min(r, w / 2, h / 2); var p = new List<Vector2>(); const int seg = 6;
   Vector2[] centers = { new Vector2(x + w - r, y + r), new Vector2(x + w - r, y + h - r), new Vector2(x + r, y + h - r), new Vector2(x + r, y + r) };
   for (int k = 0; k < 4; k++) for (int i = 0; i <= seg; i++) { float a = -Mathf.PI / 2 + (k + i / (float)seg) * Mathf.PI / 2; p.Add(centers[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
   StrokeClosed(p, lw, c);
  }
  // Rect outside a hole (ctx.rect(screen); ctx.rect(hole); fill('evenodd'))
  public void FillOutside(Rect hole, Color c) { FillRect(0, 0, 1920, hole.y, c); FillRect(0, hole.yMax, 1920, 1080 - hole.yMax, c); FillRect(0, hole.y, hole.x, hole.height, c); FillRect(hole.xMax, hole.y, 1920 - hole.xMax, hole.height, c); }
 }

 // Polyline helpers shared by the week 12 effects
 public static class PathMath {
  public static float Length(IList<Vector2> p) { float l = 0; for (int i = 1; i < p.Count; i++) l += Vector2.Distance(p[i - 1], p[i]); return l; }
  public static Vector2 At(IList<Vector2> p, float d) { if (p.Count == 1) return p[0]; for (int i = 1; i < p.Count; i++) { float l = Vector2.Distance(p[i - 1], p[i]); if (d <= l || i == p.Count - 1) return Vector2.LerpUnclamped(p[i - 1], p[i], l == 0 ? 0 : Mathf.Min(d, l) / l); d -= l; } return p[p.Count - 1]; }
  // direction of the segment that contains distance d
  public static Vector2 Dir(IList<Vector2> p, float d) { for (int i = 1; i < p.Count; i++) { float l = Vector2.Distance(p[i - 1], p[i]); if (d <= l || i == p.Count - 1) return (p[i] - p[i - 1]).normalized; d -= l; } return Vector2.up; }
  // the part of the path between distances d0..d1 (clamped)
  public static List<Vector2> Slice(IList<Vector2> p, float d0, float d1) {
   var list = new List<Vector2>(); float total = Length(p); d0 = Mathf.Clamp(d0, 0, total); d1 = Mathf.Clamp(d1, 0, total); if (d1 <= d0) return list;
   list.Add(At(p, d0)); float acc = 0; for (int i = 1; i < p.Count; i++) { acc += Vector2.Distance(p[i - 1], p[i]); if (acc > d0 && acc < d1) list.Add(p[i]); }
   list.Add(At(p, d1)); return list;
  }
 }

 // The same numbers as the HTML's JavaScript helpers.
 public static class JsMath {
  // ToInt32 of a double (JS bitwise operators)
  static int ToInt32(double v) { if (double.IsNaN(v) || double.IsInfinity(v)) return 0; double t = System.Math.Truncate(v) % 4294967296.0; if (t < 0) t += 4294967296.0; return unchecked((int)(uint)t); }
  // const hash = n => { n = (n << 13) ^ n; return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647; };
  // (01_activate.js: plain JS number arithmetic, so the products are doubles before the & — emulated exactly here)
  public static float Hash(int n) { n = (n << 13) ^ n; double d = n; double r = d * (d * d * 15731 + 789221) + 1376312589; return (float)((ToInt32(r) & 0x7fffffff) / 2147483647.0); }
  // 11_queue.js / CardTransferFx.Hash: Math.imul version (32-bit integer arithmetic)
  public static float HashImul(int n) { unchecked { n = (n << 13) ^ n; return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f; } }
 }

 // Checks freeze every week 12 effect clock at an exact moment to capture it (Scale 0), then resume (Scale 1).
 public static class FxClock { public static float Scale = 1; }
}

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 // Circuit pattern from _도구/회로무늬_생성.py (Resources/Circuit_*.txt), drawn as one mesh.
 // Coordinates are 1920 x 1080 screen pixels, y down. The parent rect must sit at (0,0).
 public sealed class CircuitData {
  public sealed class Trace { public Vector2[] Points; public float Width, Length; public bool ToBoard; public string Group; }
  public readonly List<Trace> Traces = new List<Trace>(); public readonly List<Rect> Pads = new List<Rect>(), Chips = new List<Rect>(); public readonly List<Vector3> Vias = new List<Vector3>();
  // One comet per board-bound bundle: the middle trace of each bundle.
  public readonly List<Trace> Comets = new List<Trace>();
  static readonly Dictionary<string, CircuitData> cache = new Dictionary<string, CircuitData>();
  static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }
  public static CircuitData Load(string name) {
   if (cache.TryGetValue(name, out var hit)) return hit;
   var data = new CircuitData(); var asset = Resources.Load<TextAsset>(name); cache[name] = data; if (!asset) { Debug.LogWarning("Circuit data missing: " + name); return data; }
   foreach (var raw in asset.text.Split('\n')) {
    var line = raw.Trim(); if (line.Length == 0 || line[0] == '#') continue; var p = line.Split(' ');
    if (p[0] == "T") { var t = new Trace { ToBoard = p[1] == "1", Width = F(p[2]), Group = p[3] }; var pts = new List<Vector2>(); for (int i = 4; i < p.Length; i++) { var xy = p[i].Split(','); pts.Add(new Vector2(F(xy[0]), F(xy[1]))); } t.Points = pts.ToArray(); for (int i = 1; i < pts.Count; i++) t.Length += Vector2.Distance(pts[i - 1], pts[i]); data.Traces.Add(t); }
    else if (p[0] == "P") data.Pads.Add(new Rect(F(p[1]), F(p[2]), F(p[3]), F(p[4])));
    else if (p[0] == "C") data.Chips.Add(new Rect(F(p[1]), F(p[2]), F(p[3]), F(p[4])));
    else if (p[0] == "V") data.Vias.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
   }
   foreach (var g in data.Traces.Where(t => t.ToBoard).GroupBy(t => t.Group)) { var list = g.ToList(); data.Comets.Add(list[list.Count / 2]); }
   return data;
  }
  public static Vector2 Along(Trace t, float distance) {
   distance = Mathf.Clamp(distance, 0, t.Length);
   for (int i = 1; i < t.Points.Length; i++) { float l = Vector2.Distance(t.Points[i - 1], t.Points[i]); if (distance <= l) return Vector2.Lerp(t.Points[i - 1], t.Points[i], l == 0 ? 0 : distance / l); distance -= l; }
   return t.Points[t.Points.Length - 1];
  }
 }

 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class CircuitGraphic : MaskableGraphic {
  public CircuitData Data;
  // Static pattern: Reveal draws each trace from its first point (screen edge) toward its end (UI part / board).
  public float Reveal = 1, Glow = .4f;
  // Signal mode: heads run along board-bound traces only; time in seconds, -1 = off.
  public Color Ink = new Color(.22f, .851f, 1);
  Color Mix(float white, float a) { var c = Color.Lerp(Ink, Color.white, white); c.a = a; return c; }
  // Week 12 item 3 (03_boardcircuit.js "flow"): when BandY is set, only the part inside a BandHeight tall band is drawn and its
  // alpha falls linearly from 1 at BandY to 0 at both band edges (the HTML masks the band with a 0-1-0 vertical gradient).
  public float BandY = float.NaN, BandHeight = 180;
  float Weight(float y) { return Mathf.Clamp01(1 - Mathf.Abs(y - BandY) / (BandHeight / 2)); }
  Color W(Color c, float y) { return new Color(c.r, c.g, c.b, c.a * Weight(y)); }
  // a segment cut at the band's top, middle and bottom, each piece with per-end alpha (exact, the weight is linear in y on each piece)
  void BandQuad(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c) {
   float h = BandHeight / 2; var cuts = new System.Collections.Generic.List<float> { 0, 1 };
   foreach (float y in new[] { BandY - h, BandY, BandY + h }) if (Mathf.Abs(b.y - a.y) > .0001f) { float k = (y - a.y) / (b.y - a.y); if (k > 0 && k < 1) cuts.Add(k); }
   cuts.Sort();
   for (int i = 1; i < cuts.Count; i++) {
    Vector2 p = Vector2.Lerp(a, b, cuts[i - 1]), q = Vector2.Lerp(a, b, cuts[i]); float mid = (p.y + q.y) / 2; if (Weight(mid) <= 0 && Weight(p.y) <= 0 && Weight(q.y) <= 0) continue;
    var d = q - p; if (d.sqrMagnitude < .0001f) continue; var n = new Vector2(-d.y, d.x).normalized * width / 2; int s = vh.currentVertCount; Color cp = W(c, p.y), cq = W(c, q.y);
    vh.AddVert(new Vector3(p.x + n.x, -(p.y + n.y)), cp, Vector2.zero); vh.AddVert(new Vector3(q.x + n.x, -(q.y + n.y)), cq, Vector2.zero);
    vh.AddVert(new Vector3(q.x - n.x, -(q.y - n.y)), cq, Vector2.zero); vh.AddVert(new Vector3(p.x - n.x, -(p.y - n.y)), cp, Vector2.zero);
    vh.AddTriangle(s, s + 1, s + 2); vh.AddTriangle(s, s + 2, s + 3);
   }
  }
  void BandPartial(VertexHelper vh, CircuitData.Trace t, float to, float width, Color c) {
   float acc = 0; for (int i = 1; i < t.Points.Length; i++) { var a = t.Points[i - 1]; var b = t.Points[i]; float l = Vector2.Distance(a, b); float s1 = Mathf.Min(to, acc + l); if (s1 > acc && l > 0) BandQuad(vh, a, Vector2.Lerp(a, b, (s1 - acc) / l), width, c); acc += l; if (acc >= to) break; }
  }
  void BandMesh(VertexHelper vh, Color ink, Color halo) {
   for (int i = 0; i < Data.Traces.Count; i++) { var t = Data.Traces[i]; float to = t.Length * Ease3(Reveal * 1.25f - TitleBBackdrop.Rnd(i) * .25f); if (Glow > 0) BandPartial(vh, t, to, t.Width + 3, halo); BandPartial(vh, t, to, t.Width, ink); }
   float tip = Reveal >= 1 ? 1 : Mathf.Clamp01(Reveal * 1.4f - .3f); if (tip <= 0) return; var padInk = new Color(ink.r, ink.g, ink.b, ink.a * tip);
   foreach (var p in Data.Pads) { float w = Weight(p.center.y); if (w > 0) Box(vh, p, new Color(padInk.r, padInk.g, padInk.b, padInk.a * w)); }
   foreach (var v in Data.Vias) for (int i = 0; i < 12; i++) { float a0 = i * Mathf.PI / 6, a1 = (i + 1) * Mathf.PI / 6; var c = new Vector2(v.x, v.y); BandQuad(vh, c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * v.z, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * v.z, 1.3f, padInk); }
   foreach (var r in Data.Chips) { BandQuad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), 1.6f, padInk); BandQuad(vh, new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), 1.6f, padInk); BandQuad(vh, new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), 1.6f, padInk); BandQuad(vh, new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), 1.6f, padInk); }
  }
  public bool Signals; public float SignalTime = -1; public const float SignalSpan = .75f, SignalTail = 130, SignalLength = 1.25f;
  public static float Delay(int index) { return .45f * ((index * 37) % 11) / 10f; }
  static float Ease3(float v) { v = Mathf.Clamp01(v); return 1 - (1 - v) * (1 - v) * (1 - v); }
  static float Ease(float v) { v = Mathf.Clamp01(v); return 1 - (1 - v) * (1 - v); }
  static void Quad(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c) {
   var d = b - a; if (d.sqrMagnitude < .0001f) return; var n = new Vector2(-d.y, d.x).normalized * width / 2; int s = vh.currentVertCount;
   vh.AddVert(new Vector3(a.x + n.x, -(a.y + n.y)), c, Vector2.zero); vh.AddVert(new Vector3(b.x + n.x, -(b.y + n.y)), c, Vector2.zero);
   vh.AddVert(new Vector3(b.x - n.x, -(b.y - n.y)), c, Vector2.zero); vh.AddVert(new Vector3(a.x - n.x, -(a.y - n.y)), c, Vector2.zero);
   vh.AddTriangle(s, s + 1, s + 2); vh.AddTriangle(s, s + 2, s + 3);
  }
  static void Box(VertexHelper vh, Rect r, Color c) { Quad(vh, new Vector2(r.xMin, r.center.y), new Vector2(r.xMax, r.center.y), r.height, c); }
  static void Outline(VertexHelper vh, Rect r, float w, Color c) { Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), w, c); Quad(vh, new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), w, c); Quad(vh, new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), w, c); Quad(vh, new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), w, c); }
  static void Ring(VertexHelper vh, Vector2 c, float r, float w, Color ink) { for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6, b = (i + 1) * Mathf.PI / 6; Quad(vh, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * r, w, ink); } }
  static void Partial(VertexHelper vh, CircuitData.Trace t, float from, float to, float width, Color c) {
   float acc = 0; for (int i = 1; i < t.Points.Length; i++) { var a = t.Points[i - 1]; var b = t.Points[i]; float l = Vector2.Distance(a, b); float s0 = Mathf.Max(from, acc), s1 = Mathf.Min(to, acc + l); if (s1 > s0 && l > 0) Quad(vh, Vector2.Lerp(a, b, (s0 - acc) / l), Vector2.Lerp(a, b, (s1 - acc) / l), width, c); acc += l; if (acc >= to) break; }
  }
  protected override void OnPopulateMesh(VertexHelper vh) {
   vh.Clear(); if (Data == null) return; Color ink = color, halo = new Color(color.r, color.g, color.b, color.a * Glow * .35f);
   if (Signals) {
    // Comets (2026-11-22 request: "은은하게 회로가 떠있고 회로를 따라 혜성처럼"): a bright head and a
    // 130 px tail that fades along the trace, one per bundle, staggered; the end pad glows softly on arrival.
    if (SignalTime < 0) return;
    for (int index = 0; index < Data.Comets.Count; index++) {
     var t = Data.Comets[index]; float u = (SignalTime - Delay(index)) / SignalSpan; if (u < 0) continue; float s = Mathf.Min(1, Ease(u)) * t.Length;
     if (u < 1) {
      for (int m = 0; m < 10; m++) { float f = 1 - m / 10f, a = color.a * f * f;
       Partial(vh, t, s - SignalTail * (m + 1) / 10f, s - SignalTail * m / 10f, 6, Mix(0, a * .12f));
       Partial(vh, t, s - SignalTail * (m + 1) / 10f, s - SignalTail * m / 10f, 1.2f + 1.2f * f, Mix(.45f, a * .85f)); }
      var h = CircuitData.Along(t, s); Box(vh, new Rect(h.x - 5, h.y - 5, 10, 10), Mix(.45f, color.a * .18f)); Box(vh, new Rect(h.x - 2, h.y - 2, 4, 4), Mix(.9f, color.a));
     } else { float glow = 1 - Mathf.Clamp01((u - 1) * SignalSpan / .35f); if (glow > 0) { var e = t.Points[t.Points.Length - 1]; Box(vh, new Rect(e.x - 4, e.y - 4, 8, 8), Mix(.7f, glow * color.a * .7f)); } }
    }
    return;
   }
   if (Reveal <= 0) return;
   if (!float.IsNaN(BandY)) { BandMesh(vh, ink, halo); return; }
   for (int i = 0; i < Data.Traces.Count; i++) { var t = Data.Traces[i]; float to = t.Length * Ease3(Reveal * 1.25f - TitleBBackdrop.Rnd(i) * .25f); if (Glow > 0) Partial(vh, t, 0, to, t.Width + 3, halo); Partial(vh, t, 0, to, t.Width, ink); }
   float tip = Reveal >= 1 ? 1 : Mathf.Clamp01(Reveal * 1.4f - .3f); if (tip <= 0) return; var padInk = new Color(ink.r, ink.g, ink.b, ink.a * tip);
   foreach (var p in Data.Pads) Box(vh, p, padInk);
   foreach (var v in Data.Vias) Ring(vh, new Vector2(v.x, v.y), v.z, 1.3f, padInk);
   foreach (var c in Data.Chips) Outline(vh, c, 1.6f, padInk);
  }
 }
}

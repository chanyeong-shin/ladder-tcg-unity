using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 // Circuit pattern from _도구/회로무늬_생성.py (Resources/Circuit_*.txt), drawn as one mesh.
 // Coordinates are 1920 x 1080 screen pixels, y down. The parent rect must sit at (0,0).
 public sealed class CircuitData {
  public sealed class Trace { public Vector2[] Points; public float Width, Length; public bool ToBoard; }
  public readonly List<Trace> Traces = new List<Trace>(); public readonly List<Rect> Pads = new List<Rect>(), Chips = new List<Rect>(); public readonly List<Vector3> Vias = new List<Vector3>();
  static readonly Dictionary<string, CircuitData> cache = new Dictionary<string, CircuitData>();
  static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }
  public static CircuitData Load(string name) {
   if (cache.TryGetValue(name, out var hit)) return hit;
   var data = new CircuitData(); var asset = Resources.Load<TextAsset>(name); cache[name] = data; if (!asset) { Debug.LogWarning("Circuit data missing: " + name); return data; }
   foreach (var raw in asset.text.Split('\n')) {
    var line = raw.Trim(); if (line.Length == 0 || line[0] == '#') continue; var p = line.Split(' ');
    if (p[0] == "T") { var t = new Trace { ToBoard = p[1] == "1", Width = F(p[2]) }; var pts = new List<Vector2>(); for (int i = 3; i < p.Length; i++) { var xy = p[i].Split(','); pts.Add(new Vector2(F(xy[0]), F(xy[1]))); } t.Points = pts.ToArray(); for (int i = 1; i < pts.Count; i++) t.Length += Vector2.Distance(pts[i - 1], pts[i]); data.Traces.Add(t); }
    else if (p[0] == "P") data.Pads.Add(new Rect(F(p[1]), F(p[2]), F(p[3]), F(p[4])));
    else if (p[0] == "C") data.Chips.Add(new Rect(F(p[1]), F(p[2]), F(p[3]), F(p[4])));
    else if (p[0] == "V") data.Vias.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
   }
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
  public bool Signals; public float SignalTime = -1; public const float SignalSpan = .6f, SignalTail = 70;
  public static float Delay(int index) { return .3f * ((index * 37) % 11) / 10f; }
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
    if (SignalTime < 0) return; int index = 0; var head = new Color(.94f, 1, 1, color.a); var tail = new Color(.62f, .95f, 1, color.a * .95f);
    foreach (var t in Data.Traces) {
     if (!t.ToBoard) continue; float u = (SignalTime - Delay(index++)) / SignalSpan; if (u < 0) continue; float s = Mathf.Min(1, u) * t.Length;
     if (u < 1) { Partial(vh, t, s - SignalTail, s, 6, new Color(tail.r, tail.g, tail.b, tail.a * .25f)); Partial(vh, t, s - SignalTail, s, 2.4f, tail); var h = CircuitData.Along(t, s); Box(vh, new Rect(h.x - 3.4f, h.y - 3.4f, 6.8f, 6.8f), head); }
     else { float flash = 1 - Mathf.Clamp01((u - 1) * SignalSpan / .3f); if (flash > 0) { var e = t.Points[t.Points.Length - 1]; Box(vh, new Rect(e.x - 4, e.y - 4, 8, 8), new Color(1, 1, 1, flash * color.a)); } }
    }
    return;
   }
   if (Reveal <= 0) return;
   foreach (var t in Data.Traces) { float to = t.Length * Reveal; if (Glow > 0) Partial(vh, t, 0, to, t.Width + 3, halo); Partial(vh, t, 0, to, t.Width, ink); }
   float tip = Mathf.InverseLerp(.7f, 1, Reveal); if (tip <= 0) return; var padInk = new Color(ink.r, ink.g, ink.b, ink.a * tip);
   foreach (var p in Data.Pads) Box(vh, p, padInk);
   foreach (var v in Data.Vias) Ring(vh, new Vector2(v.x, v.y), v.z, 1.3f, padInk);
   foreach (var c in Data.Chips) Outline(vh, c, 1.6f, padInk);
  }
 }
}

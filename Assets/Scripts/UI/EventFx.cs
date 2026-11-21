using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 // Event light — port of HTML tab 2 (조사_시안/2026-11-21_시안_11주차_연출모음.html, sec-glow):
 // range "경로 전체 + 도착 서버", blink "두 번 깜빡 후 잔광", speed 1.
 // The HTML board has 160 px between channels and 100 px between rows; the game board has 280 / 133,
 // so widths and contact rings use S = 1.33 (server rings keep their size: the hexes are the same size).
 public static class EventFx {
  public const float S = 1.33f;
  public static readonly Color Me = new Color(.22f, .851f, 1), Enemy = new Color(1, .294f, .4f), Core = new Color(.914f, .984f, 1);
  public static Color Side(int owner) { return owner == 0 ? Me : Enemy; }
  public sealed class Plan {
   public readonly List<(List<Vector2> pts, Color col, float delay)> Halos = new List<(List<Vector2>, Color, float)>();
   public readonly List<(Vector2 c, Color col, float r0, float r1, float delay)> Rings = new List<(Vector2, Color, float, float, float)>();
   public readonly List<(Vector2 a, Vector2 b, Color col)> Flashes = new List<(Vector2, Vector2, Color)>();
   public bool Empty { get { return Halos.Count == 0 && Rings.Count == 0 && Flashes.Count == 0; } }
  }
  // blinkA('double'): on 0.10, 15% until 0.18, on until 0.28, then 0.9 fading over 1.6 s.
  public static float Blink(float t) { return t < .10f ? 1 : t < .18f ? .15f : t < .28f ? 1 : Mathf.Max(0, .9f - (t - .28f) / 1.6f); }
  static float Ease(float v) { v = Mathf.Clamp01(v); return 1 - Mathf.Pow(1 - v, 3); }
  sealed class Band { public Graphic[] Layers; public float From, Length, Total; public float Delay; }
  public static IEnumerator Run(Transform parent, Plan plan, Func<float> delta, float duration = 3) {
   var root = Ui.Rect("이벤트 빛", parent, 0, 0, 1920, 1080); var bands = new List<Band>(); var rings = new List<(Shape s, Vector2 c, float r0, float r1, float delay)>(); var caps = new List<(SoftDisc disc, Band last)>(); var flashes = new List<Shape[]>();
   foreach (var h0 in plan.Halos) {
    var h = (pts: Extend(h0.pts, 40), h0.col, h0.delay);
    float acc = 0; int first = bands.Count;
    for (int i = 1; i < h.pts.Count; i++) {
     float len = Vector2.Distance(h.pts[i - 1], h.pts[i]); if (len < .5f) continue;
     // halo: stroke 30 under blur 9 at .55 (scaled), core: #e9fbff stroke 3 at .9
     // stroke 30 (x S) under feGaussianBlur 9 (x S): a band whose edges fade over 1.2 sigma on each side, round joins
     float sigma = 9 * S, w = 30 * S;
     var layers = new Graphic[] { SoftLine("경로 빛 띠", root, h.pts[i - 1], h.pts[i], w + 2.4f * sigma, Mathf.Max(0, (w - 2.4f * sigma) / (w + 2.4f * sigma)), new Color(h.col.r, h.col.g, h.col.b, 0)),
      SoftJoint("경로 빛 이음", root, h.pts[i - 1], w + 2.4f * sigma, new Color(h.col.r, h.col.g, h.col.b, 0)), Ui.Line("경로 심선", root, h.pts[i - 1], h.pts[i], Color.clear, 3) };
     bands.Add(new Band { Layers = layers, From = acc, Length = len, Delay = h.delay }); acc += len;
    }
    for (int i = first; i < bands.Count; i++) bands[i].Total = acc;
    if (bands.Count > first) caps.Add((SoftJoint("경로 빛 끝", root, h.pts[h.pts.Count - 1], 30 * S + 2.4f * 9 * S, new Color(h.col.r, h.col.g, h.col.b, 0)), bands[bands.Count - 1]));
   }
   foreach (var r in plan.Rings) { var ring = Ui.Box("이벤트 고리", root, 0, 0, 2 * r.r0, 2 * r.r0, new Color(r.col.r, r.col.g, r.col.b, 0), false, 3, 40); ring.rectTransform.pivot = new Vector2(.5f, .5f); ring.rectTransform.anchoredPosition = new Vector2(r.c.x, -r.c.y); rings.Add((ring, r.c, r.r0, r.r1, r.delay)); }
   foreach (var f in plan.Flashes) flashes.Add(new[] { Ui.Line("회선 깜빡 빛", root, f.a, f.b, new Color(f.col.r, f.col.g, f.col.b, 0), 7 * S + 14), Ui.Line("회선 깜빡", root, f.a, f.b, new Color(f.col.r, f.col.g, f.col.b, 0), 7 * S) });
   for (float t = 0; t < duration; t += delta()) {
    if (!root) yield break;
    foreach (var b in bands) {
     float u = t - b.Delay, spread = Ease(u / .45f), a = u < 0 ? 0 : Blink(u);
     float shown = Mathf.Clamp(spread * b.Total - b.From, 0, b.Length);
     foreach (var l in new[] { b.Layers[0], b.Layers[2] }) l.rectTransform.sizeDelta = new Vector2(shown, l.rectTransform.sizeDelta.y);
     SetA(b.Layers[0], .55f * a); SetA(b.Layers[1], shown > 0 ? .55f * a * .5f : 0); b.Layers[2].color = new Color(Core.r, Core.g, Core.b, .9f * a);
    }
    foreach (var cap in caps) { float u = t - cap.last.Delay, a = u < 0 ? 0 : Blink(u); bool reached = Ease(u / .45f) * cap.last.Total >= cap.last.Total - 1; SetA(cap.disc, reached ? .55f * a * .5f : 0); }
    foreach (var r in rings) { float u = t - r.delay, k = Mathf.Clamp01(u / .5f), rad = (r.r0 + (r.r1 - r.r0) * Ease(k)); r.s.rectTransform.sizeDelta = Vector2.one * 2 * rad; SetA(r.s, u < 0 ? 0 : 1 - k); }
    foreach (var f in flashes) { float a = Blink(t); SetA(f[0], .3f * a); SetA(f[1], a); }
    yield return null;
   }
   if (root) UnityEngine.Object.Destroy(root.gameObject);
  }
  static List<Vector2> Extend(List<Vector2> pts, float by) { var list = new List<Vector2>(pts); if (list.Count < 2) return list; var d = (list[list.Count - 1] - list[list.Count - 2]).normalized; list.Add(list[list.Count - 1] + d * by); return list; }
  static void SetA(Graphic s, float a) { var c = s.color; c.a = a; s.color = c; }
  static SoftBand SoftLine(string name, Transform parent, Vector2 a, Vector2 b, float width, float solid, Color color) {
   Vector2 d = b - a; var r = Ui.Rect(name, parent, a.x, a.y, d.magnitude, width); var g = r.gameObject.AddComponent<SoftBand>(); g.Solid = solid; g.color = color; g.raycastTarget = false;
   r.pivot = new Vector2(0, .5f); r.anchoredPosition = new Vector2(a.x, -a.y); r.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg); return g;
  }
  static SoftDisc SoftJoint(string name, Transform parent, Vector2 c, float width, Color color) {
   var r = Ui.Rect(name, parent, 0, 0, width, width); r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(c.x, -c.y); var g = r.gameObject.AddComponent<SoftDisc>(); g.Solid = .2f; g.color = color; g.raycastTarget = false; return g;
  }
 }

 // A line whose alpha is full across the middle Solid fraction of its width and fades to 0 at both edges (blurred stroke).
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class SoftBand : MaskableGraphic {
  public float Solid;
  protected override void OnPopulateMesh(VertexHelper vh) {
   vh.Clear(); Rect r = rectTransform.rect; if (r.width <= 0) return; var clear = new Color(color.r, color.g, color.b, 0); float h = r.height / 2, s = h * Solid, cy = r.center.y;
   float[] ys = { cy - h, cy - s, cy + s, cy + h }; Color[] cs = { clear, color, color, clear };
   for (int k = 0; k < 4; k++) { vh.AddVert(new Vector3(r.xMin, ys[k]), cs[k], Vector2.zero); vh.AddVert(new Vector3(r.xMax, ys[k]), cs[k], Vector2.zero); }
   for (int k = 0; k < 3; k++) { int i = k * 2; vh.AddTriangle(i, i + 1, i + 3); vh.AddTriangle(i, i + 3, i + 2); }
  }
 }
}

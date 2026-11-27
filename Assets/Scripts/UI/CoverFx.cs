using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 // Week 12 bundle C (cover screens) shared pieces.
 // CutFlare: the FlareGraphic bloom with the HTML "bloomFix" cut (09_settings.js / 10_resultlog.js): the bloom is drawn on its own canvas,
 // its own dark oval (SoftDisc Solid .55, alpha .92) is erased with destination-out, then it is laid over every oval.
 // Here the bloom mesh is sliced into thin horizontal bands and each vertex alpha is multiplied by (1 - oval alpha), which is the same
 // destination-out result up to linear interpolation inside a 2 px band. Size is set in stage pixels (no rect scale), so a hover scale
 // grows the bloom but not the cut, like the HTML (flare 209*s x 57.2*s, cut fixed).
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class CutFlare : MaskableGraphic {
  public float Length = 209, Thick = 57.2f; public bool Vertical = true;
  public float CutW, CutH; // 0 = no cut. Oval centered on the flare center (the HTML og canvas center, before bloomDy).
  public Vector2 CutOffset; // cut center relative to the flare center (stage px, y down)
  readonly List<UIVertex> tri = new List<UIVertex>();
  public void Set(float length, float thick, Color c) { if (Length != length || Thick != thick || color != c) { Length = length; Thick = thick; color = c; SetVerticesDirty(); } }
  void Add(Vector2 p, Color c) { var v = UIVertex.simpleVert; v.position = p; v.color = c; tri.Add(v); }
  void Disc(Vector2 c, float rx, float ry, Color tint) { var clear = new Color(tint.r, tint.g, tint.b, 0); for (int i = 0; i < 48; i++) { float a0 = i * Mathf.PI * 2 / 48, a1 = (i + 1) * Mathf.PI * 2 / 48; Add(c, tint); Add(c + new Vector2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry), clear); Add(c + new Vector2(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry), clear); } }
  Vector2 Axis(float along, float across) { return Vertical ? new Vector2(across, along) : new Vector2(along, across); }
  protected override void OnPopulateMesh(VertexHelper vh) {
   vh.Clear(); tri.Clear(); Vector2 c = rectTransform.rect.center; float W = Length, H = Thick; Color col = color;
   // FlareGraphic.OnPopulateMesh, in the flare's own axes (along = W, across = H)
   var d1 = Axis(W * .5f, H * .3f); Disc(c, Mathf.Abs(d1.x), Mathf.Abs(d1.y), new Color(col.r, col.g, col.b, col.a * .22f));
   var d2 = Axis(W * .34f, H * .45f); Disc(c, Mathf.Abs(d2.x), Mathf.Abs(d2.y), new Color(col.r, col.g, col.b, col.a * .25f));
   Vector2[] star = { Axis(-W * .5f, 0), Axis(-W * .045f, H * .06f), Axis(0, H * .5f), Axis(W * .045f, H * .06f), Axis(W * .5f, 0), Axis(W * .045f, -H * .06f), Axis(0, -H * .5f), Axis(-W * .045f, -H * .06f) };
   Color core = Color.Lerp(col, Color.white, .8f); core.a = col.a;
   for (int i = 0; i < 8; i++) { int j = (i + 1) % 8; Add(c, core); Add(c + star[i], new Color(col.r, col.g, col.b, i % 2 == 0 ? 0 : col.a)); Add(c + star[j], new Color(col.r, col.g, col.b, j % 2 == 0 ? 0 : col.a)); }
   var d3 = Vertical ? new Vector2(12, 9) : new Vector2(9, 12); Disc(c, d3.x, d3.y, new Color(1, 1, 1, col.a));
   if (CutW <= 0 || CutH <= 0) { for (int i = 0; i < tri.Count; i += 3) { int b = vh.currentVertCount; vh.AddVert(tri[i]); vh.AddVert(tri[i + 1]); vh.AddVert(tri[i + 2]); vh.AddTriangle(b, b + 1, b + 2); } return; }
   // band edges (local y, y up): the oval's solid edge, its rim, and 2 px steps through the fade
   Vector2 oc = c + new Vector2(CutOffset.x, -CutOffset.y); float ry = CutH / 2; var cuts = new List<float>();
   for (float y = -ry; y <= ry + .01f; y += 2) cuts.Add(oc.y + y); cuts.Add(oc.y + ry * .55f); cuts.Add(oc.y - ry * .55f); cuts.Sort();
   var poly = new List<UIVertex>(); var next = new List<UIVertex>();
   for (int i = 0; i < tri.Count; i += 3) {
    float lo = Mathf.Min(tri[i].position.y, Mathf.Min(tri[i + 1].position.y, tri[i + 2].position.y)), hi = Mathf.Max(tri[i].position.y, Mathf.Max(tri[i + 1].position.y, tri[i + 2].position.y));
    // bands: (-inf, cuts[0]), [cuts[k], cuts[k+1]], (cuts[last], inf)
    for (int k = -1; k < cuts.Count; k++) {
     float y0 = k < 0 ? -1e6f : cuts[k], y1 = k + 1 < cuts.Count ? cuts[k + 1] : 1e6f; if (hi < y0 || lo > y1) continue;
     poly.Clear(); poly.Add(tri[i]); poly.Add(tri[i + 1]); poly.Add(tri[i + 2]);
     Clip(poly, next, y0, true); Clip(next, poly, y1, false); if (poly.Count < 3) continue;
     int b = vh.currentVertCount;
     foreach (var v in poly) { var w = v; float a = OvalAlpha((Vector2)v.position - oc); var cc = w.color; cc.a = (byte)Mathf.RoundToInt(cc.a * (1 - a)); w.color = cc; vh.AddVert(w); }
     for (int q = 1; q < poly.Count - 1; q++) vh.AddTriangle(b, b + q, b + q + 1);
    }
   }
  }
  // SoftDisc Solid .55 color alpha .92: .92 inside 55% of the radius, linear to 0 at the rim (the canvas radial gradient)
  float OvalAlpha(Vector2 d) { float r = Mathf.Sqrt(Mathf.Pow(d.x / (CutW / 2), 2) + Mathf.Pow(d.y / (CutH / 2), 2)); return r <= .55f ? .92f : r >= 1 ? 0 : .92f * (1 - (r - .55f) / .45f); }
  static void Clip(List<UIVertex> src, List<UIVertex> dst, float y, bool keepAbove) {
   dst.Clear(); for (int i = 0; i < src.Count; i++) { var a = src[i]; var b = src[(i + 1) % src.Count]; bool ina = keepAbove ? a.position.y >= y : a.position.y <= y, inb = keepAbove ? b.position.y >= y : b.position.y <= y;
    if (ina) dst.Add(a); if (ina != inb) { float t = (y - a.position.y) / (b.position.y - a.position.y); var v = a; v.position = Vector3.Lerp(a.position, b.position, t); v.color = Color32.Lerp(a.color, b.color, t); dst.Add(v); } }
  }
 }

 public static class Css {
  public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
  public static float OutQ(float t) { t = Mathf.Clamp01(t); return 1 - Mathf.Pow(1 - t, 3); }
  public static float Toward(float v, float to, float d) { return v < to ? Mathf.Min(to, v + d) : Mathf.Max(to, v - d); }
  // CSS transition timing 'ease' = cubic-bezier(.25,.1,.25,1)
  public static float Ease(float x) {
   x = Mathf.Clamp01(x); float lo = 0, hi = 1, t = x;
   for (int i = 0; i < 30; i++) { t = (lo + hi) / 2; float bx = 3 * (1 - t) * (1 - t) * t * .25f + 3 * (1 - t) * t * t * .25f + t * t * t; if (bx < x) lo = t; else hi = t; }
   return 3 * (1 - t) * (1 - t) * t * .1f + 3 * (1 - t) * t * t * 1 + t * t * t;
  }
  public static Color Rgb(float r, float g, float b, float a = 1) { return new Color(r / 255, g / 255, b / 255, a); }
  public static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, c.a * a); }
  // HTML text placed in a box: TMP top = CSS line top + (line-height - 1.448 em) / 2 (Do Hyeon asset carries Noto's 1.16 / .288 metrics).
  // Flex-centered boxes: the line box (n lines x line-height) is centered in h.
  public static TextMeshProUGUI Text(Transform parent, string name, float x, float y, float w, float h, string text, TMP_FontAsset font, float size, Color col, TextAlignmentOptions align, float lineHeight = 0, bool centerV = true) {
   int lines = text.Split('\n').Length; float lh = lineHeight > 0 ? lineHeight : 1.448f * size; float top = centerV ? y + (h - lines * lh) / 2 : y; top += (lh - 1.448f * size) / 2;
   var t = Ui.Text(name, parent, x, top, w, Mathf.Max(h, lines * lh + 10), text, font, size, col); t.alignment = align; t.textWrappingMode = TextWrappingModes.NoWrap; t.richText = true;
   if (lineHeight > 0) t.lineSpacing = (lineHeight - 1.448f * size) / size * 100; return t;
  }
  public static void At(RectTransform r, float x, float y) { r.anchoredPosition = new Vector2(x, -y); }
 }
}

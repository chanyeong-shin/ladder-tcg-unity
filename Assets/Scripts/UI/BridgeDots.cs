using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Breach.UI {
 // Soft round glow. Solid = fraction of the radius drawn at full color before fading to 0 at the rim,
 // which stands in for an SVG circle under feGaussianBlur.
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class SoftDisc : MaskableGraphic {
  public float Solid;
  protected override void OnPopulateMesh(VertexHelper vh) {
   vh.Clear(); Rect r = rectTransform.rect; Vector2 c = r.center; const int n = 40; vh.AddVert(c, color, Vector2.zero);
   var clear = new Color(color.r, color.g, color.b, 0);
   for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2 / n; var d = new Vector2(Mathf.Cos(a) * r.width / 2, Mathf.Sin(a) * r.height / 2); vh.AddVert(c + d * Solid, color, Vector2.zero); vh.AddVert(c + d, clear, Vector2.zero); }
   for (int i = 0; i < n; i++) { int j = (i + 1) % n; vh.AddTriangle(0, 1 + i * 2, 1 + j * 2); vh.AddTriangle(1 + i * 2, 2 + i * 2, 2 + j * 2); vh.AddTriangle(1 + i * 2, 2 + j * 2, 1 + j * 2); }
  }
 }

 // Color bridge B — port of HTML tab 6 (조사_시안/2026-11-21_시안_11주차_연출모음.html, drawBoard + beat).
 // The HTML board is 440 px with 140 px between channels; the game board has 280 px, so every
 // length is doubled. Dots: own -36, all 0, opp +36 (x2), 24 px (x2) above the rung.
 public sealed class BridgeDots : MonoBehaviour {
  public static readonly int[] Order = { Battle.Mine, Battle.Both, Battle.Theirs };
  // --me #38d9ff, --en #ff4b66, --all #ffd166 (viewer = the player, who is always the placer here)
  public static readonly Color Me = new Color(.22f, .851f, 1), Enemy = new Color(1, .294f, .4f), All = new Color(1, .82f, .4f);
  public static Color Tint(int color) { return color == Battle.Mine ? Me : color == Battle.Both ? All : Enemy; }
  const float K = 2, Appear = .42f, Cycle = 1.6f;
  float start; Action<int> pick; Action cancel;
  readonly RectTransform[] bodies = new RectTransform[3]; readonly SoftDisc[] halos = new SoftDisc[3], rings = new SoftDisc[3], streaks = new SoftDisc[3];
  readonly DotHover[] hovers = new DotHover[3];
  readonly Shape[] cores = new Shape[3], streaks2 = new Shape[3], ghostC = new Shape[3], ghostR = new Shape[3];

  public static BridgeDots Show(Transform parent, Vector2 a, Vector2 b, Action<int> pick, Action cancel) {
   var root = Ui.Rect("색 다리 선택", parent, 0, 0, 1920, 1080); root.SetAsLastSibling();
   var dots = root.gameObject.AddComponent<BridgeDots>(); dots.pick = pick; dots.cancel = cancel; dots.start = Time.unscaledTime;
   var veil = root.gameObject.AddComponent<Image>(); veil.color = Color.clear;
   var outside = root.gameObject.AddComponent<Button>(); outside.transition = Selectable.Transition.None; outside.targetGraphic = veil; outside.onClick.AddListener(() => dots.Finish(-1));
   // Pending rung: #e9fbff, dash 8 6, opacity .8, round caps (lengths x2).
   var dashInk = new Color(.914f, .984f, 1, .8f); float len = Vector2.Distance(a, b);
   for (float s = 0; s < len; s += 28) Ui.Line("놓는 중인 가로선", root, Vector2.Lerp(a, b, s / len), Vector2.Lerp(a, b, Mathf.Min(len, s + 16) / len), dashInk, 4);
   Vector2 mid = (a + b) / 2; float[] dx = { -36, 0, 36 };
   for (int i = 0; i < 3; i++) {
    int color = Order[i]; Color tint = Tint(color); float cx = mid.x + dx[i] * K, cy = mid.y - 24 * K;
    var slot = Ui.Rect("점 " + (i + 1), root, cx - 15 * K, cy - 15 * K, 30 * K, 30 * K);
    var hit = slot.gameObject.AddComponent<Image>(); hit.color = Color.clear;
    dots.hovers[i] = slot.gameObject.AddComponent<DotHover>();
    var button = slot.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None; button.targetGraphic = hit; button.onClick.AddListener(() => dots.Finish(color));
    float c0 = 15 * K; var body = Ui.Rect("점 몸체", slot, 0, 0, 30 * K, 30 * K); dots.bodies[i] = body;
    dots.halos[i] = Blur("빛", body, c0, c0, 13 * K, 3.5f * K, tint, .18f);
    dots.rings[i] = Blur("고리 빛", body, c0, c0, 8 * K, 3.5f * K, tint, .5f);
    dots.cores[i] = Ui.Box("가운데 흰 원", body, c0 - 4.5f * K, c0 - 4.5f * K, 9 * K, 9 * K, new Color(1, 1, 1, .85f), true, 1, 40);
    Ui.Box("가운데 색", body, c0 - 4.5f * K, c0 - 4.5f * K, 9 * K, 9 * K, new Color(tint.r, tint.g, tint.b, .55f), true, 1, 40);
    dots.streaks[i] = Stretch(Blur("가로 빛 번짐", body, c0, c0, 0, 3.5f * K, tint, 0), 140 * K, 3 * K);
    dots.streaks2[i] = Ui.Box("가로 흰 줄", body, c0 - 38 * K, c0 - .75f * K, 76 * K, 1.5f * K, new Color(1, 1, 1, 0));
    dots.ghostC[i] = Ui.Box("청록 잔상", body, c0 - 4.5f * K, c0 - 4.5f * K, 9 * K, 9 * K, new Color(.22f, .851f, 1, 0), true, 1, 40);
    dots.ghostR[i] = Ui.Box("붉은 잔상", body, c0 - 4.5f * K, c0 - 4.5f * K, 9 * K, 9 * K, new Color(1, .294f, .4f, 0), true, 1, 40);
   }
   dots.Update(); return dots;
  }
  // SVG circle r under feGaussianBlur(sigma): solid up to r - 1.2 sigma, fading to 0 at r + 1.2 sigma.
  static SoftDisc Blur(string name, Transform parent, float cx, float cy, float r, float sigma, Color tint, float alpha) {
   float outer = r + 1.2f * sigma; var rect = Ui.Rect(name, parent, cx - outer, cy - outer, outer * 2, outer * 2);
   rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(cx, -cy);
   var d = rect.gameObject.AddComponent<SoftDisc>(); d.Solid = Mathf.Max(0, (r - 1.2f * sigma) / outer); d.color = new Color(tint.r, tint.g, tint.b, alpha); d.raycastTarget = false; return d;
  }
  static SoftDisc Stretch(SoftDisc d, float w, float h) { float pad = 1.2f * 3.5f * K * 2; d.rectTransform.sizeDelta = new Vector2(w + pad, h + pad); d.Solid = .45f; return d; }
  static void Alpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
  public void Choose(int color) { Finish(color); }
  public void Cancel() { Finish(-1); }
  void Finish(int color) { var p = pick; var c = cancel; pick = null; cancel = null; Destroy(gameObject); if (color >= 0) p?.Invoke(color); else c?.Invoke(); }
  void Update() {
   float t0 = Time.unscaledTime - start, tt = t0 % Cycle; int rep = Mathf.FloorToInt(t0 / Cycle);
   for (int i = 0; i < 3; i++) {
    if (!bodies[i]) continue;
    if (tt < Appear) {
     // HTML beat(): shake, the core blinks off, horizontal bloom, RGB ghosts; first appearance full, repeats 0.8.
     float a = (1 - tt / Appear) * (rep > 0 ? .8f : 1); int tick = Mathf.FloorToInt(t0 * 32);
     float R(int n) { double x = Math.Sin((tick + 1) * 12.9898 + n * 78.233 + 3 * 3.1) * 43758.5453; return (float)(x - Math.Floor(x)); }
     bodies[i].anchoredPosition = new Vector2((R(1) - .5f) * 14 * a * K, 0);
     Alpha(cores[i], R(2) > .25f * a ? 1 : .15f);
     Alpha(streaks[i], (.55f + .45f * R(3)) * a * .35f); streaks[i].rectTransform.localScale = new Vector3(.6f + .8f * R(4), 1, 1);
     Alpha(streaks2[i], (.4f + .6f * R(5)) * a);
     float sp = (4 + 8 * R(6)) * a * K, c0 = 15 * K - 4.5f * K;
     ghostC[i].rectTransform.anchoredPosition = new Vector2(c0 - sp, -c0); ghostR[i].rectTransform.anchoredPosition = new Vector2(c0 + sp, -c0);
     Alpha(ghostC[i], .7f * a); Alpha(ghostR[i], .7f * a);
     Alpha(halos[i], .25f * a); Alpha(rings[i], .5f * a); halos[i].rectTransform.localScale = rings[i].rectTransform.localScale = Vector3.one;
    } else {
     bodies[i].anchoredPosition = Vector2.zero;
     Alpha(streaks[i], 0); Alpha(streaks2[i], 0); Alpha(ghostC[i], 0); Alpha(ghostR[i], 0);
     // user 11-22: brighter under the mouse
     bool over = hovers[i] && hovers[i].Over;
     Alpha(halos[i], over ? .6f : .3f); halos[i].rectTransform.localScale = Vector3.one * (14f / 13f) * (over ? 1.25f : 1);
     Alpha(rings[i], over ? .95f : .65f); rings[i].rectTransform.localScale = Vector3.one * (8.5f / 8f) * (over ? 1.15f : 1);
     Alpha(cores[i], 1);
    }
   }
  }

  // HTML flashRung('flash'): a white line, glow blur 4, stroke 12 -> 5 and opacity 1 -> 0 in 0.7 s (ease-out).
  public static IEnumerator RungFlash(Transform parent, Vector2 a, Vector2 b, Func<float> delta) {
   var root = Ui.Rect("가로선 번쩍", parent, 0, 0, 1920, 1080); root.SetAsLastSibling();
   var glow = Ui.Line("번쩍 빛", root, a, b, Color.clear, 12); var core = Ui.Line("번쩍 심", root, a, b, Color.white, 12);
   for (float t = 0; t < 1; t += delta() / .7f) {
    if (!root) yield break; float e = 1 - (1 - t) * (1 - t), w = Mathf.Lerp(12, 5, e) * K / 2, o = 1 - e;
    core.rectTransform.sizeDelta = new Vector2(core.rectTransform.sizeDelta.x, w); glow.rectTransform.sizeDelta = new Vector2(glow.rectTransform.sizeDelta.x, w + 16);
    core.color = new Color(1, 1, 1, o); glow.color = new Color(1, 1, 1, .3f * o); yield return null;
   }
   if (root) Destroy(root.gameObject);
  }
 }
 public sealed class DotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
  public bool Over;
  public void OnPointerEnter(PointerEventData e) { Over = true; }
  public void OnPointerExit(PointerEventData e) { Over = false; }
 }
}

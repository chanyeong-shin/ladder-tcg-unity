using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 // Soft round glow: full color at the center, transparent at the rim. No hard outline.
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class SoftDisc : MaskableGraphic {
  protected override void OnPopulateMesh(VertexHelper vh) {
   vh.Clear(); Rect r = rectTransform.rect; Vector2 c = r.center; vh.AddVert(c, color, Vector2.zero);
   for (int i = 0; i < 40; i++) { float a = i * Mathf.PI * 2 / 40; vh.AddVert(c + new Vector2(Mathf.Cos(a) * r.width / 2, Mathf.Sin(a) * r.height / 2), new Color(color.r, color.g, color.b, 0), Vector2.zero); }
   for (int i = 0; i < 40; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % 40);
  }
 }

 // Color bridge B (2026-11-21): a rung dropped on an empty slot waits with three glowing dots above it.
 // Picking a dot decides who rides the rung; clicking anywhere else cancels the card.
 // Dot rhythm follows the HTML sample: a 0.42 s appear glitch (horizontal bloom, shake, RGB ghosts)
 // repeats every 1.6 s at 0.8 strength; between glitches the dots glow steadily.
 public sealed class BridgeDots : MonoBehaviour {
  public static readonly int[] Order = { Battle.Mine, Battle.Both, Battle.Theirs };
  public static readonly string[] Labels = { "나만", "양쪽", "상대만" };
  public static Color Tint(int color) { return color == Battle.Mine ? Ui.Cyan : color == Battle.Both ? Ui.Gold : Ui.Red; }
  const float Appear = .42f, Cycle = 1.6f, Gap = 56;
  float start; Action<int> pick; Action cancel;
  readonly RectTransform[] bodies = new RectTransform[3]; readonly SoftDisc[] halos = new SoftDisc[3], streaks = new SoftDisc[3], ghostC = new SoftDisc[3], ghostR = new SoftDisc[3];
  readonly Shape[] rings = new Shape[3], cores = new Shape[3];

  public static BridgeDots Show(Transform parent, Vector2 center, TMP_FontAsset font, Action<int> pick, Action cancel) {
   var root = Ui.Rect("색 다리 선택", parent, 0, 0, 1920, 1080); root.SetAsLastSibling();
   var dots = root.gameObject.AddComponent<BridgeDots>(); dots.pick = pick; dots.cancel = cancel; dots.start = Time.unscaledTime;
   var veil = root.gameObject.AddComponent<Image>(); veil.color = Color.clear;
   var outside = root.gameObject.AddComponent<Button>(); outside.transition = Selectable.Transition.None; outside.targetGraphic = veil; outside.onClick.AddListener(() => dots.Finish(-1));
   for (int i = 0; i < 3; i++) {
    int color = Order[i]; Color tint = Tint(color); float x = center.x + (i - 1) * Gap, y = center.y - 40;
    var slot = Ui.Rect("점 " + (i + 1), root, x - 20, y - 20, 40, 40);
    var hit = slot.gameObject.AddComponent<Image>(); hit.color = Color.clear;
    var button = slot.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None; button.targetGraphic = hit; button.onClick.AddListener(() => dots.Finish(color));
    var body = Ui.Rect("점 몸체", slot, 0, 0, 40, 40); dots.bodies[i] = body;
    dots.streaks[i] = Disc("가로 빛줄기", body, 20, 20, 160, 6, tint);
    dots.halos[i] = Disc("빛", body, 20, 20, 30, 30, tint);
    dots.ghostC[i] = Disc("청록 잔상", body, 20, 20, 10, 10, Ui.Cyan);
    dots.ghostR[i] = Disc("붉은 잔상", body, 20, 20, 10, 10, Ui.Red);
    dots.rings[i] = Ui.Box("점", body, 20 - 8.5f, 20 - 8.5f, 17, 17, tint, true, 1, 32);
    dots.cores[i] = Ui.Box("점 가운데", body, 20 - 4.5f, 20 - 4.5f, 9, 9, Color.Lerp(tint, Color.white, .75f), true, 1, 32);
    if (font) { var label = Ui.Text("점 이름", root, x - 30, y + 16, 60, 18, Labels[i], font, 12, new Color(tint.r, tint.g, tint.b, .85f)); label.alignment = TextAlignmentOptions.Center; }
   }
   dots.Update(); return dots;
  }
  static SoftDisc Disc(string name, Transform parent, float cx, float cy, float w, float h, Color tint) {
   var r = Ui.Rect(name, parent, cx - w / 2, cy - h / 2, w, h); var d = r.gameObject.AddComponent<SoftDisc>(); d.color = tint; d.raycastTarget = false; return d;
  }
  static float Hash(int n) { n = (n << 13) ^ n; return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f; }
  static void Alpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
  public void Choose(int color) { Finish(color); }
  public void Cancel() { Finish(-1); }
  void Finish(int color) { var p = pick; var c = cancel; pick = null; cancel = null; Destroy(gameObject); if (color >= 0) p?.Invoke(color); else c?.Invoke(); }
  void Update() {
   float t = Time.unscaledTime - start, phase = t % Cycle; int repeat = Mathf.FloorToInt(t / Cycle);
   for (int i = 0; i < 3; i++) {
    if (!bodies[i]) continue;
    if (phase < Appear) {
     float a = (1 - phase / Appear) * (repeat > 0 ? .8f : 1); int tick = Mathf.FloorToInt(t * 32) * 7 + i * 131;
     bodies[i].anchoredPosition = new Vector2((Hash(tick) - .5f) * 14 * a, 0);
     bool off = Hash(tick + 3) < .18f * a;
     Alpha(rings[i], off ? 0 : .65f + .35f * a); Alpha(cores[i], off ? 0 : 1); Alpha(halos[i], .3f + .5f * a);
     Alpha(streaks[i], .75f * a); streaks[i].rectTransform.localScale = new Vector3(.6f + .4f * Hash(tick + 5), 1, 1);
     float split = 4 + 8 * a * Hash(tick + 9);
     ghostC[i].rectTransform.anchoredPosition = new Vector2(20 - 5 - split, -20 + 5); ghostR[i].rectTransform.anchoredPosition = new Vector2(20 - 5 + split, -20 + 5);
     Alpha(ghostC[i], .7f * a); Alpha(ghostR[i], .7f * a);
    } else {
     bodies[i].anchoredPosition = Vector2.zero; Alpha(streaks[i], 0); Alpha(ghostC[i], 0); Alpha(ghostR[i], 0);
     Alpha(halos[i], .3f); Alpha(rings[i], .65f); Alpha(cores[i], 1);
    }
   }
  }
 }
}

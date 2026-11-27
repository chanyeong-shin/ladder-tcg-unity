using System.Collections;
using System.Linq;
using UnityEngine.UI;
using UnityEngine;

namespace Breach.UI {
 // Card hold (2026-11-21 selection): holding a card darkens everything but the board by 55% in 0.25 s,
 // and the screen circuit (22%, soft glow) draws itself under that dark layer in 0.5 s from the
 // screen edge toward the UI parts. The board keeps a faint inner circuit (7%).
 // While the card is scanned the circuit stays faint (2026-11-22); activation runs comets along the board-bound bundles.
 public sealed class CardHoldFx : MonoBehaviour {
  public static readonly Rect Board = new Rect(582, 129, 800, 716);
  RectTransform root; CircuitGraphic screen, inner, band, signals; Shape[] dims;
  float show, reveal, target, dimWeight = 1, innerPulse; float hideSeconds = .25f;
  // seconds since the hold appeared (the HTML variant clock t of 02_redcircuit.js / 03_boardcircuit.js)
  public float HoldTime { get; private set; }
  public float ScreenAlpha { get { return screen ? screen.color.a : 0; } }
  public float BandY { get { return band ? band.BandY : float.NaN; } }
  public bool Visible { get { return target > 0; } }
  public float Amount { get { return show; } }

  BattleController host;
  static float Lum(Color c) { return .2126f * c.r + .7152f * c.g + .0722f * c.b; }
  // user 11-22: blue turn -> red circuit, red turn -> blue circuit (background index as in BattleController.Render)
  Color Tint() { if (!host || host.State == null) return EventFx.Me; int bg = host.State.Phase == 2 ? 2 : host.State.Actor == 0 ? 0 : 1; return bg == 0 ? EventFx.Enemy : EventFx.Me; }
  public void Initialize(BattleController c) {
   host = c;
   root = Ui.Rect("카드 들기 - 어두운 막과 회로", c.game.transform, 0, 0, 1920, 1080);
   int above = c.backgrounds.Max(b => b.transform.GetSiblingIndex()); root.SetSiblingIndex(above + 1);
   screen = Make("화면 회로 (막 아래)", "Circuit_Screen", new Color(Ui.Cyan.r, Ui.Cyan.g, Ui.Cyan.b, .22f));
   var shade = new Color(0, 0, 0, .55f);
   dims = new[] {
    Ui.Box("판 밖 어둡게 - 위", root, 0, 0, 1920, Board.y, shade), Ui.Box("판 밖 어둡게 - 아래", root, 0, Board.yMax, 1920, 1080 - Board.yMax, shade),
    Ui.Box("판 밖 어둡게 - 왼쪽", root, 0, Board.y, Board.x, Board.height, shade), Ui.Box("판 밖 어둡게 - 오른쪽", root, Board.xMax, Board.y, 1920 - Board.xMax, Board.height, shade) };
   inner = Make("판 안 회로 (희미하게)", "Circuit_Board", new Color(Ui.Cyan.r, Ui.Cyan.g, Ui.Cyan.b, .07f)); inner.Glow = 0;
   // week 12 item 3 (03_boardcircuit.js "flow"): a 180 px bright band runs down the board every 3.0 s
   band = Make("판 안 회로 - 흐르는 띠", "Circuit_Board", Color.clear); band.Glow = 1; band.BandHeight = 180;
   signals = Make("발동 신호", "Circuit_Screen", Color.white); signals.Signals = true;
   Apply();
  }
  CircuitGraphic Make(string name, string data, Color color) { var g = Ui.Rect(name, root, 0, 0, 1920, 1080).gameObject.AddComponent<CircuitGraphic>(); g.Data = CircuitData.Load(data); g.color = color; g.raycastTarget = false; return g; }
  public void Show(bool dim = true) { if (show <= 0 && target <= 0) HoldTime = 0; target = 1; if (dim) dimWeight = 1; else if (show <= 0) dimWeight = 0; }
  public void Hide(float seconds = .25f) { target = 0; hideSeconds = Mathf.Max(.01f, seconds); }
  public void HideNow() { target = show = reveal = 0; if (signals) signals.SignalTime = -1; Apply(); }
  void Update() {
   float dt = Time.unscaledDeltaTime * FxClock.Scale;
   if (target > 0 && show <= 0) HoldTime = 0; else if (show > 0 || target > 0) HoldTime += dt;
   if (target > 0) { show = Mathf.MoveTowards(show, 1, dt / .25f); reveal = Mathf.MoveTowards(reveal, 1, dt / .5f); }
   else { show = Mathf.MoveTowards(show, 0, dt / hideSeconds); if (show <= 0) reveal = 0; }
   Apply();
  }
  void Apply() {
   if (!root) return; float e = show * show * (3 - 2 * show);
   foreach (var d in dims) d.color = new Color(0, 0, 0, .55f * e * dimWeight);
   screen.Reveal = reveal; var ink = Tint();
   // user 11-22: the red circuit should look as bright as the blue one; match perceived luminance (cyan 0.73 vs red 0.45)
   float gain = Lum(EventFx.Me) / Mathf.Max(.05f, Lum(ink));
   // week 12 item 2 (02_redcircuit.js "pulse"): in the blue turn the red circuit breathes, a = .46 + .16 sin(2 pi t / 2.0), Glow .6;
   // the red turn keeps its blue circuit at .22 x gain, Glow .4 (the user's reference)
   bool blueTurn = ink == EventFx.Enemy;
   screen.Glow = blueTurn ? .6f : .4f;
   float screenAlpha = blueTurn ? .46f + .16f * Mathf.Sin(2 * Mathf.PI * HoldTime / 2.0f) : Mathf.Min(1, .22f * gain);
   screen.color = new Color(ink.r, ink.g, ink.b, screenAlpha * e); screen.SetVerticesDirty();
   // week 12 item 3: always-on board circuit .12 x gain (was .073 x gain), Glow 0
   inner.Reveal = 1; inner.color = new Color(ink.r, ink.g, ink.b, Mathf.Max(.12f * gain * e * Mathf.Clamp01(reveal), innerPulse * gain)); inner.SetVerticesDirty();
   // band: y = B.y - 90 + ((t % 3.0) / 3.0) * (B.h + 180), color = ink mixed 40% toward white, alpha .55, Glow 1.0 (halo .55 x .35)
   var light = Color.Lerp(ink, Color.white, .4f);
   band.Reveal = 1; band.BandY = Board.y - 90 + ((HoldTime % 3f) / 3f) * (Board.height + 180); band.color = new Color(light.r, light.g, light.b, .55f * e * Mathf.Clamp01(reveal)); band.SetVerticesDirty();
  }
  // Activation: one comet per board-bound bundle, staggered over 0.45 s, 0.75 s each, while the circuit stays faint.
  public IEnumerator Activate(System.Func<float> delta, System.Action<float> progress) {
   signals.color = new Color(1, 1, 1, .85f); signals.Ink = Tint();
   for (float t = 0; t < CircuitGraphic.SignalLength; t += delta()) { progress?.Invoke(t / CircuitGraphic.SignalLength); signals.SignalTime = t; innerPulse = .2f * Mathf.Max(0, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / CircuitGraphic.SignalLength))); signals.SetVerticesDirty(); yield return null; }
   innerPulse = 0; signals.SignalTime = -1; signals.SetVerticesDirty();
  }
  void OnDisable() { HideNow(); }
 }
 // Target ring blink from HTML tab 4: stroke-opacity .55 + .45 * |sin(4u + i)|.
 public sealed class RingPulse : MonoBehaviour {
  public int Phase; float start; Graphic shape;
  void OnEnable() { start = Time.unscaledTime; shape = GetComponent<Graphic>(); }
  void Update() { if (!shape) return; var c = shape.color; c.a = .55f + .45f * Mathf.Abs(Mathf.Sin((Time.unscaledTime - start) * 4 + Phase)); shape.color = c; }
 }
}

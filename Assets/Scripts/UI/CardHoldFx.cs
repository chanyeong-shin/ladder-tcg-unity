using System.Collections;
using System.Linq;
using UnityEngine;

namespace Breach.UI {
 // Card hold (2026-11-21 selection): holding a card darkens everything but the board by 55% in 0.25 s,
 // and the screen circuit (22%, soft glow) draws itself under that dark layer in 0.5 s from the
 // screen edge toward the UI parts. The board keeps a faint inner circuit (7%).
 // Scanning hides the circuit; activation then runs signals along the board-bound bundles.
 public sealed class CardHoldFx : MonoBehaviour {
  public static readonly Rect Board = new Rect(582, 129, 800, 716);
  RectTransform root; CircuitGraphic screen, inner, signals; Shape[] dims;
  float show, reveal, target; float hideSeconds = .25f;
  public bool Visible { get { return target > 0; } }
  public float Amount { get { return show; } }

  public void Initialize(BattleController c) {
   root = Ui.Rect("카드 들기 - 어두운 막과 회로", c.game.transform, 0, 0, 1920, 1080);
   int above = c.backgrounds.Max(b => b.transform.GetSiblingIndex()); root.SetSiblingIndex(above + 1);
   screen = Make("화면 회로 (막 아래)", "Circuit_Screen", new Color(Ui.Cyan.r, Ui.Cyan.g, Ui.Cyan.b, .22f));
   var shade = new Color(0, 0, 0, .55f);
   dims = new[] {
    Ui.Box("판 밖 어둡게 - 위", root, 0, 0, 1920, Board.y, shade), Ui.Box("판 밖 어둡게 - 아래", root, 0, Board.yMax, 1920, 1080 - Board.yMax, shade),
    Ui.Box("판 밖 어둡게 - 왼쪽", root, 0, Board.y, Board.x, Board.height, shade), Ui.Box("판 밖 어둡게 - 오른쪽", root, Board.xMax, Board.y, 1920 - Board.xMax, Board.height, shade) };
   inner = Make("판 안 회로 (희미하게)", "Circuit_Board", new Color(Ui.Cyan.r, Ui.Cyan.g, Ui.Cyan.b, .07f)); inner.Glow = 0;
   signals = Make("발동 신호", "Circuit_Screen", Color.white); signals.Signals = true;
   Apply();
  }
  CircuitGraphic Make(string name, string data, Color color) { var g = Ui.Rect(name, root, 0, 0, 1920, 1080).gameObject.AddComponent<CircuitGraphic>(); g.Data = CircuitData.Load(data); g.color = color; g.raycastTarget = false; return g; }
  public void Show() { target = 1; }
  public void Hide(float seconds = .25f) { target = 0; hideSeconds = Mathf.Max(.01f, seconds); }
  public void HideNow() { target = show = reveal = 0; if (signals) signals.SignalTime = -1; Apply(); }
  void Update() {
   float dt = Time.unscaledDeltaTime;
   if (target > 0) { show = Mathf.MoveTowards(show, 1, dt / .25f); reveal = Mathf.MoveTowards(reveal, 1, dt / .5f); }
   else { show = Mathf.MoveTowards(show, 0, dt / hideSeconds); if (show <= 0) reveal = 0; }
   Apply();
  }
  void Apply() {
   if (!root) return; float e = show * show * (3 - 2 * show);
   foreach (var d in dims) d.color = new Color(0, 0, 0, .55f * e);
   screen.Reveal = reveal * reveal * (3 - 2 * reveal); screen.color = new Color(Ui.Cyan.r, Ui.Cyan.g, Ui.Cyan.b, .22f * e); screen.SetVerticesDirty();
   inner.Reveal = 1; inner.color = new Color(Ui.Cyan.r, Ui.Cyan.g, Ui.Cyan.b, .07f * e); inner.SetVerticesDirty();
  }
  // Activation, 0.9 s: each board-bound bundle sends a signal (0–0.3 s staggered) that flashes the board-edge pad.
  public IEnumerator Activate(System.Func<float> delta, System.Action<float> progress) {
   signals.color = Color.white;
   for (float t = 0; t < .9f; t += delta()) { progress?.Invoke(t / .9f); signals.SignalTime = t; signals.SetVerticesDirty(); yield return null; }
   signals.SignalTime = -1; signals.SetVerticesDirty();
  }
  void OnDisable() { HideNow(); }
 }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Breach.UI {
 // --soak-check <dir> [seconds]: 실제 화면 흐름으로 여러 판을 자동으로 두며 예외(에러 로그)가 나는지 본다.
 // 2026-11-27 사용자 화면 왼쪽 아래 NullReferenceException(재현 조건 모름) 때문에 만듦. 균형 측정이 아니라 오류 찾기용이다.
 // 내 턴: 낼 수 있는 카드를 무작위로 골라 반은 끌어 놓기(OnBeginDrag/OnDrag/OnEndDrag), 반은 Select/Use 로 낸다. 가로선 색 고르기는 무작위.
 // 발동 효과는 판마다 바꾼다(무작위 포함). 끝나면 다시 시작. 예외가 나면 처음 3종의 스택을 soak-result.txt 에 남긴다.
 public sealed class SoakCheck : MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() { if (Array.IndexOf(Environment.GetCommandLineArgs(), "--soak-check") >= 0) FindFirstObjectByType<BattleController>().gameObject.AddComponent<SoakCheck>(); }
  readonly List<string> errors = new List<string>(); int plays, drags, turns, games, resolves;
  void OnEnable() { Application.logMessageReceived += Log; }
  void OnDisable() { Application.logMessageReceived -= Log; }
  void Log(string msg, string stack, LogType type) { if (type == LogType.Exception || type == LogType.Error) { string key = msg + "\n" + stack; if (!errors.Contains(key)) errors.Add(key); } }
  Vector2 Screen(float x, float y, BattleController c) { return RectTransformUtility.WorldToScreenPoint(null, c.stage.TransformPoint(new Vector3(x, -y, 0))); }
  IEnumerator Start() {
   var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--soak-check"); if (at < 0 || at + 1 >= args.Length) yield break;
   string dir = args[at + 1]; Directory.CreateDirectory(dir); float limit = at + 2 < args.Length && float.TryParse(args[at + 2], out var v) ? v : 300;
   Application.runInBackground = true; var c = GetComponent<BattleController>(); c.animationSpeed = 3; var rnd = new System.Random(1127);
   int[] picks = { ActivateFx.Lockon, EffectPicker.Random, ActivateFx.Glitch, ActivateFx.Hexes, ActivateFx.Stream, ActivateFx.Now };
   float end = Time.realtimeSinceStartup + limit; int lastRound = -1;
   EffectPicker.Choice = picks[0];
   yield return new WaitForSecondsRealtime(.5f); c.startButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(1.8f);
   while (Time.realtimeSinceStartup < end) {
    if (c.result && c.result.activeSelf) { games++; EffectPicker.Choice = picks[games % picks.Length]; yield return new WaitForSecondsRealtime(.8f); c.retryButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(1); continue; }
    if (c.State != null && c.State.Round != lastRound) { lastRound = c.State.Round; resolves++; }
    if (!c.CanInput) { yield return null; continue; }
    // 내 턴: 최대 3장
    turns++; int n = rnd.Next(1, 4);
    for (int k = 0; k < n && c.CanInput && c.State.Hands[0].Count > 0; k++) {
     var opts = new List<(int i, int t, bool rung)>();
     for (int i = 0; i < c.State.Hands[0].Count; i++) { var card = c.State.Hands[0][i]; int max = card == Card.Reroute ? 6 : 3; for (int t = 0; t < max; t++) if (c.State.CanPlay(0, i, t)) opts.Add((i, t, card == Card.Reroute)); }
     if (opts.Count == 0) break; var o = opts[rnd.Next(opts.Count)]; var cardO = c.State.Hands[0][o.i];
     var view = c.hand.GetComponentsInChildren<CardView>().FirstOrDefault(x => x.index == o.i);
     if (view && rnd.Next(2) == 0) {
      // 끌어 놓기: 대상 위치로 3단계 이동 후 놓음
      Vector2 to = cardO == Card.Reroute ? new Vector2(BattleController.LaneX[(o.t % 2)] + 140, BattleController.RowY[o.t / 2]) : cardO == Card.Inject || cardO == Card.ZeroDay ? new Vector2(BattleController.LaneX[o.t], 760) : cardO == Card.Firewall ? new Vector2(BattleController.LaneX[o.t], 500) : new Vector2(982, 480);
      var rt = (RectTransform)view.transform; Vector2 from = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
      var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = from, pressPosition = from };
      view.OnBeginDrag(e); yield return null;
      for (int s = 1; s <= 3; s++) { e.position = Vector2.Lerp(from, Screen(to.x, to.y, c), s / 3f); view.OnDrag(e); yield return null; yield return null; }
      view.OnEndDrag(e); drags++;
     } else { c.Select(o.i); c.Use(o.t, o.rung); }
     plays++;
     float wait = Time.realtimeSinceStartup + 6; while (!c.CanInput && Time.realtimeSinceStartup < wait) { if (c.BridgeOpen) c.PickBridge(rnd.Next(3)); yield return null; }
     yield return new WaitForSecondsRealtime(.05f);
    }
    if (c.CanInput) { float w = Time.realtimeSinceStartup + 8; while (!c.Settled && Time.realtimeSinceStartup < w) yield return null; c.endButton.onClick.Invoke(); }
    yield return new WaitForSecondsRealtime(.2f);
   }
   string head = $"games {games} · rounds seen {resolves} · my turns {turns} · plays {plays} (drags {drags}) · {limit:0} s";
   File.WriteAllText(Path.Combine(dir, "soak-result.txt"), errors.Count == 0 ? "PASS: no exceptions · " + head : "FAIL: " + errors.Count + " kinds · " + head + "\n\n" + string.Join("\n----\n", errors.Take(3)));
   Application.Quit(errors.Count == 0 ? 0 : 1);
  }
 }
}

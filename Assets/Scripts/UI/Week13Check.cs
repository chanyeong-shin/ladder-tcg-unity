using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Breach.UI {
 // --week13-check <dir> 에서만 돈다. 13주차 빔 연출(_도구/시안13주차/tabs/04_beam.js A안)을 시안과 같은 장면·같은 시각에 1920x1080으로 캡처한다.
 // 장면 1: 내 CH1 위력 2 단독 + CH2 내 3 대 상대 2 정면. 장면 2: 상대 CH3 위력 1 단독 + CH2 2 대 2 정면 (시안 SCN 그대로).
 // 장면 3 (2026-11-27 제로데이 새 규칙, 12_zeroday.js 장면 1 · 09_impact.js ⑦): 내 CH2 3(제로데이 1) 대 상대 3 — 일반 2 대 3 힘겨루기, 제로데이 1 통과 착탄.
 // 시각 T 는 시안과 같이 충전 시작부터의 초(충전 1.3초 = 07_windup.js MIXW, 힘겨루기 1.4초 = 09_impact.js planU).
 // 캡처 순간마다 FxClock.Scale = 0 으로 시계를 멈추고 실제 시각을 week13-moments.txt 에 적는다.
 public sealed class Week13Check : MonoBehaviour {
  const float Charge = CombatPresentation.ChargeSeconds, Struggle = CombatPresentation.StruggleSeconds;
  BattleController c; string dir; bool failed; readonly List<string> passed = new List<string>(), moments = new List<string>();
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() { if (Array.IndexOf(Environment.GetCommandLineArgs(), "--week13-check") >= 0) FindFirstObjectByType<BattleController>().gameObject.AddComponent<Week13Check>(); }
  IEnumerator Start() {
   var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--week13-check"); if (at < 0 || at + 1 >= args.Length) yield break;
   dir = args[at + 1]; Directory.CreateDirectory(dir); Application.runInBackground = true; c = GetComponent<BattleController>();
   yield return null; c.animationSpeed = 4; c.startButton.onClick.Invoke();
   yield return Await(() => c.CanInput, "start"); if (failed) yield break; c.animationSpeed = 1; c.feedback.speed = 1;

   // 소켓: 빈 상태(드문 점) · 카드를 집은 상태
   Fixture(0, 0); c.State.Hands[0].Add(Card.Inject); c.State.Hands[0].Add(Card.Inject); c.State.SaveAction(); c.Render(); yield return new WaitForSecondsRealtime(1.2f);
   FxClock.Scale = 0; yield return null; moments.Add("socket-empty clock=" + c.feedback.socket.Clock.ToString("0.000")); yield return Shot("w13-01-socket-empty"); FxClock.Scale = 1;
   var views = c.HandViews; var held = views[0]; var drag = new PointerEventData(EventSystem.current) { position = Center(held) };
   held.OnBeginDrag(drag); drag.position = Screen(1500, 560); held.OnDrag(drag); yield return new WaitForSecondsRealtime(.6f);
   if (!c.Dragging) { Fail("Drag did not start"); yield break; }
   FxClock.Scale = 0; yield return null; moments.Add("socket-held dragging=" + c.Dragging); yield return Shot("w13-02-socket-held"); FxClock.Scale = 1;
   held.OnEndDrag(drag); yield return Await(() => c.Settled, "drag return"); if (failed) yield break;
   passed.Add("socket empty / held captured");

   // 상대 턴 카드 스캔 (회로·빛 막대가 턴 색 빨강)
   Fixture(1, 0); c.State.SaveAction(); c.Render(); yield return null;
   bool scanned = false; StartCoroutine(Run(c.feedback.Play(Card.Inject, 1, new Vector2(860, 40), new Vector2(126, 201.6f)), () => scanned = true));
   yield return Await(() => c.feedback.Phase == "fragment" && c.feedback.Progress >= .5f, "enemy scan"); if (failed) yield break;
   FxClock.Scale = 0; yield return null; var ink = c.feedback.socket.TargetInk; moments.Add("enemy-scan progress=" + c.feedback.Progress.ToString("0.000") + " ink=" + ink);
   yield return Shot("w13-03-enemy-scan"); FxClock.Scale = 1;
   if (ink.r < .9f || ink.b > .6f) { Fail("Enemy turn scan ink is not red: " + ink); yield break; }
   yield return Await(() => scanned, "enemy scan end"); if (failed) yield break; yield return new WaitForSecondsRealtime(.3f);
   passed.Add("enemy turn scan ink red " + ink);

   // 빔 장면 2개
   yield return Scene(1, new[] { (0, 0, 2, 0), (0, 1, 3, 0), (1, 1, 2, 0) }); if (failed) yield break;
   yield return Scene(2, new[] { (1, 2, 1, 0), (0, 1, 2, 0), (1, 1, 2, 0) }); if (failed) yield break;
   yield return Scene(3, new[] { (0, 1, 3, 1), (1, 1, 3, 0) }); if (failed) yield break;
   File.WriteAllText(Path.Combine(dir, "week13-moments.txt"), string.Join("\n", moments));
   File.WriteAllText(Path.Combine(dir, "week13-result.txt"), "PASS: " + string.Join("; ", passed)); Application.Quit(0);
  }
  // 시안과 같은 시각(충전 시작부터 T초)들: 섬광 .05, 충전 .60, 흰색으로 꽉 참 1.25, 뻗는 중 CH+.2, 맞닿음 +.12, 단독 착탄 CH+F+.1,
  // 펌프 맞닿음+.7, 승부 파동 맞닿음+STR+.12, 착탄 맞닿음+STR+.3
  IEnumerator Scene(int n, (int owner, int lane, int power, int zero)[] shots) {
   Fixture(0, 2); foreach (var s in shots) { c.State.Power[s.owner, s.lane] = s.power; c.State.Zero[s.owner, s.lane] = s.zero; c.State.Pierce[s.owner, s.lane] = s.zero > 0; }
   for (int i = 0; i < 3; i++) c.State.Health[0, i] = c.State.Health[1, i] = 3;
   c.Render(); var report = c.State.Resolve(); c.Render();
   foreach (var server in c.servers) server.Render(report.Before[server.owner, server.channel], report.Packets.Where(p => p.Owner == server.owner && p.Start == server.channel).Select(p => p.Initial).FirstOrDefault());
   var pres = c.presentation; c.animationSpeed = .25f;
   bool done = false; var phases = new List<string>(); var clashPhases = new List<string>();
   StartCoroutine(Run(pres.Play(report), () => done = true)); StartCoroutine(Watch(() => done, phases, clashPhases));
   yield return Await(() => pres.Phase == "charge", "scene " + n + " charge"); if (failed) yield break;
   float c0 = pres.ClashContact; if (c0 < 0) { Fail("Scene " + n + " has no head-on clash"); yield break; }
   var list = new List<(string name, float T)> { ("flash", .05f), ("charge", .60f), ("charge-full", 1.25f), ("beam", Charge + .2f), ("contact", Charge + c0 + .12f), ("free-hit", Charge + .6f), ("pump", Charge + c0 + .7f), ("break", Charge + c0 + Struggle + .12f), ("land", Charge + c0 + Struggle + .3f) };
   foreach (var m in list.OrderBy(x => x.T)) {
    yield return Await(() => m.T < Charge ? (pres.Phase == "charge" && pres.ChargeTime >= m.T) : (pres.Phase == "flight" && pres.FlightTime >= m.T - Charge), "scene " + n + " " + m.name); if (failed) yield break;
    FxClock.Scale = 0; yield return null;
    float T = pres.Phase == "charge" ? pres.ChargeTime : Charge + pres.FlightTime;
    moments.Add("scene" + n + " " + m.name + " target T=" + m.T.ToString("0.000") + " actual T=" + T.ToString("0.000") + " phase=" + pres.Phase + " clash=" + pres.ClashPhase + " contact c0=" + c0.ToString("0.000") + " zoom=" + pres.CameraZoom.ToString("0.000"));
    yield return Shot("w13-scene" + n + "-" + m.name); FxClock.Scale = 1;
   }
   yield return Await(() => done, "scene " + n + " end"); if (failed) yield break; c.animationSpeed = 1;
   string ph = string.Join(">", phases), cph = string.Join(">", clashPhases);
   moments.Add("scene" + n + " phases " + ph + " clash " + cph);
   if (ph != "charge>flight>return>idle") { Fail("Scene " + n + " phases " + ph); yield break; }
   if (cph != "idle>hold>yield>break>idle") { Fail("Scene " + n + " clash phases " + cph); yield break; }
   if (pres.CameraZoom != 1 || pres.ResolutionFocused) { Fail("Scene " + n + " camera not restored"); yield break; }
   passed.Add("scene " + n + " phases " + ph + ", clash " + cph + ", contact " + c0.ToString("0.000") + " s");
  }
  IEnumerator Watch(Func<bool> done, List<string> phases, List<string> clash) {
   var pres = c.presentation;
   while (!done()) { if (phases.Count == 0 || phases[phases.Count - 1] != pres.Phase) phases.Add(pres.Phase); if (clash.Count == 0 || clash[clash.Count - 1] != pres.ClashPhase) clash.Add(pres.ClashPhase); yield return null; }
   if (phases[phases.Count - 1] != pres.Phase) phases.Add(pres.Phase); if (clash[clash.Count - 1] != pres.ClashPhase) clash.Add(pres.ClashPhase);
   phases.RemoveAll(p => p == "focus" || p == "ready");
  }
  Vector2 Center(CardView v) { var r = (RectTransform)v.transform; return RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center)); }
  Vector2 Screen(float x, float y) { return RectTransformUtility.WorldToScreenPoint(null, c.stage.TransformPoint(new Vector3(x, -y, 0))); }
  void Fixture(int attacker, int phase) {
   var s = c.State; s.Attacker = attacker; s.Phase = phase; s.Winner = -1; s.DrawUsed = false; s.Hidden.Clear(); Array.Clear(s.Power, 0, 6); Array.Clear(s.Walls, 0, 3); Array.Clear(s.Pierce, 0, 6); Array.Clear(s.Zero, 0, 6);
   s.Hands[0].Clear(); s.Hands[1].Clear(); for (int i = 0; i < 3; i++) { s.Health[0, i] = s.Health[1, i] = 1; s.Rungs[i] = -1; s.RungOwners[i] = -1; s.RungRiders[i] = -1; }
  }
  IEnumerator Run(IEnumerator routine, Action done) { yield return routine; done(); }
  IEnumerator Await(Func<bool> test, string name) { float until = Time.realtimeSinceStartup + 40; while (!test() && Time.realtimeSinceStartup < until) yield return null; if (!test()) Fail("Timeout " + name + " (presentation " + c.presentation.Phase + " " + c.presentation.FlightTime.ToString("0.000") + ", card " + c.feedback.Phase + ")"); }
  IEnumerator Shot(string name) {
   yield return new WaitForEndOfFrame(); var canvas = c.stage.GetComponentInParent<Canvas>(); var camera = Camera.main; var texture = new RenderTexture(1920, 1080, 24); var mode = canvas.renderMode;
   canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; camera.targetTexture = texture; Canvas.ForceUpdateCanvases(); camera.Render();
   var old = RenderTexture.active; RenderTexture.active = texture; var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(dir, name + ".png"), image.EncodeToPNG());
   RenderTexture.active = old; camera.targetTexture = null; canvas.renderMode = mode; Destroy(image); texture.Release(); Destroy(texture); yield return null;
  }
  void Fail(string message) { if (failed) return; failed = true; FxClock.Scale = 1; File.WriteAllText(Path.Combine(dir, "week13-moments.txt"), string.Join("\n", moments)); File.WriteAllText(Path.Combine(dir, "week13-result.txt"), "FAIL: " + message + " | passed: " + string.Join("; ", passed)); Application.Quit(1); }
 }
}

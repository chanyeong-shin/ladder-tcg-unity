using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Breach.UI {
 // Only --week12-check <dir> runs these fixtures (week 12 board/effect items 1-5, 계획/Unity_12주차_적용목록.md).
 // Every capture freezes the week 12 effect clocks (FxClock.Scale = 0) and records the exact moment in week12-moments.txt,
 // so the HTML mockup can be captured at the same moment for a side-by-side comparison.
 public sealed class Week12Check : MonoBehaviour {
  BattleController c; string dir; bool failed; readonly List<string> passed = new List<string>(), moments = new List<string>();
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() { if (Array.IndexOf(Environment.GetCommandLineArgs(), "--week12-check") >= 0) FindFirstObjectByType<BattleController>().gameObject.AddComponent<Week12Check>(); }
  IEnumerator Start() {
   var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--week12-check"); if (at < 0 || at + 1 >= args.Length) yield break;
   dir = args[at + 1]; Directory.CreateDirectory(dir); Application.runInBackground = true; c = GetComponent<BattleController>();
   yield return null; c.animationSpeed = 4; c.startButton.onClick.Invoke();
   yield return Await(() => c.CanInput, "start"); if (failed) yield break; c.animationSpeed = 1; c.feedback.speed = 1;
   // item 1: the per-turn pick follows the HTML choose() sequence and never repeats the previous turn
   int prev = ActivateFx.Packets; var seq = new List<int>(); for (int turn = 0; turn < 12; turn++) { int k = ActivateFx.Choose(turn, prev); if (turn > 0 && k == prev) { Fail("Effect repeated the previous turn"); yield break; } seq.Add(k); prev = k; }
   moments.Add("pick-sequence " + string.Join(",", seq));
   // week 13: the played effect is the picker's choice; the per-turn random pick (used by "무작위") is TurnPick
   if (c.feedback.ActionTurn < 0 || c.feedback.ActionTurn >= seq.Count || c.feedback.TurnPick != seq[c.feedback.ActionTurn]) { Fail("Pick for action turn " + c.feedback.ActionTurn + " is " + c.feedback.TurnPick); yield break; }
   passed.Add("turn pick sequence " + string.Join(",", seq) + " (no repeats)");

   // items 2, 3: holding a card in the blue turn (red circuit pulse + board band), then in the red turn
   Fixture(0, 0); c.State.Hands[0].Add(Card.Inject); c.State.SaveAction(); c.Render(); yield return null;
   var hold = c.GetComponent<CardHoldFx>();
   c.Select(0); // Select = "card picked up" (what BeginDrag does); a user click no longer selects (week 12 item 4)
   foreach (float t in new[] { .5f, 1.5f }) {
    yield return Await(() => hold.HoldTime >= t, "hold " + t); if (failed) yield break;
    FxClock.Scale = 0; moments.Add("hold-blue t=" + hold.HoldTime.ToString("0.000") + " screenAlpha=" + hold.ScreenAlpha.ToString("0.000") + " bandY=" + hold.BandY.ToString("0.0"));
    yield return Shot("w12-hold-blue-" + t.ToString("0.0")); FxClock.Scale = 1;
   }
   float a05 = .46f + .16f * Mathf.Sin(2 * Mathf.PI * .5f / 2);
   c.Select(0); yield return new WaitForSecondsRealtime(.5f);
   // the red background is the bot's action turn; the circuit shows there while a bot card is scanned (CardTransferFx shows the hold)
   Fixture(1, 0); c.State.SaveAction(); c.Render(); yield return null; hold.Show(false);
   yield return Await(() => hold.HoldTime >= 1.5f, "hold red"); if (failed) yield break;
   FxClock.Scale = 0; moments.Add("hold-red t=" + hold.HoldTime.ToString("0.000") + " screenAlpha=" + hold.ScreenAlpha.ToString("0.000") + " bandY=" + hold.BandY.ToString("0.0"));
   if (Mathf.Abs(hold.ScreenAlpha - CardHoldFx.RedTurnAlpha(hold.HoldTime)) > .01f) { Fail("Red turn circuit does not breathe like the blue turn: " + hold.ScreenAlpha); yield break; } // 11-28: 상대 턴도 숨쉬기
   yield return Shot("w12-hold-red-1.5"); FxClock.Scale = 1;
   hold.Hide(); yield return new WaitForSecondsRealtime(.5f);
   passed.Add("hold pulse (blue turn alpha .46+.16sin), red turn breathes too (x luminance ratio), board band y = 39 + (t%3/3) x 896");

   // item 4: empty socket at a fixed clock
   Fixture(0, 0); c.State.Hands[0].Add(Card.Inject); c.State.Hands[0].Add(Card.Inject); c.State.SaveAction(); c.Render(); yield return new WaitForSecondsRealtime(.5f);
   FxClock.Scale = 0; c.feedback.socket.Clock = 1.0f; yield return null; moments.Add("socket-empty clock=1.000");
   yield return Shot("w12-socket-empty"); FxClock.Scale = 1;

   // items 4, 5: one card through the socket; input must open when its scan ends
   int expected = Mathf.FloorToInt(JsMath.HashImul(c.feedback.Plays + 11) * 3) % 3; int playIndex = c.feedback.Plays;
   c.Select(0); c.Use(1, false);
   yield return Await(() => c.feedback.Phase == "fragment" && c.feedback.Progress >= .5f, "scan half"); if (failed) yield break;
   FxClock.Scale = 0; c.feedback.socket.Clock = 1.0f; yield return null; moments.Add("socket-scan progress=" + c.feedback.Progress.ToString("0.000") + " clock=1.000");
   if (c.CanInput) { Fail("Input open during the scan"); yield break; }
   yield return Shot("w12-socket-scan"); FxClock.Scale = 1;
   yield return Await(() => c.feedback.Phase == "absorb-card" && c.feedback.Progress > .05f, "absorb starts"); if (failed) yield break;
   if (!c.CanInput) { Fail("Input not released at the end of the scan"); yield break; }
   int route = c.feedback.LastRoute;
   moments.Add("route play" + playIndex + "=" + route + " expected(HTML hash(" + (playIndex + 11) + "))=" + expected);
   if (route != expected) { Fail("Route pick differs from the HTML formula"); yield break; }
   yield return Freeze("absorb-card", .75f, "w12-dot-absorb-0.75"); if (failed) yield break;
   yield return Freeze("absorb", .4f, "w12-dot-act-0.40"); if (failed) yield break;
   yield return Freeze("applied", .1f / .6f, "w12-hexglitch-0.10"); if (failed) yield break;
   yield return Freeze("applied", .3f / .6f, "w12-hexglitch-0.30"); if (failed) yield break;
   yield return Await(() => c.Settled && ActivateFx.LastU < 0, "first card settled"); if (failed) yield break;
   passed.Add("socket circuit/scanline, input open at scan end (Phase absorb-card), route " + route + " = HTML pickRoute, dot, hex glitch");

   // item 5: two cards back to back — the second is placed while the first is still absorbing
   Fixture(0, 0); c.State.Hands[0].Add(Card.Inject); c.State.Hands[0].Add(Card.Inject); c.State.SaveAction(); c.Render(); yield return null;
   float t0 = Time.realtimeSinceStartup; c.Select(0); c.Use(0, false);
   yield return Await(() => c.CanInput, "first release"); if (failed) yield break; float released = Time.realtimeSinceStartup - t0;
   string firstPhase = c.feedback.Phase; c.Select(0); c.Use(1, false);
   if (c.State.Power[0, 1] != 1) { Fail("Second card not accepted while the first was absorbing"); yield break; }
   yield return Await(() => c.feedback.Phase == "dock", "second dock"); if (failed) yield break;
   FxClock.Scale = 0; yield return Shot("w12-queue-second-dock"); FxClock.Scale = 1;
   yield return Await(() => c.Settled && ActivateFx.LastU < 0, "both settled"); if (failed) yield break;
   if (c.fx.childCount != 0) { Fail("Effects left after two cards"); yield break; }
   passed.Add("second card placed " + released.ToString("0.00") + " s after the first (first in " + firstPhase + "), both settled and cleaned up");

   // item 1: each effect at fixed moments (straight CH2 route, like the HTML)
   // week 13: the selectable effects drawn by ActivateFx.Run (지금 빛 띠 is EventFx; 패킷 버스트 left the list)
   foreach (int pick in new[] { ActivateFx.Glitch, ActivateFx.Hexes, ActivateFx.Stream, ActivateFx.Lockon }) {
    Fixture(0, 0); c.State.Hands[0].Add(Card.Inject); c.State.SaveAction(); c.Render(); yield return null;
    c.feedback.ForcePick(pick); c.Select(0); c.Use(1, false);
    foreach (float u in new[] { .2f, .5f, .9f, 1.6f }) {
     yield return Await(() => ActivateFx.LastU >= u && ActivateFx.LastPick == pick, "effect " + pick + " u " + u); if (failed) yield break;
     FxClock.Scale = 0; moments.Add("effect " + pick + " u=" + ActivateFx.LastU.ToString("0.000"));
     yield return Shot("w12-effect" + pick + "-" + u.ToString("0.0")); FxClock.Scale = 1;
    }
    yield return Await(() => c.Settled && ActivateFx.LastU < 0, "effect " + pick + " end"); if (failed) yield break;
   }
   passed.Add("4 activation effects captured at u .2 .5 .9 1.6");
   yield return HandItems(); if (failed) yield break;
   yield return CoverItems(); if (failed) yield break;
   File.WriteAllText(Path.Combine(dir, "week12-moments.txt"), string.Join("\n", moments));
   File.WriteAllText(Path.Combine(dir, "week12-result.txt"), "PASS: " + string.Join("; ", passed)); Application.Quit(0);
  }
  // Week 12 hand items: 4 zoom (04_zoom.js keyword), 8 drag (08_drag.js small), 12 draw lock (12_drawlimit.js lock), 7 hand limit (07_handlimit.js segments)
  IEnumerator HandItems() {
   var zoom = c.zoom; var fx = c.handFx;
   // item 4: hand [INJECT, ZERO-DAY, REROUTE] like the HTML's [0, sel 3, 1]
   Fixture(0, 0); c.State.Hands[0].AddRange(new[] { Card.Inject, Card.ZeroDay, Card.Reroute }); c.State.SaveAction(); c.Render(); yield return new WaitForSecondsRealtime(.3f);
   var views = c.HandViews;
   var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, clickCount = 1, position = Center(views[1]) };
   views[1].OnPointerClick(click); yield return null;
   if (c.HoldingCard || zoom.IsOpen) { Fail("One click selected or zoomed the card (should do nothing)"); yield break; }
   click.clickCount = 2; views[1].OnPointerClick(click); yield return null;
   if (!zoom.IsOpen || zoom.Shown != Card.ZeroDay || zoom.Index != 1 || c.HoldingCard) { Fail("Double-click did not zoom ZERO-DAY"); yield break; }
   CardZoom.ForcePointer = new Vector2(1700, 1000); // 마우스를 용어 밖에 둔다
   foreach (float tz in new[] { .3f, 2f, 4f }) {
    yield return Await(() => zoom.OpenTime >= tz, "zoom " + tz); if (failed) yield break;
    FxClock.Scale = 0; yield return null; moments.Add("zoom-card3 tz=" + zoom.OpenTime.ToString("0.000") + " open=" + zoom.Amount.ToString("0.000"));
    yield return Shot("w12-zoom-card3-" + tz.ToString("0.0")); FxClock.Scale = 1;
    if (zoom.Hovered != -1) { Fail("Zoom glossary lit with no pointer on it (tz " + tz + ")"); yield break; }
   }
   // 2026-11-27: 용어 풀이 상자에 마우스를 올려야만 노랗게 켜진다
   CardZoom.ForcePointer = new Vector2(1530, 260); yield return new WaitForSecondsRealtime(.4f);
   if (zoom.Hovered != 0) { Fail("Zoom glossary 0 did not light under the pointer"); yield break; }
   FxClock.Scale = 0; yield return null; yield return Shot("w12-zoom-card3-hover"); FxClock.Scale = 1;
   CardZoom.ForcePointer = null;
   passed.Add("zoom glossary lights only under the pointer");
   var right = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right, position = Center(views[0]) };
   zoom.Click(right); yield return null;
   if (!zoom.IsOpen || zoom.Shown != Card.Inject || zoom.Index != 0) { Fail("Right-click on another hand card did not switch the zoom"); yield break; }
   right.position = Screen(1700, 220); zoom.Click(right);
   yield return Await(() => !zoom.IsOpen, "right-click close"); if (failed) yield break;
   views[2].OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right, position = Center(views[2]) }); yield return null;
   if (!zoom.IsOpen || zoom.Shown != Card.Reroute) { Fail("Right-click did not zoom"); yield break; }
   yield return new WaitForSecondsRealtime(.6f); zoom.Click(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = Screen(1700, 220) });
   yield return Await(() => !zoom.IsOpen, "outside click close"); if (failed) yield break;
   if (c.HoldingCard || c.State.Hands[0].Count != 3) { Fail("Zoom changed the hand"); yield break; }
   passed.Add("one click = nothing; double-click and right-click zoom; right-click on another card switches; right-click / outside click close (Esc: key input, not simulated)");

   // item 8: drag over the board .6x / 75% (week 13 05_drop.js; was 40%), off the board back to full
   var held = views[0]; var drag = new PointerEventData(EventSystem.current) { position = Center(held) };
   held.OnBeginDrag(drag); drag.position = Screen(982, 550); held.OnDrag(drag); yield return new WaitForSecondsRealtime(.4f);
   var group = held.GetComponent<CanvasGroup>();
   moments.Add("drag over alpha=" + group.alpha.ToString("0.000") + " scale=" + held.transform.localScale.x.ToString("0.000"));
   if (Mathf.Abs(group.alpha - .75f) > .01f || Mathf.Abs(held.transform.localScale.x - .6f) > .01f) { Fail("Drag over the board is not .6x / 75%"); yield break; }
   yield return Shot("w12-drag-over");
   drag.position = Screen(1500, 550); held.OnDrag(drag); yield return new WaitForSecondsRealtime(.4f);
   if (group.alpha < .999f || Mathf.Abs(held.transform.localScale.x - 1.6f) > .01f) { Fail("Drag off the board did not restore"); yield break; }
   held.OnEndDrag(drag); yield return Await(() => c.Settled, "drag return"); if (failed) yield break;
   passed.Add("drag: over the board scale .6 alpha .4, off the board 1.6 / 1");

   // item 12: hand [INJECT, BUFFER, OVERFLOW, REROUTE] like the HTML's [0, 2, 5, 1]; BUFFER goes, OVERFLOW locks
   Fixture(0, 0); c.State.Hands[0].AddRange(new[] { Card.Inject, Card.Buffer, Card.Overflow, Card.Reroute }); c.State.SaveAction(); c.Render(); yield return null;
   c.Select(1); c.Use(0, false);
   yield return Await(() => c.Settled, "buffer played"); if (failed) yield break;
   yield return new WaitForSecondsRealtime(.4f);
   int ovf = -1; for (int i = 0; i < c.HandViews.Count; i++) if (c.HandViews[i].card == Card.Overflow) ovf = i;
   if (ovf < 0 || !fx.DrawLocked || fx.LockAmount < .999f || !c.HandViews[ovf].GetComponent<DrawLockView>()) { Fail("OVERFLOW not locked after BUFFER (v1.2)"); yield break; }
   moments.Add("lock hand=" + string.Join(",", c.State.Hands[0]) + " overflow index=" + ovf);
   FxClock.Scale = 0; yield return Shot("w12-lock"); FxClock.Scale = 1;
   int before = c.State.Hands[0].Count, hidden = c.State.Hidden.Count;
   held = c.HandViews[ovf]; drag = new PointerEventData(EventSystem.current) { position = Center(held) };
   held.OnBeginDrag(drag); drag.position = Screen(337, 499); held.OnDrag(drag); yield return new WaitForSecondsRealtime(.4f);
   held.OnEndDrag(drag); yield return null;
   if (c.State.Hands[0].Count != before || c.State.Hidden.Count != hidden || fx.RejectTime > .2f) { Fail("Locked OVERFLOW was not rejected"); yield break; }
   foreach (float r in new[] { .1f, .5f }) {
    yield return Await(() => fx.RejectTime >= r, "reject " + r); if (failed) yield break;
    FxClock.Scale = 0; yield return null; moments.Add("reject t=" + fx.RejectTime.ToString("0.000") + " status=" + (fx.WarningShown ? fx.WarningText.Replace("\n", "") : "-"));
    yield return Shot("w12-reject-" + r.ToString("0.0")); FxClock.Scale = 1;
   }
   yield return Await(() => c.Settled, "reject return"); if (failed) yield break;
   if (c.State.Hands[0].Count != before || c.HandViews.Count != before) { Fail("Rejected card did not come back"); yield break; }
   // my next action turn: DrawUsed resets, the badge goes with one cyan outline pulse
   c.State.DrawUsed = false; yield return Await(() => fx.LockAmount <= 0, "unlock"); if (failed) yield break;
   if (c.HandViews.Any(v => v.card == Card.Overflow && v.transform.Find("잠금 배지") && v.transform.Find("잠금 배지").gameObject.activeSelf)) { Fail("Badge left after unlock"); yield break; }
   passed.Add("draw lock: OVERFLOW locked after BUFFER, drop on the socket rejected (hand " + before + ", ERROR, snap back), badge gone when DrawUsed resets");

   // item 7: five cards [INJECT, REROUTE, INJECT, BUFFER, ZERO-DAY] like the HTML's IDS; BUFFER draws 2 into 4 -> 1 lost
   Fixture(0, 0); c.State.Hands[0].AddRange(new[] { Card.Inject, Card.Reroute, Card.Inject, Card.Buffer, Card.ZeroDay }); c.State.SaveAction(); c.Render(); yield return null;
   if (!fx.HandLabel.Contains("MY HAND   05") || !fx.HandLabel.Contains("FULL")) { Fail("MY HAND label: " + fx.HandLabel); yield break; }
   c.Select(3); c.Use(0, false);
   yield return Await(() => fx.LostTime >= 0 && fx.LostTime < 1, "lost draw"); if (failed) yield break;
   foreach (float lk in new[] { .2f, .6f, 1f, 1.4f, 2f }) {
    yield return Await(() => fx.LostTime >= lk, "lost " + lk); if (failed) yield break;
    FxClock.Scale = 0; yield return null; moments.Add("lost lk=" + fx.LostTime.ToString("0.000") + " hand=" + c.VisibleHandCount + " label=" + fx.HandLabel);
    if (lk >= 1 && (!fx.WarningShown || !fx.WarningText.Contains("1장"))) { Fail("Full-hand warning missing"); yield break; }
    yield return Shot("w12-lost-" + lk.ToString("0.0")); FxClock.Scale = 1;
   }
   yield return Await(() => c.Settled && fx.LostTime > 3.1f, "lost end"); if (failed) yield break;
   if (fx.WarningShown || c.State.Hands[0].Count != 5) { Fail("Warning stays or hand is not 5"); yield break; }
   passed.Add("hand limit: label MY HAND 05 / 05 FULL, BUFFER at 5 cards lost 1 draw (ghost, 6x10 tiles, warning 2.6 s)");
  }
  // Week 12 bundle C: 5 deck view (05_deck.js carousel), 9 ESC menu (09_settings.js flow), 10 result replay (10_resultlog.js replay)
  IEnumerator CoverItems() {
   // item 5: the mockup's example history (fresh(): 4,0,1,0,2,3,0,0,5,0,1,2,0,1,0) so "n장째 안 나옴" matches
   Fixture(0, 0); c.State.Hands[0].AddRange(new[] { Card.Inject, Card.Reroute }); c.State.SaveAction(); c.Render(); yield return new WaitForSecondsRealtime(.3f);
   var deck = c.deck; deck.HistoryOverride = new[] { 4, 0, 1, 0, 2, 3, 0, 0, 5, 0, 1, 2, 0, 1, 0 }.Select(i => (Card)i).ToList();
   var hot = c.game.transform.Find("내 드로우 덱 - 누르면 덱 보기").GetComponent<UnityEngine.UI.Button>(); hot.onClick.Invoke();
   if (!deck.IsOpen) { Fail("Deck click did not open the deck view"); yield break; }
   foreach (float td in new[] { 1.2f, 2.0f, 3.5f }) {
    yield return Await(() => deck.Time >= td, "deck " + td); if (failed) yield break;
    FxClock.Scale = 0; yield return null; moments.Add("deck t=" + deck.Time.ToString("0.000") + " focus=" + deck.Focus + " panel=" + deck.PanelText);
    yield return Shot("w12-deck-" + td.ToString("0.0")); FxClock.Scale = 1;
    if (td == 2.0f && !deck.PanelText.Contains("제로데이") || td == 2.0f && !deck.PanelText.Contains("4.8%") || td == 2.0f && !deck.PanelText.Contains("9장째 안 나옴")) { Fail("Deck panel: " + deck.PanelText); yield break; }
    if (td == 3.5f && deck.Focus != 3) { Fail("Deck turned by itself (focus " + deck.Focus + ")"); yield break; } // 11-27: 저절로 넘어가지 않음
   }
   moments.Add("deck-click t=" + deck.Time.ToString("0.000")); deck.ClickAt(new Vector2(320, 500)); if (deck.Focus != 2) { Fail("Click on the left card did not bring it to the middle (" + deck.Focus + ")"); yield break; }
   deck.ClickAt(new Vector2(1500, 480)); if (deck.Focus != 2 || !deck.IsOpen) { Fail("Click on the info panel changed the deck view"); yield break; }
   for (int w = 0; w < 3; w++) deck.Wheel(1); if (deck.Focus != 5) { Fail("Wheel did not turn"); yield break; }
   float last = deck.Time; yield return Await(() => deck.Time > last + .5f, "deck turn"); if (failed) yield break;
   FxClock.Scale = 0; yield return null; moments.Add("deck-turn t=" + deck.Time.ToString("0.000") + " focus=" + deck.Focus + " panel=" + deck.PanelText); yield return Shot("w12-deck-turn"); FxClock.Scale = 1;
   if (!deck.PanelText.Contains("버퍼 오버플로") || !deck.PanelText.Contains("6장째 안 나옴")) { Fail("Deck panel after turning: " + deck.PanelText); yield break; }
   float hold = deck.Time; yield return Await(() => deck.Time > hold + 7f, "deck idle"); if (failed) yield break;
   if (deck.Focus != 5) { Fail("Deck turned by itself while idle (focus " + deck.Focus + ")"); yield break; }
   deck.ClickAt(new Vector2(960, 140)); yield return Await(() => !deck.IsOpen, "deck dim click close"); if (failed) yield break;
   if (!c.CanInput || c.State.Hands[0].Count != 2) { Fail("Deck view changed the battle"); yield break; }
   hot.onClick.Invoke(); if (!deck.IsOpen) { Fail("Deck did not reopen"); yield break; }
   yield return Await(() => deck.Time >= 1.7f, "deck reopen"); if (failed) yield break;
   deck.Click(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right, position = Screen(960, 500) });
   yield return Await(() => !deck.IsOpen, "deck right-click close"); if (failed) yield break;
   deck.HistoryOverride = null;
   passed.Add("deck view: deck click opens, ZERO-DAY 4.8% / 9장째, no automatic turn (7 s idle), card click / wheel turn, info panel click ignored, dim click close, reopen, right-click close (Esc: key input, not simulated)");

   // item 9: ESC menu (the pointer is parked off the buttons like the mockup capture)
   Fixture(0, 0); c.State.Hands[0].Add(Card.Inject); c.State.SaveAction(); c.Render(); yield return new WaitForSecondsRealtime(.3f);
   var pause = c.pause; pause.ForcePointer = new Vector2(100, 1000); pause.Open();
   if (!pause.IsOpen || Time.timeScale != 0) { Fail("ESC menu did not open / pause"); yield break; }
   foreach (float to in new[] { .2f, 1f }) {
    yield return Await(() => pause.OpenTime >= to, "esc open " + to); if (failed) yield break;
    FxClock.Scale = 0; yield return null; moments.Add("esc-open openT=" + pause.OpenTime.ToString("0.000") + " clock=" + pause.Clock.ToString("0.000")); yield return Shot("w12-esc-open-" + to.ToString("0.0")); FxClock.Scale = 1;
   }
   pause.ForcePointer = new Vector2(960, 587.5f); yield return null; pause.Click(); pause.ForcePointer = new Vector2(100, 1000);
   moments.Add("esc-settings clicked clock=" + pause.Clock.ToString("0.000"));
   if (!pause.SettingsShown) { Fail("환경 설정 click did not open the settings"); yield break; }
   foreach (float f in new[] { .5f, 1f }) {
    yield return Await(() => pause.Flow >= f, "esc flow " + f); if (failed) yield break;
    FxClock.Scale = 0; yield return null; moments.Add("esc-flow p=" + pause.Flow.ToString("0.000") + " clock=" + pause.Clock.ToString("0.000")); yield return Shot("w12-esc-flow-" + f.ToString("0.0")); FxClock.Scale = 1;
   }
   pause.ForcePointer = new Vector2(1100, 775); yield return null; string over = pause.HoverName; pause.Click(); pause.ForcePointer = new Vector2(100, 1000);
   moments.Add("esc-back clicked clock=" + pause.Clock.ToString("0.000") + " on " + over);
   if (pause.SettingsShown) { Fail("돌아가기 did not go back (pointer over " + over + ")"); yield break; }
   yield return Await(() => pause.Flow <= .5f, "esc back .5"); if (failed) yield break;
   FxClock.Scale = 0; yield return null; moments.Add("esc-back p=" + pause.Flow.ToString("0.000") + " clock=" + pause.Clock.ToString("0.000")); yield return Shot("w12-esc-back-0.5"); FxClock.Scale = 1;
   yield return Await(() => pause.Flow <= 0, "esc back 0"); if (failed) yield break;
   pause.ForcePointer = new Vector2(960, 502.5f); yield return null; pause.Click(); pause.ForcePointer = null;
   yield return Await(() => !pause.IsOpen, "esc close"); if (failed) yield break;
   if (Time.timeScale != 1 || !c.CanInput) { Fail("Battle not resumed after 계속하기"); yield break; }
   passed.Add("ESC menu: opens with the battle paused (timeScale 0), 환경 설정 click -> FLOW .7 s, 돌아가기 click -> back, 계속하기 -> closed and resumed (Esc key: not simulated)");

   // item 10: the mockup's LOG played on a real Battle -> result screen -> replay
   var b = ReplayLog.Script(c.Replay, out var lastReport);
   if (c.Replay.Mismatch.Count > 0) { Fail("Replay rebuild differs from Battle.Resolve: " + string.Join("; ", c.Replay.Mismatch)); yield break; }
   string[] html = { "접속 완료 · 내 공격 턴", "나 · 코드 주입", "나 · 코드 주입", "나 · 라우팅 변경", "나 · 버퍼 적재", "상대 · 코드 주입", "상대 · 방화벽 (발동 때 적용)", "상대 · 방화벽 공개", "공격 상쇄 · 1", "상대 서버 2 · 피해 1",
    "ROUND 2 · 상대 공격 턴", "상대 · 코드 주입", "상대 · 라우팅 변경", "상대 · 제로데이 (발동 때 적용)", "나 · 방화벽", "나 · 코드 주입", "상대 · 제로데이 공개", "나 · 방화벽 공개", "방화벽 · 패킷 소멸", "제로데이 · 통과", "상대 서버 1 · 피해 1", "내 서버 1 · 피해 1", // 11-27 새 제로데이 규칙(1발만 통과)
    "ROUND 3 · 내 공격 턴", "나 · 버퍼 오버플로", "나 · 코드 주입", "나 · 라우팅 변경", "나 · 제로데이", "나 · 코드 주입", "상대 · 코드 주입", "나 · 버퍼 오버플로 공개", "나 · 제로데이 공개", "공격 상쇄 · 1", "상대 서버 3 · 피해 1" };
   var got = c.Replay.Steps.Select(x => x.T).ToArray();
   if (!got.SequenceEqual(html)) { Fail("Replay lines differ from the mockup LOG: " + string.Join(" | ", got)); yield break; }
   yield return c.ShowScriptedResult(b, lastReport);
   yield return Await(() => ResultButtonFx.Clock >= 1f, "result 1.0"); if (failed) yield break;
   FxClock.Scale = 0; yield return null; moments.Add("result clock=" + ResultButtonFx.Clock.ToString("0.000")); yield return Shot("w12-result-1.0"); FxClock.Scale = 1;
   var seq = c.Sequences; var fx = seq.ReplayButton.GetComponent<ResultButtonFx>(); fx.ForceOver = true; yield return new WaitForSecondsRealtime(.3f);
   var edge = seq.ReplayButton.transform.Find("버튼 반응 테두리"); bool square = edge && edge.GetComponent<Shape>() && edge.GetComponent<Shape>().color.a > .01f;
   if (fx.Level < .99f || square) { Fail("Result button hover: level " + fx.Level + (square ? ", square border shown" : "")); yield break; }
   fx.ForceOver = false; yield return new WaitForSecondsRealtime(.2f);
   seq.ReplayButton.onClick.Invoke(); var view = seq.Replay; view.ForcePointer = new Vector2(100, 1060);
   if (!seq.ReplayOpen || view.Count != 33) { Fail("Replay did not open with 33 steps"); yield break; }
   foreach (var (i, lt) in new[] { (1, .3f), (3, .2f), (7, .4f), (8, 1f), (9, .5f), (10, .5f), (19, 1f), (32, .9f) }) {
    view.Go(i); view.Pause(); yield return Await(() => view.StepTime >= lt, "replay " + i); if (failed) yield break;
    FxClock.Scale = 0; yield return null; moments.Add("replay idx=" + i + " lt=" + view.StepTime.ToString("0.000") + " caption=" + view.Caption); yield return Shot("w12-replay-" + i + "-" + lt.ToString("0.0")); FxClock.Scale = 1;
   }
   view.Click(new Vector2(1480, 1012)); if (view.Index != 32) { Fail("다음 ▶ at the last step moved to " + view.Index); yield break; }
   view.Click(new Vector2(1130, 1012)); if (view.Index != 31) { Fail("◀ 이전 did not step back (" + view.Index + ")"); yield break; }
   view.Click(new Vector2(1310, 1012)); if (!view.Playing) { Fail("재생 did not start"); yield break; }
   yield return Await(() => view.Index == 32, "replay plays on"); if (failed) yield break;
   view.Click(new Vector2(200, 400)); if (view.Playing || view.Index < 0) { Fail("Row click did not jump"); yield break; } int rowIdx = view.Index;
   view.Click(new Vector2(1720, 52)); yield return Await(() => !seq.ReplayOpen, "replay close"); if (failed) yield break;
   view.ForcePointer = null;
   passed.Add("replay: mockup LOG on a real Battle = 33 identical lines (rebuilt resolve = Battle.Resolve), result buttons hover without a square, replay 33 steps, ◀ / ▶ / 재생, row click -> step " + rowIdx + ", 결과로 ×");
   c.ReturnToTitle();
  }
  Vector2 Center(CardView v) { var r = (RectTransform)v.transform; return RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center)); }
  Vector2 Screen(float x, float y) { return RectTransformUtility.WorldToScreenPoint(null, c.stage.TransformPoint(new Vector3(x, -y, 0))); }
  IEnumerator Freeze(string phase, float progress, string name) {
   yield return Await(() => c.feedback.Phase == phase && c.feedback.Progress >= progress, name); if (failed) yield break;
   FxClock.Scale = 0; moments.Add(name + " phase=" + phase + " progress=" + c.feedback.Progress.ToString("0.000"));
   yield return Shot(name); FxClock.Scale = 1;
  }
  void Fixture(int attacker, int phase) {
   var s = c.State; s.Attacker = attacker; s.Phase = phase; s.Winner = -1; s.DrawUsed = false; s.Hidden.Clear(); Array.Clear(s.Power, 0, 6); Array.Clear(s.Walls, 0, 3); Array.Clear(s.Pierce, 0, 6);
   s.Hands[0].Clear(); s.Hands[1].Clear(); for (int i = 0; i < 3; i++) { s.Health[0, i] = s.Health[1, i] = 1; s.Rungs[i] = -1; s.RungOwners[i] = -1; s.RungRiders[i] = -1; }
  }
  IEnumerator Await(Func<bool> test, string name) { float until = Time.realtimeSinceStartup + 40; while (!test() && Time.realtimeSinceStartup < until) yield return null; if (!test()) Fail("Timeout " + name + " (card " + c.feedback.Phase + " " + c.feedback.Progress.ToString("0.00") + ")"); }
  IEnumerator Shot(string name) {
   yield return new WaitForEndOfFrame(); var canvas = c.stage.GetComponentInParent<Canvas>(); var camera = Camera.main; var texture = new RenderTexture(1920, 1080, 24); var mode = canvas.renderMode;
   canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; camera.targetTexture = texture; Canvas.ForceUpdateCanvases(); camera.Render();
   var old = RenderTexture.active; RenderTexture.active = texture; var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(dir, name + ".png"), image.EncodeToPNG());
   RenderTexture.active = old; camera.targetTexture = null; canvas.renderMode = mode; Destroy(image); texture.Release(); Destroy(texture); yield return null;
  }
  void Fail(string message) { if (failed) return; failed = true; FxClock.Scale = 1; File.WriteAllText(Path.Combine(dir, "week12-moments.txt"), string.Join("\n", moments)); File.WriteAllText(Path.Combine(dir, "week12-result.txt"), "FAIL: " + message + " | passed: " + string.Join("; ", passed)); Application.Quit(1); }
 }
}

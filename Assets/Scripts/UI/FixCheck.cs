using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Breach.UI
{
    // Only explicit --fix-check runs these fixtures (2026-11-16 playtest fixes: skip, visible bot actions,
    // one-step undo, draw lock, rung card targets). Never runs during normal play.
    public sealed class FixCheck : MonoBehaviour
    {
        BattleController c; string dir; bool failed; readonly System.Collections.Generic.List<string> passed = new System.Collections.Generic.List<string>();
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--fix-check"); if (at < 0 || at + 1 >= args.Length) yield break;
            dir = args[at + 1]; Directory.CreateDirectory(dir); Application.runInBackground = true; c = GetComponent<BattleController>();
            yield return null; c.animationSpeed = 4; c.startButton.onClick.Invoke();
            yield return Await(() => c.CanInput, "start"); if (failed) yield break; c.animationSpeed = 1;

            // 7. Rung card: channel targets are off, a channel preview draws nothing, a rung preview draws.
            Reset(0, 0); c.State.Hands[0].Add(Card.Reroute); c.State.SaveAction(); c.Render(); yield return null;
            c.Select(0); yield return null;
            if (c.laneTargets.Any(b => b.gameObject.activeSelf)) { Fail("Channel targets active for rung card"); yield break; }
            if (!c.rungTargets.All(b => b.gameObject.activeSelf)) { Fail("Rung targets missing"); yield break; }
            c.Preview(1, false); if (c.paths.childCount != 0) { Fail("Rung card previewed a vertical channel path"); yield break; }
            c.Preview(2, true); if (c.paths.childCount == 0) { Fail("Rung preview missing"); yield break; }
            yield return Shot("01-rung-targets"); c.ClearPreview();
            // Drop over the middle vertical line between two rung slots: must not be used.
            var held = c.hand.GetComponentsInChildren<CardView>().First(v => v.index == 0);
            var drag = new PointerEventData(EventSystem.current) { position = Screen(held.transform.TransformPoint(new Vector3(50, -50, 0))) };
            held.OnBeginDrag(drag); drag.position = Screen(c.stage.TransformPoint(new Vector3(982, -497, 0))); held.OnDrag(drag);
            if (c.paths.childCount != 0) { Fail("Dragging rung card over vertical line drew a path"); yield break; }
            held.OnEndDrag(drag); yield return Await(() => c.CanInput, "rung return"); if (failed) yield break;
            if (c.State.Hands[0].Count != 1 || c.State.Rungs.Any(r => r >= 0)) { Fail("Rung card was used on a vertical line"); yield break; }
            c.Select(0); c.Use(1, false); yield return null; if (c.State.Hands[0].Count != 1) { Fail("Rung card accepted a channel target"); yield break; }
            c.Select(0); Reset(0, 0); c.State.Hands[0].Add(Card.Inject); c.State.SaveAction(); c.Render(); c.Select(0); yield return null;
            if (!c.laneTargets.All(b => b.gameObject.activeSelf)) { Fail("Channel targets not restored for inject"); yield break; }
            c.Select(0);
            passed.Add("rung card ignores vertical channel targets (hover, drag, drop, click)");

            // 5/6. One action per undo; a draw locks itself and everything before it.
            Reset(0, 0); c.State.Hands[0].Add(Card.Inject); c.State.Hands[0].Add(Card.Reroute); c.State.Hands[0].Add(Card.Inject); c.State.SaveAction(); c.Render(); yield return null;
            c.Select(0); c.Use(0, false); yield return Await(() => c.CanInput, "play 1"); if (failed) yield break;
            c.Select(0); c.Use(0, true); yield return Await(() => c.CanInput, "play 2"); if (failed) yield break;
            c.Select(0); c.Use(2, false); yield return Await(() => c.CanInput, "play 3"); if (failed) yield break;
            if (!c.undoButton.interactable) { Fail("Undo disabled after plays"); yield break; }
            c.undoButton.onClick.Invoke(); yield return null;
            if (c.State.Power[0, 2] != 0 || c.State.Rungs[0] != 0 || c.State.Power[0, 0] != 1 || c.State.Hands[0].Count != 1) { Fail("Undo did more than one step"); yield break; }
            yield return Shot("02-undo-one-step");
            c.undoButton.onClick.Invoke(); yield return null; if (c.State.Rungs[0] != -1 || c.State.Power[0, 0] != 1) { Fail("Second undo"); yield break; }
            c.undoButton.onClick.Invoke(); yield return null; if (c.State.Power[0, 0] != 0 || c.State.Hands[0].Count != 3 || c.undoButton.interactable) { Fail("Undo did not stop at turn start"); yield break; }
            Reset(0, 0); c.State.Hands[0].Add(Card.Inject); c.State.Hands[0].Add(Card.Buffer); c.State.SaveAction(); c.Render(); yield return null;
            c.Select(0); c.Use(0, false); yield return Await(() => c.CanInput, "inject before draw"); if (failed) yield break;
            c.Select(0); c.Use(0, false); yield return Await(() => c.CanInput, "buffer draw"); if (failed) yield break;
            if (c.State.Hands[0].Count != 2) { Fail("Buffer did not draw at once"); yield break; }
            if (c.undoButton.interactable) { Fail("Undo still offered after a draw"); yield break; }
            c.undoButton.onClick.Invoke(); yield return null; if (c.State.Hands[0].Count != 2 || c.State.Power[0, 0] != 1) { Fail("Draw was undone"); yield break; }
            yield return Shot("03-draw-locks-undo");
            Reset(0, 0); c.State.Hands[0].Add(Card.Overflow); c.State.SaveAction(); c.Render(); yield return null;
            c.Select(0); c.Use(0, false); yield return Await(() => c.CanInput, "overflow"); if (failed) yield break;
            if (c.State.Hands[0].Count != 4 || c.State.Hidden.Count != 1 || c.undoButton.interactable) { Fail("Overflow did not draw at once or stayed undoable"); yield break; }
            passed.Add("undo one step at a time, stops at turn start, buffer/overflow draw at once and cannot be undone");

            // 1. Skip my own card transfer mid-way: ends in the applied state with effects cleaned up.
            Reset(0, 0); c.State.Hands[0].Add(Card.Inject); c.State.SaveAction(); c.Render(); yield return null;
            c.Select(0); c.Use(1, false);
            yield return Await(() => c.feedback.Phase == "fragment", "fragment"); if (failed) yield break;
            c.SkipPresentation(); int frames = 0; while (!c.CanInput && frames < 30) { frames++; yield return null; }
            if (!c.CanInput) { Fail("Skip did not finish card transfer within 30 frames"); yield break; }
            yield return null;
            if (c.State.Power[0, 1] != 1 || c.fx.childCount != 0 || c.feedback.Phase != "idle" || c.Skipping) { Fail("Card skip left half state"); yield break; }
            passed.Add("skip card transfer in " + frames + " frames, fx cleared");

            // 2a. Bot attack turn: each action is visible on the board while the bot is still acting.
            Reset(1, 0); c.State.Hands[1].Add(Card.Inject); c.State.SaveAction(); Private("CapturePublic"); c.Render(); yield return null;
            StartCoroutine((IEnumerator)Private("BotFlow"));
            yield return Await(() => c.feedback.Phase == "applied", "enemy attack applied"); if (failed) yield break;
            if (c.State.Phase != 0 || c.State.Actor != 1) { Fail("Bot turn already ended"); yield break; }
            int lane = Enumerable.Range(0, 3).First(i => c.State.Power[1, i] > 0);
            if (c.servers.First(v => v.owner == 1 && v.channel == lane).number.text != "1") { Fail("Bot attack not shown while acting"); yield break; }
            if (!c.statusLabel.text.Contains("코드 주입")) { Fail("Bot action label missing"); yield break; }
            yield return Shot("04-enemy-attack-visible");
            yield return Await(() => c.CanInput, "player defense"); if (failed) yield break;
            passed.Add("bot attack-turn inject shown on server " + (lane + 1) + " before the bot ends its action");

            // 2b. Bot defense turn: card back moves, target and kind stay hidden until resolve.
            Reset(0, 1); c.State.Power[0, 1] = 1; c.State.Hands[1].Add(Card.Inject); c.State.SaveAction(); Private("CapturePublic"); c.Render(); yield return null;
            string logBefore = c.logText.text;
            StartCoroutine((IEnumerator)Private("BotFlow"));
            yield return Await(() => c.statusLabel.text.Contains("비공개 행동"), "enemy defense hidden"); if (failed) yield break;
            if (c.State.Phase != 1) { Fail("Bot defense already resolved"); yield break; }
            if (c.servers.Any(v => v.owner == 1 && v.number.text != "0")) { Fail("Bot defense revealed before resolve"); yield break; }
            if (c.logText.text != logBefore) { Fail("Bot defense card named before resolve"); yield break; }
            yield return Shot("05-enemy-defense-hidden");

            // 1. Skip the whole resolve flight: camera, CRT and effects restore; control returns to me.
            yield return Await(() => c.presentation.Phase == "flight", "resolve flight"); if (failed) yield break;
            yield return Shot("06-before-skip");
            c.SkipPresentation(); frames = 0; float started = Time.realtimeSinceStartup;
            // One skip ends the resolve and stops at the next round caption.
            while (c.feedback.Phase != "turn" && !c.result.activeSelf && frames < 120) { frames++; yield return null; }
            if (c.feedback.Phase != "turn" && !c.result.activeSelf) { Fail("Skip did not reach the next turn caption"); yield break; }
            if (c.Skipping) { Fail("Skip did not stop at the turn caption"); yield break; }
            if (c.presentation.CameraZoom != 1 || c.presentation.Phase != "idle" || c.presentation.PeripheralAlpha != 1) { Fail("Skip left camera focus"); yield break; }
            foreach (var server in c.servers) if (server.offline.activeSelf != (c.State.Health[server.owner, server.channel] <= 0)) { Fail("Server view does not match state after skip"); yield break; }
            yield return Shot("07-after-skip-next-caption");
            int skipFrames = frames; float skipSeconds = Time.realtimeSinceStartup - started; int presses = 0;
            // Pressing again on every frame fast-forwards the caption and the bot's next turn until input returns.
            float until = Time.realtimeSinceStartup + 40; while (!c.CanInput && !c.result.activeSelf && Time.realtimeSinceStartup < until) { if (!c.Skipping) { c.SkipPresentation(); presses++; } yield return null; }
            if (!c.CanInput && !c.result.activeSelf) { Fail("Repeated skip did not return control"); yield break; }
            yield return null;
            var crt = FindFirstObjectByType<CrtOverlay>();
            if (c.presentation.CameraZoom != 1 || c.presentation.Phase != "idle" || c.fx.childCount != 0 || (crt && crt.Burst != 0) || c.Skipping || c.feedback.Phase != "idle") { Fail("Skip left presentation state"); yield break; }
            yield return Shot("08-after-skip-control");
            passed.Add("bot defense hidden until resolve; one skip ended the resolve flight in " + skipFrames + " frames (" + skipSeconds.ToString("0.00") + "s) and stopped at the next caption; " + presses + " further presses reached player input; camera/CRT/fx restored, servers match state");

            File.WriteAllText(Path.Combine(dir, "fix-result.txt"), "PASS: " + string.Join("; ", passed)); Application.Quit(0);
        }
        void Reset(int attacker, int phase)
        {
            var s = c.State; s.Attacker = attacker; s.Phase = phase; s.Winner = -1; s.Hidden.Clear(); Array.Clear(s.Power, 0, 6); Array.Clear(s.Walls, 0, 3); Array.Clear(s.Pierce, 0, 6);
            s.Hands[0].Clear(); s.Hands[1].Clear(); for (int i = 0; i < 3; i++) { s.Health[0, i] = s.Health[1, i] = 1; s.Rungs[i] = -1; s.RungOwners[i] = -1; }
        }
        static Vector2 Screen(Vector3 world) { return RectTransformUtility.WorldToScreenPoint(null, world); }
        object Private(string name, params object[] args) => typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c, args);
        IEnumerator Await(Func<bool> test, string name) { float until = Time.realtimeSinceStartup + 40; while (!test() && Time.realtimeSinceStartup < until) yield return null; if (!test()) Fail("Timeout " + name); }
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame(); var canvas = c.stage.GetComponentInParent<Canvas>(); var camera = Camera.main; var texture = new RenderTexture(1600, 900, 24); var mode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; camera.targetTexture = texture; Canvas.ForceUpdateCanvases(); camera.Render();
            var old = RenderTexture.active; RenderTexture.active = texture; var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(dir, name + ".png"), image.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; canvas.renderMode = mode; Destroy(image); texture.Release(); Destroy(texture); yield return null;
        }
        void Fail(string message) { if (failed) return; failed = true; File.WriteAllText(Path.Combine(dir, "fix-result.txt"), "FAIL: " + message + " | passed: " + string.Join("; ", passed)); Application.Quit(1); }
    }
}

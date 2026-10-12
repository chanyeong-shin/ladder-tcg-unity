using System;
using LadderBattle.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LadderBattle.EditorTools
{
    /// <summary>
    /// 창 없이 Play 를 눌러 한 판을 끝까지 돌려 본다.
    ///
    ///   Unity.exe -batchmode -projectPath . -executeMethod LadderBattle.EditorTools.PlayTest.CLI_Play
    ///
    /// **-quit 를 붙이면 안 된다.** 붙이면 Play 에 들어가기 전에 에디터가 닫힌다.
    /// 끝나면 이쪽에서 스스로 닫는다 (성공 0 · 실패 1).
    ///
    /// 함정 — Play 에 들어갈 때 유니티는 스크립트를 통째로 다시 읽는다(도메인 재로드).
    /// 그때 static 값과 EditorApplication.update 에 걸어 둔 것이 **전부 날아간다.**
    /// 그래서 "검사 중"이라는 표시를 SessionState 에 적어 두고, 다시 읽힌 뒤에
    /// InitializeOnLoadMethod 로 스스로 다시 걸린다. 처음에 이걸 안 해서 영영 멈춰 있었다.
    /// </summary>
    public static class PlayTest
    {
        const string Running = "LadderBattle.PlayTest.running";
        const string Deadline = "LadderBattle.PlayTest.deadline";

        static AutoPlay _runner;

        public static void CLI_Play()
        {
            Debug.Log("[검사] 시작 — 씬을 연다");
            EditorSceneManager.OpenScene(BuildScreen.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Running, true);
            SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + 540f);
            걸기();
            Debug.Log("[검사] Play 로 들어간다");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>도메인이 다시 읽힌 뒤에도 검사가 이어지게 한다.</summary>
        [InitializeOnLoadMethod]
        static void 재연결()
        {
            if (!SessionState.GetBool(Running, false)) return;
            Debug.Log("[검사] 도메인 재로드 후 다시 붙었다");
            걸기();
        }

        static void 걸기()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) { EditorApplication.update -= Tick; return; }

            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Deadline, 0f))
            { 끝내기(1, "시간 초과"); return; }

            if (!EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode == false) return;

            if (_runner == null)
            {
                _runner = UnityEngine.Object.FindFirstObjectByType<AutoPlay>();
                if (_runner == null)
                {
                    var go = new GameObject("자동 플레이");
                    _runner = go.AddComponent<AutoPlay>();
                    // -noFx 를 붙이면 연출을 끄고 숫자만 본다. 평소에는 연출까지 전부 지나간다
                    _runner.연출생략 = 인자있나("-noFx");
                    _runner.눌러서낸다 = 인자있나("-byClick");
                    Debug.Log($"[검사] 자동 플레이를 붙였다 (연출생략={_runner.연출생략} "
                              + $"눌러서={_runner.눌러서낸다})");
                }
                return;
            }
            if (!_runner.끝났다) return;

            string report = _runner.보고 ?? "";
            bool ok = report.Contains("정상 종료");
            Debug.Log("[검사] 보고\n" + report);
            끝내기(ok ? 0 : 1, ok ? "정상 종료" : "실패");
        }

        static bool 인자있나(string key)
        {
            foreach (var a in Environment.GetCommandLineArgs()) if (a == key) return true;
            return false;
        }

        static void 끝내기(int code, string why)
        {
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            Debug.Log($"[검사] 마침 ({code}) · {why}");
            try { if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); } catch (Exception) { }
            EditorApplication.Exit(code);
        }
    }
}

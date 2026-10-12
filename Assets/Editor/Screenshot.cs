using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LadderBattle.EditorTools
{
    /// <summary>
    /// 씬을 1920x1080 그림으로 찍는다.
    ///
    /// 왜 필요한가 — 만든 화면이 시안과 같은지 **눈으로** 봐야 한다. 컴파일이
    /// 통과했다는 것은 글자가 제자리에 있다는 뜻이 아니다. 창을 띄우지 않고도
    /// 찍을 수 있어서, 고치고 찍고 비교하는 일을 명령줄에서 반복할 수 있다.
    ///
    /// 배치모드: Unity.exe -quit -batchmode -executeMethod ...Screenshot.CLI_Shot
    ///           (-nographics 는 넣으면 안 된다. 그리질 못한다)
    /// </summary>
    public static class Screenshot
    {
        [MenuItem("Tools/사다리전투/화면 사진 찍기", priority = 40)]
        public static void Shot() => Capture(DefaultPath());

        public static void CLI_Shot()
        {
            string path = Arg("-shotPath") ?? DefaultPath();
            string scene = Arg("-shotScene") ?? BuildScreen.ScenePath;
            if (File.Exists(scene)) EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            Capture(path);
        }

        public static void Capture(string path, int w = 1920, int h = 1080)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[사진] Main Camera 가 없다.");
                return;
            }

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
            };
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;

            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log($"[사진] {path}");
        }

        static string DefaultPath()
            => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "화면.png");

        static string Arg(string key)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
            return null;
        }
    }
}

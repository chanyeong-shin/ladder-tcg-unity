using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LadderBattle.EditorTools
{
    /// <summary>
    /// 씬을 조립하는 에디터 명령.
    ///
    /// 유니티 메뉴에서 **Tools > 사다리전투 > 화면 만들기** 를 누르면
    /// `Assets/Scenes/Game.unity` 가 시안 그대로 다시 세워진다.
    /// 만들어진 것은 평범한 게임오브젝트라서 그 뒤로는 손으로 고쳐도 된다.
    /// 다시 누르면 처음부터 새로 만든다 — 손으로 고친 것은 사라진다.
    /// </summary>
    public static class BuildScreen
    {
        public const string LayoutDir = "Assets/Figma/배치표";
        public const string ScenePath = "Assets/Scenes/Game.unity";

        /// <summary>플레이 화면의 바탕이 되는 시안. 칩소켓 1 = 아군 칩, 내 턴.</summary>
        public const string PlayLayout = LayoutDir + "/17_170.layout.json";

        [MenuItem("Tools/사다리전투/화면 만들기", priority = 0)]
        public static void BuildAndSave()
        {
            var scene = Compose();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[화면] {ScenePath} 를 시안대로 다시 세웠다.");
        }

        [MenuItem("Tools/사다리전투/글꼴 에셋 만들기", priority = 20)]
        public static void BuildFonts() => FontAssets.BuildAll();

        /// <summary>
        /// true 면 게임 쪽을 묶지 않고 **시안 그대로만** 세운다.
        /// 피그마와 몇 픽셀이나 다른지 재려면 움직이는 부분이 없어야 비교가 성립한다.
        /// </summary>
        public static bool 시안만;

        /// <summary>씬을 새로 만들고 시안을 올린다. 저장은 부르는 쪽이 한다.</summary>
        public static Scene Compose()
        {
            FontAssets.BuildAll();
            UiSprites.ClearCache();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FigmaImport.Rgba("#0a0c10", 1f);
            cam.orthographic = true;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            var canvasGo = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 화면 전체를 덮는 틀 하나를 두고 그 왼쪽 위를 0,0 으로 쓴다.
            // 이러면 시안 좌표를 그대로 넣을 수 있고, 창 비율이 달라도 가운데에 머문다
            var screen = new GameObject("화면", typeof(RectTransform));
            var srt = (RectTransform)screen.transform;
            srt.SetParent(canvasGo.transform, false);
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0f, 1f);
            srt.sizeDelta = new Vector2(1920f, 1080f);
            srt.anchoredPosition = new Vector2(-960f, 540f);

            var mock = FigmaImport.Build(PlayLayout, srt);
            if (mock != null && !시안만) WireGame.묶기(mock, srt);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return scene;
        }

        // ── 명령줄에서 부르는 자리 ──────────────────────────────

        /// <summary>배치모드: Unity.exe -quit -batchmode -executeMethod ...CLI_Build</summary>
        public static void CLI_Build()
        {
            BuildAndSave();
            Debug.Log("[화면] CLI_Build 끝");
        }

        /// <summary>
        /// 시안만 세워서 사진을 찍는다. 저장하지 않으므로 Game.unity 는 건드리지 않는다.
        /// `_검증/화면대조.py` 로 피그마 내보내기와 견주는 데 쓴다.
        /// </summary>
        public static void CLI_Mock()
        {
            시안만 = true;
            Compose();
            string path = Arg("-shotPath") ?? "Logs/시안만.png";
            Screenshot.Capture(path);
            시안만 = false;
            Debug.Log("[화면] CLI_Mock 끝");
        }

        static string Arg(string key)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
            return null;
        }
    }
}

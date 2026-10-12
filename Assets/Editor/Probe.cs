using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LadderBattle.EditorTools
{
    /// <summary>
    /// 화면의 한 점 위에 무엇이 겹쳐 있는지 그리는 순서대로 알려 준다.
    ///
    /// 왜 필요한가 — "여기가 시안보다 밝다" 까지는 그림 대조로 알 수 있지만
    /// **무엇 때문에** 밝은지는 알 수 없다. 겹친 것을 눈으로 찾다 보면 엉뚱한 데를
    /// 고치게 된다. 좌표를 넣으면 그 자리의 Image 와 글자를 전부 뽑아 준다.
    ///
    /// 배치모드:
    ///   Unity.exe -quit -batchmode -projectPath . \
    ///     -executeMethod LadderBattle.EditorTools.Probe.CLI_Probe -probe 222,470 -probe 190,500
    /// </summary>
    public static class Probe
    {
        public static void CLI_Probe()
        {
            EditorSceneManager.OpenScene(BuildScreen.ScenePath, OpenSceneMode.Single);
            foreach (var p in Points()) Report(p);
        }

        static List<Vector2> Points()
        {
            var list = new List<Vector2>();
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] != "-probe") continue;
                var bits = a[i + 1].Split(',');
                if (bits.Length == 2 &&
                    float.TryParse(bits[0], out float x) && float.TryParse(bits[1], out float y))
                    list.Add(new Vector2(x, y));
            }
            return list;
        }

        /// <summary>시안 좌표(왼쪽 위 0,0) 로 받는다.</summary>
        public static void Report(Vector2 at)
        {
            var screen = GameObject.Find("화면");
            if (screen == null) { Debug.LogError("[탐침] '화면' 틀을 못 찾았다."); return; }

            var sb = new StringBuilder();
            sb.AppendLine($"[탐침] {at.x}, {at.y} — 뒤에서 앞 순서");
            int n = 0;
            foreach (var g in screen.GetComponentsInChildren<Graphic>(true))
            {
                var rt = (RectTransform)g.transform;
                Vector2 local = Where(screen.transform, rt, at);
                var r = rt.rect;
                if (local.x < r.xMin || local.x > r.xMax || local.y < r.yMin || local.y > r.yMax) continue;

                var c = g.color;
                string what = g is TextMeshProUGUI t
                    ? $"글자 \"{Short(t.text)}\""
                    : (g is Image im ? $"Image {(im.sprite ? im.sprite.name : "없음")} / {im.type}" : g.GetType().Name);
                sb.AppendLine($"  {++n,2}. {Path(screen.transform, rt),-62} {what}");
                sb.AppendLine($"      색 #{ColorUtility.ToHtmlStringRGB(c)}  알파 {c.a:0.00}   크기 {r.width:0}x{r.height:0}");
            }
            if (n == 0) sb.AppendLine("  (아무것도 없다)");
            Debug.Log(sb.ToString());
        }

        /// <summary>'화면' 틀 기준 좌표를 그 오브젝트의 안쪽 좌표로 옮긴다.</summary>
        static Vector2 Where(Transform screen, RectTransform rt, Vector2 at)
        {
            // '화면' 의 피벗이 왼쪽 위이므로, 시안 좌표는 곧 (x, -y) 다
            Vector3 world = screen.TransformPoint(new Vector3(at.x, -at.y, 0f));
            return rt.InverseTransformPoint(world);
        }

        static string Path(Transform stop, Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != null && c != stop; c = c.parent) parts.Insert(0, c.name);
            return string.Join("/", parts);
        }

        static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " / ");
            return s.Length <= 24 ? s : s.Substring(0, 24) + "...";
        }
    }
}

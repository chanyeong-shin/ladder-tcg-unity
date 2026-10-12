using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LadderBattle.EditorTools
{
    /// <summary>
    /// 피그마 배치표를 읽어 **진짜 게임오브젝트**로 세운다.
    ///
    /// 지난 판(v1.0 이식본)은 화면을 실행할 때 코드로 지었다. 그래서 Hierarchy 에
    /// 볼 것이 없었고 Inspector 로 고칠 수도 없었다. 여기서 만든 것은 씬에 그대로
    /// 남는 오브젝트라서, 만든 뒤에는 유니티에서 끌어 옮기고 색을 바꿀 수 있다.
    ///
    /// 좌표는 시안과 같은 1920x1080 기준이고 왼쪽 위가 0,0 이다.
    /// </summary>
    public static class FigmaImport
    {
        /// <summary>방금 세운 화면에서 피그마 노드 ID 로 오브젝트를 찾는 표.</summary>
        public static readonly Dictionary<string, GameObject> ById = new Dictionary<string, GameObject>();

        /// <summary>
        /// 배치표에서 **한 묶음만** 세운다. 전환 화면 3장은 플레이 화면을 그대로 품고 있어서
        /// 통째로 세우면 같은 화면이 두 겹으로 쌓인다. 필요한 것은 그 위에 덮인 오버레이뿐이다.
        ///
        /// <paramref name="rootId"/> 가 그 묶음의 피그마 노드 ID 다.
        /// 세운 것은 <see cref="ById"/> 에 덧붙는다 (지우지 않는다).
        /// </summary>
        /// <param name="화면전체로">
        /// true 면 묶음 뿌리를 1920x1080 짜리로 깔고 그 안의 조각들에 **시안 절대 좌표**를 그대로 쓴다.
        /// 전환 오버레이는 가림막이 판만 덮기도 하고 화면 전체를 덮기도 해서,
        /// 묶음 크기에 좌표가 묶여 있으면 실행 중에 범위를 바꿀 수 없다.
        /// </param>
        public static GameObject BuildSubtree(string layoutPath, Transform parent, string rootId,
                                              bool 화면전체로 = false)
        {
            var frame = 배치표읽기(layoutPath);
            if (frame == null) return null;

            int at = -1;
            for (int i = 0; i < frame.nodes.Length; i++)
                if (frame.nodes[i].id == rootId) { at = i; break; }
            if (at < 0)
            {
                Debug.LogError($"[시안] {layoutPath} 에 노드 {rootId} 가 없다.");
                return null;
            }

            // 뿌리에서 이어지는 것만 고른다. 납작한 목록이고 부모가 늘 앞에 있으므로 한 번만 훑으면 된다
            var 쓴다 = new bool[frame.nodes.Length];
            쓴다[at] = true;
            for (int i = at + 1; i < frame.nodes.Length; i++)
            {
                int p = frame.nodes[i].p;
                if (p >= 0 && p < i && 쓴다[p]) 쓴다[i] = true;
            }

            var made = new GameObject[frame.nodes.Length];
            GameObject root = null;
            foreach (var n in frame.nodes)
            {
                if (!쓴다[n.i]) continue;
                bool 뿌리 = n.i == at;
                Transform host = 뿌리 ? parent : made[n.p].transform;
                float px = 뿌리 ? 0f : frame.nodes[n.p].x;
                float py = 뿌리 ? 0f : frame.nodes[n.p].y;
                // 바로 아래 조각들은 뿌리가 (0,0) 인 셈으로 놓는다 — 시안 좌표를 그대로 쓴다
                if (화면전체로 && n.p == at) px = py = 0f;

                var go = 노드하나(n, host, px, py);
                if (뿌리 && 화면전체로) Place((RectTransform)go.transform, 0f, 0f, frame.width, frame.height);
                made[n.i] = go;
                if (!string.IsNullOrEmpty(n.id)) ById[n.id] = go;
                if (뿌리) root = go;
            }
            return root;
        }

        public static GameObject Build(string layoutPath, Transform parent)
        {
            var frame = 배치표읽기(layoutPath);
            if (frame == null) return null;

            ById.Clear();
            var made = new GameObject[frame.nodes.Length];
            GameObject root = null;

            foreach (var n in frame.nodes)
            {
                Transform host = n.p < 0 ? parent : made[n.p].transform;
                float px = n.p < 0 ? 0f : frame.nodes[n.p].x;
                float py = n.p < 0 ? 0f : frame.nodes[n.p].y;

                var go = 노드하나(n, host, px, py);
                made[n.i] = go;
                if (!string.IsNullOrEmpty(n.id)) ById[n.id] = go;
                if (n.p < 0) root = go;
            }
            return root;
        }

        static FigmaFrame 배치표읽기(string layoutPath)
        {
            if (!File.Exists(layoutPath))
            {
                Debug.LogError($"[시안] 배치표가 없다: {layoutPath}");
                return null;
            }
            var frame = JsonUtility.FromJson<FigmaFrame>(File.ReadAllText(layoutPath, Encoding.UTF8));
            if (frame?.nodes == null || frame.nodes.Length == 0)
            {
                Debug.LogError($"[시안] 배치표를 못 읽었다: {layoutPath}");
                return null;
            }
            return frame;
        }

        static GameObject 노드하나(FigmaNode n, Transform host, float px, float py)
        {
            if (n.HasGlow) MakeGlow(n, host, px, py);

            var go = new GameObject(Clean(n.name), typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(host, false);
            Place(rt, n.x - px, n.y - py, n.w, n.h);

            switch (n.type)
            {
                case "TEXT": MakeText(go, n); break;
                case "REGULAR_POLYGON": MakePolygon(go, n); break;
                case "ELLIPSE": MakeEllipse(go, n); break;
                default: MakeBox(go, n); break;
            }

            if (n.opacity < 0.999f) go.AddComponent<CanvasGroup>().alpha = n.opacity;
            if (n.clip) go.AddComponent<RectMask2D>();
            return go;
        }

        // ── 자리잡기 ────────────────────────────────────────────

        /// <summary>왼쪽 위를 원점으로 쓴다. 시안 좌표를 그대로 넣을 수 있다.</summary>
        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        // ── 모양 ────────────────────────────────────────────────

        static void MakeBox(GameObject go, FigmaNode n)
        {
            if (n.HasFill)
            {
                var img = go.AddComponent<Image>();
                img.sprite = UiSprites.White();
                img.color = Rgba(n.fill, n.fillA);
                img.raycastTarget = false;
            }
            if (n.HasStroke) Ring(go.transform, n.w, n.h, UiSprites.RectRing(n.strokeW),
                                  Rgba(n.stroke, n.strokeA), Image.Type.Sliced);
        }

        static void MakePolygon(GameObject go, FigmaNode n)
        {
            int w = Mathf.Max(2, Mathf.RoundToInt(n.w)), h = Mathf.Max(2, Mathf.RoundToInt(n.h));
            int sides = n.sides > 2 ? n.sides : 3;
            if (n.HasFill)
            {
                var img = go.AddComponent<Image>();
                img.sprite = UiSprites.Polygon(sides, w, h);
                img.color = Rgba(n.fill, n.fillA);
                img.raycastTarget = false;
            }
            if (n.HasStroke)
                Ring(go.transform, n.w, n.h, UiSprites.PolygonRing(sides, w, h, n.strokeW),
                     Rgba(n.stroke, n.strokeA), Image.Type.Simple);
        }

        static void MakeEllipse(GameObject go, FigmaNode n)
        {
            int w = Mathf.Max(2, Mathf.RoundToInt(n.w)), h = Mathf.Max(2, Mathf.RoundToInt(n.h));
            if (n.HasFill)
            {
                var img = go.AddComponent<Image>();
                img.sprite = UiSprites.Ellipse(w, h);
                img.color = Rgba(n.fill, n.fillA);
                img.raycastTarget = false;
            }
            if (n.HasStroke)
                Ring(go.transform, n.w, n.h, UiSprites.EllipseRing(w, h, n.strokeW),
                     Rgba(n.stroke, n.strokeA), Image.Type.Simple);
        }

        /// <summary>테두리는 따로 둔다. 칠과 색이 다르니 Image 를 겹쳐야 한다.</summary>
        static void Ring(Transform host, float w, float h, Sprite sprite, Color color, Image.Type type)
        {
            var go = new GameObject("테두리", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(host, false);
            Place(rt, 0f, 0f, w, h);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            // 가운데 칸은 그리지 않는다. 비어 있어도 9분할로 늘리면 양옆 밝은 픽셀이
            // 번져 들어와 틀 안쪽이 테두리 색으로 물든다
            if (type == Image.Type.Sliced) img.fillCenter = false;
            img.color = color;
            img.raycastTarget = false;
        }

        /// <summary>번짐은 형제로 두고 바로 앞에 끼운다. 자식으로 두면 칠 위에 덮인다.</summary>
        static void MakeGlow(FigmaNode n, Transform host, float px, float py)
        {
            int r = Mathf.Max(2, Mathf.RoundToInt(n.glowR));
            var go = new GameObject(Clean(n.name) + " 번짐", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(host, false);
            Place(rt, n.x - px - r, n.y - py - r, n.w + r * 2f, n.h + r * 2f);
            var img = go.AddComponent<Image>();
            if (n.type == "REGULAR_POLYGON" || n.type == "ELLIPSE")
            {
                // 도형 둘레를 따라 번져야 한다. 네모 번짐을 육각형에 씌우면 바로 티가 난다
                int w = Mathf.Max(2, Mathf.RoundToInt(n.w)), h = Mathf.Max(2, Mathf.RoundToInt(n.h));
                img.sprite = UiSprites.ShapeGlow(n.type == "ELLIPSE" ? 0 : Mathf.Max(3, n.sides), w, h, r);
                img.type = Image.Type.Simple;
            }
            else
            {
                // 속이 빈 틀이면 가운데를 비운 번짐을 쓰고, 가운데 칸 자체를 안 그린다
                img.sprite = n.HasFill ? UiSprites.Glow(r) : UiSprites.EdgeGlow(r);
                img.type = Image.Type.Sliced;
                img.fillCenter = n.HasFill;
            }
            img.color = Rgba(n.glow, n.glowA * 0.75f);  // 겹겹이 쌓이면 너무 밝아 조금 눌러 둔다
            img.raycastTarget = false;
        }

        // ── 글자 ────────────────────────────────────────────────

        static void MakeText(GameObject go, FigmaNode n)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = FontAssets.For(n.font, n.fontWeight);
            t.fontSize = n.fontSize;
            t.color = Rgba(n.fill, n.fillA <= 0f ? 1f : n.fillA);
            t.raycastTarget = false;
            t.richText = false;              // 시안 글에 < > 가 들어와도 태그로 읽지 않게
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = Align(n.align, n.valign);
            t.text = n.text;

            if (n.fontSize > 0f && Mathf.Abs(n.letter) > 0.001f)
                t.characterSpacing = n.letter / n.fontSize * 100f;

            if (n.lineH > 0f && n.fontSize > 0f && t.font != null) FitLines(t, n);
        }

        /// <summary>
        /// 글줄 간격과 첫 줄 높이를 시안에 맞춘다.
        ///
        /// 계산식으로 때려 맞히지 않고 **실제로 그려 보고 잰다.** TMP 의 lineSpacing 단위는
        /// 글꼴마다 다르게 먹고, 한글이 섞이면 예비 글꼴 쪽 높이까지 끼어든다.
        /// 재서 맞추면 어떤 글꼴이 와도 어긋나지 않는다.
        /// (안 맞추면 줄이 내려갈수록 조금씩 밀려 끝에서 10px 넘게 벌어진다)
        /// </summary>
        static void FitLines(TextMeshProUGUI t, FigmaNode n)
        {
            t.ForceMeshUpdate();
            var info = t.textInfo;

            if (info != null && info.lineCount >= 2)
            {
                float a0 = info.lineInfo[0].baseline - info.lineInfo[1].baseline;
                t.lineSpacing = 100f;
                t.ForceMeshUpdate();
                float a1 = info.lineInfo[0].baseline - info.lineInfo[1].baseline;
                float slope = (a1 - a0) / 100f;
                t.lineSpacing = Mathf.Abs(slope) > 1e-4f ? (n.lineH - a0) / slope : 0f;
                t.ForceMeshUpdate();
            }

            // 첫 줄 — 피그마는 줄 상자 가운데에 글을 놓고, TMP 는 위를 맞춘다
            var fi = t.font.faceInfo;
            if (fi.pointSize <= 0 || info == null || info.lineCount < 1) return;
            float s = n.fontSize / fi.pointSize;
            float asc = fi.ascentLine * s, desc = fi.descentLine * s;
            float want = (n.lineH - (asc - desc)) * 0.5f + asc;
            float have = -info.lineInfo[0].baseline;
            var rt = (RectTransform)t.transform;
            rt.anchoredPosition += new Vector2(0f, -(want - have));
        }

        static TextAlignmentOptions Align(string h, string v)
        {
            bool mid = v == "CENTER", bot = v == "BOTTOM";
            if (h == "CENTER") return bot ? TextAlignmentOptions.Bottom : mid ? TextAlignmentOptions.Center : TextAlignmentOptions.Top;
            if (h == "RIGHT") return bot ? TextAlignmentOptions.BottomRight : mid ? TextAlignmentOptions.Right : TextAlignmentOptions.TopRight;
            return bot ? TextAlignmentOptions.BottomLeft : mid ? TextAlignmentOptions.Left : TextAlignmentOptions.TopLeft;
        }

        // ── 잡일 ────────────────────────────────────────────────

        /// <summary>"#rrggbb" 와 알파를 색으로. Color32 로 받아야 시안과 값이 정확히 같다.</summary>
        public static Color Rgba(string hex, float a)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length < 7) return new Color(1f, 1f, 1f, a);
            byte r = byte.Parse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
            var c = (Color)new Color32(r, g, b, 255);
            c.a = a;
            return c;
        }

        /// <summary>
        /// 레이어 이름에서 키보드로 못 치는 글자를 뺀다 (본인 10-11).
        /// 찾을 수 없는 이름은 없는 이름이나 마찬가지다.
        /// </summary>
        public static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "노드";
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                switch (c)
                {
                    case '·': sb.Append('-'); break;
                    case '→': sb.Append("->"); break;
                    case '①': sb.Append('1'); break;
                    case '②': sb.Append('2'); break;
                    case '③': sb.Append('3'); break;
                    case '④': sb.Append('4'); break;
                    case '⑤': sb.Append('5'); break;
                    case '／': sb.Append('/'); break;
                    default: sb.Append(c); break;
                }
            }
            // 가운뎃점을 바꾸면 "- -" 처럼 벌어지는 자리가 생긴다
            return sb.ToString().Replace("  ", " ").Trim();
        }
    }
}

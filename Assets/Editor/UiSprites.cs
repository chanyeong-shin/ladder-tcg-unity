using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LadderBattle.EditorTools
{
    /// <summary>
    /// 화면에 필요한 도형을 코드로 그려 PNG 로 저장하고 Sprite 로 넣어 준다.
    ///
    /// 왜 그림 파일을 피그마에서 안 받아 오나 — 받아 오면 색이 그림에 구워져서
    /// Inspector 에서 색을 못 바꾼다. 여기서 만든 것은 전부 **흰색**이라
    /// Image 의 Color 로 색을 입힌다. 시안의 색을 바꾸고 싶으면 색만 바꾸면 된다.
    ///
    /// 만든 파일은 `Assets/Art/Generated/` 에 남는다. 지워도 다시 만들어진다.
    /// </summary>
    public static class UiSprites
    {
        const string Dir = "Assets/Art/Generated";
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // ── 공개 ────────────────────────────────────────────────

        /// <summary>테두리만 있는 9분할 사각 틀. 가운데가 비어 있어 어떤 크기로도 늘어난다.</summary>
        public static Sprite RectRing(float thickness)
        {
            int t100 = Mathf.RoundToInt(thickness * 100f);
            return Get($"rect_ring_{t100}", () =>
            {
                int b = Mathf.CeilToInt(thickness) + 2;
                int n = b * 2 + 2;
                var tex = Draw(n, n, (px, py) =>
                {
                    float d = Mathf.Min(Mathf.Min(px, py), Mathf.Min(n - px, n - py));
                    return Band(-d, thickness);   // 가장자리에서 thickness 만큼만 칠한다
                });
                return Save(tex, $"rect_ring_{t100}", b);
            });
        }

        /// <summary>바깥으로 부드럽게 사라지는 9분할 번짐. 네온 발광에 쓴다.</summary>
        public static Sprite Glow(float radius)
        {
            int r = Mathf.Max(2, Mathf.RoundToInt(radius));
            return Get($"glow_{r}", () =>
            {
                int n = r * 2 + 2;
                var tex = Draw(n, n, (px, py) =>
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - px, px - (n - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - py, py - (n - r)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d / r);
                    return a * a;             // 제곱하면 가운데는 진하고 끝은 빨리 옅어진다
                });
                return Save(tex, $"glow_{r}", r);
            });
        }

        /// <summary>
        /// 테두리에서 바깥으로만 번지는 빛. **가운데는 비어 있다.**
        /// 속이 빈 틀(칠이 투명한 프레임)에 꽉 찬 번짐을 씌우면 그 안에 있는 것을 덮어 버린다.
        /// </summary>
        public static Sprite EdgeGlow(float radius)
        {
            int r = Mathf.Max(2, Mathf.RoundToInt(radius));
            return Get($"edgeglow_{r}", () =>
            {
                int n = r * 2 + 2;
                var tex = Draw(n, n, (px, py) =>
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - px, px - (n - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - py, py - (n - r)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= 0.0001f) return 0f;          // 가운데는 비운다
                    float a = Mathf.Clamp01(1f - d / r);
                    return a * a;
                });
                return Save(tex, $"edgeglow_{r}", r);
            });
        }

        /// <summary>속이 찬 정다각형. 꼭짓점이 위를 보게 그린다 (피그마 기본과 같다).</summary>
        public static Sprite Polygon(int sides, int w, int h)
            => Shape($"poly{sides}_{w}x{h}", w, h, PolySdf(sides, w, h), 0f);

        /// <summary>정다각형 테두리. 두께는 안쪽으로 들어간다 (피그마의 INSIDE 와 같다).</summary>
        public static Sprite PolygonRing(int sides, int w, int h, float thickness)
            => Shape($"poly{sides}_{w}x{h}_r{Mathf.RoundToInt(thickness * 100f)}",
                     w, h, PolySdf(sides, w, h), thickness);

        /// <summary>
        /// 도형 둘레를 따라 번지는 빛. 육각형 서버에 네모난 번짐을 씌우면 바로 티가 난다.
        /// 그림 크기는 도형보다 사방으로 radius 만큼 크다.
        /// </summary>
        public static Sprite ShapeGlow(int sides, int w, int h, int radius)
        {
            string key = sides > 0 ? $"glow_poly{sides}_{w}x{h}_{radius}" : $"glow_ellipse_{w}x{h}_{radius}";
            return Get(key, () =>
            {
                int gw = w + radius * 2, gh = h + radius * 2;
                Sdf inner = sides > 0 ? PolySdf(sides, w, h) : EllipseSdf(w, h);
                var tex = Draw(gw, gh, (px, py) =>
                {
                    float d = inner(px - radius, py - radius);
                    if (d <= 0f) return 1f;
                    float a = Mathf.Clamp01(1f - d / radius);
                    return a * a;
                });
                return Save(tex, key, 0);
            });
        }

        public static Sprite Ellipse(int w, int h)
            => Shape($"ellipse_{w}x{h}", w, h, EllipseSdf(w, h), 0f);

        public static Sprite EllipseRing(int w, int h, float thickness)
            => Shape($"ellipse_{w}x{h}_r{Mathf.RoundToInt(thickness * 100f)}",
                     w, h, EllipseSdf(w, h), thickness);

        /// <summary>가장 단순한 흰 사각형. 색칠용.</summary>
        public static Sprite White()
            => Get("white", () => Save(Draw(4, 4, (a, b) => 1f), "white", 0));

        // ── 안쪽 ────────────────────────────────────────────────

        delegate float Sdf(float px, float py);

        static Sprite Shape(string key, int w, int h, Sdf sdf, float thickness)
            => Get(key, () => Save(Draw(w, h, (px, py) =>
            {
                float d = sdf(px, py);
                return thickness > 0f ? Band(d, thickness) : Mathf.Clamp01(0.5f - d);
            }), key, 0));

        /// <summary>경계에서 안쪽으로 thickness 만큼만 남기는 띠. 가장자리는 부드럽게.</summary>
        static float Band(float d, float thickness)
            => Mathf.Min(Mathf.Clamp01(0.5f - d), Mathf.Clamp01(d + thickness + 0.5f));

        /// <summary>볼록 다각형까지의 부호 있는 거리. 안쪽이 음수다.</summary>
        static Sdf PolySdf(int sides, int w, int h)
        {
            var pts = new Vector2[sides];
            float cx = w * 0.5f, cy = h * 0.5f;
            for (int k = 0; k < sides; k++)
            {
                // 90도에서 시작해 시계방향 — 꼭짓점이 위를 본다
                float a = Mathf.Deg2Rad * (90f + 360f * k / sides);
                pts[k] = new Vector2(cx + Mathf.Cos(a) * cx, cy - Mathf.Sin(a) * cy);
            }
            // 꼭짓점을 어느 방향으로 돌렸느냐에 따라 법선이 안팎으로 뒤집힌다.
            // 넓이의 부호로 방향을 재서 바로잡는다 — 틀리면 도형이 통째로 뒤집혀 사각형처럼 보인다
            float area2 = 0f;
            for (int k = 0; k < sides; k++)
            {
                Vector2 a = pts[k], b = pts[(k + 1) % sides];
                area2 += a.x * b.y - b.x * a.y;
            }
            float sign = area2 < 0f ? -1f : 1f;

            return (px, py) =>
            {
                float best = float.NegativeInfinity;
                for (int k = 0; k < sides; k++)
                {
                    Vector2 a = pts[k], b = pts[(k + 1) % sides];
                    Vector2 e = b - a;
                    // 변의 바깥쪽 법선까지의 거리. 볼록이므로 가장 큰 값이 곧 거리다
                    float len = e.magnitude;
                    if (len < 1e-5f) continue;
                    Vector2 nrm = new Vector2(e.y, -e.x) * (sign / len);
                    best = Mathf.Max(best, Vector2.Dot(new Vector2(px, py) - a, nrm));
                }
                return best;
            };
        }

        static Sdf EllipseSdf(int w, int h)
        {
            float rx = w * 0.5f, ry = h * 0.5f;
            return (px, py) =>
            {
                float nx = (px - rx) / rx, ny = (py - ry) / ry;
                float k = Mathf.Sqrt(nx * nx + ny * ny);
                return (k - 1f) * Mathf.Min(rx, ry);
            };
        }

        static Texture2D Draw(int w, int h, Sdf alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 픽셀 가운데를 본다. 위에서 아래로 그리므로 y 를 뒤집어 저장한다
                    float a = Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f));
                    px[(h - 1 - y) * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static Sprite Save(Texture2D tex, string name, int border)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.spritePixelsPerUnit = 100f;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            if (border > 0) imp.spriteBorder = new Vector4(border, border, border, border);
            imp.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite Get(string key, System.Func<Sprite> make)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            string path = $"{Dir}/{key}.png";
            s = AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? make();
            Cache[key] = s;
            return s;
        }

        /// <summary>다시 만들 때 쓴다. 캐시를 비우면 다음 호출에서 파일을 새로 읽는다.</summary>
        public static void ClearCache() => Cache.Clear();
    }
}

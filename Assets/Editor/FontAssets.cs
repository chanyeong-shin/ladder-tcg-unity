using System.IO;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LadderBattle.EditorTools
{
    /// <summary>
    /// 시안이 쓰는 글꼴(Inter · Rajdhani)을 TextMeshPro 폰트 에셋으로 만든다.
    ///
    /// 한글은 Inter 에도 Rajdhani 에도 없다. 피그마에서는 OS 글꼴로 대신 그려 주지만
    /// 유니티는 그러지 않아서 글자가 통째로 빈칸이 된다. 그래서 **본고딕을 예비 글꼴로
    /// 달아 둔다** — 영문은 시안 글꼴로, 한글은 본고딕으로 나온다.
    ///
    /// 글자판(atlas)은 Dynamic 으로 만든다. 한글 11,172자를 미리 구워 두면 파일이
    /// 수백 MB 가 되므로, 쓰는 글자만 그때그때 채우게 한다.
    /// </summary>
    public static class FontAssets
    {
        const string Dir = "Assets/Fonts";

        public const string Inter400 = "Inter-Regular";
        public const string Inter700 = "Inter-Bold";
        public const string Rajdhani700 = "Rajdhani-Bold";
        public const string Korean = "NotoSansKR-Regular";
        public const string KoreanBold = "NotoSansKR-Bold";

        static readonly Dictionary<string, TMP_FontAsset> Cache = new Dictionary<string, TMP_FontAsset>();

        /// <summary>네 가지 폰트 에셋을 만들고 한글 예비 글꼴을 걸어 둔다. 이미 있으면 그대로 쓴다.</summary>
        public static void BuildAll()
        {
            var ko = Make(Korean);
            var koBold = Make(KoreanBold);
            foreach (var (name, fallback) in new[]
            {
                (Inter400, ko), (Inter700, koBold), (Rajdhani700, koBold),
            })
            {
                var fa = Make(name);
                if (fa == null || fallback == null) continue;
                fa.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
                EditorUtility.SetDirty(fa);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>시안의 글꼴 이름과 굵기로 폰트 에셋을 고른다.</summary>
        public static TMP_FontAsset For(string family, int weight)
        {
            if (family == "Rajdhani") return Get(Rajdhani700);
            return Get(weight >= 600 ? Inter700 : Inter400);
        }

        public static TMP_FontAsset Get(string name)
        {
            if (Cache.TryGetValue(name, out var c) && c != null) return c;
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{Dir}/{name} SDF.asset") ?? Make(name);
            Cache[name] = fa;
            return fa;
        }

        static TMP_FontAsset Make(string name)
        {
            string assetPath = $"{Dir}/{name} SDF.asset";
            var exist = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (exist != null) return exist;

            string ttf = $"{Dir}/{name}.ttf";
            if (!File.Exists(ttf))
            {
                Debug.LogError($"[글꼴] {ttf} 가 없다. 글꼴 파일부터 넣어야 한다.");
                return null;
            }
            var src = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (src == null)
            {
                Debug.LogError($"[글꼴] {ttf} 를 Font 로 못 읽었다.");
                return null;
            }

            // 90pt 로 구우면 11px 같은 작은 글자도 또렷하다. 1024 판 하나로 시작한다
            var fa = TMP_FontAsset.CreateFontAsset(
                src, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
            fa.name = name + " SDF";

            AssetDatabase.CreateAsset(fa, assetPath);
            // 글자판과 재질은 폰트 에셋 안에 같이 넣어야 파일이 흩어지지 않는다
            if (fa.atlasTextures != null && fa.atlasTextures.Length > 0)
            {
                fa.atlasTextures[0].name = name + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            }
            if (fa.material != null)
            {
                fa.material.name = name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[글꼴] {name} SDF 를 만들었다.");
            return fa;
        }
    }
}

using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Breach.UI;

namespace Breach.EditorTools
{
    public static class NovemberUpdate
    {
        [MenuItem("Breach/11월 9일 화면 반영 후 빌드")]
        public static void Build()
        {
            try {
                EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
                var c=UnityEngine.Object.FindFirstObjectByType<BattleController>();
                if(!c)throw new Exception("Saved scene has no BattleController");
                c.titleFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Rajdhani-Bold SDF.asset");
                if(!c.titleFont)throw new Exception("Rajdhani title font missing");
                if(c.loading)UnityEngine.Object.DestroyImmediate(c.loading);c.loading=null;
                if(c.title)UnityEngine.Object.DestroyImmediate(c.title);
                c.title=Ui.Rect("B 메인 - 실행 시 화면 생성",c.stage,0,0,1920,1080).gameObject;c.startButton=null;
                // Runtime title has no old A art or loading transition. Preserve all game layout/prefabs.
                ApplyDoHyeon(c);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                SceneBuilder.Build();
            } catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }

        // User 11-22: font B "Do Hyeon · 굵고 응집된 장비 UI" — Korean, Latin and numbers all in Do Hyeon (OFL, Assets/Fonts).
        // The title logo's Latin lines keep the HTML title B font (Rajdhani). Card art used by the transfer effect is re-baked.
        static void ApplyDoHyeon(BattleController c)
        {
            const string path="Assets/Fonts/DoHyeon-Regular SDF.asset";
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if(!font){
                var ttf=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/DoHyeon-Regular.ttf");if(!ttf)throw new Exception("DoHyeon-Regular.ttf missing");
                font=TMP_FontAsset.CreateFontAsset(ttf,90,9,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
                font.name="DoHyeon-Regular SDF";AssetDatabase.CreateAsset(font,path);
                font.atlasTextures[0].name="DoHyeon-Regular Atlas";AssetDatabase.AddObjectToAsset(font.atlasTextures[0],font);
                font.material.name="DoHyeon-Regular Material";AssetDatabase.AddObjectToAsset(font.material,font);
                AssetDatabase.SaveAssets();
            }
            // User 11-22 "유니티 카드 이름이 위로 쏠림": every text box was laid out for Noto Sans KR (ascent 1.16 em,
            // descent 0.288 em, line 1.448 em); Do Hyeon reports 0.8 / 0.2 / 1.25, so top-aligned text rose by 0.36 em.
            // Give the Do Hyeon asset Noto's line metrics so existing boxes keep their baselines.
            var face=font.faceInfo;float em=face.pointSize;face.ascentLine=1.16f*em;face.descentLine=-.288f*em;face.lineHeight=1.448f*em;font.faceInfo=face;EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
            // Pre-render every character the game uses so no glyph is rasterized mid-animation (a dynamic font
            // otherwise stalls the frame the first time a new Korean character appears, e.g. "서버 앞 차단").
            var used=new System.Text.StringBuilder();for(int ch=32;ch<127;ch++)used.Append((char)ch);
            foreach(var file in System.IO.Directory.GetFiles("Assets/Scripts","*.cs",System.IO.SearchOption.AllDirectories))foreach(char ch in System.IO.File.ReadAllText(file))if(ch>=0xAC00&&ch<=0xD7A3||ch>=0x2000&&ch<=0x27FF)used.Append(ch);
            foreach(var t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))used.Append(t.text);
            font.TryAddCharacters(used.ToString(),out string missing);
            // Do Hyeon has no "·" or "—": fall back to Noto Sans KR for those
            var noto=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Regular SDF.asset");if(noto){if(font.fallbackFontAssetTable==null)font.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>();if(!font.fallbackFontAssetTable.Contains(noto))font.fallbackFontAssetTable.Add(noto);}
            foreach(var tex in font.atlasTextures)if(tex&&!AssetDatabase.IsSubAsset(tex)&&!AssetDatabase.Contains(tex)){tex.name="DoHyeon-Regular Atlas";AssetDatabase.AddObjectToAsset(tex,font);}
            EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
            Debug.Log("DoHyeon atlas: "+font.characterTable.Count+" characters, "+font.atlasTextures.Length+" atlas page(s), missing: "+missing);
            bool Keep(TMP_Text t)=>c.titleFont&&t.font==c.titleFont;
            foreach(var t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(!Keep(t)){t.font=font;EditorUtility.SetDirty(t);}
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs"})){
                string prefabPath=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(prefabPath);bool changed=false;
                foreach(var t in root.GetComponentsInChildren<TMP_Text>(true))if(t.font!=font){t.font=font;changed=true;}
                if(changed)PrefabUtility.SaveAsPrefabAsset(root,prefabPath);PrefabUtility.UnloadPrefabContents(root);
            }
            c.font=font;if(c.feedback){c.feedback.font=font;EditorUtility.SetDirty(c.feedback);}EditorUtility.SetDirty(c);
            // re-bake the card art used when a card is scanned in the socket
            if(c.feedback&&c.cardPrefabs!=null){for(int i=0;i<c.cardPrefabs.Length;i++)c.feedback.cardImages[i]=CardFxBake.Bake(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(c.cardPrefabs[i].gameObject))??c.cardPrefabs[i].gameObject,"CardFx_"+i);if(c.backPrefab)c.feedback.backImage=CardFxBake.Bake(c.backPrefab,"CardFx_Back");EditorUtility.SetDirty(c.feedback);}
        }
    }
}

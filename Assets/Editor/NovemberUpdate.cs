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
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                SceneBuilder.Build();
            } catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }
    }
}

using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Breach.UI
{
    public sealed class TitleFxCheck : MonoBehaviour
    {
        BattleController c;string dir;bool failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--title-fx-check")>=0)FindFirstObjectByType<BattleController>().gameObject.AddComponent<TitleFxCheck>();}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--title-fx-check");if(at<0||at+1>=args.Length)yield break;
            dir=args[at+1];Directory.CreateDirectory(dir);Application.runInBackground=true;c=GetComponent<BattleController>();yield return null;
            float[] moments={.58f,.75f,.84f,1.0f,1.35f,2.3f,3.06f,3.18f,3.36f,3.56f,3.77f,4.5f};
            string[] names={"01-approach","02-compress","03-release","04-shockwave","05-decay","06-circuits","07-menu-interference","08-menu-split","09-menu-static","10-menu-locking","11-menu-stable","12-title-ready"};
            for(int i=0;i<moments.Length;i++){
                yield return Await(()=>c.titleScreen.IntroTime>=moments[i],names[i]);if(failed)yield break;
                c.titleScreen.IntroPlaybackSpeed=0;
                foreach(var menu in c.titleScreen.Menus){float alpha=menu.GetComponent<MenuSignal>().VisibleAlpha;if(alpha!=0&&alpha!=1){Fail("Menu faded instead of discrete signal");yield break;}}
                if(moments[i]>=3&&moments[i]<3.6f&&c.titleScreen.Ready){Fail("Input released before signal settled");yield break;}
                yield return Shot(names[i]);c.titleScreen.IntroPlaybackSpeed=1;
            }
            if(!c.titleScreen.MenuSignalAtRest){Fail("Signal offset/noise remained after intro");yield break;}
            // Skip during signal acquisition must restore exact positions and clear all ghosts.
            c.titleScreen.Show();yield return Await(()=>c.titleScreen.IntroTime>=3.15f,"skip during glitch");if(failed)yield break;
            c.titleScreen.FinishIntro();yield return null;
            if(!c.titleScreen.Ready||!c.titleScreen.MenuSignalAtRest){Fail("Skip failed to clean up");yield break;}
            yield return Shot("13-skip-clean");
            c.animationSpeed=4;c.startButton.onClick.Invoke();yield return Await(()=>c.CanInput,"start after skip");if(failed)yield break;
            if(c.title.activeSelf||c.loading){Fail("Title/loading remains in play");yield break;}
            File.WriteAllText(Path.Combine(dir,"title-fx-result.txt"),"PASS: opposing energy approach, compression/release/decay captured, discrete opaque menu acquisition (no fade), four menu rows, settled offset and ghost cleanup, skip cleanup, direct start after skip");Application.Quit(0);
        }
        IEnumerator Await(Func<bool> test,string label){float until=Time.realtimeSinceStartup+35;while(!test()&&Time.realtimeSinceStartup<until)yield return null;if(!test())Fail("Timeout "+label);}
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();var canvas=c.stage.GetComponentInParent<Canvas>();var camera=Camera.main;var rt=new RenderTexture(1600,900,24);var mode=canvas.renderMode;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();
            var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());
            RenderTexture.active=old;camera.targetTexture=null;canvas.renderMode=mode;Destroy(image);rt.Release();Destroy(rt);yield return null;
        }
        void Fail(string reason){failed=true;File.WriteAllText(Path.Combine(dir,"title-fx-result.txt"),"FAIL: "+reason);Application.Quit(1);}
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI
{
    // Only explicit --november-check launches these deterministic presentation fixtures.
    public sealed class NovemberCheck : MonoBehaviour
    {
        BattleController c;string dir;bool failed;
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--november-check");if(at<0||at+1>=args.Length)yield break;
            dir=args[at+1];Directory.CreateDirectory(dir);Application.runInBackground=true;c=GetComponent<BattleController>();
            yield return Await(()=>c.titleScreen.IntroTime>=.65f,"title collision");if(failed)yield break;yield return Shot("01-title-collision");
            yield return Await(()=>c.titleScreen.IntroTime>=1.6f,"title circuits");if(failed)yield break;yield return Shot("02-title-circuits");
            yield return Await(()=>c.titleScreen.Ready,"title ready");if(failed)yield break;yield return new WaitForSecondsRealtime(.25f);yield return Shot("03-title-B");
            if(c.loading||c.titleScreen.Menus.Length!=4){Fail("Loading not removed / menu count");yield break;}
            var menus=c.titleScreen.Menus;for(int i=0;i<4;i++){
                var r=(RectTransform)menus[i].transform;
                if(r.anchoredPosition.x!=460||r.anchoredPosition.y!=-(480+i*46)||menus[i].GetComponentsInChildren<Shape>(true).Length!=0){Fail("Menu alignment or border");yield break;}
            }
            menus[1].onClick.Invoke();yield return null;yield return Shot("04-help");if(!c.titleScreen.ModalOpen){Fail("Help not open");yield break;}c.titleScreen.CloseModal();
            menus[2].onClick.Invoke();yield return null;yield return Shot("05-settings");if(!c.titleScreen.ModalOpen){Fail("Settings not open");yield break;}c.titleScreen.CloseModal();
            c.animationSpeed=4;c.startButton.onClick.Invoke();yield return Await(()=>c.CanInput,"direct start");if(failed)yield break;c.animationSpeed=1;
            if(c.game.GetComponentsInChildren<BoardGradient>(true).Length!=3){Fail("Missing gradient phase");yield break;}yield return Shot("06-board-bright");
            for(int scenario=0;scenario<4;scenario++){
                int phase=scenario%2;bool buffer=scenario>=2;
                c.State.Attacker=phase==0?1:0;c.State.Phase=phase;c.State.Hands[1].Clear();c.State.Hands[1].Add(buffer?Card.Buffer:Card.Inject);c.State.Hidden.Clear();
                for(int i=0;i<3;i++){c.State.Health[0,i]=c.State.Health[1,i]=1;c.State.Power[0,i]=c.State.Power[1,i]=0;c.State.Rungs[i]=-1;}
                Private("CapturePublic");c.Render();yield return null;
                bool done=false;StartCoroutine(Run((IEnumerator)Private("Play",1,0,0),()=>done=true));
                yield return Await(()=>c.feedback.Phase=="move","enemy move");if(failed)yield break;
                var active=c.backgrounds.First(x=>x.activeSelf);
                if(BattleController.Find(active.transform,"Enemy_Back_374").gameObject.activeSelf){Fail("Enemy card remains in hand during flight");yield break;}
                yield return Shot("07-enemy-"+scenario+"-to-socket");
                yield return Await(()=>done,"enemy move complete");if(failed)yield break;
                // The hidden/public board contract must survive the visible card movement.
                if((!buffer&&c.State.Power[1,0]!=1)||(buffer&&c.State.Hands[1].Count!=2)){Fail("Enemy play not applied");yield break;}
                if(buffer){if(!BattleController.Find(active.transform,"Enemy_Back_375").gameObject.activeSelf){Fail("Enemy draw backs missing");yield break;}yield return Shot("07-enemy-"+scenario+"-after-draw");}
            }
            bool turnDone=false;StartCoroutine(Run(c.feedback.Turn(0,0,0),()=>turnDone=true));
            yield return Await(()=>c.feedback.Phase=="turn-hold","readable turn");if(failed)yield break;
            float start=Time.realtimeSinceStartup;yield return Shot("08-turn-readable");
            yield return Await(()=>turnDone,"turn completion");if(failed)yield break;
            if(Time.realtimeSinceStartup-start<1.3f){Fail("Turn label hold too short");yield break;}
            c.State.Attacker=0;c.State.Phase=2;c.State.Power[0,1]=2;c.State.Power[1,1]=1;c.State.Power[1,0]=0;c.Render();var report=c.State.Resolve();
            foreach(var server in c.servers)server.Render(report.Before[server.owner,server.channel],0);
            bool attackDone=false;StartCoroutine(Run(c.presentation.Play(report),()=>attackDone=true));
            yield return Await(()=>c.presentation.Phase=="flight","focus");if(failed)yield break;
            if(c.presentation.PeripheralAlpha>.5f||c.presentation.CameraZoom<1.039f){Fail("Combat focus not applied");yield break;}yield return Shot("09-combat-focus");
            yield return Await(()=>c.presentation.ClashPhase=="hold","clash hold");if(failed)yield break;yield return Shot("10-clash");
            yield return Await(()=>attackDone,"combat restore");if(failed)yield break;
            if(c.presentation.PeripheralAlpha!=1||c.presentation.CameraZoom!=1){Fail("Combat focus not restored");yield break;}yield return Shot("11-combat-restored");
            c.resultLabel.text="침투 성공 / ACCESS GRANTED";yield return c.GetComponent<UiSequencePresentation>().Result(report);yield return Shot("12-result");
            Private("ReturnToTitle");yield return Await(()=>c.titleScreen.Ready,"return title");if(failed)yield break;
            if(c.game.activeSelf||c.result.activeSelf||!c.title.activeSelf){Fail("Return to title failed");yield break;}
            File.WriteAllText(Path.Combine(dir,"november-result.txt"),"PASS: title B animation, four vertical borderless menus, no loading, help/settings, direct start, 3 gradients, enemy attack/defense hand removal and movement, readable turn hold, combat focus and restore, clash, result, return title");Application.Quit(0);
        }
        object Private(string name,params object[] args)=>typeof(BattleController).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
        IEnumerator Run(IEnumerator routine,Action done){yield return routine;done();}
        IEnumerator Await(Func<bool> test,string name){float until=Time.realtimeSinceStartup+35;while(!test()&&Time.realtimeSinceStartup<until)yield return null;if(!test())Fail("Timeout "+name);}
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();var canvas=c.stage.GetComponentInParent<Canvas>();var camera=Camera.main;var texture=new RenderTexture(1600,900,24);var mode=canvas.renderMode;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=texture;Canvas.ForceUpdateCanvases();camera.Render();
            var old=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());
            RenderTexture.active=old;camera.targetTexture=null;canvas.renderMode=mode;Destroy(image);texture.Release();Destroy(texture);yield return null;
        }
        void Fail(string message){failed=true;File.WriteAllText(Path.Combine(dir,"november-result.txt"),"FAIL: "+message);Application.Quit(1);}
    }
}

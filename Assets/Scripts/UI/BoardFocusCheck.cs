using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Breach.UI
{
    public sealed class BoardFocusCheck : MonoBehaviour
    {
        BattleController c;string dir;bool failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--board-focus-check")>=0)FindFirstObjectByType<BattleController>().gameObject.AddComponent<BoardFocusCheck>();}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--board-focus-check");if(at<0||at+1>=args.Length)yield break;
            dir=args[at+1];Directory.CreateDirectory(dir);Application.runInBackground=true;c=GetComponent<BattleController>();yield return null;c.animationSpeed=4;c.startButton.onClick.Invoke();
            yield return Await(()=>c.CanInput,"start");if(failed)yield break;c.animationSpeed=1;
            foreach(var background in c.backgrounds){
                var board=BattleController.Find(background.transform,"사다리 시각 카메라");var contacts=BattleController.Find(board,"접점 - 선 위");
                if(contacts.GetSiblingIndex()!=board.childCount-1){Fail("Contact layer not above tracks");yield break;}
                foreach(int id in new[]{270,271,272,273,274,275,276,277,278,294,295,296}){
                    var pin=BattleController.Find(board,"Source_64_"+id) as RectTransform;int row=id>=294?0:id>=276?2:1;
                    if(pin.parent!=contacts||Mathf.Abs(-pin.anchoredPosition.y+pin.rect.height/2-BattleController.RowY[row])>.01f){Fail("Contact alignment "+id);yield break;}
                }
            }
            Fixture();c.State.Hidden.Add(new Secret(1,Card.Firewall,1));c.State.Power[0,1]=c.State.Power[1,1]=1;c.Render();yield return Shot("01-board-contact-order");
            var boardSurface=BattleController.Find(c.backgrounds[2].transform,"사다리 판 - 배경과 회선") as RectTransform;
            Rect original=Bounds(boardSurface);Vector2 hand=((RectTransform)c.hand).anchoredPosition;
            c.EndTurn();
            yield return Await(()=>c.State.Phase==2&&c.feedback.Phase=="turn-hold","resolve intro");if(failed)yield break;
            Rect enlarged=Bounds(boardSurface);
            if(c.presentation.CameraZoom<1.079f||c.presentation.PeripheralAlpha>.5f||enlarged.width<original.width*1.079f||enlarged.yMin<73||enlarged.yMax>855){Fail("Whole board focus bounds");yield break;}
            if(c.hand.anchoredPosition!=hand){Fail("Hand moved");yield break;}yield return Shot("02-whole-board-before-activation");
            yield return Await(()=>c.presentation.Phase=="flight"&&c.presentation.Progress>.55f,"toward gates");if(failed)yield break;
            if(c.presentation.BlockContactCount!=0){Fail("Blocked before reaching server gate");yield break;}
            var gates=c.rungs.GetComponentsInChildren<RectTransform>().Where(t=>t.name=="반도체 방화벽").ToArray();
            if(gates.Length!=2||gates.Any(g=>{float y=-g.anchoredPosition.y+26.4f;return Mathf.Abs(y-292)>.1f&&Mathf.Abs(y-686)>.1f;})){Fail("Gates are not in front of both servers");yield break;}
            yield return Shot("03-packets-before-server-gates");
            yield return Await(()=>c.presentation.BlockContactCount==2,"both block contacts");if(failed)yield break;
            if(Mathf.Abs(c.presentation.LastBlockContact.y-318.4f)>.1f&&Mathf.Abs(c.presentation.LastBlockContact.y-659.6f)>.1f){Fail("Hit off gate surface");yield break;}yield return Shot("04-gate-contact");
            yield return Await(()=>c.CanInput,"next turn");if(failed)yield break;
            if(c.presentation.CameraZoom!=1||c.presentation.PeripheralAlpha!=1||c.presentation.ResolutionFocused){Fail("Focus did not restore");yield break;}yield return Shot("05-board-restored");
            // Rerouted crossing still follows the unchanged channel rule, with a gate
            // at that route's receiving server, rather than disappearance mid-board.
            Fixture();c.State.Rungs[2]=0;c.State.Rungs[0]=1;c.State.Power[0,0]=1;c.State.Hidden.Add(new Secret(1,Card.Firewall,1));
            var report=c.State.Resolve();if(report.Packets.Count!=1||!report.Packets[0].Blocked||report.Packets[0].End!=2){Fail("Rerouted rule fixture changed");yield break;}c.Render();
            bool done=false;StartCoroutine(Run(c.presentation.Play(report),()=>done=true));yield return Await(()=>c.presentation.BlockContactCount==1,"rerouted gate");if(failed)yield break;
            if(Vector2.Distance(c.presentation.LastBlockContact,new Vector2(1262,318.4f))>.1f){Fail("Rerouted gate mismatch");yield break;}yield return Shot("06-rerouted-server-gate");yield return Await(()=>done,"rerouted restore");if(failed)yield break;
            yield return c.presentation.BeginResolution();c.presentation.Cancel();
            if(c.presentation.CameraZoom!=1||c.presentation.PeripheralAlpha!=1){Fail("Cancel left zoom");yield break;}
            File.WriteAllText(Path.Combine(dir,"board-focus-result.txt"),"PASS: square contacts above tracks/aligned; complete board 8% zoom before activation; HUD dim, fixed hand and safe bounds; both server gates; contact only on gate surface; unchanged routed channel blocking; normal/cancel restore");Application.Quit(0);
        }
        void Fixture(){c.State.Attacker=0;c.State.Phase=0;c.State.Winner=-1;c.State.Hidden.Clear();c.State.Hands[0].Clear();c.State.Hands[1].Clear();Array.Clear(c.State.Power,0,6);Array.Clear(c.State.Pierce,0,6);Array.Clear(c.State.Walls,0,3);for(int i=0;i<3;i++){c.State.Health[0,i]=c.State.Health[1,i]=1;c.State.Rungs[i]=-1;}typeof(BattleController).GetMethod("CapturePublic",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null);}
        Rect Bounds(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);var a=c.stage.InverseTransformPoint(corners[1]);var b=c.stage.InverseTransformPoint(corners[3]);return new Rect(a.x,-a.y,b.x-a.x,a.y-b.y);}
        IEnumerator Run(IEnumerator routine,Action done){yield return routine;done();}
        IEnumerator Await(Func<bool> predicate,string name){float end=Time.realtimeSinceStartup+40;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;if(!predicate())Fail("Timeout "+name);}
        IEnumerator Shot(string name){
            float speed=c.animationSpeed,cardSpeed=c.feedback.speed;c.animationSpeed=c.feedback.speed=0.1f;
            yield return new WaitForEndOfFrame();var canvas=c.stage.GetComponentInParent<Canvas>();var camera=Camera.main;var rt=new RenderTexture(1600,900,24);var mode=canvas.renderMode;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();
            var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());
            RenderTexture.active=old;camera.targetTexture=null;canvas.renderMode=mode;Destroy(image);rt.Release();Destroy(rt);yield return null;yield return new WaitForEndOfFrame();c.animationSpeed=speed;c.feedback.speed=cardSpeed;
        }
        void Fail(string reason){failed=true;File.WriteAllText(Path.Combine(dir,"board-focus-result.txt"),"FAIL: "+reason);Application.Quit(1);}
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Breach.UI {
 // Opt-in fixed presentation fixtures. Never runs during normal play.
 public sealed class Week9Check:MonoBehaviour {
  BattleController c;string dir;bool failed;
  IEnumerator Start(){
   var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--week9-check");if(at<0||at+1>=args.Length)yield break;
   dir=args[at+1];Directory.CreateDirectory(dir);Application.runInBackground=true;c=GetComponent<BattleController>();c.animationSpeed=4;
   yield return null;c.startButton.onClick.Invoke();float limit=Time.realtimeSinceStartup+30;while(!c.CanInput&&Time.realtimeSinceStartup<limit)yield return null;
   if(!c.CanInput){Fail("Start timeout");yield break;}c.animationSpeed=1;
   Vector2 button=((RectTransform)c.endButton.transform).anchoredPosition;Vector3 hand=c.hand.localScale;
   var fixedHead=BattleController.Find(c.backgrounds[2].transform,"Source_64_438") as RectTransform;Vector2 headOrigin=fixedHead.anchoredPosition;
   for(int scenario=0;scenario<6;scenario++){
    c.State.Attacker=0;c.State.Phase=0;c.State.Winner=-1;c.State.Hidden.Clear();Array.Clear(c.State.Power,0,6);Array.Clear(c.State.Walls,0,3);Array.Clear(c.State.Pierce,0,6);
    c.State.Hands[0].Clear();c.State.Hands[1].Clear();for(int i=0;i<3;i++){c.State.Health[0,i]=c.State.Health[1,i]=1;c.State.Rungs[i]=-1;}
    c.State.Power[0,1]=scenario==1?2:1;
    if(scenario==0){c.State.Rungs[2]=1;c.State.Rungs[0]=0;}
    if(scenario==1)c.State.Power[1,1]=1;
    if(scenario==2)c.State.Hidden.Add(new Secret(1,Card.Firewall,1));
    if(scenario==3)c.State.Power[1,1]=1;
    if(scenario==4){c.State.Power[1,1]=1;c.State.Pierce[0,1]=true;}
    if(scenario==5)c.State.Health[1,1]=0;
    c.Render();var report=c.State.Resolve();c.Render(); // Restore pre-impact server shells for the fixture.
    foreach(var server in c.servers)server.Render(report.Before[server.owner,server.channel],report.Packets.Where(p=>p.Owner==server.owner&&p.Start==server.channel).Select(p=>p.Initial).FirstOrDefault());
    bool done=false;StartCoroutine(Run(c.presentation.Play(report),()=>done=true));
    yield return Await(()=>c.presentation.Phase=="charge"&&c.presentation.Progress>.55f,"charge");if(failed)yield break;yield return Shot("attack-"+scenario+"-charge");
    yield return Await(()=>c.presentation.Phase=="flight"&&c.presentation.Progress>.25f,"flight");if(failed)yield break;yield return Shot("attack-"+scenario+"-moving");
    if(c.presentation.CameraZoom<=1||fixedHead.anchoredPosition!=headOrigin){Fail("Camera focus or fixed heading changed");yield break;}
    foreach(var flare in c.game.GetComponentsInChildren<FlareGraphic>())if(Mathf.Abs(Mathf.DeltaAngle(0,flare.transform.eulerAngles.z))>.01f){Fail("Bloom rotated");yield break;}
    yield return Await(()=>c.presentation.Phase=="flight"&&c.presentation.Progress>.55f,"contact");if(failed)yield break;yield return Shot("attack-"+scenario+"-contact");
    yield return Await(()=>done,"return");if(failed)yield break;yield return null;
    if(c.presentation.CameraZoom!=1||c.presentation.CameraOffset!=Vector2.zero||((RectTransform)c.endButton.transform).anchoredPosition!=button||c.hand.localScale!=hand){Fail("Camera or HUD did not restore");yield break;}
   }
   bool wallDone=false;StartCoroutine(Run(c.feedback.Applied(Card.Firewall,0,1,true),()=>wallDone=true));
   yield return Await(()=>c.feedback.Phase=="applied"&&c.feedback.Progress>.08f,"wall open");if(failed)yield break;
   var wall=BattleController.Find(c.fx,"반도체 방화벽") as RectTransform;
   if(!wall||((RectTransform)wall.GetChild(0)).anchoredPosition.x>=-15||wall.GetChild(2).gameObject.activeSelf){Fail("Firewall did not prepare open");yield break;}yield return Shot("firewall-open");
   yield return Await(()=>c.feedback.Phase=="applied"&&c.feedback.Progress>.6f,"wall close");if(failed)yield break;
   if(Mathf.Abs(((RectTransform)wall.GetChild(0)).anchoredPosition.x)>.1f||!wall.GetChild(2).gameObject.activeSelf){Fail("Firewall did not lock");yield break;}yield return Shot("firewall-locked");yield return Await(()=>wallDone,"wall cleanup");if(failed)yield break;
   bool absorbed=false;c.feedback.speed=1;StartCoroutine(Run(c.feedback.Play(Card.Inject,0,new Vector2(850,860),new Vector2(126,201.6f)),()=>absorbed=true));
   yield return Await(()=>c.feedback.Phase=="absorb"&&c.feedback.Progress>.25f,"absorption");if(failed)yield break;
   var glows=c.fx.GetComponentsInChildren<Shape>().Where(x=>x.name=="전체 테두리 글로우").ToArray();if(glows.Length!=3||glows.Any(x=>x.color.a>0)){Fail("Whole glow before both branches arrived");yield break;}
   yield return Shot("absorb-branches");yield return Await(()=>c.feedback.Phase=="absorb"&&c.feedback.Progress>.65f,"whole glow");if(failed)yield break;yield return Shot("absorb-whole");
   if(!glows.Any(x=>x.color.a>.1f)){Fail("Missing whole glow");yield break;}yield return Await(()=>absorbed,"absorb cleanup");if(failed)yield break;
   bool changed=false,turnDone=false;var crt=FindFirstObjectByType<CrtOverlay>();StartCoroutine(Run(c.feedback.Turn(1,1,0,()=>changed=true),()=>turnDone=true));
   yield return Await(()=>c.feedback.Phase=="turn"&&c.feedback.Progress>.41f,"VHS peak");if(failed)yield break;yield return Shot("vhs-peak");
   if(!changed||!crt||crt.Burst<.9f){Fail("VHS peak or turn callback missing");yield break;}yield return Await(()=>turnDone,"VHS recovery");if(failed)yield break;yield return null;yield return Shot("vhs-recovered");
   if(crt.Burst!=0){Fail("VHS remained active");yield break;}
   File.WriteAllText(Path.Combine(dir,"week9-result.txt"),"PASS: 6 fixed attack fixtures (hit, partial cancel, wall, full cancel, pierce, offline), camera focus and restore, fixed HUD and headings, horizontal bloom, firewall open-to-lock, both absorption branches before full glow, VHS peak callback and recovery");Application.Quit(0);
  }
  IEnumerator Run(IEnumerator routine,Action done){yield return routine;done();}
  IEnumerator Await(Func<bool> test,string label){float end=Time.realtimeSinceStartup+20;while(!test()&&Time.realtimeSinceStartup<end)yield return null;if(!test())Fail("Timeout "+label);}
  IEnumerator Shot(string name){
   // PNG encoding blocks the main thread. Slow only the capture interval so the next short phase is not skipped.
   float attackSpeed=c.animationSpeed,cardSpeed=c.feedback.speed;c.animationSpeed=c.feedback.speed=.1f;
   yield return new WaitForEndOfFrame();var canvas=c.stage.GetComponentInParent<Canvas>();var camera=Camera.main;var texture=new RenderTexture(1600,900,24);var mode=canvas.renderMode;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=texture;Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;canvas.renderMode=mode;Destroy(image);texture.Release();Destroy(texture);
   yield return null;yield return new WaitForEndOfFrame();c.animationSpeed=attackSpeed;c.feedback.speed=cardSpeed;
  }
  void Fail(string reason){failed=true;File.WriteAllText(Path.Combine(dir,"week9-result.txt"),"FAIL: "+reason);Application.Quit(1);}
 }
}

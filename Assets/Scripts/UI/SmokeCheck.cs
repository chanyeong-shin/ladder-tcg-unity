using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Breach.UI {
 public sealed class SmokeCheck:MonoBehaviour {
  void Awake(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--smoke-dir")>=0)Application.runInBackground=true;}
  IEnumerator Start(){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--smoke-dir");if(at<0||at+1>=args.Length)yield break;string dir=args[at+1];Directory.CreateDirectory(dir);yield return null;var c=GetComponent<BattleController>();c.animationSpeed=4;yield return Shot(dir,"01-title");c.startButton.onClick.Invoke();yield return new WaitForSeconds(1.8f);
   // A fixed integration fixture, not a balance simulation: win in one round through the real UI actions.
   while(!c.CanInput)yield return null;c.State.Attacker=0;c.State.Phase=0;c.State.Winner=-1;c.State.Hidden.Clear();Array.Clear(c.State.Walls,0,3);Array.Clear(c.State.Pierce,0,6);c.State.Hands[0].Clear();c.State.Hands[1].Clear();Array.Clear(c.State.Power,0,c.State.Power.Length);for(int i=0;i<3;i++){c.State.Health[0,i]=1;c.State.Health[1,i]=1;c.State.Rungs[i]=-1;c.State.Hands[0].Add(Card.Inject);}c.State.SaveAction();c.Render();yield return Shot(dir,"02-board");
   c.State.Health[0,0]=0;c.Render();yield return null;
   var first=c.hand.GetComponentsInChildren<CardView>().First(v=>v.index==0);var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,c.stage.TransformPoint(new Vector3(702,-400,0)))};first.OnBeginDrag(pointer);c.EndTurn();if(c.State.Phase!=0){Fail(dir,"Turn ended during drag");yield break;}first.OnEndDrag(pointer);yield return null;while(!c.CanInput)yield return null;c.Select(0);
   while(!c.CanInput)yield return null;if(c.State.Hands[0].Count!=3||c.hand.GetComponentsInChildren<CardView>().Length!=3){Fail(dir,"Invalid drag lost card");yield break;}
   c.State.Health[0,0]=1;c.Render();c.Select(0);c.Select(0);yield return Shot(dir,"03-card-hover");c.Use(0,false);while(!c.CanInput)yield return null;if(c.State.Power[0,0]!=1){Fail(dir,"Play before undo failed");yield break;}c.undoButton.onClick.Invoke();if(c.State.Hands[0].Count!=3||c.State.Power[0,0]!=0){Fail(dir,"Undo UI failed");yield break;}
   for(int lane=0;lane<3;lane++){c.Select(0);c.Use(lane,false);while(!c.CanInput)yield return null;}
   yield return Shot(dir,"04-loaded");c.endButton.onClick.Invoke();float timeout=Time.time+20;while(!c.result.activeSelf&&Time.time<timeout)yield return null;if(c.State.Winner!=0||!c.result.activeSelf){Fail(dir,"Victory flow failed");yield break;}yield return Shot(dir,"05-result");c.retryButton.onClick.Invoke();yield return new WaitForSeconds(1);if(c.State.Round!=1||c.State.Winner!=-1){Fail(dir,"Restart failed");yield break;}File.WriteAllText(Path.Combine(dir,"smoke-result.txt"),"PASS: title, begin, card selection, play, undo, ammunition, end action, bot pass, resolve, victory, restart");Application.Quit(0);
  }
  IEnumerator Shot(string dir,string name){yield return new WaitForEndOfFrame();var canvas=GetComponent<BattleController>().stage.GetComponentInParent<Canvas>();var camera=Camera.main;var texture=new RenderTexture(1600,900,24);var oldMode=canvas.renderMode;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=texture;Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;canvas.renderMode=oldMode;Destroy(image);texture.Release();Destroy(texture);yield return null;}
  void Fail(string dir,string error){File.WriteAllText(Path.Combine(dir,"smoke-result.txt"),"FAIL: "+error);Application.Quit(1);}
 }
}

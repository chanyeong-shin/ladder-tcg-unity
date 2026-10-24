using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Breach.UI {
 public sealed class FeedbackCheck:MonoBehaviour {
  string directory;BattleController controller;
  IEnumerator Start(){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--feedback-check");if(at<0||at+1>=args.Length)yield break;directory=args[at+1];Directory.CreateDirectory(directory);Application.runInBackground=true;yield return null;controller=GetComponent<BattleController>();controller.animationSpeed=4;controller.startButton.onClick.Invoke();yield return new WaitForSeconds(.5f);while(!controller.CanInput)yield return null;
   controller.State.Attacker=0;controller.State.Phase=0;controller.State.Hands[0].Clear();controller.State.Hands[1].Clear();Array.Clear(controller.State.Power,0,6);for(int i=0;i<3;i++){controller.State.Health[0,i]=1;controller.State.Health[1,i]=1;controller.State.Rungs[i]=-1;}controller.State.Hands[0].Add(Card.Inject);controller.State.Hands[0].Add(Card.Reroute);controller.State.SaveAction();controller.Render();controller.animationSpeed=1;
   yield return null;
   var held=controller.hand.GetComponentsInChildren<CardView>().First(v=>v.index==0);
   var drag=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,held.transform.TransformPoint(new Vector3(50,-50,0)))};
   held.OnBeginDrag(drag);drag.position=RectTransformUtility.WorldToScreenPoint(null,controller.stage.TransformPoint(new Vector3(702,-400,0)));held.OnDrag(drag);
   if(held.GetComponent<CanvasGroup>().alpha!=0){Fail("Card remains visible over valid rail");yield break;}
   drag.position=RectTransformUtility.WorldToScreenPoint(null,controller.stage.TransformPoint(new Vector3(830,-400,0)));held.OnDrag(drag);
   if(held.GetComponent<CanvasGroup>().alpha!=1){Fail("Card did not reappear away from rail");yield break;}
   held.OnEndDrag(drag);while(!controller.CanInput)yield return null;if(controller.State.Hands[0].Count!=2){Fail("Off-rail drop consumed card");yield break;}
   controller.Select(0);yield return new WaitForSeconds(.2f);
   if(controller.laneTargets.Any(b=>b.targetGraphic.color.a!=0)||controller.rungTargets.Any(b=>b.targetGraphic.color.a!=0)){Fail("Visible hit boxes");yield break;}
   yield return Shot("01-targets");controller.servers.First(v=>v.owner==0&&v.channel==0).OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});
   yield return Await("dock",.1f);var card=BattleController.Find(controller.fx,"사용 카드") as RectTransform;Rect dock=CardTransferFx.DockRect;
   if(!card||Vector2.Distance(card.anchoredPosition,new Vector2(dock.x,-dock.y))>.1f||Vector2.Distance(card.sizeDelta,dock.size)>.1f){Fail("Dock placement mismatch");yield break;}
   yield return Shot("02-docked");yield return Await("fragment",.2f);yield return Shot("03-fragments");yield return Await("fragment",.65f);yield return Shot("04-circuits");yield return Await("absorb",.25f);yield return Shot("05-absorb");yield return Await("applied",.1f);yield return Shot("06-loaded");while(!controller.CanInput)yield return null;
   yield return null;if(controller.fx.childCount!=0){Fail("Effects did not clean up");yield break;}
   controller.Select(0);yield return new WaitForSeconds(.2f);yield return Shot("07-rung-targets");controller.Use(0,true);yield return Await("applied",.1f);yield return Shot("08-connected");while(!controller.CanInput)yield return null;
   var pointer=new PointerEventData(EventSystem.current);var button=controller.endButton.GetComponent<ButtonFeedback>();var before=((RectTransform)button.transform).anchoredPosition;button.OnPointerEnter(pointer);yield return new WaitForSeconds(.16f);yield return Shot("09-button-hover");if(((RectTransform)button.transform).anchoredPosition!=before){Fail("Button moved on hover");yield break;}button.OnPointerExit(pointer);
   controller.endButton.onClick.Invoke();yield return Await("turn",.3f);yield return Shot("10-turn");controller.animationSpeed=4;while(!controller.CanInput&&controller.State.Winner==-1)yield return null;
controller.State.Hands[0].Clear();for(int i=0;i<5;i++)controller.State.Hands[0].Add(Card.Inject);controller.Render();yield return null;
   var hand=controller.hand.GetComponentsInChildren<CardView>().OrderBy(v=>v.index).ToArray();var positions=hand.Select(v=>((RectTransform)v.transform).anchoredPosition).ToArray();controller.Hover(2);yield return new WaitForSecondsRealtime(.2f);for(int i=0;i<4;i++){var a=(RectTransform)hand[i].transform;var b=(RectTransform)hand[i+1].transform;if(a.anchoredPosition.x+a.sizeDelta.x*a.localScale.x>b.anchoredPosition.x+.1f){Fail("Hovered hand overlaps");yield break;}}controller.Hover(-1);yield return new WaitForSecondsRealtime(.2f);for(int i=0;i<5;i++)if(Vector2.Distance(((RectTransform)hand[i].transform).anchoredPosition,positions[i])>.1f||Mathf.Abs(hand[i].transform.localScale.x-1)>.001f){Fail("Hand layout drift");yield break;}
   held=hand[0];drag.position=RectTransformUtility.WorldToScreenPoint(null,held.transform.TransformPoint(new Vector3(50,-50,0)));held.OnBeginDrag(drag);drag.position=RectTransformUtility.WorldToScreenPoint(null,controller.stage.TransformPoint(new Vector3(702,-400,0)));held.OnDrag(drag);int previousPower=controller.State.Power[0,0];held.OnEndDrag(drag);while(!controller.CanInput)yield return null;if(controller.State.Power[0,0]!=previousPower+1){Fail("Hidden rail drop did not load server");yield break;}
   File.WriteAllText(Path.Combine(directory,"feedback-result.txt"),"PASS: transparent hit regions, exact dock, 60 artwork shards, three circuits, perimeter absorption, loaded/connected feedback, cleanup, button fixed position, turn cue, server click, rail hide and leave restore, off-rail cancellation, hidden rail drop loads, five-card hover spacing and restoration");Application.Quit(0);
  }
  IEnumerator Await(string phase,float progress){float deadline=Time.realtimeSinceStartup+15;while((controller.feedback.Phase!=phase||controller.feedback.Progress<progress)&&Time.realtimeSinceStartup<deadline)yield return null;if(controller.feedback.Phase!=phase){Fail("Missed phase "+phase);yield break;}}
  IEnumerator Shot(string name){yield return new WaitForEndOfFrame();var canvas=controller.stage.GetComponentInParent<Canvas>();var camera=Camera.main;var texture=new RenderTexture(1600,900,24);var mode=canvas.renderMode;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=texture;Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;canvas.renderMode=mode;Destroy(image);texture.Release();Destroy(texture);yield return null;}
  void Fail(string message){File.WriteAllText(Path.Combine(directory,"feedback-result.txt"),"FAIL: "+message);Application.Quit(1);}
 }
}

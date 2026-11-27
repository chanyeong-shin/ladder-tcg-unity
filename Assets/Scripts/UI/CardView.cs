using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Breach.UI {
 public sealed class CardView:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler {
  public Card card;[System.NonSerialized]public BattleController controller;[System.NonSerialized]public int index;bool dragging;Vector2 grabLocal;PointerEventData pointer;int previewTarget=-1;float lift=1,over;
  public void OnPointerEnter(PointerEventData e){if(controller&&!dragging)controller.Hover(index);}
  public void OnPointerExit(PointerEventData e){if(controller&&!dragging)controller.Hover(-1);}
  // Week 12 item 4 (04_zoom.js "keyword", 2nd review 11-26): one click does nothing (no card lock); double-click or right-click zooms.
  public void OnPointerClick(PointerEventData e){if(!controller||dragging)return;if(e.button==PointerEventData.InputButton.Right||e.button==PointerEventData.InputButton.Left&&e.clickCount>=2)controller.OpenZoom(index);}
  public void OnBeginDrag(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left||!controller||!controller.CanInput)return;RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,e.position,e.pressEventCamera,out grabLocal);lift=transform.localScale.x;over=0;dragging=true;controller.BeginDrag(this);OnDrag(e);}
  public void OnDrag(PointerEventData e){if(!dragging)return;pointer=e;Place();int target=controller.AmmoDropTarget(e.position);if(target!=previewTarget){previewTarget=target;if(target>=0)controller.Preview(target,false);else controller.ClearPreview();}}
  // Week 12 item 8 (08_drag.js "small"): over the board the card shrinks to .6x (scale 1 - .4 over); week 13 (05_drop.js mkMix: card a .75) opacity 1 - .25 over (was 40%);
  // On the board the size is the HTML's (.6 x 126 = 75.6 px wide); off the board the card keeps Unity's 1.6x lift (the HTML drags at 1x).
  // 'over' eases in .12 s (08_drag.js "half": "0.12초에 걸쳐"). The lift to the hover size (1.6x) is unchanged.
  void Update(){if(!dragging)return;lift=Mathf.MoveTowards(lift,1.6f,Time.unscaledDeltaTime*4);bool onBoard=pointer!=null&&controller.OverBoard(pointer.position);over=Mathf.MoveTowards(over,onBoard?1:0,Time.unscaledDeltaTime/.12f);transform.localScale=Vector3.one*Mathf.Lerp(lift,1-.4f*1,over);var group=GetComponent<CanvasGroup>();if(group)group.alpha=1-.25f*over;Place();}
  public float DragOver{get{return over;}}
  void Place(){if(pointer==null)return;Vector2 p;RectTransformUtility.ScreenPointToLocalPointInRectangle(controller.stage,pointer.position,pointer.pressEventCamera,out p);((RectTransform)transform).anchoredPosition=p-grabLocal*transform.localScale.x;}
  public void OnEndDrag(PointerEventData e){if(!dragging)return;dragging=false;previewTarget=-1;over=0;var group=GetComponent<CanvasGroup>();if(group)group.alpha=1;controller.Drop(e.position);}
 }
}

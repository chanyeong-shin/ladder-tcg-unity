using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Breach.UI {
 public sealed class CardView:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler {
  public Card card;[System.NonSerialized]public BattleController controller;[System.NonSerialized]public int index;bool dragging;Vector2 grabLocal;PointerEventData pointer;
  public void OnPointerEnter(PointerEventData e){if(controller&&!dragging)controller.Hover(index);}
  public void OnPointerExit(PointerEventData e){if(controller&&!dragging)controller.Hover(-1);}
  public void OnPointerClick(PointerEventData e){if(controller&&!dragging)controller.Select(index);}
  public void OnBeginDrag(PointerEventData e){if(!controller||!controller.CanInput)return;RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,e.position,e.pressEventCamera,out grabLocal);dragging=true;controller.BeginDrag(this);OnDrag(e);}
  public void OnDrag(PointerEventData e){if(!dragging)return;pointer=e;Place();}
  void Update(){if(!dragging)return;float scale=Mathf.MoveTowards(transform.localScale.x,1.6f,Time.unscaledDeltaTime*4);transform.localScale=Vector3.one*scale;Place();}
  void Place(){if(pointer==null)return;Vector2 p;RectTransformUtility.ScreenPointToLocalPointInRectangle(controller.stage,pointer.position,pointer.pressEventCamera,out p);((RectTransform)transform).anchoredPosition=p-grabLocal*transform.localScale.x;}
  public void OnEndDrag(PointerEventData e){if(!dragging)return;dragging=false;controller.Drop(e.position);}
 }
}

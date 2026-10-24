using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Breach.UI {
 public sealed class CardView:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler {
  public Card card;[System.NonSerialized]public BattleController controller;[System.NonSerialized]public int index;bool dragging;
  public void OnPointerEnter(PointerEventData e){if(controller&&!dragging)controller.Hover(index);}
  public void OnPointerExit(PointerEventData e){if(controller&&!dragging)controller.Hover(-1);}
  public void OnPointerClick(PointerEventData e){if(controller&&!dragging)controller.Select(index);}
  public void OnBeginDrag(PointerEventData e){if(!controller||!controller.CanInput)return;dragging=true;controller.BeginDrag(this);OnDrag(e);}
  public void OnDrag(PointerEventData e){if(!dragging)return;Vector2 p;RectTransformUtility.ScreenPointToLocalPointInRectangle(controller.stage,e.position,e.pressEventCamera,out p);((RectTransform)transform).anchoredPosition=new Vector2(p.x-100,p.y+270);}
  public void OnEndDrag(PointerEventData e){if(!dragging)return;dragging=false;controller.Drop(e.position);}
 }
}

using UnityEngine;
using UnityEngine.EventSystems;
namespace Breach.UI {
 public sealed class TargetView:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler {
  public BattleController controller;public int target;public bool rung;
  public void OnPointerEnter(PointerEventData e){controller.Preview(target,rung);}
  public void OnPointerExit(PointerEventData e){controller.ClearPreview();}
 }
}

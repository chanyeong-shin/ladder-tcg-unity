using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
namespace Breach.UI {
 public sealed class ServerView:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler {
  public BattleController controller;
  public GameObject normal,empty,offline;public TMP_Text number;public int owner,channel;
  public void Render(int hp,int power){normal.SetActive(hp>0&&power>0);empty.SetActive(hp>0&&power==0);offline.SetActive(hp<=0);number.text=power.ToString();}
  public void OnPointerEnter(PointerEventData e){if(controller)controller.PreviewServer(owner,channel);}
  public void OnPointerExit(PointerEventData e){if(controller)controller.ClearPreview();}
 }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Breach.UI {
 public sealed class ButtonFeedback:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler {
  Button button;Graphic body;TMP_Text text;Shape edge;Color baseColor,textColor;bool hover,focus,pressed,lastEnabled;Coroutine tween;
  void Awake(){button=GetComponent<Button>();body=button.targetGraphic;text=GetComponentInChildren<TMP_Text>();baseColor=body.color;textColor=text?text.color:Color.white;button.transition=Selectable.Transition.None;var r=(RectTransform)transform;edge=Ui.Box("버튼 반응 테두리",transform,1,1,r.rect.width-2,r.rect.height-2,Color.clear,false,1.5f);lastEnabled=button.interactable;Refresh();}
  void Update(){if(lastEnabled!=button.interactable){lastEnabled=button.interactable;Refresh();}}
  void Refresh(){if(!isActiveAndEnabled)return;if(tween!=null)StopCoroutine(tween);tween=StartCoroutine(Blend());}
  IEnumerator Blend(){bool active=button.interactable&&(hover||focus);Color b=button.interactable?(active?Color.Lerp(baseColor,pressed?Ui.Gold:Ui.Cyan,pressed?.35f:.18f):baseColor):new Color(.07f,.09f,.1f,1);Color e=active?(pressed?Ui.Gold:Ui.Cyan):Color.clear;Color tc=button.interactable?(active?Color.white:textColor):new Color(.32f,.39f,.42f);Color start=body.color,outline=edge.color,fg=text?text.color:Color.white;for(float t=0;t<1;t+=Time.unscaledDeltaTime/.12f){body.color=Color.Lerp(start,b,t);edge.color=Color.Lerp(outline,e,t);if(text)text.color=Color.Lerp(fg,tc,t);yield return null;}body.color=b;edge.color=e;if(text)text.color=tc;}
  public void OnPointerEnter(PointerEventData e){hover=true;Refresh();}public void OnPointerExit(PointerEventData e){hover=false;pressed=false;Refresh();}
  public void OnPointerDown(PointerEventData e){pressed=true;Refresh();}public void OnPointerUp(PointerEventData e){pressed=false;Refresh();}
  public void OnSelect(BaseEventData e){focus=true;Refresh();}public void OnDeselect(BaseEventData e){focus=false;Refresh();}
 }
}

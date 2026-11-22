using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breach.UI {
 // Result buttons (user 11-22): a vertical bloom behind each button that breathes slowly and brightens on hover.
 public sealed class ResultButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
  FlareGraphic bloom; bool over; float level;
  void Awake() {
   var r = (RectTransform)transform; var center = new Vector2(r.anchoredPosition.x + r.sizeDelta.x / 2, -r.anchoredPosition.y + r.sizeDelta.y / 2);
   bloom = CombatPresentation.Flare(transform.parent, name + " 세로 빛번짐", center, Ui.Cyan, 1.1f);
   bloom.rectTransform.pivot = new Vector2(.5f, .5f); bloom.rectTransform.anchoredPosition = new Vector2(center.x, -center.y); bloom.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
   bloom.transform.SetSiblingIndex(transform.GetSiblingIndex());
   // user 11-22 "마지막 화면도 가독성 떨어짐": the label sits on a dark plate so the bloom never crosses the text
   plate=Ui.Box(name+" 판",transform.parent,r.anchoredPosition.x-24,-r.anchoredPosition.y-6,r.sizeDelta.x+48,r.sizeDelta.y+12,new Color(.02f,.035f,.05f,.94f));
   border=Ui.Box(name+" 테두리",transform.parent,r.anchoredPosition.x-24,-r.anchoredPosition.y-6,r.sizeDelta.x+48,r.sizeDelta.y+12,new Color(Ui.Cyan.r,Ui.Cyan.g,Ui.Cyan.b,.35f),false,1.5f);
   plate.transform.SetSiblingIndex(transform.GetSiblingIndex());border.transform.SetSiblingIndex(transform.GetSiblingIndex());
  }
  Shape plate,border;
  public void OnPointerEnter(PointerEventData e) { over = true; }
  public void OnPointerExit(PointerEventData e) { over = false; }
  void OnDisable() { over = false; }
  void Update() {
   if (!bloom) return; level = Mathf.MoveTowards(level, over ? 1 : 0, Time.unscaledDeltaTime / .12f);
   float breathe = .5f + .5f * Mathf.Sin(Time.unscaledTime * 2.2f);
   var c = Ui.Cyan; c.a = Mathf.Lerp(.12f + .08f * breathe, .6f, level); bloom.color = c;
   if (border) { var b = Ui.Cyan; b.a = Mathf.Lerp(.35f, 1, level); border.color = b; }
   bloom.rectTransform.localScale = Vector3.one * Mathf.Lerp(1, 1.2f, level);
   foreach (var g in GetComponentsInChildren<Graphic>()) if (g is TMPro.TMP_Text t) t.color = Color.Lerp(new Color(.82f, .93f, .97f), Color.white, level);
  }
 }
}

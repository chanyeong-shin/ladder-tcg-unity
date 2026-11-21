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
  }
  public void OnPointerEnter(PointerEventData e) { over = true; }
  public void OnPointerExit(PointerEventData e) { over = false; }
  void OnDisable() { over = false; }
  void Update() {
   if (!bloom) return; level = Mathf.MoveTowards(level, over ? 1 : 0, Time.unscaledDeltaTime / .12f);
   float breathe = .5f + .5f * Mathf.Sin(Time.unscaledTime * 2.2f);
   var c = Ui.Cyan; c.a = Mathf.Lerp(.25f + .15f * breathe, .95f, level); bloom.color = c;
   bloom.rectTransform.localScale = Vector3.one * Mathf.Lerp(1, 1.2f, level);
   foreach (var g in GetComponentsInChildren<Graphic>()) if (g is TMPro.TMP_Text t) t.color = Color.Lerp(new Color(.82f, .93f, .97f), Color.white, level);
  }
 }
}

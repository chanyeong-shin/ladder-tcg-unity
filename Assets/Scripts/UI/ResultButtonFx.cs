using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breach.UI {
 // Result buttons (user 11-22): a vertical bloom behind each button that breathes slowly and brightens on hover.
 // Week 12 (10_resultlog.js result(), 2nd review 11-26 "인게임 설정(ESC) 고쳐진 것처럼 빛나는 선 문제 고치기"): every dark oval (540 x 125)
 // is laid first, then each bloom with only its own label area (540 x 79) cut out, so all three blooms show above and below.
 public sealed class ResultButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
  // seconds since the result screen started to appear (the HTML variant clock t; the breathing phase uses it)
  public static float Clock; static int frame = -1;
  CutFlare bloom; RectTransform shade; bool over; float level;
  public bool ForceOver; public float Level { get { return level; } }
  void Awake() {
   var r = (RectTransform)transform; var parent = transform.parent;
   // shared layers right under the first button: ovals, then blooms, then the buttons' labels
   var ovals = parent.Find("결과 버튼 어둠 층") as RectTransform; var blooms = parent.Find("결과 버튼 빛 층") as RectTransform;
   if (!ovals) { ovals = Ui.Rect("결과 버튼 어둠 층", parent, 0, 0, 1920, 1080); blooms = Ui.Rect("결과 버튼 빛 층", parent, 0, 0, 1920, 1080); int at = r.GetSiblingIndex(); ovals.SetSiblingIndex(at); blooms.SetSiblingIndex(at + 1); }
   // user 11-22 "마지막 화면도 가독성 떨어짐" / "네모만 없었으면 완벽했다": a soft dark oval (no edge) behind the label
   shade = Ui.Rect(name + " 글자 뒤 어둠", ovals, 0, 0, r.sizeDelta.x + 160, r.sizeDelta.y + 60); shade.pivot = new Vector2(.5f, .5f);
   var disc = shade.gameObject.AddComponent<SoftDisc>(); disc.Solid = .55f; disc.color = new Color(3 / 255f, 5 / 255f, 8 / 255f, .92f); disc.raycastTarget = false;
   var b = Ui.Rect(name + " 세로 빛번짐", blooms, 0, 0, 640, 320); b.pivot = new Vector2(.5f, .5f);
   bloom = b.gameObject.AddComponent<CutFlare>(); bloom.raycastTarget = false; bloom.Vertical = true; bloom.CutW = r.sizeDelta.x + 160; bloom.CutH = r.sizeDelta.y + 14;
   var fb = GetComponent<ButtonFeedback>(); if (fb) fb.enabled = false; // no square on hover (user 11-26 "네모 생기는 건 없에고 싶음")
   Place();
  }
  void Place() {
   var r = (RectTransform)transform; var center = new Vector2(r.anchoredPosition.x + r.sizeDelta.x / 2, -r.anchoredPosition.y + r.sizeDelta.y / 2);
   shade.anchoredPosition = new Vector2(center.x, -center.y); bloom.rectTransform.anchoredPosition = new Vector2(center.x, -center.y);
  }
  public void OnPointerEnter(PointerEventData e) { over = true; }
  public void OnPointerExit(PointerEventData e) { over = false; }
  void OnDisable() { over = false; }
  void Update() {
   if (!bloom) return; Place(); float dt = Time.unscaledDeltaTime * FxClock.Scale; if (Time.frameCount != frame) { frame = Time.frameCount; Clock += dt; }
   level = Css.Toward(level, over || ForceOver ? 1 : 0, dt / .12f);
   float breathe = .5f + .5f * Mathf.Sin(Clock * 2.2f), a = (.12f + .08f * breathe) + (.6f - (.12f + .08f * breathe)) * level, s = 1 + .2f * level;
   var c = Ui.Cyan; c.a = a; bloom.Set(209 * s, 57.2f * s, c);
   foreach (var g in GetComponentsInChildren<Graphic>()) if (g is TMPro.TMP_Text t) t.color = Color.Lerp(Css.Rgb(209, 237, 247), Color.white, level);
  }
 }
}

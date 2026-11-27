using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breach.UI {
 // Week 12 item 5 (_도구/시안12주차/tabs/05_deck.js "carousel"): click my draw deck -> the screen dims to 60% black, the six cards fly
 // out of the deck into a rotating row (center 340 x 544, sides .62 / .42), the right panel shows the center card's name, English name,
 // next-draw chance and "n장째 안 나옴". Rotate: click a side card, click left/right of the center, or the wheel (.18 s apart).
 // After 6 s without input it turns one step every 2.5 s. Close: Esc or right-click (.3 s), reopen: click the deck.
 // Chances are the current rule (fixed weights 10·5·3·1·1·1 / 21, Battle.Draw); the mockup's 'C' ceiling rule is undecided and not used.
 // "n장째 안 나옴" and the history strip count Battle.Drawn[0] (cards that actually entered my hand).
 // Clock t = the mockup's: the deck click is t = .8 (its cursor walk 0-.8 is not drawn), dim .8-1.1, cards 1.0-1.7, info from 1.6.
 public sealed class DeckView : MonoBehaviour {
  public static readonly Rect Deck = new Rect(1542, 390, 120, 192);
  static readonly string[] EN = { "INJECT", "REROUTE", "BUFFER", "ZERO-DAY", "FIREWALL", "OVERFLOW" };
  static readonly Color Cyan = Css.Rgb(56, 217, 255), Gold = Css.Rgb(255, 196, 56), Text = Css.Rgb(179, 209, 219), Dim = Css.Rgb(110, 150, 165), Panel = Css.Rgb(6, 10, 14, .96f), Line = Css.Rgb(51, 102, 115);
  static Color Acc(int i) { return i >= 3 ? Gold : Cyan; }
  static readonly Dictionary<int, float> CX = new Dictionary<int, float> { { -2, 140 }, { -1, 320 }, { 0, 620 }, { 1, 920 }, { 2, 1100 } };
  const float Idle = 6, Auto = 2.5f;
  BattleController c; RectTransform root, layer, strip; Image dim; CanvasGroup layerGroup, right; TMP_Text nm, pc, ms;
  readonly Image[] cards = new Image[6]; readonly Rect[] from = new Rect[6], to = new Rect[6], shown = new Rect[6]; readonly float[] moveT = new float[6];
  bool open; float t, closeT = -1, wheelT = -9, next = 3; int focus = 3;
  public bool IsOpen { get { return open; } }
  public int Focus { get { return focus; } }
  public float Time { get { return t; } }
  public float Amount { get; private set; }
  public string PanelText { get { return nm ? nm.text + " | " + pc.text + " | " + ms.text : ""; } }

  public void Initialize(BattleController controller) {
   c = controller;
   // the click target over my deck (05_deck.js deckHot: 10 px around the deck)
   var hot = Ui.Rect("내 드로우 덱 - 누르면 덱 보기", c.game.transform, Deck.x - 10, Deck.y - 10, Deck.width + 20, Deck.height + 20);
   var hi = hot.gameObject.AddComponent<Image>(); hi.color = Color.clear; var hb = hot.gameObject.AddComponent<Button>(); hb.transition = Selectable.Transition.None; hb.targetGraphic = hi; hb.onClick.AddListener(c.OpenDeck);
   hot.SetSiblingIndex(c.backgrounds.Max(b => b.transform.GetSiblingIndex()) + 1);
   root = Ui.Rect("덱 보기", c.stage, 0, 0, 1920, 1080); root.gameObject.SetActive(false);
   dim = root.gameObject.AddComponent<Image>(); dim.color = Css.Rgb(1, 3, 6, 0); dim.raycastTarget = true; root.gameObject.AddComponent<DeckCatcher>().deck = this;
   layer = Ui.Rect("덱 보기 - 내용", root, 0, 0, 1920, 1080); layerGroup = layer.gameObject.AddComponent<CanvasGroup>(); layerGroup.blocksRaycasts = false;
   // header and history strip get their own dark backing (the board shows through the 60% dim)
   var bands = FxPainter.Create("위아래 어두운 띠", layer); bands.Begin();
   Color dark = Css.Rgb(1, 3, 6, .9f), clear = Css.Rgb(1, 3, 6, 0);
   bands.FillRect(0, 0, 1920, 130 * .55f, dark); bands.Line(new Vector2(960, 130 * .55f), new Vector2(960, 130), 1920, dark, clear);
   bands.FillRect(0, 1080 - 200 * .55f, 1920, 200 * .55f, dark); bands.Line(new Vector2(960, 1080 - 200 * .55f), new Vector2(960, 880), 1920, dark, clear); bands.End();
   Base(layer, "제목", 80, 71, 1000, 40, Cyan, "내 드로우 덱 / DRAW DECK");
   Base(layer, "설명", 80, 110, 1000, 22, Dim, "무한 덱 · 6종 · 오른쪽 숫자는 가운데 카드가 다음 한 장으로 나올 확률");
   Base(layer, "기록 제목", 80, 928, 600, 20, Dim, "최근 뽑은 카드 (오른쪽이 최신)");
   strip = Ui.Rect("최근 뽑은 카드", layer, 0, 0, 1920, 1080);
   // right panel 1240,300 560x360: fill .96, 1 px line, 18 px corner brackets (3 px cyan) 2 px outside
   var rp = Ui.Rect("오른쪽 창", layer, 0, 0, 1920, 1080); right = rp.gameObject.AddComponent<CanvasGroup>(); right.blocksRaycasts = false;
   Ui.Box("창 바탕", rp, 1240, 300, 560, 360, Panel); Ui.Box("창 테두리", rp, 1240, 300, 560, 360, Line, false, 1);
   foreach (var b in new[] { new Rect(1239, 299, 18, 3), new Rect(1239, 299, 3, 18), new Rect(1783, 299, 18, 3), new Rect(1798, 299, 3, 18), new Rect(1239, 658, 18, 3), new Rect(1239, 643, 3, 18), new Rect(1783, 658, 18, 3), new Rect(1798, 643, 3, 18) })
    Ui.Box("모서리", rp, b.x, b.y, b.width, b.height, Cyan);
   nm = Base(rp, "카드 이름", 1275, 381, 520, 54, Color.white, "");
   pc = Base(rp, "다음 드로우 확률", 1275, 507, 520, 120, Cyan, "");
   ms = Base(rp, "몇 장째 안 나옴", 1275, 594, 520, 34, Text, "");
   for (int i = 0; i < 6; i++) { cards[i] = Ui.Rect("덱 카드 " + Cards.Names[i], layer, 0, 0, 1, 1).gameObject.AddComponent<Image>(); cards[i].sprite = c.feedback.cardImages[i]; cards[i].raycastTarget = false; }
  }
  // text placed by its first baseline (Chrome layout of the mockup; TMP top = baseline - 1.16 em)
  TMP_Text Base(Transform parent, string name, float x, float baseline, float w, float size, Color col, string text) {
   var t = Ui.Text(name, parent, x, baseline - 1.16f * size, w, size * 2, text, c.font, size, col); t.alignment = TextAlignmentOptions.TopLeft; t.textWrappingMode = TextWrappingModes.NoWrap; t.richText = true; return t;
  }

  public void Open() {
   if (open) return; open = true; t = .8f; closeT = -1; next = 3; Amount = 0; root.gameObject.SetActive(true); root.SetAsLastSibling();
   for (int i = 0; i < 6; i++) moveT[i] = -1;
   Refresh(); Step(0);
  }
  public void Close() { if (open && closeT < 0 && t >= 1.6f) closeT = t; }
  public void CloseNow() { open = false; if (root) root.gameObject.SetActive(false); }
  public void Turn(int to, bool user) { focus = ((to % 6) + 6) % 6; Render(); next = t + (user ? Idle : Auto); Retarget(); }
  // CSS transition (.45 s ease) starts from where each card is shown at the moment of the turn
  void Retarget() { if (t <= 2) return; for (int i = 0; i < 6; i++) { var target = Slot(i); if (target != to[i]) { from[i] = shown[i]; moveT[i] = t; to[i] = target; } } }
  // Week12Check: show the mockup's example history (05_deck.js fresh()) instead of the battle's
  public List<Card> HistoryOverride;
  List<Card> History { get { return HistoryOverride ?? (c.State != null ? c.State.Drawn[0] : new List<Card>()); } }
  int Miss(int i) { var h = History; int n = 0; for (int k = h.Count - 1; k >= 0; k--) { if ((int)h[k] == i) return n; n++; } return n; }
  void Render() {
   int i = focus; float p = Battle.Weights[i] / (float)Battle.WeightSum;
   nm.text = Cards.Names[i] + "<space=18><size=26><cspace=3><color=#" + ColorUtility.ToHtmlStringRGB(Acc(i)) + ">" + EN[i] + "</color></cspace></size>";
   pc.text = (p * 100).ToString("0.0") + "%"; pc.color = Acc(i);
   int m = Miss(i); ms.text = m == 0 ? "방금 나옴" : m + "장째 안 나옴";
  }
  void Refresh() {
   for (int k = strip.childCount - 1; k >= 0; k--) Destroy(strip.GetChild(k).gameObject);
   var h = History; int n = Mathf.Min(18, h.Count);
   for (int k = 0; k < n; k++) {
    var card = h[h.Count - n + k]; bool last = k == n - 1; float x = 80 + k * 60, y = 940 + 92 - 86.4f;
    if (last) Ui.Box("최신 테두리", strip, x - 2, y - 2, 58, 90.4f, Acc((int)card));
    var im = Ui.Rect("기록 " + k, strip, x, y, 54, 86.4f).gameObject.AddComponent<Image>(); im.sprite = c.feedback.cardImages[(int)card]; im.raycastTarget = false; im.color = new Color(1, 1, 1, last ? 1 : .55f);
   }
   Render();
  }
  Rect Slot(int i) {
   int d = ((i - focus) % 6 + 6) % 6; if (d > 3) d -= 6; if (d == 3) d = -3;
   if (Mathf.Abs(d) == 3) return new Rect(560, 406, 120, 192);
   float sc = d == 0 ? 1 : Mathf.Abs(d) == 1 ? .62f : .42f, w = 340 * sc, h = 544 * sc; return new Rect(CX[d] - w / 2, 230 + (544 - h) / 2, w, h);
  }
  int Z(int i) { int d = ((i - focus) % 6 + 6) % 6; if (d > 3) d -= 6; if (d == 3) d = -3; return Mathf.Abs(d) == 3 ? 1 : 10 - Mathf.Abs(d); }

  public void HandleKeys() {
   if (Input.GetKeyDown(KeyCode.Escape)) Close();
   if (!open || t < 1.6f || closeT >= 0) return;
   float w = Input.mouseScrollDelta.y;
   if (Mathf.Abs(w) > .01f && UnityEngine.Time.unscaledTime - wheelT >= .18f) { wheelT = UnityEngine.Time.unscaledTime; Turn(focus + (w < 0 ? 1 : -1), true); }
  }
  // the catcher covers the screen: right = close; left = the side card under the pointer, else one step toward the click side
  public void Click(PointerEventData e) {
   if (!open || t < 1.6f || closeT >= 0) return;
   if (e.button == PointerEventData.InputButton.Right) { Close(); return; }
   if (e.button != PointerEventData.InputButton.Left) return;
   Vector2 p; RectTransformUtility.ScreenPointToLocalPointInRectangle(c.stage, e.position, e.pressEventCamera, out p); p.y = -p.y; ClickAt(p);
  }
  public void ClickAt(Vector2 p) {
   if (!open || t < 1.6f || closeT >= 0) return;
   // 11-27 사용자 "뽑을 확률 보여주는 창에서 스크롤 하거나 카드를 클릭해야지 넘어가게 하고 다른 어두워진 부분을 클릭하면 다시 닫게 하고 싶어":
   //  카드를 누르면 그 카드로(가운데 카드는 그대로), 오른쪽 정보 창·아래 최근 기록 줄은 아무 일 없음, 그 밖의 어두운 곳은 닫기
   foreach (int k in Enumerable.Range(0, 6).OrderByDescending(Z)) if (shown[k].Contains(p)) { if (k != focus) Turn(k, true); return; }
   if (new Rect(1238, 298, 564, 364).Contains(p) || p.y >= 930) return;
   Close();
  }
  public void Wheel(int dir) { if (open && t >= 1.6f && closeT < 0) Turn(focus + dir, true); }

  void Update() { if (!open) return; Step(UnityEngine.Time.unscaledDeltaTime * FxClock.Scale); }
  void Step(float dt) {
   t += dt;
   float shut = closeT < 0 ? 0 : Mathf.Clamp01((t - closeT) / .3f);
   if (shut >= 1) { CloseNow(); return; }
   float o = Mathf.Clamp01((t - .8f) / .3f) * (1 - shut); Amount = o;
   dim.color = Css.Rgb(1, 3, 6, .6f * o); layerGroup.alpha = o > 0 ? 1 : 0;
   float info = Mathf.Clamp01((t - 1.6f) / .4f);
   // 11-27 사용자 "뽑을 확률 보여주는 창에서 스크롤 하거나 카드를 클릭해야지 넘어가게 하고 다른 어두워진 부분을 클릭하면 다시 닫게 하고 싶어": 저절로 넘어가지 않음
   right.alpha = info;
   bool trans = t > 2;
   for (int i = 0; i < 6; i++) {
    var target = Slot(i);
    if (trans && target != to[i]) { from[i] = shown[i]; moveT[i] = t; }
    to[i] = target;
    Rect r; float e = Css.OutQ((t - 1.0f - i * .06f) / .6f);
    var fly = new Rect(Mathf.Lerp(Deck.x, target.x, e), Mathf.Lerp(Deck.y, target.y, e), Mathf.Lerp(Deck.width, target.width, e), Mathf.Lerp(Deck.height, target.height, e));
    if (trans && moveT[i] >= 0 && t - moveT[i] < .45f) { float k = Css.Ease((t - moveT[i]) / .45f); r = new Rect(Mathf.Lerp(from[i].x, target.x, k), Mathf.Lerp(from[i].y, target.y, k), Mathf.Lerp(from[i].width, target.width, k), Mathf.Lerp(from[i].height, target.height, k)); }
    else r = trans ? target : fly;
    shown[i] = r;
    var rt = cards[i].rectTransform; rt.anchoredPosition = new Vector2(r.x, -r.y); rt.sizeDelta = new Vector2(r.width, r.height);
    float b = Z(i) == 10 ? 1 : .55f; cards[i].color = new Color(b, b, b, 1);
   }
   // z order (mockup zIndex): cards above everything else in the layer
   foreach (int k in Enumerable.Range(0, 6).OrderBy(Z)) cards[k].transform.SetAsLastSibling();
  }
 }
 public sealed class DeckCatcher : MonoBehaviour, IPointerClickHandler { [System.NonSerialized] public DeckView deck; public void OnPointerClick(PointerEventData e) { if (deck) deck.Click(e); } }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breach.UI {
 // Week 12 item 4 (_도구/시안12주차/tabs/04_zoom.js "keyword", 2nd review 11-26): double-click or right-click a hand card to zoom it.
 // Left: the card (420 x 672 at 150,200). Middle: name, English name, effect text with dotted gold underlines on game terms, target/timing/odds.
 // Right: one glossary box per term, joined to its underline by a gold line. Close: outside click, Esc or right-click; a right-click on
 // another hand card switches to it. The one-click select (card lock) is gone; cards are played only by dragging.
 // Values are the mockup's: open = smooth(tz/.55), close = from*(1-smooth(tz/.4)) for .45 s, panel outQ((open-.3)/.7), glossary k at .6+.25k.
 // Text positions come from Chrome's layout of that page (scratchpad dump_zoom.py): baseline = TMP top + 1.16 em (Do Hyeon line metrics).
 public sealed class CardZoom : MonoBehaviour {
  BattleController c; RectTransform root, layer; Image dim, catcher; TMP_Text hint, idleText; Shape idleBar; CanvasGroup idleGroup;
  // current card
  RectTransform content; Image flyer; FxPainter flyGlow, lines; CanvasGroup panel; readonly List<Tip> tips = new List<Tip>(); readonly List<Span> spans = new List<Span>();
  sealed class Tip { public CanvasGroup group; public Shape border; public FxPainter glow; public Rect box; }
  sealed class Span { public float left, width, baseline; }
  enum Mode { Closed, Open, Closing }
  Mode mode; float now, ts, from, o; int idx = -1; Card card;
  public bool IsOpen { get { return mode != Mode.Closed; } }
  public int Index { get { return mode == Mode.Closed ? -1 : idx; } }
  public Card Shown { get { return card; } }
  public float Amount { get { return o; } }
  public float OpenTime { get { return now - ts; } }
  public static readonly Rect Target = new Rect(150, 200, 420, 672);
  static readonly Color Dim = C(110, 150, 165), Text = C(179, 209, 219), Line = C(51, 102, 115), Panel = C(6, 10, 14, .97f), Gold = C(255, 196, 56), Cyan = C(56, 217, 255);
  static Color C(float r, float g, float b, float a = 1) { return new Color(r / 255, g / 255, b / 255, a); }
  static float Clamp(float v) { return Mathf.Clamp01(v); }
  static float Smooth(float v) { v = Clamp(v); return v * v * (3 - 2 * v); }
  static float OutQ(float v) { v = Clamp(v); return 1 - Mathf.Pow(1 - v, 3); }
  static Color A(Color col, float a) { return new Color(col.r, col.g, col.b, col.a * a); }

  public void Initialize(BattleController controller) {
   c = controller;
   root = Ui.Rect("카드 확대", c.stage, 0, 0, 1920, 1080); root.gameObject.SetActive(false);
   dim = Img(root, "확대 - 뒤 화면 어둡게", 0, 0, 1920, 1080, C(1, 3, 6, .82f));
   layer = Ui.Rect("확대 - 카드와 설명", root, 0, 0, 1920, 1080);
   // user 11-26 "쓸대 없는 안내는 없에고 싶음": the close hint under the zoom and the idle hint right of the hand are gone
   catcher = Img(root, "확대 - 클릭 받기", 0, 0, 1920, 1080, Color.clear); catcher.raycastTarget = true; catcher.gameObject.AddComponent<ZoomCatcher>().zoom = this;
  }
  Image Img(Transform parent, string name, float x, float y, float w, float h, Color col) { var i = Ui.Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>(); i.color = col; i.raycastTarget = false; return i; }
  // baseline-placed text: TMP top alignment puts the first baseline 1.16 em below the rect top (Do Hyeon asset has Noto's line metrics)
  TMP_Text Txt(Transform parent, string name, float x, float baseline, float w, float size, Color col, string text, float lineSpacing, float charSpacing, TextAlignmentOptions align) {
   var t = Ui.Text(name, parent, x, baseline - 1.16f * size, w, 400, text, c.font, size, col); t.alignment = align; t.textWrappingMode = TextWrappingModes.NoWrap; t.lineSpacing = lineSpacing; t.characterSpacing = charSpacing; t.richText = true; return t;
  }

  public void Open(int index, Card shown) { if (mode != Mode.Closed) return; root.gameObject.SetActive(true); layer.gameObject.SetActive(true); Build(shown); idx = index; mode = Mode.Open; ts = now; o = 0; root.SetAsLastSibling(); c.LayoutHand(index); Apply(0, 0); }
  public void Switch(int index, Card shown) { Build(shown); idx = index; mode = Mode.Open; ts = now - .55f; o = 1; c.LayoutHand(index, true); }
  public void Close() { if (mode == Mode.Open) { mode = Mode.Closing; from = o; ts = now; } }
  public void CloseNow() { mode = Mode.Closed; o = 0; if (root) root.gameObject.SetActive(false); }
  // the catcher covers the screen: left = close, right = switch to the hand card under the pointer or close
  public void Click(PointerEventData e) {
   if (mode != Mode.Open) return;
   if (e.button == PointerEventData.InputButton.Left) Close();
   else if (e.button == PointerEventData.InputButton.Right) { int i = c.HandIndexAt(e.position); if (i >= 0 && i != idx) Switch(i, c.HandCard(i)); else Close(); }
  }

  void Update() {
   now += Time.unscaledDeltaTime * FxClock.Scale;
   if (idleGroup) idleGroup.alpha = 1 - (mode == Mode.Closed ? 0 : o);
   if (mode == Mode.Closed) return;
   float tz = now - ts, open;
   if (mode == Mode.Open) { open = Smooth(tz / .55f); o = open; }
   else { open = from * (1 - Smooth(tz / .4f)); o = open; if (tz >= .45f) { mode = Mode.Closed; o = 0; root.gameObject.SetActive(false); if (idleGroup) idleGroup.alpha = 1; c.ZoomClosed(); return; } }
   Apply(open, mode == Mode.Open ? tz : 3);
  }

  void Apply(float open, float tz) {
   dim.color = C(1, 3, 6, .82f * open); layer.gameObject.SetActive(open > 0); if (idleGroup) idleGroup.alpha = 1 - open;
   if (!content) return;
   // flyer: from the hovered hand slot to the left target, outQ(open)
   Rect fr = BattleController.HandRect(c.HandCount, idx, idx), r = Lerp(fr, Target, OutQ(open));
   Place(flyer.rectTransform, r);
   flyGlow.Begin(); flyGlow.SoftRect(r.x, r.y, r.width, r.height, 30, C(56, 217, 255, .125f)); flyGlow.End();
   panel.alpha = OutQ((open - .3f) / .7f);
   int n = tips.Count, act = open > .99f && tz > 1.6f ? Mathf.FloorToInt((tz - 1.6f) / 1.4f) % n : -1;
   lines.Begin();
   for (int k = 0; k < n; k++) {
    var tip = tips[k]; float on = Clamp((tz - .6f - k * .25f) / .3f) * open; tip.group.alpha = on;
    tip.border.color = k == act ? Gold : Line;
    tip.glow.Begin(); if (k == act) tip.glow.SoftRect(tip.box.x, tip.box.y, tip.box.width, tip.box.height, 24, C(255, 196, 56, .175f)); tip.glow.End();
    var sp = spans[k]; if (on <= 0) continue;
    // HTML: x = 640 + 30 + offsetLeft (mb starts at 671), y = 200 + 70 + offsetTop + offsetHeight (mb at 271) -> 1 px left/up of the real box
    float y = sp.baseline + 7 + 3 - 1 + 3, x0 = sp.left - 1;
    var col = C(255, 196, 56, on * (k == act ? 1 : .45f));
    lines.Poly(new[] { new Vector2(x0, y), new Vector2(1215, y), new Vector2(1245, 240 + k * 225), new Vector2(1270, 240 + k * 225) }, 2, col);
   }
   lines.End();
  }
  static Rect Lerp(Rect a, Rect b, float k) { return new Rect(Mathf.Lerp(a.x, b.x, k), Mathf.Lerp(a.y, b.y, k), Mathf.Lerp(a.width, b.width, k), Mathf.Lerp(a.height, b.height, k)); }
  static void Place(RectTransform t, Rect r) { t.anchoredPosition = new Vector2(r.x, -r.y); t.sizeDelta = new Vector2(r.width, r.height); }

  void Build(Card shown) {
   card = shown; int i = (int)shown; bool enh = i >= 3; Color a = enh ? Gold : Cyan;
   if (content) Destroy(content.gameObject); tips.Clear(); spans.Clear();
   content = Ui.Rect("카드 " + CardZoomData.Kr[i], layer, 0, 0, 1920, 1080);
   flyGlow = FxPainter.Create("카드 빛 (drop-shadow 30px)", content);
   flyer = Img(content, "큰 카드", 0, 0, 126, 201.6f, Color.white); flyer.sprite = c.feedback.cardImages[i];
   // middle panel 640,200 560x672: fill .97, 1 px line, 22 px corner brackets (3 px, accent) at -2 px, shadow 0 0 40px rgba(0,0,0,.6)
   var p = Ui.Rect("효과 상자", content, 0, 0, 1920, 1080); panel = p.gameObject.AddComponent<CanvasGroup>(); panel.blocksRaycasts = false;
   var shadow = FxPainter.Create("상자 그림자", p); shadow.Begin(); shadow.SoftRect(640, 200, 560, 672, 40, new Color(0, 0, 0, .3f)); shadow.End();
   Ui.Box("상자 바탕", p, 640, 200, 560, 672, Panel); Ui.Box("상자 테두리", p, 640, 200, 560, 672, Line, false, 1);
   foreach (var b in new[] { new Rect(639, 199, 22, 3), new Rect(639, 199, 3, 22), new Rect(1179, 199, 22, 3), new Rect(1198, 199, 3, 22),
                            new Rect(639, 870, 22, 3), new Rect(639, 851, 3, 22), new Rect(1179, 870, 22, 3), new Rect(1198, 851, 3, 22) })
    Ui.Box("모서리", p, b.x, b.y, b.width, b.height, a);
   Ui.Box("제목 띠", p, 653, 213, 534, 34, C(20, 40, 48, .9f));
   Txt(p, "제목", 667, 236, 520, 20, Dim, "효과 / EFFECT", 0, 5, TextAlignmentOptions.TopLeft);
   Txt(p, "이름", 671, 323, 498, 56, Color.white, CardZoomData.Kr[i], 0, 0, TextAlignmentOptions.TopLeft);
   Txt(p, "영어 이름", 671, 363, 498, 24, a, CardZoomData.En[i], 0, 12.5f, TextAlignmentOptions.TopLeft);
   // effect text 34 px, line 1.7 (57.8 px): game terms #ffe3a0 with a 3 px dotted gold underline at the content bottom (baseline + 7)
   string plain = string.Join("\n", CardZoomData.Effect[i]); var phrases = CardZoomData.Phrase[i]; var at = new int[phrases.Length];
   for (int k = 0; k < phrases.Length; k++) at[k] = plain.IndexOf(phrases[k], System.StringComparison.Ordinal);
   var rich = new System.Text.StringBuilder(); for (int q = 0; q < plain.Length; q++) { for (int k = 0; k < phrases.Length; k++) if (at[k] == q) rich.Append("<color=#FFE3A0>"); rich.Append(plain[q]); for (int k = 0; k < phrases.Length; k++) if (at[k] >= 0 && at[k] + phrases[k].Length - 1 == q) rich.Append("</color>"); }
   const float effBase = 433, pitch = 57.8f; var eff = Txt(p, "효과 글", 671, effBase, 498, 34, C(233, 251, 255), rich.ToString(), (pitch - 1.448f * 34) / 34 * 100, 0, TextAlignmentOptions.TopLeft);
   eff.ForceMeshUpdate(true, true); /* works while the root is still inactive (first open) */ var info = eff.textInfo; var dots = FxPainter.Create("용어 밑줄", p); dots.Begin();
   float top = effBase - 1.16f * 34;
   for (int k = 0; k < phrases.Length; k++) {
    if (at[k] < 0 || at[k] + phrases[k].Length > info.characterCount) { spans.Add(new Span { left = 671, width = 0, baseline = effBase }); continue; }
    var first = info.characterInfo[at[k]]; var last = info.characterInfo[at[k] + phrases[k].Length - 1];
    var s = new Span { left = 671 + first.origin, width = last.xAdvance - first.origin, baseline = top - first.baseLine }; spans.Add(s);
    // Chrome 3 px dotted border: n = round((w + 3) / 6) square dots, evenly spread so both ends have one
    int dn = Mathf.Max(2, Mathf.RoundToInt((s.width + 3) / 6)); float step = (s.width - 3) / (dn - 1);
    for (int q = 0; q < dn; q++) dots.FillRect(Mathf.Round(s.left + q * step), s.baseline + 7, 3, 3, Gold);
   }
   dots.End();
   int effLines = CardZoomData.Effect[i].Length; float infoBase = 395 + effLines * pitch + 30 + 23;
   Txt(p, "대상 · 발동 · 확률", 671, infoBase, 498, 22, Dim, string.Join("\n", CardZoomData.Info[i]), (33 - 1.448f * 22) / 22 * 100, 0, TextAlignmentOptions.TopLeft);
   lines = FxPainter.Create("용어 연결선", content);
   // glossary boxes: 1270, 210 + k*225, 520 wide (border-box), padding 16/20, term 30 px gold, text 23 px line 1.45
   for (int k = 0; k < phrases.Length; k++) {
    var gl = CardZoomData.Gloss[i][k]; float y = 210 + k * 225, h = 111.3f + (gl.Length - 1) * 33.35f;
    var tr = Ui.Rect("용어 풀이 " + CardZoomData.Term[i][k], content, 0, 0, 1920, 1080); var g = tr.gameObject.AddComponent<CanvasGroup>(); g.blocksRaycasts = false;
    var glow = FxPainter.Create("빛", tr);
    Ui.Box("바탕", tr, 1270, y, 520, h, Panel); var border = Ui.Box("테두리", tr, 1270, y, 520, h, Line, false, 1);
    Txt(tr, "용어", 1291, y + 45, 478, 30, Gold, CardZoomData.Term[i][k], 0, 0, TextAlignmentOptions.TopLeft);
    Txt(tr, "풀이", 1291, y + 84, 478, 23, Text, string.Join("\n", gl), (33.35f - 1.448f * 23) / 23 * 100, 0, TextAlignmentOptions.TopLeft);
    tips.Add(new Tip { group = g, border = border, glow = glow, box = new Rect(1270, y, 520, h) });
   }
  }
 }

 public sealed class ZoomCatcher : MonoBehaviour, IPointerClickHandler {
  [System.NonSerialized] public CardZoom zoom;
  public void OnPointerClick(PointerEventData e) { if (zoom) zoom.Click(e); }
 }
}

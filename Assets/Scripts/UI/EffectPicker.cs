using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breach.UI {
 // 13주차 11_fxpick.js pTiles (확정안 "2. 미리보기 타일 3×2"): the bottom-right window that picks the card activation effect.
 // 2026-11-27 user: "이펙트 나오는 것은 랜덤 말고, 오른쪽 아래에 선택할수 있는 창을 만들자", "마우스를 가져다 대면 움직이면 좋겠음".
 //  PANEL = {x 1452, y 784, w 300, h 280}; header "발동 효과 / EFFECT" (18 px #7f98a6, letter-spacing 2, margin-bottom 8);
 //  grid 3 x 94 px, gap 9; a tile = border 1 #1c2f3c, background rgba(9,20,28,.9), padding 3, an 86 x 78 mini board and a 22 px name (13 px);
 //  the selected tile has a #38d9ff border and a 12 px rgba(56,217,255,.6) glow, its name white (others #7f98a6).
 //  A mini board stands still at the effect middle (drawMini: t = .7 -> u = .5) and plays from its start only while the mouse is over it.
 // The choice is saved (PlayerPrefs). Default = 락온 괄호 + 터미널. Only the tiles take the pointer (clicks and hover); nothing else is blocked.
 public sealed class EffectPicker : MonoBehaviour {
  public const int Random = -1;
  // 11-27 사용자 "오른쪽 아래 이펙트 ui생겨서 다른 ui와 충돌 난거 있는거 같던데, 가로로 좀 압축해서 높이는 손패 창과 비슷한 위치에 두자":
  //  3x2 (300x280, y 784) → 한 줄 6칸. 왼쪽 MY HAND 창(174, 905, 400x78)과 좌우 대칭 자리 (1346, 905, 400x78), 제목은 그 위 16px
  public static readonly Rect Panel = new Rect(1346, 887, 400, 96);
  // tile order = 11_fxpick.js NAMES: the five effects, then 턴마다 무작위
  public static readonly int[] Options = { ActivateFx.Now, ActivateFx.Glitch, ActivateFx.Hexes, ActivateFx.Stream, ActivateFx.Lockon, Random };
  // pTiles names: n.replace(' RGB 분리', '').replace(' 격자 파문', ' 파문').replace('턴마다 ', '')
  static readonly string[] TileNames = { "지금 빛 띠", "글리치", "헥스 파문", "데이터 스트림", "락온 괄호 + 터미널", "무작위" };
  const string PrefKey = "breach.activateFx.pick";
  static int choice = int.MinValue;
  static bool Valid(int v) { return System.Array.IndexOf(Options, v) >= 0; }
  public static int Choice {
   get { if (choice == int.MinValue) { int v = PlayerPrefs.GetInt(PrefKey, ActivateFx.Lockon); choice = Valid(v) ? v : ActivateFx.Lockon; } return choice; }
   set { if (!Valid(value)) return; choice = value; PlayerPrefs.SetInt(PrefKey, value); PlayerPrefs.Save(); }
  }
  // the effect to play: the saved choice, or this action turn's random pick when "무작위" is chosen
  public static int Resolve(int turnPick) { return Choice == Random ? turnPick : Choice; }
  // true while the pointer is over a tile (BattleController does not treat a press there as "hold to speed up")
  public static bool PointerOver { get; private set; }

  static readonly Color Line = new Color(28 / 255f, 47 / 255f, 60 / 255f), Cyan = new Color(56 / 255f, 217 / 255f, 1), Grey = new Color(127 / 255f, 152 / 255f, 166 / 255f);
  sealed class Tile { public int option; public Shape border; public Shape[] glow; public TextMeshProUGUI name; public FxPainter mini; public ActivateFx.Preview preview; public bool hover, still; public float t0; }
  readonly List<Tile> tiles = new List<Tile>(); BattleController c; CanvasGroup group; int shown = int.MinValue;

  public void Initialize(BattleController controller) {
   c = controller;
   // 11-27 사용자 "카드 확대 마우스 댔을때 이펙트 선택창이 앞에 있는데 뒤로 보낼래": game 맨 앞 → stage 안. 나중에 여는 카드 확대·덱·일시정지 창이 위를 덮는다
   var root = Ui.Rect("발동 효과 고르기", c.stage, Panel.x, Panel.y, Panel.width, Panel.height); root.SetAsLastSibling();
   group = root.gameObject.AddComponent<CanvasGroup>();
   var head = Ui.Text("발동 효과 제목", root, 2, 0, 396, 16, "발동 효과 / EFFECT", c.font, 13, Grey); head.alignment = TextAlignmentOptions.TopLeft; head.textWrappingMode = TextWrappingModes.NoWrap; head.characterSpacing = 2 * 100 / 13f;
   const float top = 18, w = 62, h = 78, gap = 5.6f, mw = 58, mh = 54; float k = Mathf.Min(mw / 800f, mh / 716f);
   for (int i = 0; i < Options.Length; i++) {
    var tile = new Tile { option = Options[i], still = false };
    float x = i * (w + gap), y = top;
    var cell = Ui.Rect("효과 타일 - " + TileNames[i], root, x, y, w, h);
    // glow (box-shadow 0 0 12px rgba(56,217,255,.6)) as four widening outlines
    tile.glow = new Shape[4]; float[] outs = { 1.5f, 3, 4.5f, 6 };
    for (int g = 0; g < 4; g++) { float o = outs[g]; tile.glow[g] = Ui.Box("선택 빛", cell, -o, -o, w + 2 * o, h + 2 * o, new Color(Cyan.r, Cyan.g, Cyan.b, 0), false, g < 2 ? 2 : 3); }
    var bg = Ui.Box("타일 바탕", cell, 0, 0, w, h, new Color(9 / 255f, 20 / 255f, 28 / 255f, .9f)); bg.raycastTarget = true;
    var input = bg.gameObject.AddComponent<TileInput>(); input.picker = this; input.index = i;
    tile.border = Ui.Box("타일 테두리", cell, 0, 0, w, h, Line, false, 1);
    var view = Ui.Rect("작은 판", cell, 2, 2, mw, mh); view.gameObject.AddComponent<RectMask2D>();
    Ui.Box("작은 판 바탕", view, 0, 0, mw, mh, new Color(9 / 255f, 20 / 255f, 28 / 255f));
    if (tile.option == Random) {
     var q = Ui.Text("무작위 기호", view, 0, 0, mw, mh, "?", c.font, Mathf.Round(mh * .5f), new Color(233 / 255f, 251 / 255f, 1, .85f)); q.alignment = TextAlignmentOptions.Center;
    } else {
     // setTransform(k, 0, 0, k, (w - B.w k) / 2 - B.x k, (h - B.h k) / 2 - B.y k)
     var board = Ui.Rect("작은 판 그림", view, (mw - 800 * k) / 2 - 582 * k, (mh - 716 * k) / 2 - 129 * k, 1920, 1080); board.localScale = new Vector3(k, k, 1);
     tile.mini = FxPainter.Create("효과 미리보기", board); tile.preview = ActivateFx.CreatePreview(tile.mini);
    }
    tile.name = Ui.Text("효과 이름", cell, 1, mh + 3, w - 2, h - mh - 4, TileNames[i], c.font, 11, Grey); tile.name.alignment = TextAlignmentOptions.Center; tile.name.textWrappingMode = TextWrappingModes.NoWrap;
    tile.name.enableAutoSizing = true; tile.name.fontSizeMin = 8; tile.name.fontSizeMax = 11; tile.name.overflowMode = TextOverflowModes.Ellipsis;
    tiles.Add(tile);
   }
   // The status line (BattleController.statusLabel) sat at (1459, 795, 287 x 100) under this window: it moves below the tiles
   // as one line (1452, 1042, 300 x 26), 15 px shrinking to 11 px, cut with an ellipsis.
   if (c.statusLabel) {
    // 창이 손패 높이로 내려가 상태 글은 원래 자리(1459, 795)로 돌아감. 창 제목(y 887)에 닿지 않게 높이 84, 길면 줄어듦
    var r = c.statusLabel.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(1459, -795); r.sizeDelta = new Vector2(287, 84);
    c.statusLabel.textWrappingMode = TextWrappingModes.Normal; c.statusLabel.overflowMode = TextOverflowModes.Ellipsis; c.statusLabel.enableAutoSizing = true; c.statusLabel.fontSizeMin = 11; c.statusLabel.fontSizeMax = 16; c.statusLabel.alignment = TextAlignmentOptions.TopLeft;
   }
   Refresh();
  }
  void Refresh() {
   int on = Choice; if (shown == on) return; shown = on;
   foreach (var t in tiles) { bool sel = t.option == on; t.border.color = sel ? Cyan : Line; t.name.color = sel ? Color.white : Grey; float[] alphas = { .5f, .3f, .16f, .07f }; for (int g = 0; g < 4; g++) t.glow[g].color = new Color(Cyan.r, Cyan.g, Cyan.b, sel ? alphas[g] : 0); }
  }
  void Update() {
   if (!c) return;
   // stage 안으로 옮긴 뒤 타이틀 화면에도 보여서, 게임 화면일 때만 켠다
   if (group) { bool on = c.game.activeSelf && !(c.title && c.title.activeSelf); if (group.gameObject.activeSelf != on) group.gameObject.SetActive(on); if (!on) return; }
   if (group && c.presentation) group.alpha = c.presentation.PeripheralAlpha; // dims with the other side UI during the resolve
   Refresh(); bool any = false;
   foreach (var t in tiles) {
    any |= t.hover; if (t.preview == null) continue;
    // drawMini: hovered -> t from the hover start; otherwise the still at t = .7 (drawn once, again after a hover)
    if (t.hover) { ActivateFx.DrawPreview(t.preview, t.option, Time.unscaledTime - t.t0); t.still = false; }
    else if (!t.still) { ActivateFx.DrawPreview(t.preview, t.option, .7f); t.still = true; }
   }
   PointerOver = any && isActiveAndEnabled;
  }
  void OnDisable() { PointerOver = false; foreach (var t in tiles) t.hover = false; }
  internal void Enter(int i) { tiles[i].hover = true; tiles[i].t0 = Time.unscaledTime; }
  internal void Exit(int i) { tiles[i].hover = false; }
  internal void Click(int i) { Choice = tiles[i].option; Refresh(); }
  sealed class TileInput : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler {
   public EffectPicker picker; public int index;
   public void OnPointerEnter(PointerEventData e) { picker.Enter(index); }
   public void OnPointerExit(PointerEventData e) { picker.Exit(index); }
   public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) picker.Click(index); }
  }
 }
}

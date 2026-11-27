using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breach.UI {
 // Week 12 item 10 (_도구/시안12주차/tabs/10_resultlog.js "replay"): 전투 다시 보기 on the result screen.
 // Left: the step list (rows from row(api, e, {h:44, hh:40, fs:18}), Chrome baselines), current row 1, past .55, future .3, a 4 px cyan bar.
 // Right: the board (game area 582,100 800x745 at 1.07x, left 922 top 96) drawn with the game's own pieces: copies of the three board
 // backgrounds (blue / red / resolve, .25 s crossfade), copies of the six ServerViews, rungs as BattleController.DrawRung, firewall gates as
 // CombatPresentation.Firewall, shots as CombatPresentation.Make (bloom, trail, core, power), impacts as CombatPresentation.Impact.
 // Step length: action .9 s, round head 1.1 s, reveal/event/hit 1.4 s (flight .8 + impact .36). ◀ 이전 · 재생/멈춤 · 다음 ▶ · 결과로 ×.
 // Everything is drawn from (step index, time in step) like the mockup's draw(idx, lt), so a step can be shown at an exact moment.
 public sealed class ReplayView : MonoBehaviour {
  static readonly Color CY = Css.Rgb(56, 217, 255), RD = Css.Rgb(255, 59, 87), GD = Css.Rgb(255, 196, 56), Body = Css.Rgb(214, 230, 238), Btn = Css.Rgb(150, 190, 200), Grey = Css.Rgb(127, 152, 166);
  static readonly Color RiderAll = Css.Rgb(255, 209, 102), RiderMe = Css.Rgb(56, 217, 255), RiderEnemy = Css.Rgb(255, 75, 102);
  const float SC = 1.07f, BL = 922, BT = 96, BW = 800, BH = 745, FLY = .8f;
  BattleController c; CanvasGroup content; RectTransform root, list, rowsRoot, replica, bgRoot, rungLayer, gateLayer, shotLayer, fxLayer; CanvasGroup group;
  Image bar; TMP_Text caption, pend, playLabel; readonly List<RectTransform> rows = new List<RectTransform>(); readonly List<float> rowTop = new List<float>();
  readonly List<CanvasGroup> rowGroups = new List<CanvasGroup>(); float contentH, scroll;
  readonly CanvasGroup[] bgs = new CanvasGroup[3]; readonly ServerView[] servers = new ServerView[6];
  FxPainter under, over, trails, hud; Image actionCard, revealCard; Shape revealBorder; readonly List<TMP_Text> labels = new List<TMP_Text>(), plain = new List<TMP_Text>(); int labelUsed, plainUsed;
  readonly List<FlareGraphic> flares = new List<FlareGraphic>(); int flareUsed; readonly List<ShotView> shots = new List<ShotView>();
  sealed class ShotView { public RectTransform root; public CanvasGroup g; public FlareGraphic flare; public Shape hex; public TMP_Text num; }
  readonly List<(TMP_Text t, RectTransform r, System.Action a)> controls = new List<(TMP_Text, RectTransform, System.Action)>(); readonly List<float> controlLevel = new List<float>();
  List<ReplayLog.Step> steps = new List<ReplayLog.Step>();
  int idx, last = -1, bgCur = -1, bgPrev = -1, drawnRungs = -1; float lt, logT = 9, bgT = 1; bool log, play;
  public bool IsOpen { get { return log || logT < .2f; } }
  public int Index { get { return idx; } }
  public float StepTime { get { return lt; } }
  public bool Playing { get { return play; } }
  public int Count { get { return steps.Count; } }
  public string Caption { get { return caption ? caption.text : ""; } }
  public Vector2? ForcePointer;

  public void Initialize(BattleController controller, CanvasGroup resultContent) {
   c = controller; content = resultContent;
   root = Ui.Rect("전투 다시 보기", c.result.transform, 0, 0, 1920, 1080); var bgImg = root.gameObject.AddComponent<Image>(); bgImg.color = Css.Rgb(5, 8, 11); bgImg.raycastTarget = true;
   group = root.gameObject.AddComponent<CanvasGroup>(); root.gameObject.AddComponent<ReplayCatcher>().view = this; root.gameObject.SetActive(false);
   Base(root, "제목", 80, 73, 36, Color.white, "전투 다시 보기", TextAlignmentOptions.TopLeft); Base(root, "제목 영문", 278.3f, 73, 20, Grey, "REPLAY", TextAlignmentOptions.TopLeft);
   list = Ui.Rect("단계 목록", root, 70, 110, 700, 840); list.gameObject.AddComponent<RectMask2D>();
   rowsRoot = Ui.Rect("목록 내용", list, 0, 0, 700, 10);
   bar = Ui.Rect("지금 단계 막대", rowsRoot, 0, 0, 4, 44).gameObject.AddComponent<Image>(); bar.color = CY; bar.raycastTarget = false;
   // board: the mask is the scaled game area, the replica uses game coordinates
   var mask = Ui.Rect("판 다시 보기", root, BL, BT, BW * SC, BH * SC); mask.gameObject.AddComponent<RectMask2D>();
   replica = Ui.Rect("판 (게임 좌표)", mask, -582 * SC, -100 * SC, 1920, 1080); replica.localScale = Vector3.one * SC;
   var rg = replica.gameObject.AddComponent<CanvasGroup>(); rg.blocksRaycasts = false; rg.interactable = false;
   bgRoot = Ui.Rect("판 바탕", replica, 0, 0, 1920, 1080);
   for (int i = 0; i < 3; i++) {
    var copy = Instantiate(c.backgrounds[i], bgRoot); copy.name = "판 바탕 " + (i == 0 ? "파랑" : i == 1 ? "빨강" : "동시 발동"); copy.SetActive(true);
    foreach (var b in copy.GetComponentsInChildren<Selectable>(true)) b.enabled = false; bgs[i] = copy.GetComponent<CanvasGroup>() ?? copy.AddComponent<CanvasGroup>(); bgs[i].alpha = 0;
   }
   pend = Ui.Text("강화 카드 대기", replica, 582, 121 - 1.16f * 13, 800, 20, "", c.font, 13, GD); pend.alignment = TextAlignmentOptions.TopRight; pend.textWrappingMode = TextWrappingModes.NoWrap;
   rungLayer = Ui.Rect("회선", replica, 0, 0, 1920, 1080); rungLayer.gameObject.AddComponent<CanvasGroup>();
   gateLayer = Ui.Rect("방화벽 관문", replica, 0, 0, 1920, 1080);
   for (int k = 0; k < 6; k++) { var copy = Instantiate(c.servers[k].gameObject, replica); copy.name = "서버 " + k; servers[k] = copy.GetComponent<ServerView>(); servers[k].controller = null; foreach (var g in copy.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false; }
   under = FxPainter.Create("서버 빛", replica);
   trails = FxPainter.Create("탄 잔광", replica); shotLayer = Ui.Rect("빛번짐 탄", replica, 0, 0, 1920, 1080);
   over = FxPainter.Create("충돌 표시", replica); fxLayer = Ui.Rect("충돌 빛", replica, 0, 0, 1920, 1080);
   hud = FxPainter.Create("행동 표시", replica);
   actionCard = Ui.Rect("행동 카드", replica, 0, 0, 88, 140.8f).gameObject.AddComponent<Image>(); actionCard.raycastTarget = false;
   revealBorder = Ui.Box("공개 테두리", replica, 0, 0, 94, 146.8f, GD, false, 2); revealCard = Ui.Rect("공개 카드", replica, 0, 0, 88, 140.8f).gameObject.AddComponent<Image>(); revealCard.raycastTarget = false;
   caption = Base(root, "단계 글", BL, 935.14f, 26, Color.white, "", TextAlignmentOptions.Top, BW * SC);
   Control("◀ 이전", 1110, 1021, () => { play = false; Go(Mathf.Max(0, idx - 1)); });
   playLabel = Control("멈춤", 1300, 1021, () => play = !play);
   Control("다음 ▶", 1460, 1021, () => { play = false; Go(Mathf.Min(steps.Count - 1, idx + 1)); });
   Control("결과로 ×", 1700, 61, Close);
  }
  TMP_Text Base(Transform parent, string name, float x, float baseline, float size, Color col, string text, TextAlignmentOptions align, float w = 900) {
   var t = Ui.Text(name, parent, x, baseline - 1.16f * size, w, size * 2, text, c.font, size, col); t.alignment = align; t.textWrappingMode = TextWrappingModes.NoWrap; t.richText = true; return t;
  }
  TMP_Text Control(string label, float x, float baseline, System.Action act) { var t = Base(root, label, x, baseline, 22, Btn, label, TextAlignmentOptions.TopLeft, 200); controls.Add((t, t.rectTransform, act)); controlLevel.Add(0); return t; }

  public void Open() {
   steps = c.Replay.Steps; if (steps.Count == 0) return;
   Build(); log = true; logT = 0; idx = 0; lt = 0; play = true; last = -1; bgCur = bgPrev = -1; drawnRungs = -1; root.gameObject.SetActive(true); root.SetAsLastSibling(); Step(0);
  }
  public void Close() { if (log) { log = false; logT = 0; } }
  public void CloseNow() { log = false; logT = 9; if (root) root.gameObject.SetActive(false); if (content) content.alpha = 1; }
  public void Go(int i) { idx = Mathf.Clamp(i, 0, steps.Count - 1); lt = 0; }
  public void Pause() { play = false; }
  static float Len(ReplayLog.Step e) { return e.K == "round" ? 1.1f : e.K == "reveal" || e.K == "event" || e.K == "hit" ? 1.4f : .9f; }
  static Color Col(ReplayLog.Step e) { return e.K == "round" ? (e.Att == 0 ? CY : RD) : e.K == "event" ? GD : e.K == "hit" ? (e.Who == 1 ? RD : CY) : (e.Who == 0 ? CY : RD); }
  static int BgOf(ReplayLog.Step e) { return e.K == "reveal" || e.K == "event" || e.K == "hit" ? 2 : e.K == "round" ? (e.Att == 0 ? 0 : 1) : e.K == "me" ? 0 : 1; }

  void Build() {
   foreach (var r in rows) Destroy(r.gameObject); rows.Clear(); rowTop.Clear(); rowGroups.Clear(); float y = 0;
   foreach (var e in steps) {
    var col = Col(e); bool head = e.K == "round"; if (head) y += 6; float h = head ? 40 : 44;
    var r = Ui.Rect("단계 " + rows.Count, rowsRoot, 0, y, 700, h); rows.Add(r); rowTop.Add(y); rowGroups.Add(r.gameObject.AddComponent<CanvasGroup>());
    if (head) {
     var g = FxPainter.Create("머리 바탕", r); g.Begin(); g.Line(new Vector2(0, 20), new Vector2(700, 20), 40, Css.A(col, .22f), Css.A(col, 0)); g.End();
     Ui.Box("머리 선", r, 0, 0, 4, 40, col); Base(r, "글", 26, 27.5f, 20, col, e.T, TextAlignmentOptions.TopLeft, 660);
    } else {
     if (e.C >= 0) { var im = Ui.Rect("카드", r, 18, 4, 23, 36).gameObject.AddComponent<Image>(); im.sprite = c.feedback.cardImages[e.C]; im.raycastTarget = false; }
     else { Ui.Box("기호 칸", r, 18, 10.5f, 23, 23, Css.A(col, .18f), true, 1, 40); var gt = Base(r, "기호", 18, 26.9f, 16.1f, col, e.K == "hit" ? "▼" : e.G.ToString(), TextAlignmentOptions.Top, 23); }
     Ui.Box("점", r, 53, 19, 6, 6, col, true, 1, 24);
     Base(r, "글", 71, 28.5f, 18, Body, e.T.Replace(" (발동 때 적용)", ""), TextAlignmentOptions.TopLeft, 520);
     string tag = e.K == "reveal" ? "공개" : e.K == "hit" ? "서버 파괴" : e.T.IndexOf("발동 때") > 0 ? "비공개" : null;
     if (tag != null) Base(r, "꼬리표", 388, 26.5f, 15, e.K == "reveal" ? GD : e.K == "hit" ? col : Grey, tag, TextAlignmentOptions.TopRight, 300);
    }
    y += h;
   }
   contentH = y; rowsRoot.sizeDelta = new Vector2(700, y); bar.transform.SetAsLastSibling();
  }

  Vector2 Pointer() { if (ForcePointer.HasValue) return ForcePointer.Value; Vector2 q; RectTransformUtility.ScreenPointToLocalPointInRectangle(c.stage, Input.mousePosition, null, out q); return new Vector2(q.x, -q.y); }
  public void ClickScreen(Vector2 screen) { Vector2 q; RectTransformUtility.ScreenPointToLocalPointInRectangle(c.stage, screen, null, out q); Click(new Vector2(q.x, -q.y)); }
  public void Click(Vector2 p) {
   if (!log || logT < .1f) return;
   for (int k = 0; k < controls.Count; k++) { var r = controls[k].r; float w = controls[k].t.preferredWidth; float x = r.anchoredPosition.x, top = -r.anchoredPosition.y + 1.16f * 22 - 21; if (p.x >= x && p.x <= x + w && p.y >= top && p.y <= top + 28) { controls[k].a(); return; } }
   if (p.x >= 70 && p.x <= 770 && p.y >= 110 && p.y <= 950) { float y = p.y - 110 + scroll; for (int i = 0; i < rows.Count; i++) if (y >= rowTop[i] && y < rowTop[i] + rows[i].sizeDelta.y) { Go(i); play = false; return; } }
  }

  void Update() { if (!root || !root.gameObject.activeSelf) return; float dt = Time.unscaledDeltaTime * FxClock.Scale; Step(dt); }
  void Step(float dt) {
   logT += dt;
   if (!log && logT >= .2f) { CloseNow(); return; }
   group.alpha = log ? Css.Smooth(logT / .42f) : 1 - Css.Smooth(logT / .2f); group.blocksRaycasts = log;
   if (content) content.alpha = log ? 1 - Css.Smooth(logT / .25f) : logT < .42f ? Css.Smooth(logT / .42f) : 1;
   if (log) lt += dt;
   if (play && log && lt > Len(steps[idx]) + (idx == steps.Count - 1 ? 1.5f : 0)) Go((idx + 1) % steps.Count);
   playLabel.text = play ? "멈춤" : "재생";
   var pt = Pointer();
   for (int k = 0; k < controls.Count; k++) {
    var r = controls[k].r; float w = controls[k].t.preferredWidth, x = r.anchoredPosition.x, top = -r.anchoredPosition.y + 1.16f * 22 - 21;
    bool overIt = pt.x >= x && pt.x <= x + w && pt.y >= top && pt.y <= top + 28; controlLevel[k] = Css.Toward(controlLevel[k], overIt ? 1 : 0, dt / .12f);
    controls[k].t.color = Color.Lerp(Btn, Color.white, controlLevel[k]);
   }
   if (log && Mathf.Abs(Input.mouseScrollDelta.y) > .01f && pt.x >= 70 && pt.x <= 770 && pt.y >= 110 && pt.y <= 950) SetScroll(scroll - Input.mouseScrollDelta.y * 100);
   if (idx != last) {
    last = idx; for (int i = 0; i < rows.Count; i++) rowGroups[i].alpha = i == idx ? 1 : i < idx ? .55f : .3f;
    bool head = steps[idx].K == "round"; // the mockup's offsetTop quirk: a head row's bar starts 6 px below its own top
    bar.rectTransform.anchoredPosition = new Vector2(0, -(rowTop[idx] + (head ? 6 : 0))); bar.rectTransform.sizeDelta = new Vector2(4, head ? 34 : 44);
    SetScroll(rowTop[idx] - (head ? 6 : 0) - 300);
    var e = steps[idx]; caption.color = Col(e); caption.text = (idx + 1) + " / " + steps.Count + " " + e.T; // the mockup div collapses its "   " to one space
   }
   Draw(idx, lt, dt);
  }
  void SetScroll(float v) { scroll = Mathf.Clamp(v, 0, Mathf.Max(0, contentH - 840)); rowsRoot.anchoredPosition = new Vector2(0, scroll); }

  // ---------- the board at (step, time) — 10_resultlog.js replayBoard().draw ----------
  // label(): 19 px, dark 4 px stroke under the fill (canvas strokeText + fillText); impact captions have no stroke
  TMP_Text Label(string text, Vector2 p, Color col, float a, float size = 19, bool stroke = true) {
   var pool = stroke ? labels : plain; int used = stroke ? labelUsed++ : plainUsed++;
   if (used >= pool.Count) { var t = Ui.Text("글", replica, 0, 0, 600, 60, "", c.font, 19, Color.white); t.alignment = TextAlignmentOptions.Top; t.textWrappingMode = TextWrappingModes.NoWrap; if (stroke) { t.fontMaterial.EnableKeyword("OUTLINE_ON"); t.outlineColor = new Color32(3, 6, 9, 217); t.outlineWidth = .22f; } pool.Add(t); }
   var l = pool[used]; l.gameObject.SetActive(a > 0); l.text = text; l.fontSize = size; l.color = Css.A(col, a); l.rectTransform.anchoredPosition = new Vector2(p.x - 300, -(p.y - 1.16f * size)); l.transform.SetAsLastSibling(); return l;
  }
  FlareGraphic Flare(Vector2 p, Color col, float a, float size) {
   if (flareUsed >= flares.Count) flares.Add(CombatPresentation.Flare(fxLayer, "충돌광", Vector2.zero, col));
   var f = flares[flareUsed++]; f.gameObject.SetActive(a > 0); f.rectTransform.sizeDelta = new Vector2(190 * size, 52 * size); f.rectTransform.anchoredPosition = new Vector2(p.x - 95 * size, -(p.y - 26 * size)); f.color = Css.A(col, a); return f;
  }
  ShotView ShotAt(int k) {
   while (shots.Count <= k) {
    var s = new ShotView(); s.root = Ui.Rect("탄", shotLayer, 0, 0, 1, 1); s.g = s.root.gameObject.AddComponent<CanvasGroup>();
    s.flare = CombatPresentation.Flare(s.root, "가로 빛번짐", Vector2.zero, CY); s.hex = Ui.Box("제로데이 윤곽", s.root, -15, -17, 30, 34, GD, false, 1.5f, 6);
    Ui.Box("둥근 위력 핵", s.root, -10, -13, 20, 26, Css.Rgb(232, 255, 255), true, 1, 48);
    s.num = Ui.Text("위력", s.root, -22, 7 - 1.16f * 20, 44, 36, "", c.font, 20, Css.Rgb(4, 13, 18)); s.num.alignment = TextAlignmentOptions.Top; shots.Add(s);
   }
   return shots[k];
  }
  static Vector2[] Hex(Vector2 c, float w, float h) { var p = new Vector2[6]; for (int i = 0; i < 6; i++) { float a = -Mathf.PI / 2 + i * Mathf.PI / 3; p[i] = c + new Vector2(Mathf.Cos(a) * w / 2, Mathf.Sin(a) * h / 2); } return p; }
  static Color Tint(Color col, int o) { return o == 1 ? new Color(col.b, col.r * .6f, col.g * .55f, col.a) : col; }
  static float Ease3(float t) { t = Mathf.Clamp01(t); return 1 - Mathf.Pow(1 - t, 3); }
  void Ring(FxPainter g, Vector2 p, Color col, float r0, float r1, float t, float len, float w) { if (t < 0 || t > len) return; float k = t / len, e = 1 - Mathf.Pow(1 - k, 3); g.Ring(p, r0 + (r1 - r0) * e, w, Css.A(col, 1 - k)); }
  void Impact(Vector2 p, string cap, Color col, float t, bool below) {
   if (t < 0 || t > .36f) return; float k = t / .36f, e = 1 - Mathf.Pow(1 - k, 3);
   Flare(p, col, 1 - e, 1.15f);
   for (int n = 0; n < 12; n++) { float a = n * Mathf.PI * 2 / 12, r = 14 + e * 58; over.FillRect(p.x + Mathf.Cos(a) * r - 2, p.y + Mathf.Sin(a) * r - 1, 4, 2, Css.A(col, 1 - k)); }
   Label(cap, new Vector2(p.x, p.y + (below ? 44 : -30)), col, 1 - e, 18, false);
  }

  void Draw(int i, float t, float dt) {
   var e = steps[i]; var S = e.S; labelUsed = 0; plainUsed = 0; flareUsed = 0; under.Begin(); over.Begin(); trails.Begin(); hud.Begin();
   // background: blue (my action), red (enemy action), resolve gold, .25 s crossfade
   int bg = BgOf(e); if (bg != bgCur) { bgPrev = bgCur; bgCur = bg; bgT = 0; } bgT = Mathf.Min(1, bgT + dt / .25f);
   for (int k = 0; k < 3; k++) bgs[k].alpha = 0;
   if (bgPrev >= 0 && bgT < 1) bgs[bgPrev].alpha = 1; bgs[bgCur].alpha = bgPrev >= 0 && bgT < 1 ? Css.Smooth(bgT) : 1; bgs[bgCur].transform.SetAsLastSibling();
   pend.text = "강화 카드 대기 / PENDING    나 " + S.Pend[0] + "  ·  상대 " + S.Pend[1];
   // rungs (a rung placed in this step appears in .25 s)
   if (drawnRungs != i) { drawnRungs = i; for (int k = rungLayer.childCount - 1; k >= 0; k--) Destroy(rungLayer.GetChild(k).gameObject); for (int row = 0; row < 3; row++) if (S.Rungs[row] >= 0) { var holder = Ui.Rect("회선 " + row, rungLayer, 0, 0, 1920, 1080); holder.gameObject.AddComponent<CanvasGroup>(); BattleController.DrawRung(holder, row, S.Rungs[row], row == 1 ? 7 : 4.5f, S.Riders[row]); }
    for (int k = gateLayer.childCount - 1; k >= 0; k--) Destroy(gateLayer.GetChild(k).gameObject); foreach (var gp in S.Gates) CombatPresentation.Firewall(gateLayer, gp.x, gp.y); }
   for (int row = 0, n = 0; row < 3; row++) if (S.Rungs[row] >= 0) { bool fresh = e.C == 1 && e.Row == row && (e.K == "me" || e.K == "enemy"); rungLayer.GetChild(n++).GetComponent<CanvasGroup>().alpha = fresh ? Css.Smooth(t / .25f) : 1; }
   bool closing = e.K == "reveal" && e.C == 4;
   foreach (RectTransform gate in gateLayer) { CombatPresentation.CloseFirewall(gate, closing ? Css.Smooth((t - .15f) / .45f) : 1); var cg = gate.GetComponent<CanvasGroup>() ?? gate.gameObject.AddComponent<CanvasGroup>(); cg.alpha = closing ? Css.Smooth(t / .15f) : 1; }
   // shots fly in event / hit steps
   var now = new List<(ReplayLog.Shot s, float k, float a, int n)>(); var launched = new HashSet<int>();
   if (e.K == "event" || e.K == "hit" || e.K == "reveal") foreach (var s in S.Shots) {
    int first = Mathf.Min(s.EventIdx < 0 ? int.MaxValue : s.EventIdx, s.HitIdx < 0 ? int.MaxValue : s.HitIdx); if (i < first) continue;
    launched.Add(s.O * 3 + s.S); float f = Ease3(t / FLY), after = t - FLY;
    if (i == s.EventIdx) { bool dies = s.Power <= 0; float a = !dies || after < 0 ? 1 : Mathf.Max(0, 1 - after / .3f); int num = after >= 0 ? s.Power : s.Initial; if (a > 0) now.Add((s, s.Split * f, a, num)); }
    else if (i == s.HitIdx) { float k0 = s.EventIdx >= 0 ? s.Split : 0; if (after < 0) now.Add((s, k0 + (1 - k0) * f, 1, s.Power)); }
    else if (s.EventIdx >= 0 && i > s.EventIdx && (s.HitIdx < 0 || i < s.HitIdx) && s.Power > 0) now.Add((s, s.Split, 1, s.Power));
   }
   // servers (copies of the game's ServerView)
   for (int o = 0; o < 2; o++) for (int ch = 0; ch < 3; ch++) {
    bool hitHere = e.K == "hit" && e.E != null && 1 - e.E.Shots[0].O == o && e.E.Shots[0].End == ch && t < FLY;
    int alive = hitHere ? 1 : S.H[o, ch], pw = S.P[o, ch];
    servers[o * 3 + ch].Render(alive, pw > 0 && !launched.Contains(o * 3 + ch) ? pw : 0);
    bool fresh = (e.K == "me" || e.K == "enemy") && e.C == 0 && e.Who == o && e.Ch - 1 == ch, zd = e.K == "reveal" && e.C == 3 && e.Who == o && e.Ch - 1 == ch;
    var center = new Vector2(BattleController.LaneX[ch], o == 0 ? 777 : 201);
    float glow = fresh || zd ? 1 - Mathf.Pow(Mathf.Min(1, t / .5f), 2) : 0;
    if (glow > 0 && alive > 0 && pw > 0) under.StrokeClosed(Hex(center, 76, 76), 4, Tint(new Color(.22f, .851f, 1, glow), o));
    if (zd && t < .9f) under.StrokeClosed(Hex(center, 84, 92), 2, Css.A(GD, 1 - t / .9f));
   }
   // shots: trail (10 x 12 px, .3 fading), horizontal bloom, zero-day gold hex, core, power
   for (int k = 0; k < Mathf.Max(now.Count, shots.Count); k++) {
    if (k >= now.Count) { if (k < shots.Count) shots[k].root.gameObject.SetActive(false); continue; }
    var (s, kk, a, n) = now[k]; var v = ShotAt(k); var col = s.O == 0 ? CY : RD; float step = 12 / Mathf.Max(1, s.Len);
    for (int m = 0; m < 10; m++) { float k1 = kk - m * step, k0 = k1 - step; if (k1 <= 0) break; trails.Line(PathMath.At(s.Pts, Mathf.Max(0, k0) * s.Len), PathMath.At(s.Pts, k1 * s.Len), 4, Css.A(col, .3f * (1 - m / 10f) * a)); }
    var p = PathMath.At(s.Pts, Mathf.Clamp01(kk) * s.Len); v.root.gameObject.SetActive(true); v.root.anchoredPosition = new Vector2(p.x, -p.y); v.g.alpha = a; v.flare.color = col; v.hex.gameObject.SetActive(s.Pierce); v.num.text = n.ToString();
   }
   // collision and hit marks
   if ((e.K == "event" || e.K == "hit") && e.E != null) {
    var ev = e.E; float tt = t - FLY;
    if (ev.Hit) { var s = ev.Shots[0]; var p = s.Pts[s.Pts.Count - 1]; int tg = 1 - s.O; Ring(over, p, Color.white, 26, 70, tt, .5f, 3); Ring(over, p, tg == 0 ? CY : RD, 26, 50, tt - .08f, .5f, 3); Impact(p, "서버 소실", s.O == 0 ? CY : RD, tt, false); }
    else { var s = ev.Shots[0]; var p = s.Contact ?? s.Pts[s.Pts.Count - 1]; if (!s.Blocked) Ring(over, p, Color.white, 8, 60, tt, .4f, 2); Impact(p, ev.Cap, GD, tt, s.Blocked && p.y < 487); }
   }
   // the action itself (CardTransferFx.Applied .5 s): server load, rung light, hand refill
   actionCard.enabled = false; revealCard.enabled = false; revealBorder.enabled = false;
   if (e.K == "me" || e.K == "enemy") {
    int o = e.Who; var col = o == 0 ? CY : RD; float a = t < .5f ? 1 - (t / .5f) * (t / .5f) : 0, la = Mathf.Min(1, t / .1f) * (t < .7f ? 1 : Mathf.Max(0, 1 - (t - .7f) / .2f));
    if (e.C == 0) { float y = o == 0 ? 777 : 201; Label("+1 장전", new Vector2(BattleController.LaneX[e.Ch - 1], o == 0 ? y - 52 : y + 66), col, la); }
    else if (e.C == 1) {
     float y = BattleController.RowY[e.Row], mx = (BattleController.LaneX[e.Edge] + BattleController.LaneX[e.Edge + 1]) / 2; var rc = e.Removed ? GD : e.Rider < 0 ? RiderAll : e.Rider == 0 ? RiderMe : RiderEnemy;
     if (a > 0) hud.Line(new Vector2(mx - 140, y), new Vector2(mx + 140, y), 7, Css.A(rc, a));
     Label(e.Removed ? "회선 제거" : "회선 연결 · " + (e.Rider < 0 ? "양쪽" : e.Rider == 0 ? "나만" : "상대만"), new Vector2(mx, y - 34), rc, la);
    } else {
     float y = 420 - 14 * Css.Smooth(t / .3f); actionCard.enabled = la > 0; actionCard.sprite = c.feedback.cardImages[e.C]; actionCard.color = new Color(1, 1, 1, la); actionCard.rectTransform.anchoredPosition = new Vector2(982 - 44, -(y - 70.4f)); actionCard.transform.SetAsLastSibling();
     Label(e.C == 2 || e.C == 5 ? "손패 보충 · " + (e.C == 2 ? 2 : 4) + "장" + (e.C == 5 ? " · 발동 때 공개" : "") : "발동 때 적용 · CH " + e.Ch, new Vector2(982, y + 70.4f + 30), e.C >= 3 && e.C != 5 ? GD : col, la);
    }
   }
   if (e.K == "reveal") {
    float x = e.Ch > 0 ? BattleController.LaneX[e.Ch - 1] : 982, a = Mathf.Min(1, t / .12f) * (t < .8f ? 1 : Mathf.Max(0, 1 - (t - .8f) / .25f));
    revealCard.enabled = revealBorder.enabled = a > 0; revealCard.sprite = c.feedback.cardImages[e.C]; revealCard.color = new Color(1, 1, 1, a); revealBorder.color = Css.A(GD, a);
    revealCard.rectTransform.anchoredPosition = new Vector2(x - 44, -(430 - 70.4f)); revealBorder.rectTransform.anchoredPosition = new Vector2(x - 47, -(430 - 73.4f)); revealBorder.transform.SetAsLastSibling(); revealCard.transform.SetAsLastSibling();
    Label("공개", new Vector2(x, 430 + 70.4f + 28), GD, a);
   }
   if (e.K == "round") { float a = Mathf.Min(1, t / .15f) * (t < .75f ? 1 : Mathf.Max(0, 1 - (t - .75f) / .3f)); Label(e.T.Replace("접속 완료", "ROUND 1"), new Vector2(982, 492), Col(e), a, 34); }
   for (int k = labelUsed; k < labels.Count; k++) labels[k].gameObject.SetActive(false);
   for (int k = plainUsed; k < plain.Count; k++) plain[k].gameObject.SetActive(false);
   for (int k = flareUsed; k < flares.Count; k++) flares[k].gameObject.SetActive(false);
   under.End(); over.End(); trails.End(); hud.End();
  }
 }
 public sealed class ReplayCatcher : MonoBehaviour, IPointerClickHandler {
  [System.NonSerialized] public ReplayView view;
  public void OnPointerClick(PointerEventData e) { if (view && e.button == PointerEventData.InputButton.Left) view.ClickScreen(e.position); }
 }
}

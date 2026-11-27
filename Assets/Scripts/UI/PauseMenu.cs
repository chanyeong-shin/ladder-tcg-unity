using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breach.UI {
 // Week 12 item 9 (_도구/시안12주차/tabs/09_settings.js "flow", kit opts overlay .72, blur 8, bloomFix, cutH 14):
 // Esc in battle -> the battle blurs behind a 72% cover, a vertical column (계속하기 · 환경 설정 · 타이틀로 이동 · 게임 종료) rises 18 px in .42 s.
 // 환경 설정 runs FLOW (.7 s): the column slides x 770 -> 400 over p 0-.6, the settings pane comes in from 60 px right over p .3-1,
 // its cyan top line grows to 640 px and the divider grows down. 돌아가기 / 환경 설정 again / Esc play it backwards.
 // Buttons use the result-screen bloom with bloomFix: every dark oval first, then each bloom with only its own label area (w+160 x h+14) cut.
 // Values: 창 모드 / 전체 화면 and the three resolutions do what the title settings do (Screen + PlayerPrefs); 연출 속도 sets
 // BattleController.animationSpeed for this session (not saved). No control hints (user 11-26): the mockup's "ESC · 계속하기",
 // "값을 누르면 바로 적용됩니다" and "ESC · 돌아가기" lines are left out.
 public sealed class PauseMenu : MonoBehaviour {
  sealed class Item { public RectTransform rt; public float x0, y; public bool pan; public Btn btn; }
  sealed class Btn {
   public float x, y, w, h, scale = 1, bloomDy; public bool vertical = true, oval = true, underline, pan; public Func<bool> sel; public Action click; public float level;
   public TMP_Text label; public SoftDisc ovalG; public CutFlare flare; public Image line; public Item item; public string name;
  }
  static readonly Color CY = Css.Rgb(56, 217, 255), Label = Css.Rgb(209, 237, 247);
  const float Dur = .7f, ColEnd = .6f, PanStart = .3f, X0 = 770, X1 = 400, Y0 = 470, StepY = 85;
  BattleController c; RectTransform root, ovals, blooms, lines, texts; CanvasGroup group; RawImage blurred; Image cover; FxPainter panLines;
  readonly List<Item> items = new List<Item>(); readonly List<Btn> btns = new List<Btn>();
  bool open, ready; float now, openT = 9, e, p, target, savedScale = 1;
  public bool IsOpen { get { return open || e > 0; } }
  public float Flow { get { return p; } }
  public float OpenAmount { get { return e; } }
  public float Clock { get { return now; } }
  public float OpenTime { get { return openT; } }
  public bool SettingsShown { get { return target > 0; } }
  public Vector2? ForcePointer;
  public string HoverName { get { var b = Hover(); return b == null ? "" : b.name; } }

  public void Initialize(BattleController controller) {
   c = controller;
   root = Ui.Rect("인게임 설정 (ESC)", c.stage, 0, 0, 1920, 1080); root.gameObject.SetActive(false);
   blurred = Ui.Rect("흐린 전투", root, 0, 0, 1920, 1080).gameObject.AddComponent<RawImage>(); blurred.raycastTarget = false;
   var layer = Ui.Rect("덮개와 메뉴", root, 0, 0, 1920, 1080); group = layer.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false;
   cover = Ui.Rect("덮개 72%", layer, 0, 0, 1920, 1080).gameObject.AddComponent<Image>(); cover.color = Css.Rgb(5, 8, 11, .72f); cover.raycastTarget = false;
   panLines = FxPainter.Create("설정 칸 선", layer);
   ovals = Ui.Rect("버튼 어둠 타원", layer, 0, 0, 1920, 1080); blooms = Ui.Rect("버튼 빛번짐", layer, 0, 0, 1920, 1080); lines = Ui.Rect("고른 값 밑줄", layer, 0, 0, 1920, 1080); texts = Ui.Rect("글자", layer, 0, 0, 1920, 1080);
   var catcher = root.gameObject.AddComponent<Image>(); catcher.color = Color.clear; catcher.raycastTarget = true; root.gameObject.AddComponent<PauseCatcher>().menu = this;
   // left column (the 2nd mockup's buttons)
   Txt(false, 300, Y0 - 150, 1320, 90, "일시 정지 / PAUSED", 42, CY, TextAlignmentOptions.Top);
   string[] names = { "계속하기 / RESUME", "환경 설정 / SETTINGS", "타이틀로 이동 / TITLE", "게임 종료 / QUIT" };
   Action[] acts = { Close, () => target = target > 0 ? 0 : 1, () => { CloseNow(); c.ReturnToTitle(); }, Application.Quit };
   for (int i = 0; i < 4; i++) { var b = Button(false, X0, Y0 + i * StepY, 380, 65, names[i], 20, acts[i]); if (i == 1) b.sel = () => target > 0; }
   // round info below the QUIT bloom at its 1.2x hover size: top = round(470 + 255 + 32.5 + 125.4 + 6) = 889, first line baseline 924.7 (Chrome range top 909.5 + .8 em)
   summary = Txt(false, 300, 889, 1320, 60, "", 19, Css.Rgb(179, 209, 219), TextAlignmentOptions.Top, 924.7f);
   // right settings pane
   Txt(true, 990, 350, 640, 50, "환경 설정", 30, Color.white, TextAlignmentOptions.Top);
   Row(430, "화면", new[] { "창 모드", "전체 화면" }, k => k == 0 ? !Screen.fullScreen : Screen.fullScreen, k => SetFull(k == 1));
   int[] widths = { 1280, 1600, 1920 };
   Row(520, "해상도", Array.ConvertAll(widths, w => w + "×" + w * 9 / 16), k => CurrentWidth() == widths[k], k => SetWidth(widths[k]));
   float[] speeds = { .5f, 1, 2 };
   Row(610, "연출 속도", new[] { "×0.5", "×1", "×2" }, k => Mathf.Approximately(c.animationSpeed, speeds[k]), k => c.SetAnimationSpeed(speeds[k]));
   var back = Button(true, 990, 745, 240, 60, "돌아가기 / BACK", 21, () => target = 0); back.vertical = false; back.scale = .9f; back.bloomDy = 24; back.oval = false;
   foreach (var b in btns) Dress(b);
  }
  TMP_Text summary;
  // text in a flex-centered box: Chrome centers the 1 em Do Hyeon content area, so the baseline is center + .3 em (ascent .8); TMP top = baseline - 1.16 em
  TMP_Text Txt(bool pan, float x, float y, float w, float h, string text, float size, Color col, TextAlignmentOptions align, float baseline = -1) {
   if (baseline < 0) baseline = y + h / 2 + .3f * size;
   var t = Ui.Text(text, texts, x, baseline - 1.16f * size, w, size * 2, text, c.font, size, col); t.alignment = align; t.textWrappingMode = TextWrappingModes.NoWrap;
   items.Add(new Item { rt = t.rectTransform, x0 = x, y = baseline - 1.16f * size, pan = pan }); return t;
  }
  Btn Button(bool pan, float x, float y, float w, float h, string label, float size, Action click) {
   var b = new Btn { x = x, y = y, w = w, h = h, click = click, pan = pan, name = label };
   b.label = Txt(pan, x, y, w, h, label, size, Label, TextAlignmentOptions.Top); b.item = items[items.Count - 1]; b.item.btn = b; btns.Add(b); return b;
  }
  void Row(float y, string label, string[] values, Func<int, bool> sel, Action<int> pick) {
   Txt(true, 990, y, 150, 60, label, 20, Css.Rgb(150, 180, 190), TextAlignmentOptions.TopLeft);
   for (int i = 0; i < values.Length; i++) { int k = i; var b = Button(true, 1140 + i * 165, y, 150, 60, values[i], 21, () => pick(k)); b.sel = () => sel(k); b.vertical = false; b.scale = .62f; b.bloomDy = 24; b.oval = false; b.underline = true; }
  }
  void Dress(Btn b) {
   if (b.oval) { var r = Ui.Rect(b.name + " 어둠", ovals, 0, 0, b.w + 160 * b.scale, b.h + 60 * b.scale); r.pivot = new Vector2(.5f, .5f); b.ovalG = r.gameObject.AddComponent<SoftDisc>(); b.ovalG.Solid = .55f; b.ovalG.raycastTarget = false; }
   var f = Ui.Rect(b.name + " 빛", blooms, 0, 0, 640, 320); f.pivot = new Vector2(.5f, .5f); b.flare = f.gameObject.AddComponent<CutFlare>(); b.flare.raycastTarget = false; b.flare.Vertical = b.vertical;
   if (b.oval) { b.flare.CutW = b.w + 160 * b.scale; b.flare.CutH = b.h + 14 * b.scale; b.flare.CutOffset = new Vector2(0, -b.bloomDy); }
   if (b.underline) { b.line = Ui.Rect(b.name + " 밑줄", lines, 0, 0, 56, 2).gameObject.AddComponent<Image>(); b.line.raycastTarget = false; }
  }
  int CurrentWidth() { return PlayerPrefs.GetInt("WindowWidth", Screen.width); }
  void SetFull(bool full) { Screen.fullScreen = full; PlayerPrefs.SetInt("Fullscreen", full ? 1 : 0); if (!PlayerPrefs.HasKey("WindowWidth")) PlayerPrefs.SetInt("WindowWidth", 1600); PlayerPrefs.Save(); }
  void SetWidth(int width) { Screen.SetResolution(width, width * 9 / 16, Screen.fullScreen); PlayerPrefs.SetInt("WindowWidth", width); PlayerPrefs.SetInt("Fullscreen", Screen.fullScreen ? 1 : 0); PlayerPrefs.Save(); }

  public void Open() {
   if (open || c.State == null) return;
   var s = c.State; summary.text = "ROUND " + s.Round + " · " + (s.Phase == 2 ? "동시 발동" : (s.Actor == 0 ? "내 " : "상대 ") + (s.Phase == 0 ? "공격" : "방어") + " 턴") + " · 생존 서버  나 " + s.Alive(0) + " / 3  ·  상대 " + s.Alive(1) + " / 3";
   open = true; ready = false; openT = 0; now = 0; e = 0; p = 0; target = 0; foreach (var b in btns) b.level = 0;
   savedScale = UnityEngine.Time.timeScale; UnityEngine.Time.timeScale = 0;
   root.gameObject.SetActive(true); root.SetAsLastSibling(); blurred.color = Color.clear; Apply(0);
   StartCoroutine(Snapshot());
  }
  public void Close() { if (open) { open = false; openT = 0; } }
  public void CloseNow() { open = false; e = 0; p = 0; target = 0; if (root) root.gameObject.SetActive(false); UnityEngine.Time.timeScale = savedScale; }
  public void ToggleSettings() { if (open) target = target > 0 ? 0 : 1; }
  public void Back() { target = 0; }
  public void HandleKeys() { if (Input.GetKeyDown(KeyCode.Escape)) { if (open && target > 0) target = 0; else if (open) Close(); } }
  public void Click() { var b = Hover(); if (b != null) b.click(); }

  // the battle under the cover: rendered once like Week12Check.Shot (camera into a 1920x1080 texture, without this menu), then blurred
  // (CSS blur(8px) = gaussian sigma 8; done at 1/4 size with sigma 2) and shown over the frozen battle with brightness 1 - .25e.
  IEnumerator Snapshot() {
   yield return new WaitForEndOfFrame();
   var canvas = c.stage.GetComponentInParent<Canvas>(); var cam = Camera.main; if (!cam) { ready = true; yield break; }
   var rt = new RenderTexture(1920, 1080, 24); var mode = canvas.renderMode; root.gameObject.SetActive(false);
   canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1; cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render();
   var old = RenderTexture.active; RenderTexture.active = rt; var full = new Texture2D(1920, 1080, TextureFormat.RGB24, false); full.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); full.Apply();
   RenderTexture.active = old; cam.targetTexture = null; canvas.renderMode = mode; rt.Release(); Destroy(rt); root.gameObject.SetActive(open || e > 0);
   const int W = 480, H = 270; var src = full.GetPixels32(); Destroy(full); var a = new Color[W * H];
   for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { float r = 0, g = 0, b = 0; for (int dy = 0; dy < 4; dy++) for (int dx = 0; dx < 4; dx++) { var q = src[(y * 4 + dy) * 1920 + x * 4 + dx]; r += q.r; g += q.g; b += q.b; } a[y * W + x] = new Color(r / 4080, g / 4080, b / 4080, 1); }
   var k = new float[13]; float sum = 0; for (int i = -6; i <= 6; i++) { k[i + 6] = Mathf.Exp(-i * i / 8f); sum += k[i + 6]; } for (int i = 0; i < 13; i++) k[i] /= sum;
   var tmp = new Color[W * H];
   for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { Color s = Color.clear; for (int i = -6; i <= 6; i++) s += a[y * W + Mathf.Clamp(x + i, 0, W - 1)] * k[i + 6]; tmp[y * W + x] = s; }
   for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { Color s = Color.clear; for (int i = -6; i <= 6; i++) s += tmp[Mathf.Clamp(y + i, 0, H - 1) * W + x] * k[i + 6]; s.a = 1; a[y * W + x] = s; }
   if (blurred.texture) Destroy(blurred.texture);
   var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear; tex.SetPixels(a); tex.Apply(); blurred.texture = tex; ready = true;
  }

  Vector2 Pointer() {
   if (ForcePointer.HasValue) return ForcePointer.Value;
   Vector2 q; RectTransformUtility.ScreenPointToLocalPointInRectangle(c.stage, Input.mousePosition, null, out q); return new Vector2(q.x, -q.y);
  }
  float ColE { get { return Css.Smooth(Mathf.Min(1, p / ColEnd)); } }
  float PanE { get { return Css.Smooth(Mathf.Max(0, (p - PanStart) / (1 - PanStart))); } }
  Btn Hover() {
   if (!open || e < .9f) return null; var q = Pointer();
   foreach (var b in btns) if (q.x >= b.x && q.x <= b.x + b.w && q.y >= b.y && q.y <= b.y + b.h) return b.pan && PanE < .9f ? null : b;
   return null;
  }

  void Update() { if (!root || !root.gameObject.activeSelf) return; Apply(UnityEngine.Time.unscaledDeltaTime * FxClock.Scale); }
  void Apply(float dt) {
   now += dt; openT += dt;
   e = open ? Css.Smooth(openT / .42f) : 1 - Css.Smooth(openT / .2f);
   if (!open && e <= 0) { CloseNow(); return; }
   if (!open) target = 0; if (!open && e < .05f) p = 0;
   p = Css.Toward(p, target, dt / Dur);
   group.alpha = e; blurred.color = ready ? new Color(1 - .25f * e, 1 - .25f * e, 1 - .25f * e, e) : Color.clear;
   float ce = ColE, pe = PanE, oe = Css.Smooth(openT / .42f), slide = 18 * (1 - (open ? oe : 1));
   foreach (var it in items) {
    float x = it.pan ? it.x0 + 60 * (1 - pe) : it.x0 + (X1 - X0) * ce;
    it.rt.anchoredPosition = new Vector2(x, -(it.y + slide)); if (it.btn != null) it.btn.x = x;
    if (it.pan) { var t = it.rt.GetComponent<TMP_Text>(); var col = t.color; col.a = pe; t.color = col; t.enabled = pe > 0; }
   }
   // pane lines: cyan top line grows from the left to 640 px, divider grows down (both fade in with the pane)
   panLines.Begin(); if (pe > 0) { float dx = 60 * (1 - pe); panLines.FillRect(990 + dx, 330, 640 * pe, 2, Css.A(CY, pe)); panLines.FillRect(900, 330, 1, 470 * pe, Css.Rgb(56, 217, 255, .25f * pe)); } panLines.End();
   var hv = Hover(); float breathe = .5f + .5f * Mathf.Sin(now * 2.2f);
   foreach (var b in btns) {
    b.level = Css.Toward(b.level, b == hv ? 1 : 0, dt / .12f);
    bool sel = b.sel != null && b.sel(); float a = (.12f + .08f * breathe) + (.6f - (.12f + .08f * breathe)) * b.level, al = b.pan ? pe : 1;
    float cx = b.x + b.w / 2, cy = b.y + b.h / 2 + slide;
    if (b.ovalG) { b.ovalG.rectTransform.anchoredPosition = new Vector2(cx, -cy); b.ovalG.color = new Color(3 / 255f, 5 / 255f, 8 / 255f, .92f * al); }
    float s = (1 + .2f * b.level) * b.scale; b.flare.rectTransform.anchoredPosition = new Vector2(cx, -(cy + b.bloomDy)); b.flare.Set(209 * s, 57.2f * s, Css.A(CY, (sel ? Mathf.Max(a, .35f) : a) * al)); b.flare.enabled = al > 0;
    if (b.line) { b.line.enabled = sel && al > 0; b.line.rectTransform.anchoredPosition = new Vector2(cx - 28, -(b.y + b.h - 8 + slide)); b.line.color = Css.Rgb(56, 217, 255, .9f * al); }
    var basec = sel ? CY : Label; var tc = Color.Lerp(basec, Color.white, b.level); tc.a = b.pan ? pe : 1; b.label.color = tc;
   }
  }
  void OnDisable() { if (open) { UnityEngine.Time.timeScale = savedScale; open = false; } }
 }
 public sealed class PauseCatcher : MonoBehaviour, IPointerClickHandler { [NonSerialized] public PauseMenu menu; public void OnPointerClick(PointerEventData e) { if (menu && e.button == PointerEventData.InputButton.Left) menu.Click(); } }
}

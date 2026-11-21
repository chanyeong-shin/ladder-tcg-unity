using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Breach.UI
{
    public sealed class BattleController : MonoBehaviour
    {
        public RectTransform stage, hand, fx, paths, rungs; public GameObject title, loading, game, result, logPanel;
        public GameObject[] backgrounds; public CardView[] cardPrefabs; public GameObject backPrefab;
        public ServerView[] servers; public Button endButton, undoButton, logButton, startButton, retryButton;
        public Button[] laneTargets, rungTargets; public TMP_Text roundLabel, statusLabel, secretLabel, resultLabel, logText;
        public TMP_FontAsset font, titleFont; public TitleBScreen titleScreen { get; private set; } public float animationSpeed = 1; public CardTransferFx feedback;
        public CombatPresentation presentation { get; private set; }
        UiSequencePresentation sequences; bool beginning;
        int visibleHandLimit = int.MaxValue, visibleEnemyLimit = int.MaxValue;
        int EnemyHandShown { get { return State == null ? 0 : Mathf.Min(visibleEnemyLimit, HideEnemyActions ? publicHandCount : State.Hands[1].Count); } }
        RectTransform hints; Button socketTarget; Coroutine handTween; Vector2 playOrigin, playSize; bool fromDrop;
        public Battle State { get; private set; }
        public bool CanInput { get { return State != null && State.Winner == -1 && State.Actor == 0 && State.Phase < 2 && !busy && !bridge && game.activeSelf; } }
        CardHoldFx hold; BridgeDots bridge; bool bridgeChosen; public bool BridgeOpen { get { return bridge; } }
        public void PickBridge(int color) { if (bridge) bridge.Choose(color); }
        public void CancelBridge() { if (bridge) bridge.Cancel(); }
        public static string RiderName(int rider) { return rider < 0 ? "양쪽" : rider == 0 ? "나만" : "상대만"; }
        public static Color RiderColor(int rider) { return rider < 0 ? BridgeDots.All : rider == 0 ? BridgeDots.Me : BridgeDots.Enemy; }
        bool busy, dragging, skipping; int selected = -1, hover = -1; CardView dragged; TMP_Text skipHint;
        readonly List<int> undoLogMarks = new List<int>();
        // Space or a click fast-forwards every running presentation through its normal code path,
        // so each effect still reaches its end state. It stops at the next turn caption or input point.
        public const float SkipSpeed = 1000;
        public bool Skipping { get { return skipping; } }
        public float PlaybackSpeed { get { return skipping ? SkipSpeed : animationSpeed; } }
        readonly List<CardView> cards = new List<CardView>(); readonly List<string> log = new List<string>();
        int[,] publicPower = new int[2, 3]; int[] publicRungs = { -1, -1, -1 }; int[] publicOwners = { -1, -1, -1 }; int[] publicRiders = { -1, -1, -1 }; int publicSecrets, publicHandCount; readonly List<string> deferredLog = new List<string>(); readonly List<Secret> deferredEffects = new List<Secret>();
        public static readonly float[] LaneX = { 702, 982, 1262 }; public static readonly float[] RowY = { 364, 497, 630 };
        void Start()
        {
            titleScreen = gameObject.AddComponent<TitleBScreen>(); titleScreen.Initialize(this);
            presentation = gameObject.AddComponent<CombatPresentation>(); presentation.Initialize(this); sequences = gameObject.AddComponent<UiSequencePresentation>(); sequences.Initialize(this); gameObject.AddComponent<Week9Check>(); gameObject.AddComponent<NovemberCheck>(); gameObject.AddComponent<FixCheck>(); hold = gameObject.AddComponent<CardHoldFx>(); hold.Initialize(this); startButton.onClick.AddListener(RequestBegin); retryButton.onClick.AddListener(RequestBegin); endButton.onClick.AddListener(EndTurn); undoButton.onClick.AddListener(Undo); logButton.onClick.AddListener(() => logPanel.SetActive(!logPanel.activeSelf));
            for (int i = 0; i < laneTargets.Length; i++) { int t = i; laneTargets[i].onClick.AddListener(() => Use(t, false)); }
            for (int i = 0; i < rungTargets.Length; i++) { int t = i; rungTargets[i].onClick.AddListener(() => Use(t, true)); }
            hints = Ui.Rect("배치 가능한 지점", game.transform, 0, 0, 1920, 1080); hints.SetSiblingIndex(paths.GetSiblingIndex() + 1);
            var socketHit = Ui.Rect("소켓 사용 판정", game.transform, 223, 381, 228, 236); var hitImage = socketHit.gameObject.AddComponent<Image>(); hitImage.color = Color.clear; socketTarget = socketHit.gameObject.AddComponent<Button>(); socketTarget.targetGraphic = hitImage; socketTarget.transition = Selectable.Transition.None; socketTarget.onClick.AddListener(() => Use(0, false)); socketHit.gameObject.SetActive(false);
            skipHint = Ui.Text("연출 건너뛰기 안내", game.transform, 1459, 900, 287, 24, "SPACE · 클릭  연출 건너뛰기", font, 14, new Color(.55f, .7f, .76f)); skipHint.raycastTarget = false; skipHint.gameObject.SetActive(false);
            title.SetActive(true); game.SetActive(false); result.SetActive(false); logPanel.SetActive(false); sequences.ShowTitle();
        }
        void Update()
        {
            if (title.activeSelf) { skipping = false; titleScreen.HandleKeys(); return; }
            if (skipping && (State == null || CanInput || result.activeSelf || !game.activeSelf)) skipping = false;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) SkipPresentation();
            if (skipHint) { bool show = busy && !skipping && game.activeSelf && !result.activeSelf; if (skipHint.gameObject.activeSelf != show) skipHint.gameObject.SetActive(show); }
            if (Input.GetKeyDown(KeyCode.Return) && result.activeSelf) { RequestBegin(); return; }
            if (Input.GetKeyDown(KeyCode.Escape) && result.activeSelf) { ReturnToTitle(); return; }
            if (Input.GetKeyDown(KeyCode.Escape) && bridge) CancelBridge();
            else if (Input.GetKeyDown(KeyCode.Escape) && CanInput) { selected = -1; hold.Hide(); ClearPreview(); LayoutHand(-1); RefreshTargets(); }
            if (Input.GetKeyDown(KeyCode.Return) && CanInput) EndTurn();
        }
        public void SkipPresentation() { if (State != null && busy && !dragging && game.activeSelf && !result.activeSelf) skipping = true; }
        void RequestBegin()
        {
            if (beginning || (!title.activeSelf && !result.activeSelf) || (title.activeSelf && titleScreen.ModalOpen)) return;
            StopAllCoroutines(); handTween = null; StopAllEffects();
            beginning = true; busy = true; dragging = false; fromDrop = false; skipping = false;
            if (dragged) Destroy(dragged.gameObject); dragged = null;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            StartCoroutine(Begin());
        }
        public void ReturnToTitle()
        {
            StopAllCoroutines(); handTween = null; StopAllEffects();
            beginning = false; busy = false; dragging = false; fromDrop = false; skipping = false; selected = -1; hover = -1;
            if (dragged) Destroy(dragged.gameObject); dragged = null;
            visibleHandLimit = visibleEnemyLimit = int.MaxValue;
            State = null; game.SetActive(false); result.SetActive(false); logPanel.SetActive(false);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            sequences.ShowTitle();
        }
        IEnumerator Begin()
        {
            StopAllEffects(); beginning = true; busy = true;
            title.SetActive(false); result.SetActive(false); game.SetActive(false);
            State = new Battle((uint)Environment.TickCount); log.Clear(); deferredLog.Clear(); deferredEffects.Clear();
            selected = -1; logPanel.SetActive(false); CapturePublic();
            visibleHandLimit = visibleEnemyLimit = 0;
            game.SetActive(true);
            Note("접속 완료 · " + (State.Attacker == 0 ? "내" : "상대") + " 공격 턴"); undoLogMarks.Clear();
            Render(); yield return DrawFeedback(0, 0); yield return TurnFeedback();
            busy = false; beginning = false; RefreshTargets(); yield return BotFlow();
        }
        void CapturePublic() { publicPower = (int[,])State.Power.Clone(); publicRungs = (int[])State.Rungs.Clone(); publicOwners = (int[])State.RungOwners.Clone(); publicRiders = (int[])State.RungRiders.Clone(); publicSecrets = State.Hidden.Count(s => s.Owner == 1); publicHandCount = State.Hands[1].Count; }
        // user 11-22: every bot action is public the moment it is placed (no hidden defense turn).
        bool HideEnemyActions { get { return false; } }
        public void Render()
        {
            if (State == null) return; int phase = State.Phase == 2 ? 2 : State.Actor == 0 ? 0 : 1; for (int i = 0; i < 3; i++) backgrounds[i].SetActive(i == phase);
            foreach (var s in servers) s.Render(State.Health[s.owner, s.channel], s.owner == 1 && HideEnemyActions ? publicPower[1, s.channel] : State.Power[s.owner, s.channel]);
            foreach (var root in backgrounds)
            {
                Color accent = phase == 0 ? Ui.Cyan : phase == 1 ? Ui.Red : Ui.Gold;
                for (int step = 0; step < 3; step++) { var block = Find(root.transform, "단계 " + step); if (!block) continue; foreach (var graphic in block.GetComponentsInChildren<Graphic>(true)) { bool active = step == State.Phase; graphic.color = graphic is TMP_Text ? (active ? accent : new Color(.35f, .45f, .5f)) : (graphic.gameObject == block.gameObject ? (active ? new Color(accent.r * .12f, accent.g * .12f, accent.b * .12f, 1) : new Color(.035f, .055f, .07f, 1)) : (active ? accent : new Color(.14f, .22f, .26f, 1))); } }
                for (int owner = 0; owner < 2; owner++) for (int lane = 0; lane < 3; lane++) { var meter = Find(root.transform, "Source_64_" + ((owner == 0 ? 478 : 489) + lane * 3)); if (meter) { var graphic = meter.GetComponent<Graphic>(); if (graphic) graphic.color = State.Health[owner, lane] > 0 ? (owner == 0 ? Ui.Cyan : Ui.Red) : new Color(.16f, .18f, .2f); } }
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.gameObject.name == "Source_64_476") t.text = State.Alive(0) + " / 3";
                    if (t.gameObject.name == "Source_64_487") t.text = State.Alive(1) + " / 3";
                    if (t.gameObject.name == "Source_64_372") t.text = "ENEMY HAND   " + EnemyHandShown.ToString("00");
                    if (t.gameObject.name == "Source_64_435") t.text = busy ? "신호 전송 / TRANSFER" : "빈 소켓 / READY";
                    if (t.gameObject.name == "단계와 행동 담당") { if (t.text.StartsWith("01")) t.text = "01 공격 턴 / ATTACK - " + (State.Attacker == 0 ? "나" : "상대"); else if (t.text.StartsWith("02")) t.text = "02 방어 턴 / DEFEND - " + (State.Attacker == 0 ? "상대" : "나"); }
                }
                for (int i = 0; i < 5; i++) { var n = Find(root.transform, "Enemy_Back_" + (374 + i)); if (n) n.gameObject.SetActive(i < EnemyHandShown); var holder = Find(root.transform, "Source_64_" + (374 + i)); if (holder) holder.gameObject.SetActive(i < EnemyHandShown); }
            }
            roundLabel.text = "ROUND " + State.Round.ToString("00"); statusLabel.text = State.Phase == 2 ? "동시 발동 · 패킷 해결 중" : State.Actor == 0 ? "내 " + (State.Phase == 0 ? "공격" : "방어") + " 턴 · 카드를 끌어 놓거나 선택 후 대상을 누르세요" : "상대 " + (State.Phase == 0 ? "공격" : "방어") + " 턴 · 대응 준비 중";
            secretLabel.text = "강화 카드 대기 / PENDING    나 " + State.Hidden.Count(s => s.Owner == 0) + "  ·  상대 " + (HideEnemyActions ? publicSecrets : State.Hidden.Count(s => s.Owner == 1));
            RefreshBoard(); BuildHand(); RefreshTargets();
        }
        public static Transform Find(Transform root, string name) { foreach (Transform t in root) { if (t.name == name) return t; var n = Find(t, name); if (n) return n; } return null; }
        void Clear(Transform root) { for (int i = root.childCount - 1; i >= 0; i--) { var g = root.GetChild(i).gameObject; g.SetActive(false); Destroy(g); } }
        void RefreshBoard(bool showWalls = true)
        {
            Clear(rungs); var rows = HideEnemyActions ? publicRungs : State.Rungs; var riders = HideEnemyActions ? publicRiders : State.RungRiders;
            for (int row = 0; row < 3; row++) { if (rows[row] >= 0) { int c = rows[row];float inset=row==1?7:4.5f; DrawRung(row, c, inset, riders[row]); } else for (int edge = 0; edge < 2; edge++) for (int k = 0; k < 9; k++) Ui.Line("빈 배치 자리", rungs, new Vector2(LaneX[edge] + 20 + k * 27, RowY[row]), new Vector2(LaneX[edge] + 30 + k * 27, RowY[row]), new Color(.15f, .26f, .3f, .6f), 1); }
            if(showWalls)foreach(var point in CombatPresentation.FirewallEndpoints(State))CombatPresentation.Firewall(rungs,point.x,point.y);
        }
        // Placed rung (HTML tab 6 drawBoard): rider color, stroke 5 under a blur-4 glow, ▲ me / ▼ enemy / ◆ both above the middle.
        void DrawRung(int row, int c, float inset, int rider)
        {
            Color col = RiderColor(rider); Vector2 a = new Vector2(LaneX[c] + inset, RowY[row]), b = new Vector2(LaneX[c + 1] - inset, RowY[row]);
            Ui.Line("회선 빛 " + (row + 1), rungs, a, b, new Color(col.r, col.g, col.b, .3f), 5 + 12); Ui.Line("회선 " + (row + 1), rungs, a, b, col, 5);
            var m = (a + b) / 2; var mark = Ui.Box("회선 표시 " + (rider < 0 ? "양쪽" : rider == 0 ? "나만" : "상대만"), rungs, 0, 0, 14, 14, col, true, 1, rider < 0 ? 4 : 3);
            mark.rectTransform.pivot = new Vector2(.5f, .5f); mark.rectTransform.anchoredPosition = new Vector2(m.x, -(m.y - 20));
            if (rider < 0) { mark.rectTransform.sizeDelta = new Vector2(11, 11); mark.rectTransform.localRotation = Quaternion.Euler(0, 0, 45); }
            else if (rider == 1) mark.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
        }
        void HoldDraws(int mineBefore, int enemyBefore, bool showEnemy = true)
        {
            visibleHandLimit = State.Hands[0].Count > mineBefore ? mineBefore : int.MaxValue;
            visibleEnemyLimit = showEnemy && State.Hands[1].Count > enemyBefore ? enemyBefore : int.MaxValue;
        }
        void RefreshEnemyHandDisplay()
        {
            int count = EnemyHandShown;
            foreach (var background in backgrounds)
            {
                var label = Find(background.transform, "Source_64_372");
                if (label) { var text = label.GetComponent<TMP_Text>(); if (text) text.text = "ENEMY HAND   " + count.ToString("00"); }
                for (int i = 0; i < 5; i++)
                {
                    var back = Find(background.transform, "Enemy_Back_" + (374 + i));
                    var holder = Find(background.transform, "Source_64_" + (374 + i));
                    if (back) back.gameObject.SetActive(i < count);
                    if (holder) holder.gameObject.SetActive(i < count);
                }
            }
        }
        IEnumerator DrawFeedback(int mineBefore, int enemyBefore, bool showEnemy = true)
        {
            int mineAdded = Mathf.Max(0, State.Hands[0].Count - mineBefore);
            int enemyAdded = showEnemy ? Mathf.Max(0, State.Hands[1].Count - enemyBefore) : 0;
            visibleHandLimit = visibleEnemyLimit = int.MaxValue;
            if (mineAdded == 0 && enemyAdded == 0) yield break;
            bool previous = busy; busy = true; RefreshTargets();
            if (mineAdded > 0) BuildHand();
            if (handTween != null) { StopCoroutine(handTween); handTween = null; }
            RefreshEnemyHandDisplay();
            var arrivals = cards.Skip(Mathf.Max(0, cards.Count - mineAdded)).Where(v => v).Select(v => (RectTransform)v.transform).ToArray();
            yield return sequences.Draw(arrivals, enemyAdded, State.Hands[1].Count);
            busy = previous; RefreshTargets();
        }
        void BuildHand(int visibleLimit = int.MaxValue)
        {
            if (dragging) return;
            visibleLimit = Mathf.Min(visibleLimit, visibleHandLimit);
            if (handTween != null) StopCoroutine(handTween);
            Clear(hand); cards.Clear();
            for (int i = 0; i < Mathf.Min(State.Hands[0].Count, visibleLimit); i++)
            {
                var view = Instantiate(cardPrefabs[(int)State.Hands[0][i]], hand);
                view.gameObject.SetActive(true); view.controller = this; view.index = i; cards.Add(view);
            }
            LayoutHand(-1, true);
        }
        public void Hover(int index) { if (!CanInput || dragging || selected >= 0) return; LayoutHand(index); }
        public void LayoutHand(int index, bool instant = false) { hover = index; if (handTween != null) StopCoroutine(handTween); handTween = StartCoroutine(HandMotion(index, instant)); }
        IEnumerator HandMotion(int index, bool instant)
        {
            var views = cards.Where(c => c).ToArray(); var starts = views.Select(c => ((RectTransform)c.transform).anchoredPosition).ToArray(); var scales = views.Select(c => c.transform.localScale.x).ToArray(); var ends = new Vector2[views.Length]; var sizes = new float[views.Length];
            float total = views.Length * 126 + (views.Length - 1) * 12 + (index >= 0 ? 75.6f : 0), x = 982 - total / 2;
            for (int i = 0; i < views.Length; i++) { sizes[i] = i == index ? 1.6f : 1; ends[i] = new Vector2(x, -(1064 - 201.6f * sizes[i])); x += 126 * sizes[i] + 12; }
            if (index >= 0 && index < views.Length) views[index].transform.SetAsLastSibling();
            for (float t = instant ? 1 : 0; t < 1; t += Time.unscaledDeltaTime / .15f) { float e = t * t * (3 - 2 * t); for (int i = 0; i < views.Length; i++) if (views[i]) { ((RectTransform)views[i].transform).anchoredPosition = Vector2.Lerp(starts[i], ends[i], e); views[i].transform.localScale = Vector3.one * Mathf.Lerp(scales[i], sizes[i], e); } yield return null; }
            for (int i = 0; i < views.Length; i++) if (views[i]) { ((RectTransform)views[i].transform).anchoredPosition = ends[i]; views[i].transform.localScale = Vector3.one * sizes[i]; }
        }
        public void Select(int index) { if (!CanInput || index < 0 || index >= cards.Count) return; selected = selected == index ? -1 : index; if (selected >= 0) hold.Show(); else hold.Hide(); LayoutHand(selected); RefreshTargets(); }
        public void BeginDrag(CardView v) { if (!CanInput) return; if (handTween != null) StopCoroutine(handTween); ClearPreview(); hold.Show(); selected = v.index; dragging = true; dragged = v; v.transform.SetParent(stage, false); v.transform.SetAsLastSibling(); var g = v.gameObject.AddComponent<CanvasGroup>(); g.blocksRaycasts = false; RefreshTargets(); }
        public int AmmoDropTarget(Vector2 screen) { if (!dragging || selected < 0 || selected >= State.Hands[0].Count) return -1; Card card = State.Hands[0][selected]; if (card != Card.Inject && card != Card.ZeroDay) return -1; Vector2 p; RectTransformUtility.ScreenPointToLocalPointInRectangle(stage, screen, null, out p); p.y = -p.y; if (p.y < 231 || p.y > 832) return -1; int lane = Enumerable.Range(0, 3).OrderBy(i => Mathf.Abs(p.x - LaneX[i])).First(); float width = p.y >= 722 ? 50 : 28; return Mathf.Abs(p.x - LaneX[lane]) <= width && State.CanPlay(0, selected, lane) ? lane : -1; }
        public void Drop(Vector2 screen)
        {
            if (!dragging) return; Vector2 p; RectTransformUtility.ScreenPointToLocalPointInRectangle(stage, screen, null, out p); p.y = -p.y; int ammoTarget = AmmoDropTarget(screen); dragging = false; var v = dragged; dragged = null; if (v) { var rect = (RectTransform)v.transform; playOrigin = new Vector2(rect.anchoredPosition.x, -rect.anchoredPosition.y); playSize = Vector2.Scale(rect.sizeDelta, new Vector2(rect.localScale.x, rect.localScale.y)); }
            int index = selected; int target = -1; bool rung = false;
            if (CanInput && index >= 0 && index < State.Hands[0].Count)
            {
                Card c = State.Hands[0][index]; if (c == Card.Reroute) { rung = true; for (int t = 0; t < 6; t++) if (new Rect(LaneX[t % 2] + 10, RowY[t / 2] - 42, 260, 84).Contains(p)) { target = t; break; } }
                else if (c == Card.Buffer || c == Card.Overflow) { if (new Rect(582, 129, 800, 716).Contains(p) || new Rect(173, 347, 332, 322).Contains(p)) target = 0; }
                else if (c == Card.Inject || c == Card.ZeroDay) target = ammoTarget; else if (new Rect(582, 129, 800, 716).Contains(p)) target = Enumerable.Range(0, 3).OrderBy(i => Mathf.Abs(p.x - LaneX[i])).First();
            }
            if (target >= 0 && State.CanPlay(0, index, target)) { fromDrop = true; if (v) { v.gameObject.SetActive(false); Destroy(v.gameObject); } Use(target, rung); return; }
            selected = -1; StartCoroutine(ReturnCard(v));
        }
        IEnumerator ReturnCard(CardView view) { busy = true; hold.Hide(); ClearPreview(); RefreshTargets(); if (view) view.transform.SetParent(hand, false); LayoutHand(-1); yield return new WaitForSecondsRealtime(.18f); busy = false; fromDrop = false; Render(); statusLabel.text = "배치 취소 · 손패로 돌아왔습니다."; }
        public void RefreshTargets()
        {
            bool input = CanInput; endButton.interactable = input; undoButton.interactable = input && State.CanUndo; if (hints) Clear(hints); bool selectedCard = input && selected >= 0 && selected < State.Hands[0].Count; Card card = selectedCard ? State.Hands[0][selected] : Card.Inject;
            // A rung card targets rung slots only; channel targets would otherwise catch hover/drop over the vertical lines.
            for (int i = 0; i < laneTargets.Length; i++) { laneTargets[i].gameObject.SetActive(!(selectedCard && card == Card.Reroute)); laneTargets[i].interactable = selectedCard && card != Card.Reroute && State.CanPlay(0, selected, i); laneTargets[i].transition = Selectable.Transition.None; laneTargets[i].targetGraphic.color = Color.clear; }
            for (int i = 0; i < rungTargets.Length; i++) { bool active = selectedCard && card == Card.Reroute; rungTargets[i].gameObject.SetActive(active); rungTargets[i].interactable = active && State.CanPlay(0, selected, i); rungTargets[i].transition = Selectable.Transition.None; rungTargets[i].targetGraphic.color = Color.clear; }
            if (socketTarget) socketTarget.gameObject.SetActive(selectedCard && (card == Card.Buffer || card == Card.Overflow)); if (!selectedCard || !hints) return;
            if (card == Card.Reroute) { for (int t = 0; t < 6; t++) if (State.CanPlay(0, selected, t)) Ui.Line("합법 회선 자리", hints, new Vector2(LaneX[t % 2] + 12, RowY[t / 2]), new Vector2(LaneX[t % 2 + 1] - 12, RowY[t / 2]), new Color(1, .8f, .2f, .55f), 2); }
            else if (card == Card.Buffer || card == Card.Overflow) Ui.Box("소켓에 사용", hints, 223, 381, 228, 236, Ui.Cyan, false, 2);
            else for (int lane = 0; lane < 3; lane++) if (State.CanPlay(0, selected, lane)) { if (card == Card.Firewall) Ui.Box("차단할 채널", hints, LaneX[lane] - 14, 477, 28, 40, Ui.Gold, false, 2); else { // HTML tab 4 targets(): r 18 at y 720, fill rgba(56,217,255,.12), stroke #7fe9ff 3, stroke-opacity .55+.45|sin(4u+i)|
                    // user 11-22: "서버에서 가로 빛번짐 효과" — the attack bloom sits on the server and blinks
                    var bloom = CombatPresentation.Flare(hints, "장전할 서버 빛번짐", new Vector2(LaneX[lane], 777), Ui.Cyan, .9f); bloom.gameObject.AddComponent<RingPulse>().Phase = lane; } }
        }
        public void Preview(int target, bool rung)
        {
            if (State == null || busy) return; ClearPreview(); if (selected >= 0 && selected < State.Hands[0].Count) { Card card = State.Hands[0][selected]; if ((card == Card.Reroute) != rung) return; if (card == Card.Buffer || card == Card.Overflow) return; if (!State.CanPlay(0, selected, target)) return; if (card == Card.Firewall) { Ui.Line("차단 채널 미리보기", paths, new Vector2(LaneX[target], 231), new Vector2(LaneX[target], 763), Ui.Gold, 3); return; } }
            else if (rung) return;
            if (rung) { if (selected >= 0 && State.CanPlay(0, selected, target)) Ui.Line("배치 미리보기", paths, new Vector2(LaneX[target % 2], RowY[target / 2]), new Vector2(LaneX[target % 2 + 1], RowY[target / 2]), Ui.Gold, 5); return; }
            var p = State.Route(0, target); var points = Points(p); for (int i = 1; i < points.Count; i++) Ui.Line("공개 경로", paths, points[i - 1], points[i], new Color(1, .8f, .2f, .65f), 3);
            if (CanInput) statusLabel.text = "CH " + (target + 1) + " → 상대 CH " + (p.End + 1) + " · 공개된 경로 기준";
        }
        public void ClearPreview() { Clear(paths); }
        public void PreviewServer(int owner, int channel) { if (State == null || busy) return; int amount = owner == 1 && HideEnemyActions ? publicPower[owner, channel] : State.Power[owner, channel]; if (amount <= 0) return; ClearPreview(); var packet = State.Route(owner, channel); var points = Points(packet); for (int i = 1; i < points.Count; i++) Ui.Line("장전 패킷 경로", paths, points[i - 1], points[i], Ui.Gold, 3); }
        public void Use(int target, bool rung) { if (!CanInput || selected < 0 || selected >= State.Hands[0].Count) return; if ((State.Hands[0][selected] == Card.Reroute) != rung) return;
            bool chosen = bridgeChosen; bridgeChosen = false;
            if (!chosen && rung && target < 6 && State.Rungs[target / 2] == -1 && State.CanPlay(0, selected, target)) { OpenBridge(target); return; } if (!State.CanPlay(0, selected, target)) { statusLabel.text = "이 위치에는 사용할 수 없습니다."; return; } int i = selected; if (!fromDrop && i < cards.Count && cards[i]) { var r = (RectTransform)cards[i].transform; playOrigin = new Vector2(r.anchoredPosition.x, -r.anchoredPosition.y); playSize = r.sizeDelta * r.localScale.x; } fromDrop = false; selected = -1; ClearPreview(); StartCoroutine(Play(0, i, target)); }
        // Color bridge B: the dropped rung waits as a gold line with three dots; a dot decides who rides it.
        void OpenBridge(int slot)
        {
            int index = selected; bool dropped = fromDrop;
            if (!dropped && index < cards.Count && cards[index]) { var r = (RectTransform)cards[index].transform; playOrigin = new Vector2(r.anchoredPosition.x, -r.anchoredPosition.y); playSize = r.sizeDelta * r.localScale.x; }
            ClearPreview(); Vector2 a = new Vector2(LaneX[slot % 2] + 16, RowY[slot / 2]), b = new Vector2(LaneX[slot % 2 + 1] - 16, RowY[slot / 2]);
            statusLabel.text = "점을 골라 이 가로선을 탈 쪽을 정하세요 · 파랑 나만 · 노랑 양쪽 · 빨강 상대만 · 다른 곳을 누르면 취소";
            bridge = BridgeDots.Show(game.transform, a, b,
                color => { bridge = null; selected = index; fromDrop = dropped; bridgeChosen = true; Use(Battle.RungTarget(slot, color), true); bridgeChosen = false; },
                () => { bridge = null; selected = -1; fromDrop = false; hold.Hide(); ClearPreview(); Render(); LayoutHand(-1); statusLabel.text = "배치 취소 · 카드를 손패로 돌렸습니다."; });
            RefreshTargets();
        }
        IEnumerator Play(int owner, int index, int target)
        {
            Card card = State.Hands[owner][index];
            int mineBefore = State.Hands[0].Count - (owner == 0 ? 1 : 0);
            int enemyBefore = State.Hands[1].Count - (owner == 1 ? 1 : 0);
            int logMark = log.Count;
            if (!State.Play(owner, index, target)) yield break;
            if (owner == 0) { if (State.UndoSteps == 0) undoLogMarks.Clear(); else undoLogMarks.Add(logMark); }
            bool enemyAttack = owner == 1; // all bot actions are shown as they are taken (user 11-22)
            busy = true; HoldDraws(mineBefore, enemyBefore); BuildHand(mineBefore); RefreshTargets();
            Vector2 enemyOrigin = new Vector2(300,188), enemySize = new Vector2(56.875f,91);
            if (owner == 1) {
                // Move the last visible back; keep card identity and effects concealed.
                var active = backgrounds.First(b => b.activeSelf);
                var source = Find(active.transform, "Enemy_Back_" + (374 + Mathf.Clamp(publicHandCount - 1,0,4))) as RectTransform;
                if (source) { Vector3[] corners = new Vector3[4]; source.GetWorldCorners(corners); var top = fx.InverseTransformPoint(corners[1]); var bottom = fx.InverseTransformPoint(corners[3]); enemyOrigin = new Vector2(top.x,-top.y); enemySize = new Vector2(bottom.x-top.x,top.y-bottom.y); }
                publicHandCount = enemyBefore; RefreshEnemyHandDisplay();
            }
            feedback.speed = animationSpeed;
            yield return feedback.Play(card, owner, owner == 0 ? playOrigin : enemyOrigin, owner == 0 ? playSize : enemySize);
            string enemyEntry = "상대 · " + Cards.Names[(int)card] + (Cards.Enhanced(card) ? " (발동 때 적용)" : "");
            if (enemyAttack)
            {
                // Attack-turn actions become public at end of action anyway (spec 7-2), so each one is shown
                // as it is taken. Security cards show only that one was set (spec 6.3).
                Note(enemyEntry); CapturePublic(); Render();
                bool onBoard = !Cards.Enhanced(card) && card != Card.Buffer;
                statusLabel.text = Cards.Enhanced(card) ? "상대 · " + Cards.Names[(int)card] + (card == Card.Overflow ? "" : " → CH " + (target + 1)) + " · 발동 때 적용" : card == Card.Inject ? "상대 · 코드 주입 → CH " + (target + 1) : card == Card.Reroute ? "상대 · 라우팅 변경 · " + (Battle.Slot(target) / 2 + 1) + "단 · " + (State.Rungs[Battle.Slot(target) / 2] >= 0 ? RiderName(State.RungRiders[Battle.Slot(target) / 2]) : "제거") : "상대 · 버퍼 적재";
                if (onBoard) yield return feedback.Applied(card, owner, target, card != Card.Reroute || State.Rungs[Battle.Slot(target) / 2] >= 0);
            }
            else if (owner == 1)
            {
                // Defense-turn actions stay secret until resolve (spec 7-3): only the card back movement is shown.
                deferredLog.Add(enemyEntry); if (!Cards.Enhanced(card) && card != Card.Buffer) deferredEffects.Add(new Secret(owner, card, target));
                Render(); statusLabel.text = "상대 방어 · 비공개 행동 " + deferredLog.Count + "건 [ 해결 때 공개 ]";
            }
            else { Note("나 · " + Cards.Names[(int)card]); Render(); if (!Cards.Enhanced(card)) yield return feedback.Applied(card, owner, target, card != Card.Reroute || State.Rungs[Battle.Slot(target) / 2] >= 0); }
            // Board effects remain concealed; visible hand counts follow removal then draw.
            if(owner==1)publicHandCount=State.Hands[1].Count;
            yield return DrawFeedback(mineBefore, enemyBefore);
            if (owner == 1) { string shown = statusLabel.text; Render(); statusLabel.text = shown; yield return Wait(.45f); }
            busy = false; Render();
        }
        public void EndTurn() { if (CanInput && !dragging) { selected = -1; ClearPreview(); StartCoroutine(Advance()); } }
        IEnumerator Advance() { busy = true; State.EndAction(); CapturePublic(); if (State.Phase == 2) yield return ResolveRound(); else yield return TurnFeedback(); busy = false; undoLogMarks.Clear(); Render(); yield return BotFlow(); }
        IEnumerator BotFlow()
        {
            if (State == null || State.Actor != 1 || State.Winner != -1 || State.Phase >= 2) yield break; busy = true; Render(); yield return Wait(.55f);
            int guard = 0, index, target; while (State.ChooseBot(out index, out target) && guard++ < 40) { yield return Play(1, index, target); busy = true; RefreshTargets(); yield return Wait(.2f); }
            State.EndAction(); CapturePublic(); foreach (string entry in deferredLog) Note(entry); deferredLog.Clear(); yield return TurnFeedback(); foreach (var effect in deferredEffects) yield return feedback.Applied(effect.Card, effect.Owner, effect.Target, effect.Card != Card.Reroute || State.Rungs[Battle.Slot(effect.Target) / 2] >= 0); deferredEffects.Clear(); if (State.Phase == 2) yield return ResolveRound(false); busy = false; undoLogMarks.Clear(); Render(); if (State.Actor == 1 && State.Winner == -1) yield return BotFlow();
        }
        IEnumerator ResolveRound(bool showTurn = true)
        {
            busy = true; State.Phase = 2; if (showTurn) yield return TurnFeedback();
            int mineBefore = State.Hands[0].Count, enemyBefore = State.Hands[1].Count;
            var report = State.Resolve();
            HoldDraws(mineBefore, enemyBefore); CapturePublic(); RefreshBoard(false);
            foreach (var s in report.Revealed)
            {
                Note((s.Owner == 0 ? "나" : "상대") + " · " + Cards.Names[(int)s.Card] + " 공개");
                yield return Reveal(s);
                if (s.Card == Card.ZeroDay)
                {
                    var shot = report.Packets.FirstOrDefault(p => p.Owner == s.Owner && p.Start == s.Target);
                    if (shot != null) servers.First(v => v.owner == s.Owner && v.channel == s.Target).Render(report.Before[s.Owner, s.Target], shot.Initial);
                }
                if (s.Card != Card.Overflow) yield return feedback.Applied(s.Card, s.Owner, s.Target, true);
            }
            RefreshBoard(); yield return DrawFeedback(mineBefore, enemyBefore);
            yield return Flight(report);
            foreach (string e in report.Events) if (!e.EndsWith(" 공개", StringComparison.Ordinal)) Note(e);
            Render(); yield return Wait(.5f);
            if (State.Winner != -1)
            {
                resultLabel.text = State.Winner == 0 ? "침투 성공 / ACCESS GRANTED" : State.Winner == 1 ? "서버 소실 / CONNECTION LOST" : "동시 전멸 / DRAW";
                yield return sequences.Result(report); yield break;
            }
            mineBefore = State.Hands[0].Count; enemyBefore = State.Hands[1].Count;
            State.NextRound(); HoldDraws(mineBefore, enemyBefore); CapturePublic();
            Note("ROUND " + State.Round + " · " + (State.Attacker == 0 ? "내" : "상대") + " 공격 턴");
            yield return TurnFeedback(); yield return DrawFeedback(mineBefore, enemyBefore);
        }
        // A skip lasts until the next turn caption: each turn's actions stay readable unless skipped again.
        IEnumerator TurnFeedback() { skipping = false; bool previous = busy; busy = true; RefreshTargets(); feedback.speed = animationSpeed; if(State.Phase==2){Render();yield return presentation.BeginResolution();} yield return feedback.Turn(State.Phase, State.Actor, State.Attacker, Render); busy = previous; RefreshTargets(); }
        // One press reverts only the most recent action. Drawn cards are never undone (Battle locks them).
        void Undo()
        {
            if (!CanInput || !State.Undo()) return;
            if (undoLogMarks.Count > 0) { int mark = Mathf.Clamp(undoLogMarks[undoLogMarks.Count - 1], 0, log.Count); undoLogMarks.RemoveAt(undoLogMarks.Count - 1); log.RemoveRange(mark, log.Count - mark); }
            selected = -1; ClearPreview(); CapturePublic(); Render(); logText.text = string.Join("\n", log.Skip(Math.Max(0, log.Count - 16)));
            statusLabel.text = "마지막 행동 1개를 되돌렸습니다." + (State.CanUndo ? " 더 되돌릴 수 있습니다." : "");
        }
        void Note(string message) { log.Add(message); if (log.Count > 200) { log.RemoveAt(0); for (int i = 0; i < undoLogMarks.Count; i++) undoLogMarks[i] = Mathf.Max(0, undoLogMarks[i] - 1); } logText.text = string.Join("\n", log.Skip(Math.Max(0, log.Count - 16))); }
        IEnumerator Wait(float seconds) { for (float t = 0; t < seconds; t += Time.deltaTime * Mathf.Max(.1f, PlaybackSpeed)) yield return null; }
        void StopAllEffects()
        {
            if (sequences) sequences.Cancel();
            if (presentation) presentation.Cancel();
            if (bridge) { Destroy(bridge.gameObject); bridge = null; }
            if (hold) hold.HideNow();
            if (fx) Clear(fx); if (paths) Clear(paths);
            var crt = FindFirstObjectByType<CrtOverlay>(); if (crt) crt.SetBurst(0);
        }
        void OnDisable() { StopAllCoroutines(); handTween = null; StopAllEffects(); }
        IEnumerator Reveal(Secret secret) { var view = Instantiate(cardPrefabs[(int)secret.Card], fx); view.controller = null; view.gameObject.SetActive(true); var rt = (RectTransform)view.transform; Rect dock = CardTransferFx.DockRect; rt.anchoredPosition = new Vector2(dock.x, -dock.y); rt.localScale = Vector3.one * (dock.height / 201.6f); yield return Wait(.45f); Destroy(view.gameObject); }
        public static List<Vector2> Points(Packet p) { var points = new List<Vector2>(); foreach (var c in p.Cells) points.Add(new Vector2(LaneX[c.Lane], c.Row < 0 ? 201 : c.Row > 2 ? 777 : RowY[c.Row])); return points; }
        static Vector2 Along(IList<Vector2> points, float t) { float total = 0; for (int i = 1; i < points.Count; i++) total += Vector2.Distance(points[i - 1], points[i]); float d = t * total; for (int i = 1; i < points.Count; i++) { float length = Vector2.Distance(points[i - 1], points[i]); if (d <= length) return Vector2.Lerp(points[i - 1], points[i], length == 0 ? 0 : d / length); d -= length; } return points[points.Count - 1]; }
        IEnumerator Flight(Resolution report) { yield return presentation.Play(report); }

    }
}

using System.Collections;
using System.Collections.Generic;
using LadderBattle.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LadderBattle.View
{
    /// <summary>
    /// 규칙 엔진과 화면을 잇는다. 씬의 `GameDirector` 오브젝트에 붙어 있다.
    ///
    /// **규칙은 여기에 없다.** 규칙은 `Scripts/Core` 안에 있고 유니티를 쓰지 않는다.
    /// 그래서 유니티 없이도 수천 판을 돌려 숫자를 확인할 수 있다 (`_검증/csharp`).
    ///
    /// 이 파일이 하는 일은 넷이다 — 끌어다 놓은 것을 엔진에 넘기고, 봇을 돌리고,
    /// 화면을 갱신하고, **기획서 10절의 연출 순서를 지킨다.**
    ///
    /// 턴 흐름을 바꾸는 것은 엔진의 EndAction 한곳뿐이다. 여기서 차례를 직접 넘기면
    /// 10-11 에 겪은 "방어 턴이 통째로 사라지는" 버그가 되돌아온다.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        [Header("화면")]
        public BoardView 판;
        public HandView 손패;
        public HudView 머리말;
        public ScreenFlow 화면전환;
        [Tooltip("CARD LOG 의 칩 소켓과 기판 회로")] public SocketView 소켓;
        [Tooltip("턴 전환 명판")] public TransitionView 명판;

        [Header("속도")]
        [Tooltip("봇이 생각하는 척하는 시간(초). 0이면 곧바로 둔다")]
        public float 봇지연 = 0.45f;
        [Tooltip("동시 발동 결과를 보여 주고 다음 턴으로 넘어가기까지(초)")]
        public float 발동지연 = 0.8f;
        [Tooltip("연출을 다 끄고 숫자만 돌릴 때 켠다. 검사기가 쓴다")]
        public bool 연출생략;

        [Header("지금 상태 (읽기 전용)")]
        [SerializeField] int _turn;
        [SerializeField] Step _step;
        [SerializeField] Side _acting;
        [SerializeField] bool _연출중;
        [SerializeField] bool _마쳤다;

        GameEngine _e;
        Bot _bot;
        bool _busy;
        Rules.CardId? _lastCard;
        Side _lastBy;
        string _lastResult = "";

        /// <summary>봇이 낸 카드를 한 장씩 따라가기 위한 기록.</summary>
        struct 낸것 { public Rules.CardId Card; public int 번호; public int 장수전; public int 장수후; }
        readonly List<낸것> _봇이낸것 = new List<낸것>();
        bool _봇기록중;

        public GameEngine Engine => _e;
        /// <summary>연출이 돌고 있는 동안은 아무것도 받지 않는다.</summary>
        public bool 연출중 => _연출중;

        void Start()
        {
            연결();
            if (화면전환 != null) 화면전환.시작화면으로();
            else 새판();
        }

        void 연결()
        {
            if (손패 != null)
            {
                손패.카드눌림 += 카드를눌렀다;
                손패.카드집힘 += 카드를집었다;
                손패.카드놓임 += 카드를놓았다;
            }
            if (판 != null)
            {
                판.채널눌림 += 채널을눌렀다;
                판.자리눌림 += 자리를눌렀다;
            }
            if (머리말 != null)
            {
                머리말.종료눌림 += 행동을마쳤다;
                머리말.취소눌림 += () => { };
            }
            if (화면전환 != null) 화면전환.다시하기 += 새판;
        }

        // ── 판 시작 ──────────────────────────────────────────

        public void 새판()
        {
            StopAllCoroutines();
            _연출중 = false;

            _e = new GameEngine();
            _bot = new Bot(_e);
            _e.OnTurnStart += 턴이시작됐다;
            _e.OnActApplied += 카드가나갔다;
            _busy = false;
            _마쳤다 = false;
            _lastCard = null;
            _lastResult = "";
            _봇이낸것.Clear();
            _봇기록중 = false;

            손패?.끌기취소();
            손패?.고르기(-1);
            소켓?.비우기();

            _e.NewGame();
            if (화면전환 != null) 화면전환.플레이화면으로();
            갱신();
            StartCoroutine(판돌리기());
        }

        void 턴이시작됐다() { 손패?.고르기(-1); }

        void 카드가나갔다(Side side, Act act, int 번호, int 장수전, int 장수후)
        {
            if (_봇기록중 && side == Side.B)
                _봇이낸것.Add(new 낸것 { Card = act.Card, 번호 = 번호, 장수전 = 장수전, 장수후 = 장수후 });
        }

        // ── 한 판 (기획서 7절 턴 진행 · 10.2 전환 명판) ───────

        /// <summary>
        /// 공격 턴 → 방어 턴 → 동시 발동을 한 대목씩 돌린다.
        /// 각 대목 앞에 명판이 뜨고, 사람 차례에는 `행동 종료` 를 누를 때까지 멈춘다.
        /// </summary>
        IEnumerator 판돌리기()
        {
            while (_e != null && !_e.S.Ended)
            {
                Side 행동할편 = _e.S.Acting;
                yield return 명판띄우기(_e.S.Step, 행동할편);
                if (_e == null || _e.S.Ended) break;

                if (행동할편 == Side.B) yield return 봇차례();
                else yield return 사람차례();
                if (_e == null || _e.S.Ended) break;

                // 이 차례를 마치면 동시 발동인가
                bool 발동한다 = _e.S.Step == Step.Defend;
                if (발동한다)
                {
                    // 명판을 먼저 띄운다. 입력이 잠긴다는 것을 알려야 한다
                    yield return 명판띄우기(Step.Resolve, 행동할편);
                    _e.EndAction(행동할편);     // 여기서 Resolve 가 돈다
                    갱신();

                    yield return 발동연출();
                    if (_e.S.Ended) break;

                    _e.StartTurn();
                    갱신();
                }
                else
                {
                    _e.EndAction(행동할편);
                    갱신();
                }
            }

            _busy = false;
            _연출중 = false;
            갱신();
            if (_e != null && _e.S.Ended && 화면전환 != null) 화면전환.종료화면으로(_e.S);
        }

        IEnumerator 사람차례()
        {
            _마쳤다 = false;
            _busy = false;
            갱신();
            while (!_마쳤다 && _e != null && !_e.S.Ended) yield return null;
            _busy = true;
        }

        /// <summary>
        /// 봇 차례. 봇은 한 번에 여러 장을 내므로, 낸 것을 기록해 두고
        /// **한 장씩** 예고 → 꽂기 → 흡수 로 보여 준다 (기획서 10.1 의 7단계).
        /// </summary>
        IEnumerator 봇차례()
        {
            _busy = true;
            갱신();
            if (!연출생략 && 봇지연 > 0f) yield return new WaitForSeconds(봇지연);

            _봇이낸것.Clear();
            _봇기록중 = true;
            _bot.TakeTurn(Side.B);
            _봇기록중 = false;

            // 봇 손패는 아직 바꾸지 않는다. 한 장씩 빠져나가는 것을 보여 줘야 한다
            foreach (var it in _봇이낸것)
            {
                손패?.봇손패보이기(it.장수전);
                if (!연출생략 && 손패 != null) yield return 손패.봇예고(Mathf.Min(it.번호, it.장수전 - 1));
                손패?.봇손패보이기(it.장수후);
                yield return 칩내보내기(it.Card, Side.B);
            }
            갱신();
            _busy = false;
        }

        IEnumerator 발동연출()
        {
            if (연출생략 || 판 == null || 판.연출 == null)
            {
                if (발동지연 > 0f) yield return new WaitForSeconds(발동지연);
                yield break;
            }
            _연출중 = true;
            yield return 판.연출.발동보이기(_e.S.Fx, 판.서버흔들기);
            if (발동지연 > 0f) yield return new WaitForSeconds(발동지연);
            _연출중 = false;
        }

        IEnumerator 명판띄우기(Step step, Side acting)
        {
            판?.연출?.턴색(step, acting);
            if (연출생략 || 명판 == null) yield break;
            _연출중 = true;
            갱신();
            int 단계 = step == Step.Attack ? 1 : step == Step.Defend ? 2 : 3;
            yield return 명판.보이기(TransitionView.어느것(step, acting), 단계);
            _연출중 = false;
            갱신();
        }

        // ── 카드를 내는 길 (기획서 10.1) ──────────────────────

        /// <summary>집었다 — 놓을 수 있는 곳을 켜고 판 테두리를 빛낸다.</summary>
        void 카드를집었다(int 번호, Rules.CardId card)
        {
            if (!내차례) return;
            손패?.고르기(번호);
            고를자리표시(Rules.Of(card).Need);
            판?.연출?.끌기표시(true, Side.P);
        }

        /// <summary>
        /// 놓았다 — 판 안이면 배치하고, 판 밖이면 손패 제자리로 돌아간다 (취소).
        /// </summary>
        void 카드를놓았다(int 번호, Rules.CardId card, PointerEventData e)
        {
            판?.연출?.끌기표시(false, Side.P);
            if (!내차례 || 판 == null) { 고르기해제(); return; }

            var cam = 포인터카메라(e);
            var need = Rules.Of(card).Need;

            if (!판.판안인가(e.position, cam)) { 고르기해제(); return; }

            switch (need)
            {
                case Rules.Target.None:
                    내기(Act.None_(card));
                    break;
                case Rules.Target.Lane:
                {
                    int lane = 판.놓인채널(e.position, cam);
                    if (lane < 0) { 고르기해제(); return; }
                    내기(Act.Lane_(card, lane));
                    break;
                }
                case Rules.Target.Slot:
                {
                    if (!판.놓인자리(e.position, cam, out int tier, out int left)) { 고르기해제(); return; }
                    내기(Act.Slot_(card, tier, left));
                    break;
                }
            }
        }

        /// <summary>끌 수 없는 자리(검사기)에서 쓰는 누르기. 두 번 눌러 내는 옛 길이다.</summary>
        void 카드를눌렀다(int 번호, Rules.CardId card)
        {
            if (!내차례) return;
            if (손패.고른번호 == 번호) { 고르기해제(); return; }

            손패.고르기(번호);
            var def = Rules.Of(card);
            if (def.Need == Rules.Target.None) { 내기(Act.None_(card)); return; }
            고를자리표시(def.Need);
        }

        void 채널을눌렀다(int lane)
        {
            if (!내차례 || 손패.고른번호 < 0) return;
            var card = _e.S.P.Hand[손패.고른번호];
            if (Rules.Of(card).Need != Rules.Target.Lane) return;
            내기(Act.Lane_(card, lane));
        }

        void 자리를눌렀다(int tier, int left)
        {
            if (!내차례 || 손패.고른번호 < 0) return;
            var card = _e.S.P.Hand[손패.고른번호];
            if (Rules.Of(card).Need != Rules.Target.Slot) return;
            내기(Act.Slot_(card, tier, left));
        }

        void 내기(Act act)
        {
            if (!_e.ApplyAct(Side.P, act)) { 고르기해제(); return; }
            마지막카드기록(Side.P, act);
            손패?.고르기(-1);
            판?.고를자리(false, false, null, null);
            갱신();
            StartCoroutine(칩내보내기(act.Card, Side.P));
        }

        /// <summary>
        /// 낸 카드가 CARD LOG 소켓에 꽂히고, 회로를 타고 판으로 빨려 들어간다
        /// (기획서 10.1 의 4~6단계). 이 동안은 다른 입력을 받지 않는다.
        /// </summary>
        IEnumerator 칩내보내기(Rules.CardId card, Side 누가)
        {
            마지막카드기록(누가);
            if (연출생략 || 소켓 == null)
            {
                갱신();
                yield break;
            }

            _연출중 = true;
            갱신();
            yield return 소켓.꽂기(card, 누가, _lastResult);
            yield return 소켓.흡수(누가, () => 판?.연출?.테두리번쩍());
            _연출중 = false;
            갱신();
        }

        /// <summary>검사기가 만든 사건에는 카메라가 비어 있다. 캔버스에서 찾아 쓴다.</summary>
        Camera 포인터카메라(PointerEventData e)
        {
            if (e != null && e.pressEventCamera != null) return e.pressEventCamera;
            if (_canvas == null && 판 != null && 판.판영역 != null)
                _canvas = 판.판영역.GetComponentInParent<Canvas>();
            return _canvas != null ? _canvas.worldCamera : null;
        }

        Canvas _canvas;

        void 고르기해제()
        {
            손패?.고르기(-1);
            판?.연출?.끌기표시(false, Side.P);
            갱신();
        }

        void 행동을마쳤다()
        {
            if (!내차례) return;
            손패?.고르기(-1);
            _마쳤다 = true;
        }

        bool 내차례 => !_busy && !_연출중 && _e != null && !_e.S.Ended && _e.S.Acting == Side.P;

        void 마지막카드기록(Side who, Act? act = null)
        {
            _lastBy = who;
            if (act.HasValue) _lastCard = act.Value.Card;
            _lastResult = _e.S.Log.Count > 0 ? _e.S.Log[0] : "";
            if (_lastResult.Length > 30) _lastResult = _lastResult.Substring(0, 30);
        }

        // ── 화면 갱신 ───────────────────────────────────────

        public void 갱신()
        {
            if (_e == null) return;
            var S = _e.S;
            _turn = S.Turn; _step = S.Step; _acting = S.Acting;

            판?.보이기(S);
            손패?.보이기(S.P, S.B, c => _e.CanUse(Side.P, c), 내차례);
            머리말?.보이기(S, 내차례);

            if (손패 != null && 손패.고른번호 >= 0 && 손패.고른번호 < S.P.Hand.Count)
                고를자리표시(Rules.Of(S.P.Hand[손패.고른번호]).Need);
            else
                판?.고를자리(false, false, null, null);
        }

        void 고를자리표시(Rules.Target need)
        {
            if (판 == null) return;
            판.고를자리(
                need == Rules.Target.Lane,
                need == Rules.Target.Slot,
                lane => 채널가능(lane),
                (tier, left) => _e.CanRewire(_e.S.Rungs, tier, left));
        }

        bool 채널가능(int lane)
        {
            int 고른 = 손패 != null ? 손패.고른번호 : -1;
            if (고른 < 0 || 고른 >= _e.S.P.Hand.Count) return false;
            var card = _e.S.P.Hand[고른];
            if (card == Rules.CardId.Firewall) return !_e.S.Blocked.Contains(lane);
            return _e.LaneAlive(Side.P, lane);
        }
    }
}

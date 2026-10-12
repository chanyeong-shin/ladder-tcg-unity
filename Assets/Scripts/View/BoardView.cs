using System;
using System.Collections.Generic;
using LadderBattle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LadderBattle.View
{
    /// <summary>
    /// 사다리 판. 서버 여섯 개와 가로줄, 그리고 누를 자리를 맡는다.
    ///
    /// 좌표는 시안(1920x1080) 그대로다. 숫자를 바꾸면 화면이 시안과 어긋나므로
    /// 바꿀 일이 있으면 피그마와 같이 고친다.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("서버 (위 세 개가 봇, 아래 세 개가 나)")]
        public ServerView[] 봇서버 = new ServerView[3];
        public ServerView[] 내서버 = new ServerView[3];

        [Header("가로줄")]
        [Tooltip("실행 중에 만들 가로줄의 본")] public RungView 가로줄프리팹;
        [Tooltip("만든 가로줄이 담길 자리")] public RectTransform 가로줄부모;

        [Header("누르는 자리 (평소에는 보이지 않는다)")]
        public Button[] 채널버튼 = new Button[3];
        public Button[] 자리버튼 = new Button[6];   // 3단 x 왼쪽 2자리
        public Image[] 자리표시 = new Image[6];

        [Header("끌어다 놓기")]
        [Tooltip("판 영역. 이 밖에 놓으면 취소다 (기획서 10.1 의 3단계)")]
        public RectTransform 판영역;
        [Tooltip("판에서 일어나는 연출")] public BoardFxView 연출;
        [Tooltip("자리 버튼이 작아 빗맞기 쉽다. 이 거리 안이면 가장 가까운 자리로 본다")]
        public float 자리넉넉함 = 56f;

        [Header("시안 좌표")]
        [Tooltip("채널 세로줄의 가운데 x")] public float[] 채널X = { 680f, 960f, 1240f };
        [Tooltip("단계별 가로줄 y (1단이 맨 아래)")] public float[] 단계Y = { 631f, 498f, 363f };
        public float 가로줄길이 = 280f;

        readonly List<RungView> _rungs = new List<RungView>();

        /// <summary>채널을 눌렀을 때 (0~2).</summary>
        public event Action<int> 채널눌림;
        /// <summary>가로줄 자리를 눌렀을 때 (단계 1~3, 왼쪽 채널 0~1).</summary>
        public event Action<int, int> 자리눌림;

        void Awake()
        {
            for (int i = 0; i < 채널버튼.Length; i++)
            {
                int lane = i;
                if (채널버튼[i] != null) 채널버튼[i].onClick.AddListener(() => 채널눌림?.Invoke(lane));
            }
            for (int i = 0; i < 자리버튼.Length; i++)
            {
                int tier = i / 2 + 1, left = i % 2;
                if (자리버튼[i] != null) 자리버튼[i].onClick.AddListener(() => 자리눌림?.Invoke(tier, left));
            }
        }

        /// <summary>판 전체를 지금 상태에 맞춰 다시 그린다.</summary>
        public void 보이기(GameState S)
        {
            for (int l = 0; l < Rules.Lanes; l++)
            {
                내서버[l]?.보이기(S.P.Ends[l], 세기(S.P, l), 뚫림(S.P, l));
                봇서버[l]?.보이기(S.B.Ends[l], 세기(S.B, l), 뚫림(S.B, l));
            }
            가로줄그리기(S.Rungs);
        }

        static int 세기(SideState s, int lane)
        {
            foreach (var sh in s.Shells) if (sh.Lane == lane) return sh.Power;
            return 0;
        }

        static bool 뚫림(SideState s, int lane)
        {
            foreach (var sh in s.Shells) if (sh.Lane == lane) return sh.Pierce;
            return false;
        }

        void 가로줄그리기(List<Rung> rungs)
        {
            // 개수가 자주 바뀌므로 모자라면 만들고 남으면 끈다. 매번 지웠다 만들면 깜빡인다
            while (_rungs.Count < rungs.Count)
            {
                var v = Instantiate(가로줄프리팹, 가로줄부모);
                v.gameObject.name = $"가로줄 {_rungs.Count + 1}";
                _rungs.Add(v);
            }
            for (int i = 0; i < _rungs.Count; i++)
            {
                bool 쓴다 = i < rungs.Count;
                _rungs[i].gameObject.SetActive(쓴다);
                if (!쓴다) continue;
                var r = rungs[i];
                var rt = (RectTransform)_rungs[i].transform;
                float x = 채널X[r.Left];
                float y = 단계Y[Mathf.Clamp(r.Tier - 1, 0, 2)];
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(가로줄길이, 4f);
                rt.anchoredPosition = new Vector2(x, -(y - 2f));
                _rungs[i].보이기(r.Owner);
            }
        }

        /// <summary>어디를 고를 수 있는지 표시한다. 고를 게 없으면 전부 끈다.</summary>
        public void 고를자리(bool 채널, bool 자리, Func<int, bool> 채널가능, Func<int, int, bool> 자리가능)
        {
            for (int l = 0; l < 3; l++)
            {
                bool on = 채널 && (채널가능 == null || 채널가능(l));
                if (채널버튼[l] != null)
                {
                    채널버튼[l].gameObject.SetActive(on);
                    채널버튼[l].interactable = on;
                }
            }
            for (int i = 0; i < 6; i++)
            {
                int tier = i / 2 + 1, left = i % 2;
                bool on = 자리 && (자리가능 == null || 자리가능(tier, left));
                if (자리버튼[i] != null)
                {
                    자리버튼[i].gameObject.SetActive(on);
                    자리버튼[i].interactable = on;
                }
                if (자리표시[i] != null) 자리표시[i].enabled = on;
            }
        }

        // ── 놓인 자리 찾기 (기획서 10.1 의 3단계) ──────────────

        /// <summary>판 안에서 손을 떼었나. 판 밖에서 떼면 취소다.</summary>
        public bool 판안인가(Vector2 화면점, Camera cam)
            => 판영역 != null && RectTransformUtility.RectangleContainsScreenPoint(판영역, 화면점, cam);

        /// <summary>
        /// 어느 채널에 놓았나. 켜져 있는(= 놓을 수 있는) 채널만 본다.
        /// 못 찾으면 -1 이다.
        /// </summary>
        public int 놓인채널(Vector2 화면점, Camera cam)
        {
            for (int l = 0; l < 채널버튼.Length; l++)
            {
                var b = 채널버튼[l];
                if (b == null || !b.gameObject.activeSelf) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        (RectTransform)b.transform, 화면점, cam)) return l;
            }
            return -1;
        }

        /// <summary>
        /// 어느 가로줄 자리에 놓았나. 자리가 248x26 밖에 안 되어 빗맞기 쉬우므로,
        /// 바로 맞은 자리가 없으면 <see cref="자리넉넉함"/> 안의 가장 가까운 자리로 본다.
        /// </summary>
        public bool 놓인자리(Vector2 화면점, Camera cam, out int tier, out int left)
        {
            tier = left = -1;
            int 가까운 = -1;
            float 가장가까움 = float.MaxValue;

            for (int i = 0; i < 자리버튼.Length; i++)
            {
                var b = 자리버튼[i];
                if (b == null || !b.gameObject.activeSelf) continue;
                var rt = (RectTransform)b.transform;
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, 화면점, cam))
                {
                    tier = i / 2 + 1; left = i % 2;
                    return true;
                }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, 화면점, cam, out var p);
                // 사각형 밖으로 얼마나 벗어났나
                float dx = Mathf.Max(0f, Mathf.Abs(p.x) - rt.rect.width * 0.5f);
                float dy = Mathf.Max(0f, Mathf.Abs(p.y) - rt.rect.height * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 가장가까움) { 가장가까움 = d; 가까운 = i; }
            }

            if (가까운 < 0 || 가장가까움 > 자리넉넉함) return false;
            tier = 가까운 / 2 + 1; left = 가까운 % 2;
            return true;
        }

        /// <summary>맞은 서버를 흔든다 (기획서 10.3 피격).</summary>
        public void 서버흔들기(Side 맞은쪽, int lane)
        {
            if (lane < 0 || lane >= Rules.Lanes) return;
            var v = 맞은쪽 == Side.P ? 내서버[lane] : 봇서버[lane];
            if (v != null) v.맞았다();
        }
    }
}

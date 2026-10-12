using System;
using System.Text;
using LadderBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LadderBattle.View
{
    /// <summary>
    /// 판 바깥의 글과 버튼 — 윗줄, SEQUENCE, INTEL, 덱, 행동 취소 / 행동 종료.
    ///
    /// CARD LOG 쪽(칩 소켓 · 기판 회로)은 <see cref="SocketView"/> 가 맡는다.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [Header("윗줄")]
        public TextMeshProUGUI 라운드글;
        public RectTransform 진행바채움;
        public TextMeshProUGUI 코어현황;
        public float 진행바전체너비 = 1872f;

        [Header("오른쪽 위 - 진행 단계")]
        public TextMeshProUGUI 단계제목;
        public TextMeshProUGUI 단계캡션;
        public RectTransform 단계바채움;
        public float 단계바전체너비 = 284f;

        [Header("오른쪽 - INTEL")]
        public TextMeshProUGUI 인텔글;
        [Tooltip("몇 줄까지 보여 줄지")] public int 인텔줄수 = 9;

        [Header("버튼")]
        public Button 행동취소;
        public Button 행동종료;
        public TextMeshProUGUI 행동종료글;

        public event Action 취소눌림;
        public event Action 종료눌림;

        void Awake()
        {
            if (행동취소 != null) 행동취소.onClick.AddListener(() => 취소눌림?.Invoke());
            if (행동종료 != null) 행동종료.onClick.AddListener(() => 종료눌림?.Invoke());
        }

        public void 보이기(GameState S, bool 내차례)
        {
            bool 공격 = S.Step == Step.Attack;
            string 누구 = S.First == Side.P ? "SELF" : "ENEMY";

            // 턴 제한이 없으면(MaxTurn 0) 분모를 쓸 수 없다. 턴 수만 적는다
            bool 제한있다 = Rules.MaxTurn > 0;

            if (라운드글 != null)
                라운드글.text = 제한있다
                    ? $"TURN {S.Turn:00} / {Rules.MaxTurn}      ATTACK : {누구}      DECK  ∞"
                    : $"TURN {S.Turn:00}      ATTACK : {누구}      DECK  ∞";

            if (진행바채움 != null)
            {
                // 제한이 없으면 남은 턴을 잴 수 없다. 대신 부순 상대 서버 수(0~3)를 채운다
                float t = 제한있다
                    ? Mathf.Clamp01(S.Turn / (float)Rules.MaxTurn)
                    : Mathf.Clamp01((Rules.Servers - S.B.AliveServers) / (float)Rules.Servers);
                진행바채움.sizeDelta = new Vector2(진행바전체너비 * t, 진행바채움.sizeDelta.y);
            }

            if (코어현황 != null)
                코어현황.text = $"서버 {S.P.AliveServers} : {S.B.AliveServers}";

            // 한 턴은 공격 턴 → 방어 턴 → 동시 발동, 모두 세 대목이다
            int 단계 = S.Step == Step.Attack ? 1 : S.Step == Step.Defend ? 2 : 3;
            if (단계제목 != null) 단계제목.text = $"SEQUENCE   {단계} / 3";
            if (단계캡션 != null)
                단계캡션.text = 내차례
                    ? (공격 ? "[ ATTACK ] 공격 턴 · 먼저 놓는다" : "[ DEFEND ] 방어 턴 · 대응 권한 활성")
                    : "[ WAIT ] 상대 차례 · 대기";
            if (단계바채움 != null)
                단계바채움.sizeDelta = new Vector2(단계바전체너비 * 단계 / 3f, 단계바채움.sizeDelta.y);

            if (인텔글 != null) 인텔글.text = 인텔(S);

            if (행동종료 != null) 행동종료.interactable = 내차례 && !S.Ended;
            if (행동취소 != null) 행동취소.interactable = false;   // 되돌리기는 아직 없다
            if (행동종료글 != null) 행동종료글.text = S.Ended ? "판 종료" : "행동 종료";
        }

        string 인텔(GameState S)
        {
            var sb = new StringBuilder();
            int n = Mathf.Min(인텔줄수, S.Log.Count);
            for (int i = 0; i < n; i++)
            {
                sb.Append(">> ");
                sb.Append(S.Log[i]);
                if (i < n - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

    }
}

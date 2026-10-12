using System;
using LadderBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LadderBattle.View
{
    /// <summary>
    /// 시작 화면 → 플레이 → 종료 화면.
    ///
    /// 과제의 완성 조건 1번이 "시작부터 종료까지 끊김 없이"다. 플레이 화면부터
    /// 바로 뜨면 그 조건을 못 채운다. 세 화면을 켜고 끄는 일만 여기서 맡는다.
    /// </summary>
    public class ScreenFlow : MonoBehaviour
    {
        [Header("화면 셋")]
        public GameObject 시작화면;
        public GameObject 플레이화면;
        public GameObject 종료화면;

        [Header("시작 화면")]
        public Button 시작버튼;

        [Header("종료 화면")]
        public TextMeshProUGUI 결과제목;
        public TextMeshProUGUI 결과설명;
        public Button 다시버튼;

        /// <summary>시작 또는 다시 버튼을 눌렀을 때.</summary>
        public event Action 다시하기;

        void Awake()
        {
            if (시작버튼 != null) 시작버튼.onClick.AddListener(() => 다시하기?.Invoke());
            if (다시버튼 != null) 다시버튼.onClick.AddListener(() => 다시하기?.Invoke());
        }

        public void 시작화면으로() => 켜기(true, false, false);
        public void 플레이화면으로() => 켜기(false, true, false);

        public void 종료화면으로(GameState S)
        {
            켜기(false, true, true);   // 판은 남겨 둔다. 어떻게 끝났는지 보여야 한다

            string 제목 = S.Result switch
            {
                Result.PlayerWin => "BREACH SUCCESS",
                Result.BotWin => "SYSTEM COMPROMISED",
                _ => "STALEMATE",
            };
            string 한글 = S.Result switch
            {
                Result.PlayerWin => "내 승리",
                Result.BotWin => "봇 승리",
                _ => "무승부",
            };
            if (결과제목 != null)
            {
                결과제목.text = 제목;
                결과제목.color = S.Result == Result.PlayerWin ? Palette.나밝음
                               : S.Result == Result.BotWin ? Palette.적밝음 : Palette.강화선;
            }
            if (결과설명 != null)
                결과설명.text =
                    $"{한글}  ·  {S.Turn}턴에 끝남\n" +
                    $"남은 서버   나 {S.P.AliveServers} : {S.B.AliveServers} 봇\n" +
                    $"남은 보안도 나 {S.P.SecurityLeft} : {S.B.SecurityLeft} 봇";
        }

        void 켜기(bool 시작, bool 플레이, bool 종료)
        {
            if (시작화면 != null) 시작화면.SetActive(시작);
            if (플레이화면 != null) 플레이화면.SetActive(플레이);
            if (종료화면 != null) 종료화면.SetActive(종료);
        }
    }
}

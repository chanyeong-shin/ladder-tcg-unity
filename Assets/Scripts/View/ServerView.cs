using System.Collections;
using LadderBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LadderBattle.View
{
    /// <summary>
    /// 서버(코어) 하나. 판 위아래에 셋씩 모두 여섯 개가 씬에 그대로 놓여 있다.
    ///
    /// 왜 프리팹이 아닌가 — 개수가 늘지 않기 때문이다. 실행 중에 생겼다 사라지는
    /// 것(카드 · 가로줄)만 프리팹으로 두고, 처음부터 끝까지 여섯 개인 것은 씬에 둔다.
    /// 자리를 옮기고 싶으면 Scene 창에서 끌면 된다.
    /// </summary>
    public class ServerView : MonoBehaviour
    {
        [Header("어느 서버인가")]
        public Side 편;
        [Range(0, 2)] public int 채널;

        [Header("서버 부분")]
        [Tooltip("육각형 속칠")] public Image 육각형;
        [Tooltip("육각형 테두리")] public Image 테두리;
        [Tooltip("살아 있을 때 켜지는 번짐")] public Image 번짐;
        [Tooltip("보안도 숫자")] public TextMeshProUGUI 보안도;
        [Tooltip("보안도 숫자 뒤의 번짐. 숫자와 함께 꺼져야 한다")] public Image 보안도번짐;
        [Tooltip("부서졌을 때 뜨는 글")] public TextMeshProUGUI 오프라인;
        [Tooltip("이 채널로 날아가는 패킷의 세기")] public TextMeshProUGUI 패킷;

        public void 보이기(int 보안도값, int 패킷세기, bool 뚫림)
        {
            bool 살았다 = 보안도값 > 0;
            Color 색 = Palette.편(편);

            if (육각형 != null) 육각형.color = 살았다 ? (편 == Side.P ? Palette.나칠 : Palette.적칠) : Palette.꺼짐칠;
            if (테두리 != null) 테두리.color = 살았다 ? 색 : Palette.꺼짐선;
            // 공격받고 있는 서버만 빛난다. 전부 빛나면 어디가 급한지 안 보인다
            if (번짐 != null) 번짐.enabled = 살았다 && 패킷세기 > 0;

            // 숫자 뒤의 번짐을 같이 끄지 않으면 부서진 서버가 빨갛게 빛난 채로 남는다
            if (보안도번짐 != null) 보안도번짐.enabled = 살았다;
            if (보안도 != null)
            {
                보안도.gameObject.SetActive(살았다);
                보안도.text = 보안도값.ToString();
                보안도.color = 색;
            }
            if (오프라인 != null)
            {
                오프라인.gameObject.SetActive(!살았다);
                오프라인.color = Palette.꺼짐선;
            }
            if (패킷 != null)
            {
                패킷.gameObject.SetActive(패킷세기 > 0);
                패킷.text = $"PWR {패킷세기:00}  ·  {(뚫림 ? "상쇄 없음" : "전송 대기")}";
                패킷.color = 색;
            }
        }

        /// <summary>맞았을 때 흔들린다 (기획서 10.3 피격).</summary>
        public void 맞았다() => StartCoroutine(흔들기(6f, 0.2f));

        IEnumerator 흔들기(float 세기, float 시간)
        {
            var rt = (RectTransform)transform;
            Vector2 처음 = rt.anchoredPosition;
            float t = 0f;
            while (t < 시간)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / 시간);
                rt.anchoredPosition = 처음 + new Vector2(
                    Random.Range(-세기, 세기) * k, Random.Range(-세기, 세기) * k);
                yield return null;
            }
            rt.anchoredPosition = 처음;
        }
    }
}

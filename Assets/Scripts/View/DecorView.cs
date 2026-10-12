using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

namespace LadderBattle.View
{
    /// <summary>
    /// 화면 구석의 장식이 살아 있게 한다 (기획서 10.3 장식).
    ///
    /// 이진수는 90ms 마다 뒤집히고, 바코드는 흔들리고, 장식 문구는 타자 치듯 써졌다 지워진다.
    /// **판을 바꾸지 않는다** — 읽을 정보가 아니라 화면이 돌아가고 있다는 신호다.
    /// 그래서 글자 수와 자리는 그대로 두고 내용만 바꾼다. 자리가 흔들리면 눈이 그쪽으로 끌린다.
    /// </summary>
    public class DecorView : MonoBehaviour
    {
        [Header("이진수 (90ms 마다 한 글자씩 뒤집힌다)")]
        public TextMeshProUGUI[] 이진수 = new TextMeshProUGUI[0];
        [Tooltip("한 번에 뒤집을 글자 수")] public int 한번에 = 3;
        [Tooltip("뒤집는 간격(초). 기획서 10.3 은 90ms 다")] public float 뒤집는간격 = 0.09f;

        [Header("바코드 (가로로 미세하게 흔들린다)")]
        public TextMeshProUGUI[] 바코드 = new TextMeshProUGUI[0];
        [Tooltip("흔들리는 폭(px)")] public float 흔들폭 = 1.5f;
        [Tooltip("흔들리는 간격(초)")] public float 흔들간격 = 0.16f;

        [Header("타자 (써졌다 지워진다)")]
        public TextMeshProUGUI 타자글;
        [Tooltip("한 글자 치는 데 걸리는 시간(초)")] public float 한글자 = 0.055f;
        [Tooltip("다 치고 머무는 시간(초)")] public float 머무는시간 = 1.6f;
        [TextArea]
        [Tooltip("돌아가며 치는 줄들. 줄 하나가 한 문장이다")]
        public string[] 타자문구 =
        {
            "MATCH 4471 // SHARED DECK   MIRROR MATCH",
            "TRACE ROUTE // 3 CHANNEL / 3 TIER",
            "PACKET LOSS 000 // LINK STABLE",
            "HANDSHAKE OK // SESSION LOCKED",
        };

        Vector2[] _바코드처음;
        string[] _이진수처음;

        void Start()
        {
            _이진수처음 = new string[이진수.Length];
            for (int i = 0; i < 이진수.Length; i++)
                if (이진수[i] != null) _이진수처음[i] = 이진수[i].text;

            _바코드처음 = new Vector2[바코드.Length];
            for (int i = 0; i < 바코드.Length; i++)
                if (바코드[i] != null) _바코드처음[i] = ((RectTransform)바코드[i].transform).anchoredPosition;

            StartCoroutine(이진수뒤집기());
            StartCoroutine(바코드흔들기());
            StartCoroutine(타자치기());
        }

        IEnumerator 이진수뒤집기()
        {
            var sb = new StringBuilder();
            while (true)
            {
                for (int i = 0; i < 이진수.Length; i++)
                {
                    if (이진수[i] == null || string.IsNullOrEmpty(_이진수처음[i])) continue;
                    sb.Clear();
                    sb.Append(이진수[i].text);
                    for (int n = 0; n < 한번에; n++)
                    {
                        int at = Random.Range(0, sb.Length);
                        // 0과 1만 뒤집는다. 사이의 빈칸과 줄바꿈을 건드리면 자리가 흔들린다
                        if (sb[at] == '0') sb[at] = '1';
                        else if (sb[at] == '1') sb[at] = '0';
                    }
                    이진수[i].text = sb.ToString();
                }
                yield return new WaitForSeconds(뒤집는간격);
            }
        }

        IEnumerator 바코드흔들기()
        {
            while (true)
            {
                for (int i = 0; i < 바코드.Length; i++)
                {
                    if (바코드[i] == null) continue;
                    var rt = (RectTransform)바코드[i].transform;
                    rt.anchoredPosition = _바코드처음[i]
                        + new Vector2(Random.Range(-흔들폭, 흔들폭), 0f);
                }
                yield return new WaitForSeconds(흔들간격);
            }
        }

        IEnumerator 타자치기()
        {
            if (타자글 == null || 타자문구.Length == 0) yield break;
            int 몇번째 = 0;
            while (true)
            {
                string 줄 = 타자문구[몇번째 % 타자문구.Length];
                몇번째++;

                for (int i = 1; i <= 줄.Length; i++)
                {
                    타자글.text = 줄.Substring(0, i);
                    yield return new WaitForSeconds(한글자);
                }
                yield return new WaitForSeconds(머무는시간);
                for (int i = 줄.Length; i >= 0; i--)
                {
                    타자글.text = 줄.Substring(0, i);
                    yield return new WaitForSeconds(한글자 * 0.4f);
                }
                yield return new WaitForSeconds(0.3f);
            }
        }
    }
}

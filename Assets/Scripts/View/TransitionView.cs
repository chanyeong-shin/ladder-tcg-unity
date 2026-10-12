using System.Collections;
using LadderBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LadderBattle.View
{
    /// <summary>
    /// 턴 전환 명판 (기획서 10.2). 피그마 전환 화면 3장을 그대로 쓴다.
    ///
    /// 단계가 바뀔 때 판 가운데에 명판이 뜬다. 하스스톤의 「나의 턴」 띠와 같은 자리이고
    /// 표현만 해킹 쪽으로 바꿨다.
    ///
    /// **동시 발동만 화면 전체를 덮는다** — 그 순간 양측 입력이 다 잠기기 때문이다.
    /// 앞의 둘은 판(800x716)만 덮는다.
    /// </summary>
    public class TransitionView : MonoBehaviour
    {
        /// <summary>어느 전환인가. 기획서 10.2 의 세 줄과 1:1이다.</summary>
        public enum 전환 { 나의턴, 상대턴, 동시발동 }

        [Header("묶음")]
        [Tooltip("전환 오버레이 전체. 평소에는 꺼져 있다")] public GameObject 오버레이;
        [Tooltip("가림막. 덮는 범위가 전환마다 다르다")] public Image 가림막;

        [Header("코드 레인 (가림막 위로 0과 1이 떨어진다)")]
        [Tooltip("줄기 13개. 사다리가 비쳐야 하므로 투명도가 낮다")]
        public TextMeshProUGUI[] 줄기 = new TextMeshProUGUI[0];
        [Tooltip("줄기 끝의 밝은 머리 5개. 높이가 제각각이라 떨어지는 중으로 읽힌다")]
        public TextMeshProUGUI[] 머리 = new TextMeshProUGUI[0];
        [Tooltip("머리가 1초에 내려가는 거리(px)")] public float 떨어지는속도 = 420f;

        [Header("명판")]
        public RectTransform 명판;
        [Tooltip("명판 테두리")] public Image 명판테두리;
        [Tooltip("명판 글로우")] public Image 명판번짐;
        [Tooltip("큰 한글 44px")] public TextMeshProUGUI 큰글자;
        [Tooltip("영문 캡션 12px")] public TextMeshProUGUI 캡션;

        [Header("시안 좌표")]
        [Tooltip("판만 덮을 때 — 사다리 보드 영역과 같다")]
        public Rect 판범위 = new Rect(560f, 128f, 800f, 716f);
        [Tooltip("화면 전체를 덮을 때")]
        public Rect 전체범위 = new Rect(0f, 0f, 1920f, 1080f);
        [Tooltip("판만 덮을 때 명판 y")] public float 명판판Y = 426f;
        [Tooltip("전체를 덮을 때 명판 y")] public float 명판전체Y = 480f;

        [Header("시간")]
        [Tooltip("명판이 떠 있는 시간(초). 기획서 10.2 는 0.8초다")]
        public float 머무는시간 = 0.8f;
        [Tooltip("뜨고 사라지는 데 걸리는 시간(초)")] public float 여닫기 = 0.12f;

        CanvasGroup _cg;
        float[] _머리처음Y;
        /// <summary>명판과 그 번짐의 자리 차이. 명판을 옮길 때 번짐도 같이 옮겨야 한다.</summary>
        Vector2 _번짐차이;

        void Awake()
        {
            if (오버레이 != null)
            {
                // ?? 로 받으면 안 된다 (유니티의 빈 컴포넌트는 C# null 이 아니다)
                if (!오버레이.TryGetComponent<CanvasGroup>(out _cg))
                    _cg = 오버레이.AddComponent<CanvasGroup>();
                오버레이.SetActive(false);
            }
            // 번짐은 명판의 형제다(자식으로 두면 칠 위에 덮인다). 명판만 옮기면
            // 번짐이 옛 자리에 남아 엉뚱한 데가 빛난다 — 동시 발동에서 명판이 54px 내려간다
            if (명판 != null && 명판번짐 != null)
                _번짐차이 = ((RectTransform)명판번짐.transform).anchoredPosition - 명판.anchoredPosition;

            if (머리 != null)
            {
                _머리처음Y = new float[머리.Length];
                for (int i = 0; i < 머리.Length; i++)
                    if (머리[i] != null) _머리처음Y[i] = ((RectTransform)머리[i].transform).anchoredPosition.y;
            }
        }

        /// <summary>단계 하나에 맞는 전환을 고른다.</summary>
        public static 전환 어느것(Step step, Side acting)
            => step == Step.Resolve ? 전환.동시발동
             : acting == Side.P ? 전환.나의턴 : 전환.상대턴;

        /// <summary>명판을 띄우고 기다린다. 끝나면 꺼진다.</summary>
        public IEnumerator 보이기(전환 what, int 단계)
        {
            if (오버레이 == null) yield break;

            차리기(what, 단계);
            오버레이.SetActive(true);
            오버레이.transform.SetAsLastSibling();

            yield return 흐리기(0f, 1f, 여닫기);
            yield return 떨어뜨리기(Mathf.Max(0f, 머무는시간 - 여닫기 * 2f));
            yield return 흐리기(1f, 0f, 여닫기);

            오버레이.SetActive(false);
        }

        void 차리기(전환 what, int 단계)
        {
            Color 색 = what == 전환.나의턴 ? Palette.나밝음
                     : what == 전환.상대턴 ? Palette.적밝음 : Palette.강화선;
            Color 캡션색 = what == 전환.나의턴 ? Palette.나중간
                         : what == 전환.상대턴 ? Palette.적어두움 : Palette.Hex("b8952a");

            // 동시 발동만 화면 전체를 덮는다. 그 순간 양측 입력이 다 잠긴다
            bool 전체 = what == 전환.동시발동;
            if (가림막 != null)
            {
                var r = 전체 ? 전체범위 : 판범위;
                FigmaPlace((RectTransform)가림막.transform, r.x, r.y, r.width, r.height);
            }
            if (명판 != null)
            {
                var p = 명판.anchoredPosition;
                명판.anchoredPosition = new Vector2(p.x, -(전체 ? 명판전체Y : 명판판Y));
                if (명판번짐 != null)
                    ((RectTransform)명판번짐.transform).anchoredPosition =
                        명판.anchoredPosition + _번짐차이;
            }

            if (명판테두리 != null) 명판테두리.color = 색;
            if (명판번짐 != null) 명판번짐.color = new Color(색.r, 색.g, 색.b, 0.6f);
            if (큰글자 != null)
            {
                큰글자.color = 색;
                큰글자.text = what == 전환.나의턴 ? "나 의   턴"
                            : what == 전환.상대턴 ? "상 대   턴" : "동 시   발 동";
            }
            if (캡션 != null)
            {
                캡션.color = 캡션색;
                캡션.text = what == 전환.나의턴
                    ? $"[ SECOND ]   SEQUENCE {단계} / 3   ·   대응 권한 활성"
                    : what == 전환.상대턴
                    ? $"[ ENEMY ]   SEQUENCE {단계} / 3   ·   배치 수신 중"
                    : $"[ RESOLVE ]   SEQUENCE {단계} / 3   ·   입력 잠김";
            }

            // 코드 레인도 그 단계의 색이다
            foreach (var t in 줄기)
            {
                if (t == null) continue;
                float a = t.color.a;              // 줄기마다 투명도가 다르다. 그 값은 지킨다
                t.color = new Color(색.r, 색.g, 색.b, a);
            }
            foreach (var t in 머리) if (t != null) t.color = 색;
            머리제자리로();
        }

        /// <summary>머리가 아래로 흐른다. 머리 높이가 제각각이라 "지금 떨어지는 중"으로 읽힌다.</summary>
        IEnumerator 떨어뜨리기(float 시간)
        {
            // 가림막 아래쪽. 거기까지 내려가면 다시 위에서 시작한다
            float 바닥 = -1080f;
            if (가림막 != null)
            {
                var grt = (RectTransform)가림막.transform;
                바닥 = grt.anchoredPosition.y - grt.sizeDelta.y;
            }
            float t = 0f;
            while (t < 시간)
            {
                t += Time.deltaTime;
                for (int i = 0; i < 머리.Length; i++)
                {
                    if (머리[i] == null) continue;
                    var rt = (RectTransform)머리[i].transform;
                    var p = rt.anchoredPosition;
                    p.y -= 떨어지는속도 * Time.deltaTime;
                    if (p.y < 바닥) p.y = _머리처음Y[i];
                    rt.anchoredPosition = p;
                }
                yield return null;
            }
        }

        void 머리제자리로()
        {
            if (_머리처음Y == null) return;
            for (int i = 0; i < 머리.Length && i < _머리처음Y.Length; i++)
            {
                if (머리[i] == null) continue;
                var rt = (RectTransform)머리[i].transform;
                var p = rt.anchoredPosition;
                rt.anchoredPosition = new Vector2(p.x, _머리처음Y[i]);
            }
        }

        IEnumerator 흐리기(float 처음, float 끝, float 시간)
        {
            if (_cg == null) yield break;
            if (시간 <= 0f) { _cg.alpha = 끝; yield break; }
            float t = 0f;
            while (t < 시간)
            {
                t += Time.deltaTime;
                _cg.alpha = Mathf.Lerp(처음, 끝, Mathf.Clamp01(t / 시간));
                yield return null;
            }
            _cg.alpha = 끝;
        }

        /// <summary>시안 좌표를 그대로 넣는 자리잡기. 왼쪽 위가 0,0 이다.</summary>
        static void FigmaPlace(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}

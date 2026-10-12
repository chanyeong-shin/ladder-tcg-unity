using System.Collections;
using System.Collections.Generic;
using LadderBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LadderBattle.View
{
    /// <summary>
    /// CARD LOG 의 CPU 소켓과 기판 회로 (기획서 10.1).
    ///
    /// 카드 = 칩, CARD LOG = 소켓이다. 회로가 왜 거기서 출발하는지가 이 비유로 설명된다 —
    /// 칩을 꽂아야 회로에 전기가 흐른다.
    ///
    /// **색과 발광이 서로 다른 것을 말한다.**
    ///   색   = 누가 마지막으로 꽂았나. 칩이 판으로 들어간 뒤에도 한 단계 어두워져 남는다
    ///   발광 = 지금 꽂혀 있나. 비면 꺼진다. 전기가 흐르는 동안만 빛난다
    ///
    /// 칩 그림은 소켓이 비어도 흐려진 채 남는다. 소켓은 비지만 CARD LOG 는 *기록*이라,
    /// 직전에 무엇이 들어갔는지 읽을 수 있어야 한다.
    /// </summary>
    public class SocketView : MonoBehaviour
    {
        [Header("소켓 (색이 바뀌는 범위)")]
        [Tooltip("리테이너 틀의 테두리")] public Image 리테이너;
        [Tooltip("리테이너 글로우. 꽂혀 있을 때만 켠다")] public Image 리테이너번짐;
        [Tooltip("안쪽 턱 — 금속 브래킷의 이중 벽")] public Image 안쪽턱;
        [Tooltip("핀밭 왼쪽 · 오른쪽")] public TextMeshProUGUI[] 핀그리드 = new TextMeshProUGUI[2];
        [Tooltip("1번 핀 삼각형 — 방향 표식")] public Image 삼각형;
        [Tooltip("정렬 노치 둘. 칩이 한 방향으로만 들어간다는 표시")] public Image[] 노치 = new Image[2];
        [Tooltip("캠 레버와 고리 — 잠금 상태")] public Image 레버;
        public Image 레버고리;
        [Tooltip("마운트 홀 셋")] public Image[] 마운트홀 = new Image[3];
        [Tooltip("소켓 라벨 글")] public TextMeshProUGUI 소켓라벨;

        [Header("꽂힌 칩")]
        public RectTransform 칩;
        public Image 칩바탕;
        public Image 칩테두리;
        public TextMeshProUGUI 칩글;
        public TextMeshProUGUI 칩결과;

        [Header("기판 회로 (CARD LOG -> 판 테두리)")]
        [Tooltip("회로 묶음 전체. 꺼지면 다 사라진다")] public GameObject 회로;
        [Tooltip("메인 트레이스 — 칩이 실제로 타고 가는 길")] public Graphic[] 메인 = new Graphic[0];
        [Tooltip("보조 가닥 — 전류가 흐르는 연출은 없다")] public Graphic[] 보조 = new Graphic[0];
        [Tooltip("비아 · 패드 · 핀 같은 맺음")] public Graphic[] 맺음 = new Graphic[0];
        [Tooltip("회로를 타고 판으로 빨려 들어가는 칩")] public RectTransform 날아가는칩;
        public Image 날아가는칩그림;
        [Tooltip("날아가는 칩이 지나는 꺾임점. 회로 묶음 기준 좌표다")]
        public Vector2[] 길 = new Vector2[0];

        [Header("시간 (기획서 10.1)")]
        [Tooltip("딸깍 — 되튐 없이 빠르게. 이게 길어지면 꽂히는 느낌이 사라진다")]
        public float 딸깍 = 0.09f;
        [Tooltip("회로가 그어지는 데 걸리는 시간")] public float 회로그리기 = 0.26f;
        [Tooltip("칩이 회로를 타고 판까지 가는 시간")] public float 날기 = 0.40f;
        [Tooltip("지나간 뒤 회로가 어두워졌다 사라지는 시간")] public float 회로끄기 = 0.22f;
        [Tooltip("꽂히는 순간 번쩍이는 시간")] public float 번쩍 = 0.12f;

        [Header("지금 상태 (읽기 전용)")]
        [SerializeField] bool _꽂힘;
        [SerializeField] Side _직전편;

        void Awake() => 비우기();

        /// <summary>한 판에 한 번, 아무도 안 꽂았을 때. 잔색을 쓰면 "누가 냈다"는 거짓말이 된다.</summary>
        public void 비우기()
        {
            _꽂힘 = false;
            회로보이기(false);
            칠하기(null, false);
            if (소켓라벨 != null) 소켓라벨.text = "SOCKET 01  ·  EMPTY  ·  대기";
            if (칩결과 != null) { 칩결과.text = "NO DATA"; 칩결과.color = Palette.중성밝음; }

            // 아직 아무도 안 꽂았다. 자리만 흐리게 보이고 글은 비어 있다는 표시다
            칩보이기(true);
            if (칩글 != null) { 칩글.text = "[ --- ]"; 칩글.color = Palette.중성밝음; }
            if (칩바탕 != null) 칩바탕.color = Palette.Hex("0e1317");
            칩흐리기(0.4f);
        }

        /// <summary>
        /// 칩이 소켓에 내려앉는다 (기획서 10.1 의 4단계 · 칩과 소켓).
        /// 1.06배에서 1.0배로 되튐 없이 탁 줄어드는 것이 "딸깍"이다.
        /// </summary>
        public IEnumerator 꽂기(Rules.CardId card, Side 누가, string 결과)
        {
            _꽂힘 = true;
            _직전편 = 누가;

            칩글쓰기(card, 누가, 결과);
            칩보이기(true);
            칠하기(누가, true);
            if (소켓라벨 != null)
                소켓라벨.text = $"SOCKET 01  ·  CHIP SEATED  ·  {(누가 == Side.P ? "아군" : "적군")}";

            StartCoroutine(소켓번쩍());

            // 내려앉는다 — 되튐 없이 빠르게
            if (칩 != null)
            {
                float t = 0f;
                while (t < 딸깍)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / 딸깍);
                    칩.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, k);
                    yield return null;
                }
                칩.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// 꽂힌 칩이 회로를 타고 판 테두리로 빨려 들어간다 (기획서 10.1 의 5~6단계).
        /// 방향은 CARD LOG → 판이다 — 낸 카드가 기록에 먼저 남고, 그 기록이 판으로 들어가 효력을 낸다.
        /// </summary>
        public IEnumerator 흡수(Side 누가, System.Action 판테두리번쩍)
        {
            Palette.소켓색(누가, true, out Color 밝음, out Color 어두움);

            // 5. 회로가 그어진다 — 가닥이 CARD LOG 쪽에서 판 쪽으로 자라난다
            회로보이기(true);
            회로색(밝음, 어두움);
            yield return 그어지기();

            // 6. 칩이 그 회로를 타고 간다
            if (날아가는칩 != null && 길.Length >= 2)
            {
                if (날아가는칩그림 != null) 날아가는칩그림.color = 밝음;
                날아가는칩.gameObject.SetActive(true);
                yield return 길따라가기();
                날아가는칩.gameObject.SetActive(false);
            }

            // 닿는 순간 테두리가 번쩍인다
            판테두리번쩍?.Invoke();

            // 지나간 뒤 회로는 어두워졌다 사라진다
            yield return 어두워지기(밝음, 어두움);
            회로보이기(false);

            // 소켓은 비지만 색은 남는다
            _꽂힘 = false;
            칠하기(누가, false);
            if (소켓라벨 != null)
                소켓라벨.text = $"SOCKET 01  ·  EMPTY  ·  직전 {(누가 == Side.P ? "아군" : "적군")}";
            if (칩결과 != null) 칩결과.text = "CHIP ABSORBED  ·  " + (누가 == Side.P ? "아군" : "적군");
            // 칩 그림은 흐려진 채 남는다 — CARD LOG 는 기록이다
            칩흐리기();
        }

        // ── 색 ────────────────────────────────────────────────

        void 칠하기(Side? 누가, bool 꽂힘)
        {
            Palette.소켓색(누가, 꽂힘, out Color 밝음, out Color 어두움);

            if (리테이너 != null) 리테이너.color = 밝음;
            if (안쪽턱 != null) 안쪽턱.color = 어두움;
            if (삼각형 != null) 삼각형.color = 밝음;
            if (레버 != null) 레버.color = 어두움;
            if (레버고리 != null) 레버고리.color = 밝음;
            foreach (var p in 핀그리드) if (p != null) p.color = new Color(밝음.r, 밝음.g, 밝음.b, 0.62f);
            foreach (var n in 노치) if (n != null) n.color = 밝음;
            foreach (var m in 마운트홀) if (m != null) m.color = 어두움;
            if (소켓라벨 != null) 소켓라벨.color = 밝음;
            if (칩테두리 != null) 칩테두리.color = 밝음;

            // 발광은 "지금 꽂혀 있나"만 말한다. 빈 슬롯은 전기가 안 흐르므로 빛나지 않는다
            if (리테이너번짐 != null)
            {
                리테이너번짐.enabled = 꽂힘;
                리테이너번짐.color = new Color(밝음.r, 밝음.g, 밝음.b, 0.6f);
            }
        }

        void 회로색(Color 밝음, Color 어두움)
        {
            foreach (var g in 메인) if (g != null) g.color = 밝음;
            foreach (var g in 보조) if (g != null) g.color = 어두움;
            // 비아는 가운데가 뚫린 링이라 테두리만 소유 색이다. 칠은 배경색 그대로 둔다
            foreach (var g in 맺음) if (g != null) g.color = 밝음;
        }

        IEnumerator 그어지기()
        {
            // 가닥이 자라나는 것으로 보이려면 차례로 켜져야 한다.
            // 한꺼번에 켜면 "이미 있던 그림"으로 읽혀 방향이 사라진다
            var 차례 = 그릴차례();
            foreach (var g in 차례) if (g != null) g.enabled = false;

            float 칸 = 차례.Count > 0 ? 회로그리기 / 차례.Count : 0f;
            foreach (var g in 차례)
            {
                if (g != null) g.enabled = true;
                if (칸 > 0f) yield return new WaitForSeconds(칸);
            }
        }

        IEnumerator 어두워지기(Color 밝음, Color 어두움)
        {
            float t = 0f;
            while (t < 회로끄기)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / 회로끄기);
                foreach (var g in 메인) if (g != null) g.color = Color.Lerp(어두움 * 0.35f, 밝음, k);
                yield return null;
            }
        }

        IEnumerator 길따라가기()
        {
            float 총길이 = 0f;
            for (int i = 1; i < 길.Length; i++) 총길이 += Vector2.Distance(길[i - 1], 길[i]);
            if (총길이 <= 0.01f) yield break;

            float t = 0f;
            while (t < 날기)
            {
                t += Time.deltaTime;
                놓기(Mathf.Clamp01(t / 날기), 총길이);
                yield return null;
            }
            놓기(1f, 총길이);
        }

        void 놓기(float k, float 총길이)
        {
            float 남은 = k * 총길이;
            for (int i = 1; i < 길.Length; i++)
            {
                float d = Vector2.Distance(길[i - 1], 길[i]);
                if (남은 <= d || i == 길.Length - 1)
                {
                    Vector2 p = Vector2.Lerp(길[i - 1], 길[i], d > 0.01f ? Mathf.Clamp01(남은 / d) : 1f);
                    날아가는칩.anchoredPosition = new Vector2(p.x, -p.y);
                    return;
                }
                남은 -= d;
            }
        }

        List<Graphic> 그릴차례()
        {
            var list = new List<Graphic>();
            // 칩 쪽에서 판 쪽으로. 메인과 보조를 번갈아 켜서 다발로 자라게 한다
            int n = Mathf.Max(메인.Length, 보조.Length);
            for (int i = 0; i < n; i++)
            {
                if (i < 메인.Length) list.Add(메인[i]);
                if (i < 보조.Length) list.Add(보조[i]);
            }
            foreach (var g in 맺음) list.Add(g);
            return list;
        }

        IEnumerator 소켓번쩍()
        {
            // 핀 그리드와 리테이너가 한 번 번쩍인다
            var 원래 = new List<Color>();
            var 것들 = new List<Graphic>();
            foreach (var p in 핀그리드) if (p != null) { 것들.Add(p); 원래.Add(p.color); }
            if (리테이너 != null) { 것들.Add(리테이너); 원래.Add(리테이너.color); }

            foreach (var g in 것들) g.color = Color.white;
            yield return new WaitForSeconds(번쩍);
            for (int i = 0; i < 것들.Count; i++) 것들[i].color = 원래[i];
        }

        // ── 조각 켜고 끄기 ────────────────────────────────────

        void 칩보이기(bool on)
        {
            if (칩 != null) 칩.gameObject.SetActive(on);
            if (on)
            {
                if (칩바탕 != null) 칩바탕.color = new Color(칩바탕.color.r, 칩바탕.color.g, 칩바탕.color.b, 1f);
                if (칩 != null && 칩.TryGetComponent<CanvasGroup>(out var cg)) cg.alpha = 1f;
            }
        }

        /// <summary>소켓은 비었지만 기록은 남는다 — 투명도를 내려 흐리게 둔다.</summary>
        void 칩흐리기(float alpha = 0.5f)
        {
            if (칩 == null) return;
            // ?? 로 받으면 안 된다 — 유니티의 빈 컴포넌트는 C# null 이 아니라서
            // 널 병합 연산자가 그냥 통과시키고, 쓰는 순간 MissingComponentException 이 난다
            if (!칩.TryGetComponent<CanvasGroup>(out var cg))
                cg = 칩.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = alpha;
        }

        void 회로보이기(bool on)
        {
            if (회로 == null) return;
            회로.SetActive(on);
            if (!on && 날아가는칩 != null) 날아가는칩.gameObject.SetActive(false);
            if (on)
            {
                foreach (var g in 메인) if (g != null) g.enabled = true;
                foreach (var g in 보조) if (g != null) g.enabled = true;
                foreach (var g in 맺음) if (g != null) g.enabled = true;
            }
        }

        void 칩글쓰기(Rules.CardId card, Side 누가, string 결과)
        {
            var def = Rules.Of(card);
            Color c = Palette.편(누가);
            if (칩글 != null)
            {
                string head = def.Label + "\n" + def.En;
                if (!string.IsNullOrEmpty(def.Upgrade)) head += "\n" + def.Upgrade;
                칩글.text = head + "\n\n" + def.Desc + "\n\n"
                            + (def.Grade == Rules.Grade.Security ? "강화" : "일반");
                칩글.color = c;
            }
            if (칩결과 != null)
            {
                칩결과.text = 결과;
                칩결과.color = Palette.편(누가, false);
            }
        }
    }
}

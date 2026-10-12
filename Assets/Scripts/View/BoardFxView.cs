using System.Collections;
using System.Collections.Generic;
using LadderBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LadderBattle.View
{
    /// <summary>
    /// 판에서 일어나는 연출 (기획서 10.3) 과 판 색 (기획서 10.1).
    ///
    /// **판 색은 지금 턴을 따른다.** 소켓 색은 "누가 꽂았나"이고 판 색은 "지금 누구 턴인가"다.
    /// 서로 다른 질문이라 두 색이 달라도 맞다 — 적이 꽂은 빨간 소켓이 그대로 있는 채로
    /// 시안색 내 턴 판이 시작되는 화면이 정상이다.
    ///
    /// 색이 바뀌지 않는 것 — 서버 육각형, 보안도 숫자, PWR 글자, 설치된 가로줄,
    /// MY/ENEMY SERVER 라벨. 이것들은 **소유**를 나타내므로 턴과 무관하다.
    /// </summary>
    public class BoardFxView : MonoBehaviour
    {
        [Header("턴에 따라 색이 바뀌는 것")]
        [Tooltip("판 바탕")] public Image 판바탕;
        [Tooltip("판 테두리")] public Image 판테두리;
        [Tooltip("판 테두리 글로우. 끌고 있는 동안만 켠다")] public Image 판테두리번짐;
        [Tooltip("모서리 브래킷 8개")] public Image[] 브래킷 = new Image[0];
        [Tooltip("보드 라벨과 상태 글")] public TextMeshProUGUI[] 보드글 = new TextMeshProUGUI[0];
        [Tooltip("채널 세로줄 3개")] public Image[] 세로줄 = new Image[0];
        [Tooltip("단계 눈금")] public Image[] 눈금 = new Image[0];

        [Header("연출을 올려 둘 자리")]
        [Tooltip("빛줄기 · 섬광 · 피해 숫자가 만들어지는 자리")] public RectTransform 연출층;
        [Tooltip("흔들 대상. 판 묶음을 넣는다")] public RectTransform 흔들대상;

        [Header("연출 그림 (에디터에서 넣어 준다)")]
        public Sprite 네모;
        public Sprite 동그라미;
        public TMP_FontAsset 글꼴;

        [Header("판 좌표 (시안 1920x1080)")]
        [Tooltip("채널 가운데 x")] public float[] 채널X = { 680f, 960f, 1240f };
        [Tooltip("경로 칸의 y. 0이 내 서버, 4가 상대 서버다")]
        public float[] 칸Y = { 776f, 631f, 498f, 363f, 200f };

        [Header("시간")]
        public float 빛줄기시간 = 0.42f;
        public float 섬광시간 = 0.24f;
        public float 흔들시간 = 0.22f;
        public float 숫자시간 = 0.70f;
        public float 번쩍시간 = 0.14f;

        Vector2 _흔들기전;

        void Awake()
        {
            if (흔들대상 != null) _흔들기전 = 흔들대상.anchoredPosition;
            if (판테두리번짐 != null) 판테두리번짐.enabled = false;
        }

        // ── 판 색 ─────────────────────────────────────────────

        public void 턴색(Step step, Side acting)
        {
            var c = step == Step.Resolve ? Palette.발동턴
                  : acting == Side.P ? Palette.내턴 : Palette.상대턴;

            if (판바탕 != null) 판바탕.color = c.바탕;
            if (판테두리 != null) 판테두리.color = c.테두리;
            foreach (var b in 브래킷) if (b != null) b.color = c.브래킷;
            foreach (var t in 보드글) if (t != null) t.color = c.라벨;
            foreach (var v in 세로줄) if (v != null) v.color = c.세로줄;
            foreach (var n in 눈금) if (n != null) n.color = c.눈금;
        }

        // ── 끌고 있는 동안 (기획서 10.1 의 2단계) ─────────────

        /// <summary>집고 있는 동안 판 테두리 전체가 소유 색으로 빛난다.</summary>
        public void 끌기표시(bool on, Side 편)
        {
            if (판테두리번짐 == null) return;
            판테두리번짐.enabled = on;
            Color c = Palette.편(편);
            판테두리번짐.color = new Color(c.r, c.g, c.b, 0.55f);
        }

        /// <summary>칩이 닿는 순간 테두리가 번쩍인다 (기획서 10.1 의 6단계).</summary>
        public void 테두리번쩍() => StartCoroutine(번쩍());

        IEnumerator 번쩍()
        {
            if (판테두리 == null) yield break;
            Color 원래 = 판테두리.color;
            판테두리.color = Color.white;
            if (판테두리번짐 != null) { 판테두리번짐.enabled = true; 판테두리번짐.color = new Color(1f, 1f, 1f, 0.7f); }
            yield return new WaitForSeconds(번쩍시간);
            판테두리.color = 원래;
            if (판테두리번짐 != null) 판테두리번짐.enabled = false;
        }

        // ── 동시 발동 연출 (기획서 10.3) ──────────────────────

        /// <summary>
        /// 발동 기록을 차례로 그린다. 규칙은 이미 끝난 상태이고,
        /// 여기서는 <see cref="Fx"/> 목록만 보고 그린다.
        /// </summary>
        public IEnumerator 발동보이기(IReadOnlyList<Fx> 기록, System.Action<Side, int> 서버흔들기)
        {
            if (기록 == null || 기록.Count == 0) yield break;

            // 1. 발사 — 경로를 따라 빛줄기가 달린다. 양쪽이 같이 날아가야 상쇄가 읽힌다
            var 달리기 = new List<Coroutine>();
            foreach (var f in 기록)
                if (f.Kind == FxKind.발사) 달리기.Add(StartCoroutine(빛줄기(f)));
            if (달리기.Count > 0) yield return new WaitForSeconds(빛줄기시간);

            // 2. 상쇄 · 관통 · 차단 — 경로 중간 섬광
            bool 섬광있다 = false;
            foreach (var f in 기록)
            {
                if (f.Kind != FxKind.상쇄 && f.Kind != FxKind.관통 && f.Kind != FxKind.차단) continue;
                StartCoroutine(섬광(f));
                섬광있다 = true;
            }
            if (섬광있다) yield return new WaitForSeconds(섬광시간);

            // 3. 피격 · 파괴 · 헛방
            foreach (var f in 기록)
            {
                switch (f.Kind)
                {
                    case FxKind.착탄:
                    case FxKind.번짐:
                        서버흔들기?.Invoke(GameState.Other(f.By), f.Lane);
                        StartCoroutine(피해숫자(f, false));
                        break;
                    case FxKind.파괴:
                        서버흔들기?.Invoke(GameState.Other(f.By), f.Lane);
                        StartCoroutine(폭발(f));
                        StartCoroutine(화면흔들기());
                        break;
                    case FxKind.헛방:
                        StartCoroutine(헛방(f));
                        break;
                }
            }
            yield return new WaitForSeconds(숫자시간 * 0.6f);
        }

        /// <summary>경로를 따라 빛줄기가 달린다.</summary>
        IEnumerator 빛줄기(Fx f)
        {
            if (f.Cells.Count < 2 || 연출층 == null) yield break;

            Color c = Palette.편(f.By);
            var go = 조각("빛줄기", 14f, 14f, c, 동그라미);
            var rt = (RectTransform)go.transform;

            float 칸시간 = 빛줄기시간 / (f.Cells.Count - 1);
            for (int i = 1; i < f.Cells.Count; i++)
            {
                Vector2 a = 칸자리(f.Cells[i - 1]), b = 칸자리(f.Cells[i]);
                float t = 0f;
                while (t < 칸시간)
                {
                    t += Time.deltaTime;
                    Vector2 p = Vector2.Lerp(a, b, Mathf.Clamp01(t / 칸시간));
                    rt.anchoredPosition = new Vector2(p.x - 7f, -(p.y - 7f));
                    yield return null;
                }
                // 지나간 칸에 꼬리를 남긴다. 어느 길로 갔는지 눈으로 따라갈 수 있어야 한다
                StartCoroutine(꼬리(b, c));
            }
            Destroy(go);
        }

        IEnumerator 꼬리(Vector2 p, Color c)
        {
            var go = 조각("지나간 칸", 10f, 10f, new Color(c.r, c.g, c.b, 0.5f), 동그라미);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = new Vector2(p.x - 5f, -(p.y - 5f));
            var img = go.GetComponent<Image>();
            float t = 0f;
            while (t < 섬광시간)
            {
                t += Time.deltaTime;
                var k = 1f - Mathf.Clamp01(t / 섬광시간);
                img.color = new Color(c.r, c.g, c.b, 0.5f * k);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>경로 중간 섬광. 상쇄가 어디서 일어났는지 그 자리에 뜬다.</summary>
        IEnumerator 섬광(Fx f)
        {
            if (연출층 == null || f.Y < 0) yield break;
            Vector2 p = 칸자리((f.Y, f.Lane));
            Color c = f.Kind == FxKind.차단 ? Palette.강화선 : Color.white;

            var go = 조각(f.Kind.ToString(), 10f, 10f, c, 동그라미);
            var rt = (RectTransform)go.transform;
            var img = go.GetComponent<Image>();
            float t = 0f;
            while (t < 섬광시간)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 섬광시간);
                float d = Mathf.Lerp(10f, 72f, k);
                rt.sizeDelta = new Vector2(d, d);
                rt.anchoredPosition = new Vector2(p.x - d * 0.5f, -(p.y - d * 0.5f));
                img.color = new Color(c.r, c.g, c.b, 1f - k);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>피해 숫자. 맞은 서버 위로 떠오르며 사라진다.</summary>
        IEnumerator 피해숫자(Fx f, bool 파괴)
        {
            if (연출층 == null) yield break;
            Side 맞은쪽 = GameState.Other(f.By);
            Vector2 p = 서버자리(맞은쪽, f.Lane);
            Color c = Palette.편(f.By);

            var go = new GameObject($"피해 {f.Power}", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(연출층, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(120f, 40f);

            var t = go.AddComponent<TextMeshProUGUI>();
            if (글꼴 != null) t.font = 글꼴;
            t.fontSize = 파괴 ? 34f : 28f;
            t.color = c;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.text = 파괴 ? "DOWN" : $"-{f.Power}";

            float e = 0f;
            while (e < 숫자시간)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / 숫자시간);
                rt.anchoredPosition = new Vector2(p.x - 60f, -(p.y - 28f - 34f * k));
                t.color = new Color(c.r, c.g, c.b, 1f - k * k);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>서버 폭발. 번쩍인 뒤 테두리가 퍼져 나가며 사라진다.</summary>
        IEnumerator 폭발(Fx f)
        {
            StartCoroutine(피해숫자(f, true));
            if (연출층 == null) yield break;

            Side 맞은쪽 = GameState.Other(f.By);
            Vector2 p = 서버자리(맞은쪽, f.Lane);
            Color c = Palette.편(f.By);

            var go = 조각("파괴 섬광", 56f, 56f, Color.white, 동그라미);
            var rt = (RectTransform)go.transform;
            var img = go.GetComponent<Image>();
            float t = 0f, 시간 = 섬광시간 * 1.6f;
            while (t < 시간)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 시간);
                float d = Mathf.Lerp(56f, 190f, k);
                rt.sizeDelta = new Vector2(d, d);
                rt.anchoredPosition = new Vector2(p.x - d * 0.5f, -(p.y - d * 0.5f));
                img.color = Color.Lerp(Color.white, new Color(c.r, c.g, c.b, 0f), k);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>헛방 — 이미 부서진 서버를 때렸다. 회색 글자로만 알린다.</summary>
        IEnumerator 헛방(Fx f)
        {
            if (연출층 == null) yield break;
            Side 맞은쪽 = GameState.Other(f.By);
            Vector2 p = 서버자리(맞은쪽, f.Lane);

            var go = new GameObject("헛방", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(연출층, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(140f, 30f);
            rt.anchoredPosition = new Vector2(p.x - 70f, -(p.y - 24f));

            var t = go.AddComponent<TextMeshProUGUI>();
            if (글꼴 != null) t.font = 글꼴;
            t.fontSize = 16f;
            t.color = Palette.꺼짐선;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.text = "헛방 · NO TARGET";

            yield return new WaitForSeconds(숫자시간);
            Destroy(go);
        }

        /// <summary>화면 흔들림. 서버가 떨어질 때만 쓴다.</summary>
        public IEnumerator 화면흔들기(float 세기 = 7f)
        {
            if (흔들대상 == null) yield break;
            float t = 0f;
            while (t < 흔들시간)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / 흔들시간);
                흔들대상.anchoredPosition = _흔들기전 + new Vector2(
                    Random.Range(-세기, 세기) * k, Random.Range(-세기, 세기) * k);
                yield return null;
            }
            흔들대상.anchoredPosition = _흔들기전;
        }

        // ── 자리 ──────────────────────────────────────────────

        Vector2 칸자리((int y, int lane) c)
        {
            int l = Mathf.Clamp(c.lane, 0, 채널X.Length - 1);
            int y = Mathf.Clamp(c.y, 0, 칸Y.Length - 1);
            return new Vector2(채널X[l], 칸Y[y]);
        }

        Vector2 서버자리(Side side, int lane)
        {
            int l = Mathf.Clamp(lane, 0, 채널X.Length - 1);
            return new Vector2(채널X[l], side == Side.P ? 칸Y[0] : 칸Y[칸Y.Length - 1]);
        }

        GameObject 조각(string name, float w, float h, Color c, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(연출층, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            var img = go.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : 네모;
            img.color = c;
            img.raycastTarget = false;
            return go;
        }
    }
}

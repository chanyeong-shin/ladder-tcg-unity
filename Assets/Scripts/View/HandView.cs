using System;
using System.Collections;
using System.Collections.Generic;
using LadderBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LadderBattle.View
{
    /// <summary>
    /// 손패. 내 카드는 프리팹으로 만들어 늘어놓고, 봇 손패는 뒷면 수만 맞춘다.
    ///
    /// 봇 손패는 뒷면이지만 **낼 한 장만 먼저 들린다** (기획서 10.3 적 배치 예고).
    /// 무엇인지는 끝까지 가리고 어느 장인지만 알린다 — 그래야 봇이 언제 움직였는지
    /// 놓치지 않는다.
    /// </summary>
    public class HandView : MonoBehaviour
    {
        [Header("내 손패")]
        public CardView 카드프리팹;
        public RectTransform 손패부모;
        public TextMeshProUGUI 손패라벨;
        [Tooltip("끌고 있는 카드를 올려 둘 자리. 손패 밑에 있으면 다른 것에 가린다")]
        public RectTransform 끌기층;

        [Header("봇 손패")]
        public RectTransform 봇손패부모;
        public GameObject 봇카드프리팹;
        public TextMeshProUGUI 봇손패라벨;

        [Header("시안 좌표")]
        public float 첫카드X = 647f;
        public float 카드간격 = 160f;
        public float 카드Y = 876f;
        public float 고른카드Y = 864f;
        public Vector2 카드크기 = new Vector2(147f, 170f);
        public Vector2 고른카드크기 = new Vector2(147f, 182f);
        public float 봇첫카드X = 151f;
        public float 봇카드간격 = 58f;

        [Header("적 배치 예고 (기획서 10.3)")]
        [Tooltip("낼 한 장만 이 배율로 커진다")] public float 예고배율 = 1.08f;
        [Tooltip("그만큼 위로 올라온다(px)")] public float 예고들림 = 6f;
        [Tooltip("예고를 띄우고 카드를 내기까지(초)")] public float 예고시간 = 0.55f;
        [Tooltip("예고에 쓰는 테두리 번짐 그림. 에디터 쪽에서 넣어 준다")] public Sprite 번짐그림;

        readonly List<CardView> _cards = new List<CardView>();
        readonly List<GameObject> _backs = new List<GameObject>();
        readonly List<Image> _backGlows = new List<Image>();

        /// <summary>검사기처럼 끌 수 없는 자리에서 쓰는 누르기.</summary>
        public event Action<int, Rules.CardId> 카드눌림;
        /// <summary>카드를 집었을 때. 놓을 곳을 켜려면 이것을 받는다.</summary>
        public event Action<int, Rules.CardId> 카드집힘;
        /// <summary>카드를 놓았을 때. 어디에 놓였는지는 받는 쪽이 판단한다.</summary>
        public event Action<int, Rules.CardId, PointerEventData> 카드놓임;

        int _selected = -1;
        public int 고른번호 => _selected;
        /// <summary>지금 깔려 있는 카드들. 검사기가 사람처럼 눌러 보려고 쓴다.</summary>
        public IReadOnlyList<CardView> 카드들 => _cards;

        public void 보이기(SideState 나, SideState 봇, Func<Rules.CardId, bool> 쓸수있나, bool 내차례)
        {
            while (_cards.Count < 나.Hand.Count)
            {
                var v = Instantiate(카드프리팹, 손패부모);
                int at = _cards.Count;
                v.끌기층 = 끌기층;
                v.눌림 += _ => 카드눌림?.Invoke(at, v.Card);
                v.집힘 += _ => 카드집힘?.Invoke(at, v.Card);
                v.놓임 += (_, e) => 카드놓임?.Invoke(at, v.Card, e);
                _cards.Add(v);
            }
            for (int i = 0; i < _cards.Count; i++)
            {
                bool 쓴다 = i < 나.Hand.Count;
                _cards[i].gameObject.SetActive(쓴다);
                if (!쓴다) continue;
                // 끌고 있는 카드는 자리를 다시 잡지 않는다. 손에서 빠져나간다
                if (_cards[i].끌고있다) continue;

                var card = 나.Hand[i];
                bool 가능 = 내차례 && (쓸수있나 == null || 쓸수있나(card));
                _cards[i].gameObject.name = $"카드 {i + 1} - {Rules.Of(card).Label}";
                _cards[i].보이기(card, 가능);
                _cards[i].고르기(i == _selected);
                자리잡기((RectTransform)_cards[i].transform, i, i == _selected);
            }

            봇손패보이기(봇.Hand.Count);

            if (손패라벨 != null)
            {
                int 강화 = 0;
                foreach (var c in 나.Hand) if (Rules.IsSecurity(c)) 강화++;
                손패라벨.text = $"HAND  {나.Hand.Count:00} / {Rules.HandMax:00}      " +
                                $"BASE {나.Hand.Count - 강화:00} · SECURITY {강화:00}";
            }
            if (봇손패라벨 != null) 봇손패라벨.text = $"ENEMY HAND   {봇.Hand.Count:00}";
        }

        void 자리잡기(RectTransform rt, int i, bool 고름)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = 고름 ? 고른카드크기 : 카드크기;
            // 고른 카드는 살짝 올라온다. 시안에서도 선택된 카드만 위로 나와 있다
            rt.anchoredPosition = new Vector2(i * 카드간격, -(고름 ? 고른카드Y - 카드Y : 0f));
        }

        public void 고르기(int 번호)
        {
            _selected = 번호;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (!_cards[i].gameObject.activeSelf || _cards[i].끌고있다) continue;
                _cards[i].고르기(i == _selected);
                자리잡기((RectTransform)_cards[i].transform, i, i == _selected);
            }
        }

        /// <summary>끌고 있던 카드를 전부 손으로 되돌린다. 판이 바뀔 때 쓴다.</summary>
        public void 끌기취소()
        {
            foreach (var c in _cards) if (c.끌고있다) c.제자리로();
        }

        // ── 적 배치 예고 (기획서 10.3) ────────────────────────

        /// <summary>
        /// 봇이 낼 한 장을 들어 보인다. **무엇인지는 안 보이고 어느 장인지만** 보인다.
        /// 내용까지 보이면 후공이 지나치게 유리해진다.
        /// </summary>
        public IEnumerator 봇예고(int 번호)
        {
            if (번호 < 0 || 번호 >= _backs.Count || !_backs[번호].activeSelf) yield break;
            예고중 = true;

            var rt = (RectTransform)_backs[번호].transform;
            Vector2 자리 = rt.anchoredPosition;
            rt.localScale = Vector3.one * 예고배율;
            rt.anchoredPosition = 자리 + new Vector2(0f, 예고들림);
            if (_backGlows[번호] != null) _backGlows[번호].enabled = true;

            yield return new WaitForSeconds(예고시간);

            rt.localScale = Vector3.one;
            rt.anchoredPosition = 자리;
            예고끄기(번호);
            예고중 = false;
        }

        /// <summary>지금 예고가 떠 있나. 검사기가 그 순간을 사진으로 남기는 데 쓴다.</summary>
        public bool 예고중 { get; private set; }

        /// <summary>
        /// 봇 뒷면을 몇 장 보일지 직접 정한다.
        /// 봇은 한 차례에 여러 장을 내므로, 한 장씩 보여 주는 동안은
        /// 지금 상태가 아니라 **그 장을 내던 때의 장수**를 세워야 한다.
        /// </summary>
        public void 봇손패보이기(int 장수)
        {
            while (_backs.Count < 장수)
            {
                var g = Instantiate(봇카드프리팹, 봇손패부모);
                var rt = (RectTransform)g.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(_backs.Count * 봇카드간격, 0f);
                g.name = $"봇 손패 {_backs.Count + 1} (뒷면)";
                _backs.Add(g);
                _backGlows.Add(예고번짐붙이기(g));
            }
            for (int i = 0; i < _backs.Count; i++)
            {
                bool 쓴다 = i < 장수;
                _backs[i].SetActive(쓴다);
                if (!쓴다) 예고끄기(i);
            }
            if (봇손패라벨 != null) 봇손패라벨.text = $"ENEMY HAND   {Mathf.Max(0, 장수):00}";
        }

        void 예고끄기(int i)
        {
            if (i >= 0 && i < _backGlows.Count && _backGlows[i] != null) _backGlows[i].enabled = false;
        }

        /// <summary>예고에 쓰는 약한 빨강 번짐. 평소에는 꺼져 있다.</summary>
        Image 예고번짐붙이기(GameObject back)
        {
            const int r = 12;
            var rt = (RectTransform)back.transform;
            var size = rt.sizeDelta;
            var go = new GameObject("예고 번짐", typeof(RectTransform));
            var grt = (RectTransform)go.transform;
            grt.SetParent(back.transform, false);
            grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0f, 1f);
            grt.anchoredPosition = new Vector2(-r, r);
            grt.sizeDelta = new Vector2(size.x + r * 2f, size.y + r * 2f);
            go.transform.SetAsFirstSibling();

            var img = go.AddComponent<Image>();
            img.sprite = 번짐그림;
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            img.color = new Color(Palette.적밝음.r, Palette.적밝음.g, Palette.적밝음.b, 0.45f);
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }
    }
}

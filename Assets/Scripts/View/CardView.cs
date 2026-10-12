using System;
using LadderBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LadderBattle.View
{
    /// <summary>
    /// 손패 카드 한 장. `Assets/Prefabs/카드.prefab` 에 붙어 있다.
    ///
    /// **카드는 눌러서 내지 않고 끌어서 낸다** (기획서 10.1). 누르기 방식은
    /// "눌렀다"와 "골랐다"가 같은 동작이라 되돌리는 규칙을 따로 둬야 했다.
    /// 끌기는 판 밖에 놓는 것이 곧 취소라서 규칙이 하나 줄어든다.
    ///
    /// Inspector 에서 보이는 칸들이 카드의 각 부분이다. 색이나 글자를 바꾸고 싶으면
    /// 프리팹을 열어 바꾸면 손패의 모든 카드에 한꺼번에 적용된다.
    /// </summary>
    public class CardView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("카드 부분")]
        [Tooltip("카드에 적히는 글 전체. 이름 · 영문 · 강화 관계 · 설명 · 등급이 한 덩어리다")]
        public TextMeshProUGUI 글;
        [Tooltip("카드 바탕")] public Image 바탕;
        [Tooltip("카드 테두리")] public Image 테두리;
        [Tooltip("집었을 때 켜지는 네온 번짐")] public Image 번짐;
        [Tooltip("집었을 때 아래로 깔리는 그림자")] public Image 그림자;
        [Tooltip("누르는 자리. 끌기도 이 그림이 받는다")] public Button 버튼;

        [Header("집었을 때 (기획서 10.1)")]
        [Tooltip("집히는 순간 커지는 배율. 누르기만 해서는 커지지 않는다")]
        public float 집힌배율 = 1.15f;
        [Tooltip("집히면 떠오르는 높이(px). 그림자가 이만큼 아래로 깔린다")]
        public float 떠오름 = 10f;

        [Header("지금 상태 (읽기 전용)")]
        [SerializeField] Rules.CardId _card;
        [SerializeField] bool _selected;
        [SerializeField] bool _usable = true;
        [SerializeField] bool _dragging;

        public Rules.CardId Card => _card;
        public bool Selected => _selected;
        public bool 끌고있다 => _dragging;

        /// <summary>끌기가 안 되는 자리(검사기 등)에서 쓰는 누르기.</summary>
        public event Action<CardView> 눌림;
        /// <summary>집었을 때. 놓을 곳을 켜려면 이것을 받는다.</summary>
        public event Action<CardView> 집힘;
        /// <summary>놓았을 때. 어디에 놓였는지는 받는 쪽이 눌린 자리로 판단한다.</summary>
        public event Action<CardView, PointerEventData> 놓임;

        /// <summary>끌고 있는 동안 카드를 올려 둘 자리. 손패 밑에 있으면 다른 것에 가린다.</summary>
        [HideInInspector] public RectTransform 끌기층;

        RectTransform _rt;
        Transform _집기전부모;
        int _집기전번호;
        Vector2 _집기전자리;
        Vector2 _잡은점;

        void Awake()
        {
            _rt = (RectTransform)transform;
            if (버튼 != null) 버튼.onClick.AddListener(() => { if (!_dragging) 눌림?.Invoke(this); });
            if (그림자 != null) 그림자.enabled = false;
        }

        /// <summary>카드 한 장을 이 자리에 그린다.</summary>
        public void 보이기(Rules.CardId card, bool 쓸수있음)
        {
            _card = card;
            _usable = 쓸수있음;
            var def = Rules.Of(card);

            if (글 != null)
            {
                // 시안과 같은 차례 — 이름, 영문, (강화면) 무엇의 강화인지, 빈 줄, 설명, 빈 줄, 등급
                string head = def.Label + "\n" + def.En;
                if (!string.IsNullOrEmpty(def.Upgrade)) head += "\n" + def.Upgrade;
                string grade = def.Grade == Rules.Grade.Security ? "강화" : "일반";
                글.text = head + "\n\n" + def.Desc + "\n\n" + grade;
            }
            칠하기();
        }

        public void 고르기(bool 고름)
        {
            _selected = 고름;
            칠하기();
        }

        void 칠하기()
        {
            bool 강화 = Rules.IsSecurity(_card);
            Color 선 = 강화 ? Palette.강화선 : Palette.나중간;
            Color 칠 = 강화 ? Palette.강화칠 : Palette.일반카드칠;
            Color 글색 = 강화 ? Palette.강화선 : Palette.나글;

            // 못 쓰는 카드는 끌어도 소용없다는 것이 보여야 한다
            float 진하기 = _usable ? 1f : 0.35f;

            if (바탕 != null) 바탕.color = 칠;
            if (테두리 != null) 테두리.color = Dim(_selected || _dragging ? Palette.나밝음 : 선, 진하기);
            if (글 != null) 글.color = Dim(글색, 진하기);
            // 집었다는 신호다. 고르기만 해서는 켜지지 않는다
            if (번짐 != null) 번짐.enabled = _dragging;
            if (버튼 != null) 버튼.interactable = _usable;
        }

        // ── 끌기 (기획서 10.1 의 1~3단계) ─────────────────────

        public void OnBeginDrag(PointerEventData e)
        {
            if (!_usable || 끌기층 == null) return;

            _dragging = true;
            _집기전부모 = _rt.parent;
            _집기전번호 = _rt.GetSiblingIndex();
            _집기전자리 = _rt.anchoredPosition;

            // 1. 집히는 순간 커지고 떠오른다
            _rt.SetParent(끌기층, true);
            _rt.SetAsLastSibling();
            _rt.localScale = Vector3.one * 집힌배율;
            if (그림자 != null) 그림자.enabled = true;

            // 끌고 있는 동안은 카드가 포인터를 먹지 않아야 한다.
            // 그래야 놓을 때 아래에 있는 채널·자리를 찾을 수 있다
            레이캐스트(false);
            칠하기();

            _잡은점 = 포인터(e) - _rt.anchoredPosition;
            집힘?.Invoke(this);
            따라가기(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_dragging) 따라가기(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;

            // 놓인 자리를 보는 쪽이 아직 "커진 카드"를 봐야 하므로 판단을 먼저 넘긴다
            놓임?.Invoke(this, e);

            제자리로();
        }

        /// <summary>집기 전 자리·크기로 되돌린다. 판 밖에 놓았을 때가 곧 취소다.</summary>
        public void 제자리로()
        {
            _dragging = false;
            if (_집기전부모 != null)
            {
                _rt.SetParent(_집기전부모, false);
                _rt.SetSiblingIndex(_집기전번호);
                _rt.anchoredPosition = _집기전자리;
            }
            _rt.localScale = Vector3.one;
            if (그림자 != null) 그림자.enabled = false;
            레이캐스트(true);
            칠하기();
        }

        void 따라가기(PointerEventData e)
        {
            Vector2 p = 포인터(e) - _잡은점;
            _rt.anchoredPosition = p;
            if (그림자 != null)
            {
                var srt = (RectTransform)그림자.transform;
                srt.anchoredPosition = new Vector2(0f, -떠오름);
            }
        }

        Vector2 포인터(PointerEventData e)
        {
            var host = (RectTransform)끌기층;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                host, e.position, 카메라(e), out var local);
            // 끌기층은 왼쪽 위가 기준점이다. 시안 좌표와 같은 셈을 쓴다
            return new Vector2(local.x + host.rect.width * host.pivot.x,
                               local.y - host.rect.height * (1f - host.pivot.y));
        }

        /// <summary>
        /// 포인터 좌표를 읽을 카메라. 검사기가 만든 사건에는 `pressEventCamera` 가 비어 있다
        /// (읽기 전용이라 넣을 수도 없다). 그럴 때는 캔버스의 카메라를 쓴다.
        /// </summary>
        Camera 카메라(PointerEventData e)
        {
            if (e != null && e.pressEventCamera != null) return e.pressEventCamera;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null && 끌기층 != null) _canvas = 끌기층.GetComponentInParent<Canvas>();
            return _canvas != null ? _canvas.worldCamera : null;
        }

        Canvas _canvas;

        void 레이캐스트(bool on)
        {
            if (바탕 != null) 바탕.raycastTarget = on;
            var img = 버튼 != null ? 버튼.targetGraphic : null;
            if (img != null) img.raycastTarget = on;
        }

        static Color Dim(Color c, float k)
        {
            c.r *= k; c.g *= k; c.b *= k;
            return c;
        }
    }
}

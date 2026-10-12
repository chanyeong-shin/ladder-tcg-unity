using UnityEngine;

namespace LadderBattle.View
{
    /// <summary>
    /// 시안에서 쓰는 색. 숫자는 `피그마/작업중.fig` 의 값을 그대로 옮긴 것이다.
    ///
    /// 화면을 만들 때 색을 여기저기 적어 두면 나중에 한 곳만 고쳐서 어긋난다.
    /// 색을 바꿀 일이 있으면 **여기만** 고친다.
    /// </summary>
    public static class Palette
    {
        public static Color Hex(string rrggbb, float a = 1f)
        {
            ColorUtility.TryParseHtmlString("#" + rrggbb, out var c);
            c.a = a;
            return c;
        }

        // 바탕
        public static readonly Color 배경 = Hex("0a0c10");
        public static readonly Color 판배경 = Hex("07131a");

        // 나 — 시안 계열
        public static readonly Color 나밝음 = Hex("38d9ff");
        public static readonly Color 나중간 = Hex("3899b8");
        public static readonly Color 나어두움 = Hex("1d4a5b");
        public static readonly Color 나칠 = Hex("081a21");
        public static readonly Color 나글 = Hex("b8d9e5");

        // 봇 — 빨강 계열
        public static readonly Color 적밝음 = Hex("ff3b3b");
        public static readonly Color 적중간 = Hex("a32430");
        public static readonly Color 적어두움 = Hex("8c2933");
        public static readonly Color 적칠 = Hex("240a0f");
        public static readonly Color 적글 = Hex("d9999e");

        // 강화 등급 — 금색
        public static readonly Color 강화선 = Hex("ffd23f");
        public static readonly Color 강화칠 = Hex("1b1605");

        // 꺼진 것
        public static readonly Color 꺼짐선 = Hex("474247");
        public static readonly Color 꺼짐칠 = Hex("121214");

        public static readonly Color 일반카드칠 = Hex("0e1317");
        public static readonly Color 어두운글 = Hex("0d080a");

        public static Color 편(Core.Side side, bool 밝게 = true)
            => side == Core.Side.P ? (밝게 ? 나밝음 : 나중간) : (밝게 ? 적밝음 : 적중간);

        // ── 칩 소켓 (기획서 10.1) ──────────────────────────────
        // 색은 **누가 마지막으로 꽂았나**를, 발광은 **지금 꽂혀 있나**를 말한다.
        // 그래서 칩이 판으로 들어간 뒤에도 색은 한 단계 어두워진 채로 남는다

        public static readonly Color 소켓나밝음 = Hex("38d9ff");
        public static readonly Color 소켓나어두움 = Hex("1d6b85");
        public static readonly Color 소켓적밝음 = Hex("ff3b3b");
        public static readonly Color 소켓적어두움 = Hex("8c2933");
        public static readonly Color 잔색나밝음 = Hex("1d6b85");
        public static readonly Color 잔색나어두움 = Hex("133f4d");
        public static readonly Color 잔색적밝음 = Hex("8c2933");
        public static readonly Color 잔색적어두움 = Hex("5c1b22");
        public static readonly Color 중성밝음 = Hex("2a3a44");
        public static readonly Color 중성어두움 = Hex("1f2a31");

        /// <summary>소켓 색 한 벌. <paramref name="꽂힘"/> 이 false 면 잔색이다.</summary>
        public static void 소켓색(Core.Side? 누가, bool 꽂힘, out Color 밝음, out Color 어두움)
        {
            if (!누가.HasValue) { 밝음 = 중성밝음; 어두움 = 중성어두움; return; }
            bool 나 = 누가.Value == Core.Side.P;
            if (꽂힘) { 밝음 = 나 ? 소켓나밝음 : 소켓적밝음; 어두움 = 나 ? 소켓나어두움 : 소켓적어두움; }
            else { 밝음 = 나 ? 잔색나밝음 : 잔색적밝음; 어두움 = 나 ? 잔색나어두움 : 잔색적어두움; }
        }

        // ── 판 색은 지금 턴을 따른다 (기획서 10.1) ─────────────
        // 소켓 색과 다른 질문에 답한다 — 소켓은 "누가 꽂았나", 판은 "지금 누구 턴인가"

        public struct 판색
        {
            public Color 바탕, 테두리, 브래킷, 라벨, 세로줄, 눈금;
        }

        public static readonly 판색 내턴 = new 판색
        {
            바탕 = Hex("07131a"), 테두리 = Hex("1f6f8b"), 브래킷 = Hex("38d9ff"),
            라벨 = Hex("3899b8"), 세로줄 = Hex("1d4a5b"), 눈금 = Hex("22586b"),
        };
        public static readonly 판색 상대턴 = new 판색
        {
            바탕 = Hex("1a0a0c"), 테두리 = Hex("8b2b35"), 브래킷 = Hex("ff3b3b"),
            라벨 = Hex("b85058"), 세로줄 = Hex("5b1d26"), 눈금 = Hex("6b2229"),
        };
        public static readonly 판색 발동턴 = new 판색
        {
            바탕 = Hex("141008"), 테두리 = Hex("8a6d12"), 브래킷 = Hex("ffd23f"),
            라벨 = Hex("b8952a"), 세로줄 = Hex("5c4a14"), 눈금 = Hex("735d1b"),
        };
    }
}

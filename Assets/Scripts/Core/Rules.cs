using System.Collections.Generic;

namespace LadderBattle.Core
{
    /// <summary>
    /// 규칙 v1.0 의 고정값과 카드 정의.
    ///
    /// 이 파일은 `ladder-tcg-prototype.html` 의 규칙 엔진을 그대로 옮긴 것이다.
    /// 숫자를 바꾸면 HTML 과 어긋나므로, 바꿀 때는 기획서와 HTML 을 같이 고친다.
    /// 검증은 `_검증/sim.js` 가 HTML 쪽에서 하고 있고, 여기서는 EngineTests 가 같은 불변식을 본다.
    /// </summary>
    public static class Rules
    {
        // ── 판 ────────────────────────────────────────────────
        public const int Lanes = 3;     // 채널 수
        public const int Tiers = 3;     // 연결 회선을 놓을 수 있는 단계 수
        public const int Goal = 4;      // 패킷이 끝까지 가는 데 걸리는 걸음 수

        // ── 수치 ──────────────────────────────────────────────
        public const int MaxTurn = 10;
        public const int EndHp = 1;     // 서버 보안도. 1이라 한 방에 날아간다 (본인 10-11)
        public const int HandMax = 5;   // 손패 상한. 덱이 무한이므로 유일한 제동 장치다

        /// <summary>카드 등급. 보안 등급은 뒷면으로 깔리고 동시 발동에 공개된다.</summary>
        public enum Grade { Base, Security }

        /// <summary>카드를 낼 때 무엇을 고르게 할지.</summary>
        public enum Target { None, Lane, Slot }

        public enum CardId { Inject, Reroute, Buffer, ZeroDay, Firewall, Overflow }

        public sealed class CardDef
        {
            public readonly CardId Id;
            public readonly string Label;   // 화면에 쓰는 한국어 이름
            public readonly string En;      // 화면에 쓰는 영문
            public readonly Target Need;
            public readonly Grade Grade;
            public readonly string Desc;
            public readonly int Weight;     // 뽑힐 가중치 (장수가 아니다 — 덱은 무한이다)
            /// <summary>강화 카드가 무엇의 더 센 판인지. 일반 카드는 null.</summary>
            public readonly string Upgrade;

            public CardDef(CardId id, string label, string en, Target need, Grade grade, int weight,
                           string desc, string upgrade)
            {
                Id = id; Label = label; En = en; Need = need; Grade = grade; Weight = weight;
                Desc = desc; Upgrade = upgrade;
            }
        }

        /// <summary>
        /// 카드 6종. 이름은 해킹·보안 용어로 맞췄다 (본인 10-11).
        /// 코드 주입은 부록 A 용어표의 정본이고, 나머지는 효과와 말뜻이 맞물리게 골랐다.
        /// </summary>
        public static readonly IReadOnlyList<CardDef> Cards = new[]
        {
            new CardDef(CardId.Inject,   "코드 주입",     "INJECT",   Target.Lane, Grade.Base,     10,
                "채널 하나를 공격합니다. 같은 채널에 겹쳐 내면 세기가 더해집니다.", null),
            new CardDef(CardId.Reroute,  "라우팅 변경",   "REROUTE",  Target.Slot, Grade.Base,      5,
                "가로줄을 놓거나 부숩니다. 패킷이 지나는 길이 바뀝니다.", null),
            new CardDef(CardId.Buffer,   "버퍼 적재",     "BUFFER",   Target.None, Grade.Base,      3,
                "카드를 2장 뽑습니다.", null),
            new CardDef(CardId.ZeroDay,  "제로데이",      "ZERO-DAY", Target.Lane, Grade.Security,  1,
                "채널 하나를 공격합니다. 상대 공격과 부딪혀도 지워지지 않고 통과합니다.",
                "권한 상승 - 코드 주입"),
            new CardDef(CardId.Firewall, "방화벽",        "FIREWALL", Target.Lane, Grade.Security,  1,
                "채널 하나를 이번 턴 막습니다. 그 길을 지나는 패킷은 양쪽 다 사라집니다.",
                "권한 상승 - 라우팅 변경"),
            new CardDef(CardId.Overflow, "버퍼 오버플로", "OVERFLOW", Target.None, Grade.Security,  1,
                "카드를 4장 뽑습니다.", "권한 상승 - 버퍼 적재"),
        };

        static readonly Dictionary<CardId, CardDef> ById = BuildIndex();

        static Dictionary<CardId, CardDef> BuildIndex()
        {
            var d = new Dictionary<CardId, CardDef>();
            foreach (var c in Cards) d[c.Id] = c;
            return d;
        }

        public static CardDef Of(CardId id) => ById[id];

        /// <summary>가중치 합. 한 장을 뽑을 때 쓰는 분모다.</summary>
        public static int WeightSum
        {
            get
            {
                int n = 0;
                foreach (var c in Cards) n += c.Weight;
                return n;
            }
        }

        public static bool IsSecurity(CardId id) => Of(id).Grade == Grade.Security;

        /// <summary>서버가 터질수록 더 뽑는다 (본인 10-11). 막힌 길을 스스로 열라고 주는 숨통이다.</summary>
        public static int DrawCount(int aliveServers) => 1 + (Lanes - aliveServers);
    }
}

using System.Collections.Generic;
using System.IO;
using LadderBattle.Core;
using LadderBattle.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LadderBattle.EditorTools
{
    /// <summary>
    /// 시안에서 가져온 화면을 **움직이는 게임**으로 묶는다.
    ///
    /// 하는 일 네 가지.
    ///   1. 흩어져 있는 조각(육각형 · 보안도 글자 · 번짐)을 서버 하나로 묶는다
    ///   2. 실행 중에 개수가 바뀌는 것(카드 · 가로줄 · 봇 손패)을 프리팹으로 뽑는다
    ///   3. 누를 자리(채널 · 가로줄 자리)를 깔아 둔다 — 시안에는 없는, 조작에만 쓰는 판이다
    ///   4. 시작 화면과 종료 화면을 만들고 GameDirector 에 전부 물린다
    ///
    /// 피그마 노드 번호로 조각을 찾는다. 이름으로 찾으면 같은 이름이 여럿이라 틀린다.
    /// 시안에서 노드를 지우고 다시 만들면 번호가 바뀌므로 그때는 여기도 고쳐야 한다.
    /// </summary>
    public static class WireGame
    {
        const string PrefabDir = "Assets/Prefabs";

        // 시안(칩소켓 1 · 17:170)의 노드 번호
        const string 라운드글 = "17:177", 진행바채움 = "17:176", 코어현황 = "17:178";
        const string 단계제목 = "17:284", 단계캡션 = "17:285", 단계바채움 = "17:288";
        const string 인텔글 = "17:291", 소켓라벨 = "17:343";
        const string 꽂힌칩 = "17:278", 마지막카드글 = "17:279", 마지막카드결과 = "17:280";
        const string 손패틀 = "17:238", 카드본 = "17:239", 손패라벨 = "17:215";
        const string 봇손패줄 = "17:257", 봇카드본 = "17:258", 봇손패라벨 = "17:255";
        const string 가로줄본 = "17:195", 가로줄둘째 = "17:196";
        const string 취소버튼 = "17:301", 종료버튼 = "17:304", 종료버튼글 = "17:305";
        const string 보드 = "17:180";

        static readonly string[] 봇코어 = { "17:197", "17:198", "17:199" };
        static readonly string[] 내코어 = { "17:200", "17:201", "17:202" };
        static readonly string[] 봇보안도 = { "17:207", "17:205", null };
        static readonly string[] 내보안도 = { "17:208", "17:209", "17:210" };
        static readonly string[] 봇오프라인 = { null, null, "17:206" };
        static readonly string[] 봇패킷 = { null, "17:204", null };
        static readonly string[] 내패킷 = { "17:203", null, null };
        static readonly string[] 지울카드 = { "17:241", "17:243", "17:245" };
        static readonly string[] 지울봇카드 = { "17:259", "17:260", "17:261", "17:262" };

        // CPU 소켓 (기획서 10.1)
        const string 리테이너 = "18:344", 안쪽턱 = "18:345", 삼각형 = "18:350";
        const string 레버 = "18:353", 레버고리 = "18:354";
        static readonly string[] 핀그리드 = { "18:348", "18:349" };
        static readonly string[] 소켓노치 = { "18:351", "18:352" };
        static readonly string[] 마운트홀 = { "18:355", "18:356", "18:357" };

        // 기판 회로 (기획서 10.1) — 칩 쪽에서 판 쪽으로 늘어놓는다
        const string 회로묶음 = "17:307", 날아가는칩 = "17:328";
        static readonly string[] 회로메인 =
            { "17:308", "17:309", "17:310", "17:324", "17:325", "17:323" };
        static readonly string[] 회로보조 =
            { "17:313", "17:314", "17:316", "17:317", "17:318", "17:319",
              "17:320", "20:358", "17:321", "21:2", "17:322" };
        static readonly string[] 회로맺음 = { "17:326", "17:327", "20:361" };

        /// <summary>
        /// 칩이 실제로 타고 가는 길. **회로 묶음(380,430) 기준**이고 칩(11x26)의 왼쪽 위 좌표다.
        /// 메인 트레이스 세 토막을 따라가 도착 패드(548,479) 가운데에서 멈춘다.
        /// </summary>
        static readonly Vector2[] 흡수길 =
            { new Vector2(37f, 119f), new Vector2(134f, 119f),
              new Vector2(134f, 44f), new Vector2(171f, 44f) };

        // 판 — 턴에 따라 색이 바뀌는 것 (기획서 10.1)
        static readonly string[] 보드글 = { "17:190", "17:191" };
        static readonly string[] 세로줄 = { "17:192", "17:193", "17:194" };
        static readonly string[] 브래킷 =
            { "17:216", "17:217", "17:218", "17:219", "17:220", "17:221", "17:222", "17:223" };
        static readonly string[] 눈금 =
            { "17:181", "17:182", "17:183", "17:184", "17:185", "17:186",
              "17:187", "17:188", "17:189", "17:224", "17:225", "17:226" };

        // 전환 오버레이 (기획서 10.2) — 전환 화면 1장에서 오버레이 묶음만 가져온다
        const string 전환배치표 = BuildScreen.LayoutDir + "/13_44.layout.json";
        const string 전환묶음 = "13:204", 가림막 = "13:185";
        const string 명판 = "13:182", 명판큰글자 = "13:183", 명판캡션 = "13:184";
        static readonly string[] 코드레인줄기 =
            { "13:186", "13:187", "13:188", "13:189", "13:190", "13:191", "13:192",
              "13:193", "13:194", "13:195", "13:196", "13:197", "13:198" };
        static readonly string[] 코드레인머리 =
            { "13:199", "13:200", "13:201", "13:202", "13:203" };

        // 장식 (기획서 10.3) — 판을 바꾸지 않는, 화면이 살아 있다는 신호
        static readonly string[] 이진수장식 = { "17:179", "17:213" };
        static readonly string[] 바코드장식 = { "17:173", "17:214" };
        const string 타자장식 = "17:174";

        static readonly float[] 채널X = { 680f, 960f, 1240f };
        static readonly float[] 단계Y = { 631f, 498f, 363f };
        /// <summary>경로 칸의 y. 0이 내 서버, 4가 상대 서버다 (기획서 3.2).</summary>
        static readonly float[] 칸Y = { 776f, 631f, 498f, 363f, 200f };

        public static void 묶기(GameObject screenRoot, RectTransform host)
        {
            Directory.CreateDirectory(PrefabDir);
            screenRoot.name = "플레이 화면";

            var board = 서버묶기(screenRoot);
            var fx = 판연출묶기(board, screenRoot);
            var socket = 소켓묶기(screenRoot);
            손패묶기(out var hand, screenRoot, board);
            var hud = 머리말묶기(screenRoot);
            누를자리깔기(board, screenRoot);
            var trans = 전환묶기(screenRoot);
            장식묶기(screenRoot);

            // 끌고 있는 카드는 맨 위에 떠야 한다. 손패 밑에 두면 다른 카드에 가린다
            hand.끌기층 = (RectTransform)New("끌리는 카드", screenRoot.transform, 0, 0, 1920, 1080)
                            .transform;

            var flow = 흐름만들기(host, screenRoot);

            var dirGo = new GameObject("GameDirector");
            dirGo.transform.SetParent(host.parent, false);   // Canvas 밑에 둔다
            var dir = dirGo.AddComponent<GameDirector>();
            dir.판 = board;
            dir.손패 = hand;
            dir.머리말 = hud;
            dir.화면전환 = flow;
            dir.소켓 = socket;
            dir.명판 = trans;
            board.연출 = fx;

            // 그리는 차례 — 판 위에 연출, 그 위에 끌리는 카드, 맨 위에 전환 명판
            if (fx.연출층 != null) fx.연출층.SetAsLastSibling();
            hand.끌기층.SetAsLastSibling();
            if (trans != null && trans.오버레이 != null) trans.오버레이.transform.SetAsLastSibling();
        }

        // ── 1-2. 판 연출과 턴 색 (기획서 10.1 · 10.3) ──────────

        static BoardFxView 판연출묶기(BoardView board, GameObject root)
        {
            var fx = board.gameObject.AddComponent<BoardFxView>();
            var boardGo = board.gameObject;

            board.판영역 = (RectTransform)boardGo.transform;
            fx.판바탕 = boardGo.GetComponent<Image>();
            var ring = boardGo.transform.Find("테두리");
            if (ring != null) fx.판테두리 = ring.GetComponent<Image>();

            // 집고 있는 동안 판 테두리 전체가 소유 색으로 빛난다. 시안에는 없는 조각이다
            fx.판테두리번짐 = 테두리번짐붙이기(boardGo, 20);

            fx.브래킷 = 칠들(브래킷);
            fx.세로줄 = 칠들(세로줄);
            fx.눈금 = 칠들(눈금);
            fx.보드글 = 글들(보드글);

            fx.연출층 = (RectTransform)New("연출", root.transform, 0, 0, 1920, 1080).transform;
            fx.흔들대상 = (RectTransform)root.transform;
            fx.네모 = UiSprites.White();
            fx.동그라미 = UiSprites.Ellipse(48, 48);
            fx.글꼴 = FontAssets.Get(FontAssets.Rajdhani700);
            fx.채널X = 채널X;
            fx.칸Y = 칸Y;
            return fx;
        }

        // ── 1-3. CPU 소켓과 기판 회로 (기획서 10.1) ────────────

        static SocketView 소켓묶기(GameObject root)
        {
            var go = New("칩 소켓", root.transform, 0, 0, 1920, 1080);
            var s = go.AddComponent<SocketView>();

            // 색이 바뀌는 범위 — 소켓 · 회로 전체 · 꽂힌 칩의 테두리
            s.리테이너 = 선(리테이너);
            s.리테이너번짐 = 번짐찾기(Find(리테이너))?.GetComponent<Image>();
            s.안쪽턱 = 선(안쪽턱);
            s.삼각형 = 칠(삼각형);
            s.레버 = 칠(레버);
            s.레버고리 = 칠(레버고리);
            s.핀그리드 = 글들(핀그리드);
            s.노치 = 선들(소켓노치);
            s.마운트홀 = 선들(마운트홀);
            s.소켓라벨 = Text(소켓라벨);

            var chip = Find(꽂힌칩);
            // 칩은 흐려졌다 진해진다. 투명도를 다룰 칸을 미리 달아 둔다
            if (!chip.TryGetComponent<CanvasGroup>(out _)) chip.AddComponent<CanvasGroup>();
            s.칩 = (RectTransform)chip.transform;
            s.칩바탕 = chip.GetComponent<Image>();
            var chipRing = chip.transform.Find("테두리");
            if (chipRing != null) s.칩테두리 = chipRing.GetComponent<Image>();
            s.칩글 = Text(마지막카드글);
            s.칩결과 = Text(마지막카드결과);

            // 칩 안의 글도 카드와 같다 — 유니티는 줄바꿈을 알아서 하므로 켜 준다.
            // 시안은 피그마 글상자가 고정 너비라 손으로 끊어 둔 것이고, 여기서는 그럴 필요가 없다
            if (s.칩글 != null)
            {
                s.칩글.textWrappingMode = TextWrappingModes.Normal;
                s.칩글.overflowMode = TextOverflowModes.Truncate;
                ((RectTransform)s.칩글.transform).sizeDelta = new Vector2(146f, 180f);
            }

            s.회로 = Find(회로묶음);
            s.메인 = 그림들(회로메인);
            s.보조 = 그림들(회로보조);
            s.맺음 = 선들그림(회로맺음);
            var fly = Find(날아가는칩);
            s.날아가는칩 = (RectTransform)fly.transform;
            s.날아가는칩그림 = fly.GetComponent<Image>();
            s.길 = 흡수길;
            return s;
        }

        // ── 1-4. 턴 전환 명판 (기획서 10.2) ────────────────────

        static TransitionView 전환묶기(GameObject root)
        {
            // 전환 화면 시안은 플레이 화면을 그대로 품고 있다. 오버레이 묶음만 가져온다
            var overlay = FigmaImport.BuildSubtree(전환배치표, root.transform, 전환묶음, true);
            if (overlay == null) return null;
            overlay.name = "전환 명판";

            if (!overlay.TryGetComponent<CanvasGroup>(out _)) overlay.AddComponent<CanvasGroup>();
            var t = root.AddComponent<TransitionView>();
            t.오버레이 = overlay;
            t.가림막 = Find(가림막)?.GetComponent<Image>();
            t.줄기 = 글들(코드레인줄기);
            t.머리 = 글들(코드레인머리);

            var plate = Find(명판);
            t.명판 = (RectTransform)plate.transform;
            var pring = plate.transform.Find("테두리");
            if (pring != null) t.명판테두리 = pring.GetComponent<Image>();
            t.명판번짐 = 번짐찾기(plate)?.GetComponent<Image>();
            t.큰글자 = Text(명판큰글자);
            t.캡션 = Text(명판캡션);

            // 명판 글로우는 명판과 형제라서 오버레이 밑에 그대로 있다. 같이 켜지고 꺼진다
            overlay.SetActive(false);
            return t;
        }

        // ── 1-5. 장식 (기획서 10.3) ────────────────────────────

        static void 장식묶기(GameObject root)
        {
            var d = root.AddComponent<DecorView>();
            d.이진수 = 글들(이진수장식);
            d.바코드 = 글들(바코드장식);
            d.타자글 = Text(타자장식);
        }

        /// <summary>테두리만 빛나는 번짐. 가운데가 비어 있어 판 안을 가리지 않는다.</summary>
        static Image 테두리번짐붙이기(GameObject node, int radius)
        {
            var rt = (RectTransform)node.transform;
            var size = rt.sizeDelta;
            var go = New("판 테두리 번짐", node.transform.parent,
                         rt.anchoredPosition.x - radius, -rt.anchoredPosition.y - radius,
                         size.x + radius * 2f, size.y + radius * 2f);
            go.transform.SetSiblingIndex(node.transform.GetSiblingIndex() + 1);
            var img = go.AddComponent<Image>();
            img.sprite = UiSprites.EdgeGlow(radius);
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            img.color = new Color(Palette.나밝음.r, Palette.나밝음.g, Palette.나밝음.b, 0.55f);
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        // ── 1. 서버 ────────────────────────────────────────────

        static BoardView 서버묶기(GameObject root)
        {
            var boardGo = Find(보드);
            var board = boardGo.AddComponent<BoardView>();
            board.채널X = 채널X;
            board.단계Y = 단계Y;

            for (int l = 0; l < 3; l++)
            {
                board.봇서버[l] = 서버하나(root, Side.B, l, 봇코어[l], 봇보안도[l], 봇오프라인[l], 봇패킷[l]);
                board.내서버[l] = 서버하나(root, Side.P, l, 내코어[l], 내보안도[l], 봇오프라인[0], 내패킷[l]);
            }

            // 가로줄 — 하나를 본으로 뽑고 시안에 그려 둔 둘은 치운다
            var rung = 가로줄프리팹(root);
            board.가로줄프리팹 = rung;
            var holder = New("가로줄들", root.transform, 0, 0, 1920, 1080);
            board.가로줄부모 = (RectTransform)holder.transform;
            return board;
        }

        static ServerView 서버하나(GameObject root, Side side, int lane,
                                   string hexId, string hpId, string offId, string pwrId)
        {
            var hex = Find(hexId);
            var hexRt = (RectTransform)hex.transform;
            Vector2 pos = hexRt.anchoredPosition;
            Vector2 size = hexRt.sizeDelta;

            var go = New($"서버 - {(side == Side.P ? "나" : "봇")} 채널 {lane + 1}",
                         hex.transform.parent, pos.x, -pos.y, size.x, size.y);
            go.transform.SetSiblingIndex(hex.transform.GetSiblingIndex());

            var view = go.AddComponent<ServerView>();
            view.편 = side;
            view.채널 = lane;

            var glow = 번짐찾기(hex);
            if (glow != null) { glow.transform.SetParent(go.transform, true); view.번짐 = glow.GetComponent<Image>(); }
            hex.transform.SetParent(go.transform, true);
            view.육각형 = hex.GetComponent<Image>();
            var ring = hex.transform.Find("테두리");
            if (ring != null) view.테두리 = ring.GetComponent<Image>();

            // 보안도 숫자 — 시안에 없는 자리는 다른 서버 것을 베껴 쓴다
            var hp = hpId != null ? Find(hpId) : Clone(내보안도[0], go.transform);
            var hpGlow = 옮기기(hp, go.transform, "보안도", 12f, 6f, 32f, 44f);
            view.보안도 = hp.GetComponent<TextMeshProUGUI>();
            if (hpGlow != null) view.보안도번짐 = hpGlow.GetComponent<Image>();

            var off = offId != null ? Find(offId) : Clone(봇오프라인[2], go.transform);
            옮기기(off, go.transform, "오프라인", 0f, 22f, size.x, 12f);
            view.오프라인 = off.GetComponent<TextMeshProUGUI>();
            view.오프라인.text = "OFFLINE";

            var pwr = pwrId != null ? Find(pwrId) : Clone(내패킷[0], go.transform);
            옮기기(pwr, go.transform, "패킷",
                   side == Side.P ? 64f : 72f, side == Side.P ? 34f : 3f, 118f, 16f);
            view.패킷 = pwr.GetComponent<TextMeshProUGUI>();

            return view;
        }

        static RungView 가로줄프리팹(GameObject root)
        {
            var src = Find(가로줄본);
            var holder = New("가로줄", root.transform, 0, 0, 280, 4);
            var view = holder.AddComponent<RungView>();

            var glow = 번짐찾기(src);
            if (glow != null)
            {
                glow.transform.SetParent(holder.transform, false);
                Place(glow, -12f, -12f, 280f + 24f, 4f + 24f);
                view.번짐 = glow.GetComponent<Image>();
            }
            src.transform.SetParent(holder.transform, false);
            Place(src, 0f, 0f, 280f, 4f);
            view.막대 = src.GetComponent<Image>();

            string path = $"{PrefabDir}/가로줄.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(holder, path);
            Object.DestroyImmediate(holder);

            // 시안에 그려 둔 둘째 줄은 실행 중에 만들 것이므로 치운다
            var second = Find(가로줄둘째);
            if (second != null)
            {
                var g2 = 번짐찾기(second);
                if (g2 != null) Object.DestroyImmediate(g2);
                Object.DestroyImmediate(second);
            }
            return prefab.GetComponent<RungView>();
        }

        // ── 2. 손패 ────────────────────────────────────────────

        static void 손패묶기(out HandView hand, GameObject root, BoardView board)
        {
            var handGo = Find(손패틀);
            Place(handGo, 647f, 876f, 626f, 182f);
            hand = handGo.AddComponent<HandView>();
            hand.손패부모 = (RectTransform)handGo.transform;
            hand.손패라벨 = Find(손패라벨).GetComponent<TextMeshProUGUI>();

            // 카드 본 — 시안의 첫 장을 프리팹으로 뽑고, 나머지 석 장은 치운다
            var card = Find(카드본);
            card.transform.SetParent(handGo.transform, false);
            Place(card, 0f, 0f, 147f, 170f);
            var cv = card.AddComponent<CardView>();
            cv.바탕 = card.GetComponent<Image>();
            var cring = card.transform.Find("테두리");
            if (cring != null) cv.테두리 = cring.GetComponent<Image>();
            cv.글 = card.GetComponentInChildren<TextMeshProUGUI>();
            if (cv.글 != null)
            {
                var grt = (RectTransform)cv.글.transform;
                grt.anchoredPosition = new Vector2(15f, -15f);
                grt.sizeDelta = new Vector2(117f, 140f);
                cv.글.textWrappingMode = TextWrappingModes.Normal;   // 유니티는 줄바꿈을 알아서 한다
            }
            cv.번짐 = 번짐붙이기(card, 16, Palette.나밝음);
            cv.그림자 = 그림자붙이기(card);
            cv.버튼 = 누르게(card);

            var cardPrefab = PrefabUtility.SaveAsPrefabAsset(card, $"{PrefabDir}/카드.prefab");
            hand.카드프리팹 = cardPrefab.GetComponent<CardView>();
            Object.DestroyImmediate(card);
            foreach (var id in 지울카드) 지우기(id);

            // 봇 손패 뒷면
            // 자리는 손대지 않는다. 이 줄은 '봇 손패' 묶음 안에 있어서 시안의 절대 좌표를
            // 그대로 넣으면 부모 위치가 한 번 더 더해져 화면 밖으로 밀린다
            var backHost = Find(봇손패줄);
            hand.봇손패부모 = (RectTransform)backHost.transform;
            hand.봇손패라벨 = Find(봇손패라벨).GetComponent<TextMeshProUGUI>();

            var back = Find(봇카드본);
            Place(back, 0f, 0f, 48f, 64f);
            var backPrefab = PrefabUtility.SaveAsPrefabAsset(back, $"{PrefabDir}/봇카드뒷면.prefab");
            hand.봇카드프리팹 = backPrefab;
            Object.DestroyImmediate(back);
            foreach (var id in 지울봇카드) 지우기(id);

            // 적 배치 예고에 쓰는 번짐. 런타임에서는 그림을 만들 수 없어 여기서 넣어 둔다
            hand.번짐그림 = UiSprites.Glow(12);
        }

        /// <summary>
        /// 집었을 때 아래로 깔리는 그림자 (기획서 10.1 의 1단계).
        /// 카드가 떠올랐다는 신호다 — 평소에는 꺼져 있다.
        /// </summary>
        static Image 그림자붙이기(GameObject node)
        {
            const int r = 14;
            var rt = (RectTransform)node.transform;
            var size = rt.sizeDelta;
            var go = New("그림자", node.transform, -r, -r, size.x + r * 2f, size.y + r * 2f);
            go.transform.SetAsFirstSibling();   // 카드 바탕보다 뒤
            var img = go.AddComponent<Image>();
            img.sprite = UiSprites.Glow(r);
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            img.color = new Color(0f, 0f, 0f, 0.75f);
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        // ── 3. 머리말 ──────────────────────────────────────────

        static HudView 머리말묶기(GameObject root)
        {
            var hud = root.AddComponent<HudView>();
            hud.라운드글 = Text(라운드글);
            hud.진행바채움 = (RectTransform)Find(진행바채움).transform;
            hud.코어현황 = Text(코어현황);
            hud.단계제목 = Text(단계제목);
            hud.단계캡션 = Text(단계캡션);
            hud.단계바채움 = (RectTransform)Find(단계바채움).transform;
            hud.인텔글 = Text(인텔글);
            hud.행동취소 = 누르게(Find(취소버튼));
            hud.행동종료 = 누르게(Find(종료버튼));
            hud.행동종료글 = Text(종료버튼글);

            // 인텔 글은 줄이 늘면 패널을 넘친다. 넘치면 잘라 보여 준다
            hud.인텔글.textWrappingMode = TextWrappingModes.Normal;
            hud.인텔글.overflowMode = TextOverflowModes.Truncate;
            var irt = (RectTransform)hud.인텔글.transform;
            irt.sizeDelta = new Vector2(292f, 230f);
            return hud;
        }

        // ── 4. 누를 자리 ───────────────────────────────────────

        static void 누를자리깔기(BoardView board, GameObject root)
        {
            var host = New("누를 자리", root.transform, 0, 0, 1920, 1080);
            for (int l = 0; l < 3; l++)
            {
                var go = New($"채널 {l + 1} 고르기", host.transform, 채널X[l] - 40f, 230f, 80f, 532f);
                board.채널버튼[l] = 반투명버튼(go, Palette.나밝음, 0.12f);
                go.SetActive(false);
            }
            for (int i = 0; i < 6; i++)
            {
                int tier = i / 2 + 1, left = i % 2;
                var go = New($"{tier}단 {left + 1}-{left + 2} 자리", host.transform,
                             채널X[left] + 16f, 단계Y[tier - 1] - 13f, 248f, 26f);
                board.자리버튼[i] = 반투명버튼(go, Palette.강화선, 0.18f);
                board.자리표시[i] = go.GetComponent<Image>();
                go.SetActive(false);
            }
        }

        // ── 5. 시작 · 종료 화면 ────────────────────────────────

        static ScreenFlow 흐름만들기(RectTransform host, GameObject play)
        {
            var flow = host.gameObject.AddComponent<ScreenFlow>();
            flow.플레이화면 = play;

            // 시작 화면
            var start = New("시작 화면", host, 0, 0, 1920, 1080);
            칠하기(start, Palette.배경);
            제목(start, 300f, "사다리 // LADDER BATTLE OS", 56, Palette.적밝음, FontAssets.Rajdhani700);
            제목(start, 372f, "V 1.0   ·   SHARED DECK   ·   MIRROR MATCH", 20, Palette.적어두움, FontAssets.Inter400);
            제목(start, 470f,
                 "3채널 사다리 위로 패킷을 보내 상대 서버 셋을 모두 떨어뜨린다.\n" +
                 "공격 턴에 먼저 놓고, 방어 턴이 보고 대응한 뒤, 같은 턴 끝에 동시 발동한다.\n" +
                 "같은 칸에서 만난 패킷은 서로 지워진다. 어느 한쪽 서버가 전부 부서질 때까지 계속한다.",
                 20, Palette.나중간, FontAssets.Inter400);
            flow.시작버튼 = 큰버튼(start, 700f, "EXEC >> 접속", Palette.적밝음, Palette.어두운글);
            flow.시작화면 = start;

            // 종료 화면
            var end = New("종료 화면", host, 560, 330, 800, 420);
            칠하기(end, Palette.Hex("05080c", 0.96f));
            테두리(end, 800, 420, Palette.나밝음, 2f);
            flow.결과제목 = 제목(end, 60f, "RESULT", 44, Palette.나밝음, FontAssets.Rajdhani700, 800f);
            flow.결과설명 = 제목(end, 150f, "", 20, Palette.나글, FontAssets.Inter400, 800f);
            flow.다시버튼 = 큰버튼(end, 290f, "RETRY >> 다시", Palette.나밝음, Palette.어두운글, 800f);
            flow.종료화면 = end;
            end.SetActive(false);
            start.SetActive(false);
            return flow;
        }

        static TextMeshProUGUI 제목(GameObject host, float y, string s, int size, Color c,
                                    string font, float width = 1920f)
        {
            var go = New(s.Length > 12 ? s.Substring(0, 12) : s, host.transform, 0, y, width, size * 4f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = FontAssets.Get(font);
            t.fontSize = size;
            t.color = c;
            t.text = s;
            t.alignment = TextAlignmentOptions.Top;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        static Button 큰버튼(GameObject host, float y, string label, Color fill, Color ink, float width = 1920f)
        {
            var go = New("버튼 - " + label, host.transform, width / 2f - 180f, y, 360f, 72f);
            var img = go.AddComponent<Image>();
            img.sprite = UiSprites.White();
            img.color = fill;
            var t = 제목(go, 20f, label, 26, ink, FontAssets.Inter700, 360f);
            t.alignment = TextAlignmentOptions.Top;
            return go.AddComponent<Button>();
        }

        // ── 잡일 ──────────────────────────────────────────────

        static GameObject Find(string id)
        {
            if (FigmaImport.ById.TryGetValue(id, out var go) && go != null) return go;
            Debug.LogError($"[묶기] 시안 노드 {id} 를 못 찾았다. 피그마에서 지워졌는지 확인한다.");
            return null;
        }

        static TextMeshProUGUI Text(string id) => Find(id)?.GetComponent<TextMeshProUGUI>();

        /// <summary>그 노드의 칠. 색을 바꿀 대상이 속칠일 때 쓴다.</summary>
        static Image 칠(string id) => Find(id)?.GetComponent<Image>();

        /// <summary>
        /// 그 노드의 **테두리**. 피그마에서 칠과 테두리 색이 다르면 Image 를 겹쳐 두었으므로,
        /// 색을 바꿀 대상이 선일 때는 자식 "테두리" 를 집어야 한다.
        /// 테두리가 없으면 칠을 돌려준다.
        /// </summary>
        static Image 선(string id)
        {
            var go = Find(id);
            if (go == null) return null;
            var ring = go.transform.Find("테두리");
            return ring != null ? ring.GetComponent<Image>() : go.GetComponent<Image>();
        }

        static Image[] 칠들(string[] ids)
        {
            var a = new Image[ids.Length];
            for (int i = 0; i < ids.Length; i++) a[i] = 칠(ids[i]);
            return a;
        }

        static Image[] 선들(string[] ids)
        {
            var a = new Image[ids.Length];
            for (int i = 0; i < ids.Length; i++) a[i] = 선(ids[i]);
            return a;
        }

        static Graphic[] 선들그림(string[] ids)
        {
            var a = new Graphic[ids.Length];
            for (int i = 0; i < ids.Length; i++) a[i] = 선(ids[i]);
            return a;
        }

        /// <summary>칠이든 글이든 색을 바꿀 수 있는 것으로 받는다. 회로 가닥에 쓴다.</summary>
        static Graphic[] 그림들(string[] ids)
        {
            var a = new Graphic[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                var go = Find(ids[i]);
                a[i] = go != null ? go.GetComponent<Graphic>() : null;
                if (a[i] == null) a[i] = 선(ids[i]);
            }
            return a;
        }

        static TextMeshProUGUI[] 글들(string[] ids)
        {
            var a = new TextMeshProUGUI[ids.Length];
            for (int i = 0; i < ids.Length; i++) a[i] = Text(ids[i]);
            return a;
        }

        static void 지우기(string id)
        {
            var go = Find(id);
            if (go == null) return;
            var glow = 번짐찾기(go);
            if (glow != null) Object.DestroyImmediate(glow);
            Object.DestroyImmediate(go);
        }

        static GameObject Clone(string id, Transform parent)
        {
            var src = Find(id);
            return src == null ? null : Object.Instantiate(src, parent, false);
        }

        /// <summary>
        /// 조각을 옮긴다. **딸린 번짐을 같이 끌고 가야 한다.**
        /// 번짐은 형제로 두었기 때문에 본체만 옮기면 번짐이 옛 자리에 남고,
        /// 그러면 나중에 그려져서 옮긴 것 위에 덮인다 (부서진 서버가 빨갛게 보이던 원인).
        /// </summary>
        static GameObject 옮기기(GameObject node, Transform parent, string name,
                                 float x, float y, float w, float h)
        {
            var glow = 번짐찾기(node);
            if (glow != null)
            {
                var grt = (RectTransform)glow.transform;
                Vector2 was = grt.sizeDelta;
                float pad = (was.x - ((RectTransform)node.transform).sizeDelta.x) * 0.5f;
                glow.transform.SetParent(parent, false);
                glow.name = name + " 번짐";
                FigmaImport.Place(grt, x - pad, y - pad, w + pad * 2f, h + pad * 2f);
            }
            node.transform.SetParent(parent, true);
            node.name = name;
            Place(node, x, y, w, h);
            return glow;
        }

        /// <summary>번짐은 형제로 두었다. 이름이 "<이름> 번짐" 이다.</summary>
        static GameObject 번짐찾기(GameObject node)
        {
            var parent = node.transform.parent;
            if (parent == null) return null;
            string want = node.name + " 번짐";
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == want) return parent.GetChild(i).gameObject;
            return null;
        }

        static GameObject New(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            FigmaImport.Place((RectTransform)go.transform, x, y, w, h);
            return go;
        }

        static void Place(GameObject go, float x, float y, float w, float h)
            => FigmaImport.Place((RectTransform)go.transform, x, y, w, h);

        static void 칠하기(GameObject go, Color c)
        {
            if (!go.TryGetComponent<Image>(out var img)) img = go.AddComponent<Image>();
            img.sprite = UiSprites.White();
            img.color = c;
            img.raycastTarget = true;    // 뒤에 있는 것이 눌리지 않게 막는다
        }

        static void 테두리(GameObject host, float w, float h, Color c, float t)
        {
            var go = New("테두리", host.transform, 0, 0, w, h);
            var img = go.AddComponent<Image>();
            img.sprite = UiSprites.RectRing(t);
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            img.color = c;
            img.raycastTarget = false;
        }

        /// <summary>고른 카드에만 켜지는 번짐. 시안의 선택된 카드와 같은 모양이다.</summary>
        static Image 번짐붙이기(GameObject node, int radius, Color c)
        {
            var rt = (RectTransform)node.transform;
            var size = rt.sizeDelta;
            var go = New("번짐", node.transform, -radius, -radius,
                         size.x + radius * 2f, size.y + radius * 2f);
            go.transform.SetSiblingIndex(0);   // 카드 바탕보다 뒤
            var img = go.AddComponent<Image>();
            img.sprite = UiSprites.Glow(radius);
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            img.color = new Color(c.r, c.g, c.b, 0.55f);
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        static Button 누르게(GameObject go)
        {
            var img = go.GetComponent<Image>();
            if (img == null)
            {
                img = go.AddComponent<Image>();
                img.sprite = UiSprites.White();
                img.color = new Color(0f, 0f, 0f, 0f);
            }
            img.raycastTarget = true;
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            return b;
        }

        static Button 반투명버튼(GameObject go, Color c, float a)
        {
            var img = go.AddComponent<Image>();
            img.sprite = UiSprites.White();
            img.color = new Color(c.r, c.g, c.b, a);
            img.raycastTarget = true;
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            return b;
        }
    }
}

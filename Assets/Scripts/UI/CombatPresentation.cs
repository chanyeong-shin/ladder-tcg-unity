using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI
{
    public sealed class CombatPresentation : MonoBehaviour
    {
        int playbackVersion;
        BattleController c; readonly List<RectTransform> visuals = new List<RectTransform>();
        readonly List<Vector2> origins = new List<Vector2>(); RectTransform layer;
        readonly List<CanvasGroup> peripheral = new List<CanvasGroup>();
        readonly List<float> peripheralAlpha = new List<float>();
        public float PeripheralAlpha { get; private set; } = 1;
        public string Phase { get; private set; } = "idle";
        public float Progress { get; private set; }
        public string ClashPhase { get; private set; } = "idle";
        public float CameraZoom { get; private set; } = 1;
        public Vector2 CameraOffset { get; private set; }
        public bool ResolutionFocused { get; private set; }
        public int BlockContactCount { get; private set; }
        public Vector2 LastBlockContact { get; private set; }
        // FxClock.Scale: 검사가 캡처 순간에 시계를 멈춘다(Week12Check·Week13Check)
        float Delta => c.Paused ? 0 : Time.unscaledDeltaTime * Mathf.Max(.1f, c.PlaybackSpeed) * FxClock.Scale;
        public void Initialize(BattleController controller)
        {
            c = controller;
            foreach (var background in c.backgrounds)
            {
                var surface = BattleController.Find(background.transform,"사다리 판 - 배경과 회선") as RectTransform;
                if(surface) {
                    var old = BattleController.Find(surface,"사다리 내부 - 중앙 음영"); if(old)old.gameObject.SetActive(false);
                    var gradient = Ui.Rect("사다리 내부 - 부드러운 중앙 밝기",surface,0,0,800,716).gameObject.AddComponent<BoardGradient>();
                    gradient.transform.SetAsFirstSibling();gradient.raycastTarget=false;
                    if(background==c.backgrounds[1]) {gradient.BaseColor=new Color(.14f,.055f,.075f);gradient.LightColor=new Color(.63f,.25f,.34f);}
                    if(background==c.backgrounds[2]) {gradient.BaseColor=new Color(.10f,.09f,.045f);gradient.LightColor=new Color(.63f,.51f,.25f);}
                }
                var board = Ui.Rect("사다리 시각 카메라", background.transform, 0, 0, 1920, 1080);
                // Move the whole board, including its surface, border and headings.
                var nodes = background.transform.Cast<Transform>().OfType<RectTransform>().Where(t => t != board && (t==surface || t.anchoredPosition.x >= 582 && t.anchoredPosition.x + t.sizeDelta.x <= 1382 && -t.anchoredPosition.y >= 129 && -t.anchoredPosition.y + t.sizeDelta.y <= 845)).ToArray();
                foreach (var node in nodes) node.SetParent(board, false); Track(board);
                // All square contacts are drawn AFTER the vertical tracks; the import
                // mixed nested middle contacts with root-level bottom/top contacts.
                var contacts=Ui.Rect("접점 - 선 위",board,0,0,1920,1080);
                foreach(int id in new[]{270,271,272,273,274,275,276,277,278,294,295,296}) {
                    var pin=BattleController.Find(background.transform,"Source_64_"+id) as RectTransform;if(!pin)continue;
                    pin.SetParent(contacts,true);
                    int row=id>=294?0:id>=276?2:1;
                    pin.anchoredPosition=new Vector2(pin.anchoredPosition.x,-BattleController.RowY[row]+pin.rect.height/2);
                }
                contacts.SetAsLastSibling();
                ViaBus(board, contacts);
                foreach(Transform item in background.transform) {
                    var r=item as RectTransform;if(!r||r==board||r==surface)continue;
                    if(r.anchoredPosition.x+r.rect.width<=582||r.anchoredPosition.x>=1382||-r.anchoredPosition.y+r.rect.height<=129||-r.anchoredPosition.y>=845) Dim(item.gameObject);
                }
            }
            Track(c.rungs); Track(c.paths); foreach (var server in c.servers) Track((RectTransform)server.transform);
            // user 11-22: path glow from loads, rungs and the resolve sits under the servers, not over them
            var serverParent = c.servers[0].transform.parent; int below = c.servers.Where(v => v.transform.parent == serverParent).Min(v => v.transform.GetSiblingIndex());
            underServers = Ui.Rect("이벤트 빛 - 서버 아래", serverParent, 0, 0, 1920, 1080); underServers.SetSiblingIndex(below); Track(underServers);
            layer = Ui.Rect("공격 연출 - 카메라 연동", c.game.transform, 0, 0, 1920, 1080); layer.SetAsLastSibling(); Track(layer);
            foreach(var item in new GameObject[]{c.hand.gameObject,c.endButton.gameObject,c.undoButton.gameObject,c.logButton.gameObject,c.roundLabel.gameObject,c.statusLabel.gameObject,c.secretLabel.gameObject})Dim(item);
        }
        // Board (2026-11-21 selection): contacts become via rings (20 px, 3 px #7fe9ff rim, #07131a inside)
        // above a three-line bus per vertical line (3 px #3aa6c6 center, 1 px #337a94 at +-7.5 px, no glow).
        Vector2 StagePoint(Vector3 world){var p=c.stage.InverseTransformPoint(world);return new Vector2(p.x-c.stage.rect.xMin,c.stage.rect.yMax-p.y);}
        Rect StageRect(RectTransform r){var corners=new Vector3[4];r.GetWorldCorners(corners);var a=StagePoint(corners[1]);var b=StagePoint(corners[3]);return Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));}
        void ViaBus(RectTransform board,RectTransform contacts)
        {
            float[] top={231,231,231},bottom={763,763,763};bool[] found=new bool[3];
            foreach(var g in board.GetComponentsInChildren<Graphic>(true)){
                if(g.transform.IsChildOf(contacts))continue;var r=StageRect(g.rectTransform);if(r.width>10||r.height<250)continue;
                for(int lane=0;lane<3;lane++)if(Mathf.Abs(r.center.x-BattleController.LaneX[lane])<12){g.enabled=false;if(!found[lane]){top[lane]=r.yMin;bottom[lane]=r.yMax;found[lane]=true;}else{top[lane]=Mathf.Min(top[lane],r.yMin);bottom[lane]=Mathf.Max(bottom[lane],r.yMax);}}
            }
            var bus=Ui.Rect("세로선 - 세 줄 버스",board,0,0,1920,1080);bus.SetSiblingIndex(contacts.GetSiblingIndex());
            var center=new Color(.227f,.651f,.776f);var side=new Color(.2f,.478f,.58f);
            for(int lane=0;lane<3;lane++){float x=BattleController.LaneX[lane];
                Ui.Line("버스 가운데",bus,new Vector2(x,top[lane]),new Vector2(x,bottom[lane]),center,3);
                Ui.Line("버스 왼쪽",bus,new Vector2(x-7.5f,top[lane]),new Vector2(x-7.5f,bottom[lane]),side,1.5f);
                Ui.Line("버스 오른쪽",bus,new Vector2(x+7.5f,top[lane]),new Vector2(x+7.5f,bottom[lane]),side,1.5f);}
            var pins=contacts.Cast<Transform>().OfType<RectTransform>().ToArray();
            foreach(var pin in pins){var r=StageRect(pin);foreach(var g in pin.GetComponentsInChildren<Graphic>(true))g.enabled=false;
                var ring=Ui.Box("접점 - 비아 고리",contacts,r.center.x-10,r.center.y-10,20,20,new Color(.498f,.914f,1),true,1,32);
                Ui.Box("비아 고리 안",ring.transform,3,3,14,14,new Color(.027f,.075f,.102f),true,1,32);}
        }
        void Dim(GameObject item){var group=item.GetComponent<CanvasGroup>();if(!group)group=item.AddComponent<CanvasGroup>();peripheral.Add(group);peripheralAlpha.Add(group.alpha);}
        // 11-27 user "오른쪽 위 강화카드 대기/ 이거 글자가 사다리 영역이 확대되도 있어서 이상함": the PENDING line sits on the board's top-right corner,
        // so while the board is focused/zoomed it fades out completely instead of to .48 like the other side UI.
        void SetPeripheral(float weight){PeripheralAlpha=Mathf.Lerp(1,.48f,Mathf.Clamp01(weight));for(int i=0;i<peripheral.Count;i++)if(peripheral[i])peripheral[i].alpha=peripheralAlpha[i]*(c.secretLabel&&peripheral[i].gameObject==c.secretLabel.gameObject?1-Mathf.Clamp01(weight):PeripheralAlpha);}
        void Track(RectTransform r) { visuals.Add(r); origins.Add(r.anchoredPosition); }
        public void ResetView() { for (int i = 0; i < visuals.Count; i++) if (visuals[i]) { visuals[i].anchoredPosition = origins[i]; visuals[i].localScale = Vector3.one; } CameraZoom = 1; CameraOffset = Vector2.zero; SetPeripheral(0); ResolutionFocused=false; }
        public RectTransform BoardEffect(string name) => Ui.Rect(name,layer,0,0,1920,1080);
        RectTransform underServers;
        public RectTransform BoardEffectUnderServers(string name) => Ui.Rect(name, underServers ? underServers : layer, 0, 0, 1920, 1080);
        // Rung segments of a packet path (two consecutive cells on one row): where along the path it starts, and its ends
        // inset 16 px from the lanes like the HTML (8 x 2).
        static List<(float at, int index, Vector2 a, Vector2 b)> RungCrossings(Shot s)
        {
            var list = new List<(float, int, Vector2, Vector2)>(); var cells = s.packet.Cells; float total = 0; for (int i = 1; i < s.path.Count; i++) total += Vector2.Distance(s.path[i - 1], s.path[i]);
            float acc = 0; for (int i = 1; i < cells.Count && i < s.path.Count; i++) { if (cells[i].Row == cells[i - 1].Row && cells[i].Row >= 0 && cells[i].Row <= 2 && cells[i].Lane != cells[i - 1].Lane) { float x0 = Mathf.Min(s.path[i - 1].x, s.path[i].x) + 16, x1 = Mathf.Max(s.path[i - 1].x, s.path[i].x) - 16; list.Add((total > 0 ? acc / total : 0, i, new Vector2(x0, s.path[i].y), new Vector2(x1, s.path[i].y))); } acc += Vector2.Distance(s.path[i - 1], s.path[i]); }
            return list;
        }
        public IEnumerator BeginResolution()
        {
            if(ResolutionFocused)yield break;ResolutionFocused=true;Phase="focus";
            int version=playbackVersion;
            for(float t=0;t<1;t+=Delta/.42f){if(version!=playbackVersion)yield break;Progress=t;Frame(new Vector2(982,487),Smooth(t));yield return null;}
            Frame(new Vector2(982,487),1);Phase="ready";
        }
        void Frame(Vector2 focus, float weight, float trauma = 0, float time = 0)
        {
            SetPeripheral(weight);
            float zoom = 1 + .08f * weight; Vector2 f = new Vector2(focus.x, -focus.y), center = new Vector2(982, -487);
            Vector2 shift = center * (1 - zoom) + (center - f) * (.025f * weight) + Vector2.up*(22*weight);
            // Full board stays between the upper phase strip and the fixed hand.
            shift.y = Mathf.Clamp(shift.y, 845 * zoom - 854, 129 * zoom - 74);
            shift += new Vector2(Mathf.Sin(time * 43), Mathf.Sin(time * 31) * .55f) * trauma;
            for (int i = 0; i < visuals.Count; i++) if (visuals[i]) { visuals[i].anchoredPosition = origins[i] * zoom + shift; visuals[i].localScale = Vector3.one * zoom; }
            CameraZoom = zoom; CameraOffset = shift;
        }
        static float Smooth(float p) { p = Mathf.Clamp01(p); return p * p * (3 - 2 * p); }
        static void At(RectTransform r, Vector2 p) { r.anchoredPosition = new Vector2(p.x, -p.y); }
        public static FlareGraphic Flare(Transform parent, string name, Vector2 p, Color color, float size = 1)
        {
            var r = Ui.Rect(name, parent, p.x - 95 * size, p.y - 26 * size, 190 * size, 52 * size); var g = r.gameObject.AddComponent<FlareGraphic>(); g.color = color; g.raycastTarget = false; return g;
        }
        public static RectTransform Firewall(Transform parent, float x, float y, float closed = 1)
        {
            var root = Ui.Rect("반도체 방화벽", parent, x - 43, y - 48, 86, 96);
            for (int side = 0; side < 2; side++)
            {
                var part = Ui.Rect("방화벽 접점 " + side, root, side == 0 ? -24 * (1 - closed) : 46 + 24 * (1 - closed), 0, 40, 96);
                Ui.Box("칩 하우징", part, 0, 0, 40, 96, new Color(.10f, .09f, .035f)); Ui.Box("하우징 외곽", part, 0, 0, 40, 96, Ui.Gold, false, 1.5f);
                for (int pin = 0; pin < 6; pin++) { float py = 10 + pin * 15; Ui.Line("미세 회로", part, new Vector2(4, py), new Vector2(34, py), new Color(1, .77f, .22f, .55f), 1); Ui.Box("핀", part, side == 0 ? 35 : -1, py - 2, 6, 4, Ui.Gold); }
            }
            Ui.Box("채널 잠금 칩", root, 30, 34, 26, 28, Ui.Gold, false, 2);
            // Short horizontal gate immediately before the server, outside its shell.
            root.localScale=new Vector3(1,.55f,1);root.anchoredPosition=new Vector2(x-43,-(y-26.4f));return root;
        }
        public static Vector2 FirewallPoint(int serverOwner,int lane) => new Vector2(BattleController.LaneX[lane],serverOwner==0?686:292);
        public static IEnumerable<Vector2> FirewallEndpoints(Battle state,int onlyChannel=-1)
        {
            var seen=new HashSet<int>();
            // The channel rule is unchanged. Project its blocked paths onto the
            // receiving server gates, including rerouted paths and both directions.
            for(int sender=0;sender<2;sender++)for(int lane=0;lane<3;lane++){
                var route=state.Route(sender,lane);
                if(!route.Cells.Any(cell=>onlyChannel>=0?cell.Lane==onlyChannel:state.Walls[cell.Lane]))continue;
                int receiver=1-sender,key=receiver*3+route.End;
                if(seen.Add(key))yield return FirewallPoint(receiver,route.End);
            }
        }
        public static void CloseFirewall(RectTransform root, float amount)
        {
            amount = Mathf.Clamp01(amount); ((RectTransform)root.GetChild(0)).anchoredPosition = new Vector2(-24 * (1 - amount), 0); ((RectTransform)root.GetChild(1)).anchoredPosition = new Vector2(46 + 24 * (1 - amount), 0);
            root.GetChild(2).gameObject.SetActive(amount >= .98f);
        }
        // ---------- 13주차 발동 연출 2차 (2026-11-27 확정안) ----------
        // 시안: _도구/시안13주차/tabs/
        //   07_windup.js MIXW(1+5 합침) = 충전 1.3초: LONG.charge(꺾쇠가 갈 길 끝까지 · 알갱이 14개 흡입 · 고동 0.45/0.85/1.1초 · 핵 0.6 -> 1.05배 · 마지막 0.12초 흰색)
        //     + 시작 흰 섬광 0.1초(35%) + 화면 72% 어둡게(충전 서버마다 둥근 빛 r 130) + 눈금 8개 74 -> 48 px 조여듦, 1.2~1.4초에 밝기 복귀.
        //   09_impact.js planU('all') = 08_clash.js MIXC + 통일 규칙: ① 쏘는 탄의 길은 모두 밝게 남기고 82% 어둡게(발사 0.1초 전 ~ 마지막 사건 0.3초 뒤)
        //     ② 파동(흰 고리 20 -> 300 · 금 고리 10 -> 200 · 가로 균열 ±480, 0.5초)은 힘겨루기 승부마다, 가장 큰 사건이 착탄이면 그 착탄에도
        //     ③ 카메라는 가장 큰 사건 하나만(힘겨루기 = 두 위력 합 + 0.5, 착탄 = 피해). 같으면 무작위. ④ 힘겨루기 1.4초 + 빛 가루 24개(DARK.clash).
        //   12_zeroday.js B(core) = 제로데이 몫(Packet.Zero)은 빔 속 금색 심(2.5 px)으로 같이 날아가 접점을 뚫고(옆으로 금색 불꽃) 그대로 착탄. 방화벽에는 막힘.
        // 시안의 직선 거리 d 는 BattleController.Points 폴리라인 위 길이로 바꿔 쓴다(가로선을 타면 빔·꺾쇠·어둡게 구멍도 꺾인다).
        const float Charge = 1.3f, Reach = .5f, Snap = .25f, Extend = .2f, Fade = .35f, Struggle = 1.4f;
        // 검사·다른 연출이 읽는 시간 값
        public const float ChargeSeconds = Charge, ReachSeconds = Reach, StruggleSeconds = Struggle;
        static readonly float[] Pumps = { .15f, .45f, .75f };
        static readonly float[] Beats = { .45f, .85f, 1.1f };
        const float Mid = 489; static readonly Vector2 ViewCenter = new Vector2(960, 540);
        static readonly Color Gold = new Color(1, 196 / 255f, 56 / 255f), White = Color.white, CoreFill = new Color(232 / 255f, 1, 1), CoreInk = new Color(4 / 255f, 13 / 255f, 18 / 255f);
        static readonly Color ZeroGold = new Color(1, 214 / 255f, 90 / 255f);
        // 발사 직전 "불 붙는" 연출을 붙일 자리(아직 그림 없음). 충전이 시작될 때 쏘는 탄마다 한 번: 길 점들(무대 좌표, y 아래), 쏘는 쪽(0 나 / 1 상대), 충전 길이(초)
        public static Action<List<Vector2>, int, float> OnFireRoute;
        static Color Ink(int owner) { return owner == 0 ? new Color(56 / 255f, 217 / 255f, 1) : new Color(1, 59 / 255f, 87 / 255f); }
        static float EOut(float t, float p = 3) { return 1 - Mathf.Pow(1 - Mathf.Clamp01(t), p); }
        static float EIn(float t, float p = 2) { return Mathf.Pow(Mathf.Clamp01(t), p); }
        // easeOutBack (04_beam.js back)
        static float Back(float k) { return k <= 0 ? 0 : 1 + 2.70158f * Mathf.Pow(k - 1, 3) + 1.70158f * Mathf.Pow(k - 1, 2); }
        // 02_shot.js rnd (Math.imul 해시)
        static float Rnd(int n) { unchecked { uint u = (uint)n; u = (u ^ (u >> 16)) * 0x45d9f3bu; u = (u ^ (u >> 16)) * 0x45d9f3bu; u ^= u >> 16; return (float)(u / 4294967296.0); } }
        static Color A(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }
        static Color Mix(Color a, Color b, float k) { var m = Color.Lerp(a, b, k); m.a = 1; return m; }
        // 빔 굵기 = 3 + 위력 x 2.2
        static float Width(int power) { return 3 + power * 2.2f; }
        public float ChargeTime { get; private set; }
        public float FlightTime { get; private set; }
        // 첫 정면 힘겨루기의 맞닿는 시각(발사 기준 초). 없으면 -1
        public float ClashContact { get; private set; } = -1;
        sealed class Shot
        {
            public Packet packet; public List<Vector2> path; public float split = 1; public Vector2 contact; public bool contactEvent, triggered, arrived, hits, coreArrived, corePassed;
            public float length, contactTime, head; public Vector2 position; public Clash clash; public TMP_Text number;
            public int Owner { get { return packet.Owner; } }
            // 제로데이 몫(부딪히지 않고 지나감)과 일반 몫(장전 기준)
            public int Zero { get { return Mathf.Clamp(packet.Zero, 0, packet.Initial); } }
            public int Normal { get { return packet.Initial - Zero; } }
            // 상쇄 뒤 남은 일반 몫. 방화벽은 제로데이까지 전부 막는다
            public int NormalLeft { get { return packet.Blocked ? 0 : Mathf.Max(0, packet.Power - Zero); } }
            public bool HasBeam { get { return Normal > 0; } }
            // 접점에서 일반 몫이 0이 되어 되감기는 빔(차단 포함)
            public bool Stops { get { return contactEvent && (packet.Blocked || NormalLeft == 0); } }
        }
        sealed class Clash
        {
            public readonly Shot[] by = new Shot[2]; public readonly float[] meet = new float[2]; public readonly int[] power = new int[2]; public readonly TMP_Text[] labels = new TMP_Text[2];
            public int win, rem, pumps; public float c0, c1; public bool started, broke; public Vector2 meetPoint, endPoint;
        }
        // 카메라·파동이 고르는 사건: 힘겨루기 승부(clash != null) 또는 착탄
        sealed class Moment { public float t, w; public Vector2 at; public Clash clash; public bool alive = true; }
        static float Fraction(List<Vector2> path, Vector2 point) { float total = 0, done = 0; for (int j = 1; j < path.Count; j++) total += Vector2.Distance(path[j - 1], path[j]); for (int j = 1; j < path.Count; j++) { float len = Vector2.Distance(path[j - 1], path[j]); if (Mathf.Abs(Vector2.Distance(path[j - 1], point) + Vector2.Distance(point, path[j]) - len) < .1f) return (done + Vector2.Distance(path[j - 1], point)) / Mathf.Max(1, total); done += len; } return 1; }
        TMP_Text MakeText(string name, Transform parent, bool left)
        {
            var t = Ui.Text(name, parent, 0, 0, 60, 40, "", c.font, 20, Color.clear); t.textWrappingMode = TextWrappingModes.NoWrap;
            t.alignment = left ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Midline; t.rectTransform.pivot = new Vector2(left ? 0 : .5f, .5f); return t;
        }
        Shot Make(Packet packet, Resolution report, Transform parent)
        {
            var s = new Shot { packet = packet, path = BattleController.Points(packet) }; s.contact = s.path.Last();
            if (packet.Blocked)
            {
                int receiver=1-packet.Owner;
                s.contact=FirewallPoint(receiver,packet.End)+new Vector2(0,receiver==0?-26.4f:26.4f);
                s.contactEvent=true;
            }
            else if (s.Normal > 0)
            {
                // 일반 몫끼리 상쇄된 상대만 접점이 된다(제로데이 몫은 상쇄하지 않는다)
                var opponent = report.Packets.FirstOrDefault(p => p.Owner != packet.Owner && !p.Blocked && p.Initial - Mathf.Clamp(p.Zero, 0, p.Initial) > 0 && (packet.Power < packet.Initial || p.Power < p.Initial) && packet.Cells.Any(a => p.Cells.Any(b => a.Lane == b.Lane && a.Row == b.Row)));
                if (opponent != null) { var cell = packet.Cells.Where(a => opponent.Cells.Any(b => a.Lane == b.Lane && a.Row == b.Row)).OrderBy(a => Math.Abs(a.Row - 1)).ThenBy(a => a.Lane).First(); s.contact = new Vector2(BattleController.LaneX[cell.Lane], cell.Row < 0 ? 201 : cell.Row > 2 ? 777 : BattleController.RowY[cell.Row]); s.contactEvent = true; }
            }
            s.length = Mathf.Max(1, PathMath.Length(s.path));
            if (s.contactEvent) s.split = Fraction(s.path, s.contact);
            // 머리 거리 d = L (u / F)^2 이므로 접점(비율 split)에 닿는 시각은 F sqrt(split)
            s.contactTime = s.contactEvent ? Reach * Mathf.Sqrt(s.split) : Reach;
            s.number = MakeText("핵 위 위력 - " + packet.Owner + " CH " + packet.Start, parent, false);
            return s;
        }
        static bool Touches(Shot a, Shot b)
        {
            return a.packet.Cells.Any(x => b.packet.Cells.Any(y => x.Lane == y.Lane && x.Row == y.Row));
        }
        List<Clash> CreateClashes(List<Shot> shots, Transform parent)
        {
            var clashes = new List<Clash>();
            foreach (var a in shots)
            {
                if (a.clash != null || a.packet.Blocked || !a.contactEvent || !a.HasBeam) continue;
                // Count every launched, unblocked opponent with a normal share (zero-day shares never collide), including ones cancelled to zero.
                var opponents = shots.Where(candidate => candidate.Owner != a.Owner && !candidate.packet.Blocked && candidate.HasBeam && Touches(a, candidate)).ToArray();
                if (opponents.Length != 1) continue;
                var b = opponents[0];
                if (b.clash != null || !b.contactEvent) continue;
                if (shots.Count(other => other.Owner != b.Owner && !other.packet.Blocked && other.HasBeam && Touches(b, other)) != 1) continue;
                // A partial crossing or a sequential many-to-many cancellation must keep the existing motion.
                if (a.path.Count != b.path.Count) continue;
                bool reversed = true;
                for (int i = 0; i < a.path.Count; i++)
                    if ((a.path[i] - b.path[b.path.Count - 1 - i]).sqrMagnitude > .01f) { reversed = false; break; }
                if (!reversed || a.NormalLeft != Mathf.Max(0, a.Normal - b.Normal) || b.NormalLeft != Mathf.Max(0, b.Normal - a.Normal)) continue;
                var clash = new Clash(); clash.by[a.Owner] = a; clash.by[b.Owner] = b;
                for (int o = 0; o < 2; o++) clash.power[o] = clash.by[o].Normal;
                int p0 = clash.power[0], p1 = clash.power[1];
                clash.win = p0 > p1 ? 0 : p1 > p0 ? 1 : -1; clash.rem = Mathf.Abs(p0 - p1);
                for (int o = 0; o < 2; o++) clash.meet[o] = clash.by[o].split * clash.by[o].length;
                // 두 빔이 접점에서 맞닿는 시각 = 늦게 닿는 쪽의 F sqrt(split) (시안: 가운데라 F sqrt(.5))
                clash.c0 = Reach * Mathf.Sqrt(Mathf.Max(clash.by[0].split, clash.by[1].split)); clash.c1 = clash.c0 + Struggle;
                clash.meetPoint = PathMath.At(clash.by[0].path, clash.meet[0]);
                float dyEnd = Dy(clash, 1, Charge + clash.c1); clash.endPoint = PathMath.At(clash.by[1].path, ReachOf(clash, 1, dyEnd));
                for (int o = 0; o < 2; o++) clash.labels[o] = MakeText("힘겨루기 위력 " + o, parent, true);
                a.clash = b.clash = clash;
                clashes.Add(clash);
            }
            return clashes;
        }
        // ---------- 그리기 도구 (02_shot.js) ----------
        static void RingA(FxPainter g, Vector2 p, Color col, float r, float a, float w) { if (a <= 0) return; g.Ring(p, Mathf.Max(.1f, r), w, A(col, a)); }
        static void Core(FxPainter g, TMP_Text text, Vector2 p, int power, float a, float s, bool whiteFill)
        {
            if (a <= 0) return;
            g.FillRoundRect(p.x - 10 * s, p.y - 13 * s, 20 * s, 26 * s, 10 * s, A(whiteFill ? White : CoreFill, a));
            if (power < 0 || !text) return;
            string label = power.ToString(); if (text.text != label) text.text = label;
            text.fontSize = 20 * s; text.rectTransform.anchoredPosition = new Vector2(p.x, -(p.y + s)); text.color = A(CoreInk, a);
        }
        static void Shards(FxPainter g, Vector2 p, Color col, float k, int n, float dist, int seed, Vector2 bias, float size = 4)
        {
            if (k <= 0 || k >= 1) return; float e = EOut(k, 3); bool biased = bias != Vector2.zero; float baseAngle = biased ? Mathf.Atan2(bias.y, bias.x) : 0;
            for (int i = 0; i < n; i++)
            {
                float ang = (float)i / n * Mathf.PI * 2 + Rnd(seed * 31 + i) * .6f; if (biased) ang = baseAngle + (Rnd(seed * 17 + i) - .5f) * 2.2f;
                float r = 10 + dist * e * (.6f + .4f * Rnd(seed * 7 + i)), s = size * (1 - k * .5f);
                g.FillRect(p.x + Mathf.Cos(ang) * r - s / 2, p.y + Mathf.Sin(ang) * r - s / 4, s, s / 2, A(i % 3 == 0 ? White : col, 1 - k));
            }
        }
        static void Zig(FxPainter g, Vector2 p0, Vector2 p1, int seed, float amp, int n, Color col, float w, float a)
        {
            var pts = new List<Vector2> { p0 }; Vector2 d = p1 - p0; float L = d.magnitude; if (L <= 0) L = 1; var nv = new Vector2(-d.y / L, d.x / L);
            for (int i = 1; i < n; i++) { float f = (float)i / n, o = (Rnd(seed * 97 + i) - .5f) * 2 * amp * Mathf.Sin(f * Mathf.PI); pts.Add(p0 + d * f + nv * o); }
            pts.Add(p1);
            g.Stroke(pts, w * 4, A(col, a * .3f)); g.Stroke(pts, w, A(Mix(col, White, .6f), a));
        }
        // 03_spawn.js dashes: 진행 방향으로 흐르는 꺾쇠(∧) 18 px 간격, 70 px/s, 굵기 2.5, 진영 색과 흰색 반반. 폴리라인을 따라 꺾인다
        static void Dashes(FxPainter g, Shot s, float from, float to, Color col, float a, float T)
        {
            to = Mathf.Min(to, s.length); if (a <= 0 || to <= from) return; var ink = A(Mix(col, White, .5f), a); float off = (T * 70) % 18;
            for (float d = from + off; d < to; d += 18)
            {
                Vector2 p = PathMath.At(s.path, d), dir = PathMath.Dir(s.path, d), side = new Vector2(-dir.y, dir.x);
                g.Stroke(new[] { p - dir * 6 - side * 8, p, p - dir * 6 + side * 8 }, 2.5f, ink);
            }
        }
        // 04_beam.js beam: 바깥 번짐(굵기 x 3.2, .22) + 본선(.85) + 흰 심(굵기 x .3), 둥근 끝
        static void Beam(FxPainter g, Shot s, float d0, float d1, float w, float a)
        {
            if (a <= 0 || d1 <= d0) return; var pts = PathMath.Slice(s.path, d0, d1); if (pts.Count < 2) return; var col = Ink(s.Owner);
            g.Stroke(pts, w * 3.2f, A(col, .22f * a), true); g.Stroke(pts, w, A(col, .85f * a), true); g.Stroke(pts, Mathf.Max(1.2f, w * .3f), A(Mix(col, White, .75f), a), true);
        }
        static void Head(FxPainter g, Shot s, float d, int power, float a)
        {
            var p = PathMath.At(s.path, d); g.Flare(p, Ink(s.Owner), .75f, a); Core(g, s.number, p, power, a, .85f, false);
        }
        // 04_beam.js launch: 흰 섬광 0.06초 + 고리 14 -> 54 px 0.25초
        static void Launch(FxPainter g, Shot s, float u)
        {
            Vector2 p = s.path[0]; if (u < .06f) g.Glow(p, 46, White, 1 - u / .06f);
            if (u < .25f) RingA(g, p, Mix(Ink(s.Owner), White, .5f), Mathf.Lerp(14, 54, EOut(u / .25f)), 1 - u / .25f, 2.5f);
        }
        // 07_windup.js MIXW.charge = LONG.charge + 눈금 고리. T = 충전 시작부터 초(0 ~ 1.3)
        static void ChargeShot(FxPainter g, Shot s, float T)
        {
            var col = Ink(s.Owner); Vector2 p = s.path[0];
            // 꺾쇠: 0.1초부터 0.5초에 갈 길 끝까지(단독 = 상대 서버, 맞붙음·상쇄·차단 = 접점 +18 px, 시안 HALF - 10 + 28)
            float limit = s.contactEvent ? Mathf.Min(s.length, s.split * s.length + 18) : s.length;
            Dashes(g, s, 28, 28 + Mathf.Max(0, limit - 28) * EOut(Mathf.Clamp01((T - .1f) / .5f), 3), col, .75f, T);
            // 빛 알갱이 14개가 점점 빨리 빨려 듦
            if (T >= .1f) for (int i = 0; i < 14; i++) { float k = Mathf.Repeat(T * (1 + T) * .9f + i / 14f, 1), a = i * 2.4f, r = 90 * (1 - EIn(k, 2)); g.FillRect(p.x + Mathf.Cos(a) * r - 1.5f, p.y + Mathf.Sin(a) * r * .7f - 1.5f, 3, 3, A(Mix(col, White, .6f), .9f * k)); }
            float beat = 0; for (int n = 0; n < Beats.Length; n++) beat = Mathf.Max(beat, (1 - Mathf.Abs(T - Beats[n]) / .07f) * (.5f + .25f * n));
            float grow = Smooth(T / 1.2f), w = T > 1.18f ? (T - 1.18f) / .12f : 0;
            g.Glow(p, 24 + 40 * grow + 30 * beat, col, .3f + .4f * grow + .4f * beat); g.Flare(p, col, .5f + .5f * grow + .3f * beat, .4f + .5f * grow);
            float scale = .6f + .45f * grow + .2f * beat;
            if (s.HasBeam) Core(g, s.number, p, T > .2f ? s.packet.Initial : -1, Mathf.Clamp01(T / .15f), scale, w > 0);
            else SoloZero(g, s, p, T); // 제로데이만 장전(12_zeroday.js pre solo): 금색 마름모 핵
            if (w > 0) g.Glow(p, 40 + 30 * w, White, Mathf.Clamp01(w));
            // MIXW: 서버 둘레 고리 74 -> 48 px, 눈금 8개(84 -> 54 px, 길이 16)가 점점 빨리 돌며 조여듦
            if (T < 1.2f)
            {
                float k = Smooth(T / 1.2f), a = .5f * Smooth(T / .2f); RingA(g, p, Mix(col, White, .4f), 74 - 26 * k, a, 1.5f);
                for (int i = 0; i < 8; i++) { float an = i / 8f * Mathf.PI * 2 + T * (1.2f + 2 * k), r0 = 84 - 30 * k; var dir = new Vector2(Mathf.Cos(an), Mathf.Sin(an)); g.Line(p + dir * r0, p + dir * (r0 + 16), 2, A(col, a)); }
            }
            // 12_zeroday.js pre: 핵 옆(+30 px, 나아가는 쪽 4 px) 금색 작은 마름모 = 제로데이 몫
            if (s.Zero > 0 && s.HasBeam) { float b = .6f + .4f * Mathf.Sin(T * 9); var q = p + new Vector2(30, (s.Owner == 0 ? -1 : 1) * 4); g.Glow(q, 18, ZeroGold, .7f * b); g.FillDiamond(q, 6, A(ZeroGold, .95f)); }
        }
        static void SoloZero(FxPainter g, Shot s, Vector2 p, float T)
        {
            float b = .6f + .4f * Mathf.Sin(T * 9); g.Glow(p, 30 + 10 * b, ZeroGold, .6f); g.FillDiamond(p, 11, A(ZeroGold, Mathf.Clamp01(T / .15f)));
            if (!s.number) return; string label = s.Zero.ToString(); if (s.number.text != label) s.number.text = label;
            s.number.fontSize = 18; s.number.rectTransform.anchoredPosition = new Vector2(p.x, -(p.y + 1)); s.number.color = new Color(30 / 255f, 20 / 255f, 0, Mathf.Clamp01(T / .15f));
        }
        // 착탄 (02_shot.js serverHit): 흰 고리 26 -> 70 · 맞은 쪽 색 고리 26 -> 50(0.08초 늦게)은 기존 EventFx 착탄 고리가 같은 값으로 그린다.
        // 여기서는 빛과 파편 12만
        static void Hit(FxPainter g, Shot s, float k)
        {
            if (k < 0 || k > 1 || !s.hits) return; Vector2 p = s.path[s.path.Count - 1]; var col = Ink(s.Owner);
            g.Glow(p, 70 * (1 - k * .4f), col, (1 - k) * .7f); Shards(g, p, col, k, 12, 60, 2, Vector2.zero);
        }
        // 이기는 쪽 빔을 타고 서버 -> 접점으로 오르는 빛 맥박 (흰 네모 + 빛)
        static void Pulse(FxPainter g, Shot s, float reach, float k, float w, float big)
        {
            if (k < 0 || k > 1) return; float d = reach * EIn(k, 1.5f); Vector2 p = PathMath.At(s.path, d), dir = PathMath.Dir(s.path, d);
            g.Glow(p, 22 * big, Mix(Ink(s.Owner), White, .5f), .9f); g.Line(p - dir * 6, p + dir * 6, w + 4, A(White, .9f));
        }
        // 09_impact.js wave: 흰 고리 20 -> 300(굵기 5) + 금 고리 10 -> 200(굵기 3, .8) + 양옆 가로 균열선 20 -> 500 px(8마디, ±7 px), 0.5초
        static void Wave(FxPainter g, Vector2 at, float d)
        {
            if (d < 0 || d > .5f) return; float k = d / .5f;
            RingA(g, at, White, Mathf.Lerp(20, 300, EOut(k)), 1 - k, 5); RingA(g, at, Gold, Mathf.Lerp(10, 200, EOut(k)), (1 - k) * .8f, 3);
            for (int s = -1; s <= 1; s += 2)
            {
                float L = 480 * EOut(k, 2); var pts = new List<Vector2> { new Vector2(at.x + s * 20, at.y) };
                for (int i = 1; i <= 8; i++) pts.Add(new Vector2(at.x + s * (20 + L * i / 8), at.y + (Rnd(i * 9 + (s > 0 ? 1 : 0)) - .5f) * 14));
                g.Stroke(pts, 2, A(Mix(Gold, White, .5f), (1 - k) * .9f));
            }
        }
        // A안 접점 이동(owner 0 서버 쪽이 +): 펌프 3번 easeOutBack(0.16초)로 진 쪽으로 15 x 위력 차 px, 같으면 sin(u 3 pi) x 10 왕복, 떨림 sin(41 T) x 1.8
        static float Dy(Clash k, float u, float T)
        {
            float y;
            if (k.win < 0) y = Mathf.Sin(u * Mathf.PI * 3) * 10;
            else { float o = 0; foreach (var p in Pumps) o += 15 * k.rem * Back(Mathf.Clamp01((u - p) / .16f)); y = (k.win == 0 ? -1 : 1) * o; }
            return y + Mathf.Sin(T * 41) * 1.8f;
        }
        static float ReachOf(Clash k, int o, float dy) { return Mathf.Clamp(o == 1 ? k.meet[1] + dy : k.meet[0] - dy, 0, k.by[o].length); }
        int[,] dealt = new int[2, 3];
        // 착탄: 서버 숫자는 지금까지 들어간 피해만큼(최종 체력 아래로는 내리지 않는다). zero = 제로데이 금색 착탄(고리는 DrawCore 가 그린다)
        void Arrive(Shot s, int damage, bool zero, bool label, Transform root, Transform lightRoot, Resolution report)
        {
            int target = 1 - s.Owner, end = s.packet.End;
            if (report.Before[target, end] <= 0) return;
            dealt[target, end] += damage; int shown = Mathf.Max(c.State.Health[target, end], report.Before[target, end] - dealt[target, end]);
            if (!zero) { s.hits = true; var hitLight = new EventFx.Plan(); hitLight.Rings.Add((s.path.Last(), Color.white, 26, 70, 0)); hitLight.Rings.Add((s.path.Last(), EventFx.Side(target), 26, 50, .08f)); StartCoroutine(EventFx.Run(lightRoot, hitLight, () => Delta, 1)); }
            if (label) StartCoroutine(Impact(root, s.path.Last(), shown == 0 ? "서버 소실" : "착탄", zero ? ZeroGold : s.Owner == 0 ? Ui.Cyan : Ui.Red, false, true));
            c.servers.First(v => v.owner == target && v.channel == end).Render(shown, 0);
        }
        // 정면 힘겨루기가 아닌 탄의 일반 몫: 단독, 차단, 부분 교차·다대다 상쇄
        bool DrawFree(FxPainter g, Shot s, float e, float T, Transform root, Transform lightRoot, Resolution report)
        {
            float L = s.length; Launch(g, s, e);
            if (!s.HasBeam) return false; // 제로데이만: DrawCore 가 그린다
            if (s.contactEvent && !s.triggered && e >= s.contactTime)
            {
                s.triggered = true;
                if (!s.packet.Blocked) { var clashLight = new EventFx.Plan(); clashLight.Rings.Add((s.contact, Color.white, 8 * EventFx.S, 60 * EventFx.S, 0)); StartCoroutine(EventFx.Run(lightRoot, clashLight, () => Delta, 1)); }
                if (s.packet.Blocked) { BlockContactCount++; LastBlockContact = s.contact; }
                StartCoroutine(Impact(root, s.contact, s.packet.Blocked ? "서버 앞 차단" : "공격 상쇄", Ui.Gold, s.packet.Blocked && s.contact.y < 487));
            }
            int power = s.triggered ? s.NormalLeft : s.Normal;
            if (s.Stops && s.triggered)
            {
                // 일반 몫 0: 진 빔처럼 0.25초 eIn^2 로 자기 서버로 되감김
                float r = e - s.contactTime, k = EIn(r / Snap, 2), reach = s.split * L;
                Beam(g, s, 0, reach * (1 - k), Width(s.Normal), 1 - k * .5f);
                s.head = reach * (1 - k); s.position = PathMath.At(s.path, s.head); return r < Snap;
            }
            if (e < Reach)
            {
                float d = L * EIn(e / Reach, 2); if (s.Stops) d = Mathf.Min(d, s.split * L);
                Dashes(g, s, 28 + d, s.Stops ? s.split * L - 10 : 288, Ink(s.Owner), .7f * (1 - Mathf.Clamp01(e / Reach)), T);
                Beam(g, s, 0, d, Width(power), 1); Head(g, s, d, power, 1);
                s.head = d; s.position = PathMath.At(s.path, d); return true;
            }
            if (!s.arrived) { s.arrived = true; if (s.NormalLeft > 0) Arrive(s, s.NormalLeft, false, true, root, lightRoot, report); }
            float u = e - Reach;
            Beam(g, s, u < .25f ? 0 : L * EIn((u - .25f) / Fade), L, Width(power), 1 - Mathf.Clamp01((u - .25f) / Fade) * .6f);
            Hit(g, s, u / .45f);
            s.head = L; s.position = s.path[s.path.Count - 1]; return u < .25f + Fade;
        }
        // 12_zeroday.js B(core): 제로데이 몫 = 빔 속 금색 심선 2.5 px(255,214,90). 일반 빔과 같은 가속(L (u/F)^2)으로 날아가 접점을 뚫고(±40 px 안에서
        // 옆으로 금색 불꽃 10개 · 빛 46) 그대로 착탄(금색 고리 20 -> 64 · 빛 60 · 파편 10). 방화벽에서는 멈추고 작은 금색 고리 10 -> 40.
        // 시안은 심을 힘겨루기 빔 아래에 그려 빔 속에서는 거의 가려진다. 여기서는 심이 보이도록 빔 위에 그린다.
        void DrawCore(FxPainter g, Shot s, float e, float T, Transform root, Transform lightRoot, Resolution report)
        {
            if (s.Zero <= 0) return; float L = s.length; bool blocked = s.packet.Blocked; float R = blocked ? s.split * L : L, cross = s.split * L;
            float d = Mathf.Min(R, L * EIn(e / Reach, 2)), fade = e < Reach + .25f ? 1 : 1 - Mathf.Clamp01((e - Reach - .25f) / .35f);
            if (fade > 0 && d > 0) { var pts = PathMath.Slice(s.path, 0, d); if (pts.Count >= 2) g.Stroke(pts, 2.5f, A(ZeroGold, .95f * fade)); }
            if (d < R) { Vector2 q = PathMath.At(s.path, d), dir = PathMath.Dir(s.path, d); g.Glow(q, 22, ZeroGold, 1); g.Line(q - dir * 7, q + dir * 7, 6, White); }
            if (s.contactEvent && !blocked)
            {
                float pk = 1 - Mathf.Abs(d - cross) / 40;
                if (pk > 0 && e < Reach)
                {
                    Vector2 q = PathMath.At(s.path, d), dir = PathMath.Dir(s.path, d), side = new Vector2(-dir.y, dir.x); int f30 = Mathf.FloorToInt(T * 30);
                    for (int i = 0; i < 10; i++) { float sg = i % 2 == 1 ? 1 : -1, r = 14 + Rnd(i * 7 + f30) * 30 * (1 - pk + .3f); var at = q + side * sg * r + dir * ((Rnd(i * 3) - .5f) * 14); g.FillRect(at.x - 2, at.y - 1, 4, 2, A(i % 3 != 0 ? ZeroGold : White, pk)); }
                    g.Glow(q, 46, ZeroGold, pk);
                }
                if (!s.corePassed && d >= cross) { s.corePassed = true; StartCoroutine(Impact(root, s.contact, "제로데이 통과", ZeroGold, true, true)); }
            }
            if (!blocked)
            {
                if (!s.coreArrived && e >= Reach) { s.coreArrived = true; bool mainLands = s.clash == null && s.HasBeam && !s.Stops && s.NormalLeft > 0; Arrive(s, s.Zero, true, !mainLands, root, lightRoot, report); }
                float k = (e - Reach) / .45f; Vector2 p = s.path[s.path.Count - 1];
                if (k > 0 && k < 1 && report.Before[1 - s.Owner, s.packet.End] > 0) { RingA(g, p, ZeroGold, Mathf.Lerp(20, 64, EOut(k)), 1 - k, 3); g.Glow(p, 60 * (1 - k * .4f), ZeroGold, (1 - k) * .8f); Shards(g, p, ZeroGold, k, 10, 50, 5, Vector2.zero, 4); }
            }
            else
            {
                float r = e - Mathf.Sqrt(R / L) * Reach; if (r >= 0 && r <= .4f) RingA(g, PathMath.At(s.path, R), ZeroGold, Mathf.Lerp(10, 40, EOut(r / .4f)), 1 - r / .4f, 2.5f);
            }
        }
        Vector2 DrawClash(FxPainter g, Clash k, float e, float T, Transform root, Transform lightRoot, Resolution report)
        {
            for (int o = 0; o < 2; o++) Launch(g, k.by[o], e);
            if (e < k.c0)
            {
                Vector2 sum = Vector2.zero;
                for (int o = 0; o < 2; o++)
                {
                    var s = k.by[o]; float d = Mathf.Min(k.meet[o], s.length * EIn(e / Reach, 2));
                    Dashes(g, s, 28 + d, k.meet[o] - 10, Ink(o), .7f * (1 - Mathf.Clamp01(e / Reach)), T);
                    Beam(g, s, 0, d, Width(k.power[o]), 1); Head(g, s, d, k.power[o], 1);
                    s.head = d; s.position = PathMath.At(s.path, d); sum += s.position;
                }
                return sum / 2;
            }
            if (!k.started) k.started = true;
            if (e < k.c1)
            {
                float u = (e - k.c0) / Struggle, dy = Dy(k, u, T); var reach = new[] { ReachOf(k, 0, dy), ReachOf(k, 1, dy) };
                Vector2 cp = PathMath.At(k.by[1].path, reach[1]);
                for (int o = 0; o < 2; o++) { Beam(g, k.by[o], 0, reach[o], Width(k.power[o]), 1); k.by[o].head = reach[o]; k.by[o].position = cp; }
                // 접점 구슬
                g.Glow(cp, 40, Gold, .85f); g.Glow(cp, 16, White, .9f); RingA(g, cp, Gold, 15 + 3 * Mathf.Sin(T * 30), .8f, 2);
                // 펌프 0.15초 전에 맥박이 서버 -> 접점으로 올라옴(같으면 양쪽에서 .8배)
                foreach (var p in Pumps)
                {
                    float m = (u - (p - .15f)) / .15f;
                    if (k.win >= 0) Pulse(g, k.by[k.win], reach[k.win], m, Width(k.power[k.win]), 1);
                    else for (int o = 0; o < 2; o++) Pulse(g, k.by[o], reach[o], m, Width(k.power[o]), .8f);
                }
                float pu = 0; foreach (var p in Pumps) pu = Mathf.Max(pu, 1 - Mathf.Abs(u - p) / .08f); g.Glow(cp, 30 * pu, Gold, .6f * pu);
                int f24 = Mathf.FloorToInt(T * 24);
                for (int i = 0; i < 5; i++) { float a = i / 5f * Mathf.PI * 2 + T * 9, r = 18 + Rnd(i + f24) * 14; Zig(g, cp, cp + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * .5f), f24 * 5 + i, 3, 3, Gold, 1, .7f); }
                // 08_clash.js DARK.clash: 접점 둘레를 떠도는 빛 가루 24개(금 2 : 흰 1, 3 px)
                for (int i = 0; i < 24; i++) { float a = Rnd(i * 13) * 6.28f + T * (.6f + Rnd(i) * .8f), r = 40 + Rnd(i * 7) * 90; g.FillRect(cp.x + Mathf.Cos(a) * r * .6f - 1.5f, cp.y + Mathf.Sin(a) * r * .9f - 1.5f, 3, 3, A(i % 3 != 0 ? Gold : White, .25f + .5f * Mathf.Abs(Mathf.Sin(T * 3 + i)))); }
                // 접점 옆 양쪽 위력 (+18 px, 내 것 +26 · 상대 것 -26)
                for (int o = 0; o < 2; o++) { var t = k.labels[o]; string label = k.power[o].ToString(); if (t.text != label) t.text = label; t.rectTransform.anchoredPosition = new Vector2(cp.x + 18, -(cp.y + (o == 0 ? 26 : -26))); t.color = Ink(o); }
                while (k.pumps < Pumps.Length && u >= Pumps[k.pumps]) k.pumps++;
                return cp;
            }
            float rr = e - k.c1, dyEnd = Dy(k, 1, Charge + k.c1); var rch = new[] { ReachOf(k, 0, dyEnd), ReachOf(k, 1, dyEnd) };
            Vector2 at = PathMath.At(k.by[1].path, rch[1]), focus = at;
            if (!k.broke) k.broke = true;
            for (int o = 0; o < 2; o++)
            {
                var s = k.by[o];
                if (k.win != o)
                {
                    // 진 쪽: 0.25초 eIn^2 로 자기 서버로 되감김 + 파편 10 (자기 서버 쪽으로)
                    float kk = EIn(rr / Snap, 2); Beam(g, s, 0, rch[o] * (1 - kk), Width(k.power[o]), 1 - kk * .5f);
                    if (rr < Snap) Shards(g, at, Ink(o), rr / Snap, 10, 46, 21 + o, -PathMath.Dir(s.path, rch[o]));
                    s.head = rch[o] * (1 - kk); s.position = PathMath.At(s.path, s.head);
                }
                else
                {
                    // 이긴 쪽: 0.2초 eIn^2 로 끝까지 뻗어 착탄, 0.25초 머문 뒤 0.35초에 서버 쪽부터 걷힘(알파 .4까지)
                    float L = s.length, d = rr < Extend ? Mathf.Lerp(rch[o], L, EIn(rr / Extend, 2)) : L, a = rr < Extend + .25f ? 1 : 1 - Mathf.Clamp01((rr - Extend - .25f) / Fade) * .6f;
                    Beam(g, s, rr < Extend + .25f ? 0 : L * EIn((rr - Extend - .25f) / Fade), d, Width(k.rem), a);
                    if (rr < Extend) Head(g, s, d, k.rem, 1);
                    else { if (!s.arrived) { s.arrived = true; if (s.NormalLeft > 0) Arrive(s, s.NormalLeft, false, true, root, lightRoot, report); } Hit(g, s, (rr - Extend) / .45f); }
                    s.head = d; s.position = PathMath.At(s.path, d); focus = s.position;
                }
            }
            if (rr < .3f) { g.Glow(at, 80 * (1 - rr / .3f) + 20, k.win >= 0 ? Ink(k.win) : Gold, 1 - rr / .3f); RingA(g, at, k.win >= 0 ? White : Gold, Mathf.Lerp(16, 90, EOut(rr / .3f)), 1 - rr / .3f, 3); }
            return focus;
        }
        static float EndTime(Shot s)
        {
            float t;
            if (s.clash != null) t = s.clash.win == s.Owner ? s.clash.c1 + Extend + .25f + Fade : s.clash.c1 + Mathf.Max(Snap, .3f);
            else if (!s.HasBeam) t = 0;
            else if (s.Stops) t = s.contactTime + Snap;
            else t = Reach + .25f + Fade;
            // 제로데이 심: 0.25초 머문 뒤 0.35초에 걷힘, 착탄 고리 0.45초
            if (s.Zero > 0) t = Mathf.Max(t, Reach + .6f);
            return t;
        }
        // ---------- 카메라 (06_show.js mountShow · 08_clash.js ZOOM.cam · 09_impact.js planU) ----------
        // 시안 카메라 {z, x, y} = (x, y) 를 화면 가운데로 z 배. 여기서는 발동 기본 화면(BeginResolution 의 1.08배)에 덧씌운다:
        // 최종(p) = z B(p) + B(960, 540) - z B(cam), B = 기본 화면 변환. z = 1, cam = (960, 540) 이면 기본 화면 그대로.
        void FrameCam(float z, Vector2 cam, Vector2 shake)
        {
            SetPeripheral(1);
            const float zoom0 = 1.08f; Vector2 center = new Vector2(982, -487);
            Vector2 shift0 = center * (1 - zoom0) + Vector2.up * 22; shift0.y = Mathf.Clamp(shift0.y, 845 * zoom0 - 854, 129 * zoom0 - 74);
            Vector2 B(Vector2 p) => new Vector2(p.x, -p.y) * zoom0 + shift0;
            float zoom = zoom0 * z; Vector2 shift = shift0 * z + B(ViewCenter) - B(cam) * z + new Vector2(shake.x, -shake.y);
            for (int i = 0; i < visuals.Count; i++) if (visuals[i]) { visuals[i].anchoredPosition = origins[i] * zoom + shift; visuals[i].localScale = Vector3.one * zoom; }
            CameraZoom = zoom; CameraOffset = shift;
        }
        // camKeys: 키 사이를 부드럽게(기본 smoothstep, ease = eOut)
        static (float z, Vector2 p) Keys(float T, params (float t, float z, Vector2 p, bool easeOut)[] keys)
        {
            if (T <= keys[0].t) return (keys[0].z, keys[0].p);
            for (int n = 1; n < keys.Length; n++) if (T <= keys[n].t) { var a = keys[n - 1]; var b = keys[n]; float x = (T - a.t) / (b.t - a.t), e = b.easeOut ? EOut(x) : Smooth(x); return (Mathf.Lerp(a.z, b.z, e), Vector2.Lerp(a.p, b.p, e)); }
            var last = keys[keys.Length - 1]; return (last.z, last.p);
        }
        // 08_clash.js punch: 0.05초에 확 다가갔다 0.4초에 돌아옴
        static float Punch(float d) { return d < 0 ? 0 : d < .05f ? d / .05f : Mathf.Pow(Mathf.Max(0, 1 - (d - .05f) / .4f), 2); }
        static (float z, Vector2 p) Cam(Moment E, float e)
        {
            if (E == null) return (1, ViewCenter);
            (float z, Vector2 p) b;
            if (E.clash != null) { var k = E.clash; b = Keys(e, (k.c0 - .05f, 1, ViewCenter, false), (k.c0 + .25f, 1.7f, k.meetPoint, true), (k.c1, 1.78f, k.meetPoint, false), (k.c1 + .45f, 1, ViewCenter, false)); }
            else { var aim = new Vector2(E.at.x, Mathf.Lerp(Mid, E.at.y, .6f)); b = Keys(e, (E.t - .35f, 1, ViewCenter, false), (E.t, 1.35f, aim, true), (E.t + .25f, 1.35f, aim, false), (E.t + .7f, 1, ViewCenter, false)); }
            float p = Punch(e - E.t) * .22f; if (p <= 0) return b;
            return (b.z * (1 + p), Vector2.Lerp(b.p, E.at, p / .22f));
        }
        // mountShow 흔들림: a = amp (1 - k)^2, 세로 a sin(92 T + 7 t) · 가로 .35 a sin(71 T + 3 t). T·t 는 충전 시작부터 초
        static Vector2 Shake(List<(float t, float amp, float dur)> shakes, float T)
        {
            float sx = 0, sy = 0;
            foreach (var s in shakes) { float k = (T - s.t) / s.dur; if (k < 0 || k >= 1) continue; float a = s.amp * (1 - k) * (1 - k); sy += a * Mathf.Sin(T * 92 + s.t * 7); sx += a * .35f * Mathf.Sin(T * 71 + s.t * 3); }
            return new Vector2(sx, sy);
        }
        // 어둡게: 07_windup.js MIXW.dim(충전 서버마다 둥근 빛 r 130, 72%) · 09_impact.js planU.dim(쏘는 탄의 길, 82%)
        static float WindupDim(float T) { return .72f * Smooth(T / .15f) * (1 - Smooth((T - 1.2f) / .2f)); }
        static float FireDim(float e, float lastT) { return .82f * Smooth((e + .1f) / .25f) * (1 - Smooth((e - lastT - .3f) / .35f)); }
        bool Cancelled(int version, RectTransform root)
        {
            if (version == playbackVersion && root) return false;
            if (root) Destroy(root.gameObject);
            if (dimRoot) Destroy(dimRoot.gameObject);
            return true;
        }
        RectTransform dimRoot;
        public void Cancel()
        {
            playbackVersion++;
            StopAllCoroutines();
            if (layer) foreach (Transform child in layer) Destroy(child.gameObject);
            if (underServers) foreach (Transform child in underServers) Destroy(child.gameObject);
            ResetView(); Phase = "idle"; ClashPhase = "idle"; Progress = 0;
        }
        public IEnumerator Play(Resolution report)
        {
            int version = ++playbackVersion;
            ClashPhase = "idle"; ClashContact = -1; ChargeTime = FlightTime = 0; dealt = new int[2, 3];
            BlockContactCount=0;yield return BeginResolution();
            var root = Ui.Rect("공격 흐름", layer, 0, 0, 1920, 1080);
            var painter = FxPainter.Create("빔 연출", root); // 숫자 글자들이 이 위에 그려지도록 먼저 만든다
            var shots = report.Packets.Select(p => Make(p, report, root)).ToList();
            if (shots.Count == 0) { for(float t=0;t<1;t+=Delta/.45f){if(Cancelled(version,root))yield break;Frame(new Vector2(982,487),1-Smooth(t));yield return null;}Destroy(root.gameObject);ResetView();Phase="idle";yield break; }
            var clashes = CreateClashes(shots, root); if (clashes.Count > 0) ClashContact = clashes[0].c0;
            var texts = shots.Select(s => s.number).Concat(clashes.SelectMany(k => k.labels)).ToList();

            // 사건: 힘겨루기 승부(두 위력 합 + 0.5)와 착탄(피해). 가장 큰 것 하나에 카메라, 같으면 무작위
            var moments = new List<Moment>();
            foreach (var k in clashes)
            {
                moments.Add(new Moment { t = k.c1, w = k.power[0] + k.power[1] + .5f, at = k.endPoint, clash = k });
                if (k.win >= 0) { var s = k.by[k.win]; moments.Add(new Moment { t = k.c1 + Extend, w = k.rem, at = s.path.Last(), alive = report.Before[1 - s.Owner, s.packet.End] > 0 }); }
            }
            foreach (var s in shots)
            {
                if (s.clash == null && s.HasBeam && !s.Stops && s.NormalLeft > 0) moments.Add(new Moment { t = Reach, w = s.NormalLeft, at = s.path.Last(), alive = report.Before[1 - s.Owner, s.packet.End] > 0 });
                if (s.Zero > 0 && !s.packet.Blocked) moments.Add(new Moment { t = Reach, w = s.Zero, at = s.path.Last(), alive = report.Before[1 - s.Owner, s.packet.End] > 0 });
            }
            var live = moments.Where(m => m.alive).ToList(); Moment E = null;
            if (live.Count > 0) { float top = live.Max(m => m.w); var ties = live.Where(m => Mathf.Abs(m.w - top) < 1e-4f).ToList(); E = ties[UnityEngine.Random.Range(0, ties.Count)]; }
            float lastT = Mathf.Max(Reach, moments.Count > 0 ? moments.Max(m => m.t) : 0);
            // 파동: 힘겨루기 승부마다 + 가장 큰 사건이 착탄이면 그 착탄
            var waves = moments.Where(m => m.clash != null).ToList(); if (E != null && E.clash == null) waves.Add(E);
            // 흔들림(충전 시작 기준 초): 힘겨루기 맞닿음 3 · 승부 6 · 펌프 3(같으면 2), 착탄마다 8, 가장 큰 사건 12
            var shakes = new List<(float t, float amp, float dur)>();
            foreach (var k in clashes) { shakes.Add((Charge + k.c0, 3, .2f)); shakes.Add((Charge + k.c1, 6, .3f)); foreach (var p in Pumps) shakes.Add((Charge + k.c0 + p * Struggle, k.win >= 0 ? 3 : 2, .15f)); }
            foreach (var m in moments) if (m.clash == null && m.alive) shakes.Add((Charge + m.t, 8, .35f));
            if (E != null) shakes.Add((Charge + E.t, 12, .35f));

            // 어둡게(서버 아래, 판 좌표라 카메라를 따라간다)
            dimRoot = BoardEffectUnderServers("집중 어둡게"); dimRoot.SetAsFirstSibling(); var dimRef = dimRoot;
            var dim = FxDim.Create("어둡게 - 구멍", dimRoot);
            foreach (var s in shots)
            {
                dim.Layers[0].Spots.Add(new Vector3(s.path[0].x, s.path[0].y, 130));
                // 곧은 세로 길은 기둥(폭 120, 시안 cols), 꺾인 길은 길 모양(굵기 150 · 흐림 24, 시안 paths)
                if (s.path.All(q => Mathf.Abs(q.x - s.path[0].x) < .5f)) dim.Layers[1].Cols.Add(new Vector2(s.path[0].x, 120)); else dim.Layers[1].Paths.Add(s.path);
            }

            // 발사 직전 불 연출 자리
            if (OnFireRoute != null) foreach (var s in shots) { try { OnFireRoute(new List<Vector2>(s.path), s.Owner, Charge); } catch (Exception ex) { Debug.LogException(ex); } }
            Phase = "charge";
            for (float t = 0; t < Charge; t += Delta)
            {
                if (Cancelled(version, root)) yield break;
                Progress = t / Charge; ChargeTime = t;
                foreach (var x in texts) x.color = Color.clear;
                painter.Begin(); foreach (var s in shots) ChargeShot(painter, s, t);
                // 시작 흰 섬광 0.1초(35%)
                if (t < .1f) painter.FillRect(-400, -400, 2720, 1880, A(White, .35f * (1 - t / .1f)));
                painter.End();
                dim.Layers[0].Alpha = WindupDim(t); dim.Layers[1].Alpha = FireDim(t - Charge, lastT); dim.Refresh();
                FrameCam(1, ViewCenter, Vector2.zero); yield return null;
            }
            if (Cancelled(version, root)) yield break;
            foreach (var s in shots) c.servers.First(v => v.owner == s.packet.Owner && v.channel == s.packet.Start).Render(report.Before[s.packet.Owner, s.packet.Start], 0);
            // HTML tab 2: every launched path glows in its side's color; tab 6: a packet crossing a rung flashes it.
            var lights = new EventFx.Plan(); foreach (var s in shots) if (s.clash == null) lights.Halos.Add((s.path, EventFx.Side(s.packet.Owner), 0)); // a tug-of-war keeps its own effect visible
            var lightRoot = BoardEffectUnderServers("이벤트 빛 - 발동"); StartCoroutine(EventFx.Run(lightRoot, lights, () => Delta));
            var crossings = shots.Select(s => RungCrossings(s)).ToList(); var crossed = new HashSet<(int, int)>();
            Phase = "flight";
            float camEnd = E == null ? 0 : E.clash != null ? E.clash.c1 + .45f : E.t + .7f;
            float duration = Mathf.Max(shots.Max(s => EndTime(s)), lastT + .3f + .35f, camEnd, E != null ? E.t + .45f : 0, shakes.Count > 0 ? shakes.Max(s => s.t + s.dur) - Charge : 0);
            var first = clashes.Count > 0 ? clashes[0] : null;
            for (float elapsed = 0; ; elapsed = Mathf.Min(duration, elapsed + Delta))
            {
                if (Cancelled(version, root)) yield break;
                FlightTime = elapsed; float T = Charge + elapsed;
                float held = first != null ? Mathf.Clamp(elapsed - first.c0, 0, Struggle) : 0;
                Progress = Mathf.Clamp01((elapsed - held) / Reach);
                // hold = 맞닿은 뒤 첫 펌프 전, yield = 펌프 구간, break = 끝난 뒤
                ClashPhase = first == null || elapsed < first.c0 ? "idle" : elapsed < first.c0 + Pumps[0] * Struggle ? "hold" : elapsed < first.c1 ? "yield" : "break";
                foreach (var x in texts) x.color = Color.clear;
                painter.Begin();
                foreach (var s in shots) if (s.clash == null) DrawFree(painter, s, elapsed, T, root, lightRoot, report);
                foreach (var k in clashes) DrawClash(painter, k, elapsed, T, root, lightRoot, report);
                foreach (var s in shots) DrawCore(painter, s, elapsed, T, root, lightRoot, report);
                foreach (var m in waves) Wave(painter, m.at, elapsed - m.t);
                painter.End();
                for (int si = 0; si < shots.Count; si++) { var s = shots[si]; float p = s.head / s.length; foreach (var cross in crossings[si]) if (p >= cross.at && !crossed.Contains((si, cross.index)) && s.packet.Power > 0) { crossed.Add((si, cross.index)); StartCoroutine(BridgeDots.RungFlash(lightRoot, cross.a, cross.b, () => Delta)); } }
                if (dimRef) { dim.Layers[0].Alpha = WindupDim(T); dim.Layers[1].Alpha = FireDim(elapsed, lastT); dim.Refresh(); }
                var cam = Cam(E, elapsed); FrameCam(cam.z, cam.p, Shake(shakes, T)); yield return null; if (elapsed >= duration) break;
            }
            if (Cancelled(version, root)) yield break;
            ClashPhase = "idle";
            painter.Begin(); painter.End(); foreach (var x in texts) x.color = Color.clear;
            if (dimRoot) Destroy(dimRoot.gameObject);
            Phase = "return";
            for (float t = 0; t < 1; t += Delta / .45f) { if (Cancelled(version, root)) yield break; Progress = t; Frame(new Vector2(982, 487), 1 - Smooth(t)); yield return null; }
            if (Cancelled(version, root)) yield break;
            ResetView(); Destroy(root.gameObject); Phase = "idle"; ClashPhase = "idle"; Progress = 0;
        }
        // labelOnly: 착탄 글자만(빛·파편은 빔 착탄 그림이 맡는다)
        IEnumerator Impact(Transform parent, Vector2 p, string caption, Color ink, bool labelBelow = false, bool labelOnly = false)
        {
            var root = Ui.Rect("충돌 결과", parent, p.x, p.y, 1, 1); var glow = labelOnly ? null : Flare(root, "충돌광", Vector2.zero, ink, 1.15f); var bits = new List<Shape>(); if (!labelOnly) for (int n = 0; n < 12; n++) bits.Add(Ui.Box("회로 파편", root, 0, 0, 4, 2, ink)); var label = Ui.Text("결과", root, -90, labelBelow ? 24 : -50, 180, 28, caption, c.font, 18, ink); label.alignment = TextAlignmentOptions.Center;
            for (float t = 0; t < 1; t += Delta / .36f) { if (!root) yield break; float e = 1 - Mathf.Pow(1 - t, 3); label.color = new Color(ink.r, ink.g, ink.b, 1 - e); if (glow) glow.color = label.color; for (int n = 0; n < bits.Count; n++) { float a = n * Mathf.PI * 2 / bits.Count; At(bits[n].rectTransform, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (14 + e * 58)); bits[n].color = new Color(ink.r, ink.g, ink.b, 1 - t); } yield return null; }
            Destroy(root.gameObject);
        }
        void OnDisable() { Cancel(); }
    }
}

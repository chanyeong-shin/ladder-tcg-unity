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
        void SetPeripheral(float weight){PeripheralAlpha=Mathf.Lerp(1,.48f,Mathf.Clamp01(weight));for(int i=0;i<peripheral.Count;i++)if(peripheral[i])peripheral[i].alpha=peripheralAlpha[i]*PeripheralAlpha;}
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
        // ---------- 13주차 빔 연출 (2026-11-27) ----------
        // 시안: _도구/시안13주차/tabs/04_beam.js A안(id 'pump') + 03_spawn.js TELE·dashes(예고선 충전) + 02_shot.js 그리기 도구, 값 그대로.
        // 시안은 직선 경로(서버 y 201 <-> 777, 576 px) 하나만 다룬다. 여기서는 시안의 "거리 d"를 BattleController.Points 폴리라인 위
        // 길이로 바꿔 쓴다(가로선을 타면 빔·꺾쇠·맥박도 경로를 따라 꺾인다).
        const float Charge = .65f, Reach = .5f, Snap = .25f, Extend = .2f, Fade = .35f, Struggle = 1f;
        static readonly float[] Pumps = { .15f, .45f, .75f };
        static readonly Color Gold = new Color(1, 196 / 255f, 56 / 255f), White = Color.white, CoreFill = new Color(232 / 255f, 1, 1), CoreInk = new Color(4 / 255f, 13 / 255f, 18 / 255f);
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
            public Packet packet; public List<Vector2> path; public float split = 1; public Vector2 contact; public bool contactEvent, piercing, triggered, arrived, hits;
            public float length, contactTime, head; public Vector2 position; public Clash clash; public TMP_Text number;
            public int Owner { get { return packet.Owner; } }
            // 접점에서 위력이 0이 되어 되감기는 탄(차단 포함)
            public bool Stops { get { return contactEvent && (packet.Blocked || packet.Power == 0); } }
        }
        sealed class Clash
        {
            public readonly Shot[] by = new Shot[2]; public readonly float[] meet = new float[2]; public readonly TMP_Text[] labels = new TMP_Text[2];
            public int win, rem, pumps; public float c0, c1; public bool started, broke;
        }
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
            else
            {
                var opponent = report.Packets.FirstOrDefault(p => p.Owner != packet.Owner && !p.Blocked && (packet.Power < packet.Initial || p.Power < p.Initial) && packet.Cells.Any(a => p.Cells.Any(b => a.Lane == b.Lane && a.Row == b.Row)));
                if (opponent != null) { var cell = packet.Cells.Where(a => opponent.Cells.Any(b => a.Lane == b.Lane && a.Row == b.Row)).OrderBy(a => Math.Abs(a.Row - 1)).ThenBy(a => a.Lane).First(); s.contact = new Vector2(BattleController.LaneX[cell.Lane], cell.Row < 0 ? 201 : cell.Row > 2 ? 777 : BattleController.RowY[cell.Row]); s.contactEvent = true; s.piercing = packet.Pierce != opponent.Pierce; }
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
                if (a.clash != null || a.packet.Blocked || !a.contactEvent) continue;
                // Count every launched, unblocked opponent, including packets whose final power is zero.
                var opponents = shots.Where(candidate => candidate.packet.Owner != a.packet.Owner && !candidate.packet.Blocked && candidate.packet.Initial > 0 && Touches(a, candidate)).ToArray();
                if (opponents.Length != 1) continue;
                var b = opponents[0];
                if (b.clash != null || b.packet.Pierce != a.packet.Pierce || !b.contactEvent) continue;
                if (shots.Count(other => other.packet.Owner != b.packet.Owner && !other.packet.Blocked && other.packet.Initial > 0 && Touches(b, other)) != 1) continue;
                // A partial crossing or a sequential many-to-many cancellation must keep the existing motion.
                if (a.path.Count != b.path.Count) continue;
                bool reversed = true;
                for (int i = 0; i < a.path.Count; i++)
                    if ((a.path[i] - b.path[b.path.Count - 1 - i]).sqrMagnitude > .01f) { reversed = false; break; }
                if (!reversed || a.packet.Power != Mathf.Max(0, a.packet.Initial - b.packet.Initial) || b.packet.Power != Mathf.Max(0, b.packet.Initial - a.packet.Initial)) continue;
                var clash = new Clash(); clash.by[a.Owner] = a; clash.by[b.Owner] = b;
                int p0 = clash.by[0].packet.Initial, p1 = clash.by[1].packet.Initial;
                clash.win = p0 > p1 ? 0 : p1 > p0 ? 1 : -1; clash.rem = Mathf.Abs(p0 - p1);
                for (int o = 0; o < 2; o++) clash.meet[o] = clash.by[o].split * clash.by[o].length;
                // 두 빔이 접점에서 맞닿는 시각 = 늦게 닿는 쪽의 F sqrt(split) (시안: 가운데라 F sqrt(.5))
                clash.c0 = Reach * Mathf.Sqrt(Mathf.Max(clash.by[0].split, clash.by[1].split)); clash.c1 = clash.c0 + Struggle;
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
            if (s.packet.Pierce) g.StrokeRoundRect(p.x - 15, p.y - 17, 30, 34, 6, 1.5f, A(Ui.Gold, a)); // 제로데이 윤곽 (기존 유지)
        }
        // 04_beam.js launch: 흰 섬광 0.06초 + 고리 14 -> 54 px 0.25초
        static void Launch(FxPainter g, Shot s, float u)
        {
            Vector2 p = s.path[0]; if (u < .06f) g.Glow(p, 46, White, 1 - u / .06f);
            if (u < .25f) RingA(g, p, Mix(Ink(s.Owner), White, .5f), Mathf.Lerp(14, 54, EOut(u / .25f)), 1 - u / .25f, 2.5f);
        }
        // 03_spawn.js TELE.charge: 꺾쇠가 0.05초 뒤부터 0.3초에 260 px로 뻗음, 핵 고동 2번(0.15초 작게 · 0.4초 크게), 0.6초부터 흰색으로 차오름
        static void ChargeShot(FxPainter g, Shot s, float T)
        {
            var col = Ink(s.Owner); Vector2 p = s.path[0]; float L = 260 * EOut(Mathf.Clamp01((T - .05f) / .3f), 3);
            float limit = s.clash != null ? s.clash.meet[s.Owner] : s.length;
            Dashes(g, s, 28, Mathf.Min(28 + L, limit), col, .7f, T);
            float beat = Mathf.Max(0, 1 - Mathf.Abs(T - .15f) / .07f) * .6f + Mathf.Max(0, 1 - Mathf.Abs(T - .4f) / .08f), w = T > .6f ? (T - .6f) / .05f : 0;
            g.Glow(p, 24 + 26 * beat, col, .3f + .5f * beat); g.Flare(p, col, .5f + .4f * beat, .4f + .5f * beat);
            Core(g, s.number, p, T > .15f ? s.packet.Initial : -1, Mathf.Clamp01(T / .1f), .7f + .3f * Mathf.Clamp01(T / .4f) + .15f * beat, w > 0);
            if (w > 0) g.Glow(p, 40, White, w);
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
        // A안 접점 이동(owner 0 서버 쪽이 +): 펌프 3번 easeOutBack(0.16초)로 진 쪽으로 15 x 위력 차 px, 같으면 sin(u 3 pi) x 10 왕복, 떨림 sin(41 T) x 1.8
        static float Dy(Clash k, float u, float T)
        {
            float y;
            if (k.win < 0) y = Mathf.Sin(u * Mathf.PI * 3) * 10;
            else { float o = 0; foreach (var p in Pumps) o += 15 * k.rem * Back(Mathf.Clamp01((u - p) / .16f)); y = (k.win == 0 ? -1 : 1) * o; }
            return y + Mathf.Sin(T * 41) * 1.8f;
        }
        static float ReachOf(Clash k, int o, float dy) { return Mathf.Clamp(o == 1 ? k.meet[1] + dy : k.meet[0] - dy, 0, k.by[o].length); }
        void Arrive(Shot s, Transform root, Transform lightRoot, Resolution report, ref float impulse)
        {
            s.arrived = true; int target = 1 - s.Owner;
            if (report.Before[target, s.packet.End] <= 0) return;
            s.hits = true; var hitLight = new EventFx.Plan(); hitLight.Rings.Add((s.path.Last(), Color.white, 26, 70, 0)); hitLight.Rings.Add((s.path.Last(), EventFx.Side(target), 26, 50, .08f));
            StartCoroutine(EventFx.Run(lightRoot, hitLight, () => Delta, 1)); impulse = 8;
            StartCoroutine(Impact(root, s.path.Last(), c.State.Health[target, s.packet.End] == 0 ? "서버 소실" : "착탄", s.Owner == 0 ? Ui.Cyan : Ui.Red, false, true));
            c.servers.First(v => v.owner == target && v.channel == s.packet.End).Render(c.State.Health[target, s.packet.End], 0);
        }
        // 정면 힘겨루기가 아닌 탄: 단독, 차단, 관통, 부분 교차·다대다 상쇄
        bool DrawFree(FxPainter g, Shot s, float e, float T, Transform root, Transform lightRoot, Resolution report, ref float impulse)
        {
            float L = s.length; Launch(g, s, e);
            if (s.contactEvent && !s.triggered && e >= s.contactTime)
            {
                s.triggered = true;
                if (!s.packet.Blocked && !s.piercing) { var clashLight = new EventFx.Plan(); clashLight.Rings.Add((s.contact, Color.white, 8 * EventFx.S, 60 * EventFx.S, 0)); StartCoroutine(EventFx.Run(lightRoot, clashLight, () => Delta, 1)); }
                if (s.packet.Blocked) { BlockContactCount++; LastBlockContact = s.contact; }
                impulse = Mathf.Max(impulse, s.piercing ? 0 : s.packet.Blocked ? 4 : 3);
                StartCoroutine(Impact(root, s.contact, s.packet.Blocked ? "서버 앞 차단" : s.piercing ? "관통" : "공격 상쇄", Ui.Gold, s.packet.Blocked && s.contact.y < 487));
            }
            int power = s.triggered ? s.packet.Power : s.packet.Initial;
            if (s.Stops && s.triggered)
            {
                // 위력 0: 진 빔처럼 0.25초 eIn^2 로 자기 서버로 되감김
                float r = e - s.contactTime, k = EIn(r / Snap, 2), reach = s.split * L;
                Beam(g, s, 0, reach * (1 - k), Width(s.packet.Initial), 1 - k * .5f);
                s.head = reach * (1 - k); s.position = PathMath.At(s.path, s.head); return r < Snap;
            }
            if (e < Reach)
            {
                float d = L * EIn(e / Reach, 2); if (s.Stops) d = Mathf.Min(d, s.split * L);
                Dashes(g, s, 28 + d, s.Stops ? s.split * L - 10 : 288, Ink(s.Owner), .7f * (1 - Mathf.Clamp01(e / Reach)), T);
                Beam(g, s, 0, d, Width(power), 1); Head(g, s, d, power, 1);
                s.head = d; s.position = PathMath.At(s.path, d); return true;
            }
            if (!s.arrived && s.packet.Power > 0) Arrive(s, root, lightRoot, report, ref impulse);
            float u = e - Reach;
            Beam(g, s, u < .25f ? 0 : L * EIn((u - .25f) / Fade), L, Width(power), 1 - Mathf.Clamp01((u - .25f) / Fade) * .6f);
            Hit(g, s, u / .45f);
            s.head = L; s.position = s.path[s.path.Count - 1]; return u < .25f + Fade;
        }
        Vector2 DrawClash(FxPainter g, Clash k, float e, float T, Transform root, Transform lightRoot, Resolution report, ref float impulse)
        {
            for (int o = 0; o < 2; o++) Launch(g, k.by[o], e);
            if (e < k.c0)
            {
                Vector2 sum = Vector2.zero;
                for (int o = 0; o < 2; o++)
                {
                    var s = k.by[o]; float d = Mathf.Min(k.meet[o], s.length * EIn(e / Reach, 2));
                    Dashes(g, s, 28 + d, k.meet[o] - 10, Ink(o), .7f * (1 - Mathf.Clamp01(e / Reach)), T);
                    Beam(g, s, 0, d, Width(s.packet.Initial), 1); Head(g, s, d, s.packet.Initial, 1);
                    s.head = d; s.position = PathMath.At(s.path, d); sum += s.position;
                }
                return sum / 2;
            }
            if (!k.started) { k.started = true; impulse = Mathf.Max(impulse, 2); }
            if (e < k.c1)
            {
                float u = (e - k.c0) / Struggle, dy = Dy(k, u, T); var reach = new[] { ReachOf(k, 0, dy), ReachOf(k, 1, dy) };
                Vector2 cp = PathMath.At(k.by[1].path, reach[1]);
                for (int o = 0; o < 2; o++) { Beam(g, k.by[o], 0, reach[o], Width(k.by[o].packet.Initial), 1); k.by[o].head = reach[o]; k.by[o].position = cp; }
                // 접점 구슬
                g.Glow(cp, 40, Gold, .85f); g.Glow(cp, 16, White, .9f); RingA(g, cp, Gold, 15 + 3 * Mathf.Sin(T * 30), .8f, 2);
                // 펌프 0.15초 전에 맥박이 서버 -> 접점으로 올라옴(같으면 양쪽에서 .8배)
                foreach (var p in Pumps)
                {
                    float m = (u - (p - .15f)) / .15f;
                    if (k.win >= 0) Pulse(g, k.by[k.win], reach[k.win], m, Width(k.by[k.win].packet.Initial), 1);
                    else for (int o = 0; o < 2; o++) Pulse(g, k.by[o], reach[o], m, Width(k.by[o].packet.Initial), .8f);
                }
                float pu = 0; foreach (var p in Pumps) pu = Mathf.Max(pu, 1 - Mathf.Abs(u - p) / .08f); g.Glow(cp, 30 * pu, Gold, .6f * pu);
                int f24 = Mathf.FloorToInt(T * 24);
                for (int i = 0; i < 5; i++) { float a = i / 5f * Mathf.PI * 2 + T * 9, r = 18 + Rnd(i + f24) * 14; Zig(g, cp, cp + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * .5f), f24 * 5 + i, 3, 3, Gold, 1, .7f); }
                // 접점 옆 양쪽 위력 (+18 px, 내 것 +26 · 상대 것 -26)
                for (int o = 0; o < 2; o++) { var t = k.labels[o]; string label = k.by[o].packet.Initial.ToString(); if (t.text != label) t.text = label; t.rectTransform.anchoredPosition = new Vector2(cp.x + 18, -(cp.y + (o == 0 ? 26 : -26))); t.color = Ink(o); }
                while (k.pumps < Pumps.Length && u >= Pumps[k.pumps]) { impulse = Mathf.Max(impulse, k.win >= 0 ? 3 : 2); k.pumps++; }
                return cp;
            }
            float rr = e - k.c1, dyEnd = Dy(k, 1, Charge + k.c1); var rch = new[] { ReachOf(k, 0, dyEnd), ReachOf(k, 1, dyEnd) };
            Vector2 at = PathMath.At(k.by[1].path, rch[1]), focus = at;
            if (!k.broke) { k.broke = true; impulse = Mathf.Max(impulse, 5); }
            for (int o = 0; o < 2; o++)
            {
                var s = k.by[o];
                if (k.win != o)
                {
                    // 진 쪽: 0.25초 eIn^2 로 자기 서버로 되감김 + 파편 10 (자기 서버 쪽으로)
                    float kk = EIn(rr / Snap, 2); Beam(g, s, 0, rch[o] * (1 - kk), Width(s.packet.Initial), 1 - kk * .5f);
                    if (rr < Snap) Shards(g, at, Ink(o), rr / Snap, 10, 46, 21 + o, -PathMath.Dir(s.path, rch[o]));
                    s.head = rch[o] * (1 - kk); s.position = PathMath.At(s.path, s.head);
                }
                else
                {
                    // 이긴 쪽: 0.2초 eIn^2 로 끝까지 뻗어 착탄, 0.25초 머문 뒤 0.35초에 서버 쪽부터 걷힘(알파 .4까지)
                    float L = s.length, d = rr < Extend ? Mathf.Lerp(rch[o], L, EIn(rr / Extend, 2)) : L, a = rr < Extend + .25f ? 1 : 1 - Mathf.Clamp01((rr - Extend - .25f) / Fade) * .6f;
                    Beam(g, s, rr < Extend + .25f ? 0 : L * EIn((rr - Extend - .25f) / Fade), d, Width(k.rem), a);
                    if (rr < Extend) Head(g, s, d, k.rem, 1);
                    else { if (!s.arrived && s.packet.Power > 0) Arrive(s, root, lightRoot, report, ref impulse); Hit(g, s, (rr - Extend) / .45f); }
                    s.head = d; s.position = PathMath.At(s.path, d); focus = s.position;
                }
            }
            if (rr < .3f) { g.Glow(at, 80 * (1 - rr / .3f) + 20, k.win >= 0 ? Ink(k.win) : Gold, 1 - rr / .3f); RingA(g, at, k.win >= 0 ? White : Gold, Mathf.Lerp(16, 90, EOut(rr / .3f)), 1 - rr / .3f, 3); }
            return focus;
        }
        static float EndTime(Shot s)
        {
            if (s.clash != null) return s.clash.win == s.Owner ? s.clash.c1 + Extend + .25f + Fade : s.clash.c1 + Mathf.Max(Snap, .3f);
            if (s.Stops) return s.contactTime + Snap;
            return Reach + .25f + Fade;
        }
        bool Cancelled(int version, RectTransform root)
        {
            if (version == playbackVersion && root) return false;
            if (root) Destroy(root.gameObject);
            return true;
        }
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
            ClashPhase = "idle"; ClashContact = -1; ChargeTime = FlightTime = 0;
            BlockContactCount=0;yield return BeginResolution();
            var root = Ui.Rect("공격 흐름", layer, 0, 0, 1920, 1080);
            var painter = FxPainter.Create("빔 연출", root); // 숫자 글자들이 이 위에 그려지도록 먼저 만든다
            var shots = report.Packets.Select(p => Make(p, report, root)).ToList();
            if (shots.Count == 0) { for(float t=0;t<1;t+=Delta/.45f){if(Cancelled(version,root))yield break;Frame(new Vector2(982,487),1-Smooth(t));yield return null;}Destroy(root.gameObject);ResetView();Phase="idle";yield break; }
            var clashes = CreateClashes(shots, root); if (clashes.Count > 0) ClashContact = clashes[0].c0;
            var texts = shots.Select(s => s.number).Concat(clashes.SelectMany(k => k.labels)).ToList();
            Vector2 source = shots.Aggregate(Vector2.zero, (p, s) => p + s.path[0]) / shots.Count;
            Phase = "charge";
            for (float t = 0; t < Charge; t += Delta)
            {
                if (Cancelled(version, root)) yield break;
                Progress = t / Charge; ChargeTime = t;
                foreach (var x in texts) x.color = Color.clear;
                painter.Begin(); foreach (var s in shots) ChargeShot(painter, s, t); painter.End();
                Frame(source, 1); yield return null;
            }
            if (Cancelled(version, root)) yield break;
            foreach (var s in shots) c.servers.First(v => v.owner == s.packet.Owner && v.channel == s.packet.Start).Render(report.Before[s.packet.Owner, s.packet.Start], 0);
            // HTML tab 2: every launched path glows in its side's color; tab 6: a packet crossing a rung flashes it.
            var lights = new EventFx.Plan(); foreach (var s in shots) if (s.clash == null) lights.Halos.Add((s.path, EventFx.Side(s.packet.Owner), 0)); // a tug-of-war keeps its own effect visible
            var lightRoot = BoardEffectUnderServers("이벤트 빛 - 발동"); StartCoroutine(EventFx.Run(lightRoot, lights, () => Delta));
            var crossings = shots.Select(s => RungCrossings(s)).ToList(); var crossed = new HashSet<(int, int)>();
            Phase = "flight"; float impulse = 0;
            float duration = shots.Max(s => EndTime(s));
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
                Vector2 focus = Vector2.zero; int count = 0;
                foreach (var s in shots) if (s.clash == null && DrawFree(painter, s, elapsed, T, root, lightRoot, report, ref impulse)) { focus += s.position; count++; }
                foreach (var k in clashes) { focus += DrawClash(painter, k, elapsed, T, root, lightRoot, report, ref impulse); count++; }
                painter.End();
                for (int si = 0; si < shots.Count; si++) { var s = shots[si]; float p = s.head / s.length; foreach (var cross in crossings[si]) if (p >= cross.at && !crossed.Contains((si, cross.index)) && s.packet.Power > 0) { crossed.Add((si, cross.index)); StartCoroutine(BridgeDots.RungFlash(lightRoot, cross.a, cross.b, () => Delta)); } }
                impulse = Mathf.Max(0, impulse - Delta * 15); Frame(count > 0 ? focus / count : new Vector2(982, 487), 1, impulse, elapsed); yield return null; if (elapsed >= duration) break;
            }
            if (Cancelled(version, root)) yield break;
            ClashPhase = "idle";
            painter.Begin(); painter.End(); foreach (var x in texts) x.color = Color.clear;
            Phase = "return"; Vector2 endFocus = shots.Aggregate(Vector2.zero, (p, s) => p + s.position) / shots.Count;
            for (float t = 0; t < 1; t += Delta / .45f) { if (Cancelled(version, root)) yield break; Progress = t; Frame(endFocus, 1 - Smooth(t), impulse * (1 - t) * (1 - t), 1 + t); yield return null; }
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

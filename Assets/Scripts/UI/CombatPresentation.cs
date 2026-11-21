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
        const float FlightSeconds = 1.55f, ContactSeconds = FlightSeconds * .52f, HoldSeconds = .85f, YieldSeconds = .95f;
        const float StruggleSeconds = HoldSeconds + YieldSeconds;
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
        float Delta => Time.unscaledDeltaTime * Mathf.Max(.1f, c.animationSpeed);
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
                foreach(Transform item in background.transform) {
                    var r=item as RectTransform;if(!r||r==board||r==surface)continue;
                    if(r.anchoredPosition.x+r.rect.width<=582||r.anchoredPosition.x>=1382||-r.anchoredPosition.y+r.rect.height<=129||-r.anchoredPosition.y>=845) Dim(item.gameObject);
                }
            }
            Track(c.rungs); Track(c.paths); foreach (var server in c.servers) Track((RectTransform)server.transform);
            layer = Ui.Rect("공격 연출 - 카메라 연동", c.game.transform, 0, 0, 1920, 1080); layer.SetAsLastSibling(); Track(layer);
            foreach(var item in new GameObject[]{c.hand.gameObject,c.endButton.gameObject,c.undoButton.gameObject,c.logButton.gameObject,c.roundLabel.gameObject,c.statusLabel.gameObject,c.secretLabel.gameObject})Dim(item);
        }
        void Dim(GameObject item){var group=item.GetComponent<CanvasGroup>();if(!group)group=item.AddComponent<CanvasGroup>();peripheral.Add(group);peripheralAlpha.Add(group.alpha);}
        void SetPeripheral(float weight){PeripheralAlpha=Mathf.Lerp(1,.48f,Mathf.Clamp01(weight));for(int i=0;i<peripheral.Count;i++)if(peripheral[i])peripheral[i].alpha=peripheralAlpha[i]*PeripheralAlpha;}
        void Track(RectTransform r) { visuals.Add(r); origins.Add(r.anchoredPosition); }
        public void ResetView() { for (int i = 0; i < visuals.Count; i++) if (visuals[i]) { visuals[i].anchoredPosition = origins[i]; visuals[i].localScale = Vector3.one; } CameraZoom = 1; CameraOffset = Vector2.zero; SetPeripheral(0); ResolutionFocused=false; }
        public RectTransform BoardEffect(string name) => Ui.Rect(name,layer,0,0,1920,1080);
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
        sealed class Shot
        {
            public Packet packet; public List<Vector2> path; public float split = 1; public Vector2 contact; public bool contactEvent, piercing, triggered, arrived;
            public RectTransform root; public TMP_Text number; public Shape[] trails; public Vector2 position;
            public float length;
            public CanvasGroup opacity;
            public Clash clash;
        }
        sealed class Clash
        {
            public Shot first, second, lead;
            public float distance, releaseFraction;
            public Vector2 point, releasePoint;
            public RectTransform root;
            public FlareGraphic glow;
            public Shape ring;
            public Shape[] sparks;
            public TMP_Text caption;
            public bool started, released;
        }
        static float Fraction(List<Vector2> path, Vector2 point) { float total = 0, done = 0; for (int j = 1; j < path.Count; j++) total += Vector2.Distance(path[j - 1], path[j]); for (int j = 1; j < path.Count; j++) { float len = Vector2.Distance(path[j - 1], path[j]); if (Mathf.Abs(Vector2.Distance(path[j - 1], point) + Vector2.Distance(point, path[j]) - len) < .1f) return (done + Vector2.Distance(path[j - 1], point)) / Mathf.Max(1, total); done += len; } return 1; }
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
            s.length = 0;
            for (int i = 1; i < s.path.Count; i++) s.length += Vector2.Distance(s.path[i - 1], s.path[i]);
            s.length = Mathf.Max(1, s.length);
            if (s.contactEvent) s.split = Fraction(s.path, s.contact);
            s.root = Ui.Rect("빛번짐 탄 - " + packet.Owner + " CH " + packet.Start, parent, 0, 0, 1, 1); Color ink = packet.Owner == 0 ? Ui.Cyan : Ui.Red;
            s.trails = new Shape[10]; for (int n = 0; n < s.trails.Length; n++) s.trails[n] = Ui.Line("이동 잔광 " + n, parent, Vector2.zero, Vector2.right, new Color(ink.r, ink.g, ink.b, .3f), 4);
            s.opacity = s.root.gameObject.AddComponent<CanvasGroup>();
            s.opacity.blocksRaycasts = false; s.opacity.interactable = false;
            s.root.SetAsLastSibling(); Flare(s.root, "가로 빛번짐", Vector2.zero, ink);
            if (packet.Pierce) Ui.Box("제로데이 윤곽", s.root, -15, -17, 30, 34, Ui.Gold, false, 1.5f, 6);
            Ui.Box("둥근 위력 핵", s.root, -10, -13, 20, 26, new Color(.91f, 1, 1), true, 1, 48);
            s.number = Ui.Text("핵 위 위력", s.root, -22, -18, 44, 36, packet.Initial.ToString(), c.font, 20, new Color(.015f, .05f, .07f)); s.number.alignment = TextAlignmentOptions.Midline;
            s.root.gameObject.SetActive(false); return s;
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
                var clash = new Clash { first = a, second = b, lead = a.packet.Power > 0 ? a : b.packet.Power > 0 ? b : a };
                clash.distance = clash.lead.packet.Power > 0 ? Mathf.Min(100, (1 - clash.lead.split) * clash.lead.length * .3f) : 0;
                clash.releaseFraction = clash.lead.split + clash.distance / clash.lead.length;
                clash.releasePoint = CardTransferFx.Along(clash.lead.path, clash.releaseFraction);
                clash.point = clash.lead.contact;
                clash.root = Ui.Rect("힘겨루기 접점", parent, clash.point.x, clash.point.y, 1, 1);
                clash.glow = Flare(clash.root, "접점 압력광", Vector2.zero, Ui.Gold, .8f);
                clash.ring = Ui.Box("압력 고리", clash.root, -27, -20, 54, 40, Ui.Gold, false, 1.5f, 40);
                clash.sparks = new Shape[8];
                for (int i = 0; i < clash.sparks.Length; i++)
                    clash.sparks[i] = Ui.Line("힘겨루기 스파크", clash.root, Vector2.zero, Vector2.right, i % 2 == 0 ? Ui.Cyan : Ui.Red, 1.8f);
                clash.caption = Ui.Text("힘겨루기 상태", clash.root, -125, -65, 250, 28, "팽팽한 힘겨루기", c.font, 17, new Color(.9f, .97f, 1));
                clash.caption.alignment = TextAlignmentOptions.Center;
                clash.root.gameObject.SetActive(false);
                a.clash = b.clash = clash;
                clashes.Add(clash);
            }
            return clashes;
        }
        void UpdateClash(Clash clash, float elapsed, Transform parent, ref float impulse)
        {
            if (elapsed < ContactSeconds) return;
            if (!clash.started) { clash.started = true; impulse = Mathf.Max(impulse, 2); }
            float u = Mathf.Clamp01((elapsed - ContactSeconds) / StruggleSeconds);
            float yielding = Mathf.Clamp01((elapsed - ContactSeconds - HoldSeconds) / YieldSeconds);
            if (elapsed < ContactSeconds + StruggleSeconds)
            {
                float tremor = (Mathf.Sin(u * 38) * 3.5f + Mathf.Sin(u * 71) * 1.3f) * (1 - yielding * .75f);
                float p = Mathf.Clamp01(clash.lead.split + (clash.distance * Mathf.Pow(yielding, 2.2f) + tremor) / clash.lead.length);
                clash.point = CardTransferFx.Along(clash.lead.path, p);
                clash.root.gameObject.SetActive(true); At(clash.root, clash.point);
                clash.glow.color = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, .5f + yielding * .2f + Mathf.Sin(u * 15) * .1f);
                clash.glow.rectTransform.localScale = Vector3.one * (1 + yielding * .25f);
                At(clash.glow.rectTransform, -clash.glow.rectTransform.sizeDelta * (1 + yielding * .25f) * .5f);
                var ringSize = new Vector2(54 + yielding * 12, 40 + yielding * 8);
                clash.ring.rectTransform.sizeDelta = ringSize; At(clash.ring.rectTransform, -ringSize * .5f);
                clash.caption.text = clash.lead.packet.Power > 0 && yielding > 0 ? "조금씩 밀리는 중" : "팽팽한 힘겨루기";
                for (int i = 0; i < clash.sparks.Length; i++)
                {
                    float a = i * Mathf.PI / 4 + u * .35f, radius = 21 + Mathf.Sin(u * 24 + i) * 3;
                    Vector2 start = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    Vector2 end = new Vector2(Mathf.Cos(a + .18f), Mathf.Sin(a + .18f)) * (radius + 12);
                    Vector2 delta = end - start; var spark = clash.sparks[i];
                    At(spark.rectTransform, start);
                    spark.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    spark.rectTransform.sizeDelta = new Vector2(delta.magnitude, 1.8f);
                }
                return;
            }
            clash.root.gameObject.SetActive(false);
            float rush = 1 - Mathf.Pow(1 - Mathf.Clamp01((elapsed - ContactSeconds - StruggleSeconds) / (FlightSeconds - ContactSeconds)), 3);
            clash.point = clash.lead.packet.Power > 0 ? CardTransferFx.Along(clash.lead.path, Mathf.Lerp(clash.releaseFraction, 1, rush)) : clash.releasePoint;
            if (!clash.released)
            {
                clash.released = true;
                clash.first.triggered = clash.second.triggered = true;
                clash.first.number.text = clash.first.packet.Power.ToString();
                clash.second.number.text = clash.second.packet.Power.ToString();
                bool winner = clash.lead.packet.Power > 0;
                Color ink = winner ? (clash.lead.packet.Owner == 0 ? Ui.Cyan : Ui.Red) : Ui.Gold;
                impulse = Mathf.Max(impulse, winner ? 5 : 3);
                StartCoroutine(Impact(parent, clash.releasePoint, winner ? "돌파 · 잔여 " + clash.lead.packet.Power : "동시 상쇄", ink));
            }
        }
        float ShotProgress(Shot shot, float elapsed)
        {
            shot.opacity.alpha = 1;
            if (shot.clash == null)
            {
                float t = Mathf.Clamp01(elapsed / FlightSeconds);
                if(shot.packet.Blocked)return Mathf.Min(t,shot.split);
                return shot.contactEvent ? (t < .52f ? Mathf.Lerp(0, shot.split, t / .52f) : Mathf.Lerp(shot.split, 1, (t - .52f) / .48f)) : t;
            }
            if (elapsed < ContactSeconds)
                return Mathf.Max(0, shot.split - 14 / shot.length) * Mathf.Clamp01(elapsed / ContactSeconds);
            float after = Mathf.Max(0, elapsed - ContactSeconds - StruggleSeconds);
            bool fading = shot.triggered && shot.packet.Power == 0;
            Vector2 front = fading ? shot.clash.releasePoint : shot.clash.point;
            if (fading) shot.opacity.alpha = 1 - Mathf.Clamp01(after / .22f);
            shot.root.localScale = Vector3.one * (.75f + shot.opacity.alpha * .25f);
            float gap = 14 * (1 - Mathf.Clamp01(after / .18f));
            return Mathf.Clamp01(Fraction(shot.path, front) - gap / shot.length);
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
            ResetView(); Phase = "idle"; ClashPhase = "idle"; Progress = 0;
        }
        public IEnumerator Play(Resolution report)
        {
            int version = ++playbackVersion;
            ClashPhase = "idle";
            BlockContactCount=0;yield return BeginResolution();
            var root = Ui.Rect("공격 흐름", layer, 0, 0, 1920, 1080); var shots = report.Packets.Select(p => Make(p, report, root)).ToList();
            if (shots.Count == 0) { for(float t=0;t<1;t+=Delta/.45f){if(Cancelled(version,root))yield break;Frame(new Vector2(982,487),1-Smooth(t));yield return null;}Destroy(root.gameObject);ResetView();Phase="idle";yield break; }
            var clashes = CreateClashes(shots, root);
            Vector2 source = shots.Aggregate(Vector2.zero, (p, s) => p + s.path[0]) / shots.Count;
            var beams = new List<Shape>(); var glows = new List<FlareGraphic>();
            foreach (var s in shots) { Color ink = s.packet.Owner == 0 ? Ui.Cyan : Ui.Red; for (int n = 0; n < 6; n++) beams.Add(Ui.Line("응축 광선", root, Vector2.zero, Vector2.right, ink, 2)); glows.Add(Flare(root, "발사구 응축", s.path[0], ink, .65f)); }
            Phase = "charge";
            for (float t = 0; t < 1; t += Delta / .62f)
            {
                if (Cancelled(version, root)) yield break;
                Progress = t; float compression = t < .78f ? Smooth(t / .78f) : 1; int k = 0;
                for (int j = 0; j < shots.Count; j++) { var origin = shots[j].path[0]; for (int n = 0; n < 6; n++) { float a = n * Mathf.PI / 3; Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), p = origin + dir * Mathf.Lerp(67, 19, compression); var beam = beams[k++]; At(beam.rectTransform, p); beam.rectTransform.localRotation = Quaternion.Euler(0, 0, -a * Mathf.Rad2Deg + 180); beam.rectTransform.sizeDelta = new Vector2(8 + 16 * compression, 1 + compression); beam.color = new Color(beam.color.r, beam.color.g, beam.color.b, .25f + .75f * compression); } glows[j].color = new Color(glows[j].color.r, glows[j].color.g, glows[j].color.b, compression * .55f); }
                Frame(source, 1); yield return null;
            }
            if (Cancelled(version, root)) yield break;
            foreach (var b in beams) Destroy(b.gameObject); foreach (var g in glows) Destroy(g.gameObject);
            foreach (var s in shots) { StartCoroutine(Flash(root, s.path[0], s.packet.Owner == 0 ? Ui.Cyan : Ui.Red)); c.servers.First(v => v.owner == s.packet.Owner && v.channel == s.packet.Start).Render(report.Before[s.packet.Owner, s.packet.Start], 0); }
            Phase = "flight"; float impulse = 0;
            float duration = FlightSeconds + (clashes.Count > 0 ? StruggleSeconds : 0);
            for (float elapsed = 0; ; elapsed = Mathf.Min(duration, elapsed + Delta))
            {
                if (Cancelled(version, root)) yield break;
                float t = Mathf.Clamp01(elapsed / FlightSeconds);
                float held = clashes.Count > 0 ? Mathf.Clamp(elapsed - ContactSeconds, 0, StruggleSeconds) : 0;
                Progress = Mathf.Clamp01((elapsed - held) / FlightSeconds);
                ClashPhase = clashes.Count == 0 || elapsed < ContactSeconds ? "idle" : elapsed < ContactSeconds + HoldSeconds ? "hold" : elapsed < ContactSeconds + StruggleSeconds ? "yield" : "break";
                foreach (var clash in clashes) UpdateClash(clash, elapsed, root, ref impulse);
                Vector2 focus = Vector2.zero; int count = 0;
                foreach (var s in shots)
                {
                    float p = ShotProgress(s, elapsed); s.position = CardTransferFx.Along(s.path, p);
                    if (s.clash == null && s.contactEvent && !s.triggered && t >= (s.packet.Blocked?s.split:.52f)) { s.triggered = true;if(s.packet.Blocked){BlockContactCount++;LastBlockContact=s.contact;} impulse = Mathf.Max(impulse, s.piercing ? 0 : s.packet.Blocked ? 4 : 3); StartCoroutine(Impact(root, s.contact, s.packet.Blocked ? "서버 앞 차단" : s.piercing ? "관통" : "공격 상쇄", Ui.Gold, s.packet.Blocked && s.contact.y < 487)); s.number.text = s.packet.Power.ToString(); }
                    bool visible = !s.arrived && (s.clash != null ? s.opacity.alpha > .001f : !(s.triggered && s.packet.Power == 0)); s.root.gameObject.SetActive(visible); At(s.root, s.position);
                    for (int n = 0; n < s.trails.Length; n++) { var trail = s.trails[n]; float q = p - .012f * n; trail.gameObject.SetActive(visible && q > 0); Vector2 a = CardTransferFx.Along(s.path, Mathf.Max(0, q - .012f)), b = CardTransferFx.Along(s.path, Mathf.Max(0, q)); var d = b - a; At(trail.rectTransform, a); trail.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg); trail.rectTransform.sizeDelta = new Vector2(d.magnitude, 3 + (1 - n / 10f) * 4); Color ink = s.packet.Owner == 0 ? Ui.Cyan : Ui.Red; trail.color = new Color(ink.r, ink.g, ink.b, (1 - n / 10f) * .45f * s.opacity.alpha); }
                    if (visible) { focus += s.position; count++; }
                    if (elapsed >= FlightSeconds + (s.clash != null ? StruggleSeconds : 0) && !s.arrived && s.packet.Power > 0) { s.arrived = true; int target = 1 - s.packet.Owner; if (report.Before[target, s.packet.End] > 0) { impulse = 8; StartCoroutine(Impact(root, s.path.Last(), c.State.Health[target, s.packet.End] == 0 ? "서버 소실" : "착탄", s.packet.Owner == 0 ? Ui.Cyan : Ui.Red)); c.servers.First(v => v.owner == target && v.channel == s.packet.End).Render(c.State.Health[target, s.packet.End], 0); } }
                }
                impulse = Mathf.Max(0, impulse - Delta * 15); Frame(count > 0 ? focus / count : new Vector2(982, 487), 1, impulse, elapsed); yield return null; if (elapsed >= duration) break;
            }
            if (Cancelled(version, root)) yield break;
            ClashPhase = "idle";
            foreach (var s in shots) { s.root.gameObject.SetActive(false); foreach (var trail in s.trails) trail.gameObject.SetActive(false); }
            Phase = "return"; Vector2 endFocus = shots.Aggregate(Vector2.zero, (p, s) => p + s.position) / shots.Count;
            for (float t = 0; t < 1; t += Delta / .45f) { if (Cancelled(version, root)) yield break; Progress = t; Frame(endFocus, 1 - Smooth(t), impulse * (1 - t) * (1 - t), 1 + t); yield return null; }
            if (Cancelled(version, root)) yield break;
            ResetView(); Destroy(root.gameObject); Phase = "idle"; ClashPhase = "idle"; Progress = 0;
        }
        IEnumerator Flash(Transform parent, Vector2 p, Color ink) { var glow = Flare(parent, "발사 순간 방출", p, ink, 1.35f); for (float t = 0; t < 1; t += Delta / .16f) { if (!glow) yield break; glow.color = new Color(ink.r, ink.g, ink.b, (1 - t) * (1 - t)); yield return null; } Destroy(glow.gameObject); }
        IEnumerator Impact(Transform parent, Vector2 p, string caption, Color ink, bool labelBelow = false)
        {
            var root = Ui.Rect("충돌 결과", parent, p.x, p.y, 1, 1); var glow = Flare(root, "충돌광", Vector2.zero, ink, 1.15f); var bits = new List<Shape>(); for (int n = 0; n < 12; n++) bits.Add(Ui.Box("회로 파편", root, 0, 0, 4, 2, ink)); var label = Ui.Text("결과", root, -90, labelBelow ? 24 : -50, 180, 28, caption, c.font, 18, ink); label.alignment = TextAlignmentOptions.Center;
            for (float t = 0; t < 1; t += Delta / .36f) { if (!root) yield break; float e = 1 - Mathf.Pow(1 - t, 3); glow.color = label.color = new Color(ink.r, ink.g, ink.b, 1 - e); for (int n = 0; n < bits.Count; n++) { float a = n * Mathf.PI * 2 / bits.Count; At(bits[n].rectTransform, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (14 + e * 58)); bits[n].color = new Color(ink.r, ink.g, ink.b, 1 - t); } yield return null; }
            Destroy(root.gameObject);
        }
        void OnDisable() { Cancel(); }
    }
}

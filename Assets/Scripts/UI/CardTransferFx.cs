using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 public sealed class CardTransferFx : MonoBehaviour {
  public RectTransform layer;
  public TMP_FontAsset font;
  public Sprite[] cardImages;
  public Sprite backImage;
  public float speed=1;
  public string Phase {get;private set;}="idle";
  public float Progress {get;private set;}
  // week 12 (계획/Unity_12주차_적용목록.md items 1, 4, 5)
  public SocketFx socket;
  // item 1: action-turn counter and this turn's random pick (ActivateFx.Choose: never the previous turn's pick).
  // Week 13 (11_fxpick.js): the effect is chosen in the bottom-right picker (EffectPicker, saved); "무작위" uses TurnPick.
  public int ActionTurn {get;private set;}=-1;
  public int TurnPick {get;private set;}=ActivateFx.Lockon;
  int forced=-1;
  public int EffectPick {get{return forced>=0?forced:EffectPicker.Resolve(TurnPick);}}
  // item 5: socket-to-board route of the last dot card (0 top, 1 middle, 2 bottom) and how many cards were placed this battle
  public int LastRoute {get;private set;}=-1;
  int plays; public int Plays {get{return plays;}}
  // cards between their scan and their end keep the hold circuit on (two may overlap since item 5)
  int holders;
  public float EffectRate {get{return Mathf.Max(.1f,Rate);}}
  public void ResetTurns(){ActionTurn=-1;TurnPick=ActivateFx.Lockon;forced=-1;plays=0;holders=0;LastRoute=-1;fires.Clear();if(socket)socket.ResetAll();}
  public void ForcePick(int pick){forced=pick;} // checks only: overrides the picker until the next action turn
  public static readonly Rect Socket=new Rect(223,381,228,236);
  public static Rect DockRect {get {float h=Socket.height-16,w=h/1.6f;return new Rect(Socket.center.x-w/2,Socket.y+8,w,h);}}
  static readonly Vector2[][] Routes={
   new[]{new Vector2(448,480),new Vector2(532,480),new Vector2(548,480),new Vector2(548,385),new Vector2(582,385)},
   new[]{new Vector2(448,498),new Vector2(532,498),new Vector2(560,498),new Vector2(560,423),new Vector2(582,423)},
   new[]{new Vector2(448,516),new Vector2(532,516),new Vector2(550,516),new Vector2(550,561),new Vector2(582,561)}
  };
  BattleController host;
  // While the controller is skipping, every phase below runs to its end in one frame.
  float Rate {get{if(!host)host=GetComponent<BattleController>();return host&&host.Skipping?BattleController.SkipSpeed:speed*(host?host.HoldFactor:1);}}
  float Delta {get{return Time.deltaTime*Mathf.Max(.1f,Rate)*FxClock.Scale;}}
  static float Ease(float t){return t*t*(3-2*t);}
  RectTransform Root(string name){return Ui.Rect(name,layer,0,0,1920,1080);}
  void At(RectTransform r,Vector2 p){r.anchoredPosition=new Vector2(p.x,-p.y);}
  Image Picture(string name,Transform parent,Sprite sprite,Rect rect){var r=Ui.Rect(name,parent,rect.x,rect.y,rect.width,rect.height);var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;return image;}
  // released: invoked when the scan ends (week 12 item 5, 11_queue.js "scan": the socket empties and the next card may be placed
  // while this card's circuit, absorption, dot and result keep playing). target: the card's target (for the dot's destination).
  public IEnumerator Play(Card card,int owner,Vector2 origin,Vector2 originSize,System.Action released=null,int target=-1){
   Color color=owner==0?Ui.Cyan:Ui.Red;var token=new object();
   // item 5: board cards (code injection, rerouting) become a dot that takes one of the three socket-to-board routes, chosen per card
   // like the HTML: pickRoute = Math.floor(hash(cycle * 4 + i + 11) * 3) % 3 (CardTransferFx.Hash = the HTML's Math.imul hash)
   bool dot=card==Card.Inject||card==Card.Reroute;int route=Mathf.FloorToInt(Hash(plays+11)*3)%3;plays++;if(dot)LastRoute=route;
   // user 11-22: "봇이 내는 카드 보고 싶음 ... 그냥 다 공개 하자" — the bot's cards show their face when placed
   Sprite sprite=cardImages[(int)card]; // v1.1: every placed card shows its face
   var root=Root("카드 사용 - 안착 분해 전송");Rect dock=DockRect;
   var chip=Picture("사용 카드",root,sprite,new Rect(origin,originSize));var rect=chip.rectTransform;
   Phase="move";
   for(float t=0;t<1;t+=Delta/.35f){Progress=t;float e=Ease(t);At(rect,Vector2.Lerp(origin,dock.position,e));rect.sizeDelta=Vector2.Lerp(originSize,dock.size,e);if(socket)socket.Move(token,t);yield return null;}
   At(rect,dock.position);rect.sizeDelta=dock.size;
   // item 4: the socket clamp turns white -> turn color and the gap fill turns from the circuit pattern to scanlines (SocketFx)
   Phase="dock";for(float t=0;t<1;t+=Delta/.25f){Progress=t;if(socket)socket.Dock(token,t);yield return null;}if(socket)socket.Dock(token,1);

   // Scan (HTML tab 4 "스캔 → 회로선"): a light bar sweeps the docked card in 0.8 s and the part it has passed
   // becomes 7 circuit lines with a jog and an end pad. The circuit stays faintly on screen (2026-11-22 request).
   var hold=GetComponent<CardHoldFx>();if(hold)hold.Show(hold.Visible);holders++;
   chip.type=Image.Type.Filled;chip.fillMethod=Image.FillMethod.Vertical;chip.fillOrigin=(int)Image.OriginVertical.Bottom;
   // 13주차 시안 (2026-11-27 사용자 "상대턴인데 카드가 스켄될때 파랑색이네 이거 고쳐야할듯"): 스캔 회로·끝 패드·빛 막대를 턴 색으로
   //  (01_socket.js scanCircuit: 회로 = 턴 색과 흰색 45%, 끝 패드 = 턴 색, 빛 막대 = 턴 색과 흰색 85%)
   Color turnInk=socket?socket.TargetInk:color,barInk=Color.Lerp(turnInk,Color.white,.85f);
   var barGlow=Ui.Box("스캔 빛 번짐",root,dock.x-16,dock.y-8,dock.width+32,16,new Color(barInk.r,barInk.g,barInk.b,.3f));
   var bar=Ui.Box("스캔 빛 막대",root,dock.x-8,dock.y-2,dock.width+16,4,barInk);
   // user 11-22: "카드 스캔될때 회로가 퍼져야지 쭉 늘어나는건 별로다" / "회로는 레퍼런스대로".
   // Reference rules (same as _도구/회로무늬_생성.py): parallel bundles at a fixed pitch, 45 degree bends with mitred
   // offsets so the pitch stays equal, an end pad on every line, no crossings. When the bar passes a bundle row a chip
   // appears at the card's center and its 4 lines spread out to both card edges.
   var lineInk=Color.Lerp(turnInk,Color.white,.45f);var seeds=new List<(Vector2 p,float at,Shape pad)>();var traces=new List<(int seed,Vector2[] pts,float total,Shape[] segs,Shape tip)>();
   // user 11-22: "카드도 좀 회로 촘촘하게 다시 만들고, 테두리까지 각 살려서 주입되게".
   // 6 bundles x 4 lines at pitch 5 fill the card; every line ends on the card border, which is itself a trace with
   // 45 degree chamfered corners drawn down both sides as the bar passes; a pad marks each junction.
   const float pitch=5;const int lanes=4;float cx0=dock.center.x,chamfer=12;
   for(int row=0;row<6;row++){
    float cy=dock.y+dock.height*(.1f+row*.16f);int bend=row%2==0?-1:1;
    var scanChip=Ui.Box("스캔 회로 칩",root,cx0-7,cy-lanes*pitch/2-3,14,lanes*pitch+6,lineInk,false,1.4f);scanChip.gameObject.SetActive(false);seeds.Add((new Vector2(cx0,cy),(cy-dock.y)/dock.height,scanChip));
    foreach(int dir in new[]{-1,1}){
     for(int i=0;i<lanes;i++){
      float y=cy+(i-(lanes-1)/2f)*pitch;int inner=bend*dir>0?lanes-1-i:i;float jogX=cx0+dir*(7+8+inner*pitch*.414f),rise=bend*8;
      var pts=new[]{new Vector2(cx0+dir*7,y),new Vector2(jogX,y),new Vector2(jogX+dir*Mathf.Abs(rise),y+rise),new Vector2(dir<0?dock.x:dock.xMax,y+rise)};
      float total=0;var segs=new Shape[3];
      for(int k=1;k<4;k++){total+=Vector2.Distance(pts[k-1],pts[k]);segs[k-1]=Ui.Line("스캔 회로선",root,pts[k-1],pts[k],lineInk,1.2f);segs[k-1].gameObject.SetActive(false);}
      var tip=Ui.Box("스캔 회로 테두리 접점",root,pts[3].x-2,pts[3].y-2,4,4,turnInk);tip.gameObject.SetActive(false);traces.Add((seeds.Count-1,pts,total,segs,tip));
     }
    }
   }
   // card border trace (chamfered), split into two halves from top center down to bottom center
   Vector2 tl=new Vector2(dock.x,dock.y),br=new Vector2(dock.xMax,dock.yMax);var borderHalves=new List<Vector2[]>();
   foreach(int dir in new[]{-1,1}){float ex=dir<0?tl.x:br.x;borderHalves.Add(new[]{new Vector2(dock.center.x,tl.y),new Vector2(ex-dir*chamfer,tl.y),new Vector2(ex,tl.y+chamfer),new Vector2(ex,br.y-chamfer),new Vector2(ex-dir*chamfer,br.y),new Vector2(dock.center.x,br.y)});}
   var borderSegs=new List<(Vector2 a,Vector2 b,Shape glow,Shape line)>();
   foreach(var half in borderHalves)for(int k=1;k<half.Length;k++){var glow=Ui.Line("카드 테두리 회로 빛",root,half[k-1],half[k],new Color(lineInk.r,lineInk.g,lineInk.b,.25f),6);var line=Ui.Line("카드 테두리 회로",root,half[k-1],half[k],lineInk,1.6f);glow.gameObject.SetActive(false);line.gameObject.SetActive(false);borderSegs.Add((half[k-1],half[k],glow,line));}
   // reveal the border down to the bar
   void Border(float barY){foreach(var bs in borderSegs){float len=Vector2.Distance(bs.a,bs.b),shown;
     if(Mathf.Abs(bs.b.y-bs.a.y)<.01f)shown=barY>=bs.a.y?len:0;else shown=len*Mathf.Clamp01((barY-bs.a.y)/(bs.b.y-bs.a.y));
     foreach(var g in new[]{bs.glow,bs.line}){g.gameObject.SetActive(shown>0);g.rectTransform.sizeDelta=new Vector2(shown,g.rectTransform.sizeDelta.y);}}}
   // grow: 0 = nothing, 1 = whole trace drawn from its seed
   void Grow(int index,float grow){var tr=traces[index];float d=tr.total*grow;
    for(int i=1;i<4;i++){float len=Vector2.Distance(tr.pts[i-1],tr.pts[i]);var seg=tr.segs[i-1];bool on=d>0&&len>.5f;seg.gameObject.SetActive(on);if(on)seg.rectTransform.sizeDelta=new Vector2(Mathf.Min(d,len),1.2f);d-=len;}
    tr.tip.gameObject.SetActive(grow>=.999f);}
   Phase="fragment";
   for(float t=0;t<1;t+=Delta/.8f){Progress=t;float y=dock.y+dock.height*t;chip.fillAmount=1-t;At(bar.rectTransform,new Vector2(dock.x-8,y-2));At(barGlow.rectTransform,new Vector2(dock.x-16,y-8));
    for(int i=0;i<seeds.Count;i++)seeds[i].pad.gameObject.SetActive(t>=seeds[i].at);Border(y);
    for(int i=0;i<traces.Count;i++){float since=(t-seeds[traces[i].seed].at)*.8f;Grow(i,since<=0?0:Ease(Mathf.Clamp01(since/.22f)));}
    if(socket)socket.Scan(token,t);yield return null;}
   if(socket)socket.Scan(token,1);
   // item 5: the scan is over: the card picture is gone, input and the socket open for the next card
   released?.Invoke();
   chip.gameObject.SetActive(false);bar.gameObject.SetActive(false);barGlow.gameObject.SetActive(false);
   for(float t=0;t<.22f;t+=Delta){for(int i=0;i<traces.Count;i++){float since=(1-seeds[traces[i].seed].at)*.8f+t;Grow(i,Ease(Mathf.Clamp01(since/.22f)));}if(socket)socket.Hold(token);yield return null;}
   for(int i=0;i<traces.Count;i++)Grow(i,1);Border(dock.yMax+1);
   // Absorption: the traces pull back into their pads, the pads gather at the socket exit,
   // and three comets leave the exit along the socket-to-board routes.
   var exit=Routes[dot?route:1][0];var routeInk=new Color(color.r,color.g,color.b,.18f);var routeLines=new List<Shape>();
   for(int r=0;r<3;r++)for(int seg=1;seg<Routes[r].Length;seg++)routeLines.Add(Ui.Line("소켓-판 회로",root,Routes[r][seg-1],Routes[r][seg],routeInk,r==1?3:1.5f));
   Color ink=owner==0?new Color(56/255f,217/255f,1):EventFx.Enemy;var dotPaint=dot?FxPainter.Create("카드 점 - 소켓에서 판으로",root):null;
   var comets=new List<Shape>();for(int lane=0;lane<3;lane++)for(int part=0;part<10;part++)comets.Add(Ui.Box("소켓 혜성",root,0,0,1,1,Color.clear));
   Phase="absorb-card";
   for(float t=0;t<1;t+=Delta/.7f){Progress=t;float drain=Ease(Mathf.Clamp01(t/.4f)),slide=Ease(Mathf.InverseLerp(.3f,.6f,t));
    for(int i=0;i<traces.Count;i++)Grow(i,1-drain);
    foreach(var bs in borderSegs){Vector2 a0=Vector2.Lerp(bs.a,exit,slide),b0=Vector2.Lerp(bs.b,exit,slide),dv=b0-a0;foreach(var g in new[]{bs.glow,bs.line}){At(g.rectTransform,a0);g.rectTransform.sizeDelta=new Vector2(dv.magnitude,g.rectTransform.sizeDelta.y);var c=g.color;c.a=(g==bs.glow?.25f:1)*(1-Mathf.InverseLerp(.5f,.7f,t));g.color=c;}}
    for(int i=0;i<seeds.Count;i++){var pad=seeds[i].pad;var half=pad.rectTransform.sizeDelta/2;At(pad.rectTransform,Vector2.Lerp(seeds[i].p,exit+new Vector2(0,(i-1)*4),slide)-half);pad.rectTransform.localScale=Vector3.one*Mathf.Lerp(1,.4f,slide);pad.color=new Color(lineInk.r,lineInk.g,lineInk.b,1-Mathf.InverseLerp(.55f,.7f,t));}
    if(dot){ // 11_queue.js: routes fade out over the last 15%, the dot runs the chosen route in the second half
     foreach(var l in routeLines){var c=l.color;c.a=.18f*(1-Mathf.Clamp01((t-.85f)/.15f));l.color=c;}
     float head=Mathf.Clamp01((t-.5f)/.5f);dotPaint.Begin();if(head>0&&head<1)Dot(dotPaint,Routes[route],head,1,ink);dotPaint.End();
     if(socket)socket.Absorb(token,t);yield return null;continue;}
    if(socket)socket.Absorb(token,t);
    int index=0;for(int lane=0;lane<3;lane++){float head=Mathf.InverseLerp(.5f+lane*.04f,1,t);
     for(int part=0;part<10;part++){var piece=comets[index++];float h1=head-part*.035f,h0=h1-.035f;bool visible=h1>0&&h0<1;piece.gameObject.SetActive(visible);if(!visible)continue;
      Vector2 pa=Along(Routes[lane],Mathf.Clamp01(h0)),pb=Along(Routes[lane],Mathf.Clamp01(h1)),dv=pb-pa;At(piece.rectTransform,pa);piece.rectTransform.pivot=new Vector2(0,.5f);piece.rectTransform.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(dv.y,dv.x)*Mathf.Rad2Deg);
      float f=1-part/10f;piece.rectTransform.sizeDelta=new Vector2(Mathf.Max(1,dv.magnitude),1.2f+1.6f*f);piece.color=part==0?new Color(.94f,1,1,1):new Color(.62f,.95f,1,.8f*f*f);}}
    yield return null;}
   // Activation: the circuit stays faint and comets run along it toward the board.
   if(socket)socket.Release(token);if(dotPaint){dotPaint.Begin();dotPaint.End();}
   Phase="absorb";var fade=root.GetComponentsInChildren<Shape>(true).ToArray();var start=fade.Select(f=>f.color.a).ToArray();
   System.Action<float> step=p=>{Progress=p;for(int i=0;i<fade.Length;i++)if(fade[i]){var c=fade[i].color;c.a=start[i]*(1-p);fade[i].color=c;}};
   if(dot){
    // 11_queue.js drawBoard 'act': the dot enters the board at the chosen route's end, runs 40 px in, across, then to the target;
    // the board edge glows rgba(cy, .25 sin(pi k)) at width 6. It sits under the servers like the HTML.
    // 13주차 10_nowait.js 'touch' (닿자마자 장전, 확정): act 1.25 -> .45 s, head = ease3(k) so the dot arrives exactly at the end (no .25 s idle);
    // the result (Applied) starts the moment Play returns.
    var controller=GetComponent<BattleController>();var boardLayer=controller&&controller.presentation?controller.presentation.BoardEffectUnderServers("카드 점 - 판 안"):Root("카드 점 - 판 안");
    var paint=FxPainter.Create("카드 점 - 대상까지",boardLayer);Vector2 from=Routes[route][4],tg=DotTarget(card,owner,target);
    var path=new List<Vector2>{from,from+new Vector2(40,0),new Vector2(tg.x,from.y),tg};
    for(float t=0;t<1;t+=Delta/ActLength){step(t);paint.Begin();Dot(paint,path,Ease3(t),1,ink);paint.StrokeRect(582,129,800,716,6,new Color(ink.r,ink.g,ink.b,.25f*Mathf.Sin(Mathf.PI*t)));paint.End();yield return null;}
    Destroy(boardLayer.gameObject);
   }
   else if(hold)yield return hold.Activate(()=>Delta,step);else for(float t=0;t<CircuitGraphic.SignalLength;t+=Delta){step(t/CircuitGraphic.SignalLength);yield return null;}
   // a card the player is holding now (selected or dragged) keeps its hold layer
   holders=Mathf.Max(0,holders-1);var owner2=GetComponent<BattleController>();if(hold&&holders==0&&!(owner2&&owner2.HoldingCard))hold.Hide(.4f);
   Destroy(root.gameObject);
   Phase="idle";Progress=0;
  }
  // 10_nowait.js touch: Dn({ act: .45 })
  public const float ActLength=.45f;
  static float Ease3(float v){v=Mathf.Clamp01(v);return 1-(1-v)*(1-v)*(1-v);}
  static readonly Color DotCore=new Color(233/255f,251/255f,1);
  // 11_queue.js comet() + dot(): 10 tail pieces of .035 path each (head core, then rgba(158,242,255,.8 f^2), width 1.2 + 2f),
  // a 9 px halo at .35 and a 3.5 px core at the head
  public static void Dot(FxPainter g,IList<Vector2> pts,float head,float a,Color ink){
   for(int m=0;m<10;m++){Vector2 p0=Along(pts,head-(m+1)*.035f),p1=Along(pts,head-m*.035f);float f=1-m/10f;
    g.Line(p0,p1,1.2f+2*f,m>0?new Color(158/255f,242/255f,1,.8f*f*f*a):new Color(DotCore.r,DotCore.g,DotCore.b,a));}
   var h=Along(pts,head);g.Disc(h,9,new Color(ink.r,ink.g,ink.b,.35f*a));g.Disc(h,3.5f,new Color(DotCore.r,DotCore.g,DotCore.b,a));
  }
  // the dot's destination: a loaded server (inject) or the middle of the rung (rerouting), as target() in 11_queue.js
  public static Vector2 DotTarget(Card card,int owner,int target){
   if(card==Card.Reroute){int slot=target<0?2:Battle.Slot(target);return new Vector2((BattleController.LaneX[slot%2]+BattleController.LaneX[slot%2+1])/2,BattleController.RowY[slot/2]);}
   return new Vector2(BattleController.LaneX[target<0?1:target],owner==0?777:201);
  }
  public static Vector2 Along(IList<Vector2> points,float t){float length=0;for(int i=1;i<points.Count;i++)length+=Vector2.Distance(points[i-1],points[i]);float distance=Mathf.Clamp01(t)*length;for(int i=1;i<points.Count;i++){float n=Vector2.Distance(points[i-1],points[i]);if(distance<=n)return Vector2.Lerp(points[i-1],points[i],n==0?0:distance/n);distance-=n;}return points[points.Count-1];}
  IEnumerator Absorb(Color color){
   var root=Root("판 테두리 흡수");var lines=new List<Shape>();
   Vector2[][] points={new[]{new Vector2(582,487),new Vector2(582,129),new Vector2(1382,129),new Vector2(1382,487)},new[]{new Vector2(582,487),new Vector2(582,845),new Vector2(1382,845),new Vector2(1382,487)}};
   // Equal-length branches meet together. Arrival pads merge along the left edge first.
   foreach(var route in Routes)Ui.Line("도착부 연결",root,route[route.Length-1],new Vector2(582,487),new Color(color.r,color.g,color.b,.35f),2);
   foreach(var path in points)for(int i=1;i<path.Length;i++)lines.Add(Ui.Line("양방향 흡수 확산",root,path[i-1],path[i],color,3));
   var full=new List<Shape>();foreach(float width in new[]{20f,10f,3f})full.Add(Ui.Box("전체 테두리 글로우",root,582,129,800,716,Color.clear,false,width));
   Phase="absorb";
   for(float t=0;t<1;t+=Delta/.72f){Progress=t;int index=0;float travel=Ease(Mathf.Clamp01(t/.62f));
    foreach(var path in points){float total=0;for(int i=1;i<path.Length;i++)total+=Vector2.Distance(path[i-1],path[i]);float d=total*travel;
     for(int i=1;i<path.Length;i++){var line=lines[index++];float len=Vector2.Distance(path[i-1],path[i]);line.rectTransform.sizeDelta=new Vector2(Mathf.Clamp(d,0,len),3);line.color=new Color(color.r,color.g,color.b,t<.62f?.85f:1-Mathf.InverseLerp(.62f,1,t));d-=len;}}
    float peak=t<.62f?0:Mathf.Pow(1-Mathf.InverseLerp(.62f,1,t),2);for(int i=0;i<full.Count;i++)full[i].color=new Color(color.r,color.g,color.b,peak*(i==0?.12f:i==1?.25f:.9f));
    yield return null;
   }
   Destroy(root.gameObject);
  }
  static float Hash(int n){n=(n<<13)^n;return ((n*(n*n*15731+789221)+1376312589)&0x7fffffff)/2147483647f;}
  public IEnumerator Applied(Card card,int owner,int target,bool connected){
   var controller=GetComponent<BattleController>();
   bool onBoard=card!=Card.Buffer&&card!=Card.Overflow;
   var root=onBoard&&controller&&controller.presentation?controller.presentation.BoardEffect("카드 적용 결과"):Root("카드 적용 결과");Color color=owner==0?Ui.Cyan:Ui.Red;Vector2 point;string message;Shape mark;RectTransform wall=null;var gates=new List<RectTransform>();
   FxPainter glitchHex=null;RectTransform glitchLayer=null;
   // item 5 (11_queue.js hexGlitch): the load mark is a server-shaped hexagon that jitters for .28 s and flashes, instead of the 76 px square
   if(card==Card.Inject||card==Card.ZeroDay){point=new Vector2(BattleController.LaneX[target],owner==0?777:201);message=card==Card.ZeroDay?"통과 1발 장전":"+1 장전";mark=Ui.Box("대상 서버 점등 (자리)",root,point.x-38,point.y-38,76,76,Color.clear,false,4,6);mark.enabled=false;
    glitchLayer=controller&&controller.presentation?controller.presentation.BoardEffectUnderServers("장전 육각형"):Root("장전 육각형");glitchHex=FxPainter.Create("장전 육각형 지직",glitchLayer);}
   else if(card==Card.Reroute){int slot=Battle.Slot(target);point=new Vector2((BattleController.LaneX[slot%2]+BattleController.LaneX[slot%2+1])/2,BattleController.RowY[slot/2]);var rider=controller&&controller.State!=null?controller.State.RungRiders[slot/2]:-1;message=connected?"회선 연결 · "+BattleController.RiderName(rider):"회선 제거";color=connected?BattleController.RiderColor(rider):new Color(.5f,.6f,.66f);mark=Ui.Line("회선 적용",root,new Vector2(point.x-140,point.y),new Vector2(point.x+140,point.y),color,7);}
   else if(card==Card.Firewall){point=CombatPresentation.FirewallPoint(owner,target);message="양방향 경로 차단";color=Ui.Gold;
    if(controller&&controller.State!=null)foreach(var gatePoint in CombatPresentation.FirewallEndpoints(controller.State,target))gates.Add(CombatPresentation.Firewall(root,gatePoint.x,gatePoint.y,0));
    else {gates.Add(CombatPresentation.Firewall(root,point.x,292,0));gates.Add(CombatPresentation.Firewall(root,point.x,686,0));}
    wall=gates[0];mark=Ui.Box("방화벽 점등",root,point.x-45,point.y-29,90,58,Color.clear,false,3);}
   else {point=new Vector2(982,873);message="손패 보충";mark=Ui.Line("손패 보충",root,new Vector2(742,866),new Vector2(1222,866),color,3);}
   if(controller&&controller.State!=null&&controller.presentation){
    var st=controller.State;var plan=new EventFx.Plan();
    if(card==Card.Inject||card==Card.ZeroDay){
     var packet=st.Route(owner,target);var pts=BattleController.Points(packet);
     // load (05_drop.js atk): the chosen effect along the packet's route, my server -> arrival server. 지금 빛 띠 = ring at 26S from the start
     // (10S -> 34S), the path from .15 s, the arrival ring from .6 s (NowPlan, the same numbers as before)
     var scene=ActivateFx.LoadScene(pts,owner,target,packet.End,card==Card.ZeroDay);
     if(EffectPick==ActivateFx.Now){var now=ActivateFx.NowPlan(scene);plan.Halos.AddRange(now.Halos);plan.Rings.AddRange(now.Rings);}
     else StartEffect(scene,0);
    }else if(card==Card.Reroute){
     // link / unlink: the rung flashes (white / #ff8b9b), its two contacts ring (14 -> 46), and only the paths whose
     // arrival changed because of it glow in their side's color, each with its arrival server ring from 0.55 s.
     int slot=Battle.Slot(target),row=slot/2,edge=slot%2;var after=new List<Packet>();var before=new List<Packet>();
     for(int who=0;who<2;who++)for(int lane=0;lane<3;lane++)after.Add(st.Route(who,lane));
     int keepRung=st.Rungs[row],keepRider=st.RungRiders[row];
     if(connected)st.Rungs[row]=-1;else{st.Rungs[row]=edge;st.RungRiders[row]=st.LastRemovedRider;}
     for(int who=0;who<2;who++)for(int lane=0;lane<3;lane++)before.Add(st.Route(who,lane));
     st.Rungs[row]=keepRung;st.RungRiders[row]=keepRider;
     Vector2 x1=new Vector2(BattleController.LaneX[edge],BattleController.RowY[row]),x2=new Vector2(BattleController.LaneX[edge+1],BattleController.RowY[row]);
     plan.Flashes.Add((x1,x2,connected?Color.white:new Color(1,.545f,.608f)));plan.Rings.Add((x1,EventFx.Core,14*EventFx.S,46*EventFx.S,0));plan.Rings.Add((x2,EventFx.Core,14*EventFx.S,46*EventFx.S,0));
     int n=0;for(int i=0;i<after.Count;i++)if(after[i].End!=before[i].End&&st.Health[after[i].Owner,after[i].Start]>0){ // an offline server cannot fire: no glow for its lane (user 11-22)
     var pts=BattleController.Points(after[i]);plan.Halos.Add((pts,EventFx.Side(after[i].Owner),.12f+n*.06f));plan.Rings.Add((pts[pts.Count-1],EventFx.Side(after[i].Owner),28,52,.55f));n++;}
     mark.color=Color.clear;mark.gameObject.SetActive(false);
     // 13주차 11_fxpick.js / 05_drop.js (사용자 "사다리 없에거나 생기는 건 이펙트를 안바꿨네"): the chosen effect along the rung segment
     // (left node -> right node of that row) with LINK / CUT ROW n and the reroute terminal. The flash and changed-route glow above stay.
     var scene=ActivateFx.RungScene(slot,!connected,owner);
     if(EffectPick==ActivateFx.Now){var now=ActivateFx.NowPlan(scene);plan.Halos.AddRange(now.Halos);plan.Rings.AddRange(now.Rings);}
     else StartEffect(scene,0);
     // rungDraw 'cut': the removed rung stays .45 s, then breaks into 8 pieces (in the removed rung's rider color)
     if(!connected)controller.presentation.StartCoroutine(ActivateFx.RungCut(controller.presentation.BoardEffectUnderServers("가로선 제거"),x1,x2,BattleController.RiderColor(st.LastRemovedRider),()=>Delta));
    }
    if(!plan.Empty)controller.presentation.StartCoroutine(EventFx.Run(controller.presentation.BoardEffectUnderServers("이벤트 빛"),plan,()=>Delta));
   }
   var label=Ui.Text("적용 결과",root,point.x-110,point.y-(wall?85:63),220,30,message,font,19,color);label.alignment=TextAlignmentOptions.Center;
   float span=glitchHex?.6f:.5f;
   Phase="applied";for(float t=0;t<1;t+=Delta/span){Progress=t;float a=1-t*t;
    if(glitchHex){float v=t*span;a=v<.5f?1-(v/.5f)*(v/.5f):0;glitchHex.Begin();ActivateFx.HexGlitch(glitchHex,point,v,color);glitchHex.End();}if(wall){float close=Ease(Mathf.InverseLerp(.22f,.52f,t));foreach(var gate in gates)CombatPresentation.CloseFirewall(gate,close);if(t<.52f)a=0;}mark.color=new Color(color.r,color.g,color.b,a);label.color=new Color(color.r,color.g,color.b,a);yield return null;}
   Destroy(root.gameObject);if(glitchLayer)Destroy(glitchLayer.gameObject);Phase="idle";
  }
  static IEnumerator Owned(IEnumerator routine,Texture2D shot){yield return routine;if(shot)Destroy(shot);}
  // Start the picked effect (not 지금 빛 띠, which callers add to their EventFx plan) on the presentation; the glitch needs a still of the board.
  void StartEffect(ActivateFx.Scene scene,float delay){
   var controller=GetComponent<BattleController>();if(!controller||!controller.presentation)return;var p=controller.presentation;int pick=EffectPick;if(pick==ActivateFx.Now)return;
   Texture2D shot=pick==ActivateFx.Glitch&&Rate<BattleController.SkipSpeed?Grab():null;
   p.StartCoroutine(Owned(ActivateFx.Run(pick,p.BoardEffectUnderServers("카드 발동 이펙트"),p.BoardEffect("카드 발동 - 판 밖 어둡게"),scene,shot,font,()=>Delta,delay),shot));
  }
  // 13주차 11_fxpick.js 'fire' (fireBeam: 효과가 길을 먼저 그린 뒤 빔): CombatPresentation.OnFireRoute is invoked once per firing packet at the
  // charge start. The packets of one volley are gathered for a frame and get one effect each along their route; the volley shares one
  // board still (glitch), the board-wide parts (glitch board copies, stream rain) and one terminal per side. The mockup's effect starts .3 s
  // after the charge and the beam leaves .35 s after the effect start, so u = 0 at max(0, charge - .35). Enemy shots use the red side color.
  // The resolve already darkens the screen (CombatPresentation FxDim), so the fire effect adds no outside-the-board dark.
  readonly List<(List<Vector2> pts,int owner)> fires=new List<(List<Vector2>,int)>();float fireCharge;bool fireQueued;
  void OnEnable(){CombatPresentation.OnFireRoute+=OnFire;}
  void OnDisable(){CombatPresentation.OnFireRoute-=OnFire;fires.Clear();fireQueued=false;}
  void OnFire(List<Vector2> pts,int owner,float charge){
   if(!isActiveAndEnabled||pts==null||pts.Count<2)return;fires.Add((new List<Vector2>(pts),owner));fireCharge=charge;
   if(!fireQueued){fireQueued=true;StartCoroutine(FireVolley());}
  }
  IEnumerator FireVolley(){
   yield return null;fireQueued=false;var volley=fires.ToList();fires.Clear();
   var controller=GetComponent<BattleController>();if(!controller||!controller.presentation||volley.Count==0)yield break;var p=controller.presentation;
   int pick=EffectPick;float delay=Mathf.Max(0,fireCharge-.35f);
   System.Func<float> delta=()=>controller.Paused?0:Time.unscaledDeltaTime*Mathf.Max(.1f,controller.PlaybackSpeed)*FxClock.Scale; // CombatPresentation.Delta
   if(pick==ActivateFx.Now){var plan=new EventFx.Plan();foreach(var f in volley){var now=ActivateFx.NowPlan(ActivateFx.FireScene(f.pts,f.owner));plan.Halos.AddRange(now.Halos);plan.Rings.AddRange(now.Rings);}
    p.StartCoroutine(Delayed(()=>EventFx.Run(p.BoardEffectUnderServers("이벤트 빛 - 발사 효과"),plan,delta),delay,delta));yield break;}
   Texture2D shot=pick==ActivateFx.Glitch&&controller.PlaybackSpeed<BattleController.SkipSpeed?Grab():null;int running=0;bool lead=true;float termY=400;
   foreach(int side in volley.Select(f=>f.owner).Distinct().ToList()){
    var mine=volley.Where(f=>f.owner==side).ToList();bool first=true;
    foreach(var f in mine){
     var scene=ActivateFx.FireScene(f.pts,f.owner);scene.Dim=false;scene.Board=lead;lead=false;
     // 11-27 Unity 확인: 발사 중 터미널 창 두 개가 확대된 힘겨루기 화면을 크게 가려 발사에서는 띄우지 않음(장전·가로선에서는 그대로)
     if(false&&first){int s0=Lane(f.pts[0].x),e0=Lane(f.pts[f.pts.Count-1].x);
      scene.Term=new[]{"> fire --ch "+string.Join(",",mine.Select(m=>Lane(m.pts[0].x)+1).OrderBy(v=>v)),"  route  CH"+(s0+1)+" → "+(side==0?"ENEMY":"MY")+" CH"+(e0+1),mine.Count==1?"  packet  sent":"  packets  "+mine.Count+"  sent","  [ OK ]"};
      scene.TermAt=new Vector2(1040,termY);termY+=170;first=false;}
     else scene.Term=null;
     running++;p.StartCoroutine(Then(ActivateFx.Run(pick,p.BoardEffectUnderServers("발사 효과"),p.BoardEffect("발사 효과 - 글자"),scene,shot,font,delta,delay),()=>{running--;if(running==0&&shot)Destroy(shot);}));
    }
   }
   if(running==0&&shot)Destroy(shot);
  }
  static int Lane(float x){int best=0;for(int i=1;i<3;i++)if(Mathf.Abs(x-BattleController.LaneX[i])<Mathf.Abs(x-BattleController.LaneX[best]))best=i;return best;}
  static IEnumerator Then(IEnumerator routine,System.Action done){yield return routine;done();}
  static IEnumerator Delayed(System.Func<IEnumerator> make,float delay,System.Func<float> delta){for(float t=0;t<delay;t+=delta())yield return null;yield return make();}
  // Render the canvas once through the main camera into an opaque texture. Reading the back buffer
  // (ScreenCapture) returned black or alpha-0 frames in the player, so the copy is drawn directly.
  Texture2D Grab(){
   var canvas=layer.GetComponentInParent<Canvas>().rootCanvas;var camera=Camera.main;if(!canvas||!camera)return null;
   var rt=new RenderTexture(1280,720,24);var mode=canvas.renderMode;var cam=canvas.worldCamera;float plane=canvas.planeDistance;
   canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();
   var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply(false);
   RenderTexture.active=old;camera.targetTexture=null;canvas.renderMode=mode;canvas.worldCamera=cam;canvas.planeDistance=plane;rt.Release();Destroy(rt);return image;
  }
  static RawImage Layer(Transform parent,string name,Texture texture,Color color){var r=Ui.Rect(name,parent,0,0,1920,1080);var image=r.gameObject.AddComponent<RawImage>();image.texture=texture;image.color=color;image.raycastTarget=false;return image;}
  static Material screenBlend;
  static Material ScreenBlend {get{if(!screenBlend){var shader=Resources.Load<Shader>("UIScreenBlend");if(shader)screenBlend=new Material(shader);}return screenBlend;}}
  // VHS turn change — port of HTML tab 5 (sec-vhs, vfx "rgb", length 1.1 s). The HTML stage is 1280 x 720; the game is
  // 1920 x 1080, so offsets and the center band scale by 1.5.
  //  k = 1 - |p - 0.5| * 2; red copy at -40k, cyan copy at +40k (screen blend, opacity 0.75k);
  //  8 slices: top rnd(tick,i,1)*90 %, height 3 + rnd(tick,i,2)*12 %, shifted (rnd(tick,i,3)-0.5)*160k, shown while k > 0.15;
  //  whole stage shakes on x only by (rnd(tick,9)-0.5)*10k; tick = floor(u*30). At p = 0.5 the next screen, already
  //  carrying the center band, replaces the copies. After the change the band holds 1.2 s and fades in 0.4 s.
  public IEnumerator Turn(int phase,int actor,int attacker,System.Action atPeak=null){
   if(phase<2){ActionTurn++;TurnPick=ActivateFx.Choose(ActionTurn,TurnPick);forced=-1;}
   var root=Root("턴 전환");root.SetAsLastSibling();Color color=phase==2?new Color(1,.82f,.4f):actor==0?new Color(.22f,.851f,1):new Color(1,.294f,.4f);
   string title=phase==2?"RESOLVE":phase==0?"ATTACK":"DEFEND";
   string detail=(phase==2?"동시 발동":phase==0?"공격 턴":"방어 턴")+" · "+(phase==2?"양쪽":actor==0?"나":"상대");
   var header=Ui.Box("현재 단계 점등",root,161+544*phase,23,536,36,Color.clear,false,2);
   var crt=FindFirstObjectByType<CrtOverlay>();if(crt)crt.SetBurst(0);bool changed=false;bool fast=Rate>=BattleController.SkipSpeed;
   var vhs=Ui.Rect("VHS 화면 겹",root,0,0,1920,1080);Texture2D shot=null;var layers=new List<RawImage>();
   var band=Ui.Rect("턴 설명 띠",root,0,0,1920,1080);band.gameObject.SetActive(false);
   var bandBox=Ui.Box("띠 바탕",band,0,300*1.5f,1920,110*1.5f,new Color(.02f,.031f,.051f,.8f));
   // SVG text baselines y=372 (54 px) and y=398 (18 px): centers at about baseline - 0.35 em, x1.5.
   var big=Ui.Text("턴 이름",band,0,(372-.35f*54)*1.5f-45,1920,90,title,font,54*1.5f,color);big.alignment=TextAlignmentOptions.Center;var host2=GetComponent<BattleController>();if(host2&&host2.titleFont)big.font=host2.titleFont;big.fontStyle=FontStyles.Bold;big.characterSpacing=6*100/54f;
   var small=Ui.Text("턴 설명",band,0,(398-.35f*18)*1.5f-17,1920,34,detail,font,18*1.5f,new Color(.769f,.898f,.937f));small.alignment=TextAlignmentOptions.Center;
   if(!fast)shot=Grab();
   if(shot){var mat=ScreenBlend;
    layers.Add(Layer(vhs,"붉은 겹",shot,new Color(1,.25f,.3f,0)));layers.Add(Layer(vhs,"청록 겹",shot,new Color(.2f,.9f,1,0)));if(mat){layers[0].material=mat;layers[1].material=mat;}
    for(int i=0;i<8;i++)layers.Add(Layer(vhs,"찢김 띠 "+i,shot,new Color(1,1,1,0)));}
   Phase="turn";float started=0;
   for(float t=0;t<1;t+=Delta/1.1f){Progress=t;float k=1-Mathf.Abs(Mathf.Clamp01(t)-.5f)*2;started+=Delta;
    if(!changed&&t>=.5f){changed=true;atPeak?.Invoke();band.gameObject.SetActive(true);header.color=color;
     if(shot){vhs.gameObject.SetActive(false);var next=Grab();vhs.gameObject.SetActive(true);if(next){Destroy(shot);shot=next;foreach(var l in layers)l.texture=shot;}}}
    if(shot){int tick=Mathf.FloorToInt(started*30);
     At(layers[0].rectTransform,new Vector2(-40*1.5f*k,0));layers[0].color=new Color(1,.25f,.3f,.75f*k*.8f);
     At(layers[1].rectTransform,new Vector2(40*1.5f*k,0));layers[1].color=new Color(.2f,.9f,1,.75f*k*.8f);
     for(int i=0;i<8;i++){var strip=layers[2+i];float y0=TitleBBackdrop.Rnd(tick,i,1)*.9f,h=.03f+TitleBBackdrop.Rnd(tick,i,2)*.12f;h=Mathf.Min(h,1-y0);
      At(strip.rectTransform,new Vector2((TitleBBackdrop.Rnd(tick,i,3)-.5f)*160*1.5f*k,y0*1080));strip.rectTransform.sizeDelta=new Vector2(1920,h*1080);strip.uvRect=new Rect(0,1-y0-h,1,h);strip.color=new Color(1,1,1,k>.15f?1:0);}
     vhs.anchoredPosition=new Vector2((TitleBBackdrop.Rnd(tick,9)-.5f)*10*1.5f*k,0);}
    yield return null;
   }
   if(!changed){atPeak?.Invoke();band.gameObject.SetActive(true);}
   Destroy(vhs.gameObject);if(shot)Destroy(shot);band.gameObject.SetActive(true);header.color=color;Phase="turn-hold";
   for(float t=0;t<1;t+=Time.unscaledDeltaTime*(Rate>=BattleController.SkipSpeed?BattleController.SkipSpeed:(host?host.HoldFactor:1))/1.2f){Progress=t;yield return null;}
   for(float t=0;t<1;t+=Time.unscaledDeltaTime*(Rate>=BattleController.SkipSpeed?BattleController.SkipSpeed:(host?host.HoldFactor:1))/.4f){float a=1-t;header.color=new Color(color.r,color.g,color.b,a);big.color=new Color(color.r,color.g,color.b,a);small.color=new Color(.769f,.898f,.937f,a);bandBox.color=new Color(.02f,.031f,.051f,.8f*a);yield return null;}
   Destroy(root.gameObject);Phase="idle";
  }
  public IEnumerator Impact(Vector2 point,string caption,Color color){var root=Root("패킷 충돌");var ring=Ui.Box("충돌 섬광",root,point.x-22,point.y-22,44,44,color,false,3,6);var label=Ui.Text("충돌 결과",root,point.x-95,point.y-50,190,26,caption,font,17,color);label.alignment=TextAlignmentOptions.Center;for(float t=0;t<1;t+=Delta/.3f){ring.color=label.color=new Color(color.r,color.g,color.b,1-t);float size=44+t*24;At(ring.rectTransform,point-Vector2.one*size/2);ring.rectTransform.sizeDelta=Vector2.one*size;yield return null;}Destroy(root.gameObject);}
 }
}

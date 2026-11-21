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
  public static readonly Rect Socket=new Rect(223,381,228,236);
  public static Rect DockRect {get {float h=Socket.height-16,w=h/1.6f;return new Rect(Socket.center.x-w/2,Socket.y+8,w,h);}}
  static readonly Vector2[][] Routes={
   new[]{new Vector2(448,480),new Vector2(532,480),new Vector2(548,480),new Vector2(548,385),new Vector2(582,385)},
   new[]{new Vector2(448,498),new Vector2(532,498),new Vector2(560,498),new Vector2(560,423),new Vector2(582,423)},
   new[]{new Vector2(448,516),new Vector2(532,516),new Vector2(550,516),new Vector2(550,561),new Vector2(582,561)}
  };
  BattleController host;
  // While the controller is skipping, every phase below runs to its end in one frame.
  float Rate {get{if(!host)host=GetComponent<BattleController>();return host&&host.Skipping?BattleController.SkipSpeed:speed;}}
  float Delta {get{return Time.deltaTime*Mathf.Max(.1f,Rate);}}
  static float Ease(float t){return t*t*(3-2*t);}
  RectTransform Root(string name){return Ui.Rect(name,layer,0,0,1920,1080);}
  void At(RectTransform r,Vector2 p){r.anchoredPosition=new Vector2(p.x,-p.y);}
  Image Picture(string name,Transform parent,Sprite sprite,Rect rect){var r=Ui.Rect(name,parent,rect.x,rect.y,rect.width,rect.height);var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;return image;}
  public IEnumerator Play(Card card,int owner,Vector2 origin,Vector2 originSize){
   Color color=owner==0?Ui.Cyan:Ui.Red;
   Sprite sprite=(owner==1||Cards.Enhanced(card))?backImage:cardImages[(int)card];
   var root=Root("카드 사용 - 안착 분해 전송");Rect dock=DockRect;
   var chip=Picture("사용 카드",root,sprite,new Rect(origin,originSize));var rect=chip.rectTransform;
   Phase="move";
   for(float t=0;t<1;t+=Delta/.35f){Progress=t;float e=Ease(t);At(rect,Vector2.Lerp(origin,dock.position,e));rect.sizeDelta=Vector2.Lerp(originSize,dock.size,e);yield return null;}
   At(rect,dock.position);rect.sizeDelta=dock.size;
   var clamp=Ui.Box("소켓 고정",root,dock.x-3,dock.y-3,dock.width+6,dock.height+6,color,false,2);
   for(int side=0;side<2;side++)for(int pin=0;pin<12;pin++){
    float edge=side==0?dock.x:dock.xMax,direction=side==0?-1:1,y=dock.y+25+pin*15;
    Vector2 a=new Vector2(edge,y),b=a+new Vector2(direction*(7+pin%3*3),0),c=b+new Vector2(direction*5,(pin%2==0?1:-1)*5),d=c+new Vector2(direction*7,0);
    Color faint=new Color(color.r,color.g,color.b,.2f);
    Ui.Line("소켓 미세 회로",root,a,b,faint,.7f);Ui.Line("소켓 미세 회로 굴절",root,b,c,faint,.7f);Ui.Line("소켓 미세 회로 끝",root,c,d,faint,.7f);
    Ui.Box("회로 접점",root,d.x-1,d.y-1,2,2,new Color(color.r,color.g,color.b,.3f),false,.6f,12);
   }
   Phase="dock";for(float t=0;t<1;t+=Delta/.25f){Progress=t;clamp.color=Color.Lerp(Color.white,color,t);yield return null;}

   // Scan (2026-11-21 selection): a light bar sweeps the docked card top to bottom in 0.8 s and the part it
   // has passed becomes 7 circuit lines that gather at the socket exit. The hold circuit fades in the first 60%.
   var hold=GetComponent<CardHoldFx>();if(hold)hold.Hide(.48f/Mathf.Max(.1f,Rate));
   chip.type=Image.Type.Filled;chip.fillMethod=Image.FillMethod.Vertical;chip.fillOrigin=(int)Image.OriginVertical.Bottom;
   var barGlow=Ui.Box("스캔 빛 번짐",root,dock.x-16,dock.y-8,dock.width+32,16,new Color(color.r,color.g,color.b,.35f));
   var bar=Ui.Box("스캔 빛 막대",root,dock.x-10,dock.y-1.5f,dock.width+20,3,Color.white);
   var lines=new List<Shape>();
   for(int k=0;k<7;k++){float x=dock.x+(k+.5f)*dock.width/7;var line=Ui.Line("스캔 회로선",root,new Vector2(x,dock.y),new Vector2(x,dock.yMax),new Color(color.r,color.g,color.b,.9f),1.6f);line.rectTransform.sizeDelta=new Vector2(1,1.6f);lines.Add(line);}
   Phase="fragment";
   for(float t=0;t<1;t+=Delta/.8f){Progress=t;float y=dock.y+dock.height*t;chip.fillAmount=1-t;At(bar.rectTransform,new Vector2(dock.x-10,y-1.5f));At(barGlow.rectTransform,new Vector2(dock.x-16,y-8));
    foreach(var line in lines)line.rectTransform.sizeDelta=new Vector2(Mathf.Max(1,y-dock.y),1.6f);yield return null;}
   chip.gameObject.SetActive(false);bar.gameObject.SetActive(false);barGlow.gameObject.SetActive(false);
   foreach(var line in lines)line.rectTransform.sizeDelta=new Vector2(dock.height,1.6f);
   var exit=Routes[1][0];var gather=new List<Shape>();
   for(int k=0;k<7;k++){float x=dock.x+(k+.5f)*dock.width/7;gather.Add(Ui.Line("회로선 모임",root,new Vector2(x,dock.yMax),exit+new Vector2(0,(k-3)*3),new Color(color.r,color.g,color.b,.9f),1.6f));}
   var lengths=gather.Select(g=>g.rectTransform.sizeDelta.x).ToArray();
   for(float t=0;t<1;t+=Delta/.25f){for(int k=0;k<7;k++)gather[k].rectTransform.sizeDelta=new Vector2(lengths[k]*Ease(t),1.6f);yield return null;}
   for(int lane=0;lane<3;lane++)for(int seg=1;seg<Routes[lane].Length;seg++){
    Ui.Line("회로 번짐",root,Routes[lane][seg-1],Routes[lane][seg],new Color(color.r,color.g,color.b,.12f),lane==1?15:9);
    Ui.Line("회로 중심",root,Routes[lane][seg-1],Routes[lane][seg],new Color(color.r,color.g,color.b,.7f),lane==1?6:2);
   }
   // Activation, 0.9 s: signals run along the board-bound circuit bundles and flash the board-edge pads.
   Phase="absorb";var fade=root.GetComponentsInChildren<Shape>(true).ToArray();var start=fade.Select(f=>f.color.a).ToArray();
   System.Action<float> step=p=>{Progress=p;for(int i=0;i<fade.Length;i++)if(fade[i]){var c=fade[i].color;c.a=start[i]*(1-p);fade[i].color=c;}};
   if(hold)yield return hold.Activate(()=>Delta,step);else for(float t=0;t<.9f;t+=Delta){step(t/.9f);yield return null;}
   Destroy(root.gameObject);
   Phase="idle";Progress=0;
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
  // Event light (2026-11-21 selection): the whole path plus its arrival server. The path spreads from its
  // start in 0.45 s, blinks twice (on 0.10, 15% 0.08, on 0.10), then fades from 90% over 1.6 s.
  // The arrival ring grows 28 -> 52 px in 0.5 s, 0.55 s after the start. Runs beside the card result.
  public IEnumerator EventLight(Transform parent,List<List<Vector2>> routes,Color color,bool rings=true){
   var root=Ui.Rect("이벤트 빛",parent,0,0,1920,1080);var shapes=new List<Shape>();var from=new List<float>();var lengths=new List<float>();var weight=new List<float>();var ends=new List<Shape>();
   Color core=Color.Lerp(color,Color.white,.55f);
   foreach(var pts in routes){float acc=0;
    for(int i=1;i<pts.Count;i++){float len=Vector2.Distance(pts[i-1],pts[i]);if(len<.5f)continue;
     foreach(var (w,a) in new[]{(30f,.2f),(10f,.35f),(3f,1f)}){var line=Ui.Line("경로 빛",root,pts[i-1],pts[i],new Color(color.r,color.g,color.b,0),w);if(w==3)line.color=new Color(core.r,core.g,core.b,0);shapes.Add(line);from.Add(acc);lengths.Add(len);weight.Add(a);}
     acc+=len;}
    if(rings){var end=pts[pts.Count-1];ends.Add(Ui.Box("도착 서버 고리",root,end.x-28,end.y-28,56,56,new Color(color.r,color.g,color.b,0),false,3,32));}
   }
   float total=routes.Count==0?0:routes.Max(r=>{float l=0;for(int i=1;i<r.Count;i++)l+=Vector2.Distance(r[i-1],r[i]);return l;});
   for(float t=0;t<2.33f;t+=Delta){if(!root)yield break;
    float spread=total*Ease(Mathf.Clamp01(t/.45f));
    float a=t<.45f?1:t<.55f?1:t<.63f?.15f:t<.73f?1:.9f*(1-Mathf.Clamp01((t-.73f)/1.6f));
    for(int i=0;i<shapes.Count;i++){var r=shapes[i].rectTransform;r.sizeDelta=new Vector2(Mathf.Clamp(spread-from[i],0,lengths[i]),r.sizeDelta.y);var c=shapes[i].color;c.a=a*weight[i];shapes[i].color=c;}
    float u=Mathf.Clamp01((t-.55f)/.5f);foreach(var ring in ends){float size=2*Mathf.Lerp(28,52,Ease(u));var rr=ring.rectTransform;var center=new Vector2(rr.anchoredPosition.x+rr.sizeDelta.x/2,-rr.anchoredPosition.y+rr.sizeDelta.y/2);rr.sizeDelta=Vector2.one*size;At(rr,center-Vector2.one*size/2);ring.color=new Color(color.r,color.g,color.b,t<.55f?0:1-u);}
    yield return null;}
   if(root)Destroy(root.gameObject);
  }
  public IEnumerator Applied(Card card,int owner,int target,bool connected){
   var controller=GetComponent<BattleController>();
   bool onBoard=card!=Card.Buffer&&card!=Card.Overflow;
   var root=onBoard&&controller&&controller.presentation?controller.presentation.BoardEffect("카드 적용 결과"):Root("카드 적용 결과");Color color=owner==0?Ui.Cyan:Ui.Red;Vector2 point;string message;Shape mark;RectTransform wall=null;var gates=new List<RectTransform>();
   if(card==Card.Inject||card==Card.ZeroDay){point=new Vector2(BattleController.LaneX[target],owner==0?777:201);message=card==Card.ZeroDay?"관통 패킷 장전":"+1 장전";mark=Ui.Box("대상 서버 점등",root,point.x-38,point.y-38,76,76,color,false,4,6);}
   else if(card==Card.Reroute){int slot=Battle.Slot(target);point=new Vector2((BattleController.LaneX[slot%2]+BattleController.LaneX[slot%2+1])/2,BattleController.RowY[slot/2]);var rider=controller&&controller.State!=null?controller.State.RungRiders[slot/2]:-1;message=connected?"회선 연결 · "+BattleController.RiderName(rider):"회선 제거";color=connected?BattleController.RiderColor(rider):new Color(.5f,.6f,.66f);mark=Ui.Line("회선 적용",root,new Vector2(point.x-140,point.y),new Vector2(point.x+140,point.y),color,7);}
   else if(card==Card.Firewall){point=CombatPresentation.FirewallPoint(owner,target);message="양방향 경로 차단";color=Ui.Gold;
    if(controller&&controller.State!=null)foreach(var gatePoint in CombatPresentation.FirewallEndpoints(controller.State,target))gates.Add(CombatPresentation.Firewall(root,gatePoint.x,gatePoint.y,0));
    else {gates.Add(CombatPresentation.Firewall(root,point.x,292,0));gates.Add(CombatPresentation.Firewall(root,point.x,686,0));}
    wall=gates[0];mark=Ui.Box("방화벽 점등",root,point.x-45,point.y-29,90,58,Color.clear,false,3);}
   else {point=new Vector2(982,873);message="손패 보충";mark=Ui.Line("손패 보충",root,new Vector2(742,866),new Vector2(1222,866),color,3);}
   if(controller&&controller.State!=null&&controller.presentation){
    var routes=new List<List<Vector2>>();Color lightColor=color;
    if(card==Card.Inject||card==Card.ZeroDay)routes.Add(BattleController.Points(controller.State.Route(owner,target)));
    else if(card==Card.Reroute&&connected){int slot=Battle.Slot(target),row=slot/2,edge=slot%2;
     for(int who=0;who<2;who++)for(int lane=0;lane<3;lane++)if(controller.State.Power[who,lane]>0){var packet=controller.State.Route(who,lane);if(packet.Cells.Any(cell=>cell.Row==row&&(cell.Lane==edge||cell.Lane==edge+1)))routes.Add(BattleController.Points(packet));}
     if(routes.Count==0)routes.Add(new List<Vector2>{new Vector2(BattleController.LaneX[edge],BattleController.RowY[row]),new Vector2(BattleController.LaneX[edge+1],BattleController.RowY[row])});}
    if(routes.Count>0)StartCoroutine(EventLight(controller.presentation.BoardEffect("이벤트 빛"),routes,lightColor,card!=Card.Reroute||routes.Count>0&&routes[0].Count>2));
   }
   var label=Ui.Text("적용 결과",root,point.x-110,point.y-(wall?85:63),220,30,message,font,19,color);label.alignment=TextAlignmentOptions.Center;
   Phase="applied";for(float t=0;t<1;t+=Delta/.5f){Progress=t;float a=1-t*t;if(wall){float close=Ease(Mathf.InverseLerp(.22f,.52f,t));foreach(var gate in gates)CombatPresentation.CloseFirewall(gate,close);if(t<.52f)a=0;}mark.color=new Color(color.r,color.g,color.b,a);label.color=new Color(color.r,color.g,color.b,a);yield return null;}
   Destroy(root.gameObject);Phase="idle";
  }
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
  // VHS turn change (2026-11-21 selection): RGB split + horizontal tearing, 1.1 s, strongest at the middle
  // where the screen changes. Then a center band holds the big turn name and a Korean line for 1.2 s.
  public IEnumerator Turn(int phase,int actor,int attacker,System.Action atPeak=null){
   var root=Root("턴 전환");root.SetAsLastSibling();Color color=phase==2?Ui.Gold:actor==0?Ui.Cyan:Ui.Red;
   string title=phase==2?"RESOLVE":phase==0?"ATTACK":"DEFENSE";
   string detail=phase==2?"동시 발동 · 장전된 패킷이 한꺼번에 날아갑니다":(actor==0?"내 ":"상대 ")+(phase==0?"공격 턴 · ":"방어 턴 · ")+(phase==0?(actor==0?"패킷을 장전하고 길을 바꾸세요":"상대가 패킷을 장전합니다"):(actor==0?"막을 길을 고르세요":"상대가 막을 길을 고릅니다"));
   var header=Ui.Box("현재 단계 점등",root,161+544*phase,23,536,36,Color.clear,false,2);
   var crt=FindFirstObjectByType<CrtOverlay>();bool changed=false;bool fast=Rate>=BattleController.SkipSpeed;
   var vhs=Ui.Rect("VHS 화면 겹",root,0,0,1920,1080);Texture2D shot=null;var layers=new List<RawImage>();
   if(!fast)shot=Grab();if(shot){
    layers.Add(Layer(vhs,"원본",shot,Color.white));layers.Add(Layer(vhs,"청록 겹",shot,new Color(0,1,1,0)));layers.Add(Layer(vhs,"붉은 겹",shot,new Color(1,0,.25f,0)));
    for(int i=0;i<8;i++)layers.Add(Layer(vhs,"찢김 띠 "+i,shot,new Color(1,1,1,0)));}
   Phase="turn";
   for(float t=0;t<1;t+=Delta/1.1f){Progress=t;float k=Mathf.Sin(Mathf.PI*Mathf.Clamp01(t));if(crt)crt.SetBurst(k);
    if(!changed&&t>=.5f){changed=true;atPeak?.Invoke();
     if(shot){vhs.gameObject.SetActive(false);header.gameObject.SetActive(false);if(crt)crt.SetBurst(0);var next=Grab();if(crt)crt.SetBurst(k);vhs.gameObject.SetActive(true);header.gameObject.SetActive(true);if(next){Destroy(shot);shot=next;foreach(var l in layers)l.texture=shot;}}}
    if(shot){int tick=Mathf.FloorToInt(Time.unscaledTime*30);
     At(layers[1].rectTransform,new Vector2(-40*k,0));layers[1].color=new Color(0,1,1,.75f*k*.5f);
     At(layers[2].rectTransform,new Vector2(40*k,0));layers[2].color=new Color(1,0,.25f,.75f*k*.5f);
     for(int i=0;i<8;i++){var strip=layers[3+i];float h=1080*(.03f+.12f*Hash(tick*13+i)),y=(1080-h)*Hash(tick*7+i*31),dx=(Hash(tick*5+i*17)-.5f)*160*k;
      At(strip.rectTransform,new Vector2(dx,y));strip.rectTransform.sizeDelta=new Vector2(1920,h);strip.uvRect=new Rect(0,1-(y+h)/1080,1,h/1080);strip.color=new Color(1,1,1,k>.05f?1:0);}
     vhs.anchoredPosition=new Vector2((Hash(tick)-.5f)*10*k,(Hash(tick+1)-.5f)*10*k);}
    float a=changed?Ease(Mathf.InverseLerp(.5f,.75f,t)):0;header.color=new Color(color.r,color.g,color.b,a);yield return null;
   }
   if(!changed)atPeak?.Invoke();if(crt)crt.SetBurst(0);Destroy(vhs.gameObject);if(shot)Destroy(shot);
   var band=Ui.Box("턴 설명 띠",root,0,540-55,1920,110,new Color(.02f,.03f,.05f,.8f));
   var big=Ui.Text("턴 이름",root,0,540-52,1920,64,title,font,54,color);big.alignment=TextAlignmentOptions.Center;
   var small=Ui.Text("턴 설명",root,0,540+17,1920,28,detail,font,18,new Color(.82f,.9f,.94f));small.alignment=TextAlignmentOptions.Center;
   header.color=color;Phase="turn-hold";
   for(float t=0;t<1;t+=Time.unscaledDeltaTime*(Rate>=BattleController.SkipSpeed?BattleController.SkipSpeed:1)/1.2f){Progress=t;yield return null;}
   for(float t=0;t<1;t+=Time.unscaledDeltaTime*(Rate>=BattleController.SkipSpeed?BattleController.SkipSpeed:1)/.4f){float a=1-t;header.color=big.color=new Color(color.r,color.g,color.b,a);small.color=new Color(.82f,.9f,.94f,a);band.color=new Color(.02f,.03f,.05f,.8f*a);yield return null;}
   Destroy(root.gameObject);Phase="idle";
  }
  public IEnumerator Impact(Vector2 point,string caption,Color color){var root=Root("패킷 충돌");var ring=Ui.Box("충돌 섬광",root,point.x-22,point.y-22,44,44,color,false,3,6);var label=Ui.Text("충돌 결과",root,point.x-95,point.y-50,190,26,caption,font,17,color);label.alignment=TextAlignmentOptions.Center;for(float t=0;t<1;t+=Delta/.3f){ring.color=label.color=new Color(color.r,color.g,color.b,1-t);float size=44+t*24;At(ring.rectTransform,point-Vector2.one*size/2);ring.rectTransform.sizeDelta=Vector2.one*size;yield return null;}Destroy(root.gameObject);}
 }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 public sealed class CombatPresentation : MonoBehaviour {
  BattleController c;readonly List<RectTransform> visuals=new List<RectTransform>();
  readonly List<Vector2> origins=new List<Vector2>();RectTransform layer;
  public string Phase {get;private set;}="idle";
  public float Progress {get;private set;}
  public float CameraZoom {get;private set;}=1;
  public Vector2 CameraOffset {get;private set;}
  float Delta=>Time.unscaledDeltaTime*Mathf.Max(.1f,c.animationSpeed);
  public void Initialize(BattleController controller){
   c=controller;
   foreach(var background in c.backgrounds){
    var board=Ui.Rect("사다리 시각 카메라",background.transform,0,0,1920,1080);
    // The outer frame, background and channel headings stay fixed; only board contents follow the action.
    var nodes=background.transform.Cast<Transform>().OfType<RectTransform>().Where(t=>t!=board&&t.anchoredPosition.x>=680&&t.anchoredPosition.x+t.sizeDelta.x<=1360&&-t.anchoredPosition.y>=200&&-t.anchoredPosition.y+t.sizeDelta.y<=800).ToArray();
    foreach(var node in nodes)node.SetParent(board,false);Track(board);
    // The middle-row contacts are nested in the background frame in the Figma export.
    for(int id=270;id<=275;id++){var pin=BattleController.Find(background.transform,"Source_64_"+id) as RectTransform;if(!pin)continue;var position=pin.anchoredPosition+((RectTransform)pin.parent).anchoredPosition;pin.SetParent(board,false);pin.anchoredPosition=position;}
   }
   Track(c.rungs);Track(c.paths);foreach(var server in c.servers)Track((RectTransform)server.transform);
   layer=Ui.Rect("공격 연출 - 카메라 연동",c.game.transform,0,0,1920,1080);layer.SetAsLastSibling();Track(layer);
  }
  void Track(RectTransform r){visuals.Add(r);origins.Add(r.anchoredPosition);}
  public void ResetView(){for(int i=0;i<visuals.Count;i++)if(visuals[i]){visuals[i].anchoredPosition=origins[i];visuals[i].localScale=Vector3.one;}CameraZoom=1;CameraOffset=Vector2.zero;}
  void Frame(Vector2 focus,float weight,float trauma=0,float time=0){
   float zoom=1+.035f*weight;Vector2 f=new Vector2(focus.x,-focus.y),center=new Vector2(982,-487);
   Vector2 shift=f*(1-zoom)+(center-f)*(.05f*weight);
   // Keep visible server shells below the fixed headings and above the fixed hand.
   shift.y=Mathf.Clamp(shift.y,812*zoom-835,166*zoom-162);
   shift+=new Vector2(Mathf.Sin(time*43),Mathf.Sin(time*31)*.55f)*trauma;
   for(int i=0;i<visuals.Count;i++)if(visuals[i]){visuals[i].anchoredPosition=origins[i]*zoom+shift;visuals[i].localScale=Vector3.one*zoom;}
   CameraZoom=zoom;CameraOffset=shift;
  }
  static float Smooth(float p){p=Mathf.Clamp01(p);return p*p*(3-2*p);}
  static void At(RectTransform r,Vector2 p){r.anchoredPosition=new Vector2(p.x,-p.y);}
  public static FlareGraphic Flare(Transform parent,string name,Vector2 p,Color color,float size=1){
   var r=Ui.Rect(name,parent,p.x-95*size,p.y-26*size,190*size,52*size);var g=r.gameObject.AddComponent<FlareGraphic>();g.color=color;g.raycastTarget=false;return g;
  }
  public static RectTransform Firewall(Transform parent,float x,float y,float closed=1){
   var root=Ui.Rect("반도체 방화벽",parent,x-43,y-48,86,96);
   for(int side=0;side<2;side++){
    var part=Ui.Rect("방화벽 접점 "+side,root,side==0?-24*(1-closed):46+24*(1-closed),0,40,96);
    Ui.Box("칩 하우징",part,0,0,40,96,new Color(.10f,.09f,.035f));Ui.Box("하우징 외곽",part,0,0,40,96,Ui.Gold,false,1.5f);
    for(int pin=0;pin<6;pin++){float py=10+pin*15;Ui.Line("미세 회로",part,new Vector2(4,py),new Vector2(34,py),new Color(1,.77f,.22f,.55f),1);Ui.Box("핀",part,side==0?35:-1,py-2,6,4,Ui.Gold);}
   }
   Ui.Box("채널 잠금 칩",root,30,34,26,28,Ui.Gold,false,2);return root;
  }
  public static void CloseFirewall(RectTransform root,float amount){
   amount=Mathf.Clamp01(amount);((RectTransform)root.GetChild(0)).anchoredPosition=new Vector2(-24*(1-amount),0);((RectTransform)root.GetChild(1)).anchoredPosition=new Vector2(46+24*(1-amount),0);
   root.GetChild(2).gameObject.SetActive(amount>=.98f);
  }
  sealed class Shot {
   public Packet packet;public List<Vector2> path;public float split=1;public Vector2 contact;public bool contactEvent,piercing,triggered,arrived;
   public RectTransform root;public TMP_Text number;public Shape[] trails;public Vector2 position;
  }
  static float Fraction(List<Vector2> path,Vector2 point){float total=0,done=0;for(int j=1;j<path.Count;j++)total+=Vector2.Distance(path[j-1],path[j]);for(int j=1;j<path.Count;j++){float len=Vector2.Distance(path[j-1],path[j]);if(Mathf.Abs(Vector2.Distance(path[j-1],point)+Vector2.Distance(point,path[j])-len)<.1f)return (done+Vector2.Distance(path[j-1],point))/Mathf.Max(1,total);done+=len;}return 1;}
  Shot Make(Packet packet,Resolution report,Transform parent){
   var s=new Shot{packet=packet,path=BattleController.Points(packet)};s.contact=s.path.Last();
   if(packet.Blocked){
    // First entry into a blocked channel, not an arbitrary percentage of the flight.
    s.contact=s.path.First(p=>c.State.Walls[Array.IndexOf(BattleController.LaneX,p.x)]);s.contactEvent=true;
    if(s.contact==s.path[0])s.contact=Vector2.Lerp(s.path[0],s.path[1],.35f);
   }else {
    var opponent=report.Packets.FirstOrDefault(p=>p.Owner!=packet.Owner&&!p.Blocked&&(packet.Power<packet.Initial||p.Power<p.Initial)&&packet.Cells.Any(a=>p.Cells.Any(b=>a.Lane==b.Lane&&a.Row==b.Row)));
    if(opponent!=null){var cell=packet.Cells.Where(a=>opponent.Cells.Any(b=>a.Lane==b.Lane&&a.Row==b.Row)).OrderBy(a=>Math.Abs(a.Row-1)).ThenBy(a=>a.Lane).First();s.contact=new Vector2(BattleController.LaneX[cell.Lane],cell.Row<0?201:cell.Row>2?777:BattleController.RowY[cell.Row]);s.contactEvent=true;s.piercing=packet.Pierce!=opponent.Pierce;}
   }
   if(s.contactEvent)s.split=Fraction(s.path,s.contact);
   s.root=Ui.Rect("빛번짐 탄 - "+packet.Owner+" CH "+packet.Start,parent,0,0,1,1);Color ink=packet.Owner==0?Ui.Cyan:Ui.Red;
   s.trails=new Shape[10];for(int n=0;n<s.trails.Length;n++)s.trails[n]=Ui.Line("이동 잔광 "+n,parent,Vector2.zero,Vector2.right,new Color(ink.r,ink.g,ink.b,.3f),4);
   s.root.SetAsLastSibling();Flare(s.root,"가로 빛번짐",Vector2.zero,ink);
   Ui.Box("둥근 위력 핵",s.root,-10,-13,20,26,new Color(.91f,1,1),true,1,48);
   s.number=Ui.Text("핵 위 위력",s.root,-22,-18,44,36,packet.Initial.ToString(),c.font,20,new Color(.015f,.05f,.07f));s.number.alignment=TextAlignmentOptions.Midline;
   s.root.gameObject.SetActive(false);return s;
  }
  public IEnumerator Play(Resolution report){
   ResetView();var root=Ui.Rect("공격 흐름",layer,0,0,1920,1080);var shots=report.Packets.Select(p=>Make(p,report,root)).ToList();
   if(shots.Count==0){Destroy(root.gameObject);yield break;}
   Vector2 source=shots.Aggregate(Vector2.zero,(p,s)=>p+s.path[0])/shots.Count;
   var beams=new List<Shape>();var glows=new List<FlareGraphic>();
   foreach(var s in shots){Color ink=s.packet.Owner==0?Ui.Cyan:Ui.Red;for(int n=0;n<6;n++)beams.Add(Ui.Line("응축 광선",root,Vector2.zero,Vector2.right,ink,2));glows.Add(Flare(root,"발사구 응축",s.path[0],ink,.65f));}
   Phase="charge";
   for(float t=0;t<1;t+=Delta/.62f){Progress=t;float compression=t<.78f?Smooth(t/.78f):1;int k=0;
    for(int j=0;j<shots.Count;j++){var origin=shots[j].path[0];for(int n=0;n<6;n++){float a=n*Mathf.PI/3;Vector2 dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),p=origin+dir*Mathf.Lerp(67,19,compression);var beam=beams[k++];At(beam.rectTransform,p);beam.rectTransform.localRotation=Quaternion.Euler(0,0,-a*Mathf.Rad2Deg+180);beam.rectTransform.sizeDelta=new Vector2(8+16*compression,1+compression);beam.color=new Color(beam.color.r,beam.color.g,beam.color.b,.25f+.75f*compression);}glows[j].color=new Color(glows[j].color.r,glows[j].color.g,glows[j].color.b,compression*.55f);}
    Frame(source,Smooth(t));yield return null;
   }
   foreach(var b in beams)Destroy(b.gameObject);foreach(var g in glows)Destroy(g.gameObject);
   foreach(var s in shots){StartCoroutine(Flash(root,s.path[0],s.packet.Owner==0?Ui.Cyan:Ui.Red));c.servers.First(v=>v.owner==s.packet.Owner&&v.channel==s.packet.Start).Render(report.Before[s.packet.Owner,s.packet.Start],0);}
   Phase="flight";float impulse=0;
   for(float t=0;;t=Mathf.Min(1,t+Delta/1.55f)){Progress=t;Vector2 focus=Vector2.zero;int count=0;
    foreach(var s in shots){float p=s.contactEvent?(t<.52f?Mathf.Lerp(0,s.split,t/.52f):Mathf.Lerp(s.split,1,(t-.52f)/.48f)):t;s.position=CardTransferFx.Along(s.path,p);
     if(s.contactEvent&&!s.triggered&&t>=.52f){s.triggered=true;impulse=Mathf.Max(impulse,s.piercing?0:s.packet.Blocked?4:3);StartCoroutine(Impact(root,s.contact,s.packet.Blocked?"채널 차단":s.piercing?"관통":"공격 상쇄",Ui.Gold));s.number.text=s.packet.Power.ToString();}
     bool visible=!(s.triggered&&s.packet.Power==0);s.root.gameObject.SetActive(visible);At(s.root,s.position);
     for(int n=0;n<s.trails.Length;n++){var trail=s.trails[n];float q=p-.012f*n;trail.gameObject.SetActive(visible&&q>0);Vector2 a=CardTransferFx.Along(s.path,Mathf.Max(0,q-.012f)),b=CardTransferFx.Along(s.path,Mathf.Max(0,q));var d=b-a;At(trail.rectTransform,a);trail.rectTransform.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);trail.rectTransform.sizeDelta=new Vector2(d.magnitude,3+(1-n/10f)*4);Color ink=s.packet.Owner==0?Ui.Cyan:Ui.Red;trail.color=new Color(ink.r,ink.g,ink.b,(1-n/10f)*.45f);}
     if(visible){focus+=s.position;count++;}
     if(t>.97f&&!s.arrived&&s.packet.Power>0){s.arrived=true;int target=1-s.packet.Owner;if(report.Before[target,s.packet.End]>0){impulse=8;StartCoroutine(Impact(root,s.path.Last(),c.State.Health[target,s.packet.End]==0?"서버 소실":"착탄",s.packet.Owner==0?Ui.Cyan:Ui.Red));c.servers.First(v=>v.owner==target&&v.channel==s.packet.End).Render(c.State.Health[target,s.packet.End],0);}}
    }
    impulse=Mathf.Max(0,impulse-Delta*15);Frame(count>0?focus/count:new Vector2(982,487),1,impulse,t);yield return null;if(t>=1)break;
   }
   foreach(var s in shots){s.root.gameObject.SetActive(false);foreach(var trail in s.trails)trail.gameObject.SetActive(false);}
   Phase="return";Vector2 endFocus=shots.Aggregate(Vector2.zero,(p,s)=>p+s.position)/shots.Count;
   for(float t=0;t<1;t+=Delta/.45f){Progress=t;Frame(endFocus,1-Smooth(t),impulse*(1-t)*(1-t),1+t);yield return null;}
   ResetView();Destroy(root.gameObject);Phase="idle";Progress=0;
  }
  IEnumerator Flash(Transform parent,Vector2 p,Color ink){var glow=Flare(parent,"발사 순간 방출",p,ink,1.35f);for(float t=0;t<1;t+=Delta/.16f){glow.color=new Color(ink.r,ink.g,ink.b,(1-t)*(1-t));yield return null;}Destroy(glow.gameObject);}
  IEnumerator Impact(Transform parent,Vector2 p,string caption,Color ink){var root=Ui.Rect("충돌 결과",parent,p.x,p.y,1,1);var glow=Flare(root,"충돌광",Vector2.zero,ink,1.15f);var bits=new List<Shape>();for(int n=0;n<12;n++)bits.Add(Ui.Box("회로 파편",root,0,0,4,2,ink));var label=Ui.Text("결과",root,-90,-50,180,28,caption,c.font,18,ink);label.alignment=TextAlignmentOptions.Center;
   for(float t=0;t<1;t+=Delta/.36f){float e=1-Mathf.Pow(1-t,3);glow.color=label.color=new Color(ink.r,ink.g,ink.b,1-e);for(int n=0;n<bits.Count;n++){float a=n*Mathf.PI*2/bits.Count;At(bits[n].rectTransform,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(14+e*58));bits[n].color=new Color(ink.r,ink.g,ink.b,1-t);}yield return null;}Destroy(root.gameObject);}
  void OnDisable(){ResetView();}
 }
}

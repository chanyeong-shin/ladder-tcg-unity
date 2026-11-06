using System.Collections;
using System.Collections.Generic;
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
  float Delta {get{return Time.deltaTime*Mathf.Max(.1f,speed);}}
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
   for(float t=0;t<1;t+=Delta/.28f){Progress=t;float e=Ease(t);At(rect,Vector2.Lerp(origin,dock.position,e));rect.sizeDelta=Vector2.Lerp(originSize,dock.size,e);yield return null;}
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

   // The rendered card itself is sliced. Each shard retains the corresponding artwork and letters.
   const int columns=6,rows=10;
   var tiles=new List<Image>();var sprites=new List<Sprite>();var starts=new List<Vector2>();
   Rect source=sprite.rect;float sw=source.width/columns,sh=source.height/rows;
   for(int row=0;row<rows;row++)for(int col=0;col<columns;col++){
    var slice=Sprite.Create(sprite.texture,new Rect(source.x+col*sw,source.y+source.height-(row+1)*sh,sw,sh),new Vector2(.5f,.5f),100);sprites.Add(slice);
    var p=new Vector2(dock.x+col*dock.width/columns,dock.y+row*dock.height/rows);starts.Add(p);
    var image=Picture("카드 조각 "+row+"-"+col,root,slice,new Rect(p,new Vector2(dock.width/columns,dock.height/rows)));tiles.Add(image);
   }
   chip.gameObject.SetActive(false);
   var traceRoot=Ui.Rect("세 회로 활성 발광",root,0,0,1920,1080);
   for(int lane=0;lane<3;lane++)for(int seg=1;seg<Routes[lane].Length;seg++){
    Ui.Line("회로 번짐",traceRoot,Routes[lane][seg-1],Routes[lane][seg],new Color(color.r,color.g,color.b,.12f),lane==1?15:9);
    Ui.Line("회로 중심",traceRoot,Routes[lane][seg-1],Routes[lane][seg],new Color(color.r,color.g,color.b,.7f),lane==1?6:2);
   }
   var pulses=new List<Shape>();
   for(int lane=0;lane<3;lane++)for(int wave=0;wave<3;wave++)for(int part=0;part<9;part++)pulses.Add(Ui.Box("이동 신호 잔광",root,0,0,1,1,color));
   Phase="fragment";
   for(float elapsed=0;elapsed<1.65f;elapsed+=Delta){Progress=elapsed/1.65f;
    for(int i=0;i<tiles.Count;i++){
     int row=i/columns,col=i%columns,lane=row%3;float delay=(columns-1-col)*.05f+row*.008f;
     float u=Mathf.Clamp01((elapsed-delay)/1.25f);var tile=tiles[i];Vector2 p=starts[i],size=new Vector2(dock.width/columns,dock.height/rows);
     if(u<.25f){float e=Ease(u/.25f);p+=new Vector2(18+row%3*9,(row-4.5f)*1.5f)*e;}
     else if(u<.48f){float e=Ease((u-.25f)/.23f);Vector2 a=starts[i]+new Vector2(18+row%3*9,(row-4.5f)*1.5f),b=Routes[lane][0];p=Vector2.Lerp(a,b,e);size=Vector2.Lerp(size,new Vector2(12,4),e);}
     else {float routeT=(u-.48f)/.52f;p=Along(Routes[lane],routeT);size=new Vector2(12,4);}
     tile.gameObject.SetActive(u<1);At(tile.rectTransform,p);tile.rectTransform.sizeDelta=size;
     tile.color=Color.Lerp(Color.white,new Color(color.r,color.g,color.b,1),Mathf.InverseLerp(.25f,.6f,u));
    }
    int pulseIndex=0;
    for(int lane=0;lane<3;lane++)for(int wave=0;wave<3;wave++){
     float head=(elapsed-.48f-wave*.17f-lane*.025f)/.72f;
     for(int part=0;part<9;part++){
      var pulse=pulses[pulseIndex++];float t=head-part*.027f;bool visible=t>=0&&t<=1;pulse.gameObject.SetActive(visible);if(!visible)continue;
      Vector2 a=Along(Routes[lane],Mathf.Max(0,t-.027f)),b=Along(Routes[lane],t),d=b-a;
      At(pulse.rectTransform,a);pulse.rectTransform.pivot=new Vector2(0,.5f);pulse.rectTransform.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
      pulse.rectTransform.sizeDelta=new Vector2(Mathf.Max(2,d.magnitude),part==0?6:10-part*.65f);
      Color ink=part==0?Color.Lerp(color,Color.white,.8f):color;ink.a=part==0?1:.48f*(1-part/9f);pulse.color=ink;
     }
    }
    yield return null;
   }
   foreach(var slice in sprites)Destroy(slice);
   Destroy(root.gameObject);
   yield return Absorb(color);
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
  public IEnumerator Applied(Card card,int owner,int target,bool connected){
   var root=Root("카드 적용 결과");Color color=owner==0?Ui.Cyan:Ui.Red;Vector2 point;string message;Shape mark;RectTransform wall=null;
   if(card==Card.Inject||card==Card.ZeroDay){point=new Vector2(BattleController.LaneX[target],owner==0?777:201);message=card==Card.ZeroDay?"관통 패킷 장전":"+1 장전";mark=Ui.Box("대상 서버 점등",root,point.x-38,point.y-38,76,76,color,false,4,6);}
   else if(card==Card.Reroute){point=new Vector2((BattleController.LaneX[target%2]+BattleController.LaneX[target%2+1])/2,BattleController.RowY[target/2]);message=connected?"회선 연결":"회선 제거";color=connected?color:Ui.Gold;mark=Ui.Line("회선 적용",root,new Vector2(point.x-140,point.y),new Vector2(point.x+140,point.y),color,7);}
   else if(card==Card.Firewall){point=new Vector2(BattleController.LaneX[target],497);message="채널 차단";color=Ui.Gold;wall=CombatPresentation.Firewall(root,point.x,point.y,0);mark=Ui.Box("방화벽 점등",root,point.x-45,point.y-50,90,100,Color.clear,false,3);}
   else {point=new Vector2(982,873);message="손패 보충";mark=Ui.Line("손패 보충",root,new Vector2(742,866),new Vector2(1222,866),color,3);}
   var label=Ui.Text("적용 결과",root,point.x-110,point.y-(wall?85:63),220,30,message,font,19,color);label.alignment=TextAlignmentOptions.Center;
   Phase="applied";for(float t=0;t<1;t+=Delta/.5f){Progress=t;float a=1-t*t;if(wall){float close=Ease(Mathf.InverseLerp(.22f,.52f,t));CombatPresentation.CloseFirewall(wall,close);if(t<.52f)a=0;}mark.color=new Color(color.r,color.g,color.b,a);label.color=new Color(color.r,color.g,color.b,a);yield return null;}
   Destroy(root.gameObject);Phase="idle";
  }
  public IEnumerator Turn(int phase,int actor,int attacker,System.Action atPeak=null){
   var root=Root("턴 전환");Color color=phase==2?Ui.Gold:actor==0?Ui.Cyan:Ui.Red;
   string caption=phase==2?"동시 발동 / RESOLVE":(actor==0?"내 ":"상대 ")+(phase==0?"공격 턴 / ATTACK":"방어 턴 / DEFEND");
   var label=Ui.Text("턴 전환 안내",root,742,270,480,50,caption,font,28,Color.clear);label.alignment=TextAlignmentOptions.Center;
   var header=Ui.Box("현재 단계 점등",root,161+544*phase,23,536,36,Color.clear,false,2);
   var crt=FindFirstObjectByType<CrtOverlay>();bool changed=false;
   Phase="turn";for(float t=0;t<1;t+=Delta/.85f){Progress=t;float noise=t<.35f?Ease(t/.35f):t<.48f?1:Mathf.Pow(1-Mathf.InverseLerp(.48f,.92f,t),2);if(crt)crt.SetBurst(noise);
    if(!changed&&t>=.4f){changed=true;atPeak?.Invoke();}float a=changed?Mathf.Sin(Mathf.InverseLerp(.4f,1,t)*Mathf.PI):0;
    label.color=new Color(color.r,color.g,color.b,a);header.color=new Color(color.r,color.g,color.b,a);yield return null;
   }
   if(!changed)atPeak?.Invoke();if(crt)crt.SetBurst(0);Destroy(root.gameObject);Phase="idle";
  }
  public IEnumerator Impact(Vector2 point,string caption,Color color){var root=Root("패킷 충돌");var ring=Ui.Box("충돌 섬광",root,point.x-22,point.y-22,44,44,color,false,3,6);var label=Ui.Text("충돌 결과",root,point.x-95,point.y-50,190,26,caption,font,17,color);label.alignment=TextAlignmentOptions.Center;for(float t=0;t<1;t+=Delta/.3f){ring.color=label.color=new Color(color.r,color.g,color.b,1-t);float size=44+t*24;At(ring.rectTransform,point-Vector2.one*size/2);ring.rectTransform.sizeDelta=Vector2.one*size;yield return null;}Destroy(root.gameObject);}
 }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Breach.UI {
 public sealed class BattleController:MonoBehaviour {
  public RectTransform stage,hand,fx,paths,rungs;public GameObject title,loading,game,result,logPanel;
  public GameObject[] backgrounds;public CardView[] cardPrefabs;public GameObject backPrefab;
  public ServerView[] servers;public Button endButton,undoButton,logButton,startButton,retryButton;
  public Button[] laneTargets,rungTargets;public TMP_Text roundLabel,statusLabel,secretLabel,resultLabel,logText;
  public TMP_FontAsset font;public float animationSpeed=1;public CardTransferFx feedback;
  RectTransform hints;Button socketTarget;Coroutine handTween;Vector2 playOrigin,playSize;bool fromDrop;
  public Battle State {get;private set;}public bool CanInput {get{return State!=null&&State.Winner==-1&&State.Actor==0&&State.Phase<2&&!busy&&game.activeSelf;}}
  bool busy,dragging;int selected=-1,hover=-1,actionLogStart;CardView dragged;
  readonly List<CardView> cards=new List<CardView>();readonly List<string> log=new List<string>();
  int[,] publicPower=new int[2,3];int[] publicRungs={-1,-1,-1};int[] publicOwners={-1,-1,-1};int publicSecrets,publicHandCount;readonly List<string> deferredLog=new List<string>();readonly List<Secret> deferredEffects=new List<Secret>();
  public static readonly float[] LaneX={702,982,1262};public static readonly float[] RowY={364,497,630};
  void Start(){startButton.onClick.AddListener(()=>StartCoroutine(Begin()));retryButton.onClick.AddListener(()=>StartCoroutine(Begin()));endButton.onClick.AddListener(EndTurn);undoButton.onClick.AddListener(Undo);logButton.onClick.AddListener(()=>logPanel.SetActive(!logPanel.activeSelf));
   for(int i=0;i<laneTargets.Length;i++){int t=i;laneTargets[i].onClick.AddListener(()=>Use(t,false));}for(int i=0;i<rungTargets.Length;i++){int t=i;rungTargets[i].onClick.AddListener(()=>Use(t,true));}
   hints=Ui.Rect("배치 가능한 지점",game.transform,0,0,1920,1080);hints.SetSiblingIndex(paths.GetSiblingIndex()+1);
   var socketHit=Ui.Rect("소켓 사용 판정",game.transform,223,381,228,236);var hitImage=socketHit.gameObject.AddComponent<Image>();hitImage.color=Color.clear;socketTarget=socketHit.gameObject.AddComponent<Button>();socketTarget.targetGraphic=hitImage;socketTarget.transition=Selectable.Transition.None;socketTarget.onClick.AddListener(()=>Use(0,false));socketHit.gameObject.SetActive(false);
   title.SetActive(true);loading.SetActive(false);game.SetActive(false);result.SetActive(false);logPanel.SetActive(false);
  }
  void Update(){if(title.activeSelf&&Input.GetKeyDown(KeyCode.Return)){startButton.onClick.Invoke();return;}if(Input.GetKeyDown(KeyCode.Escape)&&CanInput){selected=-1;ClearPreview();LayoutHand(-1);RefreshTargets();}if(Input.GetKeyDown(KeyCode.Return)&&CanInput)EndTurn();}
  IEnumerator Begin(){StopAllEffects();title.SetActive(false);result.SetActive(false);game.SetActive(false);loading.SetActive(true);yield return Wait(.7f);State=new Battle((uint)Environment.TickCount);log.Clear();deferredLog.Clear();deferredEffects.Clear();selected=-1;busy=false;logPanel.SetActive(false);CapturePublic();loading.SetActive(false);game.SetActive(true);Note("접속 완료 · "+(State.Attacker==0?"내":"상대")+" 공격 턴");actionLogStart=log.Count;Render();yield return TurnFeedback();yield return BotFlow();}
  void CapturePublic(){publicPower=(int[,])State.Power.Clone();publicRungs=(int[])State.Rungs.Clone();publicOwners=(int[])State.RungOwners.Clone();publicSecrets=State.Hidden.Count(s=>s.Owner==1);publicHandCount=State.Hands[1].Count;}
  bool HideEnemyActions {get{return State.Actor==1&&State.Phase<2;}}
  public void Render(){if(State==null)return;int phase=State.Phase==2?2:State.Actor==0?0:1;for(int i=0;i<3;i++)backgrounds[i].SetActive(i==phase);
   foreach(var s in servers)s.Render(State.Health[s.owner,s.channel],s.owner==1&&HideEnemyActions?publicPower[1,s.channel]:State.Power[s.owner,s.channel]);
   foreach(var root in backgrounds){
    Color accent=phase==0?Ui.Cyan:phase==1?Ui.Red:Ui.Gold;
    for(int step=0;step<3;step++){var block=Find(root.transform,"단계 "+step);if(!block)continue;foreach(var graphic in block.GetComponentsInChildren<Graphic>(true)){bool active=step==State.Phase;graphic.color=graphic is TMP_Text?(active?accent:new Color(.35f,.45f,.5f)):(graphic.gameObject==block.gameObject?(active?new Color(accent.r*.12f,accent.g*.12f,accent.b*.12f,1):new Color(.035f,.055f,.07f,1)):(active?accent:new Color(.14f,.22f,.26f,1)));}}
    for(int owner=0;owner<2;owner++)for(int lane=0;lane<3;lane++){var meter=Find(root.transform,"Source_64_"+((owner==0?478:489)+lane*3));if(meter){var graphic=meter.GetComponent<Graphic>();if(graphic)graphic.color=State.Health[owner,lane]>0?(owner==0?Ui.Cyan:Ui.Red):new Color(.16f,.18f,.2f);}}
    foreach(var t in root.GetComponentsInChildren<TMP_Text>(true)){
    if(t.gameObject.name=="Source_64_476")t.text=State.Alive(0)+" / 3";
    if(t.gameObject.name=="Source_64_487")t.text=State.Alive(1)+" / 3";
    if(t.gameObject.name=="Source_64_372")t.text="ENEMY HAND   "+(HideEnemyActions?publicHandCount:State.Hands[1].Count).ToString("00");
    if(t.gameObject.name=="Source_64_435")t.text=busy?"신호 전송 / TRANSFER":"빈 소켓 / READY";
    if(t.gameObject.name=="단계와 행동 담당"){if(t.text.StartsWith("01"))t.text="01 공격 턴 / ATTACK - "+(State.Attacker==0?"나":"상대");else if(t.text.StartsWith("02"))t.text="02 방어 턴 / DEFEND - "+(State.Attacker==0?"상대":"나");}
   }for(int i=0;i<5;i++){var n=Find(root.transform,"Enemy_Back_"+(374+i));if(n)n.gameObject.SetActive(i<(HideEnemyActions?publicHandCount:State.Hands[1].Count));var holder=Find(root.transform,"Source_64_"+(374+i));if(holder)holder.gameObject.SetActive(i<(HideEnemyActions?publicHandCount:State.Hands[1].Count));}}
   roundLabel.text="ROUND "+State.Round.ToString("00");statusLabel.text=State.Phase==2?"동시 발동 · 패킷 해결 중":State.Actor==0?"내 "+(State.Phase==0?"공격":"방어")+" 턴 · 카드를 끌어 놓거나 선택 후 대상을 누르세요":"상대 "+(State.Phase==0?"공격":"방어")+" 턴 · 대응 준비 중";
   secretLabel.text="보안 카드 / ENCRYPTED    나 "+State.Hidden.Count(s=>s.Owner==0)+"  ·  상대 "+(HideEnemyActions?publicSecrets:State.Hidden.Count(s=>s.Owner==1));
   RefreshBoard();BuildHand();RefreshTargets();
  }
  public static Transform Find(Transform root,string name){foreach(Transform t in root){if(t.name==name)return t;var n=Find(t,name);if(n)return n;}return null;}
  void Clear(Transform root){for(int i=root.childCount-1;i>=0;i--){var g=root.GetChild(i).gameObject;g.SetActive(false);Destroy(g);}}
  void RefreshBoard(){Clear(rungs);var rows=HideEnemyActions?publicRungs:State.Rungs;var owners=HideEnemyActions?publicOwners:State.RungOwners;
   for(int row=0;row<3;row++){if(rows[row]>=0){int c=rows[row];Ui.Line("회선 "+(row+1),rungs,new Vector2(LaneX[c],RowY[row]),new Vector2(LaneX[c+1],RowY[row]),owners[row]==0?Ui.Cyan:Ui.Red,4);}else for(int edge=0;edge<2;edge++)for(int k=0;k<9;k++)Ui.Line("빈 배치 자리",rungs,new Vector2(LaneX[edge]+20+k*27,RowY[row]),new Vector2(LaneX[edge]+30+k*27,RowY[row]),new Color(.15f,.26f,.3f,.6f),1);}
   for(int lane=0;lane<3;lane++)if(State.Walls[lane]){Ui.Box("방화벽",rungs,LaneX[lane]-28,330,56,330,new Color(1,.6f,.1f,.15f));Ui.Text("차단",rungs,LaneX[lane]-42,480,84,30,"BLOCKED",font,15,Ui.Gold);}
  }
  void BuildHand(int visibleLimit=int.MaxValue){if(dragging)return;if(handTween!=null)StopCoroutine(handTween);Clear(hand);cards.Clear();for(int i=0;i<Mathf.Min(State.Hands[0].Count,visibleLimit);i++){var view=Instantiate(cardPrefabs[(int)State.Hands[0][i]],hand);view.gameObject.SetActive(true);view.controller=this;view.index=i;cards.Add(view);}LayoutHand(-1,true);}
  public void Hover(int index){if(!CanInput||dragging||selected>=0)return;LayoutHand(index);}
  public void LayoutHand(int index,bool instant=false){hover=index;if(handTween!=null)StopCoroutine(handTween);handTween=StartCoroutine(HandMotion(index,instant));}
  IEnumerator HandMotion(int index,bool instant){
   var views=cards.Where(c=>c).ToArray();var starts=views.Select(c=>((RectTransform)c.transform).anchoredPosition).ToArray();var scales=views.Select(c=>c.transform.localScale.x).ToArray();var ends=new Vector2[views.Length];var sizes=new float[views.Length];
   float total=views.Length*126+(views.Length-1)*12+(index>=0?75.6f:0),x=982-total/2;
   for(int i=0;i<views.Length;i++){sizes[i]=i==index?1.6f:1;ends[i]=new Vector2(x,-(1064-201.6f*sizes[i]));x+=126*sizes[i]+12;}if(index>=0&&index<views.Length)views[index].transform.SetAsLastSibling();
   for(float t=instant?1:0;t<1;t+=Time.unscaledDeltaTime/.15f){float e=t*t*(3-2*t);for(int i=0;i<views.Length;i++)if(views[i]){((RectTransform)views[i].transform).anchoredPosition=Vector2.Lerp(starts[i],ends[i],e);views[i].transform.localScale=Vector3.one*Mathf.Lerp(scales[i],sizes[i],e);}yield return null;}
   for(int i=0;i<views.Length;i++)if(views[i]){((RectTransform)views[i].transform).anchoredPosition=ends[i];views[i].transform.localScale=Vector3.one*sizes[i];}
  }
  public void Select(int index){if(!CanInput||index<0||index>=cards.Count)return;selected=selected==index?-1:index;LayoutHand(selected);RefreshTargets();}
  public void BeginDrag(CardView v){if(!CanInput)return;if(handTween!=null)StopCoroutine(handTween);ClearPreview();selected=v.index;dragging=true;dragged=v;v.transform.SetParent(stage,false);v.transform.SetAsLastSibling();var g=v.gameObject.AddComponent<CanvasGroup>();g.blocksRaycasts=false;RefreshTargets();}
  public int AmmoDropTarget(Vector2 screen){if(!dragging||selected<0||selected>=State.Hands[0].Count)return -1;Card card=State.Hands[0][selected];if(card!=Card.Inject&&card!=Card.ZeroDay)return -1;Vector2 p;RectTransformUtility.ScreenPointToLocalPointInRectangle(stage,screen,null,out p);p.y=-p.y;if(p.y<231||p.y>832)return -1;int lane=Enumerable.Range(0,3).OrderBy(i=>Mathf.Abs(p.x-LaneX[i])).First();float width=p.y>=722?50:28;return Mathf.Abs(p.x-LaneX[lane])<=width&&State.CanPlay(0,selected,lane)?lane:-1;}
  public void Drop(Vector2 screen){if(!dragging)return;Vector2 p;RectTransformUtility.ScreenPointToLocalPointInRectangle(stage,screen,null,out p);p.y=-p.y;int ammoTarget=AmmoDropTarget(screen);dragging=false;var v=dragged;dragged=null;if(v){var rect=(RectTransform)v.transform;playOrigin=new Vector2(rect.anchoredPosition.x,-rect.anchoredPosition.y);playSize=Vector2.Scale(rect.sizeDelta,new Vector2(rect.localScale.x,rect.localScale.y));}int index=selected;int target=-1;bool rung=false;
   if(CanInput&&index>=0&&index<State.Hands[0].Count){Card c=State.Hands[0][index];if(c==Card.Reroute){rung=true;for(int t=0;t<6;t++)if(new Rect(LaneX[t%2]+10,RowY[t/2]-42,260,84).Contains(p)){target=t;break;}}
    else if(c==Card.Buffer||c==Card.Overflow){if(new Rect(582,129,800,716).Contains(p)||new Rect(173,347,332,322).Contains(p))target=0;}
    else if(c==Card.Inject||c==Card.ZeroDay)target=ammoTarget;else if(new Rect(582,129,800,716).Contains(p))target=Enumerable.Range(0,3).OrderBy(i=>Mathf.Abs(p.x-LaneX[i])).First();}
   if(target>=0&&State.CanPlay(0,index,target)){fromDrop=true;if(v){v.gameObject.SetActive(false);Destroy(v.gameObject);}Use(target,rung);return;}selected=-1;StartCoroutine(ReturnCard(v));}
  IEnumerator ReturnCard(CardView view){busy=true;ClearPreview();RefreshTargets();if(view)view.transform.SetParent(hand,false);LayoutHand(-1);yield return new WaitForSecondsRealtime(.18f);busy=false;fromDrop=false;Render();statusLabel.text="배치 취소 · 손패로 돌아왔습니다.";}
  public void RefreshTargets(){bool input=CanInput;endButton.interactable=input;undoButton.interactable=input;if(hints)Clear(hints);bool selectedCard=input&&selected>=0&&selected<State.Hands[0].Count;Card card=selectedCard?State.Hands[0][selected]:Card.Inject;
   for(int i=0;i<laneTargets.Length;i++){laneTargets[i].interactable=selectedCard&&card!=Card.Reroute&&State.CanPlay(0,selected,i);laneTargets[i].transition=Selectable.Transition.None;laneTargets[i].targetGraphic.color=Color.clear;}
   for(int i=0;i<rungTargets.Length;i++){bool active=selectedCard&&card==Card.Reroute;rungTargets[i].gameObject.SetActive(active);rungTargets[i].interactable=active&&State.CanPlay(0,selected,i);rungTargets[i].transition=Selectable.Transition.None;rungTargets[i].targetGraphic.color=Color.clear;}
   if(socketTarget)socketTarget.gameObject.SetActive(selectedCard&&(card==Card.Buffer||card==Card.Overflow));if(!selectedCard||!hints)return;
   if(card==Card.Reroute){for(int t=0;t<6;t++)if(State.CanPlay(0,selected,t))Ui.Line("합법 회선 자리",hints,new Vector2(LaneX[t%2]+12,RowY[t/2]),new Vector2(LaneX[t%2+1]-12,RowY[t/2]),new Color(1,.8f,.2f,.55f),2);}
   else if(card==Card.Buffer||card==Card.Overflow)Ui.Box("소켓에 사용",hints,223,381,228,236,Ui.Cyan,false,2);
   else for(int lane=0;lane<3;lane++)if(State.CanPlay(0,selected,lane)){if(card==Card.Firewall)Ui.Box("차단할 채널",hints,LaneX[lane]-14,477,28,40,Ui.Gold,false,2);else Ui.Box("장전할 서버",hints,LaneX[lane]-35,742,70,70,new Color(.2f,.9f,1,.65f),false,2,6);}
  }
  public void Preview(int target,bool rung){if(State==null||busy)return;ClearPreview();if(selected>=0&&selected<State.Hands[0].Count){Card card=State.Hands[0][selected];if(card==Card.Buffer||card==Card.Overflow)return;if(!State.CanPlay(0,selected,target))return;if(card==Card.Firewall){Ui.Line("차단 채널 미리보기",paths,new Vector2(LaneX[target],231),new Vector2(LaneX[target],763),Ui.Gold,3);return;}}if(rung){if(selected>=0&&State.CanPlay(0,selected,target))Ui.Line("배치 미리보기",paths,new Vector2(LaneX[target%2],RowY[target/2]),new Vector2(LaneX[target%2+1],RowY[target/2]),Ui.Gold,5);return;}
   var p=State.Route(0,target);var points=Points(p);for(int i=1;i<points.Count;i++)Ui.Line("공개 경로",paths,points[i-1],points[i],new Color(1,.8f,.2f,.65f),3);
   if(CanInput)statusLabel.text="CH "+(target+1)+" → 상대 CH "+(p.End+1)+" · 공개된 경로 기준";
  }
  public void ClearPreview(){Clear(paths);}
  public void PreviewServer(int owner,int channel){if(State==null||busy)return;int amount=owner==1&&HideEnemyActions?publicPower[owner,channel]:State.Power[owner,channel];if(amount<=0)return;ClearPreview();var packet=State.Route(owner,channel);var points=Points(packet);for(int i=1;i<points.Count;i++)Ui.Line("장전 패킷 경로",paths,points[i-1],points[i],Ui.Gold,3);}
  public void Use(int target,bool rung){if(!CanInput||selected<0||selected>=State.Hands[0].Count)return;if((State.Hands[0][selected]==Card.Reroute)!=rung)return;if(!State.CanPlay(0,selected,target)){statusLabel.text="이 위치에는 사용할 수 없습니다.";return;}int i=selected;if(!fromDrop&&i<cards.Count&&cards[i]){var r=(RectTransform)cards[i].transform;playOrigin=new Vector2(r.anchoredPosition.x,-r.anchoredPosition.y);playSize=r.sizeDelta*r.localScale.x;}fromDrop=false;selected=-1;ClearPreview();StartCoroutine(Play(0,i,target));}
  IEnumerator Play(int owner,int index,int target){Card card=State.Hands[owner][index];int remaining=State.Hands[0].Count-(owner==0?1:0);if(!State.Play(owner,index,target))yield break;busy=true;BuildHand(remaining);RefreshTargets();
   if(owner==1&&State.Phase==1){deferredLog.Add("상대 · "+(Cards.Enhanced(card)?"보안 카드 배치":Cards.Names[(int)card]));yield return Wait(.15f);}
   else {feedback.speed=animationSpeed;yield return feedback.Play(card,owner,owner==0?playOrigin:new Vector2(300,188),owner==0?playSize:new Vector2(56.875f,91));if(owner==1)deferredLog.Add("상대 · "+(Cards.Enhanced(card)?"보안 카드 배치":Cards.Names[(int)card]));else Note("나 · "+Cards.Names[(int)card]);}Render();if(!Cards.Enhanced(card)){if(owner==1)deferredEffects.Add(new Secret(owner,card,target));else yield return feedback.Applied(card,owner,target,card!=Card.Reroute||State.Rungs[target/2]>=0);}busy=false;Render();}
  public void EndTurn(){if(CanInput&&!dragging){selected=-1;ClearPreview();StartCoroutine(Advance());}}
  IEnumerator Advance(){busy=true;State.EndAction();CapturePublic();Render();if(State.Phase==2)yield return ResolveRound();else yield return TurnFeedback();busy=false;actionLogStart=log.Count;Render();yield return BotFlow();}
  IEnumerator BotFlow(){if(State==null||State.Actor!=1||State.Winner!=-1||State.Phase>=2)yield break;busy=true;Render();yield return Wait(.55f);
   int guard=0,index,target;while(State.ChooseBot(out index,out target)&&guard++<40){yield return Play(1,index,target);busy=true;RefreshTargets();yield return Wait(.2f);}
   State.EndAction();CapturePublic();foreach(string entry in deferredLog)Note(entry);deferredLog.Clear();Render();foreach(var effect in deferredEffects)yield return feedback.Applied(effect.Card,effect.Owner,effect.Target,effect.Card!=Card.Reroute||State.Rungs[effect.Target/2]>=0);deferredEffects.Clear();if(State.Phase==2)yield return ResolveRound();else yield return TurnFeedback();busy=false;actionLogStart=log.Count;Render();if(State.Actor==1&&State.Winner==-1)yield return BotFlow();}
  IEnumerator ResolveRound(){busy=true;State.Phase=2;Render();yield return TurnFeedback();var report=State.Resolve();CapturePublic();RefreshBoard();foreach(var s in report.Revealed){Note((s.Owner==0?"나":"상대")+" · "+Cards.Names[(int)s.Card]+" 공개");yield return Reveal(s);if(s.Card==Card.ZeroDay){var shot=report.Packets.FirstOrDefault(p=>p.Owner==s.Owner&&p.Start==s.Target);if(shot!=null)servers.First(v=>v.owner==s.Owner&&v.channel==s.Target).Render(report.Before[s.Owner,s.Target],shot.Initial);}if(s.Card==Card.Overflow&&s.Owner==0)BuildHand();yield return feedback.Applied(s.Card,s.Owner,s.Target,true);}yield return Flight(report);foreach(string e in report.Events)Note(e);Render();yield return Wait(.5f);if(State.Winner!=-1){resultLabel.text=State.Winner==0?"침투 성공 / ACCESS GRANTED":State.Winner==1?"서버 소실 / CONNECTION LOST":"동시 전멸 / DRAW";result.SetActive(true);yield break;}State.NextRound();CapturePublic();Note("ROUND "+State.Round+" · "+(State.Attacker==0?"내":"상대")+" 공격 턴");Render();yield return TurnFeedback();}
  IEnumerator TurnFeedback(){bool previous=busy;busy=true;RefreshTargets();feedback.speed=animationSpeed;yield return feedback.Turn(State.Phase,State.Actor,State.Attacker);busy=previous;RefreshTargets();}
  void Undo(){if(!CanInput)return;State.Undo();if(log.Count>actionLogStart)log.RemoveRange(actionLogStart,log.Count-actionLogStart);selected=-1;ClearPreview();CapturePublic();Render();Note("이번 행동을 시작 상태로 복원했습니다.");}
  void Note(string message){log.Add(message);if(log.Count>200){log.RemoveAt(0);actionLogStart=Mathf.Max(0,actionLogStart-1);}logText.text=string.Join("\n",log.Skip(Math.Max(0,log.Count-16)));}
  WaitForSeconds Wait(float seconds){return new WaitForSeconds(seconds/Mathf.Max(.1f,animationSpeed));}
  void StopAllEffects(){Clear(fx);Clear(paths);}
  IEnumerator Reveal(Secret secret){var view=Instantiate(cardPrefabs[(int)secret.Card],fx);view.controller=null;view.gameObject.SetActive(true);var rt=(RectTransform)view.transform;Rect dock=CardTransferFx.DockRect;rt.anchoredPosition=new Vector2(dock.x,-dock.y);rt.localScale=Vector3.one*(dock.height/201.6f);yield return Wait(.45f);Destroy(view.gameObject);}
  public static List<Vector2> Points(Packet p){var points=new List<Vector2>();foreach(var c in p.Cells)points.Add(new Vector2(LaneX[c.Lane],c.Row<0?201:c.Row>2?777:RowY[c.Row]));return points;}
  static Vector2 Along(IList<Vector2> points,float t){float total=0;for(int i=1;i<points.Count;i++)total+=Vector2.Distance(points[i-1],points[i]);float d=t*total;for(int i=1;i<points.Count;i++){float length=Vector2.Distance(points[i-1],points[i]);if(d<=length)return Vector2.Lerp(points[i-1],points[i],length==0?0:d/length);d-=length;}return points[points.Count-1];}
  IEnumerator Flight(Resolution report){var shots=new List<RectTransform>();foreach(var p in report.Packets){var pt=Points(p)[0];var shell=Ui.Box("패킷 CH "+(p.Start+1),fx,pt.x-18,pt.y-18,36,36,new Color(.04f,.06f,.09f));Ui.Box("패킷 테두리",shell.transform,0,0,36,36,p.Pierce?Ui.Gold:p.Owner==0?Ui.Cyan:Ui.Red,false,2);var txt=Ui.Text("패킷 안 위력",shell.transform,0,0,36,36,p.Initial.ToString(),font,22,Color.white);txt.alignment=TextAlignmentOptions.Center;shots.Add(shell.rectTransform);}
   var collided=new bool[shots.Count];var landed=new bool[shots.Count];
   for(float t=0;t<1;t+=Time.deltaTime*.65f*animationSpeed){for(int i=0;i<shots.Count;i++){var packet=report.Packets[i];
    if(!collided[i]&&t>(packet.Blocked?.3f:.55f)&&(packet.Blocked||packet.Power<packet.Initial)){collided[i]=true;Vector2 point=CardTransferFx.Along(Points(packet),packet.Blocked?.3f:.5f);if(!packet.Blocked){var shared=packet.Cells.Where(cell=>report.Packets.Any(other=>other.Owner!=packet.Owner&&other.Cells.Any(c=>c.Row==cell.Row&&c.Lane==cell.Lane))).OrderBy(cell=>Math.Abs(cell.Row-1)).ToArray();if(shared.Length>0)point=new Vector2(LaneX[shared[0].Lane],shared[0].Row<0?201:shared[0].Row>2?777:RowY[shared[0].Row]);}StartCoroutine(feedback.Impact(point,packet.Blocked?"방화벽 차단":"공격 상쇄",Ui.Gold));}
    if(!landed[i]&&t>.94f&&packet.Power>0&&report.Before[1-packet.Owner,packet.End]>0){landed[i]=true;StartCoroutine(feedback.Impact(Points(packet).Last(),"착탄",packet.Owner==0?Ui.Cyan:Ui.Red));}
if((packet.Blocked&&t>.3f)||(packet.Power==0&&t>.55f)){shots[i].gameObject.SetActive(false);continue;}Vector2 p=Along(Points(packet),t);shots[i].anchoredPosition=new Vector2(p.x-18,-p.y+18);if(t>.55f)shots[i].GetComponentInChildren<TMP_Text>().text=packet.Power.ToString();}yield return null;}foreach(var r in shots)Destroy(r.gameObject);
   foreach(var s in servers)if(report.Before[s.owner,s.channel]>0&&State.Health[s.owner,s.channel]==0){var t=(RectTransform)s.transform;Ui.Box("서버 파괴 잔광",fx,t.anchoredPosition.x+17,-t.anchoredPosition.y+22,66,66,new Color(1,.3f,.3f,.4f),false,4,6);}yield return Wait(.35f);Clear(fx);
  }
 }
}

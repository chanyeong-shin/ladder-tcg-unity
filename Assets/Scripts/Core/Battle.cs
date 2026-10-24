using System;
using System.Collections.Generic;
using System.Linq;

namespace Breach {
 public enum Card { Inject, Reroute, Buffer, ZeroDay, Firewall, Overflow }
 public static class Cards {
  public static readonly string[] Names={"코드 주입","라우팅 변경","버퍼 적재","제로데이","방화벽","버퍼 오버플로"};
  public static readonly string[] English={"INJECT","REROUTE","BUFFER","ZERO-DAY","FIREWALL","OVERFLOW"};
  public static readonly string[] Description={"채널 하나를 공격합니다.\n같은 채널에 겹쳐 내면 세기가 더해집니다.","가로줄을 놓거나 부숩니다.\n패킷이 지나는 길이 바뀝니다.","카드를 2장 뽑습니다.","권한 상승 - 코드 주입\n채널 하나를 공격합니다.\n상대 공격과 부딪혀도 지워지지 않고 통과합니다.","권한 상승 - 라우팅 변경\n채널 하나를 이번 턴 막습니다.\n그 길을 지나는 패킷은 양쪽 다 사라집니다.","권한 상승 - 버퍼 적재\n카드를 4장 뽑습니다."};
  public static bool Enhanced(Card c){return c>=Card.ZeroDay;}
 }
 public struct Secret {public int Owner,Target;public Card Card;public Secret(int owner,Card card,int target){Owner=owner;Card=card;Target=target;}}
 public struct Cell {public int Row,Lane;public Cell(int row,int lane){Row=row;Lane=lane;}}
 public class Packet {public int Owner,Start,End,Power,Initial;public bool Pierce,Blocked;public List<Cell> Cells=new List<Cell>();}
 public class Resolution {public List<Packet> Packets=new List<Packet>();public List<Secret> Revealed=new List<Secret>();public List<string> Events=new List<string>();public int[,] Before;}
 public sealed class Battle {
  public int[,] Health=new int[2,3],Power=new int[2,3];
  public bool[,] Pierce=new bool[2,3];public bool[] Walls=new bool[3];
  public int[] Rungs={-1,-1,-1},RungOwners={-1,-1,-1};
  public List<Card>[] Hands={new List<Card>(),new List<Card>()};
  public List<Secret> Hidden=new List<Secret>();
  public int Round=1,Attacker,Phase,Winner=-1;public int Actor {get{return Phase==0?Attacker:1-Attacker;}}
  uint random;Snapshot snapshot;
  sealed class Snapshot {public int[,] Power;public bool[,] Pierce;public int[] Rungs,Owners;public List<Card>[] Hands;public List<Secret> Hidden;public uint Random;}
  public Battle(uint seed,int attacker=-1){random=seed==0?1:seed;Attacker=attacker<0?Next(2):attacker;for(int p=0;p<2;p++){for(int c=0;c<3;c++)Health[p,c]=1;Draw(p,3);Draw(p,1);}SaveAction();}
  public int Next(int max){random^=random<<13;random^=random>>17;random^=random<<5;return (int)(random%(uint)max);}
  public void Draw(int player,int count){int[] weights={10,5,3,1,1,1};for(int i=0;i<count&&Hands[player].Count<5;i++){int r=Next(21);for(int c=0;c<6;c++){r-=weights[c];if(r<0){Hands[player].Add((Card)c);break;}}}}
  public void SaveAction(){snapshot=new Snapshot{Power=(int[,])Power.Clone(),Pierce=(bool[,])Pierce.Clone(),Rungs=(int[])Rungs.Clone(),Owners=(int[])RungOwners.Clone(),Hands=Hands.Select(h=>new List<Card>(h)).ToArray(),Hidden=new List<Secret>(Hidden),Random=random};}
  public bool Undo(){if(Phase>=2||Winner!=-1||snapshot==null)return false;Power=(int[,])snapshot.Power.Clone();Pierce=(bool[,])snapshot.Pierce.Clone();Rungs=(int[])snapshot.Rungs.Clone();RungOwners=(int[])snapshot.Owners.Clone();Hands=snapshot.Hands.Select(h=>new List<Card>(h)).ToArray();Hidden=new List<Secret>(snapshot.Hidden);random=snapshot.Random;return true;}
  public bool CanPlay(int owner,int index,int target){
   if(Winner!=-1||Phase>=2||owner!=Actor||index<0||index>=Hands[owner].Count)return false;
   Card c=Hands[owner][index];
   if(c==Card.Inject||c==Card.ZeroDay)return target>=0&&target<3&&Health[owner,target]>0;
   if(c==Card.Firewall)return target>=0&&target<3;
   if(c==Card.Reroute){if(target<0||target>=6)return false;return Rungs[target/2]==-1||Rungs[target/2]==target%2;}
   return true;
  }
  public bool Play(int owner,int index,int target){if(!CanPlay(owner,index,target))return false;Card c=Hands[owner][index];Hands[owner].RemoveAt(index);
   if(Cards.Enhanced(c))Hidden.Add(new Secret(owner,c,target));
   else if(c==Card.Inject)Power[owner,target]++;
   else if(c==Card.Reroute){int row=target/2;Rungs[row]=Rungs[row]==-1?target%2:-1;RungOwners[row]=Rungs[row]<0?-1:owner;}
   else Draw(owner,2);return true;
  }
  public void EndAction(){if(Winner!=-1||Phase>=2)return;Phase++;if(Phase==1)SaveAction();}
  public Packet Route(int owner,int start){var p=new Packet{Owner=owner,Start=start,Power=Power[owner,start],Initial=Power[owner,start],Pierce=Pierce[owner,start]};int lane=start;
   p.Cells.Add(new Cell(owner==0?3:-1,lane));
   for(int k=0;k<3;k++){int row=owner==0?2-k:k;p.Cells.Add(new Cell(row,lane));int edge=Rungs[row];if(edge>=0&&(lane==edge||lane==edge+1)){lane=lane==edge?edge+1:edge;p.Cells.Add(new Cell(row,lane));}}
   p.Cells.Add(new Cell(owner==0?-1:3,lane));p.End=lane;return p;
  }
  public bool Touch(Packet a,Packet b){return a.Cells.Any(c=>b.Cells.Any(d=>c.Row==d.Row&&c.Lane==d.Lane));}
  public Resolution Resolve(){var r=new Resolution{Before=(int[,])Health.Clone()};
   foreach(var s in Hidden){r.Revealed.Add(s);r.Events.Add((s.Owner==0?"나":"상대")+" · "+Cards.Names[(int)s.Card]+" 공개");if(s.Card==Card.ZeroDay){Power[s.Owner,s.Target]++;Pierce[s.Owner,s.Target]=true;}else if(s.Card==Card.Firewall)Walls[s.Target]=true;else Draw(s.Owner,4);}Hidden.Clear();
   for(int p=0;p<2;p++)for(int c=0;c<3;c++)if(Power[p,c]>0){var shot=Route(p,c);if(shot.Cells.Any(cell=>Walls[cell.Lane])){shot.Blocked=true;shot.Power=0;r.Events.Add("방화벽 · 패킷 소멸");}r.Packets.Add(shot);}
   foreach(var a in r.Packets.Where(p=>p.Owner==0))foreach(var b in r.Packets.Where(p=>p.Owner==1)){if(a.Power<=0||b.Power<=0||!Touch(a,b))continue;
    if(a.Pierce&&!b.Pierce){b.Power=0;r.Events.Add("제로데이 · 상대 패킷 관통");}
    else if(b.Pierce&&!a.Pierce){a.Power=0;r.Events.Add("제로데이 · 내 패킷 관통");}
    else {int n=Math.Min(a.Power,b.Power);a.Power-=n;b.Power-=n;r.Events.Add("공격 상쇄 · "+n);}}
   // All impacts are adjudicated before victory, so neither side wins by iteration order.
   var damage=new int[2,3];foreach(var p in r.Packets){if(p.Power<=0)continue;int target=1-p.Owner;AccumulateHit(target,p.End,p.Power,r,damage);}
   for(int owner=0;owner<2;owner++)for(int channel=0;channel<3;channel++)Health[owner,channel]=Math.Max(0,r.Before[owner,channel]-damage[owner,channel]);
   Array.Clear(Power,0,Power.Length);Array.Clear(Pierce,0,Pierce.Length);
   bool mine=Alive(0)>0,theirs=Alive(1)>0;Winner=!mine&&!theirs?-2:!theirs?0:!mine?1:-1;Phase=2;return r;
  }
  void AccumulateHit(int owner,int channel,int amount,Resolution r,int[,] damage){if(r.Before[owner,channel]<=0)return;int over=Math.Max(0,amount-r.Before[owner,channel]);damage[owner,channel]+=amount;r.Events.Add((owner==0?"내":"상대")+" 서버 "+(channel+1)+" · 피해 "+amount);int splash=over/2;if(splash>0){if(channel>0)damage[owner,channel-1]+=splash;if(channel<2)damage[owner,channel+1]+=splash;}}
  public int Alive(int player){int n=0;for(int c=0;c<3;c++)if(Health[player,c]>0)n++;return n;}
  public void NextRound(){if(Winner!=-1)return;if(Phase!=2)throw new InvalidOperationException("Resolve before next round");Round++;Attacker=1-Attacker;Phase=0;Array.Clear(Walls,0,3);for(int p=0;p<2;p++)Draw(p,1+3-Alive(p));SaveAction();}
  // The bot evaluates public ammunition only. It never reads Hidden or the other hand.
  public bool ChooseBot(out int index,out int target){index=-1;target=0;if(Actor!=1||Phase>=2||Winner!=-1)return false;var choices=new List<int[]>();float best=float.NegativeInfinity;
   for(int i=0;i<Hands[1].Count;i++){Card c=Hands[1][i];int count=c==Card.Reroute?6:(c==Card.Buffer||c==Card.Overflow?1:3);for(int t=0;t<count;t++){if(!CanPlay(1,i,t))continue;float score=0;
    if(c==Card.Buffer||c==Card.Overflow)score=Hands[1].Count<=(c==Card.Buffer?3:2)?20:-5;
    else if(c==Card.Inject||c==Card.ZeroDay){var route=Route(1,t);score=Health[0,route.End]>0?7:0;score-=Power[1,t]*3;foreach(var p in Enumerable.Range(0,3)){var incoming=Route(0,p);if(Power[0,p]>0&&Touch(route,incoming)&&Health[1,incoming.End]>0)score+=Phase==1?8:1;}if(c==Card.ZeroDay)score+=1;}
    else if(c==Card.Firewall){score=-2;for(int p=0;p<3;p++){var route=Route(0,p);if(Power[0,p]>0&&route.Cells.Any(q=>q.Lane==t)&&Health[1,route.End]>0)score+=12;}}
    else {float before=PublicScore();int row=t/2,old=Rungs[row];Rungs[row]=old<0?t%2:-1;score=(PublicScore()-before)*5-1;Rungs[row]=old;}
    if(score>best){best=score;choices.Clear();}if(score==best)choices.Add(new[]{i,t});}}
   if(best<=0||choices.Count==0)return false;var pick=choices[Next(choices.Count)];index=pick[0];target=pick[1];return true;
  }
  float PublicScore(){float n=0;for(int c=0;c<3;c++){if(Power[1,c]>0&&Health[0,Route(1,c).End]>0)n+=Power[1,c];if(Phase==1&&Power[0,c]>0&&Health[1,Route(0,c).End]>0)n-=Power[0,c];}return n;}
 }
}

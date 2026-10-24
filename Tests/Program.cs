using System;
using Breach;
class Program {
 static int count;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
 static Battle Empty(){var b=new Battle(7,0);b.Hands[0].Clear();b.Hands[1].Clear();return b;}
 static int Main(){try {Run();return 0;}catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}}
 static void Run(){
 var b=Empty();b.Hands[0].Add(Card.Inject);b.SaveAction();Check(b.Play(0,0,1),"inject accepted");Check(b.Power[0,1]==1&&b.Hands[0].Count==0,"use consumes card");b.Undo();Check(b.Power[0,1]==0&&b.Hands[0][0]==Card.Inject,"undo restores state");
 b=Empty();b.Hands[0].Add(Card.Reroute);Check(b.Play(0,0,0),"build rung");b.Hands[0].Add(Card.Reroute);Check(!b.Play(0,0,1),"adjacent same row forbidden");Check(b.Play(0,0,0)&&b.Rungs[0]==-1,"remove existing rung");
 b=Empty();b.Power[0,0]=2;b.Power[1,0]=1;var report=b.Resolve();Check(b.Health[1,0]==0&&b.Health[0,0]==1,"cancellation then hit");Check(b.Power[0,0]==0,"clear ammunition");
 b=Empty();b.Hands[0].Add(Card.ZeroDay);Check(b.Play(0,0,0)&&b.Power[0,0]==0&&b.Hidden.Count==1,"enhanced card deferred");b.Power[1,0]=4;b.Resolve();Check(b.Health[1,0]==0&&b.Health[0,0]==1,"zero day pierces stronger packet");
 b=Empty();b.Hands[0].Add(Card.Firewall);b.Play(0,0,0);b.Power[0,0]=3;b.Power[1,0]=3;b.Resolve();Check(b.Health[0,0]==1&&b.Health[1,0]==1,"firewall blocks both sides");
 b=Empty();b.Power[0,1]=3;b.Resolve();Check(b.Health[1,0]==0&&b.Health[1,1]==0&&b.Health[1,2]==0&&b.Winner==0,"overflow splash and victory");
 b=Empty();Array.Clear(b.Health,0,b.Health.Length);b.Resolve();Check(b.Winner==-2,"both sides destroyed is draw");
 b=Empty();b.Health[0,0]=0;b.Hands[0].Add(Card.Inject);Check(!b.Play(0,0,0),"offline source rejects attack");
 b=Empty();b.Hands[0].Add(Card.Buffer);b.SaveAction();b.Play(0,0,0);string draw=string.Join(",",b.Hands[0]);b.Undo();b.Play(0,0,0);Check(draw==string.Join(",",b.Hands[0]),"undo restores random stream");b.Draw(0,20);Check(b.Hands[0].Count==5,"hand limit five");
 b=Empty();b.Power[0,1]=2;b.EndAction();Check(b.Actor==1&&b.Phase==1,"defender gets turn");b.Hands[1].Add(Card.Inject);b.SaveAction();b.Play(1,0,2);b.Undo();Check(b.Power[0,1]==2&&b.Power[1,2]==0,"defender undo preserves attacker");
 b=Empty();b.EndAction();b.EndAction();Check(b.Phase==2,"resolution phase");b.Resolve();b.NextRound();Check(b.Round==2&&b.Attacker==1&&b.Actor==1,"alternate attacker");
 b=Empty();b.Rungs[0]=0;b.Rungs[1]=1;Check(b.Route(0,0).End==1&&b.Route(1,0).End==2,"opposite route traversal");
 b=Empty();b.Power[0,0]=3;b.Power[0,1]=3;b.Resolve();Check(b.Alive(1)==0,"simultaneous splash cannot erase a scheduled impact");
 b=Empty();b.Hands[0].Add(Card.Overflow);b.Play(0,0,0);Check(b.Hands[0].Count==0&&b.Hidden.Count==1,"overflow waits for reveal");b.Resolve();Check(b.Hands[0].Count==4,"overflow draws four at reveal");
 b=Empty();b.Health[0,0]=0;b.Health[0,1]=0;b.Phase=2;b.NextRound();Check(b.Hands[0].Count==3&&b.Hands[1].Count==1,"destroyed servers increase round draw");
 Console.WriteLine("TOTAL "+count);
 }
}

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
 b=Empty();b.Hands[0].Add(Card.Buffer);b.SaveAction();b.Play(0,0,0);Check(b.Hands[0].Count==2,"buffer draws two at once");string draw=string.Join(",",b.Hands[0]);Check(!b.Undo()&&draw==string.Join(",",b.Hands[0]),"drawn cards cannot be undone");b.Draw(0,20);Check(b.Hands[0].Count==5,"hand limit five");
 b=Empty();b.Hands[0].Add(Card.Inject);b.Hands[0].Add(Card.Reroute);b.Hands[0].Add(Card.Inject);b.SaveAction();b.Play(0,0,0);b.Play(0,0,0);b.Play(0,0,2);
 Check(b.UndoSteps==3&&b.Undo()&&b.Power[0,2]==0&&b.Rungs[0]==0&&b.Power[0,0]==1&&b.Hands[0].Count==1,"undo reverts only the last action");
 Check(b.Undo()&&b.Rungs[0]==-1&&b.Power[0,0]==1&&b.Hands[0].Count==2,"second undo reverts the previous action");
 Check(b.Undo()&&b.Power[0,0]==0&&b.Hands[0].Count==3&&!b.Undo(),"undo stops at turn start");
 b=Empty();b.Hands[0].Add(Card.Inject);b.Hands[0].Add(Card.Buffer);b.SaveAction();b.Play(0,0,0);b.Play(0,0,0);b.Hands[0].Clear();b.Hands[0].Add(Card.Inject);b.Play(0,0,1);
 Check(b.Undo()&&b.Power[0,1]==0&&!b.Undo()&&b.Power[0,0]==1,"draw locks every earlier action");
 b=Empty();b.Hands[0].Add(Card.Inject);b.SaveAction();b.Play(0,0,0);b.EndAction();Check(!b.Undo()&&b.Power[0,0]==1,"ending the action clears undo");
 b=Empty();b.Power[0,1]=2;b.EndAction();Check(b.Actor==1&&b.Phase==1,"defender gets turn");b.Hands[1].Add(Card.Inject);b.SaveAction();b.Play(1,0,2);b.Undo();Check(b.Power[0,1]==2&&b.Power[1,2]==0,"defender undo preserves attacker");
 b=Empty();b.EndAction();b.EndAction();Check(b.Phase==2,"resolution phase");b.Resolve();b.NextRound();Check(b.Round==2&&b.Attacker==1&&b.Actor==1,"alternate attacker");
 b=Empty();b.Rungs[0]=0;b.Rungs[1]=1;Check(b.Route(0,0).End==1&&b.Route(1,0).End==2,"opposite route traversal");
 b=Empty();b.Power[0,0]=3;b.Power[0,1]=3;b.Resolve();Check(b.Alive(1)==0,"simultaneous splash cannot erase a scheduled impact");
 b=Empty();b.Hands[0].Add(Card.Overflow);b.SaveAction();b.Play(0,0,0);Check(b.Hands[0].Count==4&&b.Hidden.Count==1&&b.Hidden[0].Card==Card.Overflow,"overflow draws four at once and stays face down");Check(!b.Undo(),"overflow draw cannot be undone");b.Resolve();Check(b.Hands[0].Count==4,"overflow reveal does not draw again");
 b=Empty();b.Health[0,0]=0;b.Health[0,1]=0;b.Phase=2;b.NextRound();Check(b.Hands[0].Count==3&&b.Hands[1].Count==1,"destroyed servers increase round draw");
 b=Empty();b.Phase=1;b.Power[0,0]=1;b.Power[1,0]=3;b.Hands[1].Add(Card.Firewall);
 Check(!b.ChooseBot(out var bi,out var bt),"bot does not wall off its winning counterattack");
 b=Empty();b.Attacker=1;b.Health[0,1]=b.Health[0,2]=0;b.Hidden.Add(new Secret(1,Card.ZeroDay,0));b.Hands[1].Add(Card.ZeroDay);
 Check(!b.ChooseBot(out bi,out bt),"bot does not repeat an already lethal reserved zero-day");
 b=Empty();b.Phase=1;b.Power[0,0]=3;b.Hands[1].Add(Card.Firewall);
 Check(b.ChooseBot(out bi,out bt)&&bt==0,"bot blocks an otherwise lethal public attack");
 b=Empty();b.Attacker=1;b.Health[0,0]=0;b.Power[1,0]=1;b.Hands[1].Add(Card.Reroute);
 Check(b.ChooseBot(out bi,out bt)&&bt%2==0,"bot reroutes ammunition toward a live server");
 var plain=Empty();plain.Phase=1;plain.Power[0,1]=2;plain.Hands[1].Add(Card.Inject);plain.Hands[1].Add(Card.Firewall);
 var hidden=Empty();hidden.Phase=1;hidden.Power[0,1]=2;hidden.Hands[1].Add(Card.Inject);hidden.Hands[1].Add(Card.Firewall);hidden.Hidden.Add(new Secret(0,Card.ZeroDay,2));hidden.Hands[0].Add(Card.Overflow);
 plain.ChooseBot(out var pi,out var pt);hidden.ChooseBot(out var hi,out var ht);
 Check(pi==hi&&pt==ht,"bot decision cannot read opponent secret or hand");
 Check(hidden.Power[0,1]==2&&hidden.Hidden.Count==1&&hidden.Hands[1].Count==2,"bot evaluation leaves live board untouched");
 b=Empty();b.Hands[0].Add(Card.Reroute);Check(b.Play(0,0,Battle.RungTarget(0,Battle.Mine))&&b.RungRiders[0]==0,"blue rung rides placer only");
 Check(b.Route(0,0).End==1&&b.Route(1,0).End==0,"blue rung is ignored by the opponent");
 b=Empty();b.Hands[1].Add(Card.Reroute);b.Phase=1;Check(b.Play(1,0,Battle.RungTarget(4,Battle.Theirs))&&b.RungRiders[2]==0,"red rung rides the placer's opponent");
 Check(b.Route(0,0).End==1&&b.Route(1,0).End==0,"red rung turns only the other side");
 b=Empty();b.Hands[0].Add(Card.Reroute);b.Play(0,0,Battle.RungTarget(0,Battle.Both));Check(b.RungRiders[0]==-1&&b.Route(0,0).End==1&&b.Route(1,0).End==1,"yellow rung rides both");
 b.Hands[0].Add(Card.Reroute);Check(!b.CanPlay(0,0,Battle.RungTarget(0,Battle.Theirs))&&b.Play(0,0,0)&&b.Rungs[0]==-1&&b.RungRiders[0]==-1,"removing a rung is one target and clears its color");
 b=Empty();b.Hands[0].Add(Card.Reroute);b.SaveAction();b.Play(0,0,Battle.RungTarget(2,Battle.Theirs));Check(b.Undo()&&b.RungRiders[1]==-1,"undo restores rung color");
 b=Empty();b.Attacker=1;b.Hands[1].Add(Card.Inject);b.Hands[1].Add(Card.Inject);b.Hands[1].Add(Card.Buffer);
 for(int g=0;g<6&&b.ChooseBot(out bi,out bt);g++)b.Play(1,bi,bt);
 Check(Math.Max(b.Power[1,0],Math.Max(b.Power[1,1],b.Power[1,2]))>=2,"attacking bot stacks injects on one channel");
 Console.WriteLine("TOTAL "+count);
 }
}

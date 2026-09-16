using System.IO;
using TaskbarTails;
using System.Text.Json;
var checks=new List<string>();
string resultPath=Path.GetFullPath(Path.Combine(Environment.CurrentDirectory,"..","network-results.txt"));
using var a=new RoomClient{SessionFile=null};using var b=new RoomClient{SessionFile=null};
a.Status+=s=>Console.WriteLine("A: "+s);b.Status+=s=>Console.WriteLine("B: "+s);
a.CurrentPet=()=>new PetEvent{Name="연결검사A",Species="cat"};b.CurrentPet=()=>new PetEvent{Name="연결검사B",Species="rabbit"};
try{
 await a.Open("시제품 연결 확인",true,new PetState{Name="연결검사A"});checks.Add("PASS: anonymous auth and private room creation");
 await b.Open(a.InviteCode,false,new PetState{Name="연결검사B",Species="rabbit"});checks.Add("PASS: invitation join and authorized private subscription");
 var motion=new TaskCompletionSource<PetEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
 b.Received+=e=>{if(e.Kind=="loaf")motion.TrySetResult(e);};
 await a.Publish(new PetEvent{Kind="loaf",Name="연결검사A",Species="cat",Action="loaf",X=.4});
 var received=await motion.Task.WaitAsync(TimeSpan.FromSeconds(12));
 if(received.UserId!=a.UserId||received.Action!="loaf")throw new Exception("Server sender identity or action mismatch");
 checks.Add("PASS: cat loaf motion delivered to another member with server-stamped sender ID");
 var message=new TaskCompletionSource<PetEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
 a.Received+=e=>{if(e.Kind=="message")message.TrySetResult(e);};
 await b.Publish(new PetEvent{Kind="message",Name="연결검사B",Species="rabbit",Message="말풍선 연결 테스트"});
 if((await message.Task.WaitAsync(TimeSpan.FromSeconds(12))).Message!="말풍선 연결 테스트")throw new Exception("Message mismatch");
 checks.Add("PASS: Korean speech bubble delivered in reverse direction");
 var roster=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);a.RosterChanged+=m=>roster.TrySetResult(m.Count);await a.RefreshRoster();if(await roster.Task!=2)throw new Exception("Roster mismatch");checks.Add("PASS: both participants appear in room roster");
 await b.Leave();await a.Leave();checks.Add("PASS: leave room");
 File.WriteAllLines(@"C:\Users\sangjun.park\Documents\Codex\2026-09-16\ghrt\work\network-results.txt",checks);Console.WriteLine(string.Join(Environment.NewLine,checks));
}catch(Exception e){checks.Add("FAIL: "+e.ToString());File.WriteAllLines(@"C:\Users\sangjun.park\Documents\Codex\2026-09-16\ghrt\work\network-results.txt",checks);Console.WriteLine(string.Join(Environment.NewLine,checks));Environment.ExitCode=1;}



using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
namespace TaskbarTails;
public sealed class PetWindow : Window
{
 public readonly PetVisual Visual = new();
 readonly App app;
 readonly bool demo;
 readonly Random random = new();
 IntPtr handle;
 double x, lift, phase, leap, bubbleUntil, actionStart, actionUntil, nextAction=18, turnPause, velocity, fallAge, targetX, targetLift;
 int direction=1;
 bool dragging,moved,falling;
 Native.Point dragStart;
 double dragWindowX,dragLift,dpi=1,fetchOrigin,fetchTarget,fetchTime;
 int fetchStage;
 string lastSpecies="";
 BallWindow? ball;
 public bool IsDemo=>demo;
 public bool IsRemote {get;}
 public string RemoteId {get;set;}="";
 public PetWindow(App owner,string name,string species,string coat,double initialX,bool isDemo=false,bool remote=false)
 {
  app=owner;demo=isDemo;IsRemote=remote;x=targetX=initialX;
  Width=160;Height=180;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
  Topmost=true;ShowInTaskbar=false;ShowActivated=false;ResizeMode=ResizeMode.NoResize;Title="Taskbar Tails · "+name;
  Visual.PetName=name;Visual.Species=lastSpecies=species;Content=Visual;Cursor=Cursors.Hand; Closed+=(_,_)=>ball?.Close(); IsVisibleChanged+=(_,_)=>{if(!IsVisible)ball?.Hide();};
  SourceInitialized+=(_,_)=>{handle=new WindowInteropHelper(this).Handle;Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x08000000|0x80);HwndSource.FromHwnd(handle)?.AddHook(Hook);Place();};
  MouseLeftButtonDown+=(_,e)=>{
   if(e.ClickCount==2){app.ShowPanel();return;}
   if(IsRemote){Say("친구가 함께 놀고 있어요");return;}
   Native.GetCursorPos(out dragStart);dragWindowX=x;dragLift=lift;dragging=true;moved=false;falling=false;fetchStage=0;Visual.BallVisible=false;ball?.Hide();
   CaptureMouse();e.Handled=true;
  };
  MouseMove+=(_,_)=>{
   if(!dragging)return;Native.GetCursorPos(out var p);
   if(Math.Abs(p.X-dragStart.X)+Math.Abs(p.Y-dragStart.Y)>5)moved=true;
   if(moved){x=dragWindowX+p.X-dragStart.X;lift=dragLift-(p.Y-dragStart.Y);Visual.ActionKey="";Place();}
  };
  MouseLeftButtonUp+=(_,_)=>{
   if(!dragging)return;dragging=false;ReleaseMouseCapture();
   if(!moved){if(!demo)app.Pet();else Say("안녕! 같이 놀자");Bounce();}
   else if(lift>8*dpi){falling=true;fallAge=0;Visual.Sleeping=false;if(!demo){app.State.Sleeping=false;app.Broadcast("parachute");}}
  };
  LostMouseCapture+=(_,_)=>{if(dragging){dragging=false;if(lift>0){falling=true;fallAge=0;}}};
  var menu=new ContextMenu();
  void Item(string text,Action action){var i=new MenuItem{Header=text};i.Click+=(_,_)=>action();menu.Items.Add(i);}
  Item("친구 관리 열기",app.ShowPanel);
  if(!demo&&!remote){Item("먹이 주기",app.Feed);Item("함께 놀기",app.Play);Item("잠자기 / 깨우기",app.ToggleSleep);}
  menu.Items.Add(new Separator());Item("프로그램 종료",app.Quit);ContextMenu=menu;
 }
 IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled){if(msg==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;}
 public void Say(string message){Visual.Bubble=message.Length>80?message[..80]:message;bubbleUntil=phase+Math.Clamp(3+message.Length*.12,6,12);}
 public void Bounce(){leap=.01;}
 public void Act(string key,bool announce=true)
 {
  if(key=="fetch"&&Visual.Species=="dog"){fetchStage=1;fetchTime=0;fetchOrigin=x;var w=Native.Primary().Work;fetchTarget=Math.Clamp(x+direction*240*dpi,w.Left,Math.Max(w.Left,w.Right-Width*dpi));Visual.BallVisible=true;ball??=new BallWindow();Visual.ActionKey="";}
  else{Visual.ActionKey=key;actionStart=phase;actionUntil=phase+(PetCatalog.HoldPose.Contains(key)?8:4.8);}
  nextAction=phase+random.Next(20,38);
  if(announce&&!demo&&!IsRemote)app.Broadcast(key);
 }
 void ResetMotion(){Visual.ActionKey="";fetchStage=0;Visual.BallVisible=false;ball?.Hide();}
 public void Step(double dt)
 {
  phase+=dt;
  if(!demo&&!IsRemote){Visual.PetName=app.State.Name;Visual.Species=app.State.Species;Visual.Sleeping=app.State.Sleeping;}
  // A new kind has its own motion set; never keep playing the previous kind's action key.
  if(Visual.Species!=lastSpecies){lastSpecies=Visual.Species;ResetMotion();}
  if(phase>=bubbleUntil)Visual.Bubble="";
  if(Visual.Sleeping){string rest=PetCatalog.RestPose(Visual.Species);if(rest.Length>0&&Visual.ActionKey!=rest){Visual.ActionKey=rest;actionStart=phase;}}
  else if(phase>actionUntil&&fetchStage==0)Visual.ActionKey="";
  if(!IsRemote&&!Visual.Sleeping&&!falling&&!dragging&&fetchStage==0&&phase>=nextAction){var k=PetCatalog.Get(Visual.Species);Act(random.Next(2)==0?k.FirstAction:k.SecondAction);}
  bool walking=false;
  if(IsRemote){x+=(targetX-x)*(1-Math.Exp(-dt*7));lift+=(targetLift-lift)*(1-Math.Exp(-dt*2.5));if(Visual.Parachute)x+=Math.Sin(phase*2.1)*dt*22*dpi;walking=Visual.Walking&&Math.Abs(targetX-x)>1;}
  else if(falling){fallAge+=dt;lift=Math.Max(0,lift-dt*Math.Min(100,40+fallAge*30)*dpi);x+=Math.Sin(fallAge*2.1)*dt*22*dpi;if(lift<=0){falling=false;Visual.Parachute=false;turnPause=.3;Say("사뿐! 착지 완료");if(!demo)app.Broadcast("land");}}
  else if(fetchStage>0&&!dragging){
   fetchTime+=dt;
   double target=fetchStage==1?fetchTarget:fetchOrigin;
   direction=target>=x?1:-1;walking=true;x+=direction*dt*65*dpi;
   Visual.BallX=(fetchTarget-x)/dpi+80;
   Visual.BallY=174-Math.Max(0,Math.Sin(Math.Min(1,fetchTime/.9)*Math.PI))*55;
   if(fetchStage==1){var ground=Native.Primary().Work.Bottom;double t=Math.Min(1,fetchTime/.9);ball?.Place(fetchOrigin+(fetchTarget-fetchOrigin)*t+73*dpi,ground-16*dpi-Math.Sin(t*Math.PI)*65*dpi,dpi);}
   if(Math.Abs(x-target)<8*dpi){x=target;if(fetchStage==1){fetchStage=2;Visual.BallVisible=false;ball?.Hide();Visual.ActionKey="fetch";actionStart=phase;}else{fetchStage=0;Visual.ActionKey="wag";actionStart=phase;actionUntil=phase+4.8;Say("공 가져왔어요!");}}
  }
  else if(!dragging&&!Visual.Sleeping&&Visual.ActionKey.Length==0&&!IsMouseOver){
   if(turnPause>0)turnPause-=dt;
   else{walking=true;velocity+=(direction*28*dpi-velocity)*Math.Min(1,dt*6);x+=velocity*dt;}
  }
  else velocity=0;
  // Parachute state is derived after this frame's movement so it opens on the very frame the drop starts.
  if(IsRemote){Visual.Parachute=lift>8*dpi;Visual.Sway=Visual.Parachute?Math.Sin(phase*2.1)*6:0;}
  else{Visual.Parachute=falling&&fallAge>.12;Visual.Sway=Math.Sin(fallAge*2.1)*6;}
  if(leap>0){leap+=dt;if(leap>.65)leap=0;}
  Visual.Jump=leap==0?0:Math.Sin(leap/.65*Math.PI)*39;
  Visual.Phase=phase;Visual.ActionTime=phase-actionStart;Visual.Walking=walking;Visual.FaceLeft=direction<0;
  Place();Visual.InvalidateVisual();
 }
 void Place()
 {
  if(handle==IntPtr.Zero)return;var work=Native.Primary().Work;dpi=Math.Max(1,Native.GetDpiForWindow(handle)/96.0);
  int width=(int)Math.Round(Width*dpi),height=(int)Math.Round(Height*dpi);double max=Math.Max(work.Left,work.Right-width);
  if(x<work.Left){x=work.Left;direction=1;velocity=0;turnPause=.3;}
  if(x>max){x=max;direction=-1;velocity=0;turnPause=.3;}
  lift=Math.Clamp(lift,0,Math.Max(0,work.Bottom-work.Top-height));
  Native.SetWindowPos(handle,new IntPtr(-1),(int)x,work.Bottom-height-(int)lift,width,height,0x0010);
 }
 public PetEvent Snapshot(string kind="state",string message="")
 {
  var w=Native.Primary().Work;
  return new PetEvent{Kind=kind,Name=Visual.PetName,Species=Visual.Species,Action=Visual.ActionKey,Message=message,X=Math.Clamp((x-w.Left)/Math.Max(1,w.Right-w.Left-Width*dpi),0,1),Lift=lift/Math.Max(1,w.Bottom-w.Top),Left=direction<0,Walking=Visual.Walking,Sleeping=Visual.Sleeping};
 }
 public void Apply(PetEvent e)
 {
  var w=Native.Primary().Work;Visual.PetName=e.Name;Visual.Species=PetCatalog.Valid(e.Species)?e.Species:"cat";
  if(Visual.Species!=lastSpecies){lastSpecies=Visual.Species;ResetMotion();}
  targetX=w.Left+Math.Clamp(e.X,0,1)*Math.Max(1,w.Right-w.Left-Width*dpi);direction=e.Left?-1:1;Visual.Walking=e.Walking;Visual.Sleeping=e.Sleeping;
  targetLift=Math.Clamp(e.Lift,0,1)*(w.Bottom-w.Top);
  if(e.Kind=="parachute"){lift=Math.Max(lift,targetLift);targetLift=0;}
  else if(e.Kind=="land"){targetLift=0;lift=Math.Min(lift,12*dpi);}
  if(e.Action!=Visual.ActionKey&&e.Kind=="state"){Visual.ActionKey=e.Action;actionStart=phase;actionUntil=phase+8;}
  if(e.Kind=="message")Say(e.Message);
  else if(e.Kind!="state"&&e.Kind!="land"&&e.Kind!="parachute"){Visual.ActionKey=e.Kind;actionStart=phase;actionUntil=phase+8;}
 }
 public bool InsideWorkArea(){if(!Native.GetWindowRect(handle,out var r))return false;var w=Native.Primary().Work;return r.Left>=w.Left&&r.Right<=w.Right&&Math.Abs(r.Bottom-w.Bottom)<=2;}
 public void TestDrop(double fraction){var w=Native.Primary().Work;lift=fraction*(w.Bottom-w.Top-Height*dpi);falling=true;fallAge=0;}
 public bool IsFalling=>falling;
 public bool IsFetching=>fetchStage>0;
}

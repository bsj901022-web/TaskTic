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
 // ball passed between friends: 1 = flying, 0 = idle
 int ballStage; double ballT, ballFromX, ballToX; bool ballOut;
 double frontUntil, nextChatter=35, refuseUntil;
 // Windows as platforms (v0.6.4): perch = the window the character stands on; climb = walking to (1) and up (2) a window edge.
 DesktopWindow? perch; double perchLeft, perchUntil, exposureChecked;
 int climbStage; DesktopWindow? climbWindow; bool climbLeftEdge; double climbWallX, nextClimb=45;
 bool remotePerched; int seenVersion=-1;
 int placedX=int.MinValue, placedY, placedW, placedH, lastSignature;
 readonly ContextMenu menu=new();
 public bool IsDemo=>demo;
 public bool IsRemote {get;}
 public string RemoteId {get;set;}="";
 public double CenterX=>x+Width*dpi/2;
 public double Dpi=>dpi;
 public string PetName=>Visual.PetName;
 public bool IsFalling=>falling;
 public bool IsFetching=>fetchStage>0;
 public bool IsBallMoving=>ballStage>0;
 public bool IsPerched=>perch!=null;
 public bool IsClimbing=>climbStage==2;
 public bool IsRefusing=>phase<refuseUntil;
 public bool IsBusy=>falling||dragging||fetchStage>0||ballStage>0||climbStage>0||Visual.ActionKey.Length>0||Visual.Bubble.Length>0;
 // Physical y of the feet (the ground line or the top edge of the window the character stands on).
 public double FeetY=>app.GroundY-lift;
 // Windows whose top edge is higher than this would put the character above the screen.
 int MinPlatformTop=>app.Screen.Work.Top+(int)Math.Round(140*dpi);
 public PetWindow(App owner,string name,string species,string coat,double initialX,bool isDemo=false,bool remote=false)
 {
  app=owner;demo=isDemo;IsRemote=remote;x=targetX=initialX;
  Width=160;Height=220;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
  Topmost=true;ShowInTaskbar=false;ShowActivated=false;ResizeMode=ResizeMode.NoResize;Title="Taskbar Tails · "+name;
  Visual.PetName=name;Visual.Species=lastSpecies=species;Content=Visual;Cursor=Cursors.Hand; Closed+=(_,_)=>ball?.Close(); IsVisibleChanged+=(_,_)=>{if(!IsVisible)ball?.Hide();};
  SourceInitialized+=(_,_)=>{handle=new WindowInteropHelper(this).Handle;Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x08000000|0x80);HwndSource.FromHwnd(handle)?.AddHook(Hook);Place(true);};
  MouseDown+=(_,e)=>{
   if(e.ChangedButton!=MouseButton.Middle)return;
   if(IsRemote)app.Poke(this);else if(!demo)app.OpenQuickChat();
   e.Handled=true;
  };
  MouseLeftButtonDown+=(_,e)=>{
   if(e.ClickCount==2){app.ShowPanel();return;}
   if(IsRemote){app.Poke(this);return;}
   Native.GetCursorPos(out dragStart);dragWindowX=x;dragLift=lift;dragging=true;moved=false;falling=false;fetchStage=0;Visual.BallVisible=false;ball?.Hide();
   CaptureMouse();e.Handled=true;
  };
  MouseMove+=(_,_)=>{
   if(!dragging)return;Native.GetCursorPos(out var p);
   if(!moved&&Math.Abs(p.X-dragStart.X)+Math.Abs(p.Y-dragStart.Y)>5){moved=true;LeaveSurfaces();}
   if(moved){x=dragWindowX+p.X-dragStart.X;lift=dragLift-(p.Y-dragStart.Y);Visual.ActionKey="";Place();}
  };
  MouseLeftButtonUp+=(_,_)=>{
   if(!dragging)return;dragging=false;ReleaseMouseCapture();
   if(!moved){if(!demo)app.Pet();else Say(L.Get("demo_click"));Bounce();if(lift>0&&perch==null&&climbStage!=2){falling=true;fallAge=Math.Max(fallAge,.2);}}
   else if(lift>8*dpi){falling=true;fallAge=0;Visual.Sleeping=false;if(!demo){app.State.Sleeping=false;app.Broadcast("parachute");}}
  };
  LostMouseCapture+=(_,_)=>{if(dragging){dragging=false;if(lift>0&&perch==null){falling=true;fallAge=0;}}};
  BuildMenu();ContextMenu=menu;
 }
 // Rebuilt when the language or the character kind changes.
 public void BuildMenu()
 {
  menu.Items.Clear();
  void Item(string text,Action action){var i=new MenuItem{Header=text};i.Click+=(_,_)=>action();menu.Items.Add(i);}
  Item(L.Get("tray_open"),app.ShowPanel);
  if(!demo&&!IsRemote){Item(L.Feed(PetCatalog.Get(app.State.Species).Group),app.Feed);Item(L.Get("ctx_play"),app.Play);Item(L.Get("ctx_sleep"),app.ToggleSleep);Item(L.Get("ctx_say"),app.OpenQuickChat);}
  if(IsRemote){Item(L.Get("ctx_poke"),()=>app.Poke(this));Item(L.Get("ctx_ball"),()=>app.ThrowBallTo(this));}
  menu.Items.Add(new Separator());Item(L.Get("ctx_quit"),app.Quit);
 }
 IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled){if(msg==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;}
 public void Say(string message){Visual.Bubble=message.Length>80?message[..80]:message;bubbleUntil=phase+Math.Clamp(3+message.Length*.12,6,12);}
 public void Bounce(){leap=.01;}
 public void Act(string key,bool announce=true)
 {
  if(key=="fetch"&&Visual.Species=="dog"){fetchStage=1;fetchTime=0;fetchOrigin=x;var w=app.Screen.Work;fetchTarget=Math.Clamp(x+direction*240*dpi,w.Left,Math.Max(w.Left,w.Right-Width*dpi));Visual.BallVisible=true;ball??=new BallWindow();Visual.ActionKey="";}
  else{Visual.ActionKey=key;actionStart=phase;actionUntil=phase+(PetCatalog.HoldPose.Contains(key)?8:4.8);}
  nextAction=phase+random.Next(20,38);
  if(announce&&!demo&&!IsRemote)app.Broadcast(key);
 }
 // Stops walking and faces the viewer for a few seconds; optionally says a short line.
 public void LookAtViewer(double seconds,string? line=null)
 {
  frontUntil=phase+seconds;velocity=0;turnPause=Math.Max(turnPause,seconds);
  if(line!=null)Say(line);
 }
 public bool IsFacingViewer=>Visual.FaceFront;
 // Too full to eat: faces the viewer and shakes its head for a moment.
 public void Refuse(){refuseUntil=phase+1.6;frontUntil=Math.Max(frontUntil,phase+2.4);velocity=0;turnPause=Math.Max(turnPause,2.4);Visual.ActionKey="";}
 // Turns toward the other character, stops for a moment and says hi.
 public void Greet(string otherName,double otherX)
 {
  direction=otherX>=CenterX?1:-1;velocity=0;turnPause=2.4;Visual.ActionKey="";
  Say(L.F("greet",otherName));Bounce();
 }
 public void ThrowBall(bool toRight)
 {
  var w=app.Screen.Work;ballOut=true;ballStage=1;ballT=0;direction=toRight?1:-1;velocity=0;turnPause=1.4;
  ballFromX=CenterX-8*dpi;ballToX=toRight?w.Right-16*dpi:w.Left;ball??=new BallWindow();Bounce();
 }
 public void ReceiveBall(bool fromLeft,string fromName)
 {
  var w=app.Screen.Work;ballOut=false;ballStage=1;ballT=0;direction=fromLeft?-1:1;velocity=0;turnPause=2.5;
  ballFromX=fromLeft?w.Left:w.Right-16*dpi;ballToX=CenterX-8*dpi;ball??=new BallWindow();Say(L.F("ball_received",fromName));
 }
 void ResetMotion(){Visual.ActionKey="";fetchStage=0;Visual.BallVisible=false;if(ballStage==0)ball?.Hide();}
 // --- windows as platforms ---
 void LeaveSurfaces(){perch=null;climbStage=0;climbWindow=null;Visual.Climb=0;}
 void Perch(DesktopWindow w,bool? fromLeftEdge=null)
 {
  perch=w;perchLeft=w.Bounds.Left;lift=app.GroundY-w.Bounds.Top;falling=false;Visual.Parachute=false;perchUntil=phase+random.Next(60,150);exposureChecked=phase;
  if(fromLeftEdge!=null)x=(fromLeftEdge.Value?w.Bounds.Left+14*dpi:w.Bounds.Right-14*dpi)-80*dpi;
  velocity=0;turnPause=.4;
 }
 void LandOn(DesktopWindow w){Perch(w);Say(L.Get("win_landed"));Bounce();if(!demo)app.Broadcast("land");}
 void Unperch(string line){perch=null;falling=true;fallAge=0;velocity=0;Say(line);if(!demo)app.Broadcast("parachute");}
 // Follows the window it stands on; drops off when the window closes, minimises, shrinks away or gets covered.
 void UpdatePerch()
 {
  // The window list changes a few times a second at most; between scans the perch record is still current.
  var w=Desktop.Version==seenVersion?perch:Desktop.Find(perch!.Handle);seenVersion=Desktop.Version;
  bool ok=w!=null&&w.Bounds.Top>=MinPlatformTop&&CenterX>=w.Bounds.Left+6*dpi&&CenterX<=w.Bounds.Right-6*dpi;
  if(ok&&phase-exposureChecked>.5){exposureChecked=phase;ok=Desktop.Exposed(w!,CenterX,w!.Bounds.Top+6);}
  if(!ok){Unperch(L.Get("win_gone"));return;}
  x+=w!.Bounds.Left-perchLeft;perchLeft=w.Bounds.Left;lift=app.GroundY-w.Bounds.Top;perch=w;
  if(phase>=perchUntil&&!Visual.Sleeping)Unperch(L.Get("win_down"));
 }
 // Turns around at the ends of the window's top edge, or now and then hops off with the parachute.
 void ClampToPerch()
 {
  var b=perch!.Bounds;double min=b.Left+12*dpi-80*dpi,max=b.Right-12*dpi-80*dpi;
  if(max<min){x=(min+max)/2;return;}
  if(x<min){x=min;EdgeReached(1);}else if(x>max){x=max;EdgeReached(-1);}
 }
 void EdgeReached(int back){if(random.Next(3)==0&&!Visual.Sleeping)Unperch(L.Get("win_edge"));else{direction=back;velocity=0;turnPause=.5;}}
 bool TryStartClimb()
 {
  Desktop.Refresh(app.Screen);
  var (w,leftEdge)=Desktop.ClimbTarget(CenterX,app.GroundY,app.Screen.Work,MinPlatformTop,dpi);
  if(w==null)return false;
  climbWindow=w;climbLeftEdge=leftEdge;climbStage=1;Visual.ActionKey="";return true;
 }
 void CancelClimb(){climbStage=0;climbWindow=null;Visual.Climb=0;}
 public void Step(double dt)
 {
  phase+=dt;Visual.SizeFactor=app.State.Scale/100.0;Visual.BubbleStyle=app.State.EffectiveBubbleStyle;Visual.NameStyle=app.State.NameStyle;Visual.ShowName=app.State.NameStyle>0;Visual.GoldName=!IsRemote&&!demo&&app.State.Level>=10;
  if(!demo&&!IsRemote){Visual.PetName=app.State.Name;Visual.Species=app.State.Species;Visual.Sleeping=app.State.Sleeping;}
  // A new kind has its own motion set; never keep playing the previous kind's action key.
  if(Visual.Species!=lastSpecies){lastSpecies=Visual.Species;ResetMotion();}
  if(phase>=bubbleUntil)Visual.Bubble="";
  if(Visual.Sleeping){string rest=PetCatalog.RestPose(Visual.Species);if(rest.Length>0&&Visual.ActionKey!=rest){Visual.ActionKey=rest;actionStart=phase;}}
  else if(phase>actionUntil&&fetchStage==0)Visual.ActionKey="";
  if(!IsRemote&&!Visual.Sleeping&&!falling&&!dragging&&fetchStage==0&&ballStage==0&&climbStage==0&&phase>=nextAction){var k=PetCatalog.Get(Visual.Species);Act(random.Next(2)==0?k.FirstAction:k.SecondAction);}
  // Every 40-90 s the local character turns to the viewer and says something that fits the moment.
  if(!IsRemote&&!demo&&!Visual.Sleeping&&!falling&&!dragging&&fetchStage==0&&ballStage==0&&climbStage==0&&Visual.ActionKey.Length==0&&Visual.Bubble.Length==0&&phase>=nextChatter){nextChatter=phase+random.Next(40,90);LookAtViewer(random.Next(4,7),app.Chatter());}
  if(!IsRemote)Visual.FaceFront=!Visual.Sleeping&&!falling&&!dragging&&fetchStage==0&&ballStage==0&&climbStage!=2&&(phase<frontUntil||(IsMouseOver&&!demo));
  if(!IsRemote&&!dragging&&!falling&&lift>0&&perch==null&&climbStage!=2){falling=true;fallAge=Math.Max(fallAge,.2);}
  if(!IsRemote&&perch!=null&&!dragging)UpdatePerch();
  bool walking=false;
  if(IsRemote){
   // Friends walk continuously at the shared pace; the 3-second snapshot only corrects drift, so no stop-and-jump.
   if(Visual.Walking&&!Visual.Sleeping&&turnPause<=0)x+=direction*28*dpi*dt;
   x+=(targetX-x)*(1-Math.Exp(-dt*(Visual.Walking?1.2:7)));
   lift+=(targetLift-lift)*(1-Math.Exp(-dt*2.5));
   if(lift>8*dpi&&!remotePerched)x+=Math.Sin(phase*2.1)*dt*22*dpi;
   if(turnPause>0)turnPause-=dt;
   walking=Visual.Walking&&!Visual.Sleeping&&turnPause<=0&&!Visual.FaceFront;
  }
  else if(falling){
   fallAge+=dt;double feetBefore=FeetY;
   lift=Math.Max(0,lift-dt*Math.Min(100,40+fallAge*30)*dpi);x+=Math.Sin(fallAge*2.1)*dt*22*dpi;
   // A window top edge under the feet catches the character on the way down.
   var platform=app.State.WindowPlay?Desktop.PlatformBetween(CenterX,feetBefore,FeetY,app.GroundY,MinPlatformTop,12*dpi):null;
   if(platform!=null)LandOn(platform);
   else if(lift<=0){falling=false;Visual.Parachute=false;turnPause=.3;Say(L.Get("landed"));if(!demo)app.Broadcast("land");}
  }
  else if(fetchStage>0&&!dragging){
   fetchTime+=dt;
   double target=fetchStage==1?fetchTarget:fetchOrigin;
   direction=target>=x?1:-1;walking=true;x+=direction*dt*65*dpi;
   if(fetchStage==1){double t=Math.Min(1,fetchTime/.9);ball?.Place(fetchOrigin+(fetchTarget-fetchOrigin)*t+73*dpi,app.GroundY-16*dpi-Math.Sin(t*Math.PI)*65*dpi,dpi);}
   if(Math.Abs(x-target)<8*dpi){x=target;if(fetchStage==1){fetchStage=2;Visual.BallVisible=false;ball?.Hide();Visual.ActionKey="fetch";actionStart=phase;}else{fetchStage=0;Visual.ActionKey="wag";actionStart=phase;actionUntil=phase+4.8;Say(L.Get("fetched"));}}
  }
  else if(climbStage==1&&!dragging){
   // Walks along the ground to the foot of the window edge.
   var w=Desktop.Find(climbWindow!.Handle);
   if(w==null||!Desktop.EdgeUsable(w,climbLeftEdge,app.GroundY,app.Screen.Work,dpi))CancelClimb();
   else{
    climbWallX=climbLeftEdge?w.Bounds.Left:w.Bounds.Right;direction=climbWallX>=CenterX?1:-1;walking=true;x+=direction*dt*45*dpi;
    if(Math.Abs(CenterX-climbWallX)<4*dpi){climbStage=2;x=climbWallX-80*dpi+(climbLeftEdge?-1:1);velocity=0;direction=climbLeftEdge?1:-1;Visual.Climb=climbLeftEdge?-90:90;Visual.ActionKey="";Say(L.Get("win_climb"));}
   }
  }
  else if(climbStage==2&&!dragging){
   // Climbs the edge with the walk frames turned sideways (feet against the window), then steps onto the top edge.
   var w=Desktop.Find(climbWindow!.Handle);
   if(w==null||w.Bounds.Top<MinPlatformTop){CancelClimb();falling=true;fallAge=0;}
   else{
    climbWallX=climbLeftEdge?w.Bounds.Left:w.Bounds.Right;x=climbWallX-80*dpi+(climbLeftEdge?-1:1);walking=true;direction=climbLeftEdge?1:-1;
    double top=app.GroundY-w.Bounds.Top;lift=Math.Min(top,lift+dt*60*dpi);
    if(lift>=top-.5){Visual.Climb=0;climbStage=0;Perch(w,climbLeftEdge);Say(L.Get("win_perch"));Bounce();if(!demo)app.Broadcast("state");}
   }
  }
  else if(!dragging&&!Visual.Sleeping&&Visual.ActionKey.Length==0&&!IsMouseOver&&!Visual.FaceFront){
   if(turnPause>0)turnPause-=dt;
   else{walking=true;velocity+=(direction*28*dpi-velocity)*Math.Min(1,dt*6);x+=velocity*dt;}
   // Every couple of minutes a nearby window that reaches the taskbar invites a climb.
   if(perch==null&&app.State.WindowPlay&&phase>=nextClimb){nextClimb=phase+random.Next(90,200);TryStartClimb();}
  }
  else velocity=0;
  if(perch!=null&&!dragging)ClampToPerch();
  if(ballStage>0){
   ballT+=dt/1.1;double t=Math.Min(1,ballT);
   ball?.Place(ballFromX+(ballToX-ballFromX)*t,app.GroundY-16*dpi-Math.Sin(t*Math.PI)*90*dpi,dpi);
   if(t>=1){ballStage=0;ball?.Hide();if(!ballOut){Say(L.Get("ball_caught"));if(Visual.Species=="dog"){Visual.ActionKey="fetch";actionStart=phase;actionUntil=phase+3;}else Bounce();}}
  }
  // Parachute state is derived after this frame's movement so it opens on the very frame the drop starts.
  if(IsRemote){Visual.Parachute=lift>8*dpi&&!remotePerched;Visual.Sway=Visual.Parachute?Math.Sin(phase*2.1)*6:0;}
  else{Visual.Parachute=falling&&fallAge>.12;Visual.Sway=Math.Sin(fallAge*2.1)*6;}
  Visual.Shake=phase<refuseUntil?Math.Sin((refuseUntil-phase)*24)*3:0;
  if(leap>0){leap+=dt;if(leap>.65)leap=0;}
  Visual.Jump=leap==0?0:Math.Sin(leap/.65*Math.PI)*39;
  Visual.Phase=phase;Visual.ActionTime=phase-actionStart;Visual.Walking=walking;Visual.FaceLeft=direction<0;
  Place();
  // Redraw only when something visible changed (sprite frame, bubble, jump, parachute...), not 60 times a second.
  int signature=HashCode.Combine((int)(phase*SpriteSet.WalkFps),(int)(Visual.ActionTime*SpriteSet.ActionFps),Visual.Bubble,Visual.ActionKey,HashCode.Combine(Visual.Walking,Visual.FaceLeft,Visual.Sleeping,Visual.Parachute,(int)(Visual.Jump*4),(int)(Visual.Sway*4),Visual.Species,Visual.SizeFactor),HashCode.Combine(Visual.BubbleStyle,Visual.GoldName,Visual.PetName,Visual.FaceFront,Visual.NameStyle,Visual.ShowName,(int)(Visual.Shake*4),Visual.Climb));
  if(signature!=lastSignature){lastSignature=signature;Visual.InvalidateVisual();}
 }
 // Moves the overlay only when its pixel position changed. assertTop re-applies the top-most z-order (done every few seconds by App).
 public void Place(bool assertTop=false)
 {
  if(handle==IntPtr.Zero)return;var work=app.Screen.Work;int ground=app.GroundY;dpi=Math.Max(1,Native.GetDpiForWindow(handle)/96.0);
  int width=(int)Math.Round(Width*dpi),height=(int)Math.Round(Height*dpi);double max=Math.Max(work.Left,work.Right-width);
  if(x<work.Left){x=work.Left;direction=1;velocity=0;turnPause=.3;}
  if(x>max){x=max;direction=-1;velocity=0;turnPause=.3;}
  // On a window the transparent headroom above the sprite may leave the screen; only the sprite itself has to stay visible.
  bool elevated=perch!=null||climbStage==2||remotePerched;
  lift=Math.Clamp(lift,0,Math.Max(0,ground-work.Top-(elevated?110*dpi:height)));
  int px=(int)Math.Round(x),py=ground-height-(int)Math.Round(lift);
  if(!assertTop&&px==placedX&&py==placedY&&width==placedW&&height==placedH)return;
  Native.SetWindowPos(handle,assertTop?Native.TopMost:IntPtr.Zero,px,py,width,height,assertTop?Native.SwpNoActivate:Native.SwpNoActivate|Native.SwpNoZOrder);
  placedX=px;placedY=py;placedW=width;placedH=height;
 }
 // Physical pixels: horizontal centre of the pet and the top edge of its overlay window (where bubbles appear).
 public (double X,double Top) HeadPoint()=>(CenterX,app.GroundY-Height*dpi-lift);
 public PetEvent Snapshot(string kind="state",string message="")
 {
  var w=app.Screen.Work;
  return new PetEvent{Kind=kind,Name=Visual.PetName,Species=Visual.Species,Action=Visual.ActionKey,Message=message,X=Math.Clamp((x-w.Left)/Math.Max(1,w.Right-w.Left-Width*dpi),0,1),Lift=lift/Math.Max(1,app.GroundY-w.Top),Left=direction<0,Walking=Visual.Walking,Sleeping=Visual.Sleeping,Front=Visual.FaceFront,Perched=perch!=null||climbStage==2};
 }
 public void Apply(PetEvent e)
 {
  var w=app.Screen.Work;Visual.PetName=e.Name;Visual.Species=PetCatalog.Valid(e.Species)?e.Species:"cat";
  if(Visual.Species!=lastSpecies){lastSpecies=Visual.Species;ResetMotion();}
  targetX=w.Left+Math.Clamp(e.X,0,1)*Math.Max(1,w.Right-w.Left-Width*dpi);direction=e.Left?-1:1;Visual.Walking=e.Walking;Visual.Sleeping=e.Sleeping;Visual.FaceFront=e.Front;remotePerched=e.Perched;
  targetLift=Math.Clamp(e.Lift,0,1)*(app.GroundY-w.Top);
  if(e.Kind=="parachute"){lift=Math.Max(lift,targetLift);targetLift=0;remotePerched=false;}
  else if(e.Kind=="land"){if(!e.Perched){targetLift=0;lift=Math.Min(lift,12*dpi);}}
  if(e.Action!=Visual.ActionKey&&e.Kind=="state"){Visual.ActionKey=e.Action;actionStart=phase;actionUntil=phase+8;}
  if(e.Kind=="message")Say(e.Message);
  else if(e.Kind is not ("state" or "land" or "parachute" or "greet" or "poke" or "ball")){Visual.ActionKey=e.Kind;actionStart=phase;actionUntil=phase+8;}
 }
 public bool InsideWorkArea(){if(!Native.GetWindowRect(handle,out var r))return false;var w=app.Screen.Work;return r.Left>=w.Left&&r.Right<=w.Right&&Math.Abs(r.Bottom-app.GroundY)<=2;}
 public void TestDrop(double fraction){LeaveSurfaces();lift=fraction*(app.GroundY-app.Screen.Work.Top-Height*dpi);falling=true;fallAge=0;}
 public void TestMoveTo(double screenX){x=screenX;targetX=screenX;Place();}
 public void TestInterruptFall(){falling=false;dragging=false;}
 public bool TestClimb()=>TryStartClimb();
}

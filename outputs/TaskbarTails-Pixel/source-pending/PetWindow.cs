using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
namespace TaskbarTails;
// One character overlay. Local (mine), demo (local sample friends) or remote (a room member's character).
// Remote copies are simulated locally: they walk on their own and only react to events the owner sends
// (parachute, balloon ride, landing, motions, sleep, bubbles), so network latency never makes them stutter.
// Surfaces: 0 ground (taskbar), 1 left screen edge (climbing), 2 right screen edge, 3 ceiling (upside down along the top edge).
public sealed class PetWindow : Window
{
 public readonly PetVisual Visual = new();
 readonly App app;
 readonly bool demo;
 readonly Random random = new();
 IntPtr handle;
 double x, lift, phase, leap, bubbleUntil, actionStart, actionUntil, nextAction=18, turnPause, velocity, fallAge;
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
 // Windows as platforms: perch = the window the character stands on; rise = a balloon ride up to a window's top edge (stage 1 walk under it, 2 float up).
 DesktopWindow? perch; double perchLeft, perchUntil, exposureChecked;
 int riseStage; DesktopWindow? riseWindow; double riseX, riseTarget, nextRise=45;
 int seenVersion=-1;
 // Screen edges: which surface the feet are on, when the next wall/ceiling decision may happen, and when the next wall climb may start.
 int surface, wallGoal; double nextEdgeDecision, nextWallTry;
 // Remote copies: an invisible ledge at the height the owner reported (perched on one of their windows), or a balloon ride toward it.
 bool remotePerched; double remoteLedgeX, remoteLedgeLift;
 bool sentSleeping;
 int placedX=int.MinValue, placedY, placedW, placedH, lastSignature;
 readonly ContextMenu menu=new();
 // Feet position inside the overlay (DIP): canvas x=80 sits at 110 because the 160-wide canvas is centred in the 220-wide window.
 const double FeetLocalX=110, FeetLocalY=214, WallFeetLocalY=114, CeilingFeetLocalY=46;
 public bool IsDemo=>demo;
 public bool IsRemote {get;}
 public string RemoteId {get;set;}="";
 public double CenterX=>x+Width*dpi/2;
 public double Dpi=>dpi;
 public string PetName=>Visual.PetName;
 public bool IsFalling=>falling;
 public bool IsFetching=>fetchStage>0;
 public bool IsBallMoving=>ballStage>0;
 public bool IsPerched=>perch!=null||remotePerched;
 public bool IsRising=>riseStage==2;
 public bool IsRefusing=>phase<refuseUntil;
 public int Surface=>surface;
 public bool IsBusy=>falling||dragging||fetchStage>0||ballStage>0||riseStage>0||surface!=0||Visual.ActionKey.Length>0||Visual.Bubble.Length>0;
 // Physical y of the feet (the ground line, a window's top edge, a point on a screen edge, or the top of the screen).
 public double FeetY=>app.GroundY-lift;
 // Windows whose top edge is higher than this would put the character above the screen.
 int MinPlatformTop=>app.Screen.Work.Top+(int)Math.Round(140*dpi);
 double Span=>Math.Max(1,app.Screen.Work.Right-app.Screen.Work.Left-Width*dpi);
 double WallTop=>Math.Max(0,app.GroundY-app.Screen.Work.Top);
 public PetWindow(App owner,string name,string species,string coat,double initialX,bool isDemo=false,bool remote=false)
 {
  app=owner;demo=isDemo;IsRemote=remote;x=initialX;
  Width=220;Height=220;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
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
   if(!moved){if(!demo)app.Pet();else Say(L.Get("demo_click"));Bounce();if(lift>0&&perch==null&&riseStage!=2&&surface==0){falling=true;fallAge=Math.Max(fallAge,.2);}}
   else Released();
  };
  LostMouseCapture+=(_,_)=>{if(dragging){dragging=false;if(lift>0&&perch==null&&surface==0){falling=true;fallAge=0;}}};
  BuildMenu();ContextMenu=menu;
 }
 // Rebuilt when the language or the character kind changes.
 public void BuildMenu()
 {
  menu.Items.Clear();
  void Item(string text,Action action){var i=new MenuItem{Header=text};i.Click+=(_,_)=>action();menu.Items.Add(i);}
  Item(L.Get("tray_open"),app.ShowPanel);
  if(!demo&&!IsRemote){Item(L.Feed(PetCatalog.Get(app.State.Species).Group),app.Feed);Item(L.Get("ctx_play"),app.Play);Item(L.Get("ctx_sleep"),app.ToggleSleep);Item(L.Get("ctx_say"),app.OpenQuickChat);}
  if(!IsRemote)Item(L.Get("ctx_wall"),RequestWall);
  if(IsRemote){Item(L.Get("ctx_poke"),()=>app.Poke(this));Item(L.Get("ctx_ball"),()=>app.ThrowBallTo(this));}
  menu.Items.Add(new Separator());Item(L.Get("ctx_quit"),app.Quit);
 }
 IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled){if(msg==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;}
 // auto = a line the character says by itself (chatter, reactions, landing...): drawn as a thought cloud and hidden while automatic bubbles are off.
 // Typed messages (mine or a friend's) are speech bubbles and always show.
 public void Say(string message,bool auto=true){if(auto&&!app.State.AutoBubbles)return;Visual.Bubble=message.Length>80?message[..80]:message;Visual.BubbleTyped=!auto;bubbleUntil=phase+Math.Clamp(3+message.Length*.12,6,12);}
 public void Bounce(){leap=.01;}
 public void Act(string key,bool announce=true)
 {
  if(key=="fetch"&&Visual.Species=="dog"&&surface==0){fetchStage=1;fetchTime=0;fetchOrigin=x;var w=app.Screen.Work;fetchTarget=Math.Clamp(x+direction*240*dpi,w.Left,Math.Max(w.Left,w.Right-Width*dpi));Visual.BallVisible=true;ball??=new BallWindow();Visual.ActionKey="";}
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
 // Turns toward the other character, stops for a moment and says hi (used once when a friend joins the room).
 public void Greet(string otherName,double otherX)
 {
  if(surface==0)direction=otherX>=CenterX?1:-1;velocity=0;turnPause=2.4;Visual.ActionKey="";
  Say(L.F("greet",otherName));Bounce();
 }
 public void ThrowBall(bool toRight)
 {
  var w=app.Screen.Work;ballOut=true;ballStage=1;ballT=0;if(surface==0)direction=toRight?1:-1;velocity=0;turnPause=1.4;
  ballFromX=CenterX-8*dpi;ballToX=toRight?w.Right-16*dpi:w.Left;ball??=new BallWindow();Bounce();
 }
 public void ReceiveBall(bool fromLeft,string fromName)
 {
  var w=app.Screen.Work;ballOut=false;ballStage=1;ballT=0;if(surface==0)direction=fromLeft?-1:1;velocity=0;turnPause=2.5;
  ballFromX=fromLeft?w.Left:w.Right-16*dpi;ballToX=CenterX-8*dpi;ball??=new BallWindow();Say(L.F("ball_received",fromName));
 }
 void ResetMotion(){Visual.ActionKey="";fetchStage=0;Visual.BallVisible=false;if(ballStage==0)ball?.Hide();}
 // --- windows as platforms (local) ---
 void LeaveSurfaces(){perch=null;riseStage=0;riseWindow=null;Visual.Balloon=false;surface=0;}
 void Perch(DesktopWindow w)
 {
  perch=w;perchLeft=w.Bounds.Left;lift=app.GroundY-w.Bounds.Top;falling=false;Visual.Parachute=false;Visual.Balloon=false;perchUntil=phase+random.Next(60,150);exposureChecked=phase;
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
 // Turns around at the ends of the ledge (a window's top edge, or the small invisible ledge a remote copy stands on), or now and then hops off.
 void ClampToLedge(double min,double max,bool canHopOff)
 {
  if(max<min){x=(min+max)/2;return;}
  if(x<min){x=min;EdgeReached(1,canHopOff);}else if(x>max){x=max;EdgeReached(-1,canHopOff);}
 }
 void EdgeReached(int back,bool canHopOff){if(canHopOff&&random.Next(3)==0&&!Visual.Sleeping)Unperch(L.Get("win_edge"));else{direction=back;velocity=0;turnPause=.5;}}
 // Picks a window top edge nearby and walks under it; the balloon ride starts when the feet are below that point.
 bool TryStartRise()
 {
  Desktop.Refresh(app.Screen);
  var (w,targetX)=Desktop.RiseTarget(CenterX,app.GroundY,app.Screen.Work,MinPlatformTop,dpi);
  if(w==null)return false;
  riseWindow=w;riseX=targetX;riseStage=1;Visual.ActionKey="";return true;
 }
 void CancelRise(){riseStage=0;riseWindow=null;Visual.Balloon=false;}
 // --- screen edges ---
 // Steps from the ground onto the left (1) or right (2) screen edge and starts climbing.
 void StartWall(int side)
 {
  var w=app.Screen.Work;surface=side;wallGoal=0;lift=0;direction=1;velocity=0;turnPause=.2;Visual.ActionKey="";
  x=(side==1?w.Left:w.Right)-FeetLocalX*dpi;
  Say(L.Get("edge_climb"));
 }
 // Dropped after a drag: against the left or right screen edge the character grabs the wall at that height; anywhere else it parachutes down.
 void Released()
 {
  var w=app.Screen.Work;Native.GetCursorPos(out var p);
  if(app.State.EdgeRoam&&perch==null&&(p.X<=w.Left+30*dpi||p.X>=w.Right-30*dpi)){double h=lift;StartWall(p.X<=w.Left+30*dpi?1:2);lift=Math.Clamp(h,0,WallTop);Place();return;}
  if(lift>8*dpi){falling=true;fallAge=0;Visual.Sleeping=false;if(!demo){app.State.Sleeping=false;app.Broadcast("parachute");}}
 }
 // Context menu: walk to the nearest screen edge and climb it (no coin toss).
 public void RequestWall()
 {
  if(surface!=0||falling||dragging)return;
  var w=app.Screen.Work;wallGoal=CenterX<(w.Left+w.Right)/2.0?1:2;direction=wallGoal==1?-1:1;velocity=0;turnPause=0;frontUntil=0;Visual.ActionKey="";perch=null;CancelRise();fetchStage=0;
 }
 // Lets go of the wall or ceiling: a parachute drop from where the feet were.
 void LetGo(string line){int from=surface;surface=0;falling=true;fallAge=0;velocity=0;if(from==1)x=app.Screen.Work.Left;else if(from==2)x=app.Screen.Work.Right-Width*dpi;Say(line);if(!demo&&!IsRemote)app.Broadcast("parachute");}
 // Climbing and ceiling walking. Speeds are a little slower than on the ground; every few seconds the character may turn or let go.
 void EdgeStep(double dt,ref bool walking)
 {
  var w=app.Screen.Work;
  if(turnPause>0){turnPause-=dt;return;}
  walking=true;
  if(surface==3){
   x+=direction*28*dpi*dt;
   double minX=w.Left+8*dpi-FeetLocalX*dpi,maxX=w.Right-8*dpi-FeetLocalX*dpi;
   if(x<=minX&&direction<0){surface=1;lift=WallTop;x=w.Left-FeetLocalX*dpi;direction=-1;turnPause=.2;}
   else if(x>=maxX&&direction>0){surface=2;lift=WallTop;x=w.Right-FeetLocalX*dpi;direction=-1;turnPause=.2;}
  }
  else{
   lift+=direction*30*dpi*dt;
   if(lift>=WallTop&&direction>0){lift=WallTop;int side=surface;surface=3;x=(side==1?w.Left+8*dpi:w.Right-8*dpi)-FeetLocalX*dpi;direction=side==1?1:-1;turnPause=.2;}
   else if(lift<=0&&direction<0){lift=0;int side=surface;surface=0;x=side==1?w.Left:w.Right-Width*dpi;direction=side==1?1:-1;turnPause=.3;Say(L.Get("edge_down"));}
  }
  // Not in the smoke test, so the route stays predictable there.
  if(!app.IsSmokeTest&&phase>=nextEdgeDecision){
   nextEdgeDecision=phase+3;int roll=random.Next(100);
   if(roll<8&&!Visual.Sleeping)LetGo(L.Get("edge_letgo"));
   else if(roll<20){direction=-direction;velocity=0;turnPause=.4;}
  }
 }
 public void Step(double dt)
 {
  phase+=dt;Visual.SizeFactor=app.State.Scale/100.0;Visual.BubbleStyle=app.State.EffectiveBubbleStyle;Visual.NameStyle=app.State.NameStyle;Visual.ShowName=app.State.NameStyle>0;Visual.GoldName=!IsRemote&&!demo&&app.State.Level>=10;
  if(!demo&&!IsRemote){Visual.PetName=app.State.Name;Visual.Species=app.State.Species;Visual.Sleeping=app.State.Sleeping;}
  // A new kind has its own motion set; never keep playing the previous kind's action key.
  if(Visual.Species!=lastSpecies){lastSpecies=Visual.Species;ResetMotion();}
  if(phase>=bubbleUntil)Visual.Bubble="";
  if(Visual.Sleeping){string rest=PetCatalog.RestPose(Visual.Species);if(rest.Length>0&&Visual.ActionKey!=rest){Visual.ActionKey=rest;actionStart=phase;}}
  else if(phase>actionUntil&&fetchStage==0)Visual.ActionKey="";
  bool free=!Visual.Sleeping&&!falling&&!dragging&&fetchStage==0&&ballStage==0&&riseStage==0&&surface==0;
  if(!IsRemote&&free&&phase>=nextAction){var k=PetCatalog.Get(Visual.Species);Act(random.Next(2)==0?k.FirstAction:k.SecondAction);}
  // Every 40-90 s the local character turns to the viewer and says something that fits the moment.
  if(!IsRemote&&!demo&&free&&Visual.ActionKey.Length==0&&Visual.Bubble.Length==0&&phase>=nextChatter){nextChatter=phase+random.Next(40,90);LookAtViewer(random.Next(4,7),app.Chatter());}
  Visual.FaceFront=free&&(phase<frontUntil||(IsMouseOver&&!demo));
  if(!IsRemote&&!dragging&&!falling&&lift>0&&perch==null&&riseStage!=2&&surface==0){falling=true;fallAge=Math.Max(fallAge,.2);}
  if(!IsRemote&&perch!=null&&!dragging)UpdatePerch();
  bool walking=false;
  if(falling){
   fallAge+=dt;double feetBefore=FeetY;
   lift=Math.Max(0,lift-dt*Math.Min(100,40+fallAge*30)*dpi);x+=Math.Sin(fallAge*2.1)*dt*22*dpi;
   // A window top edge under the feet catches the local character on the way down.
   var platform=!IsRemote&&app.State.WindowPlay?Desktop.PlatformBetween(CenterX,feetBefore,FeetY,app.GroundY,MinPlatformTop,12*dpi):null;
   if(platform!=null)LandOn(platform);
   else if(lift<=0){falling=false;Visual.Parachute=false;turnPause=.3;Say(L.Get("landed"));if(!demo&&!IsRemote)app.Broadcast("land");}
  }
  else if(surface!=0&&!dragging){
   if(!Visual.Sleeping&&Visual.ActionKey.Length==0&&!IsMouseOver)EdgeStep(dt,ref walking);
  }
  else if(fetchStage>0&&!dragging){
   fetchTime+=dt;
   double target=fetchStage==1?fetchTarget:fetchOrigin;
   direction=target>=x?1:-1;walking=true;x+=direction*dt*65*dpi;
   if(fetchStage==1){double t=Math.Min(1,fetchTime/.9);ball?.Place(fetchOrigin+(fetchTarget-fetchOrigin)*t+(FeetLocalX-7)*dpi,app.GroundY-16*dpi-Math.Sin(t*Math.PI)*65*dpi,dpi);}
   if(Math.Abs(x-target)<8*dpi){x=target;if(fetchStage==1){fetchStage=2;Visual.BallVisible=false;ball?.Hide();Visual.ActionKey="fetch";actionStart=phase;}else{fetchStage=0;Visual.ActionKey="wag";actionStart=phase;actionUntil=phase+4.8;Say(L.Get("fetched"));}}
  }
  else if(riseStage==1&&!dragging){
   // Walks along the ground until the feet are under the chosen point of the window's top edge.
   var w=IsRemote?null:Desktop.Find(riseWindow!.Handle);
   if(!IsRemote&&(w==null||w.Bounds.Top<MinPlatformTop||riseX<w.Bounds.Left+12*dpi||riseX>w.Bounds.Right-12*dpi))CancelRise();
   else{
    direction=riseX>=CenterX?1:-1;walking=true;x+=direction*dt*45*dpi;
    if(Math.Abs(CenterX-riseX)<4*dpi){
     x=riseX-Width*dpi/2;riseStage=2;velocity=0;Visual.Balloon=true;Visual.ActionKey="";
     if(!IsRemote){riseTarget=app.GroundY-w!.Bounds.Top;Say(L.Get("win_balloon"));if(!demo)app.Broadcast("balloon");}
    }
   }
  }
  else if(riseStage==2&&!dragging){
   // Floats up under the balloon, swaying a little, and steps onto the top edge (local) or the reported ledge (remote copy).
   var w=IsRemote?null:Desktop.Find(riseWindow!.Handle);
   if(!IsRemote){if(w==null||w.Bounds.Top<MinPlatformTop){CancelRise();falling=true;fallAge=0;}else riseTarget=app.GroundY-w.Bounds.Top;}
   if(riseStage==2){
    lift=Math.Min(riseTarget,lift+dt*55*dpi);x+=Math.Sin(phase*1.7)*dt*14*dpi;
    if(lift>=riseTarget-.5){
     riseStage=0;Visual.Balloon=false;
     if(IsRemote){remotePerched=true;remoteLedgeX=CenterX;remoteLedgeLift=lift;turnPause=.4;}
     else{Perch(w!);Say(L.Get("win_perch"));Bounce();if(!demo)app.Broadcast("land");}
    }
   }
  }
  else if(!dragging&&!Visual.Sleeping&&Visual.ActionKey.Length==0&&!IsMouseOver&&!Visual.FaceFront){
   if(turnPause>0)turnPause-=dt;
   else{
    if(wallGoal!=0)direction=wallGoal==1?-1:1;
    walking=true;velocity+=(direction*28*dpi-velocity)*Math.Min(1,dt*6);x+=velocity*dt;
    // At a screen edge the character may step onto the wall instead of turning around (roughly half the time, then not again for a while;
    // always when the menu asked for it).
    if(perch==null&&!remotePerched&&(wallGoal!=0||(app.State.EdgeRoam&&phase>=nextWallTry))){
     var wa=app.Screen.Work;
     if(x<=wa.Left&&direction<0){if(wallGoal==1||random.Next(2)==0)StartWall(1);else nextWallTry=phase+20;}
     else if(x>=wa.Right-Width*dpi&&direction>0){if(wallGoal==2||random.Next(2)==0)StartWall(2);else nextWallTry=phase+20;}
    }
   }
   // Every couple of minutes a window nearby invites a balloon ride to its top edge (local character only).
   if(!IsRemote&&perch==null&&surface==0&&app.State.WindowPlay&&phase>=nextRise){nextRise=phase+random.Next(90,200);TryStartRise();}
  }
  else velocity=0;
  if(perch!=null&&!dragging){var b=perch.Bounds;ClampToLedge(b.Left+12*dpi-Width*dpi/2,b.Right-12*dpi-Width*dpi/2,true);}
  else if(IsRemote&&remotePerched&&!falling&&riseStage==0&&surface==0){lift=remoteLedgeLift;ClampToLedge(remoteLedgeX-70*dpi-Width*dpi/2,remoteLedgeX+70*dpi-Width*dpi/2,false);}
  if(ballStage>0){
   ballT+=dt/1.1;double t=Math.Min(1,ballT);
   ball?.Place(ballFromX+(ballToX-ballFromX)*t,app.GroundY-16*dpi-Math.Sin(t*Math.PI)*90*dpi,dpi);
   if(t>=1){ballStage=0;ball?.Hide();if(!ballOut){Say(L.Get("ball_caught"));if(Visual.Species=="dog"){Visual.ActionKey="fetch";actionStart=phase;actionUntil=phase+3;}else Bounce();}}
  }
  // Parachute state is derived after this frame's movement so it opens on the very frame the drop starts.
  Visual.Parachute=falling&&fallAge>.12;Visual.Sway=falling?Math.Sin(fallAge*2.1)*6:riseStage==2?Math.Sin(phase*1.7)*4:0;
  Visual.Shake=phase<refuseUntil?Math.Sin((refuseUntil-phase)*24)*3:0;
  if(leap>0){leap+=dt;if(leap>.65)leap=0;}
  Visual.Jump=leap==0?0:Math.Sin(leap/.65*Math.PI)*39;
  // On the left wall the sprite is turned 90° clockwise, so "up" needs the west-facing frames; on the right wall it is the other way round.
  Visual.Phase=phase;Visual.ActionTime=phase-actionStart;Visual.Walking=walking;Visual.FaceLeft=surface==1?direction>0:direction<0;Visual.Surface=surface;
  // Friends only need to know about sleep changes; everything else they see is an explicit event or their own simulation.
  if(!IsRemote&&!demo&&Visual.Sleeping!=sentSleeping){sentSleeping=Visual.Sleeping;app.RequestSnapshot();}
  Place();
  // Redraw only when something visible changed (sprite frame, bubble, jump, parachute...), not 60 times a second.
  int signature=HashCode.Combine((int)(phase*SpriteSet.WalkFps),(int)(Visual.ActionTime*SpriteSet.ActionFps),Visual.Bubble,Visual.ActionKey,HashCode.Combine(Visual.Walking,Visual.FaceLeft,Visual.Sleeping,Visual.Parachute,(int)(Visual.Jump*4),(int)(Visual.Sway*4),Visual.Species,Visual.SizeFactor),HashCode.Combine(Visual.BubbleStyle,Visual.GoldName,Visual.PetName,Visual.FaceFront,Visual.NameStyle,Visual.ShowName,(int)(Visual.Shake*4),HashCode.Combine(Visual.Balloon,Visual.BubbleTyped,Visual.Surface)));
  if(signature!=lastSignature){lastSignature=signature;Visual.InvalidateVisual();}
 }
 // Moves the overlay only when its pixel position changed. assertTop re-applies the top-most z-order (done every few seconds by App).
 public void Place(bool assertTop=false)
 {
  if(handle==IntPtr.Zero)return;var work=app.Screen.Work;int ground=app.GroundY;dpi=Math.Max(1,Native.GetDpiForWindow(handle)/96.0);
  int width=(int)Math.Round(Width*dpi),height=(int)Math.Round(Height*dpi);
  int px,py;
  if(surface==1||surface==2){
   // Feet against the screen edge; the transparent body part of the window may hang outside the screen.
   x=(surface==1?work.Left:work.Right)-FeetLocalX*dpi;lift=Math.Clamp(lift,0,WallTop);
   px=(int)Math.Round(x);py=(int)Math.Round(ground-lift-WallFeetLocalY*dpi);
  }
  else if(surface==3){
   x=Math.Clamp(x,work.Left+8*dpi-FeetLocalX*dpi,work.Right-8*dpi-FeetLocalX*dpi);lift=WallTop;
   px=(int)Math.Round(x);py=(int)Math.Round(work.Top-CeilingFeetLocalY*dpi);
  }
  else{
   double max=Math.Max(work.Left,work.Right-width);
   if(x<work.Left){x=work.Left;direction=1;velocity=0;turnPause=.3;}
   if(x>max){x=max;direction=-1;velocity=0;turnPause=.3;}
   // On a window (or a remote ledge) the transparent headroom above the sprite may leave the screen; only the sprite itself has to stay visible.
   bool elevated=perch!=null||riseStage==2||remotePerched;
   lift=Math.Clamp(lift,0,Math.Max(0,ground-work.Top-(elevated?110*dpi:height)));
   px=(int)Math.Round(x);py=ground-height-(int)Math.Round(lift);
  }
  if(!assertTop&&px==placedX&&py==placedY&&width==placedW&&height==placedH)return;
  Native.SetWindowPos(handle,assertTop?Native.TopMost:IntPtr.Zero,px,py,width,height,assertTop?Native.SwpNoActivate:Native.SwpNoActivate|Native.SwpNoZOrder);
  placedX=px;placedY=py;placedW=width;placedH=height;
 }
 // Physical pixels: horizontal centre of the pet and the top edge of its overlay window (where bubbles appear).
 public (double X,double Top) HeadPoint()=>(CenterX,surface==3?app.Screen.Work.Top+120*dpi:app.GroundY-Height*dpi-lift);
 // Events carry the position only where it matters: where a parachute drop or balloon ride starts and how high it goes.
 public PetEvent Snapshot(string kind="state",string message="")
 {
  var w=app.Screen.Work;double height=Math.Max(1,app.GroundY-w.Top);
  return new PetEvent{Kind=kind,Name=Visual.PetName,Species=Visual.Species,Action=Visual.ActionKey,Message=message,X=Math.Clamp((x-w.Left)/Span,0,1),Lift=(kind=="balloon"?riseTarget:lift)/height,Left=direction<0,Walking=Visual.Walking,Sleeping=Visual.Sleeping,Front=Visual.FaceFront,Perched=perch!=null||riseStage==2};
 }
 public void Apply(PetEvent e)
 {
  var w=app.Screen.Work;double height=Math.Max(1,app.GroundY-w.Top);Visual.PetName=e.Name;Visual.Species=PetCatalog.Valid(e.Species)?e.Species:"cat";
  if(Visual.Species!=lastSpecies){lastSpecies=Visual.Species;ResetMotion();}
  Visual.Sleeping=e.Sleeping;
  switch(e.Kind){
   case "parachute": surface=0;x=w.Left+Math.Clamp(e.X,0,1)*Span;lift=Math.Max(lift,Math.Clamp(e.Lift,0,1)*height);falling=true;fallAge=0;remotePerched=false;riseStage=0;Visual.Balloon=false;break;
   case "balloon": surface=0;x=w.Left+Math.Clamp(e.X,0,1)*Span;riseX=CenterX;riseTarget=Math.Clamp(e.Lift,0,1)*height;riseStage=1;falling=false;remotePerched=false;break;
   case "land":
    surface=0;falling=false;Visual.Parachute=false;
    if(e.Perched){remotePerched=true;remoteLedgeLift=Math.Clamp(e.Lift,0,1)*height;lift=remoteLedgeLift;x=w.Left+Math.Clamp(e.X,0,1)*Span;remoteLedgeX=CenterX;}
    else{remotePerched=false;lift=0;}
    break;
   case "message": Say(e.Message,e.Auto);break;
   case "state": if(e.Action!=Visual.ActionKey&&e.Action.Length>0){Visual.ActionKey=e.Action;actionStart=phase;actionUntil=phase+8;}break;
   case "greet": case "poke": case "ball": break;
   default: Visual.ActionKey=e.Kind;actionStart=phase;actionUntil=phase+8;break;
  }
 }
 public bool InsideWorkArea(){if(!Native.GetWindowRect(handle,out var r))return false;var w=app.Screen.Work;return r.Left>=w.Left&&r.Right<=w.Right&&Math.Abs(r.Bottom-app.GroundY)<=2;}
 public Native.Rect WindowRect(){Native.GetWindowRect(handle,out var r);return r;}
 public void TestDrop(double fraction){LeaveSurfaces();lift=fraction*(app.GroundY-app.Screen.Work.Top-Height*dpi);falling=true;fallAge=0;}
 public void TestMoveTo(double screenX){x=screenX;Place();}
 public void TestInterruptFall(){falling=false;dragging=false;}
 public bool TestRise()=>TryStartRise();
 public void TestStartWall(int side){LeaveSurfaces();StartWall(side);Place();}
 public void TestRequestWall(){LeaveSurfaces();falling=false;RequestWall();}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace TaskbarTails;
// Room management: create / join by code, my rooms (switch or leave), members (poke / throw a ball).
// Bubbles are sent from the main panel or the quick bubble (Ctrl+Alt+T).
public sealed class RoomWindow:Window
{
 readonly App app;
 public bool AllowClose;
 readonly TextBox value=new(){Margin=new Thickness(0,8,0,10),Padding=new Thickness(10)};
 readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8)};
 readonly TextBlock code=new(){FontSize=19,FontWeight=FontWeights.Bold,Margin=new Thickness(0,4,0,10)};
 readonly ListBox rooms=new(){Height=104,Margin=new Thickness(0,8,0,8)};
 readonly ListBox roster=new(){Height=104,Margin=new Thickness(0,8,0,8)};
 readonly Button create,join;
 IReadOnlyList<RoomMember> members=Array.Empty<RoomMember>();
 List<RoomInfo> roomList=new();
 bool busy;
 public RoomWindow(App owner)
 {
  app=owner;Title=L.Get("room_title");Width=520;Height=760;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterOwner;
  Background=new SolidColorBrush(Color.FromRgb(247,247,242));FontFamily=new FontFamily("Malgun Gothic");Foreground=new SolidColorBrush(Color.FromRgb(39,62,55));
  value.Text=L.Get("room_default_name");status.Text=L.Get("room_status_idle");
  var stack=new StackPanel{Margin=new Thickness(28)};Content=new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  stack.Children.Add(new TextBlock{Text=L.Get("room_head"),FontSize=25,FontWeight=FontWeights.Bold});
  stack.Children.Add(new TextBlock{Text=L.Get("room_desc"),FontSize=12,Margin=new Thickness(0,9,0,16),TextWrapping=TextWrapping.Wrap});
  stack.Children.Add(new TextBlock{Text=L.Get("room_input")});stack.Children.Add(value);
  var row=new StackPanel{Orientation=Orientation.Horizontal};stack.Children.Add(row);
  create=Make(L.Get("room_create"),()=>Connect(true));join=Make(L.Get("room_join"),()=>Connect(false));row.Children.Add(create);row.Children.Add(join);
  stack.Children.Add(status);stack.Children.Add(code);
  var tools=new StackPanel{Orientation=Orientation.Horizontal};stack.Children.Add(tools);
  tools.Children.Add(Make(L.Get("room_copy"),()=>{if(app.Room?.InviteCode.Length>0)Clipboard.SetText(app.Room.InviteCode);return Task.CompletedTask;}));
  tools.Children.Add(Make(L.Get("room_leave"),async()=>{if(app.Room!=null)await app.Room.Leave();app.ForgetRoom();UpdateStatus(L.Get("room_left"));await RefreshRooms();}));
  stack.Children.Add(new TextBlock{Text=L.Get("my_rooms"),FontWeight=FontWeights.Bold,Margin=new Thickness(0,18,0,0),TextWrapping=TextWrapping.Wrap});stack.Children.Add(rooms);
  var roomTools=new StackPanel{Orientation=Orientation.Horizontal};stack.Children.Add(roomTools);
  roomTools.Children.Add(Make(L.Get("room_switch"),SwitchSelected));roomTools.Children.Add(Make(L.Get("room_quit"),LeaveSelected));roomTools.Children.Add(Make(L.Get("room_refresh"),RefreshRooms));
  stack.Children.Add(new TextBlock{Text=L.Get("room_members"),FontWeight=FontWeights.Bold,Margin=new Thickness(0,18,0,0)});stack.Children.Add(roster);
  var memberTools=new StackPanel{Orientation=Orientation.Horizontal};stack.Children.Add(memberTools);
  memberTools.Children.Add(Make(L.Get("room_poke"),()=>{var m=Selected();if(m!=null)app.PokeMember(m.UserId,m.Name);return Task.CompletedTask;}));
  memberTools.Children.Add(Make(L.Get("room_throw"),()=>{var m=Selected();if(m!=null)app.ThrowBallToMember(m.UserId);return Task.CompletedTask;}));
  stack.Children.Add(new TextBlock{Text=L.Get("room_note"),FontSize=11,Foreground=Brushes.Gray,Margin=new Thickness(0,16,0,0),TextWrapping=TextWrapping.Wrap});
  Closing+=(_,e)=>{if(!app.Exiting&&!AllowClose){e.Cancel=true;Hide();}};
  if(app.Room!=null&&app.Room.RoomId.Length>0){UpdateStatus(L.F("room_connected",app.Room.RoomName));UpdateRoster(app.Room.Members);}
  if(app.Room!=null)_=RefreshRooms();
 }
 Button Make(string label,Func<Task> action){var b=new Button{Content=label,Padding=new Thickness(12,9,12,9),Margin=new Thickness(0,0,8,8)};b.Click+=async(_,_)=>{try{await action();}catch(Exception e)when(e is HttpRequestException or InvalidOperationException or TaskCanceledException or TimeoutException){UpdateStatus(e.Message);}};return b;}
 RoomMember? Selected()
 {
  var m=members.ElementAtOrDefault(roster.SelectedIndex);
  if(m==null||m.UserId==app.Room?.UserId){UpdateStatus(L.Get("select_member"));return null;}
  return m;
 }
 async Task Connect(bool make)
 {
  if(busy)return;string input=value.Text.Trim();if(input.Length==0){UpdateStatus(L.Get("room_input_required"));return;}
  busy=true;create.IsEnabled=join.IsEnabled=false;UpdateStatus(L.Get("room_connecting"));
  try{await app.JoinRoom(input,make);UpdateStatus(L.F("room_connected",app.Room!.RoomName));await RefreshRooms();}
  finally{busy=false;create.IsEnabled=join.IsEnabled=true;}
 }
 async Task SwitchSelected()
 {
  var r=roomList.ElementAtOrDefault(rooms.SelectedIndex);
  if(r==null){UpdateStatus(L.Get("select_room"));return;}
  if(r.Id==app.Room?.RoomId)return;
  UpdateStatus(L.Get("room_connecting"));await app.JoinRoom(r.Code,false);UpdateStatus(L.F("room_connected",app.Room!.RoomName));await RefreshRooms();
 }
 async Task LeaveSelected()
 {
  var r=roomList.ElementAtOrDefault(rooms.SelectedIndex);
  if(r==null||app.Room==null){UpdateStatus(L.Get("select_room"));return;}
  bool current=r.Id==app.Room.RoomId;await app.Room.LeaveRoom(r.Id);if(current){app.ForgetRoom();UpdateStatus(L.Get("room_left"));}
  await RefreshRooms();
 }
 public async Task RefreshRooms()
 {
  if(app.Room==null)return;
  roomList=await app.Room.ListRooms();
  rooms.ItemsSource=roomList.Select(r=>r.Name+" · "+r.Code+(r.Id==app.Room.RoomId?L.Get("current_room"):"")).ToArray();
  rooms.SelectedIndex=roomList.FindIndex(r=>r.Id==app.Room.RoomId);
 }
 public void UpdateStatus(string text){status.Text=text;code.Text=app.Room?.InviteCode.Length>0?L.F("room_code",app.Room.InviteCode):"";}
 public void UpdateRoster(IReadOnlyList<RoomMember> list){members=list;roster.ItemsSource=list.Select(m=>m.Name+" · "+PetCatalog.Label(PetCatalog.Get(m.Species))+(m.UserId==app.Room?.UserId?L.Get("me"):"")).ToArray();}
}

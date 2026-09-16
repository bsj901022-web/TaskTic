using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace TaskbarTails;
public sealed class RoomWindow:Window
{
 readonly App app;
 readonly TextBox value=new(){Text="우리들의 작은 방",Margin=new Thickness(0,8,0,10),Padding=new Thickness(10)};
 readonly TextBox bubble=new(){MaxLength=80,Margin=new Thickness(0,8,0,10),Padding=new Thickness(10)};
 readonly TextBlock status=new(){Text="방을 만들거나 초대코드를 입력하세요.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)};
 readonly TextBlock code=new(){FontSize=19,FontWeight=FontWeights.Bold,Margin=new Thickness(0,10,0,10)};
 readonly ListBox roster=new(){Height=130,Margin=new Thickness(0,8,0,16)};
 readonly Button create,join;
 bool busy;
 public RoomWindow(App owner)
 {
  app=owner;Title="Taskbar Tails · 함께하는 방";Width=490;Height=680;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterOwner;
  Background=new SolidColorBrush(Color.FromRgb(247,247,242));FontFamily=new FontFamily("Malgun Gothic");Foreground=new SolidColorBrush(Color.FromRgb(39,62,55));
  var stack=new StackPanel{Margin=new Thickness(28)};Content=new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  stack.Children.Add(new TextBlock{Text="함께하는 작은 방",FontSize=25,FontWeight=FontWeights.Bold});
  stack.Children.Add(new TextBlock{Text="같은 방 친구의 움직임과 말풍선이 내 화면에도 보여요.",FontSize=12,Margin=new Thickness(0,9,0,16),TextWrapping=TextWrapping.Wrap});
  stack.Children.Add(new TextBlock{Text="새 방 이름 또는 받은 초대코드"});stack.Children.Add(value);
  var row=new StackPanel{Orientation=Orientation.Horizontal};stack.Children.Add(row);
  create=Make("방 만들기",async()=>await Connect(true));join=Make("코드로 참여",async()=>await Connect(false));row.Children.Add(create);row.Children.Add(join);
  stack.Children.Add(status);stack.Children.Add(code);
  var tools=new StackPanel{Orientation=Orientation.Horizontal};stack.Children.Add(tools);
  tools.Children.Add(Make("초대코드 복사",()=>{if(app.Room?.InviteCode.Length>0)Clipboard.SetText(app.Room.InviteCode);return System.Threading.Tasks.Task.CompletedTask;}));
  tools.Children.Add(Make("방 나가기",async()=>{if(app.Room!=null)await app.Room.Leave();UpdateStatus("방에서 나왔어요.");}));
  stack.Children.Add(new TextBlock{Text="함께 있는 친구",FontWeight=FontWeights.Bold,Margin=new Thickness(0,17,0,0)});stack.Children.Add(roster);
  stack.Children.Add(new TextBlock{Text="내 캐릭터가 할 말 · 최대 80자"});stack.Children.Add(bubble);
  bubble.KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Enter){app.SendBubble(bubble.Text);bubble.Clear();}};
  stack.Children.Add(Make("말풍선 보내기",()=>{app.SendBubble(bubble.Text);bubble.Clear();return System.Threading.Tasks.Task.CompletedTask;}));
  stack.Children.Add(new TextBlock{Text="말풍선은 잠시 표시되고 대화 내역으로 저장되지 않아요.\n관리 화면의 데모 친구는 온라인 참가자가 아닙니다. 이 창을 닫아도 방 연결은 유지되며 초대코드는 관리 화면에도 표시됩니다.",FontSize=11,Foreground=Brushes.Gray,Margin=new Thickness(0,16,0,0),TextWrapping=TextWrapping.Wrap});
  Closing+=(_,e)=>{if(!app.Exiting){e.Cancel=true;Hide();}};
  if(app.Room!=null&&app.Room.RoomId.Length>0){UpdateStatus("연결됨 · "+app.Room.RoomName);UpdateRoster(app.Room.Members);}
 }
 Button Make(string label,Func<System.Threading.Tasks.Task> action){var b=new Button{Content=label,Padding=new Thickness(12,9,12,9),Margin=new Thickness(0,0,8,0)};b.Click+=async(_,_)=>{try{await action();}catch(Exception e){UpdateStatus(e.Message);}};return b;}
 async System.Threading.Tasks.Task Connect(bool make)
 {
  if(busy)return;string input=value.Text.Trim();if(input.Length==0){UpdateStatus("방 이름 또는 초대코드를 입력해 주세요.");return;}
  busy=true;create.IsEnabled=join.IsEnabled=false;UpdateStatus("연결하고 있어요…");
  try{app.EnsureRoom();await app.Room!.Open(input,make,app.State);UpdateStatus("연결됨 · "+app.Room.RoomName);app.Broadcast("state");}
  finally{busy=false;create.IsEnabled=join.IsEnabled=true;}
 }
 public void UpdateStatus(string text){status.Text=text;code.Text=app.Room?.InviteCode.Length>0?"초대코드  "+app.Room.InviteCode:"";}
 public void UpdateRoster(System.Collections.Generic.IReadOnlyList<RoomMember> members){roster.ItemsSource=members.Select(m=>m.Name+" · "+PetCatalog.Get(m.Species).Label+(m.UserId==app.Room?.UserId?" (나)":"")).ToArray();}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace TaskbarTails;
public sealed record RoomMember(string UserId,string Name,string Species);
public sealed class RoomClient:IDisposable
{
 readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(20)};
 readonly SemaphoreSlim sendLock=new(1,1),publishLock=new(1,1);
 static readonly JsonSerializerOptions json=new(){PropertyNameCaseInsensitive=true};
 readonly string url,key;
 string token="",refresh="";
 DateTime expires=DateTime.MinValue;
 bool sessionLoaded;
 CancellationTokenSource? lifetime;
 ClientWebSocket? socket;
 TaskCompletionSource<bool>? joined;
 long sequence;
 public string UserId{get;private set;}="";
 public string RoomId{get;private set;}="";
 public string InviteCode{get;private set;}="";
 public string RoomName{get;private set;}="";
 // Anonymous session (refresh token + user id). Stored per Windows user under %LOCALAPPDATA%, never inside the app folder,
 // so copying or zipping the app for a friend cannot hand them the same identity. Also bound to this PC + account.
 // Set to null to keep the session in memory only (used by the two-client network check).
 public string? SessionFile{get;set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TaskbarTails","session.json");
 static string MachineKey=>Environment.MachineName+"|"+(System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value??"");
 public IReadOnlyList<RoomMember> Members{get;private set;}=Array.Empty<RoomMember>();
 public bool Connected=>socket?.State==WebSocketState.Open&&joined?.Task.IsCompletedSuccessfully==true;
 public event Action<PetEvent>? Received;
 public event Action<IReadOnlyList<RoomMember>>? RosterChanged;
 public event Action<string>? Status;
 public Func<PetEvent>? CurrentPet;
 public RoomClient()
 {
  var config=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"supabase.json"))).RootElement;
  url=config.GetProperty("url").GetString()!.TrimEnd('/');key=config.GetProperty("anonKey").GetString()!;
  if(!Uri.TryCreate(url,UriKind.Absolute,out var u)||u.Scheme!="https")throw new InvalidOperationException("Supabase HTTPS 주소를 확인해 주세요.");
  http.DefaultRequestHeaders.Add("apikey",key);
  // v0.5.0 kept the session next to pet.json; remove it so a shared app folder never carries an identity.
  try{var legacy=Path.Combine(Path.GetDirectoryName(StateStore.PathName)!,"session.json");if(File.Exists(legacy))File.Delete(legacy);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}
 }
 async Task<JsonElement> Request(string endpoint,object? body=null,bool auth=true)
 {
  using var req=new HttpRequestMessage(body==null?HttpMethod.Get:HttpMethod.Post,url+endpoint);
  if(auth)req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
  if(body!=null)req.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");
  using var res=await http.SendAsync(req);string text=await res.Content.ReadAsStringAsync();
  if(!res.IsSuccessStatusCode){
   if(text.Contains("anonymous_provider_disabled"))throw new InvalidOperationException("Supabase에서 Anonymous Sign-Ins를 켜 주세요.");
   if(text.Contains("PGRST202")||text.Contains("PGRST205"))throw new InvalidOperationException("먼저 Supabase setup.sql을 실행해 주세요.");
   if(text.Contains("tt_members_species_check"))throw new InvalidOperationException("서버가 이 캐릭터 종류를 아직 몰라요. supabase/upgrade-v05.sql을 실행해 주세요.");
   if((int)res.StatusCode is 400 or 401 or 403 && endpoint.StartsWith("/auth/v1/token"))throw new SessionExpiredException();
   string msg=$"서버 응답 {(int)res.StatusCode}";try{var j=JsonDocument.Parse(text).RootElement;msg=j.TryGetProperty("message",out var m)?m.GetString()??msg:j.TryGetProperty("msg",out m)?m.GetString()??msg:msg;}catch(JsonException){}
   throw new InvalidOperationException(msg.Length>240?msg[..240]:msg);
  }
  return string.IsNullOrWhiteSpace(text)?JsonDocument.Parse("null").RootElement.Clone():JsonDocument.Parse(text).RootElement.Clone();
 }
 sealed class SessionExpiredException:Exception{}
 void LoadSession()
 {
  if(sessionLoaded)return;sessionLoaded=true;
  if(SessionFile==null||!File.Exists(SessionFile))return;
  try{var s=JsonDocument.Parse(File.ReadAllText(SessionFile)).RootElement;
   if(!s.TryGetProperty("machine",out var m)||m.GetString()!=MachineKey){refresh="";UserId="";return;}
   refresh=s.GetProperty("refresh_token").GetString()??"";UserId=s.GetProperty("user_id").GetString()??"";}
  catch(Exception e)when(e is IOException or JsonException or KeyNotFoundException or UnauthorizedAccessException){refresh="";UserId="";}
 }
 void SaveSession()
 {
  if(SessionFile==null)return;
  try{Directory.CreateDirectory(Path.GetDirectoryName(SessionFile)!);File.WriteAllText(SessionFile,JsonSerializer.Serialize(new{refresh_token=refresh,user_id=UserId,machine=MachineKey,saved_at=DateTime.UtcNow}));}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException){Status?.Invoke("세션 저장 실패 · 다음 실행 시 새 사용자로 시작할 수 있어요.");}
 }
 async Task Authenticate()
 {
  if(token.Length>0&&DateTime.UtcNow<expires.AddMinutes(-2))return;
  LoadSession();string previous=UserId;
  JsonElement data;
  if(refresh.Length>0){
   try{data=await Request("/auth/v1/token?grant_type=refresh_token",new{refresh_token=refresh},false);}
   catch(SessionExpiredException){refresh="";token="";data=await Request("/auth/v1/signup",new{data=new{app="Taskbar Tails"}},false);}
  }
  else data=await Request("/auth/v1/signup",new{data=new{app="Taskbar Tails"}},false);
  token=data.GetProperty("access_token").GetString()!;refresh=data.GetProperty("refresh_token").GetString()!;
  expires=DateTime.UtcNow.AddSeconds(data.GetProperty("expires_in").GetInt32());UserId=data.GetProperty("user").GetProperty("id").GetString()!;
  SaveSession();
  if(previous.Length>0&&previous!=UserId&&RoomId.Length>0){
   var pet=CurrentPet?.Invoke();await Request("/rest/v1/rpc/tt_join_room",new{p_code=InviteCode,p_pet_name=pet?.Name??"친구",p_species=pet?.Species??"cat"});
   Status?.Invoke("새 세션으로 방에 다시 참여했어요.");
  }
 }
 public async Task Open(string value,bool create,PetState state)
 {
  if(RoomId.Length>0)await Leave();await Authenticate();
  object args=create?new{p_name=value,p_pet_name=state.Name,p_species=state.Species}:new{p_code=value,p_pet_name=state.Name,p_species=state.Species};
  var room=await Request("/rest/v1/rpc/"+(create?"tt_create_room":"tt_join_room"),args);
  RoomId=room.GetProperty("room_id").GetString()!;InviteCode=room.GetProperty("invite_code").GetString()!;RoomName=room.GetProperty("name").GetString()!;
  joined=new(TaskCreationOptions.RunContinuationsAsynchronously);lifetime=new();_ = Run(lifetime.Token);
  await joined.Task.WaitAsync(TimeSpan.FromSeconds(20));
 }
 async Task WsSend(object value,CancellationToken ct)
 {
  var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));await sendLock.WaitAsync(ct);
  try{if(socket?.State==WebSocketState.Open)await socket.SendAsync(bytes,WebSocketMessageType.Text,true,ct);}finally{sendLock.Release();}
 }
 async Task Run(CancellationToken ct)
 {
  int failures=0;
  while(!ct.IsCancellationRequested){
   try{
    await Authenticate();socket=new ClientWebSocket();
    string ws=url.Replace("https://","wss://")+"/realtime/v1/websocket?apikey="+Uri.EscapeDataString(key)+"&vsn=1.0.0";
    await socket.ConnectAsync(new Uri(ws),ct);
    string topic="realtime:tt:"+RoomId;
    await WsSend(new{topic,@event="phx_join",payload=new{config=new{broadcast=new{ack=true,self=false},presence=new{key=UserId},@private=true},access_token=token},@ref="join"},ct);
    using var connection=CancellationTokenSource.CreateLinkedTokenSource(ct);
    var heartbeat=Heartbeat(connection.Token);
    try{
     var buffer=new byte[16384];
     while(socket.State==WebSocketState.Open&&!ct.IsCancellationRequested){
      using var message=new MemoryStream();WebSocketReceiveResult part;
      do{part=await socket.ReceiveAsync(buffer,ct);if(part.MessageType==WebSocketMessageType.Close)throw new WebSocketException("서버 연결이 닫혔어요.");message.Write(buffer,0,part.Count);if(message.Length>65536)throw new InvalidOperationException("이벤트가 너무 큽니다.");}while(!part.EndOfMessage);
      using var doc=JsonDocument.Parse(message.ToArray());var root=doc.RootElement;
      var kind=root.GetProperty("event").GetString();var payload=root.GetProperty("payload");
      if(kind=="phx_reply"&&root.TryGetProperty("ref",out var reference)&&reference.GetString()=="join"){
       if(payload.GetProperty("status").GetString()!="ok"){string reason=payload.TryGetProperty("response",out var resp)?resp.ToString():"";if(reason.Length>200)reason=reason[..200];throw new InvalidOperationException("방 구독 권한을 확인해 주세요. setup.sql의 Realtime 정책이 필요합니다. ("+reason+")");}
       failures=0;joined?.TrySetResult(true);Status?.Invoke("연결됨 · "+RoomName);await RefreshRoster();
      }
      else if(kind=="broadcast"&&payload.TryGetProperty("event",out var eventName)&&eventName.GetString()=="pet"){
       var ev=payload.GetProperty("payload").Deserialize<PetEvent>(json);if(ev!=null&&ev.UserId!=UserId&&ev.UserId.Length>0)Received?.Invoke(ev);
      }
     }
    }finally{connection.Cancel();try{await heartbeat;}catch(OperationCanceledException){}}
   }catch(OperationCanceledException){break;}catch(Exception e){failures++;Status?.Invoke("재연결 중 · "+e.Message);if(joined?.Task.IsCompleted==false&&(failures>=2||e is HttpRequestException))joined.TrySetException(e);}
   finally{socket?.Dispose();socket=null;}
   // Exponential back-off between reconnect attempts (4s, 8s, 16s, ... up to 60s).
   if(!ct.IsCancellationRequested)try{await Task.Delay(TimeSpan.FromSeconds(Math.Min(60,4*Math.Pow(2,Math.Min(4,failures)))),ct);}catch(OperationCanceledException){break;}
  }
 }
 async Task Heartbeat(CancellationToken ct)
 {
  while(!ct.IsCancellationRequested){await Task.Delay(15000,ct);await WsSend(new{topic="phoenix",@event="heartbeat",payload=new{},@ref=Interlocked.Increment(ref sequence).ToString()},ct);try{await RefreshRoster();}catch(Exception e)when(e is HttpRequestException or InvalidOperationException or TaskCanceledException){Status?.Invoke("참여자 갱신 대기 · "+e.Message);}}
 }
 public async Task RefreshRoster()
 {
  if(RoomId.Length==0)return;var pet=CurrentPet?.Invoke();
  if(pet!=null)await Request("/rest/v1/rpc/tt_touch_room",new{p_room=RoomId,p_pet_name=pet.Name,p_species=pet.Species});
  var data=await Request("/rest/v1/tt_members?room_id=eq."+RoomId+"&select=user_id,pet_name,species,last_seen");
  var members=new List<RoomMember>();foreach(var row in data.EnumerateArray()){
   if(row.GetProperty("last_seen").GetDateTime().ToUniversalTime()<DateTime.UtcNow.AddSeconds(-90))continue;
   members.Add(new(row.GetProperty("user_id").GetString()!,row.GetProperty("pet_name").GetString()!,row.GetProperty("species").GetString()!));
  }Members=members;RosterChanged?.Invoke(members);
 }
 public async Task Publish(PetEvent value)
 {
  if(!Connected)return;await publishLock.WaitAsync();try{await Authenticate();await Request("/rest/v1/rpc/tt_send_event",new{p_room=RoomId,p_event=value});}finally{publishLock.Release();}
 }
 public async Task Leave()
 {
  lifetime?.Cancel();socket?.Abort();
  if(RoomId.Length>0)try{await Request("/rest/v1/rpc/tt_leave_room",new{p_room=RoomId});}catch(Exception e){Status?.Invoke(e.Message);}
  RoomId="";InviteCode="";RoomName="";Members=Array.Empty<RoomMember>();RosterChanged?.Invoke(Members);Status?.Invoke("방에서 나왔어요.");
 }
 public void Dispose(){lifetime?.Cancel();socket?.Abort();socket?.Dispose();http.Dispose();}
}

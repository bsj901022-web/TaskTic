using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace TaskbarTails;
public sealed record RoomMember(string UserId,string Name,string Species,int Level=1);
public sealed record RoomInfo(string Id,string Name,string Code);
// One anonymous Supabase user, one live room at a time. Membership in other rooms is kept on the server,
// so switching rooms only moves the realtime channel; LeaveRoom removes a membership for good.
// Traffic (v0.6.8): events go out as Realtime broadcasts over the websocket (no REST call, no database row) and the
// roster comes from Realtime presence. The database only sees room create/join/leave, one "touch" a minute and a
// member list read when joining or when an event arrives from an unknown user (older clients).
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
 string topic="";
 // Roster: live presence (key = user id) merged with the member table read for older clients that do not track presence.
 readonly Dictionary<string,RoomMember> presence=new();
 List<RoomMember> tableMembers=new();
 string tracked="";
 DateTime lastTouch=DateTime.MinValue;
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
  if(!Uri.TryCreate(url,UriKind.Absolute,out var u)||u.Scheme!="https")throw new InvalidOperationException(L.Get("rc_https"));
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
   if(text.Contains("anonymous_provider_disabled"))throw new InvalidOperationException(L.Get("rc_anon"));
   if(text.Contains("PGRST202")||text.Contains("PGRST205"))throw new InvalidOperationException(L.Get("rc_setup"));
   if(text.Contains("tt_members_species_check"))throw new InvalidOperationException(L.Get("rc_species"));
   if((int)res.StatusCode is 400 or 401 or 403 && endpoint.StartsWith("/auth/v1/token"))throw new SessionExpiredException();
   string msg=L.F("rc_server",(int)res.StatusCode);try{var j=JsonDocument.Parse(text).RootElement;msg=j.TryGetProperty("message",out var m)?m.GetString()??msg:j.TryGetProperty("msg",out m)?m.GetString()??msg:msg;}catch(JsonException){}
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
  catch(Exception e)when(e is IOException or UnauthorizedAccessException){Status?.Invoke(L.Get("rc_session_save"));}
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
   Status?.Invoke(L.Get("rc_rejoined"));
  }
 }
 // Creates a room (create=true, value=name) or joins/switches by invite code. Membership in the previous room is kept.
 public async Task Open(string value,bool create,PetState state)
 {
  if(RoomId.Length>0)Disconnect();await Authenticate();
  object args=create?new{p_name=value,p_pet_name=state.Name,p_species=state.Species}:new{p_code=value,p_pet_name=state.Name,p_species=state.Species};
  var room=await Request("/rest/v1/rpc/"+(create?"tt_create_room":"tt_join_room"),args);
  RoomId=room.GetProperty("room_id").GetString()!;InviteCode=room.GetProperty("invite_code").GetString()!;RoomName=room.GetProperty("name").GetString()!;
  topic="realtime:tt:"+RoomId;presence.Clear();tableMembers=new();tracked="";lastTouch=DateTime.UtcNow;
  joined=new(TaskCreationOptions.RunContinuationsAsynchronously);lifetime=new();_ = Run(lifetime.Token);
  await joined.Task.WaitAsync(TimeSpan.FromSeconds(20));
 }
 // Rooms this user belongs to (RLS returns only those). Used for the room list and switching.
 public async Task<List<RoomInfo>> ListRooms()
 {
  await Authenticate();
  var data=await Request("/rest/v1/tt_rooms?select=id,name,invite_code&order=created_at.asc");
  var list=new List<RoomInfo>();
  foreach(var row in data.EnumerateArray())list.Add(new(row.GetProperty("id").GetString()!,row.GetProperty("name").GetString()!,row.GetProperty("invite_code").GetString()!));
  return list;
 }
 async Task WsSend(object value,CancellationToken ct)
 {
  var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));await sendLock.WaitAsync(ct);
  try{if(socket?.State==WebSocketState.Open)await socket.SendAsync(bytes,WebSocketMessageType.Text,true,ct);}finally{sendLock.Release();}
 }
 string NextRef()=>Interlocked.Increment(ref sequence).ToString();
 async Task Run(CancellationToken ct)
 {
  int failures=0;
  while(!ct.IsCancellationRequested){
   try{
    await Authenticate();socket=new ClientWebSocket();
    string ws=url.Replace("https://","wss://")+"/realtime/v1/websocket?apikey="+Uri.EscapeDataString(key)+"&vsn=1.0.0";
    await socket.ConnectAsync(new Uri(ws),ct);
    await WsSend(new{topic,@event="phx_join",payload=new{config=new{broadcast=new{ack=false,self=false},presence=new{key=UserId},@private=true},access_token=token},@ref="join"},ct);
    using var connection=CancellationTokenSource.CreateLinkedTokenSource(ct);
    var heartbeat=Heartbeat(connection.Token);
    try{
     var buffer=new byte[16384];
     while(socket.State==WebSocketState.Open&&!ct.IsCancellationRequested){
      using var message=new MemoryStream();WebSocketReceiveResult part;
      do{part=await socket.ReceiveAsync(buffer,ct);if(part.MessageType==WebSocketMessageType.Close)throw new WebSocketException(L.Get("rc_closed"));message.Write(buffer,0,part.Count);if(message.Length>65536)throw new InvalidOperationException(L.Get("rc_big"));}while(!part.EndOfMessage);
      using var doc=JsonDocument.Parse(message.ToArray());var root=doc.RootElement;
      var kind=root.GetProperty("event").GetString();var payload=root.GetProperty("payload");
      if(kind=="phx_reply"&&root.TryGetProperty("ref",out var reference)&&reference.GetString()=="join"){
       if(payload.GetProperty("status").GetString()!="ok"){string reason=payload.TryGetProperty("response",out var resp)?resp.ToString():"";if(reason.Length>200)reason=reason[..200];throw new InvalidOperationException(L.Get("rc_policy")+" ("+reason+")");}
       failures=0;joined?.TrySetResult(true);Status?.Invoke(L.F("room_connected",RoomName));
       tracked="";await TrackPresence(ct);
       try{await RefreshRoster();}catch(Exception e)when(e is HttpRequestException or InvalidOperationException or TaskCanceledException){Status?.Invoke(L.F("rc_roster_wait",e.Message));}
      }
      else if(kind=="broadcast"&&payload.TryGetProperty("event",out var eventName)&&eventName.GetString()=="pet"){
       var ev=payload.GetProperty("payload").Deserialize<PetEvent>(json);if(ev!=null&&ev.UserId!=UserId&&ev.UserId.Length>0)Received?.Invoke(ev);
      }
      else if(kind=="presence_state"){presence.Clear();ApplyPresence(payload,true);UpdateMembers();}
      else if(kind=="presence_diff"){
       if(payload.TryGetProperty("leaves",out var leaves))foreach(var p in leaves.EnumerateObject())presence.Remove(p.Name);
       if(payload.TryGetProperty("joins",out var joins))ApplyPresence(joins,true);
       UpdateMembers();
      }
     }
    }finally{connection.Cancel();try{await heartbeat;}catch(OperationCanceledException){}}
   }catch(OperationCanceledException){break;}catch(Exception e){failures++;Status?.Invoke(L.F("rc_reconnect",e.Message));if(joined?.Task.IsCompleted==false&&(failures>=2||e is HttpRequestException))joined.TrySetException(e);}
   finally{socket?.Dispose();socket=null;}
   // Exponential back-off between reconnect attempts (4s, 8s, 16s, ... up to 60s).
   if(!ct.IsCancellationRequested)try{await Task.Delay(TimeSpan.FromSeconds(Math.Min(60,4*Math.Pow(2,Math.Min(4,failures)))),ct);}catch(OperationCanceledException){break;}
  }
 }
 // Phoenix presence payload: { "<key>": { "metas": [ { user_id, pet_name, species, phx_ref } ] } }
 void ApplyPresence(JsonElement state,bool add)
 {
  foreach(var entry in state.EnumerateObject()){
   if(!entry.Value.TryGetProperty("metas",out var metas)||metas.GetArrayLength()==0)continue;
   var meta=metas[metas.GetArrayLength()-1];
   string id=meta.TryGetProperty("user_id",out var u)?u.GetString()??entry.Name:entry.Name;
   string name=meta.TryGetProperty("pet_name",out var n)?n.GetString()??"친구":"친구";
   string species=meta.TryGetProperty("species",out var s)?s.GetString()??"cat":"cat";
   int level=meta.TryGetProperty("level",out var l)&&l.ValueKind==JsonValueKind.Number?l.GetInt32():1;
   if(add)presence[entry.Name]=new RoomMember(id,name,species,level);
  }
 }
 void UpdateMembers()
 {
  var list=new List<RoomMember>(presence.Values);
  foreach(var m in tableMembers)if(!list.Any(p=>p.UserId==m.UserId))list.Add(m);
  Members=list;RosterChanged?.Invoke(list);
 }
 // Tells the room who we are (name, kind). Re-sent only when that changes.
 async Task TrackPresence(CancellationToken ct)
 {
  var pet=CurrentPet?.Invoke();if(pet==null||!Connected)return;
  string signature=pet.Name+"|"+pet.Species+"|"+pet.Level;if(signature==tracked)return;tracked=signature;
  await WsSend(new{topic,@event="presence",payload=new{type="presence",@event="track",payload=new{user_id=UserId,pet_name=pet.Name,species=pet.Species,level=pet.Level}},@ref=NextRef()},ct);
 }
 async Task Heartbeat(CancellationToken ct)
 {
  while(!ct.IsCancellationRequested){
   await Task.Delay(15000,ct);
   await WsSend(new{topic="phoenix",@event="heartbeat",payload=new{},@ref=NextRef()},ct);
   // last_seen in the member table once a minute: keeps older clients' rosters showing us and costs one small request.
   if(DateTime.UtcNow-lastTouch>TimeSpan.FromSeconds(45)){lastTouch=DateTime.UtcNow;try{await Touch();}catch(Exception e)when(e is HttpRequestException or InvalidOperationException or TaskCanceledException){Status?.Invoke(L.F("rc_roster_wait",e.Message));}}
  }
 }
 async Task Touch()
 {
  if(RoomId.Length==0)return;var pet=CurrentPet?.Invoke();
  if(pet!=null){await Authenticate();await Request("/rest/v1/rpc/tt_touch_room",new{p_room=RoomId,p_pet_name=pet.Name,p_species=pet.Species});}
 }
 // Reads the member table (members seen in the last 90 s) and merges it with presence. Used on join and when an event arrives
 // from a user presence does not know (an older client that does not track presence).
 public async Task RefreshRoster()
 {
  if(RoomId.Length==0)return;await Authenticate();
  var data=await Request("/rest/v1/tt_members?room_id=eq."+RoomId+"&select=user_id,pet_name,species,last_seen");
  var members=new List<RoomMember>();foreach(var row in data.EnumerateArray()){
   if(row.GetProperty("last_seen").GetDateTime().ToUniversalTime()<DateTime.UtcNow.AddSeconds(-90))continue;
   members.Add(new(row.GetProperty("user_id").GetString()!,row.GetProperty("pet_name").GetString()!,row.GetProperty("species").GetString()!));
  }tableMembers=members;UpdateMembers();
 }
 // One event to the room over the live channel: no REST call and no database row. The sender id is set here (self=false, so we never receive it).
 public async Task Publish(PetEvent value)
 {
  if(!Connected||lifetime==null)return;await publishLock.WaitAsync();
  try{
   value.UserId=UserId;
   await WsSend(new{topic,@event="broadcast",payload=new{type="broadcast",@event="pet",payload=value},@ref=NextRef()},lifetime.Token);
   await TrackPresence(lifetime.Token);
  }finally{publishLock.Release();}
 }
 // Closes the live channel but keeps the membership (used when switching rooms).
 public void Disconnect()
 {
  lifetime?.Cancel();socket?.Abort();
  RoomId="";InviteCode="";RoomName="";topic="";presence.Clear();tableMembers=new();Members=Array.Empty<RoomMember>();RosterChanged?.Invoke(Members);
 }
 // Leaves the current room for good.
 public async Task Leave()
 {
  string room=RoomId;Disconnect();
  if(room.Length>0)try{await Request("/rest/v1/rpc/tt_leave_room",new{p_room=room});}catch(Exception e)when(e is HttpRequestException or InvalidOperationException or TaskCanceledException){Status?.Invoke(e.Message);}
  Status?.Invoke(L.Get("room_left"));
 }
 // Removes membership of a room that is not the live one.
 public async Task LeaveRoom(string roomId)
 {
  if(roomId==RoomId){await Leave();return;}
  await Authenticate();await Request("/rest/v1/rpc/tt_leave_room",new{p_room=roomId});
 }
 public void Dispose(){lifetime?.Cancel();socket?.Abort();socket?.Dispose();http.Dispose();}
}

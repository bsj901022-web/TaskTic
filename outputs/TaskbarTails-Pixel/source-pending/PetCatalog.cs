using System;
using System.Collections.Generic;
using System.Linq;
namespace TaskbarTails;
public sealed record PetKind(string Id,string Label,string Group,string FirstAction,string FirstLabel,string SecondAction,string SecondLabel);
public static class PetCatalog
{
 // 22 kinds. Sprites for every kind come from PixelLab (assets/<Id>/...).
 public static readonly PetKind[] All = {
  new("cat","고양이","동물","loaf","식빵 굽기","groom","세수하기"), new("rabbit","토끼","동물","hop","깡총깡총","ears","귀 쫑긋"),
  new("dog","강아지","동물","fetch","공 물어오기","wag","꼬리 흔들기"), new("hamster","햄스터","동물","nibble","오물오물","curl","동글 휴식"),
  new("fox","여우","동물","pounce","폴짝 사냥놀이","tail","꼬리 이불"), new("penguin","펭귄","동물","flap","날개 파닥","bow","꾸벅 인사"),
  new("duck","오리","동물","waddle","뒤뚱 댄스","preen","깃털 정리"), new("bear","아기곰","동물","honey","꿀 먹기","stretch","기지개"),
  new("frog","개구리","동물","leap","폴짝","tongue","혀 내밀기"), new("panda","판다","동물","bamboo","대나무 먹기","roll","뒹굴뒹굴"),
  new("slime","슬라임","비동물","squish","말랑말랑","jiggle","통통 젤리"), new("robot","로봇","비동물","wave","삐빅 인사","dance","로봇 댄스"),
  new("ghost","유령","비동물","float","둥실둥실","peek","까꿍"), new("mushroom","버섯","비동물","bounce","톡톡 점프","spore","반짝 포자"),
  new("dragon","아기 드래곤","비동물","puff","불 뿜기","wings","날개 파닥"), new("cactus","선인장","비동물","bloom","꽃 피우기","shimmy","살랑 댄스"),
  new("cloudpup","몽실 강아지","마스코트","smile","헤헤 웃기","flop","엎드려 쉬기"), new("puffball","하양 뭉치","마스코트","teary","글썽글썽","yay","만세"),
  new("bluecat","도도 고양이","마스코트","sing","노래하기","grin","히히 웃기"), new("yellowbunny","나나 토끼","마스코트","yaha","야하 점프","spin","빙글 돌기"),
  new("boy","남자 캐릭터","사람","wave","손 흔들기","cheer","신나는 응원"), new("girl","여자 캐릭터","사람","wave","손 흔들기","cheer","신나는 응원") };
 // Motions that end in a resting pose: the last frame is held instead of looping. Also used as the sleeping pose.
 public static readonly HashSet<string> HoldPose = new(){"loaf","curl","tail","flop"};
 public static PetKind Get(string id) => All.FirstOrDefault(x=>x.Id==id) ?? All[0];
 public static bool Valid(string id) => All.Any(x=>x.Id==id);
 public static string RestPose(string id){var k=Get(id);return HoldPose.Contains(k.FirstAction)?k.FirstAction:HoldPose.Contains(k.SecondAction)?k.SecondAction:"";}
}
public sealed class PetEvent
{
 public string Kind {get;set;}="state";
 public string UserId {get;set;}="";
 public string Name {get;set;}="친구";
 public string Species {get;set;}="cat";
 public string Action {get;set;}="";
 public string Message {get;set;}="";
 public double X {get;set;}
 public double Lift {get;set;}
 public bool Left {get;set;}
 public bool Walking {get;set;}
 public bool Sleeping {get;set;}
}

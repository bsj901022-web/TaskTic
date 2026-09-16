using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Markup;

namespace TaskbarTails;

// UI strings in Korean and English. XAML uses {local:T key}; code uses L.Get / L.F.
public static class L
{
    public static string Lang { get; private set; } = "ko";
    public static void Init(string setting)
    {
        Lang = setting == "en" ? "en" : setting == "ko" ? "ko" : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko" ? "ko" : "en";
    }
    public static string Get(string key) => Table.TryGetValue(key, out var v) ? (Lang == "en" ? v.en : v.ko) : key;
    public static string F(string key, params object[] args) => string.Format(Get(key), args);
    public static string Species(PetKind k) => Lang == "en" && SpeciesEn.TryGetValue(k.Id, out var n) ? n : k.Label;
    public static string Group(string ko) => Lang == "en" && GroupEn.TryGetValue(ko, out var n) ? n : ko;
    public static string Action(string species, string key, string koLabel) => Lang == "en" && ActionEn.TryGetValue(species + ":" + key, out var n) ? n : koLabel;

    static readonly Dictionary<string, (string ko, string en)> Table = new()
    {
        ["app_title"] = ("Taskbar Tails · 작은 친구들의 하루", "Taskbar Tails · A day with little friends"),
        ["brand_sub"] = ("작업표시줄 위, 작은 친구들.", "Little friends on your taskbar."),
        ["brand_desc"] = ("바쁜 하루에도 함께하는 나만의 작은 반려동물", "A tiny companion that stays with you through busy days"),
        ["badge_portable"] = (" · 포터블", " · portable"),
        ["kinds"] = ("{0}종", "{0} kinds"),
        ["level"] = ("Lv. {0}", "Lv. {0}"),
        ["care_title"] = ("오늘의 돌봄", "Today's care"),
        ["fullness"] = ("포만감", "Fullness"),
        ["happiness"] = ("행복도", "Happiness"),
        ["xp"] = ("성장 경험치", "Growth XP"),
        ["feed"] = ("먹이 주기", "Feed"),
        ["play"] = ("놀아주기", "Play"),
        ["sleep"] = ("재우기", "Put to sleep"),
        ["wake"] = ("깨우기", "Wake up"),
        ["customize"] = ("나의 친구 꾸미기", "Customize my friend"),
        ["apply"] = ("적용", "Apply"),
        ["group_title"] = ("작은 친구들의 모임", "Little friends' gathering"),
        ["group_desc"] = ("보리와 구름이를 작업표시줄에 불러보세요.", "Invite Bori and Cloud onto your taskbar."),
        ["group_desc2"] = ("실제 친구와는 방에 참여해 움직임과 말풍선을 나눠보세요.", "Join a room to share moves and speech bubbles with real friends."),
        ["room_button"] = ("방 만들기 / 참여", "Create / join a room"),
        ["demo_friends"] = ("데모 친구 2마리", "2 demo friends"),
        ["bubble_title"] = ("내 캐릭터가 할 말 · 최대 80자 · Enter로 보내기", "What my character says · up to 80 chars · Enter to send"),
        ["bubble_send"] = ("말풍선 보내기", "Send bubble"),
        ["bubble_note"] = ("방에 연결되어 있으면 같은 방 친구 화면에도 말풍선이 보여요. 대화 내역은 저장되지 않습니다.", "When connected to a room, friends see your bubble too. Nothing is stored."),
        ["quick_hint"] = ("빠른 반응", "Quick reactions"),
        ["quick_tip"] = ("{0} 또는 캐릭터 휠 클릭으로 캐릭터 머리 위에서 바로 말할 수 있어요.", "Press {0} or middle-click the character to speak right above it."),
        ["settings_title"] = ("설정", "Settings"),
        ["set_language"] = ("언어", "Language"),
        ["lang_auto"] = ("시스템 언어", "System language"),
        ["lang_ko"] = ("한국어", "한국어 (Korean)"),
        ["lang_en"] = ("English", "English"),
        ["set_monitor"] = ("캐릭터가 사는 모니터", "Monitor for the character"),
        ["monitor_item"] = ("모니터 {0} ({1}×{2}){3}", "Monitor {0} ({1}×{2}){3}"),
        ["monitor_primary"] = (" · 기본", " · primary"),
        ["set_size"] = ("작업표시줄 캐릭터 크기", "Taskbar character size"),
        ["size_default"] = ("100% (기본)", "100% (default)"),
        ["set_startup"] = ("Windows 시작 시 자동 실행", "Start with Windows"),
        ["set_hotkeys"] = ("전역 단축키 사용 (Ctrl+Alt+P 숨기기/보이기 · 아래 키로 빠른 말풍선)", "Use global hotkeys (Ctrl+Alt+P hide/show · quick bubble with the key below)"),
        ["set_chat_hotkey"] = ("빠른 말풍선 단축키", "Quick bubble hotkey"),
        ["hotkey_default"] = ("{0} (기본)", "{0} (default)"),
        ["hotkey_press"] = ("키 조합을 누르세요… (Esc 취소)", "Press a key combo… (Esc to cancel)"),
        ["hotkey_reset"] = ("기본값", "Default"),
        ["hotkey_invalid"] = ("Ctrl·Alt·Shift 중 하나와 글자·숫자를 함께 누르거나 F1~F12를 누르세요.", "Hold Ctrl, Alt or Shift with a letter or digit, or press an F key."),
        ["hotkey_saved"] = ("빠른 말풍선 단축키를 {0}로 등록했어요.", "Quick bubble hotkey set to {0}."),
        ["hotkey_conflict"] = ("Ctrl+T처럼 브라우저·편집기가 쓰는 조합을 고르면 이 앱이 켜져 있는 동안 그 프로그램에서는 그 키가 막혀요. Ctrl+Alt+T나 F9처럼 비어 있는 키를 권해요.", "A combo such as Ctrl+T is used by browsers and editors; while Taskbar Tails runs, that key stops working in them. Prefer a free key like Ctrl+Alt+T or F9."),
        ["set_night"] = ("밤 11시~아침 7시 자동 취침", "Auto sleep 11 PM to 7 AM"),
        ["set_idle"] = ("자리 비움 낮잠", "Nap when away"),
        ["idle_off"] = ("끄기", "Off"),
        ["idle_min"] = ("{0}분 후", "after {0} min"),
        ["set_stretch"] = ("50분마다 스트레칭 알림", "Stretch reminder every 50 min"),
        ["set_sound"] = ("클릭 효과음", "Click sound"),
        ["set_greet"] = ("친구 캐릭터와 마주치면 인사", "Greet friends' characters when passing"),
        ["set_bubble_style"] = ("말풍선 스타일 (레벨로 해금)", "Bubble style (unlocked by level)"),
        ["style_0"] = ("크림", "Cream"),
        ["style_1"] = ("민트", "Mint"),
        ["style_2"] = ("라벤더", "Lavender"),
        ["style_3"] = ("피치", "Peach"),
        ["style_locked"] = ("{0} · Lv.{1} 해금", "{0} · unlocks at Lv.{1}"),
        ["set_note"] = ("설정은 바로 적용되고 저장됩니다. 이름 색은 Lv.10부터 금색이 돼요.", "Settings apply immediately and are saved. The name turns gold from Lv.10."),
        ["footer_hint"] = ("클릭: 쓰다듬기  ·  위로 끌어 놓기: 낙하산  ·  휠 클릭: 말하기  ·  우클릭: 메뉴", "Click: pet · Drag up and release: parachute · Middle-click: speak · Right-click: menu"),
        ["footer_note"] = ("창을 닫아도 친구는 남아 있어요. 트레이 아이콘 또는 캐릭터를 더블클릭하면 다시 열립니다.", "Closing this window keeps your friend around. Double-click the tray icon or the character to reopen."),
        ["update_now"] = ("지금 업데이트하고 다시 시작", "Update now and restart"),
        ["update_check"] = ("업데이트 확인", "Check for updates"),
        ["hide_pets"] = ("캐릭터 숨기기", "Hide characters"),
        ["show_pets"] = ("캐릭터 보이기", "Show characters"),
        ["quit"] = ("종료", "Quit"),
        ["mood_sleep"] = ("쉿, 기분 좋은 꿈을 꾸고 있어요.", "Shh, sweet dreams in progress."),
        ["mood_hungry"] = ("배가 고파요. 간식 시간을 기다려요!", "Hungry! Waiting for snack time."),
        ["mood_ok"] = ("오늘도 함께 놀 준비 완료!", "Ready to play together today!"),
        ["name_required"] = ("친구의 이름을 입력해 주세요.", "Please enter a name."),
        ["saved_look"] = ("친구의 이름과 모습을 저장했어요.", "Saved your friend's name and look."),
        ["size_changed"] = ("작업표시줄 캐릭터 크기를 {0}%로 바꿨어요.", "Character size set to {0}%."),
        ["bubble_empty"] = ("말풍선에 넣을 내용을 입력해 주세요.", "Type something for the bubble."),
        ["bubble_sent_room"] = ("말풍선을 방 친구들에게 보냈어요.", "Bubble sent to your room."),
        ["bubble_sent_local"] = ("말풍선을 표시했어요. 방에 연결하면 친구에게도 보여요.", "Bubble shown. Join a room to share it with friends."),
        ["checking_update"] = ("업데이트를 확인하고 있어요…", "Checking for updates…"),
        ["update_ready_btn"] = ("v{0} 지금 업데이트하고 다시 시작", "Update to v{0} now and restart"),
        ["update_ready"] = ("새 버전 v{0}이 준비됐어요. 지금 적용하거나 다음 실행 때 자동으로 적용됩니다.", "Version v{0} is ready. Apply now or it installs on the next launch."),
        ["portable_no_update"] = ("포터블 실행판은 자동 업데이트 대상이 아니에요. GitHub Releases에서 설치판을 받아 주세요.", "The portable build does not auto-update. Get the installer from GitHub Releases."),
        ["latest_version"] = ("최신 버전이에요 ({0}).", "You are on the latest version ({0})."),
        ["downloading"] = ("새 버전 v{0} 내려받는 중…", "Downloading v{0}…"),
        ["downloading_pct"] = ("새 버전 v{0} 내려받는 중… {1}%", "Downloading v{0}… {1}%"),
        ["update_failed"] = ("업데이트 확인 실패 · {0}", "Update check failed · {0}"),
        ["update_ready_bubble"] = ("새 버전 v{0}이 준비됐어요!", "Version v{0} is ready!"),
        ["hello"] = ("안녕! 만나서 반가워", "Hi! Nice to meet you"),
        ["back_short"] = ("잠깐 나갔었네? 다시 반가워!", "Back already? Good to see you!"),
        ["back_long"] = ("오랜만이야! 배고팠어…", "It's been a while! I got hungry…"),
        ["yum"] = ("냠냠, 맛있다!", "Yum, delicious!"),
        ["petted"] = ("쓰담쓰담, 좋아요 ♥", "Pets! I love it ♥"),
        ["lets_play"] = ("같이 놀자!", "Let's play!"),
        ["good_night"] = ("잘 자요… z Z", "Good night… z Z"),
        ["good_morning"] = ("잘 잤다! 좋은 아침!", "Slept well! Good morning!"),
        ["new_look"] = ("새 모습이 마음에 들어!", "I love my new look!"),
        ["idle_nap"] = ("조용하네… 잠깐 눈 붙일게", "It's quiet… I'll nap a little"),
        ["welcome_back"] = ("다시 왔네! 반가워", "You're back! Yay"),
        ["night_sleep"] = ("밤이 늦었네… 잘 자요 z Z", "It's late… good night z Z"),
        ["morning_wake"] = ("아침이야! 좋은 하루!", "Morning! Have a great day!"),
        ["stretch"] = ("50분 지났어요, 잠깐 스트레칭!", "50 minutes passed, time to stretch!"),
        ["greet"] = ("안녕, {0}!", "Hi, {0}!"),
        ["poke_local"] = ("콕!", "Poke!"),
        ["poked_by"] = ("{0}가 콕 찔렀어요!", "{0} poked you!"),
        ["ball_thrown"] = ("받아!", "Catch!"),
        ["ball_received"] = ("{0}가 공을 던졌어요!", "{0} threw a ball!"),
        ["ball_caught"] = ("공 받았다!", "Got the ball!"),
        ["landed"] = ("사뿐! 착지 완료", "Landed softly!"),
        ["fetched"] = ("공 가져왔어요!", "Fetched the ball!"),
        ["demo_click"] = ("안녕! 같이 놀자", "Hi! Let's play"),
        ["demo_bori"] = ("보리 · 데모", "Bori · demo"),
        ["demo_cloud"] = ("구름 · 데모", "Cloud · demo"),
        ["tray_title"] = ("Taskbar Tails · 작은 친구들", "Taskbar Tails · little friends"),
        ["tray_open"] = ("친구 관리 열기", "Open companion panel"),
        ["tray_toggle"] = ("캐릭터 숨기기 / 보이기", "Hide / show characters"),
        ["tray_say"] = ("빠른 말풍선 ({0})", "Quick bubble ({0})"),
        ["ctx_play"] = ("함께 놀기", "Play together"),
        ["ctx_sleep"] = ("잠자기 / 깨우기", "Sleep / wake"),
        ["ctx_say"] = ("말하기", "Speak"),
        ["ctx_poke"] = ("콕 찌르기", "Poke"),
        ["ctx_ball"] = ("공 던지기", "Throw a ball"),
        ["ctx_quit"] = ("프로그램 종료", "Quit"),
        ["room_title"] = ("Taskbar Tails · 함께하는 방", "Taskbar Tails · Room"),
        ["room_head"] = ("함께하는 작은 방", "A little room together"),
        ["room_desc"] = ("같은 방 친구의 움직임과 말풍선이 내 화면에도 보여요.", "Friends in the same room appear on your taskbar with their moves and bubbles."),
        ["room_input"] = ("새 방 이름 또는 받은 초대코드", "New room name or an invite code"),
        ["room_default_name"] = ("우리들의 작은 방", "Our little room"),
        ["room_create"] = ("방 만들기", "Create room"),
        ["room_join"] = ("코드로 참여", "Join with code"),
        ["room_copy"] = ("초대코드 복사", "Copy invite code"),
        ["room_leave"] = ("방 나가기", "Leave room"),
        ["room_members"] = ("함께 있는 친구", "Friends here"),
        ["room_throw"] = ("선택한 친구에게 공 던지기", "Throw a ball to the selected friend"),
        ["room_poke"] = ("선택한 친구 콕 찌르기", "Poke the selected friend"),
        ["room_note"] = ("말풍선은 관리 화면이나 Ctrl+Alt+T로 보냅니다. 이 창을 닫아도 방 연결은 유지되고 초대코드는 관리 화면에도 표시됩니다.", "Send bubbles from the panel or with Ctrl+Alt+T. Closing this window keeps you connected; the invite code also shows on the panel."),
        ["room_status_idle"] = ("방을 만들거나 초대코드를 입력하세요.", "Create a room or enter an invite code."),
        ["room_input_required"] = ("방 이름 또는 초대코드를 입력해 주세요.", "Enter a room name or an invite code."),
        ["room_connecting"] = ("연결하고 있어요…", "Connecting…"),
        ["room_connected"] = ("연결됨 · {0}", "Connected · {0}"),
        ["room_left"] = ("방에서 나왔어요.", "Left the room."),
        ["room_code"] = ("초대코드  {0}", "Invite code  {0}"),
        ["me"] = (" (나)", " (me)"),
        ["room_summary"] = ("방 '{0}' · 초대코드 {1} · 함께 {2}명", "Room '{0}' · code {1} · {2} together"),
        ["select_member"] = ("먼저 목록에서 친구를 선택하세요.", "Select a friend in the list first."),
        ["qc_placeholder"] = ("할 말을 입력하고 Enter · Esc로 닫기", "Type and press Enter · Esc to close"),
        ["crash"] = ("앱 실행 중 오류가 발생했습니다. 프로그램 폴더의 crash.log를 확인해 주세요.", "Something went wrong. See crash.log in the program folder."),
        ["already_running"] = ("이미 실행 중이에요. 트레이 아이콘 또는 캐릭터를 더블클릭해 주세요.", "Already running. Double-click the tray icon or the character."),
        ["hotkey_failed"] = ("전역 단축키를 등록하지 못했어요. 다른 프로그램이 같은 키를 쓰고 있을 수 있어요.", "Could not register the global hotkeys; another app may be using them."),
        ["startup_failed"] = ("시작 프로그램 등록에 실패했어요 · {0}", "Could not change startup registration · {0}"),
        ["send_wait"] = ("전송 대기 · {0}", "Send pending · {0}"),
        ["my_rooms"] = ("내 방 목록 · 전환하면 그 방 친구들만 보여요", "My rooms · switching shows only that room's friends"),
        ["room_switch"] = ("이 방으로 전환", "Switch to this room"),
        ["room_quit"] = ("이 방 탈퇴", "Leave this room for good"),
        ["room_refresh"] = ("목록 새로 고침", "Refresh list"),
        ["current_room"] = (" (현재)", " (current)"),
        ["select_room"] = ("먼저 방 목록에서 방을 선택하세요.", "Select a room in the list first."),
        ["set_rejoin"] = ("시작할 때 마지막 방에 자동 참여", "Rejoin the last room on start"),
        ["chat_hungry"] = ("배가 살짝 고픈 것 같아… 간식 있어?", "I'm getting a little hungry… any snacks?"),
        ["chat_morning"] = ("좋은 아침! 오늘도 같이 힘내자", "Good morning! Let's make today a good one"),
        ["chat_lunch"] = ("점심 먹었어? 나도 간식 좋아해", "Had lunch yet? I love snacks too"),
        ["chat_night"] = ("밤이 깊었네, 슬슬 쉴 시간이야", "It's getting late, time to wind down"),
        ["chat_0"] = ("지금 뭐 하고 있어?", "What are you working on?"),
        ["chat_1"] = ("나 좀 봐줘, 여기 있어!", "Look at me, I'm right here!"),
        ["chat_2"] = ("오늘 하루는 어때?", "How's your day going?"),
        ["chat_3"] = ("같이 있어서 좋아", "I like being here with you"),
        ["chat_4"] = ("잠깐 기지개 한번 할까?", "How about a quick stretch?"),
        ["chat_5"] = ("물 한 잔 마셨어?", "Did you drink some water?"),
        ["chat_6"] = ("친구도 불러볼까? 방 만들기!", "Shall we invite a friend? Make a room!"),
        ["chat_7"] = ("심심하면 나를 위로 던져 봐", "Bored? Toss me up in the air"),
        ["rc_anon"] = ("Supabase에서 Anonymous Sign-Ins를 켜 주세요.", "Enable Anonymous Sign-Ins in Supabase."),
        ["rc_setup"] = ("먼저 Supabase setup.sql을 실행해 주세요.", "Run Supabase setup.sql first."),
        ["rc_species"] = ("서버가 이 캐릭터 종류를 아직 몰라요. supabase/upgrade-v05.sql을 실행해 주세요.", "The server does not know this character kind yet. Run supabase/upgrade-v05.sql."),
        ["rc_https"] = ("Supabase HTTPS 주소를 확인해 주세요.", "Check the Supabase HTTPS URL."),
        ["rc_closed"] = ("서버 연결이 닫혔어요.", "The server closed the connection."),
        ["rc_big"] = ("이벤트가 너무 큽니다.", "The event is too large."),
        ["rc_policy"] = ("방 구독 권한을 확인해 주세요. setup.sql의 Realtime 정책이 필요합니다.", "Room subscription was refused. The Realtime policy from setup.sql is required."),
        ["rc_reconnect"] = ("재연결 중 · {0}", "Reconnecting · {0}"),
        ["rc_rejoined"] = ("새 세션으로 방에 다시 참여했어요.", "Rejoined the room with a new session."),
        ["rc_session_save"] = ("세션 저장 실패 · 다음 실행 시 새 사용자로 시작할 수 있어요.", "Could not save the session; the next run may start as a new user."),
        ["rc_roster_wait"] = ("참여자 갱신 대기 · {0}", "Roster refresh pending · {0}"),
        ["rc_server"] = ("서버 응답 {0}", "Server responded {0}"),
    };

    static readonly Dictionary<string, string> SpeciesEn = new()
    {
        ["cat"] = "Cat", ["rabbit"] = "Rabbit", ["dog"] = "Puppy", ["hamster"] = "Hamster", ["fox"] = "Fox", ["penguin"] = "Penguin",
        ["duck"] = "Duckling", ["bear"] = "Bear cub", ["frog"] = "Frog", ["panda"] = "Panda", ["slime"] = "Slime", ["robot"] = "Robot",
        ["ghost"] = "Ghost", ["mushroom"] = "Mushroom", ["dragon"] = "Baby dragon", ["cactus"] = "Cactus", ["cloudpup"] = "Fluffy puppy",
        ["puffball"] = "White puff", ["bluecat"] = "Dodo cat", ["yellowbunny"] = "Nana bunny", ["boy"] = "Boy", ["girl"] = "Girl",
        ["bobgirl"] = "Bob-cut girl", ["ponygirl"] = "Ponytail girl", ["hoodieboy"] = "Hoodie boy", ["suitboy"] = "Suit boy", ["yeodini"] = "Yeodini",
    };
    static readonly Dictionary<string, string> GroupEn = new() { ["동물"] = "Animals", ["비동물"] = "Creatures", ["마스코트"] = "Mascots", ["사람"] = "People" };
    static readonly Dictionary<string, string> ActionEn = new()
    {
        ["cat:loaf"] = "Cat loaf", ["cat:groom"] = "Groom", ["rabbit:hop"] = "Hop", ["rabbit:ears"] = "Ear twitch", ["dog:fetch"] = "Fetch", ["dog:wag"] = "Tail wag",
        ["hamster:nibble"] = "Nibble", ["hamster:curl"] = "Curl up", ["fox:pounce"] = "Pounce", ["fox:tail"] = "Tail blanket", ["penguin:flap"] = "Flap", ["penguin:bow"] = "Bow",
        ["duck:waddle"] = "Waddle dance", ["duck:preen"] = "Preen", ["bear:honey"] = "Eat honey", ["bear:stretch"] = "Stretch", ["frog:leap"] = "Leap", ["frog:tongue"] = "Tongue flick",
        ["panda:bamboo"] = "Eat bamboo", ["panda:roll"] = "Roll over", ["slime:squish"] = "Squish", ["slime:jiggle"] = "Jiggle", ["robot:wave"] = "Beep wave", ["robot:dance"] = "Robot dance",
        ["ghost:float"] = "Float", ["ghost:peek"] = "Peekaboo", ["mushroom:bounce"] = "Bounce", ["mushroom:spore"] = "Sparkle spores", ["dragon:puff"] = "Fire puff", ["dragon:wings"] = "Flutter",
        ["cactus:bloom"] = "Bloom", ["cactus:shimmy"] = "Shimmy", ["cloudpup:smile"] = "Big smile", ["cloudpup:flop"] = "Flop down", ["puffball:teary"] = "Teary eyes", ["puffball:yay"] = "Yay!",
        ["bluecat:sing"] = "Sing", ["bluecat:grin"] = "Grin", ["yellowbunny:yaha"] = "Yaha jump", ["yellowbunny:spin"] = "Spin", ["boy:wave"] = "Wave", ["boy:cheer"] = "Cheer",
        ["girl:wave"] = "Wave", ["girl:cheer"] = "Cheer", ["bobgirl:wave"] = "Wave", ["bobgirl:heart"] = "Finger heart", ["ponygirl:stretch"] = "Stretch", ["ponygirl:cheer"] = "Cheer",
        ["hoodieboy:wave"] = "Wave", ["hoodieboy:dance"] = "Dance", ["suitboy:bow"] = "Polite bow", ["suitboy:thumbs"] = "Thumbs up",
        ["yeodini:swing"] = "Tennis swing", ["yeodini:goggles"] = "Goggles on",
    };
}

// Selectable quick-bubble hotkeys. Ctrl+T is offered but flagged: it is what browsers and editors use.
public static class Hotkeys
{
    public const string Default = "ctrl+alt+t";
    // "ctrl+alt+t" style id from a key press; "" when the key is not supported (letters, digits, F1-F12, space).
    public static string Compose(System.Windows.Input.ModifierKeys mods, System.Windows.Input.Key key)
    {
        var parts = new List<string>();
        if (mods.HasFlag(System.Windows.Input.ModifierKeys.Control)) parts.Add("ctrl");
        if (mods.HasFlag(System.Windows.Input.ModifierKeys.Alt)) parts.Add("alt");
        if (mods.HasFlag(System.Windows.Input.ModifierKeys.Shift)) parts.Add("shift");
        string main = key switch
        {
            >= System.Windows.Input.Key.A and <= System.Windows.Input.Key.Z => ((char)('a' + (key - System.Windows.Input.Key.A))).ToString(),
            >= System.Windows.Input.Key.D0 and <= System.Windows.Input.Key.D9 => ((char)('0' + (key - System.Windows.Input.Key.D0))).ToString(),
            >= System.Windows.Input.Key.F1 and <= System.Windows.Input.Key.F12 => "f" + (key - System.Windows.Input.Key.F1 + 1),
            System.Windows.Input.Key.Space => "space",
            _ => "",
        };
        if (main.Length == 0) return "";
        parts.Add(main); return string.Join("+", parts);
    }
    // A modifier is required except for F keys, so a plain letter can never be swallowed system-wide.
    public static bool IsValid(string id)
    {
        var (mods, key) = Native.ParseHotkey(id ?? "");
        return key != 0 && (mods != 0 || (key >= 0x70 && key <= 0x7B));
    }
    public static string Label(string id) => string.Join("+", (id ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Length <= 3 && p != "alt" ? p.ToUpperInvariant() : char.ToUpperInvariant(p[0]) + p[1..]));
    // Combos that browsers, editors and Windows itself commonly use; still allowed, but the settings card warns.
    public static bool Conflicts(string id)
    {
        var (mods, key) = Native.ParseHotkey(id ?? "");
        bool ctrl = (mods & 0x2) != 0, alt = (mods & 0x1) != 0, shift = (mods & 0x4) != 0;
        if (ctrl && !alt) return true;                 // Ctrl+X / Ctrl+Shift+X: app shortcuts
        if (alt && !ctrl && !shift) return true;        // Alt+X: menu accelerators
        if (mods == 0) return key is 0x70 or 0x74 or 0x79 or 0x7A or 0x7B; // F1 F5 F10 F11 F12
        return false;
    }
}

// {local:T key} in XAML resolves to the string for the current language at load time.
public sealed class TExtension : MarkupExtension
{
    public string Key { get; set; }
    public TExtension(string key) { Key = key; }
    public override object ProvideValue(IServiceProvider serviceProvider) => L.Get(Key);
}

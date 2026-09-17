# Changelog · 변경 기록

릴리스마다 이 파일의 해당 절이 GitHub Release 노트로 올라갑니다. Each release's section is published as its GitHub Release notes.

## v0.6.8

**한국어**
- 서버 부하 대폭 감소: 캐릭터 이벤트를 RPC 대신 Realtime 웹소켓 Broadcast로 직접 보냅니다. 이전에는 이벤트마다 realtime.messages 테이블에 행이 하나씩 쌓여(접속자 1명당 초당 1~3행) 데이터베이스 용량과 CPU를 잡아먹었는데, 이제 이벤트는 DB에 전혀 기록되지 않습니다.
- 참여자 명단은 15초마다 서버를 조회하던 방식에서 Realtime Presence로 바꿨습니다. 입장·퇴장이 즉시 반영되고 DB 요청은 1분에 한 번의 last_seen 갱신만 남습니다.
- 서버 SQL 갱신 필요: 기존 프로젝트는 `supabase/upgrade-v08.sql`을 한 번 실행하세요(Broadcast·Presence 허용 정책 추가, 쌓인 realtime.messages 정리, pg_cron으로 매시 자동 정리). 새 프로젝트는 `setup.sql` 하나로 끝납니다. v0.6.7 이하 클라이언트도 계속 동작합니다.
- 기본 접속 프로젝트가 새 Supabase 프로젝트로 바뀌었습니다. 기존 방은 새 프로젝트에서 다시 만들어야 하며, 앱은 자동으로 새 익명 사용자를 만듭니다.

**English**
- Much lighter on the server: character events are now Realtime broadcasts over the websocket instead of an RPC. Previously every event inserted a row into realtime.messages (1-3 rows per second per client), eating database storage and CPU; events no longer touch the database at all.
- The roster comes from Realtime presence instead of polling the member table every 15 s; joins and leaves show immediately and only a once-a-minute last_seen update remains.
- Server SQL update required: existing projects run `supabase/upgrade-v08.sql` once (broadcast/presence policy, purge of accumulated realtime.messages rows, hourly pg_cron purge). New projects only need `setup.sql`. Clients on v0.6.7 and older keep working.
- The default Supabase project changed; rooms must be re-created there and the app creates a fresh anonymous user automatically.

## v0.6.7

**한국어**
- 화면 가장자리 타고 돌기(설정에서 끌 수 있음): 작업표시줄 끝에 닿으면 절반쯤의 확률로 돌아서는 대신 화면 좌우 끝 벽을 타고 올라가고, 위쪽 끝에서는 뒤집혀 천장을 걷다가 반대편 벽으로 내려옵니다. 도중에 가끔 방향을 바꾸거나 손이 미끄러져 낙하산으로 내려옵니다. 벽에서는 캐릭터를 90° 돌려 발이 화면 끝에 닿게, 천장에서는 위아래를 뒤집어 그립니다. 벽·천장에서는 이름과 말풍선 위치도 그에 맞게 옮겨집니다.
- 캐릭터를 끌어 화면 좌우 끝에 놓으면 그 높이에서 바로 벽을 잡고, 우클릭 메뉴의 "벽 타고 올라가기"를 누르면 가까운 벽으로 걸어가 올라갑니다(시험용으로도 좋습니다).
- 캐릭터 창 폭을 160→220으로 넓혀 회전한 몸이 잘리지 않게 했습니다.

**English**
- Roam the screen edges (can be switched off in Settings): at the end of the taskbar the character may climb the left or right screen edge instead of turning, walk upside down along the top edge and come down the other side, sometimes turning back or slipping into a parachute drop. On walls the sprite is turned 90° with its feet on the edge; on the ceiling it is mirrored. Names and bubbles follow.
- Drop the character against the left or right screen edge to make it grab the wall there; the right-click menu's "Climb the screen edge" walks to the nearest edge and climbs.
- The character window is wider (160→220) so the turned body is never clipped.

## v0.6.6

**한국어**
- 방 친구 캐릭터가 각 PC에서 스스로 걸어 다닙니다. 상대 위치를 실시간으로 따라가지 않으므로 인터넷 지연 때문에 멈추거나 제자리걸음을 하거나 빠르게 따라붙는 증상이 사라집니다. 위치 정보는 낙하산으로 내려올 때와 풍선을 타고 올라갈 때, 창 위에 착지했을 때만 받아 그 자리에서 재현합니다. 특수 모션·잠·기상·말풍선·콕 찌르기·공은 그대로 전달됩니다.
- 인사는 친구가 방에 들어올 때 한 번만 합니다(마주칠 때마다 인사하던 동작 제거). 설정 문구도 "친구가 방에 들어오면 인사"로 바뀌었습니다.
- 말풍선 구분: 캐릭터가 스스로 하는 말(잡담·반응·착지 대사)은 점선 테두리에 작은 동그라미 꼬리가 달린 **생각 구름**으로, 사람이 직접 입력한 말은 진한 글씨의 **말풍선**으로 그립니다. 친구 캐릭터도 같은 규칙입니다.
- 창 옆면을 타고 오르던 동작을 **풍선 타고 올라가기**로 바꿨습니다. 캐릭터가 창 아래로 걸어가 작은 풍선을 잡고 살짝 흔들리며 창 윗변까지 떠올라 착지합니다. 창이 작업표시줄에 닿아 있을 필요가 없어 더 자주 볼 수 있습니다.

**English**
- Friends' characters now walk on their own on each PC instead of tracking the owner's position, so network latency can no longer make them stop, walk in place or rush to catch up. Position is only received where it matters: a parachute drop, a balloon ride and a landing on a window. Motions, sleep, bubbles, pokes and balls are still relayed.
- Greetings happen once, when a friend joins the room (no more greeting on every encounter).
- Bubble types: the character's own lines are thought clouds (dashed edge, little circle tail); typed messages are speech bubbles with bold text. Friends' characters follow the same rule.
- Climbing a window's side is replaced by a balloon ride: the character walks under the window, grabs a small balloon and floats up to the top edge. Windows no longer need to touch the taskbar.

## v0.6.5

**한국어**
- 방 친구 캐릭터가 더 매끄럽게 움직입니다. 방향을 바꾸거나 멈추거나 다시 걷기 시작하면 다음 정기 스냅샷(이제 2초)을 기다리지 않고 0.3초 안에 바로 알려 주고, 걷는 속도도 함께 보내 상대 화면 너비에 맞춰 같은 빠르기로 걷습니다. 그래서 3초 뒤에 미끄러지듯 되돌아가던 "순간이동"이 거의 사라집니다.
- 친구 캐릭터가 작업표시줄 위에 떠 있던 문제 수정: 상대가 바닥에 있다고 알려 오면 높이를 서서히 줄이는 대신 즉시 바닥에 붙입니다. 착지 이벤트가 늦거나 빠져도 다음 스냅샷에서 바로 내려옵니다.
- 자동 말풍선 켜기/끄기: 잡담·반응·창 한마디 같은 자동 말풍선을 숨기는 설정과 단축키(기본 Ctrl+Alt+B). 직접 입력한 말과 친구가 보낸 말은 항상 보입니다. 트레이 메뉴에서도 켜고 끌 수 있습니다.
- 캐릭터 숨기기/보이기 단축키도 설정에서 원하는 키로 바꿀 수 있습니다(기본 Ctrl+Alt+P). 세 단축키는 서로 같은 키를 쓸 수 없습니다.

**English**
- Friends' characters move more smoothly: turns, stops and starts are sent within 0.3 s instead of waiting for the periodic snapshot (now every 2 s), and the walking pace is shared so the copy walks at the same speed scaled to the other screen. The slide-back "teleport" after 3 s is mostly gone.
- Fix: friends' characters hovering above the taskbar. When a friend reports standing on the ground the copy snaps down immediately instead of easing.
- Automatic bubbles on/off: a setting and hotkey (default Ctrl+Alt+B) hide small talk, reactions and window comments. Typed messages, yours and your friends', always show. Also in the tray menu.
- The hide/show characters hotkey is now rebindable too (default Ctrl+Alt+P). The three hotkeys cannot share a key.

## v0.6.4

**한국어**
- 창 위에서 놀기(설정에서 끌 수 있음): 캐릭터를 끌어 올려 다른 프로그램 창 위에 놓으면 그 창의 윗변에 착지해 걸어 다닙니다. 창을 옮기면 따라가고, 창이 닫히거나 최소화되거나 다른 창에 가려지면 낙하산을 펴고 내려옵니다. 가장자리에서는 돌아서거나 가끔 뛰어내립니다. 작업표시줄까지 닿아 있는 창은 1.5~3분마다 옆면을 타고 올라갑니다(걷기 프레임을 옆으로 돌려 벽을 오르는 모션). 활성 창 제목을 보고 한마디도 합니다(영상·음악·게임·코딩·문서·표·메신저·쇼핑·브라우저).
- 경량 설계: 창 목록은 캐릭터가 공중이거나 창 위에 있을 때만 초당 4회, 평소에는 2초에 한 번 읽고, 활성 창은 제목만 2초마다 확인합니다. 접근성 API나 창 내부 요소 조회는 쓰지 않습니다. 설정을 끄면 창을 전혀 조회하지 않습니다.
- 먹이 버튼 이름이 캐릭터에 따라 바뀝니다: 동물·마스코트·로봇은 "먹이 주기", 사람은 "음식 먹기".
- 과식 방지: 포만감이 85 이상일 때 먹이면 먹지 않고 정면을 보며 고개를 젓습니다("배불러… 더는 못 먹어"). 경험치 -4(현재 레벨 아래로는 내려가지 않음), 행복도 -4. 포만감 게이지는 배부름 상태에서 주황색으로 바뀌고 "배부름" 표시가 붙습니다.

**English**
- Play on windows (can be switched off in Settings): drop the character onto another program's window and it lands on the top edge and walks along it, follows the window when it moves, and parachutes down when the window closes, minimises or gets covered. At the ends it turns around or occasionally hops off. Windows that reach the taskbar get climbed every 1.5-3 minutes (walk frames turned sideways against the edge). The character also comments on the active window's title (video, music, games, coding, documents, spreadsheets, chat, shopping, browsing).
- Lightweight by design: the window list is read four times a second only while the character is airborne or on a window, otherwise every 2 s; only the active window's title is checked every 2 s. No accessibility API or in-window element queries; nothing is queried when the setting is off.
- The feed button reads "Feed" for animals, mascots and the robot and "Eat" for human characters.
- Overfeeding guard: at 85+ fullness the character refuses to eat, faces you and shakes its head; XP -4 (never below the current level) and happiness -4. The fullness bar turns orange with a "full" tag.

## v0.6.3

**한국어**
- "정보 · 문의"가 관리 화면 오른쪽에 붙어 함께 움직이는 사이드 창으로 바뀌었습니다(오른쪽에 공간이 없으면 왼쪽). 버전, 문의 메일(bsj_2200@naver.com, 메일 앱 열기·주소 복사), Instagram @Rinsomnia__, GitHub 저장소, 버그 신고·제안(Issues), 릴리스 노트, 데이터·로그 폴더 열기, 조작 방법을 섹션으로 정리했습니다. 만든 사람 표기를 박상준으로 바로잡고, 주소의 밑줄(_)이 버튼에서 사라지던 표시 오류를 고쳤습니다.
- 쩡을 다시 그렸습니다: 생머리에 가까운 긴 검은 머리, 다른 사람 캐릭터와 같은 또렷한 얼굴, 빨간 상의와 네이비 H라인 롱스커트. 지또는 흰 티 + 회색 셔츠 + 짙은 청바지로 옷을 바꿨습니다.
- 클릭하면 캐릭터마다 다른 인사·응원·취미 한마디를 합니다(공통 8개 + 종류별 전용 문구, 연속 반복 없음). 신체 접촉을 연상시키는 표현은 쓰지 않습니다.
- 이름 표시 설정: 숨기기 / 기본(작게) / 크게(배경 라벨). 크게를 고르면 굵은 글자를 밝은 라벨 위에 올려 어떤 배경에서도 읽힙니다.

**English**
- About · Contact is now a side panel docked to the main window (moves with it; docks left when there is no room): version, email (mail app, copy), Instagram, GitHub repo, Issues, releases, data/log folders and a controls guide. Author credit corrected to Sangjun Park; underscores in the addresses no longer disappear from the buttons.
- Jjeong redrawn: long nearly-straight black hair, a clear youthful face matching the other humans, red top and navy H-line long skirt. Jitto's outfit is now a white tee, gray shirt and dark jeans.
- Clicking the character now gives a per-character greeting, cheer or hobby line (shared pool plus species lines, never repeated back to back); no physical-contact wording.
- Name display setting: hidden / small / large on a label (bold text on a light rounded label, readable on any wallpaper).

## v0.6.2

**한국어**
- 새 캐릭터 4종: **여디니**(밝은 갈색 단발, 머리 위 수영안경, 테니스 라켓 · 테니스 스윙/물안경 쓰기), **지또**(검은 히피펌과 앞머리, 두부처럼 하얀 피부 · 머리 빙글/브이 포즈), **쩡**(약한 파마, 원색 상의와 긴 치마 · 치마 빙글/신나는 박수), **승현**(키 크고 안경, 투블럭, 밝은 청바지 · 안경 고쳐 쓰기/어깨 스트레칭).
- 비동물 캐릭터는 로봇만 남기고 슬라임·유령·버섯·아기 드래곤·선인장은 선택 목록에서 숨겼습니다. 이미 그 종류를 쓰는 친구의 캐릭터는 그대로 보입니다. 선택 가능 25종.
- 픽셀이 깨져 보이던 문제 수정: 스프라이트를 화면 픽셀 정수(또는 0.5) 배율로만 그립니다. 100%는 1:1(지금보다 약 1.3배 크게), 150%는 1.5배, 200%는 2배이며 위치도 픽셀 격자에 맞춥니다.
- 설정 아래 "정보 · 문의" 카드: 문의 메일(bsj_2200@naver.com, 클릭하면 메일 앱·주소 복사), Instagram @Rinsomnia__, GitHub 저장소, 버그 신고·제안(Issues), 릴리스 노트, 데이터·로그 폴더 열기.
- 버그 수정: 낙하산으로 떨어지는 중에 클릭(또는 더블클릭의 첫 클릭)하면 공중에 멈춘 채 걷던 문제. 클릭 후 낙하를 재개하고, 공중에 있는데 낙하 중이 아니면 자동으로 낙하를 시작합니다.
- 캐릭터가 40~90초마다 정면을 보며 상황에 맞는 말을 합니다(아침·점심·밤 인사, 배고픔, 잡담). 마우스를 올리면 정면을 보고, 빠른 말풍선을 열면 나를 쳐다봅니다. 정면 상태는 방 친구에게도 전달됩니다.

**English**
- Four new human characters: **Yeodini** (light-brown bob, swim goggles, tennis racket), **Jitto** (black hippie perm with bangs, porcelain skin), **Jjeong** (light perm, primary-color top, long skirt), **Seunghyeon** (tall, glasses, two-block cut, light jeans), each with two motions.
- Creature kinds other than the robot (slime, ghost, mushroom, dragon, cactus) are hidden from the picker; friends already using them still render. 25 selectable kinds.
- Crisp pixels: sprites are drawn at whole/half device-pixel multiples only (100% = 1:1, about 1.3x larger than before; 150% = 1.5x; 200% = 2x), snapped to the pixel grid.
- About · Contact card under Settings: email (opens mail app, copy button), Instagram, GitHub repo, Issues, release notes, open data/log folders.
- Fix: clicking the character mid-parachute (including the first click of a double-click) left it hovering and walking in the air; the fall now resumes.
- The character now turns to face you every 40-90 s and says something fitting (time of day, hunger, small talk), faces you on hover and when the quick bubble opens; friends see it too.

## v0.6.1

**한국어**
- 빠른 말풍선 단축키를 설정에서 키를 직접 눌러 바꿀 수 있습니다(기본 Ctrl+Alt+T). Ctrl·Alt·Shift + 글자/숫자 또는 F1~F12. Ctrl+T처럼 브라우저·편집기가 쓰는 조합은 등록되지만 경고를 표시하고, 다른 앱이 선점한 키는 등록 실패를 안내합니다.

**English**
- The quick-bubble hotkey can be rebound by pressing keys in Settings (default Ctrl+Alt+T). Combos that clash with browsers and editors (e.g. Ctrl+T) are allowed but flagged.

## v0.6.0

**한국어**
- 자리 비움 감지: 입력이 없으면(기본 5분) 캐릭터가 낮잠을 자고, 돌아오면 일어나 인사합니다.
- 꺼져 있던 시간 반영: 다시 켤 때 경과 시간만큼 포만감·행복도가 조금 줄고(하한 있음) "오랜만이야" 같은 인사를 합니다.
- 친구 캐릭터와 마주치면 서로 인사하고, 친구 캐릭터를 클릭하면 콕 찌릅니다. 친구에게 공을 던지면 상대 화면에서 굴러갑니다.
- 빠른 반응 이모지(❤️ 👋 😂 👍 😢 🎉)와 Ctrl+Alt+T(또는 캐릭터 휠 클릭)로 캐릭터 머리 위에 바로 뜨는 말풍선 입력칸.
- 방 여러 개: 내 방 목록에서 전환·탈퇴, 마지막 방 자동 재참여. 방마다 대화와 캐릭터가 분리됩니다.
- 설정: 한국어/English, 캐릭터가 사는 모니터 선택(작업표시줄 자동 숨김 대응), Windows 시작 시 실행, 전역 단축키(Ctrl+Alt+P 숨기기/보이기), 밤 11시~7시 자동 취침, 50분 스트레칭 알림, 클릭 효과음, 말풍선 스타일(레벨로 해금), Lv.10부터 금색 이름.
- 움직임: 60Hz 렌더 타이머, 위치·프레임이 바뀔 때만 창을 옮기고 다시 그려 끊김 감소. 친구 캐릭터는 연속으로 걷고 3초 스냅샷은 보정에만 사용.
- 설치판이 런타임을 포함하지 않아 약 10MB로 줄었습니다. 런타임이 없는 PC는 설치 중 자동으로 받습니다. 런타임 포함 포터블 ZIP은 별도 제공.

**English**
- Idle detection (nap after 5 minutes by default, greeting on return), offline time applied on launch, greetings when characters meet, poke a friend, throw a ball across screens.
- Quick reactions and a quick bubble input above the character (Ctrl+Alt+T or middle-click).
- Multiple rooms: list, switch, leave; auto-rejoin the last room. Each room is isolated.
- Settings: Korean/English UI, monitor selection (auto-hide taskbar aware), start with Windows, global hotkeys, night sleep, stretch reminder, click sound, bubble styles unlocked by level.
- Smoother motion (60 Hz render timer, redraw/move only on change); friends walk continuously.
- Installer no longer bundles the runtime (~10 MB); it installs .NET 8 desktop runtime on demand. A self-contained portable zip is still provided.

## v0.5.4
- CI 스모크 테스트를 진단 단계로 전환하고 결과를 실행 요약에 기록. 첫 자동 릴리스 성공.
- CI smoke test made diagnostic; first automated release published.

## v0.5.3
- 인간형 캐릭터 4종 추가(검은 단발 여자, 포니테일 여자, 후드 남자, 정장 남자) → 총 26종.
- Four more human characters (26 total).

## v0.5.2
- Velopack 설치판과 자동 업데이트, GitHub Actions 릴리스 워크플로, 앱 아이콘.
- Velopack installer with auto-update, GitHub Actions release workflow, app icon.

## v0.5.1
- 공유한 앱 폴더의 세션 파일 때문에 모두 같은 사용자로 로그인되던 문제 수정(세션을 사용자별 폴더로 이동).
- 방 창을 닫아도 초대코드 유지, 관리 화면에 방 정보 표시, 말풍선 입력을 관리 화면으로 이동, 캐릭터 크기 100/150/200%.
- Fixed shared identity from a copied session file; invite code kept after closing the room window; bubble input on the main panel; character size setting.

## v0.5
- PixelLab 캐릭터 22종, 16프레임 걷기와 특수 모션, 낙하산, 강아지 공 놀이, Supabase 방(초대코드·말풍선·실시간 동작 공유).
- 22 PixelLab characters with 16-frame walks and motions, parachute drop, fetch, Supabase rooms with invite codes.

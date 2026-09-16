# Changelog · 변경 기록

릴리스마다 이 파일의 해당 절이 GitHub Release 노트로 올라갑니다. Each release's section is published as its GitHub Release notes.

## v0.6.2

**한국어**
- 새 캐릭터 4종: **여디니**(밝은 갈색 단발, 머리 위 수영안경, 테니스 라켓 · 테니스 스윙/물안경 쓰기), **지또**(검은 히피펌과 앞머리, 두부처럼 하얀 피부 · 머리 빙글/브이 포즈), **쩡**(긴 검은 머리에 약한 웨이브, 원색 상의와 H라인 롱스커트 · 빙글 돌기/신나는 박수), **승현**(키 크고 안경, 투블럭, 밝은 청바지 · 안경 고쳐 쓰기/어깨 스트레칭).
- 비동물 캐릭터는 로봇만 남기고 슬라임·유령·버섯·아기 드래곤·선인장은 선택 목록에서 숨겼습니다. 이미 그 종류를 쓰는 친구의 캐릭터는 그대로 보입니다. 선택 가능 25종.
- 픽셀이 깨져 보이던 문제 수정: 스프라이트를 화면 픽셀 정수(또는 0.5) 배율로만 그립니다. 100%는 1:1(지금보다 약 1.3배 크게), 150%는 1.5배, 200%는 2배이며 위치도 픽셀 격자에 맞춥니다.
- "정보 · 문의" 버튼을 누르면 관리 화면 오른쪽에 붙어 함께 움직이는 사이드 창이 열립니다(오른쪽에 공간이 없으면 왼쪽). 버전, 문의 메일(bsj_2200@naver.com, 메일 앱 열기·주소 복사), Instagram @Rinsomnia__, GitHub 저장소, 버그 신고·제안(Issues), 릴리스 노트, 데이터·로그 폴더 열기, 조작 방법 안내를 섹션으로 정리했습니다.
- 버그 수정: 낙하산으로 떨어지는 중에 클릭(또는 더블클릭의 첫 클릭)하면 공중에 멈춘 채 걷던 문제. 클릭 후 낙하를 재개하고, 공중에 있는데 낙하 중이 아니면 자동으로 낙하를 시작합니다.
- 만질 때 반응이 캐릭터마다 다릅니다: 공통 문구 8개에 종류별 전용 문구(고양이 "골골골…", 강아지 "꼬리가 멈추지 않아!", 로봇 "삐빅. 만족도 12% 상승" 등)를 섞어 무작위로 고르고 같은 말은 연달아 나오지 않습니다.
- 이름 표시 설정: 숨기기 / 기본(작게) / 크게(배경 라벨). 크게를 고르면 굵은 글자를 밝은 라벨 위에 올려 어떤 배경에서도 읽힙니다.
- 캐릭터가 40~90초마다 정면을 보며 상황에 맞는 말을 합니다(아침·점심·밤 인사, 배고픔, 잡담). 마우스를 올리면 정면을 보고, 빠른 말풍선을 열면 나를 쳐다봅니다. 정면 상태는 방 친구에게도 전달됩니다.

**English**
- Four new human characters: **Yeodini** (light-brown bob, swim goggles, tennis racket), **Jitto** (black hippie perm with bangs, porcelain skin), **Jjeong** (long black lightly-waved hair, primary-color top, H-line long skirt), **Seunghyeon** (tall, glasses, two-block cut, light jeans), each with two motions.
- Creature kinds other than the robot (slime, ghost, mushroom, dragon, cactus) are hidden from the picker; friends already using them still render. 25 selectable kinds.
- Crisp pixels: sprites are drawn at whole/half device-pixel multiples only (100% = 1:1, about 1.3x larger than before; 150% = 1.5x; 200% = 2x), snapped to the pixel grid.
- About · Contact opens as a side panel docked to the main window (moves with it): version, email (mail app, copy), Instagram, GitHub repo, Issues, releases, data/log folders and a controls guide.
- Fix: clicking the character mid-parachute (including the first click of a double-click) left it hovering and walking in the air; the fall now resumes.
- Touch reactions now vary per character (shared pool plus species lines), never the same line twice in a row.
- Name display setting: hidden / small / large on a label (bold text on a light rounded label, readable on any wallpaper).
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

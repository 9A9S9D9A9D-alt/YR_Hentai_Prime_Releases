# 유란 모드 확장 — 공개 배포

RimWorld 1.7용 Yuran Hentai Prime · Restoration v8의 실행 파일과 공개 소스입니다.

- **[실행용 모드 ZIP 다운로드](https://github.com/9A9S9D9A9D-alt/YR_Hentai_Prime_Releases/releases/download/restoration-v8-20261106-apparel-guard/YR_Hentai_Prime_20261106_ApparelGenerationGuard.zip)**
- [최신 릴리스와 변경점](https://github.com/9A9S9D9A9D-alt/YR_Hentai_Prime_Releases/releases/latest)
- [소스 ZIP 다운로드](https://github.com/9A9S9D9A9D-alt/YR_Hentai_Prime_Releases/archive/refs/tags/restoration-v8-20261106-apparel-guard.zip)

게임에 설치할 때는 **실행용 모드 ZIP**을 받으세요. GitHub의 `Source code` ZIP은 C#·XML 소스이며 실행용 모드 전체가 아닙니다.

## 최신 수정사항 · 2026-11-06

- **사목 의상 4종의 외부 자동 생성·지급·장착 차단:** 특정 종족이나 모드 이름 목록에 의존하지 않고 지급 출처를 검사합니다.
- 폰 생성 이후의 별도 장착, 지연 이벤트, 인벤토리·지면 지급으로 만들어진 외부 의상도 차단·정리합니다.
- 사목나무를 포함한 이 모드 내부 지급 경로와 일반 제작·착용, 기존 저장본의 보유 의상은 유지합니다.
- 기존에는 일반 생성 후보를 제외했지만, 외부 코드가 의상을 직접 만들어 지급하는 경로에는 출처 검사가 없었습니다. 이 빈틈을 보완했습니다.

**검증:** HAR 인간형 정의 42종·남녀 84개체, 야생 드라고니안 출현과 비스카 이벤트의 폰 생성 경로를 포함해 496개 검사 통과. 외부 후처리 장착 시도 352개를 차단·정리했고, 기존 의상 109개를 보존했습니다. 사목나무 내부 장착은 출처 판정과 기존 소스 보존을 확인했으며, 이벤트 자체를 재생한 검사는 아닙니다.

## 함께 포함된 주요 수정

- 사목의 딸 장비 탭의 빈 의상 관리자 오류 수정. 동물 분류와 무기 사용, 의상 착용 제한은 유지합니다.
- 유란 이외 종족의 의상 텍스처 경로 오류에 대한 대체 경로 처리.
- 전도근의 전력 연결, 맵 전체 숨김·표시와 파지직 발생원 제외 처리.
- 사목뿌리 지반의 초기 10×10 범위와 드래그 확장, 뿌리 장식 단일 배치 및 관련 아이콘 개선.
- 신규 이미지의 해상도·용량 최적화와 애니메이션 프레임 점검. 기존 이미지 자산은 유지합니다.

## 필수 선행 모드

| 모드 | 패키지 ID | 설치 페이지 |
|---|---|---|
| Harmony | `brrainz.harmony` | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) |
| Humanoid Alien Races | `erdelf.HumanoidAlienRaces` | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=839005762) |
| Apparel BodyType Resolver | `gguake.apparelbodyresolver` | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2037354634) |
| 유란 종족 원본 — Yuranrace | `RooAndGloomy.YuranRaceMod` | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2844129100) |

RJW·SizedApparel·추가 종족 모드는 선택 연동입니다. RJW 미설치 상태에서의 전체 기능 실행 검증은 아직 완료하지 않았습니다. 호환성 시험에 사용한 모든 종족 모드를 설치해야 하는 것은 아닙니다.

## 설치

1. RimWorld 1.7에 맞는 필수 선행 모드 4개를 설치하고 활성화합니다.
2. 위 **실행용 모드 ZIP**을 받아 압축을 풉니다.
3. `YR_Hentai_Prime` 폴더를 게임의 `Mods` 폴더에 넣습니다. `Mods/YR_Hentai_Prime/About/About.xml` 구조인지 확인하세요.
4. 선행 모드보다 뒤에 이 모드를 배치하고 게임을 재시작합니다.

## 배포 범위

- 실행용 ZIP에는 모드만 포함합니다. README·기타 MD·소스·로그·검사 결과·백업·테스트 도구는 넣지 않습니다.
- 공개 저장소에는 C# 소스·XML 정의와 이 안내를 제공합니다. 개발 저장소와 수정 이력은 비공개로 유지합니다.
- **사목 공물함과 종족 테스트맵은 현재 테스트 도우미 소속이며, 실행용 본체 ZIP에는 포함하지 않습니다.** 공물함 연결·분산 공급 기능의 검증은 해당 테스트 환경에서 진행했습니다.

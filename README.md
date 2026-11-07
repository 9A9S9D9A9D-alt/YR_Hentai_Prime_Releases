# 유란 모드 확장 — 공개 배포

RimWorld 1.7용 Yuran Hentai Prime · Restoration v8의 실행 파일과 공개 소스입니다.

- **[실행용 모드 ZIP 다운로드](https://github.com/9A9S9D9A9D-alt/YR_Hentai_Prime_Releases/releases/download/restoration-v8-20261107-fluid-sound/YR_Hentai_Prime_20261107_FluidSound.zip)**
- [최신 릴리스와 변경점](https://github.com/9A9S9D9A9D-alt/YR_Hentai_Prime_Releases/releases/latest)
- [소스 ZIP 다운로드](https://github.com/9A9S9D9A9D-alt/YR_Hentai_Prime_Releases/archive/refs/tags/restoration-v8-20261107-fluid-sound.zip)

게임에 설치할 때는 **실행용 모드 ZIP**을 받으세요. GitHub의 `Source code` ZIP은 C#·XML 소스이며 실행용 모드 전체가 아닙니다.

## 최종 변경사항 · 2026-11-07

- **촉수 이식체와 포획** — 사목의 나무에서 이식 씨앗을 배양합니다. 이식체의 촉수 공격, 원거리 백윤액 사격, 전기 충전, 도약, 응급 처치와 포획를 지원합니다. 스플릿 마우스 외피는 한 번 펼쳐진 뒤 붙잡은 형태를 유지합니다.
- **스플릿 마우스 말풍선** — 모드를 로드한 뒤 붙잡힌 폰에게 실제로 적용된 기본 몸체를 표시합니다. SA0 사용 시 해당 몸체 부품도 포함합니다. 장비·의상·머리는 표시하지 않습니다. 자동 방향은 앞모습이며, 몸체의 위치·배율·방향을 종족·제노타입·체형·성별별로 저장합니다.
- **안쪽 촉수와 백윤액 착취** — 양옆에서 감기고 아래에서 교차하는 촉수가 말풍선 안에서 움직입니다. 게임용 이미지는 256×256, 약 22.9KB입니다. 스플릿 마우스로 대상을 붙잡은 상태에서 백윤액 착취를 시작하면 액체가 차오르고, 게임 시간 1시간 후 백윤액 10개가 배출됩니다. 백윤액 난사와 원거리 사출에 짧고 묵직한 침 뱉는 소리를 적용했습니다.
- **사목 무기와 연단낭** — 연단낭의 작업 탭에서 사목의 활과 단검을 제작합니다. 각 무기의 재료는 사목의 촉수 140개, 백교질 95개, 맥동조직 30개입니다. 연단낭의 내부 원료 가공과 제작 작업은 함께 진행할 수 있습니다. 활과 단검의 상호 변형은 한 무기를 다른 형태로 바꾸며 제작품의 품질을 유지합니다.

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

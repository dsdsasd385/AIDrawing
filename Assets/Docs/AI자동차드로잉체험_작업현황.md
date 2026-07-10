# AI 자동차 드로잉 체험 — 작업 현황

> 기준 문서: [작업계획서](AI자동차드로잉체험_작업계획서.md) · 환경/함정: [인수인계](AI자동차드로잉체험_인수인계.md)
> 최종 갱신: 2026-07-10
>
> **갱신 규칙**: 이 문서는 **모든 작업 세션이 끝날 때마다** 갱신한다. 절차는 맨 아래 [5. 문서 갱신 절차](#5-문서-갱신-절차-작업-세션-종료-시) — 세 문서를 함께 점검하는 체크리스트가 있다.

---

## 진행 요약

| 단계 | 내용 | 상태 |
| --- | --- | --- |
| ① | ComfyUI 파이프라인 검증 (설치·모델·워크플로·생성 테스트) | ✅ 완료 (2회차 7.2초, 목표 4~8초 달성) |
| ② | 그리기 캔버스 (이중 텍스처 + 도구 + PNG 저장) | ✅ 완료 (플레이 모드 실측 검증) |
| ③ | 전체 흐름 연결 (패널 상태머신 + ComfyUI 연동 + 시간 정책) | ✅ 완료 (실생성 포함 1사이클 완주) |
| ④ | 갤러리(Display 2) + QR(GCS) + VLM 필터 + opt-in | ⬜ 예정 (다음 작업) |
| ⑤ | 운영 안정화 (자동 시작·워치독·키오스크·관리자) + 스타일 확장·품질 튜닝 | ⬜ 예정 |

---

## 1. 완료된 작업

### 마일스톤 ① — AI 생성 파이프라인 검증

#### 설치 환경 (Unity 프로젝트 외부)

| 항목 | 내용 |
| --- | --- |
| ComfyUI | `C:\Users\HULIAC\ComfyUI` (v0.27.0, 자체 venv) |
| Python / PyTorch | 3.12.10 / 2.12.1+cu126 (RTX 3060 8GB CUDA 인식 확인) |
| 서버 주소 | `http://127.0.0.1:8188` |
| 실행 스크립트 | `Tools/run_comfyui.bat` — 부팅 자동 시작·워치독 재시작에 공용 사용 예정 |

#### 설치된 모델 (`ComfyUI/models/`)

| 종류 | 파일 | 크기 |
| --- | --- | --- |
| 체크포인트 | `checkpoints/Realistic_Vision_V5.1_fp16-no-ema.safetensors` | 2.0GB |
| ControlNet | `controlnet/control_v11p_sd15_scribble_fp16.safetensors` | 689MB |
| VAE | `vae/vae-ft-mse-840000-ema-pruned.safetensors` | 319MB |

#### 워크플로

- `Assets/StreamingAssets/ComfyUI/car_workflow_api.json` — Unity가 그대로 `/prompt`에 제출하는 API 형식
- 구조: ColorLayer→VAE Encode(img2img) + LineLayer→**ImageInvert**→ControlNet Scribble → KSampler(denoise 0.7, 25스텝, dpmpp_2m/karras) → SaveImage
- ※ ImageInvert가 들어간 이유: Scribble ControlNet은 "검정 배경 + 흰 선" 입력을 기대하는데 캔버스는 흰 배경 + 검정 선이므로 반전 필요

#### 생성 테스트 결과 (2026-07-08 실측)

- 테스트 입력: 낙서풍 자동차 스케치 2장 (선화 + 컬러, 768×512)
- 1회차 11.0초 (모델 로딩 포함) / **2회차 7.2초 → 목표 4~8초 달성**
- 품질: 스케치의 형태(측면 뷰·창문·바퀴 위치)와 색(빨간 차체·하늘색 창문)이 결과에 정확히 반영됨 → 이중 텍스처 설계 검증 완료

### 마일스톤 ② — 그리기 캔버스

#### 스크립트 (Assets/Scripts)

| 파일 | 역할 |
| --- | --- |
| `Drawing/DrawingCanvas.cs` | 핵심. 이중 RenderTexture(선/색 레이어, 768×512), GL 스탬핑(두꺼운 선+원형 캡), 스트로크 단위 undo(최대 10), 지우개(양 레이어 흰색, 2.5배 반경), 전체 지우기 |
| `Drawing/CanvasExporter.cs` | RenderTexture → Texture2D/PNG 변환. undo 스냅샷과 ComfyUI 업로드 공용 |
| `Drawing/CanvasMouseInput.cs` | RawImage 위 마우스 이벤트 → UV 변환 → DrawingCanvas 전달. 색 레이어를 화면에 표시 |
| `UI/DrawingPanelController.cs` | 그리기 화면 도구 UI. 팔레트 10색·펜 굵기 3단계·지우개·되돌리기·전체 지우기·완성 버튼을 런타임 생성 (디자인 리소스 적용 전 임시), 완성 시 PNG 2장 저장 |
| `Results/SessionStore.cs` | 저장 폴더 관리 (계획서 9장): `Sessions/`(전체 기록)·`Gallery/`(전시)·`Quarantine/`(격리), 세션 ID 발급, 스케치 쌍/결과 저장 |

#### 씬 구성 (`Assets/Scenes/AI자동차드로잉체험.unity`)

```
AI자동차드로잉체험.unity
├── Main Camera
├── EventSystem
├── DrawingSystem          ← DrawingCanvas (RenderTexture 보유)
└── KioskCanvas            ← Overlay, 1920×1080 스케일
    └── DrawingPanel       ← DrawingPanelController + 배경
        ├── Title          "자동차를 그려보세요!"
        ├── CanvasArea     RawImage 1152×768 + CanvasMouseInput
        ├── PaletteContainer  (팔레트 버튼 런타임 생성)
        └── ToolbarContainer  (도구 버튼 런타임 생성)
```

#### 검증 결과 (플레이 모드 실측)

- 프로그램 스트로크 + 마우스 직접 입력 모두 정상 동작
- undo 검증: 취소한 스트로크가 저장본에 없음 확인
- 저장 확인: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Practice01\Sessions\`
  - `<세션ID>_line.png` — 색과 무관하게 전부 검정 선만 (ControlNet 입력)
  - `<세션ID>_sketch.png` — 색 포함 그림 (img2img 입력)

### 마일스톤 ③ — 전체 흐름 연결 (2026-07-09)

#### 신규 스크립트 (Assets/Scripts)

| 파일 | 역할 |
| --- | --- |
| `Core/AppFlowManager.cs` | 패널 상태머신 (대기→그리기→스타일→생성→결과→복귀). 세션 저장·생성 요청 조율, 시간 정책 집행 |
| `Core/ConfigManager.cs` | `Data/Config.json` 로드 (ComfyUI 주소·시간 정책·GCS·필터). 파일 없어도 기본값으로 동작 |
| `Core/TextLibrary.cs` | `Data/Texts.json` 화면 문구 사전. 키 없으면 키 자체 반환 (빈 화면 방지) |
| `Core/LogManager.cs` | `Logs/yyyyMMdd.log` 파일 로그 + Unity Error/Exception 자동 수집 훅 |
| `Core/IdleWatcher.cs` | 무입력 시간 측정 (마우스 이동·클릭·키) |
| `Generation/StyleLibrary.cs` | `Data/Styles.json` 스타일 프리셋 로드 (v1 실사 1종, 폴백 내장) |
| `Generation/ComfyUIClient.cs` | 업로드 → 워크플로 치환 제출(검증 오류 재시도 1회) → 0.5초 폴링 → 결과 다운로드. 실패는 콜백으로만 보고 |
| `UI/UiBuilder.cs` | 런타임 uGUI 생성 공용 헬퍼 (패널들이 공유) |
| `UI/AttractPanelController.cs` | 대기 화면 (전체 화면 클릭 시작, 문구 깜빡임) |
| `UI/StylePanelController.cs` | 스타일 선택. 씬 배치 4슬롯(VerticalGroup) 방식으로 전환됨 (아래 「StylePanel 씬 배치 전환」 참조) |
| `UI/GeneratingPanelController.cs` | 생성 중 연출 (스케치 + 말줄임표) / 실패 사과 문구 |
| `UI/ResultPanelController.cs` | 스케치 vs 완성본 비교 + [다시 그리기]. QR·전시 버튼은 ④에서 |

기존 변경: `DrawingPanelController`에 CompleteRequested/ContinueRequested 이벤트, 방치 팝업, 도구 문구 Texts.json화. 데이터 파일 3종 신설 (`StreamingAssets/Data/Config.json`, `Styles.json`, `Texts.json`).

#### 씬 구성 변경

```
KioskCanvas
├── DrawingPanel (기존)
├── AttractPanel / GeneratingPanel / ResultPanel  ← 신규 (UI는 런타임 생성)
├── StylePanel  ← 신규, UI는 씬 배치(TMP) — 런타임 생성 아님
AppFlow  ← 신규 (AppFlowManager + IdleWatcher + ComfyUIClient, 참조 인스펙터 배선 완료)
```

#### 검증 결과 (2026-07-09 플레이 모드 실측)

- 전체 사이클 완주: 대기 클릭 → 그리기(캔버스 초기화 확인) → [완성!] → 스타일 미리보기 → [실사] → 생성(워밍업 후 정상 완료) → 결과 비교 표시 → [다시 그리기](그림 유지 확인)
- 결과 품질: 스케치의 측면 뷰·창문·바퀴 위치와 빨간 차체·하늘색 창문이 결과에 정확히 반영
- 저장 확인: `Sessions/`에 `_line.png`(9KB) / `_sketch.png`(9KB) / `_result.png`(177KB) 3종
- 방치 정책 실동작: 그리기 90초 무입력 → 팝업 → 30초 무응답 → 대기 복귀 확인
- 실패 경로 실동작: 콜드 부팅 첫 생성 30초 타임아웃 → 사과 문구 → 대기 복귀, 앱 계속 동작

### 마일스톤 ③ 후속 — StylePanel 씬 배치 전환 (2026-07-09)

스타일 화면 UI를 런타임 생성에서 **씬 배치 4슬롯** 방식으로 전환 (DrawingPanel과 같은 방향).

- 씬 구조: `StylePanel > BG/Title/GridGroup`, GridGroup 아래 `VerticalGroup ×4`, 각 슬롯 = `ExampleImg(Image) + SelcetBtn(Button > Text(TMP))`
- `StylePanelController` 재작성: `styleGroups`(VerticalGroup 4개)를 Styles.json 순서대로 연결, 각 슬롯의 예시 이미지·버튼·라벨을 이름/타입으로 찾아 채움. 스타일 수 < 슬롯 수면 남는 슬롯 자동 숨김
- 데이터: Styles.json 4종(실사/카툰/픽셀아트/채색, thumbnail=`StyleExamples/<id>.png`), Texts.json에 `style.<id>` 라벨 키 추가. 실사 예시 이미지는 이전 생성 결과를 재활용해 배치
- **검증(플레이 실측)**: 제목·라벨 4종 정상 주입, 실사 슬롯 예시 이미지 표시, 실사 클릭 → Generating 전환 확인 (스크린샷 확인)
- **주의**: TMP 라벨은 Awake 아닌 Start에서 설정해야 반영됨(인수인계 §6), 예시 이미지는 Image.color=white로 되돌려야 안 검게 나옴

### 스타일 3종 프롬프트 튜닝 + 생성 검증 (2026-07-10)

- `Styles.json` 카툰/픽셀아트/클레이 프롬프트를 실사와 같은 구조로 강화: `(… single complete car:1.4)` + `full car body` + `(four round wheels touching the ground:0.7)` + 스타일 키워드, 부정 프롬프트에 기형 억제 세트 + `photorealistic` 차단, denoise 0.8 통일
- 「클레이」(id `simplecolor`)는 이름과 달리 플랫 벡터 프롬프트였던 것을 claymation·plasticine·스톱모션 프롬프트로 교체. "handcrafted"가 점토 손·받침대를 소환하는 문제를 발견해 제거 (인수인계 §6 함정 추가)
- 워크플로 파일의 ControlNet strength가 0.6으로 남아 있어(문서 채택값 0.35와 불일치 — 재발) **0.35로 재반영**. 0.6에서는 엉성한 낙서 입력 시 실사조차 범퍼 클로즈업으로 붕괴함을 실측
- **검증 (ComfyUI API 직접 생성)**: ①색칠 자동차 낙서 — 4종 모두 온전한 단일 차 + 스타일 반영 + 색 유지(빨간 차체·하늘색 창문), 2회차 이후 6~7초 ②극단적 낙서(찌그러진 바퀴·안 닫힌 차체·잡선 포함) — 4종 모두 정상 차, 잡선은 무시됨(카툰만 잡선이 배경 구름 캐릭터로 변하는 애교 수준 아티팩트). 차 형태 자체가 없는 입력(가로선 하나 등)만 시드 편차가 남음. **플레이 모드 통합 확인은 미실시** (다음 플레이 때 스타일 4종 각 1회 생성 권장)

### 스타일 색 적용 문제 수정 + 시점별 매트릭스 검증 (2026-07-10 오후)

카툰·픽셀 결과가 그림의 색을 무시하는 문제 보고 → 시점 4종(옆·정면·대각·모서리) × 스타일 4종 × 3회 = 라운드당 48장 매트릭스로 진단·수정 (이번엔 실제 앱과 동일하게 **색칠 획도 선 레이어에 검정 포함**).

- **원인**: ①denoise 0.8이 색 레이어 정보를 대부분 재해석 ②픽셀 프롬프트의 "limited color palette"(색 재배정 조장)·카툰의 "vibrant flat colors"(임의 팔레트 유도)
- **수정 (Styles.json)**: 카툰·픽셀 denoise 0.8→**0.7** + 색 유도 단어를 "colors matching the drawing"으로 교체, 4종 부정에 multiple cars, 실사 부정에 poster/advertisement/magazine layout 추가
- **수정 후**: 옆(빨강)·정면(노랑) 카툰·픽셀 3/3 색 유지, 대각(초록)도 계열 유지. 실사·클레이는 이전과 동일한 수준(색 드리프트는 denoise 0.8의 확정 트레이드오프)
- **남은 취약 케이스 (수정 불가로 문서화)**: ①**모서리에 작게 그린 그림** — 실사 색 무시, 카툰 빈 공간에 미완성 낙서 덩어리, 클레이 멀티카(부정 프롬프트로도 못 막음). 픽셀만 완벽. → 그리기 화면에 "크게 그려주세요" 안내 문구 검토 ②**정면 그림 + 실사** — 탑뷰·이상 구도로 해석 잦음 (카툰·픽셀·클레이는 정면 정상)

### 픽셀 프롬프트 방향 전환 + 반절림 그림 검증 (2026-07-10 오후 2차)

- **픽셀 "깨져 보임" 수정**: 16-bit crisp 방향에서 **32-bit 소프트 방향으로 전환** (사용자 제시안 채택: soft pixel shading, smooth color transitions, subtle anti-aliasing, 32-bit style). 부정 프롬프트에서 상충하는 smooth gradient/antialiased를 제거하고 노이즈 계열(noisy, dithering, glitch, mosaic…) 차단 추가. 옆·정면·대각 재검증에서 노이즈 감소 + 색 유지 확인
- **픽셀 최종안 (같은 날 재교체)**: 사용자 제시로 **모던 인디게임 방향**(Eastward/CrossCode style, clean pixel clusters, fine pixel detail, light dithering…)으로 재전환 — 스프라이트 품질이 역대 최고. 단독으로는 색이 옅어져(정면 노랑→크림) "vibrant colors matching the drawing"을 추가해 보완 (A/B 검증). `light dithering` 채택에 따라 부정의 `dithering`도 제거. 잔여 한계: 노랑 계열은 여전히 옅어질 수 있음
- **픽셀 확정판 (품질 강조 버전)**: 사용자 제시안으로 최종 교체 — (masterpiece/best quality 앵커) + 정확한 차 비례·클린 실루엣·4-6 color ramp shading·colored outlines 등 품질 서술 중심. 검증(옆·정면·대각×3): 빨강 3/3·초록 3/3(직전 안보다 개선)·노랑 1~2/3(공통 약점). 색 문구 없이도 색 유지가 준수해 이대로 확정
- **「카툰/실사 생성 후 후처리 픽셀화」 대안 검토 (2026-07-10, 기각)**: 다운스케일+팔레트 양자화+니어리스트 확대로 비교한 결과 — 실사→픽셀화는 흐린 사진 축소판(최하), 카툰→픽셀화는 색 보존은 완벽하나 외곽선이 바스러져 "줄어든 일러스트"로 보임. SD 직접 생성이 검은 외곽선·픽셀 클러스터 등 픽셀아트 정체성이 뚜렷해 **현행(직접 생성) 유지**. 색 충실도가 최우선이 되면 카툰→픽셀화가 플랜 B (결정적·균일하며 노랑도 정확히 보존)
- **반절림 그림 검증** (가장자리 반절림·모서리 대각 반절림 × 4스타일 × 3회): ①실사 — 가장자리는 반쪽 차 사진으로 자연스럽게 재현, 모서리 대각은 클로즈업으로 새기도 함, 색은 대체로 무시 ②카툰 — 색은 유지하나 빈 공간에 미완성 유령차 낙서 동반 잦음(2/3) ③픽셀 — **가장자리 반절림에서 파편화**(스프라이트 조각), 모서리 대각은 오히려 3/3 안정 ④클레이 — 잘린 차를 완성차로 채워버림, 색 이탈. 종합: 반절림은 프롬프트로 못 잡는 구조적 한계 → 운영 대응(그리기 안내 문구 "차 전체를 화면 안에 그려주세요") 권장

---

## 2. 알려진 이슈 / 메모

| 항목 | 내용 | 대응 |
| --- | --- | --- |
| ComfyUI 첫 제출 검증 오류 | 업로드 직후 첫 `/prompt` 제출에서 `Invalid image file` 1회 발생, 재시도로 해결 | ✅ 해소 — ComfyUIClient에 제출 재시도 1회 내장 (Config `submitMaxRetries`) |
| ~~첫 생성이 타임아웃(첫 이미지 안 나옴)~~ **해결됨(2026-07-09)** | 원인 2개: ①콜드 모델 로딩 31.66초 > 타임아웃 30초, ②폴링 캐싱으로 첫 생성 완료 미감지. **대응 완료**: `ComfyUIClient.Warmup()`(시작 시 예열, 3회 재시도) + PollHistory 캐시버스터·no-cache + 타임아웃 45초. 콜드 재시작 실측으로 워밍업 첫 시도 완료 + 실제 생성 Result 도달 확인. 상세 인수인계 §6 |
| 결과 화면 미완 | QR 코드·[전시장에 내 작품 걸기] 버튼 없음 | ④에서 ResultPanel에 추가 |
| ~~스타일 예시 이미지 3종 없음~~ 해소 | `StyleExamples/`에 4종 PNG 배치됨 (cartoon/pixelart/clayStyle/realistic) | ✅ 해소 — 프롬프트 확정본으로 재생성해 교체하면 더 정확 (선택) |
| ~~스타일 3종 프롬프트 임시값~~ **해소(2026-07-10)** | 카툰/픽셀아트/클레이 프롬프트·denoise 미검증 상태였음 | ✅ 해소 — 실사 구조로 강화 + ComfyUI API 직접 생성으로 4종 검증 (§1). 플레이 모드 통합 확인만 남음 |
| 엉성한 그림 아티팩트 → **재발·재수정(2026-07-09 저녁)** | 바퀴 이상/없음·문 이상·차 모양 붕괴 재발. **원인: ControlNet 0.35/0.6 튜닝이 문서에만 있고 워크플로 파일엔 0.9/1.0으로 남아 있었음** + 실사 프롬프트의 "isometric view of a boxy car"(낙서 시점과 충돌) + 부정 프롬프트 "floor, shadow"(바퀴 접지 삭제) | 대응 완료: 워크플로 노드 9에 **strength 0.35 / end_percent 0.6 실반영**, 실사 프롬프트에서 isometric 제거·four wheels 강조·부정 프롬프트에 바퀴 이상 계열 추가, **실사 denoise 0.8**(우선순위 변경: 모양 정상 > 색 보존, 장난감풍 허용). 플레이 모드 재검증 필요 (인수인계 §5). **2026-07-10 또 재발 발견**: 파일에 strength 0.6으로 되돌아가 있어 0.35 재반영 (인수인계 §5 경고 참조) |
| 도구 UI 임시 생성 | 팔레트/버튼이 런타임 코드 생성 (기본 uGUI 모양) | 디자인 리소스 확보 후 프리팹 교체 |
| 프로젝트 정리 | 이전 프로젝트(지구환경코딩) 스크립트·씬 삭제가 워킹 트리에 미커밋 상태 | clean slate 커밋으로 정리 필요 |

---

## 3. 다음 작업 (마일스톤 ④ — 갤러리 + QR + 필터)

1. `Gallery/GallerySlideshow.cs` — Display 2 전용 카메라 + Screen Space-Camera 캔버스, `Gallery/` 폴더 감시 슬라이드쇼 (계획서 5장)
2. `Results/GcsUploader.cs` — `IResultUploader` 인터페이스 + GCS 구현 (실패해도 체험 계속), GCS 버킷·서비스 계정 생성 선행 (인수인계 §7)
3. `Results/QrCodeView.cs` — 업로드 URL의 QR 생성·표시, 오프라인 시 자동 숨김
4. `Results/ContentFilter.cs` — VLM 필터 (CPU 비동기), 모델 선정 선행 (Moondream2 / Qwen2-VL-2B급)
5. ResultPanel에 QR 영역 + [전시장에 내 작품 걸기] 버튼 추가, opt-in → 필터 → Gallery/Quarantine 흐름
6. AttractPanel에 미니 슬라이드쇼 추가 (계획서 5장)
7. 검증: 더미/실데이터로 3경로(표시·QR·갤러리) 모두 동작

---

## 4. 실행 방법 (개발용)

1. ComfyUI 서버: `Tools\run_comfyui.bat` 실행 (이미 켜져 있으면 생략. 기동 후 준비까지 ~90초)
2. Unity에서 `Assets/Scenes/AI자동차드로잉체험.unity` 열고 플레이 → 대기 화면이 뜬다
3. 화면 클릭 → 그리기 → [완성!] → 스타일 선택 → 4~8초 후 결과 화면 (콜드 부팅 첫 생성은 타임아웃될 수 있음 — 한 번 더 시도)
4. 산출물: `AppData\LocalLow\DefaultCompany\Practice01\Sessions\`에 `_line`/`_sketch`/`_result` PNG 3장
5. 서버를 안 켜고 플레이하면 생성 실패 → 사과 문구 → 대기 복귀로 동작해야 정상 (예외로 죽지 않기)

---

## 5. 문서 갱신 절차 (작업 세션 종료 시)

작업을 마칠 때마다 아래 순서로 세 문서를 점검한다. **원칙: 같은 내용을 두 문서에 쓰지 않는다** — 상태는 현황에만, 설계는 계획서에만, 환경·함정·절차는 인수인계에만. 중복되는 순간부터 어긋나기 시작한다.

### 5-1. 본 문서 (매번, 필수)

- [ ] 「진행 요약」 표의 마일스톤 상태 갱신 (✅/🔶 진행 중/⬜)
- [ ] §1에 완료한 작업 추가 (스크립트는 역할 표, 검증은 실측 결과)
- [ ] §2에 새로 발견한 이슈 추가, 해소된 이슈는 대응란에 해소 기록
- [ ] §3 「다음 작업」을 실제 다음 착수 목록으로 재작성
- [ ] §4 실행 방법이 달라졌으면 갱신
- [ ] 상단 「최종 갱신」 날짜 변경

### 5-2. 작업계획서 (설계가 바뀐 경우에만)

- [ ] 바뀐 결정이 있으면 해당 장 본문 수정
- [ ] 계획서 §18 「변경 이력」에 한 줄 기록 (날짜 | 장 | 무엇을 왜)

### 5-3. 인수인계 (해당 사항이 생긴 경우에만)

- [ ] 새 함정 발견 → 인수인계 §6에 추가
- [ ] 환경·설치·경로·계정 변경 → §2·3·7 갱신
- [ ] 미결 항목 해소/추가 → §8 체크리스트 갱신
- [ ] 새 스크립트 폴더·데이터 흐름 변화 → §4 갱신

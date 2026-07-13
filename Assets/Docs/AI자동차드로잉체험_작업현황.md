# AI 자동차 드로잉 체험 — 작업 현황

> 기준 문서: [작업계획서](AI자동차드로잉체험_작업계획서.md) · 환경/함정: [인수인계](AI자동차드로잉체험_인수인계.md)
> 최종 갱신: 2026-07-13
>
> **갱신 규칙**: 이 문서는 **모든 작업 세션이 끝날 때마다** 갱신한다. 절차는 맨 아래 [5. 문서 갱신 절차](#5-문서-갱신-절차-작업-세션-종료-시) — 세 문서를 함께 점검하는 체크리스트가 있다.

---

## 진행 요약

| 단계 | 내용 | 상태 |
| --- | --- | --- |
| ① | ComfyUI 파이프라인 검증 (설치·모델·워크플로·생성 테스트) | ✅ 완료 (2회차 7.2초, 목표 4~8초 달성) |
| ② | 그리기 캔버스 (이중 텍스처 + 도구 + PNG 저장) | ✅ 완료 (플레이 모드 실측 검증) |
| ③ | 전체 흐름 연결 (패널 상태머신 + ComfyUI 연동 + 시간 정책) | ✅ 완료 (실생성 포함 1사이클 완주) |
| ④ | 갤러리(Display 2) + QR(B2) + VLM 필터 + opt-in | 🔶 코드·씬·플레이 검증 완료 (B2 계정 개통 후 QR 실검증만 남음. VLM은 운영 결정 보류) |
| ⑤ | 운영 안정화 (자동 시작·워치독·키오스크·관리자) + 스타일 확장·품질 튜닝 | ⬜ 예정 |
| ⑥ | 결과 영상화 (AnimateDiff PoC → IVideoGenerator + 영상 재생·저장·업로드) | 🔶 코드·씬 구성 완료 (2026-07-13 — 플레이 검증·모션 튜닝·B2 영상 업로드 남음) |

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

### 마일스톤 ④ — 갤러리 + QR + 필터 (2026-07-10, 코드·씬 구성 완료)

#### 신규 스크립트 (Assets/Scripts/AIDrawing)

| 파일 | 역할 |
| --- | --- |
| `Results/QrEncoder.cs` | QR 인코더 자체 구현 (바이트 모드·오류정정 M·버전 1~10 자동, ISO/IEC 18004). 외부 라이브러리 의존 없음 |
| `Results/QrCodeView.cs` | QR 행렬 → Texture2D (Point 필터, 콰이어트존 4). 실패 시 null 반환 → QR만 숨김 |
| `Results/IResultUploader.cs` | 업로더 인터페이스 (계획서 9-2: 스토리지 교체 가능 구조) |
| `Results/GcsUploader.cs` | GCS 구현: 서비스 계정 JWT RS256 서명 → OAuth2 토큰(캐시) → 공개 버킷 업로드. 키·버킷 미설정이면 IsConfigured=false로 QR 전체 자동 비활성 |
| `Results/ContentFilter.cs` | 갤러리 게이트. OpenAI 호환 chat completions(Ollama 등)로 스케치+결과 2장 판정. 꺼짐→통과, 판정 불가→격리(보수적, 계획서 10장) |
| `Gallery/GallerySlideshow.cs` | Display 2 슬라이드쇼. Gallery 폴더 감시(복사만으로 반영), 빈 갤러리 안내 문구, 빌드에서 Display 2 Activate |

기존 변경: `ResultPanelController`에 QR 영역(업로드 완료 시에만 표시) + [전시장에 내 작품 걸기] 버튼(중복 신청 방지, 즉시 "신청 완료" 피드백). `AttractPanelController`에 미니 슬라이드쇼(갤러리 비면 자동 숨김). `AppFlowManager`에 업로드 시작(세션 ID 가드로 늦은 콜백 차단)·opt-in→필터→Gallery/Quarantine 흐름. `ConfigManager`에 gcs.objectPrefix·filter.question 필드. Config.json에 gcs/filter/gallery 섹션, Texts.json에 QR·전시·갤러리 문구 6종.

#### 씬 구성 변경

```
GalleryCamera   ← 신규. Display 2(targetDisplay 1), SolidColor 검정, cullingMask UI만, y=-1000(본 씬과 분리)
GalleryCanvas   ← 신규. Screen Space-Camera(GalleryCamera 연결), 1920×1080 스케일, UI 레이어, GallerySlideshow
AppFlow         ← GcsUploader + ContentFilter 컴포넌트 추가 (AppFlowManager가 FindObjectOfType 폴백으로 연결)
```

#### 검증 결과 (2026-07-10)

- **QR 인코더 대조 검증 완료**: 강제 마스크 0~7 × 입력 4종(버전 1·5·9·10 — 16비트 카운트 경로 포함) 총 32개 행렬을 레퍼런스 구현(python-qrcode)과 MD5 대조 → **전부 일치**. 자동 마스크 선택도 2/4 동일(불일치 2건은 벌점 동점 처리 차이로 둘 다 유효한 QR)
- **플레이 모드 검증 완료 (사용자 실측, 2026-07-10)**: 그리기→생성→결과→갤러리 신청→Display 2·대기 화면 슬라이드쇼 반영까지 문제없음. QR 자동 숨김(미설정) 확인

### 마일스톤 ④ 후속 — QR 스토리지 GCS → Backblaze B2 전환 (2026-07-10)

- 무료 클라우드 비교(웹 확인) 결과 **B2 채택**: 카드 등록 불필요 + 10GB 영구 무료 + 공개 URL 기본 제공. R2는 카드 필요 가능성·공개 도메인 제약, Supabase는 7일 무활동 일시정지로 무인 전시 부적합
- `Results/B2Uploader.cs` 신규 (authorize → upload URL → 업로드 → 다운로드 토큰 → URL, 인증 24h 캐시 + 만료 401 재시도). `IResultUploader` 덕에 흐름 변화 없음
- **버킷은 비공개 운영**: B2가 공개 버킷에 카드 등록을 요구함을 실측 → QR 링크에 만료형 다운로드 토큰(7일, `b2.downloadAuthSeconds`)을 붙이는 방식으로 카드 없이 해결 (계획서 9-2 변경 이력)
- **QR = 저장 버튼이 있는 랜딩 페이지**: PNG+HTML을 같은 접두사로 업로드, 토큰 1개로 커버. 템플릿 `StreamingAssets/Data/QrLanding.html`. **B2 계정 개통 + CLI 종단 검증 완료 (2026-07-10)**: 버킷 `Practice01` 생성, 버킷 제한 키 발급·`Config/b2-key.json` 배치, 인증→PNG→토큰→HTML→랜딩 페이지/이미지 다운로드 모두 HTTP 200, QR URL 197자(인코더 한도 213바이트 내). 플레이 모드 QR 표시·폰 스캔 확인만 남음
- AppFlowManager가 **B2 우선, GCS 폴백**으로 업로더 선택 (`ActiveUploader`). Config.json에 `b2` 섹션, 씬 AppFlow에 B2Uploader 추가
- 같은 날 ComfyUI 기동 문제 해결: `run_comfyui.bat`이 UTF-8 한국어 주석 때문에 cmd(cp949)에서 통째로 깨지던 것 — **ASCII 전용 재작성** + 출력을 `ComfyUI\comfyui_run.log`로 기록 (인수인계 §6 함정 추가). Desktop 앱 불필요 확인

### 마일스톤 ⑥ — 결과 영상화 PoC (2026-07-13, 통과)

기존 결과 이미지를 AnimateDiff(img2vid)로 움직이는 영상으로 만드는 로컬 PoC. **판정 기준(생성 시간·차 형태 유지) 통과** — Unity 본작업 착수 가능.

#### 설치 (ComfyUI 쪽, 인수인계 §3에 재설치 절차 반영)

- 커스텀 노드: `ComfyUI-AnimateDiff-Evolved` + `ComfyUI-VideoHelperSuite` (venv에 opencv-python·imageio-ffmpeg 추가)
- 모션 모듈: **`mm_sd15_v2.ckpt` 채택** (1.7GB). AnimateLCM 계열은 기각 (아래)

#### 실측 결과 (RTX 3060 8GB, 512×344, 웜 기준)

| 항목 | 값 |
| --- | --- |
| 2초 (16프레임 @ 8fps) | **~40초** (콜드 +10~15초) |
| 3초 (24프레임 @ 8fps) | **~52초** (선형 스케일, OOM 없음) |
| 관람객 체감 합계 (이미지 7~10초 + 영상) | **약 50~60초** |
| 품질 | 실사 크리스프, 색(빨강) 유지, 배경 깨끗, 온전한 차 형태 |

채택 파라미터: mm_sd15_v2 + beta_schedule `sqrt_linear (AnimateDiff)` + dpmpp_2m/karras 20스텝 cfg 7.0 + **denoise 0.35** + RepeatLatentBatch(16~24) + VHS_VideoCombine(h264-mp4). 선화 ControlNet(0.35/0.6) 병행 가능 확인(E안). PoC 워크플로·비교 산출물은 ComfyUI `output/poc_*`.

#### 기각·함정 (상세는 인수인계 §6)

- **AnimateLCM(+LoRA, lcm 샘플러) 기각**: 전 조합(beta 2종·denoise 0.35~0.55·512/768)에서 유화처럼 질감 붕괴 + 차종 변형. 표준 v2 + 일반 샘플러로 교체하니 즉시 해결
- **이미지·영상 워크플로가 같은 노드 ID를 쓰면 교차 오염**: 초기 워크플로(둘 다 체크포인트가 노드 "1")에서 AnimateDiff 실행 후 이미지 생성이 노이즈로 붕괴. 영상 워크플로 노드 ID를 101번대로 분리 후 이미지→영상→이미지→영상 4연속 교대 실행에서 이미지 안정 확인

#### 남은 튜닝 과제 (본작업에서, 단 "봐줄 만한 수준"까지만 — 계획서 §18 매몰 방지 원칙)

1. **움직임이 미묘함** (바퀴·그림자 수준의 idle 모션) — denoise↑는 형태 표류와 상충, motion LoRA·프롬프트 실험 여지
2. **차 디자인 표류** — denoise 0.35에서도 입력(모던 쿠페)과 다른 클래식카로 변형됨(모션 모듈의 재해석). 결과 화면에 "이미지와 다른 차가 움직이는" UX 불일치 → 선화 ControlNet 강도 조정으로 완화 시도
3. 바닥 전선 아티팩트 등 소소한 부산물

### 마일스톤 ⑥ — Unity 본작업: 영상 생성·재생 연동 (2026-07-13, 코드·씬 구성 완료)

#### 신규 스크립트 (Assets/Scripts/AIDrawing)

| 파일 | 역할 |
| --- | --- |
| `Generation/IVideoGenerator.cs` | 영상 생성기 계약 — 로컬↔외부 API 교체 가능 구조 (IResultUploader 패턴, 계획서 §18) |
| `Generation/ComfyUIVideoGenerator.cs` | 로컬 구현: 결과 이미지+선화 업로드 → 영상 워크플로(노드 101번대) 치환 제출 → 1초 폴링 → mp4 다운로드. VHS 출력은 history의 `gifs` 키에 실림(images 아님) |

#### 기존 변경

- `ConfigManager`: `VideoConfig`(enabled/workflowPath/generateTimeoutSeconds 120) + Config.json `video` 섹션
- `SessionStore`: `SaveResultVideo`·`ResultVideoPath` (`Sessions/<세션>_result.mp4`)
- `ResultPanelController`: VideoPlayer(APIOnly, 루프)로 결과 RawImage를 이미지→영상 교체. 대기 캐시 패턴 유지(활성화 전 호출 안전), 재생 실패 시 이미지 복귀, 패널 비활성화 시 정지. "잠시 후 그림이 움직이기 시작해요!" 안내 라벨(Texts `result.videoPending`)
- `AppFlowManager`: 결과 화면 진입 직후 백그라운드로 영상 생성 시작 → 도착 시 `ShowVideo` + **자동 복귀 타이머 리셋**(영상 감상 시간 확보) → 실패·타임아웃·세션 전환 시 이미지 유지(폴백). 세션 ID 가드로 늦은 콜백 차단
- 씬: AppFlow에 `ComfyUIVideoGenerator` 컴포넌트 추가 (FindObjectOfType 폴백도 있음)
- 같은 날 버그 수정: 팔레트 색 클릭 시 지우개 버튼이 활성색(주황)으로 물들던 문제 — `SetEraser(false)`가 무조건 주황을 칠하던 것을 on/off 분기로 수정 (`DrawingPanelController`)

#### 체험 흐름 (변경 후)

생성(이미지, 7~10초) → 결과 화면(이미지 비교, 지금과 동일) → 백그라운드 영상 생성(~40초) → 도착하면 우측 이미지가 움직이는 영상으로 교체(루프 재생). 영상이 실패해도 관람객은 이미지 결과를 그대로 본다 — 실패를 모른다.

#### 미실시 (다음 세션)

- **플레이 모드 통합 검증** (사용자 실측 필요): 이미지→영상 교체, 안내 라벨, 다시 그리기/대기 복귀 시 정지, 서버 꺼짐 폴백
- B2 업로드는 여전히 PNG만 (QR 랜딩에 영상 미포함), 갤러리 슬라이드쇼도 이미지만 — 후속 결정
- 모션 튜닝 (PoC 과제: 움직임 미묘·디자인 표류)

### 영상 모션 강화 + 세션 만료 수정 (2026-07-13 오후)

- **영상 도착 전 세션 만료 수정**: §2 이슈 표 참조 (`_videoInProgress` 자동 복귀 보류)
- **"바퀴만 움직임" 대응 — 1차(프롬프트·denoise·motion_scale)**: denoise 0.35→0.45 + ControlNet end 0.6→0.35 + motion_scale 1.3 + 범용 동적 프롬프트 + static 네거티브. 잔여 죽은 노드(AnimateLCM LoRA 103) 삭제. **→ 프레임 실측 결과 여전히 정지 수준(모션지표 1.6)** — 프롬프트·denoise·motion_scale로는 움직임이 거의 안 생김을 확인
- **"바퀴만 움직임" 대응 — 2차(카메라 모션 LoRA, 확정)**: `v2_lora_PanLeft.ckpt`(77MB, 인수인계 §3) 다운로드 → 워크플로 노드 **120 `ADE_AnimateDiffLoRALoader`(강도 0.8)** 추가 → 노드 104 `motion_lora` 연결. 카메라가 차를 따라 팬해서 **명확히 움직임**(A/B/C + denoise 비교 프레임 실측, 몽타주로 검증). 차 형태 유지됨. denoise는 사용자가 0.6으로 조정(움직임 소폭↑, 원본 대비 표류도↑ — 0.45는 표류 작음). 더 큰 움직임은 LoRA 강도 0.8→1.0이 레버. **플레이 재검증 필요**

### 스타일 품질 개편 — 카툰 전용 체크포인트 + 픽셀 그리드 스냅 (2026-07-13 오후)

카툰 품질 불만("이미지 생성부터 별로")에 대응. 원인 = Realistic Vision은 실사 특화라 카툰 화풍의 상한이 낮음 (클레이가 잘 나오는 건 점토가 사진 피사체라서 — 인수인계 §6).

- **카툰 = ToonYou 전용 체크포인트로 분리** (`toonyou_beta6.safetensors` 2.3GB 설치, 인수인계 §3):
  - ComfyUI API 직접 생성으로 후보 12종(A~L: 프롬프트 3계열 × denoise 0.6~0.8 × CN 0.35~0.6 × 시드 2종 × 무채색/색칠 입력) 비교 → **사용자가 D안 룩 선택** → 확정: denoise 0.75 + CN 0.5 + booru 태그 프롬프트(no humans, vehicle focus) + 가중 네거티브(운전자·의인화·구름 억제)
  - 색칠 입력 검증(L): 관람객의 빨강·하늘색 유지 + 선명한 셀 셰이딩 + 바닥 접지 — 전시 수준
  - 코드: `StylePreset`에 `checkpoint`·`controlnetStrength` 필드, `ComfyUIClient`가 노드 1·9에 조건 치환 (계획서 §8)
- **픽셀아트 = 그리드 스냅 후처리 추가** (`Generation/PixelArtFilter.cs` 신규):
  - AI 생성(기존 확정 프롬프트 유지) 결과를 블록 평균 축소(128px) + 채널 6단계 포스터라이즈 + 니어리스트 확대로 진짜 픽셀 그리드에 스냅. 2026-07-10 기각안("생성을 필터로 대체")과 달리 생성은 유지 — 기각 사유였던 외곽선 바스러짐 없음. 파라미터는 파이썬 시뮬레이션으로 128/6(정밀)·96/5(청키) 비교 후 128/6 채택 (Styles.json `pixelateWidth`/`pixelateColorLevels`, 0이면 비활성)
  - `AppFlowManager`가 선택 스타일을 보관(`_chosenStyle`)하고 생성 성공 직후 적용 — 표시·저장·QR 업로드·영상 입력이 모두 후처리본 공유. 필터 실패 시 원본 사용(무인 운영)
- 컴파일 확인 완료 (assets-refresh + 콘솔 에러 0). **플레이 모드 검증 미실시**

### 4스타일 전 파이프라인(이미지→영상) 검증 + 픽셀 영상 수정 (2026-07-13 오후)

공통 색칠 낙서 1개(빨강 차체·파랑 캐빈)로 실사·카툰·픽셀·클레이를 이미지→영상까지 ComfyUI API로 재현(ComfyUIClient·PixelArtFilter 로직 그대로). 프레임 몽타주로 눈검증.

- **이미지**: 실사·클레이는 온전한 차 + 스타일 반영(단 denoise 0.8이라 색 드리프트 + 낙서의 잔선/분리된 바퀴가 바닥 부유물로 나옴 — 알려진 구조적 한계), 카툰·픽셀은 색 유지 + 스타일 우수
- **영상**: 실사 ✅(배경 방사 블러+바퀴 회전+카메라 팬, 모션지표 4.6)/클레이 ✅(팬+굴러가는 클레이 공, 3.1)/카툰 🔶(스타일은 유지되나 평평한 면이라 팬이 약함, 1.7)/**픽셀 ❌ → 수정**
- **픽셀 영상 문제·수정**: 영상 워크플로(RV 모델, denoise 0.6)가 픽셀 스프라이트를 **매끈한 반실사 차로 다시 그려 픽셀감 소실**. 검증(P1 denoise 0.3만 → 모션 0.28로 거의 정지 / P3 denoise 0.55 + 프레임 재픽셀화 → 모션 3.1 + 픽셀 그리드 복원 ✅). **채택=P3 방식**: 영상 프레임을 니어리스트 축소(128)→확대로 재픽셀화. `ComfyUIVideoGenerator`가 `pixelateWidth>0`이면 워크플로에 ImageScale 노드 121·122 주입하고 VHS 입력을 그쪽으로 연결(`IVideoGenerator.Generate`에 `pixelateWidth` 인자 추가, AppFlowManager가 `_chosenStyle.pixelateWidth` 전달). 낮은 denoise로는 움직임이 죽어 안 됨을 확인 — 재픽셀화가 정답
- 컴파일 확인 완료 (에러 0). **플레이 모드 검증 미실시** — 특히 픽셀 영상이 움직이는 스프라이트로 나오는지

### 픽셀아트 LoRA 방식 전환 (2026-07-13 저녁) — 후처리 방식 대체

위 "그리드 스냅 후처리(128/6)"가 **모자이크처럼 뭉개져 보이는 문제** + 영상 품질 저하 보고. 원인 = RV+프롬프트의 픽셀 출력이 흐릿한데 128px 후처리가 그 위에 더 잘게 재샘플링해 뭉갬. **사용자 제안대로 체크포인트/LoRA 방식 채택** — 카툰(ToonYou)과 같은 계열.

- **픽셀 전용 LoRA 도입**: `PixelArtRedmond15V.safetensors`(27MB, artificialguybr, 인수인계 §3). 트리거 `Pixel Art, PixArFK`. RV 위에 얹어 생성 → **선명한 정품 픽셀 스프라이트**(강도 0.8이 1.0보다 주변 잡픽셀 적어 채택). 후처리는 **160px/8단계로 완화**(LoRA가 디테일을 만드니 그리드만 균일하게 스냅, 모자이크 사라짐)
- **영상에도 같은 LoRA 주입**: 영상 워크플로(RV)에 LoRA 없으면 화풍을 잃고 매끈해짐 → 노드 123에 LoraLoader 주입 + 트리거 prepend + 재픽셀화(160). 검증(V1 LoRA無=거친 모자이크 영상 / V2 LoRA有=선명한 움직이는 픽셀 스프라이트, 모션 4.2 ✅)
- **코드 일반화**: `StylePreset`에 `lora`·`loraStrength`·`loraTrigger` 필드. `ComfyUIClient`가 이미지에 LoRA 노드 20 주입, `ComfyUIVideoGenerator`가 영상에 노드 123 주입(+ 픽셀화). `IVideoGenerator.Generate`가 `pixelateWidth` 대신 **`StylePreset`을 받도록 변경**(LoRA·픽셀화·향후 스타일 파라미터를 하나로). AppFlowManager가 `_chosenStyle` 전달
- **프로덕션 재현 최종 검증**: 실제 Styles.json 픽셀 설정 + 갱신된 코드 경로 그대로 재현 → 이미지·영상 모두 선명한 픽셀아트 확인(영상 모션 4.2). 컴파일 에러 0. **플레이 모드 검증 남음**

---

## 2. 알려진 이슈 / 메모

| 항목 | 내용 | 대응 |
| --- | --- | --- |
| ~~영상 도착 전 세션 만료~~ **해결(2026-07-13)** | 결과 화면 자동 복귀(60초)가 영상 생성(웜 ~45초, 콜드·VRAM 스왑 시 60초 초과)보다 먼저 발동 — 영상을 못 보고 대기 화면으로 복귀 반복 (플레이 실측) | ✅ AppFlowManager: 영상 생성 진행 중(`_videoInProgress`)에는 자동 복귀 보류. 상한 = resultReturnSeconds + video.generateTimeoutSeconds (콜백 유실 대비). 영상 도착 시 타이머 리셋은 기존대로 |
| ComfyUI 첫 제출 검증 오류 | 업로드 직후 첫 `/prompt` 제출에서 `Invalid image file` 1회 발생, 재시도로 해결 | ✅ 해소 — ComfyUIClient에 제출 재시도 1회 내장 (Config `submitMaxRetries`) |
| ~~첫 생성이 타임아웃(첫 이미지 안 나옴)~~ **해결됨(2026-07-09)** | 원인 2개: ①콜드 모델 로딩 31.66초 > 타임아웃 30초, ②폴링 캐싱으로 첫 생성 완료 미감지. **대응 완료**: `ComfyUIClient.Warmup()`(시작 시 예열, 3회 재시도) + PollHistory 캐시버스터·no-cache + 타임아웃 45초. 콜드 재시작 실측으로 워밍업 첫 시도 완료 + 실제 생성 Result 도달 확인. 상세 인수인계 §6 |
| ~~결과 화면 미완~~ **해소(2026-07-10)** | QR 코드·[전시장에 내 작품 걸기] 버튼 없음 | ✅ 해소 — ④에서 ResultPanel에 추가 완료 |
| B2 계정 미개통 | 코드(`B2Uploader`)는 완성 — 키가 없으면 QR이 자동 숨김으로 동작 (플레이 실측 확인) | 가입(카드 불필요) → 공개 버킷 + 버킷 제한 키 → `Config/b2-key.json` 배치 (절차: 인수인계 §7) |
| VLM 필터 — 운영 결정 보류 | 필터 코드는 완성(OpenAI 호환 API 호출), 기본값 `filter.enabled=false` — opt-in 작품이 곧장 갤러리로 감. 상주 인력이 있으면 관리자 모드 사후 관리로 대체 가능 | 전시 운영 형태 확정 시 결정. 켜려면 Ollama + 모델 선정 (인수인계 §7) |
| ~~스타일 예시 이미지 3종 없음~~ 해소 | `StyleExamples/`에 4종 PNG 배치됨 (cartoon/pixelart/clayStyle/realistic) | ✅ 해소 — 프롬프트 확정본으로 재생성해 교체하면 더 정확 (선택) |
| ~~스타일 3종 프롬프트 임시값~~ **해소(2026-07-10)** | 카툰/픽셀아트/클레이 프롬프트·denoise 미검증 상태였음 | ✅ 해소 — 실사 구조로 강화 + ComfyUI API 직접 생성으로 4종 검증 (§1). 플레이 모드 통합 확인만 남음 |
| ~~카툰 품질 열세~~ **개편(2026-07-13)** | RV 실사 모델로는 카툰 화풍 상한이 낮음 (이미지 생성부터 품질 불만) | ✅ ToonYou 전용 체크포인트로 분리 (§1). 잔여 특성: 무채색 낙서의 빈 창문에 운전자 캐릭터 가능(수용), 스타일 전환 첫 생성 몇 초 지연(모델 스왑), 예시 썸네일 교체 필요 |
| ~~카툰 생성 타임아웃 실패~~ **해결(2026-07-13)** | 카툰 첫 선택 시 ToonYou 2.3GB 콜드 디스크 로드가 45초 타임아웃 초과 (워밍업이 실사만 예열했음) | ✅ `Warmup()`을 모든 distinct 체크포인트 예열로 확장 — 콜드 로드를 부팅으로 이동, 이후 스왑은 OS 캐시로 ~7초. `generateTimeoutSeconds` 45→90(예열 콜드 로드 여유). 인수인계 §6 |
| ~~카툰 이미지 → 영상 스타일 불일치 (우려)~~ **검증: 문제 없음(2026-07-13)** | 영상 워크플로가 RV 고정이라 카툰이 실사로 되칠해질까 우려 | ✅ 4스타일 파이프라인 실측: 카툰 영상이 **셀 셰이딩 그대로 유지**됨(denoise 0.6에서도 img2vid가 입력 스타일 보존). 클레이도 유지. 단 카툰은 평평한 면이라 카메라 팬 체감이 약함(모션지표 1.7) — 필요 시 스타일별 LoRA 강도로 보강(미결) |
| ~~픽셀 이미지 모자이크·영상 스타일 소실~~ **해결(2026-07-13 저녁)** | 후처리(128/6)가 모자이크처럼 뭉개짐 + 영상은 RV가 매끈하게 재해석 | ✅ **픽셀 LoRA(PixelArtRedmond) 도입** — 이미지·영상 양쪽에 LoRA 주입 + 후처리 160/8로 완화. 선명한 정품 픽셀 스프라이트 + 움직이는 픽셀 영상 확인(§1). 플레이 검증 남음 |
| 실사·클레이 이미지 바닥 부유물 | 낙서의 잔선(끝단 연장선)·분리된 바퀴 원이 denoise 0.8에서 바닥의 검은/크롬 덩어리, 떠다니는 클레이 공으로 나옴 (2026-07-13 공통 입력 실측) | 프롬프트로 못 잡는 구조적 한계(부정 프롬프트 floating/disconnected parts로도 denoise 0.8이 우선). 운영 대응: 그리기 안내 "차 전체를 크게, 선을 닫아서" |
| 엉성한 그림 아티팩트 → **재발·재수정(2026-07-09 저녁)** | 바퀴 이상/없음·문 이상·차 모양 붕괴 재발. **원인: ControlNet 0.35/0.6 튜닝이 문서에만 있고 워크플로 파일엔 0.9/1.0으로 남아 있었음** + 실사 프롬프트의 "isometric view of a boxy car"(낙서 시점과 충돌) + 부정 프롬프트 "floor, shadow"(바퀴 접지 삭제) | 대응 완료: 워크플로 노드 9에 **strength 0.35 / end_percent 0.6 실반영**, 실사 프롬프트에서 isometric 제거·four wheels 강조·부정 프롬프트에 바퀴 이상 계열 추가, **실사 denoise 0.8**(우선순위 변경: 모양 정상 > 색 보존, 장난감풍 허용). 플레이 모드 재검증 필요 (인수인계 §5). **2026-07-10 또 재발 발견**: 파일에 strength 0.6으로 되돌아가 있어 0.35 재반영 (인수인계 §5 경고 참조) |
| 도구 UI 임시 생성 | 팔레트/버튼이 런타임 코드 생성 (기본 uGUI 모양) | 디자인 리소스 확보 후 프리팹 교체 |
| 프로젝트 정리 | 이전 프로젝트(지구환경코딩) 스크립트·씬 삭제가 워킹 트리에 미커밋 상태 | clean slate 커밋으로 정리 필요 |

---

## 3. 다음 작업 (마일스톤 ④ 마무리 → ⑤)

1. **B2 계정 개통 + QR 실검증**: backblaze.com 가입(카드 불필요) → 공개 버킷 + 버킷 제한 키 발급 → `<프로젝트 루트>/Config/b2-key.json` 배치 → 결과 화면 QR 표시·폰 스캔 다운로드 실측 (절차: 인수인계 §7)
2. **VLM 필터**: 운영 형태(상주 인력 유무) 확정 시 결정 — 켜기로 하면 Ollama + 모델 비교 후 `filter.enabled=true` → 정상/부적절 낙서로 Gallery/Quarantine 분기 실측
3. **마일스톤 ⑤ 착수**: 운영 안정화 — 부팅 자동 시작(ComfyUI→앱 순), `ComfyUIWatchdog`(무응답 감지·재시작), 키오스크 전체화면, 관리자 모드(계획서 11장: 갤러리 관리·격리 복원·상태 확인), Build Settings 씬 등록 + Windows 빌드 검증
4. **마일스톤 ⑥ — 결과 영상화 마무리 (코드·씬은 2026-07-13 완료 — §1 참조)**
   1. **플레이 모드 통합 검증** (사용자 실측): 전체 사이클에서 결과 이미지 → ~40초 후 영상 교체 확인. 함께 확인: 안내 라벨 표시/소멸, [다시 그리기]·대기 복귀 시 재생 정지, ComfyUI 꺼짐/영상 실패 시 이미지 유지, 세션 만료 수정(영상 도착까지 화면 유지), 모션 강화 후 움직임 체감
   2. 모션이 여전히 부족하면 모션 LoRA(v2_lora_PanLeft 등) — "봐줄 만한 수준"까지만 (API 전환 가능성 있는 매몰 구간)
   3. 후속 결정: B2 업로드에 mp4 포함(QR 랜딩 페이지 video 태그) 여부, GallerySlideshow 영상 재생 여부, 카툰→영상 스타일 불일치 대응
5. **스타일 개편 검증 (2026-07-13 개편 — §1 참조)**: 플레이에서 카툰(ToonYou)·픽셀(그리드 스냅) 각 1회 이상 생성 — 카툰 색칠/무채색 입력 모두, 스타일 연속 전환 시 모델 스왑 지연 체감 확인. 카툰 예시 썸네일(StyleExamples/cartoon.png)을 ToonYou 결과물로 교체

---

## 4. 실행 방법 (개발용)

1. ComfyUI 서버: `Tools\run_comfyui.bat` 실행 (이미 켜져 있으면 생략. 기동 후 준비까지 ~90초)
2. Unity에서 `Assets/Scenes/AI자동차드로잉체험.unity` 열고 플레이 → 대기 화면이 뜬다
3. 화면 클릭 → 그리기 → [완성!] → 스타일 선택 → 4~8초 후 결과 화면 (콜드 부팅 첫 생성은 타임아웃될 수 있음 — 한 번 더 시도)
4. 산출물: `AppData\LocalLow\DefaultCompany\Practice01\Sessions\`에 `_line`/`_sketch`/`_result` PNG 3장 + `_result.mp4` (영상 성공 시)
   - 결과 화면에서 ~40초 기다리면 우측 이미지가 움직이는 영상으로 바뀐다 (video.enabled=true일 때)
5. 서버를 안 켜고 플레이하면 생성 실패 → 사과 문구 → 대기 복귀로 동작해야 정상 (예외로 죽지 않기)
6. **갤러리(Display 2) 확인**: Game 뷰 탭을 2개 열고 각각 Display 1 / Display 2 지정 (계획서 5장, 빌드 불필요). 결과 화면에서 [전시장에 내 작품 걸기] 클릭 → `Gallery/` 폴더에 복사 → 몇 초 안에 Display 2와 대기 화면 미니 슬라이드쇼에 반영
7. **QR 확인**: B2 키 배치(인수인계 §7)가 된 경우에만 결과 화면 좌하단에 QR 표시 — 미설정이면 안 보이는 게 정상

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

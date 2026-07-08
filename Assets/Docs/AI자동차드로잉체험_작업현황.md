# AI 자동차 드로잉 체험 — 작업 현황

> 기준 문서: [작업계획서](AI자동차드로잉체험_작업계획서.md) · 환경/함정: [인수인계](AI자동차드로잉체험_인수인계.md)
> 최종 갱신: 2026-07-08
>
> **갱신 규칙**: 이 문서는 **모든 작업 세션이 끝날 때마다** 갱신한다. 절차는 맨 아래 [5. 문서 갱신 절차](#5-문서-갱신-절차-작업-세션-종료-시) — 세 문서를 함께 점검하는 체크리스트가 있다.

---

## 진행 요약

| 단계 | 내용 | 상태 |
| --- | --- | --- |
| ① | ComfyUI 파이프라인 검증 (설치·모델·워크플로·생성 테스트) | ✅ 완료 (2회차 7.2초, 목표 4~8초 달성) |
| ② | 그리기 캔버스 (이중 텍스처 + 도구 + PNG 저장) | ✅ 완료 (플레이 모드 실측 검증) |
| ③ | 전체 흐름 연결 (패널 상태머신 + ComfyUI 연동 + 시간 정책) | ⬜ 예정 (다음 작업) |
| ④ | 갤러리(Display 2) + QR(GCS) + VLM 필터 + opt-in | ⬜ 예정 |
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

---

## 2. 알려진 이슈 / 메모

| 항목 | 내용 | 대응 |
| --- | --- | --- |
| ComfyUI 첫 제출 검증 오류 | 업로드 직후 첫 `/prompt` 제출에서 `Invalid image file` 1회 발생, 재시도로 해결 | ComfyUIClient(③)에 제출 재시도 1회 내장 예정 |
| 첫 생성 지연 | 서버 기동 후 첫 생성은 모델 로딩 포함 ~11초 | 앱 시작 시 워밍업 생성 1회 실행 예정 (⑤) |
| 도구 UI 임시 생성 | 팔레트/버튼이 런타임 코드 생성 (기본 uGUI 모양) | 디자인 리소스 확보 후 프리팹 교체 |
| 프로젝트 정리 | 이전 프로젝트(지구환경코딩) 스크립트·씬 삭제가 워킹 트리에 미커밋 상태 | clean slate 커밋으로 정리 필요 |

---

## 3. 다음 작업 (마일스톤 ③ — 전체 흐름 연결)

1. `Generation/ComfyUIClient.cs` — 이미지 업로드 → 워크플로 제출(검증 오류 재시도 1회) → 폴링 → 결과 다운로드 (UnityWebRequest, 타임아웃 30초)
2. `Generation/StyleLibrary.cs` + `StreamingAssets/Data/Styles.json` — 스타일 = (프롬프트 + denoise) 데이터 관리 (v1은 실사 1종)
3. `Core/AppFlowManager.cs` — 패널 상태머신: 대기 → 그리기 → 스타일 선택 → 생성 중 → 결과 → 복귀
4. `Core/IdleWatcher.cs` — 방치 감지 (무입력 90초 팝업 + 30초 후 복귀), 결과 화면 60초 자동 복귀
5. 나머지 패널 4종 (Attract / Style / Generating / Result) UI 구성
6. 검증: 대기→그리기→생성→결과→복귀 1사이클 완주 (실제 생성 포함)

---

## 4. 실행 방법 (개발용)

1. ComfyUI 서버: `Tools\run_comfyui.bat` 실행 (이미 켜져 있으면 생략)
2. Unity에서 `Assets/Scenes/AI자동차드로잉체험.unity` 열고 플레이
3. 그리기 → [완성!] 버튼 → `AppData\LocalLow\DefaultCompany\Practice01\Sessions\`에 PNG 2장 저장 확인

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

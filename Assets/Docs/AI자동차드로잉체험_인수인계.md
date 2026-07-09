# AI 자동차 드로잉 체험 — 인수인계 문서

> 최종 갱신: 2026-07-09
> 새 담당자(개발자 또는 AI 에이전트)가 이 문서 하나로 환경을 재구성하고 작업을 이어받을 수 있도록 작성한다.

## 문서 3종의 역할과 갱신 시점

| 문서 | 내용 | 갱신 시점 |
| --- | --- | --- |
| [AI자동차드로잉체험_작업계획서.md](AI자동차드로잉체험_작업계획서.md) | **무엇을 왜 만드는가** — 확정된 설계 전체. 설계 변경 전 반드시 읽을 것 | 설계 결정이 바뀔 때만 (+ §18 변경 이력 기록) |
| [AI자동차드로잉체험_작업현황.md](AI자동차드로잉체험_작업현황.md) | **어디까지 됐는가** — 마일스톤 진행 상태, 다음 작업 목록 | **모든 작업 세션 종료 시** |
| 본 문서 | **어떻게 이어받는가** — 환경 재구성, 구조, 튜닝 포인트, 함정, 미결사항 | 새 함정·환경/계정 변경·미결 해소 시 |

> 세 문서를 함께 점검하는 체크리스트는 [작업현황 §5 「문서 갱신 절차」](AI자동차드로잉체험_작업현황.md)에 있다. 같은 내용을 두 문서에 쓰지 않는 것이 원칙이다.

---

# 1. 프로젝트 개요 (한 줄)

전시장 설치물: 관람객이 마우스로 자동차를 그리면 로컬 Stable Diffusion이 실사 이미지로 변환 →
화면 표시 + QR 다운로드 + (본인 동의 시) Display 2 갤러리 전시.

- 저장소: `https://github.com/dsdsasd385/Practice01.git` (main 브랜치)
- 이전 프로젝트(지구환경코딩)를 같은 저장소에서 갈아엎고 시작함. 이전 스크립트 삭제가 미커밋 상태로 남아 있음

---

# 2. 개발 환경 요구사항

| 항목 | 요구사항 | 비고 |
| --- | --- | --- |
| Unity | **2022.3.62f3 LTS** | uGUI 기반, TMP 미사용 (한글 폰트는 LegacyRuntime 동적 폰트) |
| Newtonsoft JSON | `com.unity.nuget.newtonsoft-json` 3.2.2 (UPM) | 워크플로 JSON 노드 치환·Texts.json 파싱용. JsonUtility는 동적 JSON을 못 다뤄서 추가 (2026-07-09) |
| GPU | NVIDIA 8GB VRAM 이상 | 현 개발 PC: RTX 3060 8GB. **8GB 전제로 SD 1.5를 선택**했으므로 GPU가 더 좋아져도 계획서 2장 근거를 먼저 읽을 것 |
| Python | 3.12.x | ComfyUI venv용 (Unity와 무관) |
| 디스크 | ComfyUI + 모델 약 10GB | |

---

# 3. 외부 구성 요소 재설치 절차 (새 PC 세팅 시)

ComfyUI는 Unity 프로젝트 **밖** `C:\Users\<사용자>\ComfyUI`에 설치한다.

```bat
git clone --depth 1 https://github.com/comfyanonymous/ComfyUI.git C:\Users\<사용자>\ComfyUI
cd C:\Users\<사용자>\ComfyUI
python -m venv venv
venv\Scripts\pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu126
venv\Scripts\pip install -r requirements.txt
```

모델 3종 다운로드 (인증 불필요, 직링크):

| 대상 폴더 (`ComfyUI\models\`) | URL |
| --- | --- |
| `checkpoints\` | `https://huggingface.co/SG161222/Realistic_Vision_V5.1_noVAE/resolve/main/Realistic_Vision_V5.1_fp16-no-ema.safetensors` |
| `controlnet\` | `https://huggingface.co/comfyanonymous/ControlNet-v1-1_fp16_safetensors/resolve/main/control_v11p_sd15_scribble_fp16.safetensors` |
| `vae\` | `https://huggingface.co/stabilityai/sd-vae-ft-mse-original/resolve/main/vae-ft-mse-840000-ema-pruned.safetensors` |

설치 후 `Tools\run_comfyui.bat`의 경로를 새 PC에 맞게 수정한다 (현재 `C:\Users\HULIAC\ComfyUI` 하드코딩).

동작 확인: 브라우저에서 `http://127.0.0.1:8188` 접속 → UI가 뜨면 정상.

---

# 4. 코드 구조 (namespace `CarDrawing.*`)

```
Assets/Scripts/
├── Drawing/      캔버스. DrawingCanvas가 핵심 (이중 RenderTexture)
├── Generation/   ComfyUI 연동 (ComfyUIClient·StyleLibrary)
├── Results/      저장(SessionStore). 업로드·QR·필터는 ④에서 작성 예정
├── Gallery/      Display 2 슬라이드쇼 (④에서 작성 예정)
├── Core/         AppFlowManager(상태머신)·ConfigManager·TextLibrary·LogManager·IdleWatcher
└── UI/           패널 컨트롤러 5종 + UiBuilder(런타임 uGUI 공용 헬퍼)
```

패널 전환은 `Core/AppFlowManager`가 GameObject 활성/비활성으로 제어한다.
패널 UI는 각 컨트롤러의 Awake에서 런타임 생성 — 씬에는 빈 패널 오브젝트만 있다 (§6 함정 참조).
문구·시간·스타일은 전부 `StreamingAssets/Data/*.json` (Config/Texts/Styles) — 코드에 문구를 넣지 말 것.

데이터 흐름 (완성 기준):

```
DrawingCanvas (선 RT + 색 RT)
  → CanvasExporter (PNG 2장)
  → SessionStore.SaveSketchPair (Sessions/)
  → ComfyUIClient (업로드 → /prompt → 폴링 → 결과 PNG)
  → SessionStore.SaveResult
  → ResultPanel (표시 + QR 업로드 + opt-in 갤러리 신청)
```

설계 원칙 (계획서 12~14장에서 발췌, 코드 작성 시 반드시 지킬 것):

- **하드코딩 금지** — 문구·시간·프롬프트·주소는 `StreamingAssets/Data/*.json`
- **예외로 죽지 않기** — 파일/서버 없음은 기본값 + 로그로 계속 동작
- **주석은 한국어**, "왜"를 설명 (계획서 17장 규칙)

---

# 5. 워크플로 JSON 튜닝 가이드

파일: `Assets/StreamingAssets/ComfyUI/car_workflow_api.json` (ComfyUI API 형식)

Unity 클라이언트가 실행 시 치환하는(할) 필드 — **노드 ID를 바꾸면 클라이언트도 수정 필요**:

| 노드 ID | 클래스 | 치환 대상 |
| --- | --- | --- |
| `"3"` | CLIPTextEncode | 긍정 프롬프트 (스타일별) |
| `"4"` | CLIPTextEncode | 부정 프롬프트 (스타일별) |
| `"5"` | LoadImage | 색 레이어 파일명 (`color.png` → 업로드한 파일명) |
| `"6"` | LoadImage | 선 레이어 파일명 (`line.png` → 업로드한 파일명) |
| `"11"` | KSampler | `seed`(매회 랜덤), `denoise`(스타일별) |

품질 튜닝 시 만지는 순서 (한 번에 하나씩):

1. **denoise** (현재 0.7) — 낮추면 낙서 충실/AI 완성도↓, 올리면 색·형태가 날아감. 0.55~0.8 범위에서 조정
2. **ControlNet strength** (노드 9, 현재 0.9) — 형태 고정력. 낮추면 AI가 형태를 더 "예쁘게" 재해석
3. **steps** (현재 25) — 시간과 트레이드오프. 20까지 내리면 ~1.5초 단축
4. 프롬프트 — 차종·배경·조명 묘사 추가

측정 기준: 2회차 이후 생성 시간 (1회차는 모델 로딩 포함이라 제외).

---

# 6. 함정 / 반드시 알아야 할 것

| 함정 | 설명 |
| --- | --- |
| **Scribble 입력은 반전 필요** | ControlNet Scribble은 "검정 배경+흰 선"을 기대. 캔버스는 흰 배경+검정 선이므로 워크플로에 ImageInvert(노드 7)가 들어있다. 빼면 결과가 망가짐 |
| **업로드 직후 첫 제출 실패** | `/upload/image` 직후 `/prompt`가 `Invalid image file`을 1회 낼 수 있음 (실측). 클라이언트에 재시도 1회 필수 |
| **첫 생성은 느림** | 서버 기동 후 첫 생성 ~11초 (모델 로딩). 전시 운영 시 앱 시작할 때 워밍업 생성 1회 돌릴 것 |
| **RenderTexture는 플레이 모드에서 생성** | DrawingCanvas.Awake에서 만들므로 에디트 모드에서 캔버스가 비어 보이는 건 정상 |
| **PS 5.1에서 JSON 만들 때** | `Out-File`은 BOM을 붙여 unity-mcp-cli가 거부. `[System.IO.File]::WriteAllText` 사용, 문자열은 `[string]` 캐스팅 |
| **씬 빌드 인덱스** | `AI자동차드로잉체험.unity`가 Build Settings에 아직 없음. 빌드 전 등록 필요 |
| **도구 버튼은 런타임 생성** | DrawingPanelController가 팔레트·버튼을 코드로 만든다. 씬에서 버튼이 안 보여도 정상. 디자인 리소스가 나오면 프리팹 방식으로 교체 예정 |
| **패널 UI도 런타임 생성** | Attract/Style/Generating/Result 패널의 배경·텍스트·버튼은 각 컨트롤러 Awake에서 생성. 씬의 패널 오브젝트는 비어 보이는 게 정상. 버튼 GameObject 이름 = Texts.json 라벨 문구라서 문구를 바꾸면 `GameObject.Find` 경로도 바뀐다 (테스트 스크립트 주의) |
| **콜드 부팅 첫 생성은 30초 초과** | 서버 프로세스를 막 띄운 직후의 첫 생성은 모델 디스크 로딩 포함 30초 타임아웃을 넘겨 실패할 수 있다 (실측). ⑤의 워밍업 생성이 필수인 이유. 개발 중엔 실패 후 한 번 더 시도하면 됨 |
| **에디터 게임 뷰 클릭 주의** | 대기 화면은 화면 전체가 시작 버튼이라, 플레이 중 게임 뷰를 클릭만 해도 그리기 화면으로 넘어간다. 상태 꼬임이 아니라 정상 동작 |

---

# 7. 외부 계정 / 보안 (미생성 — ④에서 필요)

- **GCS (QR 업로드)**: 소유자 개인 Google Cloud 테스트 계정 사용 예정. 아직 버킷·서비스 계정 미생성
  - 생성 시: 공개 읽기 버킷 + 서비스 계정 키는 해당 버킷 **objectCreator 권한만** (전시장 PC 유출 대비)
  - 키 파일은 저장소에 커밋 금지. `StreamingAssets` 밖 exe 옆 `Config/` 폴더에서 로드하는 구조로 만들 것
- **VLM 필터 모델**: 미선정. 후보 Moondream2 / Qwen2-VL-2B급, CPU 비동기 실행 전제 (계획서 10장)

---

# 8. 미결 사항 체크리스트

- [ ] 마일스톤 ④~⑤ (작업현황 문서의 "다음 작업" 참조)
- [ ] 이전 프로젝트 삭제분 clean slate 커밋 정리
- [ ] GCS 버킷·서비스 계정 생성 (7장)
- [ ] VLM 필터 모델 선정·검증 (7장)
- [ ] 스타일 프리셋 3~4종 확정 (v1은 실사 1종) — 스타일별 denoise 개별 튜닝 필요
- [ ] 관리자 모드 진입 키 조합·비밀번호 확정 (계획서 11장, 예시: Ctrl+Shift+F12)
- [ ] Build Settings에 씬 등록 + Windows 빌드 검증
- [ ] 전시장 PC 사양·인터넷 회선 확정 시 재검토 (현재는 개발 PC = 전시 PC 가정)

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
| Unity | **2022.3.62f3 LTS** | uGUI 기반. 런타임 생성 UI(UiBuilder)는 레거시 Text(LegacyRuntime 폰트), **씬 배치 UI(StylePanel 등)는 TMP 사용** — 혼용 상태 (2026-07-09 StylePanel부터 TMP 도입) |
| Newtonsoft JSON | `com.unity.nuget.newtonsoft-json` 3.2.2 (UPM) | 워크플로 JSON 노드 치환·Texts.json 파싱용. JsonUtility는 동적 JSON을 못 다뤄서 추가 (2026-07-09) |
| NaughtyAttributes | `Assets/NaughtyAttributes/` (에셋, asmdef 포함) | 인스펙터 `[Button]` 등. 예: DrawingPanelController의 방치 팝업 테스트 버튼 |
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

1. **denoise** (스타일별, 실사 **현재 0.8**) — 낮추면 낙서 충실/색 유지, 올리면 AI가 더 재해석(색·형태가 날아감). ~~0.75가 「관람객 색 보존」 우선값~~ → **2026-07-09 저녁 우선순위 변경: 「모양 정상(바퀴·문·차체 온전)」이 최우선, 장난감풍·색 손실은 허용** — 정상차 보장값인 0.8 채택 (관람객 색이 옅어지는 상충은 감수)
2. **ControlNet strength** (노드 9, **현재 0.35**) + **end_percent** (노드 9, **현재 0.6**) — 선 추종력. **0.9/1.0 → 0.35/0.6으로 대폭 낮춤(2026-07-09)**: "선을 그대로 따르지 말고 형태만 비슷하게, 항상 정상적인 자동차로" 요구 반영. 낮은 strength = 선을 느슨하게 참고, 낮은 end_percent = 후반 스텝은 ControlNet 없이 AI가 자유롭게 정상차로 마무리. 실측상 잘 그린 그림은 형태·색 유지, 엉성한 그림도 대체로 정상차. **⚠ 주의: 이 값은 문서에만 있고 워크플로 JSON 파일엔 0.9/1.0으로 남아 있었음 → 바퀴·문 뭉개짐 재발의 원인. 2026-07-09 저녁 파일에 실반영 완료. 워크플로를 ComfyUI GUI에서 다시 export하면 튜닝값이 초기화되니 반드시 이 표와 대조할 것**. **2026-07-10 또 재발**: 파일에 strength 0.6으로 되돌아가 있었음(커밋 79747d8) → 0.35 재반영. 0.6에서는 엉성한 낙서 입력 시 실사조차 범퍼 클로즈업으로 붕괴함을 API 직접 생성으로 실측 — 워크플로 파일을 만졌으면 커밋 전에 반드시 이 표와 대조
3. **steps** (현재 25) — 시간과 트레이드오프. 20까지 내리면 ~1.5초 단축
4. 프롬프트 — 스타일 4종 공통 구조(2026-07-10 확정): `(<스타일> single complete car:1.4~1.5)` + `full car body` + `(four round wheels touching the ground:0.7)` + 스타일 키워드, 부정 프롬프트에 기형 억제 세트(extra/missing/square/broken wheels, floating/disconnected parts, unfinished, deformed…). 실사 프롬프트는 "single complete car / full car body / four round wheels touching the ground"로 온전한 단일 차를 강하게 고정, 부정 프롬프트로 미완성·다중차·왜곡·낙서잔재·바퀴 이상(square/broken wheels, floating parts, open doors)을 억제(2026-07-09 강화). **"isometric view of a boxy car"는 제거함(2026-07-09 저녁)** — 관람객 낙서(측면 뷰)와 시점이 충돌해 차 모양이 왜곡되고, 부정 프롬프트의 "floor, shadow"는 바퀴 접지를 지워 바퀴가 떠 있거나 흰 덩어리로 뭉개지는 원인이라 함께 제거

> **"엉성한 그림 → 이상한 차" 대응 정리 (2026-07-09)**: prompt/negative만으로는 잡선 아티팩트를 못 없앤다(ControlNet이 모든 획을 따름). 실효 레버 순서 = ①ControlNet strength/end_percent 낮추기(선을 느슨히) → ②denoise 올리기(재해석↑). 단 **denoise를 올리면 관람객 색이 날아가는 상충**이 있어, 현재는 색 보존을 위해 실사 denoise 0.75 + CN 0.35/end 0.6로 절충. 잡선이 아주 많은 극단적 낙서는 드물게 문 열린 듯한 아티팩트가 남을 수 있음(정상차 100% 보장하려면 denoise 0.8, 색 희생). A/B 근거 이미지는 검증 세션 스크래치패드(final_good, f_good_e045, t_cn35_dn08, g_cn35_dn08 등).

측정 기준: 2회차 이후 생성 시간 (1회차는 모델 로딩 포함이라 제외).

---

# 6. 함정 / 반드시 알아야 할 것

| 함정 | 설명 |
| --- | --- |
| **Scribble 입력은 반전 필요** | ControlNet Scribble은 "검정 배경+흰 선"을 기대. 캔버스는 흰 배경+검정 선이므로 워크플로에 ImageInvert(노드 7)가 들어있다. 빼면 결과가 망가짐 |
| **업로드 직후 첫 제출 실패** | `/upload/image` 직후 `/prompt`가 `Invalid image file`을 1회 낼 수 있음 (실측). 클라이언트에 재시도 1회 필수 |
| **클레이 프롬프트에 손 관련 단어 금지** | "handcrafted"·"stop motion" 계열 단어가 점토 손·받침대를 소환한다 (2026-07-10 실측: 클레이 손이 차를 들고 있는 결과). "handcrafted" 제거 + 부정 프롬프트에 hands, fingers, arms, person, stand, pedestal 유지 필수 |
| **색 유도 단어가 관람객 색을 지운다** | "limited color palette"(픽셀)·"vibrant flat colors"(카툰)는 AI가 색을 마음대로 재배정하는 원인 (2026-07-10 매트릭스 실측). "colors matching the drawing"으로 교체 + 카툰·픽셀 denoise 0.7로 색 보존. 색 충실도의 실질 레버는 denoise (0.8=색 재해석, 0.7=색 유지) |
| **모서리에 작게 그린 그림·반절림 그림은 취약** | 화면 구석의 작은 그림은 빈 공간을 AI가 채운다 — 실사는 색 무시, 카툰은 미완성 낙서 덩어리, 클레이는 멀티카(부정 프롬프트 multiple cars로도 못 막음). 픽셀만 안정. 차가 화면 밖으로 반쯤 잘린 그림(2026-07-10 검증)도 마찬가지 — 카툰은 유령차 낙서 동반, 픽셀은 가장자리 반절림에서 파편화, 클레이는 잘린 차를 멋대로 완성차로 채움. 정면 그림 + 실사 조합도 탑뷰·포스터 구도로 샐 수 있음. 프롬프트로 못 잡는 구조적 한계라 운영 대응은 그리기 안내 문구("차 전체를 크게 그려주세요") 수준 |
| **픽셀 스타일 프롬프트 이력과 주의점** | 16-bit crisp/retro 계열 단어는 결과가 깨져 보이는(노이즈 픽셀) 원인이었음. 최종안(2026-07-10)은 **품질 강조판** — (masterpiece/best quality 앵커) + accurate vehicle proportions·clean silhouette·4-6 color ramp shading·colored outlines·minimal dithering. 빨강·초록 3/3, 노랑 1~2/3 검증. **주의**: 긍정에 dithering 계열 단어가 있으면 부정의 dithering을, 소프트/AA 계열이면 부정의 smooth gradient/antialiased를 함께 제거해야 함 (정면충돌 시 조용히 품질 저하). 색이 옅어지면 "vibrant colors matching the drawing" 추가가 검증된 보완책 |
| **Realistic Vision으로 비실사 스타일 생성** | 체크포인트가 실사 특화라 카툰/픽셀/클레이는 스타일 가중치 `:1.4` + 부정에 photorealistic/photo/3d render 차단이 있어야 스타일이 잡힌다. 색칠한 그림은 4종 모두 안정, 선 몇 개 낙서는 시드 편차 존재 (denoise 0.8·CN 0.35에서도) |
| **"첫 이미지 안 나옴" — 원인 2개, 둘 다 대응 완료(2026-07-09)** | ①**콜드 모델 로딩**: 첫 생성은 모델 적재로 실측 31.66초, 클라 타임아웃 30초라 첫 관람객만 사과 화면. ②**폴링 캐싱**: 첫 생성 때 `/history/{id}` 반복 GET에서 "아직 없음" 응답이 캐시돼 완료 후에도 갱신 안 됨 → 서버는 8~10초에 끝났는데 클라가 45초 타임아웃(콜드 재시작 실측 재현). **대응**: (a) `ComfyUIClient.Warmup()` — 앱 시작 시 더미 생성 1회로 모델 예열(AppFlowManager.Start에서 호출, 서버 늦게 뜨면 3회 재시도), (b) PollHistory URL에 캐시버스터(`?t=ticks`)+`Cache-Control: no-cache`, (c) `generateTimeoutSeconds` 30→45. 실측: 콜드 재시작 후 워밍업이 첫 시도에 완료, 이어 실제 생성이 Result까지 정상 도달 |
| **RenderTexture는 플레이 모드에서 생성** | DrawingCanvas.Awake에서 만들므로 에디트 모드에서 캔버스가 비어 보이는 건 정상 |
| **PS 5.1에서 JSON 만들 때** | `Out-File`은 BOM을 붙여 unity-mcp-cli가 거부. `[System.IO.File]::WriteAllText` 사용, 문자열은 `[string]` 캐스팅 |
| **씬 빌드 인덱스** | `AI자동차드로잉체험.unity`가 Build Settings에 아직 없음. 빌드 전 등록 필요 |
| **도구 버튼은 런타임 생성** | DrawingPanelController가 팔레트·버튼을 코드로 만든다. 씬에서 버튼이 안 보여도 정상. 디자인 리소스가 나오면 프리팹 방식으로 교체 예정 |
| **패널 UI도 런타임 생성** | Attract/Style/Generating/Result 패널의 배경·텍스트·버튼은 각 컨트롤러 Awake에서 생성. 씬의 패널 오브젝트는 비어 보이는 게 정상. 버튼 GameObject 이름 = Texts.json 라벨 문구라서 문구를 바꾸면 `GameObject.Find` 경로도 바뀐다 (테스트 스크립트 주의) |
| **콜드 부팅 첫 생성은 30초 초과** | 서버 프로세스를 막 띄운 직후의 첫 생성은 모델 디스크 로딩 포함 30초 타임아웃을 넘겨 실패할 수 있다 (실측). ⑤의 워밍업 생성이 필수인 이유. 개발 중엔 실패 후 한 번 더 시도하면 됨 |
| **에디터 게임 뷰 클릭 주의** | 대기 화면은 화면 전체가 시작 버튼이라, 플레이 중 게임 뷰를 클릭만 해도 그리기 화면으로 넘어간다. 상태 꼬임이 아니라 정상 동작 |
| **씬 배치 UI는 SerializeField 배선 필요** | StylePanel처럼 씬에 미리 배치한 패널은 컨트롤러의 참조(styleGroups·title 등)를 인스펙터에서 연결해야 동작한다. 계층만 만들고 배선을 빼면 조용히 아무 것도 안 채워짐(로그 경고만) |
| **TMP 텍스트는 Awake 아닌 Start에서 설정** | 비활성으로 시작하는 패널이 활성화될 때, TMP 컴포넌트 초기화가 끝나기 전(Awake)에 `.text`를 넣으면 직렬화값으로 덮어써져 무시된다. 반드시 Start에서 설정 (StylePanelController가 이 방식) |
| **예시 이미지가 검게 보이면 Image.color 확인** | 씬의 Image color가 검정이면 스프라이트를 넣어도 검게 물든다. 코드에서 스프라이트 할당 시 `color=white`로 되돌린다 (StylePanel 예시 이미지에서 실측) |
| **런타임 생성 패널: 활성화 전에 데이터 지정 금지** | Awake에서 UI를 만드는 패널(ResultPanel 등)은 **활성화되는 순간 Awake가 돈다**. AppFlowManager가 `SetImages()`를 `EnterState(활성화)`보다 먼저 호출하면 그 시점엔 RawImage 참조가 null이라 지정이 무시되고, 직후 Awake가 빈 UI를 새로 만들어 **결과 이미지가 흰 화면**이 된다(2026-07-09 실측, 데이터는 정상인데 표시만 안 됨). 대응: ResultPanelController가 텍스처를 캐시해 SetImages·Awake 어느 쪽이 먼저 와도 반영. (GeneratingPanel은 `EnterState→Begin` 순, StylePanel preview는 SerializeField라 원래 안전) |
| **런타임 UI가 씬에 저장되면 중복** | Awake 생성 UI가 씬에 저장돼 있으면 플레이 때 Awake가 또 만들어 중복된다. ResultPanelController는 Awake 첫머리에서 기존 자식을 비운 뒤 생성한다 |
| **StreamingAssets 이미지는 런타임 File 로드** | 스타일 예시 이미지는 `StreamingAssets/StyleExamples/<id>.png`를 `File.ReadAllBytes`→`Texture2D.LoadImage`→`Sprite.Create`로 읽는다 (텍스처 임포트 대상 아님). 경로는 Styles.json `thumbnail` |

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

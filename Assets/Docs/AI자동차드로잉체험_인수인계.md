# AI 자동차 드로잉 체험 — 인수인계 문서

> 최종 갱신: 2026-07-13
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

**영상 생성(마일스톤 ⑥)용 추가 구성 (2026-07-13 PoC에서 도입)**:

```bat
cd C:\Users\<사용자>\ComfyUI\custom_nodes
git clone --depth 1 https://github.com/Kosinkadink/ComfyUI-AnimateDiff-Evolved.git
git clone --depth 1 https://github.com/Kosinkadink/ComfyUI-VideoHelperSuite.git
..\venv\Scripts\pip install opencv-python imageio-ffmpeg
```

| 대상 폴더 (`ComfyUI\models\`) | URL |
| --- | --- |
| `animatediff_models\` | `https://huggingface.co/guoyww/animatediff/resolve/main/mm_sd_v15_v2.ckpt` → 파일명 `mm_sd15_v2.ckpt` |
| `animatediff_motion_lora\` | `https://huggingface.co/guoyww/animatediff/resolve/main/v2_lora_PanLeft.ckpt` (77MB, **영상 움직임의 핵심** — §6) |

(같은 폴더의 AnimateLCM 2종은 품질 기각된 잔재 — 삭제해도 무방. §6 함정 참조. `v2_lora_ZoomIn`·`v2_lora_TiltUp`은 대안 카메라 모션으로 실험용 — 현재 워크플로는 PanLeft만 사용)

**스타일 전용 모델 (2026-07-13 도입, 계획서 §8)**:

| 대상 폴더 (`ComfyUI\models\`) | URL |
| --- | --- |
| `checkpoints\` | `https://huggingface.co/pbxadb/sd1.5-Models/resolve/main/toonyou_beta6.safetensors` (2.3GB, 카툰용, 원본은 CivitAI ToonYou Beta 6) |
| `loras\` | `https://huggingface.co/artificialguybr/pixelartredmond-1-5v-pixel-art-loras-for-sd-1-5/resolve/main/PixelArtRedmond15V-PixelArt-PIXARFK.safetensors` → 파일명 `PixelArtRedmond15V.safetensors` (27MB, **픽셀아트용**, 트리거 `Pixel Art, PixArFK`) |

설치 후 `Tools\run_comfyui.bat`의 경로를 새 PC에 맞게 수정한다 (현재 `C:\Users\HULIAC\ComfyUI` 하드코딩).

동작 확인: 브라우저에서 `http://127.0.0.1:8188` 접속 → UI가 뜨면 정상.

---

# 4. 코드 구조 (namespace `CarDrawing.*`)

```
Assets/Scripts/AIDrawing/
├── Drawing/      캔버스. DrawingCanvas가 핵심 (이중 RenderTexture)
├── Generation/   ComfyUI 연동 (ComfyUIClient·StyleLibrary)
├── Results/      저장(SessionStore) + QR(QrEncoder·QrCodeView) + 업로드(IResultUploader — 기본 B2Uploader, 대안 GcsUploader) + 필터(ContentFilter)
├── Gallery/      Display 2 슬라이드쇼 (GallerySlideshow — Gallery 폴더 감시, 대기 화면 미니 슬라이드쇼와 목록 공유)
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
  → ResultPanel 표시
      ├→ ComfyUIVideoGenerator (IVideoGenerator, 백그라운드 ~40초 → 성공 시 SessionStore.SaveResultVideo
      │     + ResultPanel.ShowVideo로 이미지→영상 교체, 실패 시 이미지 유지 — 관람객은 실패를 모른다)
      ├→ B2Uploader/GcsUploader (비동기 업로드 → 성공 시 ResultPanel.ShowQr, 실패/미설정 시 QR 숨김 유지)
      └→ [전시장에 내 작품 걸기] → ContentFilter.Evaluate (백그라운드)
            → 통과: SessionStore.AddToGallery (Gallery/) → GallerySlideshow가 폴더 감시로 자동 반영
            → 부적합·판정 불가: SessionStore.AddToQuarantine (Quarantine/)
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
| `"1"` | CheckpointLoaderSimple | 스타일에 `checkpoint`가 있을 때만 교체 (카툰=toonyou_beta6, 2026-07-13) |
| `"20"` | LoraLoader (동적 주입) | 스타일에 `lora`가 있으면 클라이언트가 노드 1과 소비자(3·4·11) 사이에 끼운다 (픽셀=PixelArtRedmond, 2026-07-13). model→11, clip→3·4로 재연결 + 트리거를 긍정 프롬프트 앞에 붙임 |
| `"3"` | CLIPTextEncode | 긍정 프롬프트 (스타일별, LoRA 트리거 prepend) |
| `"4"` | CLIPTextEncode | 부정 프롬프트 (스타일별) |
| `"5"` | LoadImage | 색 레이어 파일명 (`color.png` → 업로드한 파일명) |
| `"6"` | LoadImage | 선 레이어 파일명 (`line.png` → 업로드한 파일명) |
| `"9"` | ControlNetApplyAdvanced | 스타일에 `controlnetStrength` > 0일 때만 `strength` 교체 (카툰=0.5) — 파일 기본값 0.35는 그대로 유지 |
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
| **픽셀 스타일 프롬프트 이력 (2026-07-13 LoRA 방식으로 대체 — 위 "픽셀아트는 LoRA 방식" 함정 참조)** | 아래는 RV+프롬프트 시절 이력(역사적 참고용). 16-bit crisp/retro 계열 단어는 결과가 깨져 보이는(노이즈 픽셀) 원인이었음. 최종안(2026-07-10)은 **품질 강조판** — (masterpiece/best quality 앵커) + accurate vehicle proportions·clean silhouette·4-6 color ramp shading·colored outlines·minimal dithering. 빨강·초록 3/3, 노랑 1~2/3 검증. **주의**: 긍정에 dithering 계열 단어가 있으면 부정의 dithering을, 소프트/AA 계열이면 부정의 smooth gradient/antialiased를 함께 제거해야 함 (정면충돌 시 조용히 품질 저하). 색이 옅어지면 "vibrant colors matching the drawing" 추가가 검증된 보완책 |
| **Realistic Vision으로 비실사 스타일 생성** | 체크포인트가 실사 특화라 픽셀/클레이는 스타일 가중치 `:1.4` + 부정에 photorealistic/photo/3d render 차단이 있어야 스타일이 잡힌다. **카툰은 이 방식으로는 상한이 낮아 2026-07-13 전용 체크포인트(ToonYou)로 분리** — 클레이가 RV에서 잘 나오는 건 점토(스톱모션)가 사진의 피사체이기 때문이며, 플랫 컬러·만화 외곽선은 실사 모델이 근본적으로 못 그린다 |
| **전용 체크포인트 스타일은 부팅 워밍업 필수** | 카툰(ToonYou 2.3GB)처럼 기본 워크플로와 다른 체크포인트를 쓰는 스타일은, 관람객이 처음 고르면 그 모델을 디스크에서 콜드 로드하느라 첫 생성이 타임아웃난다 (2026-07-13 실측, 카툰 생성 실패의 원인). `ComfyUIClient.Warmup()`이 **모든 스타일의 distinct 체크포인트를 부팅 시 예열**하도록 확장함 — 콜드 디스크 로드를 부팅으로 옮기면 이후 스타일 전환 스왑은 OS 파일 캐시 덕에 ~7초. 8GB VRAM이라 상주는 하나뿐이지만 목적은 VRAM이 아니라 디스크 캐시 적재. 예열이 콜드 로드를 견디도록 `generateTimeoutSeconds`를 45→**90**으로 상향(둘 다 이 값을 공유). 새 전용 체크포인트 스타일을 추가하면 Styles.json `checkpoint`만 채우면 워밍업이 자동 포함 |
| **ToonYou(카툰 체크포인트)는 캐릭터 편향** | 애니메이션 캐릭터 학습 비중이 커서 ①빈(무채색) 창문에 운전자 캐릭터를 그려 넣고 ②약한 ControlNet(0.35)에서는 스케치 구도를 무시하고 정면 뷰 등으로 재해석한다 (2026-07-13 A~L 12종 비교 실측). 대응: booru 태그(`no humans, vehicle focus`) + 가중 네거티브(`(person:1.3)` 등) + **CN 0.5**로 구도 고정. 운전자는 빈도만 줄었고 무채색 낙서에서는 여전히 나올 수 있음 — 귀여운 수준이라 수용 결정. 색칠한 그림에서는 관람객 색이 잘 유지됨(denoise 0.75) |
| **SD1.5 프롬프트는 77토큰 초과분을 버린다** | CLIP 한계로 긴 프롬프트의 뒷부분은 조용히 무시된다 (2026-07-13 영상 프롬프트에서 실측 — 장문 액션 서술이 전혀 반영 안 됨). 이미지·영상 프롬프트 모두 77토큰 내로 유지할 것. 강조가 필요하면 문장을 늘리지 말고 `(단어:1.3)` 가중치를 쓸 것 |
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
| **Display 2 캔버스는 레이어 상속 필수** | GalleryCamera는 UI 레이어(5)만 렌더링하는데 `UiBuilder`류의 `new GameObject`는 **Default 레이어(0)로 생성**된다 — 그대로 두면 Display 2가 빈 화면. GallerySlideshow가 Awake 끝에 SetLayerRecursively로 캔버스 레이어를 자식 전체에 물려준다. 갤러리 캔버스에 UI를 추가할 때 이 규칙을 지킬 것 |
| **QR 인코더는 자체 구현 — 수정 시 반드시 대조 검증** | `QrEncoder.cs`는 ISO 18004 직접 구현(외부 라이브러리 없음). 수정하면 `Encode(text, forceMask)`로 마스크 0~7 행렬을 덤프해 python-qrcode(`QRData(mode=MODE_8BIT_BYTE)`, `border=0`, `mask_pattern` 강제)와 MD5 대조할 것. 2026-07-10 버전 1·5·9·10 × 마스크 8종 = 32행렬 전부 일치 확인. 표시 텍스처는 FilterMode.Point 유지(보간되면 스캔 불가) |
| **스토리지 키는 프로젝트 밖 Config/ 폴더** | 키 경로 기본값(`Config/b2-key.json`, `Config/gcs-key.json`)은 exe 옆(에디터: 프로젝트 루트) 기준 상대 경로다. `/[Cc]onfig/`가 .gitignore에 있어 커밋되지 않는다. StreamingAssets에 넣으면 빌드에 포함되므로 금지 |
| **에디터에서 Display 2 확인** | 빌드 없이 Game 뷰 탭 2개를 열고 각각 Display 1/Display 2를 지정하면 갤러리 월이 보인다. `Display.displays[1].Activate()`는 빌드 전용(`#if !UNITY_EDITOR`) |
| **run_comfyui.bat은 ASCII 전용으로 유지** | 배치 파일에 한국어 주석을 UTF-8로 저장하면 cmd(cp949)가 줄 경계를 잘못 잘라 **스크립트가 조용히 아무것도 안 한다** (2026-07-10 실측: `'ONIOENCODING' is not recognized` 식으로 깨짐). 주석은 영문만. 서버 출력은 `ComfyUI\comfyui_run.log`로 남긴다 — 서버가 죽으면 이 파일부터 볼 것. `PYTHONIOENCODING=utf-8`은 cp949 콘솔의 UnicodeEncodeError 사망 방지용으로 유지 |
| **영상: AnimateLCM 조합은 품질 붕괴 — 기각** | AnimateLCM(모듈+LoRA+lcm 샘플러)은 beta 2종·denoise 0.35~0.55·512/768 전 조합에서 유화풍 질감 붕괴 + 차종 변형 (2026-07-13 실측). **표준 `mm_sd15_v2` + dpmpp_2m/karras 20스텝 cfg 7 + beta `sqrt_linear (AnimateDiff)`가 채택 조합** — 같은 입력에서 실사 크리스프. "3060이라 LCM으로 빨라야" 가정은 불필요했음(512×344 16프레임 40초로 충분) |
| **픽셀아트는 LoRA 방식 (후처리 강한 픽셀화는 모자이크가 됨)** | RV+프롬프트의 픽셀 출력은 흐릿하고, 여기에 강한 후처리(128px/6단계)를 걸면 모자이크처럼 뭉갠다(2026-07-13 실측·사용자 보고). **해법은 픽셀 LoRA(PixelArtRedmond, §3)**: RV 위에 얹으면 선명한 정품 픽셀 스프라이트가 나온다(강도 0.8, 트리거 `Pixel Art, PixArFK`). 후처리는 그리드만 균일하게 스냅하는 용도로 **160px/8단계로 완화**. 카툰(ToonYou)과 같은 "전용 모델" 접근 |
| **픽셀 영상은 LoRA + 프레임 재픽셀화 둘 다 필요** | 영상 워크플로(RV)에 LoRA를 안 얹으면 화풍을 잃고 매끈하게 다시 그린다. denoise만 낮추면 이번엔 움직임이 죽는다(denoise 0.3 → 모션 0.28, 정지). **해법**: `ComfyUIVideoGenerator`가 픽셀 스타일이면 ①영상 워크플로에 LoRA(노드 123, 체크포인트 101→AnimateDiff 104·CLIP 105·106 사이) 주입 + 트리거 prepend, ②VAEDecode(111) 뒤 ImageScale 노드 121·122로 재픽셀화 후 VHS(112)에 연결. denoise는 움직임 확보용 0.55~0.6 유지. LoRA有 영상 = 선명한 움직이는 픽셀 스프라이트(모션 4.2). 카툰·클레이 영상은 LoRA·픽셀화 불필요(RV가 스타일 보존) |
| **영상 움직임은 카메라 모션 LoRA에서 나온다 (프롬프트·denoise 아님)** | "바퀴만 움직이고 티가 안 난다"의 실제 해법. 프롬프트(과격한 액션 서술)·motion_scale·denoise 상향 모두 움직임을 거의 못 만든다(2026-07-13 프레임 실측: 현재안 모션지표 1.6, 정지 영상 수준). **AnimateDiff 카메라 모션 LoRA(`v2_lora_PanLeft` 강도 0.8)를 노드 120에 추가**하니 카메라가 차를 따라 팬해서 눈에 확 띔 — 화면 전체가 움직여 차 형태도 안 뭉개진다. 워크플로: 노드 120 `ADE_AnimateDiffLoRALoader` → 노드 104의 `motion_lora` 입력. 더 큰 움직임은 강도 0.8→1.0. denoise는 움직임이 아니라 원본 대비 표류(차 디자인 변형)만 키우므로 낮게(0.45~0.6) 유지 |
| **영상·이미지 워크플로는 노드 ID를 겹치면 안 됨** | 두 워크플로가 같은 노드 ID(예: 체크포인트 "1")를 쓰면 ComfyUI 프롬프트 간 캐시가 공유돼 AnimateDiff 실행 후 이미지 생성이 노이즈로 붕괴한다 (2026-07-13 실측). **영상 워크플로는 노드 ID 101번대 사용** — 분리 후 이미지↔영상 4연속 교대 실행 안정 확인. 영상 워크플로를 수정할 때 ID를 1~13번대로 되돌리지 말 것 |
| **ComfyUI Desktop 앱은 불필요** | 설치본은 git+venv 방식(`C:\Users\HULIAC\ComfyUI`)이며 Desktop 앱과 무관. "서버가 안 뜬다"면 Desktop 설치가 아니라 ①`comfyui_run.log` 확인 ②`venv\Scripts\python.exe main.py --listen 127.0.0.1 --port 8188` 직접 실행으로 진단 |

---

# 7. 외부 계정 / 보안 (코드 준비 완료 — 계정·모델만 미개통)

- **Backblaze B2 (QR 업로드, 기본)**: 계정 **아직 미생성** — 카드 등록 없이 가입 가능(10GB 영구 무료). 코드(`B2Uploader`)는 완성돼 있어 아래만 하면 QR이 켜진다:
  1. [backblaze.com](https://www.backblaze.com/sign-up/cloud-storage) 가입 (카드 불필요)
  2. 버킷 생성, **Files are: Private 유지** — ⚠ Public을 고르면 카드 등록을 요구한다(B2 정책, 2026-07-10 실측).
     비공개 버킷이어도 QR 링크는 클라이언트가 만료형 다운로드 토큰(`b2_get_download_authorization`)을 붙여 만들므로 폰에서 그대로 열린다. **링크 유효 기간 7일**(B2 최대치, Config `b2.downloadAuthSeconds`)
  3. App Keys에서 **해당 버킷 전용 키** 생성 — UI 기본 권한 세트에 포함된 writeFiles(업로드)·shareFiles(다운로드 토큰)가 필요하다
  4. `<프로젝트 루트>/Config/b2-key.json` 배치 (빌드에서는 exe 옆 `Config/`. .gitignore 처리됨 — 커밋 금지):
     ```json
     { "keyId": "발급된 keyID", "applicationKey": "발급된 applicationKey" }
     ```
     (버킷 제한이 없는 키를 쓰면 `"bucketId"`, `"bucketName"` 두 필드를 추가로 넣어야 한다)
  5. Config.json은 기본값 그대로 동작. 카드 등록 후 공개 버킷으로 바꾸면 `downloadAuthSeconds: 0`으로 토큰 없는 영구 링크가 된다
- **GCS (QR 업로드, 대안)**: `GcsUploader`로 구현 유지. B2 키가 없고 GCS 설정(버킷명+`Config/gcs-key.json`)이 있으면 자동으로 GCS를 쓴다. 절차: 공개 버킷 + objectCreator 전용 서비스 계정 키 + `gcs.bucketName` 기입. 결제 수단 등록 필요
- **VLM 필터 모델**: 미선정. 코드(`ContentFilter`)는 OpenAI 호환 chat completions 호출로 완성 — Ollama 설치 후 Moondream2 / Qwen2-VL-2B급을 비교(차량 판정 정확도·CPU 소요)해 `filter.model` 확정, `filter.enabled=true`로 전환. 필터가 꺼져 있으면 opt-in 작품이 곧장 갤러리로 간다. **운영 결정 보류 중** — 상주 인력이 있으면 관리자 모드 사후 관리로 대체 가능 (2026-07-10 논의)

---

# 8. 미결 사항 체크리스트

- [x] ~~마일스톤 ④ 코드·씬 구성~~ (2026-07-10 완료 — 플레이 검증·계정 개통만 남음, 작업현황 §3)
- [ ] 마일스톤 ④ 플레이 검증 + ⑤ (작업현황 문서의 "다음 작업" 참조)
- [ ] 이전 프로젝트 삭제분 clean slate 커밋 정리
- [ ] Backblaze B2 계정·버킷·키 생성 (7장 절차대로 — 코드는 준비됨. GCS는 대안으로 강등)
- [ ] VLM 필터 — 운영 형태 확정 시 결정 (7장 — 코드는 준비됨, 기본 filter.enabled=false, 보류 중)
- [x] ~~스타일 프리셋 3~4종 확정~~ (2026-07-10 — 4종 프롬프트·denoise 확정 → 2026-07-13 카툰 전용 체크포인트·픽셀 그리드 스냅으로 재확정, 작업현황 §1)
- [ ] 카툰(ToonYou)·픽셀(PixelArtRedmond LoRA) 개편 플레이 검증 (이미지+영상) + 예시 썸네일(StyleExamples/cartoon.png·pixelart.png) 새 결과물로 교체
- [ ] 카툰 이미지 → 영상 스타일 불일치 (영상 워크플로는 RV 고정이라 카툰 결과가 실사풍으로 되칠해짐) — 스타일별 영상 프롬프트/체크포인트 연동 여부 결정
- [ ] 관리자 모드 진입 키 조합·비밀번호 확정 (계획서 11장, 예시: Ctrl+Shift+F12)
- [ ] Build Settings에 씬 등록 + Windows 빌드 검증
- [ ] 전시장 PC 사양·인터넷 회선 확정 시 재검토 (현재는 개발 PC = 전시 PC 가정)
- [ ] 마일스톤 ⑥ 결과 영상화 — ~~AnimateDiff PoC~~ → ~~Unity 코드·씬 구성~~ (2026-07-13 완료) → **플레이 검증 + 모션 튜닝 + B2 영상 업로드·갤러리 영상 결정** (작업현황 §3). 외부 API 전환 시 회선·건당 과금(~$0.25) 확정 필요

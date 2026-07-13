using System;
using CarDrawing.Core;
using CarDrawing.Results;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CarDrawing.UI
{
    /// <summary>
    /// 결과 화면(ResultPanel). 계획서 4장: 스케치 vs 완성본 비교 + QR + [전시장에 내 작품 걸기] + [다시 그리기].
    /// UI 시스템에 속하며 AppFlowManager가 SetImages/ShowQr로 데이터를 넣고
    /// RetryRequested/GalleryRequested 이벤트를 받는다.
    /// QR은 업로드 완료 시에만 나타난다 — 오프라인·미설정이면 영역째 숨김 (계획서 9-2 자동 숨김).
    /// </summary>
    public class ResultPanelController : MonoBehaviour
    {
        /// <summary>[다시 그리기]를 눌렀을 때 (그림 유지 상태로 그리기 화면 복귀)</summary>
        public event Action RetryRequested;
        /// <summary>[전시장에 내 작품 걸기]를 눌렀을 때 (opt-in — 필터를 거쳐 갤러리 등재, 계획서 9-3)</summary>
        public event Action GalleryRequested;

        private RawImage _sketch;
        private RawImage _result;

        // QR 표시 요소. 텍스처는 세션마다 새로 만들므로 파괴 책임도 여기 있다
        private GameObject _qrGroup;
        private RawImage _qrImage;
        private Texture2D _qrTexture;

        private Button _galleryButton;
        private Text _galleryButtonLabel;
        private bool _gallerySubmitted;

        // 결과 영상 재생 (마일스톤 ⑥). 영상이 도착하면 결과 RawImage의 텍스처를 이미지→영상으로 교체한다.
        // VideoPlayer는 처음 필요할 때 한 번 만들어 재사용한다 (패널 자식은 Awake마다 파괴되므로 자기 자신에 붙임)
        private VideoPlayer _videoPlayer;
        private Text _videoPendingLabel;

        // 표시할 데이터를 캐시한다. SetImages/ShowQr/ShowVideo는 이 패널이 활성화(=Awake로 UI 생성)되기 전에
        // 호출될 수 있어(인수인계 §6 함정), 값을 캐시해 두고 어느 쪽이 먼저 실행돼도 최종 반영되게 한다.
        private Texture _pendingSketch;
        private Texture _pendingResult;
        private string _pendingQrUrl;
        private string _pendingVideoPath;
        private bool _videoPending; // 영상 생성이 백그라운드에서 진행 중 — 안내 문구 표시

        private void Awake()
        {
            // 씬에 이전 런타임 UI가 저장돼 있으면 Awake가 또 만들어 중복이 생기고,
            // 텍스처가 붙지 않은 쪽이 위에 보여 이미지가 비어 보인다. 생성 전에 기존 자식을 비운다.
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            UiBuilder.Stretch((RectTransform)transform);

            Image background = UiBuilder.CreateImage(transform, "Background", new Color(0.95f, 0.96f, 0.99f));
            UiBuilder.Stretch((RectTransform)background.transform);

            Text title = UiBuilder.CreateText(background.transform, "Title",
                TextLibrary.Get("result.title"), 64, new Color(0.15f, 0.17f, 0.25f));
            UiBuilder.Place((RectTransform)title.transform, new Vector2(0, 430), new Vector2(1600, 90));

            // 좌: 스케치, 우: AI 완성본 (비교 표시). 850×550 프레임 두 장 + 간격 40 = 가로 1740이라
            // 중심을 ±445로 벌리고, 제목(y 430)과 안 겹치게 y 95에 둔다 (프레임 상단 370 < 제목 하단 385)
            _sketch = CreateLabeledImage(background.transform, "Sketch",
                TextLibrary.Get("result.sketchLabel"), new Vector2(-445, 95));
            _result = CreateLabeledImage(background.transform, "Result",
                TextLibrary.Get("result.resultLabel"), new Vector2(445, 95));

            // 영상 준비 안내 (결과 라벨 아래, 하단 버튼 위). 영상이 도착하거나 실패하면 사라진다
            _videoPendingLabel = UiBuilder.CreateText(background.transform, "VideoPendingLabel",
                TextLibrary.Get("result.videoPending"), 26, new Color(0.45f, 0.48f, 0.55f));
            UiBuilder.Place((RectTransform)_videoPendingLabel.transform, new Vector2(445, -272), new Vector2(700, 40));

            // QR 한 벌 (좌하단). 업로드가 완료될 때만 보인다. 라벨(캡션) 하단 -255보다 아래에서 시작
            Image qrFrame = UiBuilder.CreateImage(background.transform, "QrFrame", Color.white);
            UiBuilder.Place((RectTransform)qrFrame.transform, new Vector2(-780, -390), new Vector2(210, 210));
            _qrGroup = qrFrame.gameObject;

            _qrImage = UiBuilder.CreateRawImage(qrFrame.transform, "QrImage");
            UiBuilder.Place((RectTransform)_qrImage.transform, Vector2.zero, new Vector2(194, 194));

            Text qrLabel = UiBuilder.CreateText(qrFrame.transform, "QrLabel",
                TextLibrary.Get("result.qrLabel"), 24, new Color(0.25f, 0.28f, 0.35f));
            UiBuilder.Place((RectTransform)qrLabel.transform, new Vector2(0, -125), new Vector2(320, 40));

            Button retry = UiBuilder.CreateButton(background.transform,
                TextLibrary.Get("result.retry"), new Color(0.35f, 0.75f, 0.45f), 40);
            UiBuilder.Place((RectTransform)retry.transform, new Vector2(-30, -400), new Vector2(360, 100));
            retry.onClick.AddListener(() => RetryRequested?.Invoke());

            _galleryButton = UiBuilder.CreateButton(background.transform,
                TextLibrary.Get("result.gallery"), new Color(0.95f, 0.70f, 0.25f), 40);
            UiBuilder.Place((RectTransform)_galleryButton.transform, new Vector2(400, -400), new Vector2(480, 100));
            _galleryButtonLabel = _galleryButton.GetComponentInChildren<Text>();
            _galleryButton.onClick.AddListener(OnGalleryClicked);

            // SetImages/ShowQr/ShowVideo가 Awake보다 먼저 호출됐다면 캐시해 둔 값을 지금 반영한다
            ApplyTextures();
            ApplyQr();
            ApplyGalleryButton();
            ApplyVideo();
        }

        private void OnDestroy()
        {
            if (_qrTexture != null) Destroy(_qrTexture);
        }

        private void OnDisable()
        {
            // 패널이 닫히면(대기 복귀·다시 그리기) 재생을 멈춘다 — 다음 세션에서 이전 영상이 이어 나오지 않도록
            StopVideo();
        }

        /// <summary>
        /// 비교 이미지 2장을 설정한다 (생성 성공 시 AppFlowManager가 호출).
        /// 새 세션의 결과이므로 QR·전시 신청 상태도 함께 초기화한다.
        /// </summary>
        /// <param name="sketch">관람객이 그린 그림 (색 레이어)</param>
        /// <param name="result">AI 생성 결과</param>
        public void SetImages(Texture sketch, Texture result)
        {
            _pendingSketch = sketch;
            _pendingResult = result;
            _pendingQrUrl = null;      // 이전 세션의 QR이 새 결과에 붙지 않도록
            _gallerySubmitted = false; // 전시 신청은 세션마다 다시 받는다
            _pendingVideoPath = null;  // 이전 세션의 영상도 새 결과에 붙지 않도록
            _videoPending = false;
            ApplyTextures();
            ApplyQr();
            ApplyGalleryButton();
            ApplyVideo();
        }

        /// <summary>
        /// 영상 생성이 백그라운드에서 시작됐음을 알린다 — 안내 문구를 띄운다 (AppFlowManager가 호출).
        /// </summary>
        public void ShowVideoPending()
        {
            _videoPending = true;
            ApplyVideo();
        }

        /// <summary>
        /// 완성된 결과 영상을 재생한다 — 결과 이미지를 영상으로 교체 (AppFlowManager가 생성 콜백에서 호출).
        /// </summary>
        /// <param name="mp4Path">SessionStore에 저장된 mp4 파일 경로</param>
        public void ShowVideo(string mp4Path)
        {
            _pendingVideoPath = mp4Path;
            _videoPending = false;
            ApplyVideo();
        }

        /// <summary>영상 생성 실패 시 안내 문구만 내린다 — 이미지가 그대로 남는다 (폴백)</summary>
        public void HideVideoPending()
        {
            _videoPending = false;
            ApplyVideo();
        }

        /// <summary>
        /// 업로드 완료된 다운로드 URL로 QR을 표시한다 (AppFlowManager가 업로드 콜백에서 호출).
        /// </summary>
        /// <param name="url">GCS 공개 다운로드 URL</param>
        public void ShowQr(string url)
        {
            _pendingQrUrl = url;
            ApplyQr();
        }

        // 캐시된 텍스처를 RawImage에 반영한다. RawImage가 아직 없으면(Awake 전) 캐시만 남기고,
        // Awake가 UI를 만든 뒤 다시 호출돼 반영된다.
        private void ApplyTextures()
        {
            if (_sketch != null) _sketch.texture = _pendingSketch;
            if (_result != null) _result.texture = _pendingResult;
        }

        private void ApplyQr()
        {
            if (_qrGroup == null) return;
            if (string.IsNullOrEmpty(_pendingQrUrl))
            {
                _qrGroup.SetActive(false);
                return;
            }

            if (_qrTexture != null) Destroy(_qrTexture);
            _qrTexture = QrCodeView.CreateTexture(_pendingQrUrl);
            if (_qrTexture == null)
            {
                _qrGroup.SetActive(false); // 인코딩 실패 — QR만 포기하고 체험은 계속
                return;
            }
            _qrImage.texture = _qrTexture;
            _qrGroup.SetActive(true);
        }

        private void ApplyGalleryButton()
        {
            if (_galleryButton == null) return;
            _galleryButton.interactable = !_gallerySubmitted;
            _galleryButtonLabel.text = TextLibrary.Get(_gallerySubmitted ? "result.galleryDone" : "result.gallery");
        }

        // 캐시된 영상 상태를 반영한다. UI가 아직 없으면(Awake 전) 캐시만 남는다 — Awake가 다시 부른다.
        private void ApplyVideo()
        {
            if (_videoPendingLabel != null)
                _videoPendingLabel.gameObject.SetActive(_videoPending);
            if (_result == null) return;

            if (string.IsNullOrEmpty(_pendingVideoPath))
            {
                // 영상 없음(새 세션·실패) — 재생을 멈추고 이미지 표시로 되돌린다
                StopVideo();
                _result.texture = _pendingResult;
                return;
            }

            if (_videoPlayer == null)
            {
                // APIOnly 모드: 준비 완료 후 VideoPlayer.texture를 RawImage에 꽂는 방식 —
                // RenderTexture를 영상 크기에 맞춰 따로 관리할 필요가 없다
                _videoPlayer = gameObject.AddComponent<VideoPlayer>();
                _videoPlayer.playOnAwake = false;
                _videoPlayer.isLooping = true; // 2~3초 클립을 계속 반복 — 전시 표시에 자연스럽다
                _videoPlayer.renderMode = VideoRenderMode.APIOnly;
                _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
                _videoPlayer.prepareCompleted += vp =>
                {
                    if (_result != null) _result.texture = vp.texture;
                    vp.Play();
                };
                _videoPlayer.errorReceived += (vp, message) =>
                {
                    // 재생 실패(코덱 등)는 이미지 폴백으로 조용히 복귀 (예외로 죽지 않기)
                    LogManager.Warn($"[ResultPanel] 영상 재생 실패 — 이미지 유지: {message}");
                    _pendingVideoPath = null;
                    ApplyVideo();
                };
            }

            _videoPlayer.Stop();
            _videoPlayer.url = "file://" + _pendingVideoPath.Replace('\\', '/');
            _videoPlayer.Prepare();
        }

        // 재생 중이면 멈춘다. RawImage 텍스처 복원은 호출부(ApplyVideo/SetImages)가 책임진다
        private void StopVideo()
        {
            if (_videoPlayer != null && (_videoPlayer.isPlaying || _videoPlayer.isPrepared))
                _videoPlayer.Stop();
        }

        private void OnGalleryClicked()
        {
            if (_gallerySubmitted) return; // 중복 신청 방지
            _gallerySubmitted = true;
            // 즉시 피드백으로 바꾼다 — 필터 판정은 백그라운드라 관람객은 결과를 기다리지 않는다 (계획서 10장)
            ApplyGalleryButton();
            GalleryRequested?.Invoke();
        }

        // 흰 테두리 + 이미지 + 하단 라벨 한 벌을 만든다
        private static RawImage CreateLabeledImage(Transform parent, string name, string label, Vector2 center)
        {
            Image frame = UiBuilder.CreateImage(parent, name + "Frame", Color.white);
            UiBuilder.Place((RectTransform)frame.transform, center, new Vector2(850, 550));

            // 이미지는 캔버스 원본 비율 3:2(768×512)를 유지한다 — 프레임(850×534 내부)에 꽉 채우면
            // 가로로 늘어나 보이므로 세로 기준 801×534, 남는 좌우는 흰 여백(액자 매트 느낌)
            RawImage image = UiBuilder.CreateRawImage(frame.transform, name);
            UiBuilder.Place((RectTransform)image.transform, Vector2.zero, new Vector2(801, 534));

            // 라벨은 프레임 하단(-180) 바로 아래, 하단 버튼(top -350) 위 구간에 배치
            Text caption = UiBuilder.CreateText(parent, name + "Label", label, 40, new Color(0.25f, 0.28f, 0.35f));
            UiBuilder.Place((RectTransform)caption.transform, center + new Vector2(0, -320), new Vector2(700, 60));

            return image;
        }
    }
}

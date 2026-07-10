using System;
using CarDrawing.Core;
using CarDrawing.Results;
using UnityEngine;
using UnityEngine.UI;

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

        // 표시할 데이터를 캐시한다. SetImages/ShowQr는 이 패널이 활성화(=Awake로 UI 생성)되기 전에
        // 호출될 수 있어(인수인계 §6 함정), 값을 캐시해 두고 어느 쪽이 먼저 실행돼도 최종 반영되게 한다.
        private Texture _pendingSketch;
        private Texture _pendingResult;
        private string _pendingQrUrl;

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

            // SetImages/ShowQr가 Awake보다 먼저 호출됐다면 캐시해 둔 값을 지금 반영한다
            ApplyTextures();
            ApplyQr();
            ApplyGalleryButton();
        }

        private void OnDestroy()
        {
            if (_qrTexture != null) Destroy(_qrTexture);
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
            ApplyTextures();
            ApplyQr();
            ApplyGalleryButton();
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

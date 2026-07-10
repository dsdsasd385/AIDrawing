using System;
using System.Collections;
using System.IO;
using CarDrawing.Core;
using CarDrawing.Gallery;
using UnityEngine;
using UnityEngine.UI;

namespace CarDrawing.UI
{
    /// <summary>
    /// 대기 화면(AttractPanel). 계획서 4장: 시작 안내 + 미니 슬라이드쇼 + 화면 클릭으로 체험 시작.
    /// UI 시스템에 속하며 AppFlowManager가 StartRequested 이벤트를 받아 그리기 화면으로 전환한다.
    /// 미니 슬라이드쇼는 GallerySlideshow와 같은 Gallery 폴더를 공유한다 (계획서 5장).
    /// </summary>
    public class AttractPanelController : MonoBehaviour
    {
        /// <summary>관람객이 화면을 클릭해 시작을 요청했을 때</summary>
        public event Action StartRequested;

        // 깜빡임 연출 대상 (시선 유도)
        private Text _startHint;

        // 미니 슬라이드쇼. 갤러리가 비어 있으면 통째로 숨긴다
        private GameObject _slideGroup;
        private RawImage _slideImage;
        private Texture2D _slideTexture; // 재사용 텍스처 (장시간 무인 운영의 메모리 누수 방지)
        private int _slideIndex = -1;

        private void Awake()
        {
            UiBuilder.Stretch((RectTransform)transform);

            Image background = UiBuilder.CreateImage(transform, "Background", new Color(0.10f, 0.12f, 0.20f));
            UiBuilder.Stretch((RectTransform)background.transform);

            // 화면 전체가 시작 버튼 역할을 한다 (계획서 4장: 클릭으로 시작)
            var startButton = background.gameObject.AddComponent<Button>();
            startButton.transition = Selectable.Transition.None;
            startButton.onClick.AddListener(() => StartRequested?.Invoke());

            Text title = UiBuilder.CreateText(background.transform, "Title",
                TextLibrary.Get("attract.title"), 96, Color.white);
            UiBuilder.Place((RectTransform)title.transform, new Vector2(0, 300), new Vector2(1600, 140));

            Text subtitle = UiBuilder.CreateText(background.transform, "Subtitle",
                TextLibrary.Get("attract.subtitle"), 44, new Color(0.75f, 0.80f, 0.90f));
            UiBuilder.Place((RectTransform)subtitle.transform, new Vector2(0, 190), new Vector2(1600, 70));

            // 미니 슬라이드쇼 (프레임 + 작품 + 캡션 한 벌). 768×512(3:2) 비율 유지
            Image frame = UiBuilder.CreateImage(background.transform, "GalleryFrame", Color.white);
            UiBuilder.Place((RectTransform)frame.transform, new Vector2(0, -80), new Vector2(590, 400));
            _slideGroup = frame.gameObject;

            _slideImage = UiBuilder.CreateRawImage(frame.transform, "GalleryImage");
            UiBuilder.Place((RectTransform)_slideImage.transform, Vector2.zero, new Vector2(570, 380));

            Text caption = UiBuilder.CreateText(frame.transform, "GalleryCaption",
                TextLibrary.Get("attract.gallery"), 30, new Color(0.75f, 0.80f, 0.90f));
            UiBuilder.Place((RectTransform)caption.transform, new Vector2(0, -235), new Vector2(600, 50));

            _startHint = UiBuilder.CreateText(background.transform, "StartHint",
                TextLibrary.Get("attract.start"), 48, new Color(1f, 0.85f, 0.30f));
            UiBuilder.Place((RectTransform)_startHint.transform, new Vector2(0, -420), new Vector2(1200, 80));

            _slideGroup.SetActive(false); // 갤러리에 작품이 생기면 코루틴이 켠다
        }

        private void OnEnable()
        {
            // 대기 화면이 보일 때만 돌린다 (첫 활성화 시 Awake가 먼저 실행돼 UI 참조가 준비된 상태)
            StartCoroutine(MiniSlideshowRoutine());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            if (_slideTexture != null) Destroy(_slideTexture);
        }

        private void Update()
        {
            // 시작 안내 문구 깜빡임 (전시장에서 시작 방법을 알아채도록)
            if (_startHint == null) return;
            Color c = _startHint.color;
            c.a = 0.45f + 0.55f * Mathf.PingPong(Time.unscaledTime, 1f);
            _startHint.color = c;
        }

        private IEnumerator MiniSlideshowRoutine()
        {
            while (true)
            {
                string[] files = GallerySlideshow.ListImages();
                if (files.Length == 0)
                {
                    _slideGroup.SetActive(false);
                }
                else
                {
                    _slideIndex = (_slideIndex + 1) % files.Length;
                    _slideGroup.SetActive(TryShow(files[_slideIndex]));
                }
                yield return new WaitForSecondsRealtime(
                    Mathf.Max(1f, ConfigManager.Config.gallery.attractSlideIntervalSeconds));
            }
        }

        private bool TryShow(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (_slideTexture == null) _slideTexture = new Texture2D(2, 2);
                if (!_slideTexture.LoadImage(bytes)) return false;
                _slideImage.texture = _slideTexture;
                return true;
            }
            catch (Exception e)
            {
                // 파일이 지워졌을 수 있다(관리자 삭제) — 다음 주기의 재검색이 목록을 갱신한다
                LogManager.Warn($"[Attract] 슬라이드 표시 실패: {Path.GetFileName(path)} — {e.Message}");
                return false;
            }
        }
    }
}

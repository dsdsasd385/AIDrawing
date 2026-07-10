using System;
using System.Collections;
using System.IO;
using CarDrawing.Core;
using CarDrawing.Results;
using CarDrawing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CarDrawing.Gallery
{
    /// <summary>
    /// Display 2 갤러리 월 슬라이드쇼 (계획서 5장). 갤러리 시스템의 유일한 컴포넌트로,
    /// Gallery 폴더(opt-in + 필터 통과 작품)를 주기적으로 재검색해 순환 표시한다.
    /// 폴더 감시 방식이라 SessionStore.AddToGallery의 파일 복사만으로 전시에 반영된다.
    /// GalleryCanvas(Screen Space-Camera + 전용 카메라)에 붙이며 키오스크 상태머신과는 독립적으로 돈다.
    /// </summary>
    public class GallerySlideshow : MonoBehaviour
    {
        private RawImage _image;
        private GameObject _imageFrame;
        private Text _emptyText;
        // 슬라이드 텍스처는 1장을 재사용한다 — 매 전환마다 새 텍스처를 만들면 장시간 무인 운영에서 메모리가 샌다
        private Texture2D _texture;
        private string[] _files = Array.Empty<string>();
        private int _index = -1;

        private void Awake()
        {
            BuildUi();
            // 런타임 생성 자식은 Default 레이어로 만들어진다 — 전용 카메라가 UI 레이어만 렌더링하므로
            // 캔버스(UI 레이어)의 레이어를 물려주지 않으면 Display 2에 아무것도 안 보인다
            SetLayerRecursively(transform, gameObject.layer);
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i), layer);
        }

        private void Start()
        {
            // Display 2가 물려 있으면 켠다. 에디터는 Game 뷰 탭 2개로 확인하므로 빌드에서만 (계획서 5장)
#if !UNITY_EDITOR
            if (Display.displays.Length > 1 && !Display.displays[1].active)
                Display.displays[1].Activate();
#endif
            StartCoroutine(SlideshowRoutine());
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        /// <summary>
        /// 갤러리 폴더의 이미지 경로 목록 (파일명 = 세션 ID 순 → 시간순).
        /// 대기 화면 미니 슬라이드쇼(AttractPanelController)도 이 목록을 공유한다.
        /// </summary>
        public static string[] ListImages()
        {
            try
            {
                if (!Directory.Exists(SessionStore.GalleryDir)) return Array.Empty<string>();
                string[] files = Directory.GetFiles(SessionStore.GalleryDir, "*.png");
                Array.Sort(files, StringComparer.Ordinal);
                return files;
            }
            catch (Exception e)
            {
                LogManager.Warn($"[Gallery] 폴더 검색 실패: {e.Message}");
                return Array.Empty<string>();
            }
        }

        // 런타임 UI 생성 — 다른 패널들과 같은 방침 (디자인 리소스 적용 전 임시, 인수인계 §6)
        private void BuildUi()
        {
            UiBuilder.Stretch((RectTransform)transform);

            Image background = UiBuilder.CreateImage(transform, "Background", new Color(0.05f, 0.06f, 0.10f));
            UiBuilder.Stretch((RectTransform)background.transform);

            Text title = UiBuilder.CreateText(background.transform, "Title",
                TextLibrary.Get("gallery.title"), 64, Color.white);
            UiBuilder.Place((RectTransform)title.transform, new Vector2(0, 470), new Vector2(1600, 90));

            // 흰 프레임 + 작품 이미지. 768×512(3:2) 원본과 같은 비율로 잡아 왜곡을 피한다
            Image frame = UiBuilder.CreateImage(background.transform, "Frame", Color.white);
            UiBuilder.Place((RectTransform)frame.transform, new Vector2(0, -40), new Vector2(1460, 980));
            _imageFrame = frame.gameObject;

            _image = UiBuilder.CreateRawImage(frame.transform, "Artwork");
            UiBuilder.Place((RectTransform)_image.transform, Vector2.zero, new Vector2(1440, 960));

            _emptyText = UiBuilder.CreateText(background.transform, "EmptyText",
                TextLibrary.Get("gallery.empty"), 48, new Color(0.6f, 0.65f, 0.75f));
            UiBuilder.Place((RectTransform)_emptyText.transform, new Vector2(0, -40), new Vector2(1400, 200));

            _imageFrame.SetActive(false);
        }

        private IEnumerator SlideshowRoutine()
        {
            while (true)
            {
                GalleryConfig cfg = ConfigManager.Config.gallery;
                _files = ListImages();

                if (_files.Length == 0)
                {
                    ShowEmpty();
                    // 빈 갤러리는 첫 작품이 빨리 반영되도록 짧은 주기로 재검색한다
                    yield return new WaitForSecondsRealtime(Mathf.Max(1f, cfg.rescanIntervalSeconds));
                    continue;
                }

                _index = (_index + 1) % _files.Length;
                if (TryShow(_files[_index]))
                    yield return new WaitForSecondsRealtime(Mathf.Max(1f, cfg.slideIntervalSeconds));
                // 표시 실패(파일 삭제 등)면 대기 없이 다음 파일로 넘어간다
            }
        }

        private bool TryShow(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (_texture == null) _texture = new Texture2D(2, 2);
                if (!_texture.LoadImage(bytes)) throw new Exception("PNG 해석 실패");

                _image.texture = _texture;
                _imageFrame.SetActive(true);
                _emptyText.gameObject.SetActive(false);
                return true;
            }
            catch (Exception e)
            {
                // 슬라이드쇼 도중 관리자가 파일을 지울 수 있다(계획서 11장) — 다음 재검색이 목록을 갱신한다
                LogManager.Warn($"[Gallery] 이미지 표시 실패: {Path.GetFileName(path)} — {e.Message}");
                return false;
            }
        }

        private void ShowEmpty()
        {
            _imageFrame.SetActive(false);
            _emptyText.gameObject.SetActive(true);
        }
    }
}

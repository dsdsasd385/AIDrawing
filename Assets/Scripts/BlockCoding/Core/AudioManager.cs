using System.Collections.Generic;
using UnityEngine;

namespace EarthCoding.Core
{
    /// <summary>
    /// 사운드 재생 매니저. (작업계획서 7장, 8장 대응)
    /// BGM 1채널 + 효과음 1채널 구조. 리소스는 별도 제작 예정이므로
    /// Resources/Sound 폴더에서 이름으로 클립을 찾고, 없으면 조용히 넘어간다(오류 대응).
    /// Config 의 UseBgm / UseSfx 설정을 따르므로 운영자가 사운드를 끌 수 있다.
    /// GameManager 가 생성하며 프로그램 전체에서 하나만 존재한다.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        /// <summary>전역 접근용 싱글턴 인스턴스</summary>
        public static AudioManager Instance { get; private set; }

        /// <summary>배경음악 전용 오디오 소스 (루프 재생)</summary>
        private AudioSource _bgmSource;

        /// <summary>효과음 전용 오디오 소스 (원샷 재생)</summary>
        private AudioSource _sfxSource;

        /// <summary>이름 → 클립 캐시. Resources.Load 반복 호출을 피한다.</summary>
        private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();

        /// <summary>싱글턴 등록 및 오디오 소스 2채널 생성</summary>
        private void Awake()
        {
            Instance = this;
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;   // BGM 은 반복 재생
            _sfxSource = gameObject.AddComponent<AudioSource>();
        }

        /// <summary>
        /// 배경음악을 재생한다. Config 에서 BGM 이 꺼져 있거나 클립이 없으면 무시한다.
        /// </summary>
        /// <param name="clipName">Resources/Sound 폴더 안의 클립 이름</param>
        public void PlayBgm(string clipName)
        {
            if (!DataManager.Config.UseBgm) return;
            var clip = LoadClip(clipName);
            if (clip == null) return;

            // 같은 곡이 이미 재생 중이면 다시 시작하지 않는다
            if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;
            _bgmSource.clip = clip;
            _bgmSource.Play();
        }

        /// <summary>배경음악을 정지한다.</summary>
        public void StopBgm() => _bgmSource.Stop();

        /// <summary>
        /// 효과음을 1회 재생한다. Config 에서 효과음이 꺼져 있거나 클립이 없으면 무시한다.
        /// </summary>
        /// <param name="clipName">Resources/Sound 폴더 안의 클립 이름</param>
        public void PlaySfx(string clipName)
        {
            if (!DataManager.Config.UseSfx) return;
            var clip = LoadClip(clipName);
            if (clip != null) _sfxSource.PlayOneShot(clip);
        }

        /// <summary>
        /// Resources/Sound 에서 클립을 찾는다. 없으면 null 을 반환하고
        /// 최초 1회만 경고 로그를 남긴다. (리소스 없음 → 프로그램은 계속 동작)
        /// </summary>
        /// <param name="clipName">클립 이름 (확장자 제외)</param>
        private AudioClip LoadClip(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return null;
            if (_clipCache.TryGetValue(clipName, out var cached)) return cached;

            var clip = Resources.Load<AudioClip>($"Sound/{clipName}");
            if (clip == null)
                LogManager.Write("Warning", $"효과음 없음: {clipName} (재생 생략)");

            // null 도 캐시하여 같은 경고가 반복되지 않게 한다
            _clipCache[clipName] = clip;
            return clip;
        }
    }
}

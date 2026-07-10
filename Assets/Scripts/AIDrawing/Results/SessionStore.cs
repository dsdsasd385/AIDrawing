using System;
using System.IO;
using UnityEngine;

namespace CarDrawing.Results
{
    /// <summary>
    /// 체험 세션 산출물(스케치/결과 이미지)의 로컬 저장을 담당하는 정적 저장소.
    /// 결과 처리 시스템에 속하며, 계획서 9장의 폴더 구조를 만든다.
    /// - Sessions: 모든 체험 기록 (항상 저장)
    /// - Gallery: 필터 통과 + 관람객 opt-in 작품 (슬라이드쇼가 감시)
    /// - Quarantine: 필터에 걸린 opt-in 작품 (관리자가 복원 가능)
    /// </summary>
    public static class SessionStore
    {
        /// <summary>전체 체험 기록 폴더</summary>
        public static string SessionsDir => Path.Combine(Application.persistentDataPath, "Sessions");
        /// <summary>갤러리 전시 폴더 (GallerySlideshow가 이 폴더를 감시한다)</summary>
        public static string GalleryDir => Path.Combine(Application.persistentDataPath, "Gallery");
        /// <summary>필터 격리 폴더</summary>
        public static string QuarantineDir => Path.Combine(Application.persistentDataPath, "Quarantine");

        /// <summary>
        /// 새 세션 ID를 발급한다. 파일명 접두사로 쓰인다 (예: 20260708_143012).
        /// </summary>
        public static string NewSessionId()
        {
            return DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }

        /// <summary>
        /// 스케치 한 쌍(선/색 레이어)을 Sessions 폴더에 저장한다.
        /// </summary>
        /// <param name="sessionId">NewSessionId()로 발급한 세션 ID</param>
        /// <param name="linePng">선 레이어 PNG (ControlNet 입력)</param>
        /// <param name="colorPng">색 레이어 PNG (img2img 입력, 관람객이 그린 그림)</param>
        /// <returns>(선 파일 경로, 색 파일 경로)</returns>
        public static (string linePath, string colorPath) SaveSketchPair(string sessionId, byte[] linePng, byte[] colorPng)
        {
            Directory.CreateDirectory(SessionsDir);
            string linePath = Path.Combine(SessionsDir, sessionId + "_line.png");
            string colorPath = Path.Combine(SessionsDir, sessionId + "_sketch.png");
            File.WriteAllBytes(linePath, linePng);
            File.WriteAllBytes(colorPath, colorPng);
            return (linePath, colorPath);
        }

        /// <summary>
        /// 생성 결과 이미지를 Sessions 폴더에 저장한다.
        /// </summary>
        /// <param name="sessionId">세션 ID</param>
        /// <param name="resultPng">ComfyUI가 생성한 결과 PNG</param>
        /// <returns>저장된 파일 경로</returns>
        public static string SaveResult(string sessionId, byte[] resultPng)
        {
            Directory.CreateDirectory(SessionsDir);
            string path = Path.Combine(SessionsDir, sessionId + "_result.png");
            File.WriteAllBytes(path, resultPng);
            return path;
        }

        /// <summary>세션의 결과 이미지 경로 (Sessions 폴더 기준). 파일 존재는 보장하지 않는다</summary>
        public static string ResultPath(string sessionId) => Path.Combine(SessionsDir, sessionId + "_result.png");
        /// <summary>세션의 스케치(색 레이어) 경로. VLM 필터가 낙서 원본을 검사할 때 쓴다</summary>
        public static string SketchPath(string sessionId) => Path.Combine(SessionsDir, sessionId + "_sketch.png");

        /// <summary>
        /// 결과 이미지를 갤러리 폴더로 복사한다 (opt-in + 필터 통과 작품, 계획서 9-3).
        /// GallerySlideshow가 폴더를 감시하므로 복사만으로 전시에 반영된다.
        /// </summary>
        /// <param name="sessionId">세션 ID</param>
        /// <returns>갤러리에 복사된 파일 경로. 원본이 없으면 null</returns>
        public static string AddToGallery(string sessionId) => CopyResultTo(GalleryDir, sessionId);

        /// <summary>
        /// 결과 이미지를 격리 폴더로 복사한다 (필터에 걸린 opt-in 작품, 계획서 10장).
        /// 관리자 모드에서 갤러리로 복원할 수 있다.
        /// </summary>
        /// <param name="sessionId">세션 ID</param>
        /// <returns>격리 폴더에 복사된 파일 경로. 원본이 없으면 null</returns>
        public static string AddToQuarantine(string sessionId) => CopyResultTo(QuarantineDir, sessionId);

        // 결과 PNG를 대상 폴더로 복사한다. 파일명을 세션 ID 그대로 유지해 Sessions와 추적이 이어지게 한다
        private static string CopyResultTo(string dir, string sessionId)
        {
            string source = ResultPath(sessionId);
            if (!File.Exists(source)) return null;
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, sessionId + "_result.png");
            File.Copy(source, dest, true);
            return dest;
        }
    }
}

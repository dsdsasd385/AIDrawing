using System;

namespace CarDrawing.Generation
{
    /// <summary>
    /// 결과 이미지를 짧은 영상(mp4)으로 만드는 생성기 계약 (마일스톤 ⑥).
    /// IResultUploader와 같은 교체 가능 구조 — 지금은 로컬 ComfyUI(AnimateDiff) 구현이 기본이고,
    /// 전시 품질·속도가 미달이면 외부 API 구현으로 갈아 끼운다 (계획서 §18 2026-07-13 결정).
    /// 어떤 구현이든 실패는 콜백으로만 알리고 예외를 밖으로 던지지 않는다 (예외로 죽지 않기).
    /// </summary>
    public interface IVideoGenerator
    {
        /// <summary>설정이 갖춰져 영상 생성이 가능한 상태인지. false면 호출부가 이미지-only로 진행한다</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// 결과 이미지를 움직이는 영상으로 만든다.
        /// </summary>
        /// <param name="sessionId">세션 ID. 업로드 파일명 충돌 방지·늦은 콜백 판별에 쓴다</param>
        /// <param name="resultPng">직전에 생성된 결과 이미지 (영상의 기반)</param>
        /// <param name="linePng">관람객 선 레이어 (형태 고정용 ControlNet 입력)</param>
        /// <param name="style">선택된 스타일. 로컬 구현이 영상에도 화풍을 유지하는 데 쓴다(LoRA·픽셀화). 외부 API 구현은 무시해도 된다</param>
        /// <param name="onSuccess">완성된 mp4 바이트를 받는 콜백</param>
        /// <param name="onFailure">실패 사유(로그용 한국어)를 받는 콜백 — 호출부는 이미지 폴백 유지</param>
        void Generate(string sessionId, byte[] resultPng, byte[] linePng, StylePreset style,
            Action<byte[]> onSuccess, Action<string> onFailure);
    }
}

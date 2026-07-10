using System;

namespace CarDrawing.Results
{
    /// <summary>
    /// 결과 이미지 업로더 인터페이스 (계획서 9-2).
    /// 업로더를 인터페이스 뒤에 두어 GCS가 아닌 다른 스토리지로 교체할 수 있게 한다.
    /// 업로드는 부가 기능이므로 실패해도 체험 흐름을 막지 않는 계약이다 (콜백에 null 전달).
    /// </summary>
    public interface IResultUploader
    {
        /// <summary>업로드 가능 상태인지 (설정·자격 증명이 갖춰졌는지). false면 QR 기능 전체를 숨긴다</summary>
        bool IsConfigured { get; }

        /// <summary>
        /// 결과 PNG를 비동기로 업로드한다.
        /// </summary>
        /// <param name="sessionId">세션 ID (업로드 파일명에 포함)</param>
        /// <param name="png">업로드할 결과 PNG 바이트</param>
        /// <param name="onDone">공개 다운로드 URL을 받는 콜백. 실패 시 null</param>
        void Upload(string sessionId, byte[] png, Action<string> onDone);
    }
}

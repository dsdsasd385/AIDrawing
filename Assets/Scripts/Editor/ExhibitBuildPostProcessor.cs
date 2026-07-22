using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CarDrawing.EditorTools
{
    /// <summary>
    /// 빌드 후처리 — 전시장 운영에 필요한 파일을 exe 옆에 자동 복사한다 (계획서 12장).
    /// StreamingAssets에 못 넣는 것들이라 빌드가 끝난 뒤 따로 옮겨야 한다:
    /// - Tools\*.bat : 워치독 재시작(run_comfyui.bat)·부팅 자동 시작(start_exhibit.bat).
    ///                 워치독의 restartCommand가 exe 기준 상대 경로라 여기 없으면 재시작이 조용히 꺼진다
    /// - Config\*.json : B2/GCS 키. StreamingAssets(=배포물 내부 노출)에 두지 않는 원칙 (인수인계 §7)
    /// 복사에 실패해도 빌드는 성공으로 둔다 — 없으면 QR·재시작만 꺼지고 체험 자체는 돈다.
    /// </summary>
    public class ExhibitBuildPostProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
                report.summary.platform != BuildTarget.StandaloneWindows) return;

            string outputPath = report.summary.outputPath;           // ...\Build\CarDrawing.exe
            string buildDir = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(buildDir)) return;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            int copied = 0;
            copied += CopyFolder(Path.Combine(projectRoot, "Tools"), Path.Combine(buildDir, "Tools"), "*.bat");
            // 키 파일만 가져간다 — Config\ 에는 이전 프로젝트(지구환경코딩)의 Config.json도 있어서
            // *.json으로 긁으면 이 앱과 무관한 설정이 배포물에 섞인다
            copied += CopyFolder(Path.Combine(projectRoot, "Config"), Path.Combine(buildDir, "Config"), "*-key.json");

            Debug.Log($"[Build] 운영 파일 {copied}개를 빌드 폴더에 복사했습니다: {buildDir}");
        }

        // 없는 폴더는 조용히 건너뛴다 (개발 PC에 키가 없을 수 있다 — 그때는 QR만 꺼진 빌드가 나온다)
        private static int CopyFolder(string sourceDir, string destDir, string pattern)
        {
            if (!Directory.Exists(sourceDir))
            {
                Debug.LogWarning($"[Build] 복사할 폴더 없음 — 건너뜀: {sourceDir}");
                return 0;
            }

            int count = 0;
            try
            {
                Directory.CreateDirectory(destDir);
                foreach (string file in Directory.GetFiles(sourceDir, pattern))
                {
                    File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
                    count++;
                }
            }
            catch (IOException e)
            {
                // 빌드를 실패시키지 않는다 — 운영자가 수동 복사로 복구할 수 있다
                Debug.LogError($"[Build] 복사 실패({sourceDir} → {destDir}): {e.Message}");
            }
            return count;
        }
    }
}

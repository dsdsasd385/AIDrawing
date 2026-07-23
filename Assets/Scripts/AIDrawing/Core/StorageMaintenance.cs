using System;
using System.IO;
using System.Text.RegularExpressions;
using CarDrawing.Results;

namespace CarDrawing.Core
{
    /// <summary>앱 전용 로컬 파일만 대상으로 시작 시 한 번 보존기간을 적용한다.</summary>
    public static class StorageMaintenance
    {
        private static readonly Regex SessionFile = new Regex(
            @"^\d{8}_\d{6}(?:_\d{3}_[0-9a-fA-F]{8})?_(line|color|sketch|result|vline|vresult)\.(png|mp4)$",
            RegexOptions.Compiled);
        private static bool _ran;

        public static void RunOnce()
        {
            if (_ran) return;
            _ran = true;

            StorageConfig cfg = ConfigManager.Config.storage;
            if (cfg == null || !cfg.cleanupEnabled) return;

            int removed = 0;
            removed += DeleteExpired(SessionStore.SessionsDir, cfg.sessionRetentionDays,
                name => SessionFile.IsMatch(name));
            removed += DeleteExpired(LogManager.LogsDir, cfg.logRetentionDays,
                name => name.EndsWith(".log", StringComparison.OrdinalIgnoreCase));

            string comfyRoot = ResolveComfyRoot(cfg.comfyUiRootPath);
            removed += DeleteExpired(Path.Combine(comfyRoot, "input"), cfg.comfyUiRetentionDays,
                name => SessionFile.IsMatch(name));
            removed += DeleteExpired(Path.Combine(comfyRoot, "output"), cfg.comfyUiRetentionDays,
                name => name.StartsWith("car_result", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("car_video", StringComparison.OrdinalIgnoreCase));

            LogManager.Info($"[Storage] 보존기간 정리 완료: {removed}개 삭제");
        }

        private static int DeleteExpired(string dir, int retentionDays, Func<string, bool> isOwnedFile)
        {
            if (retentionDays <= 0 || !Directory.Exists(dir)) return 0;

            int removed = 0;
            DateTime cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            try
            {
                foreach (string path in Directory.GetFiles(dir))
                {
                    string name = Path.GetFileName(path);
                    if (!isOwnedFile(name) || File.GetLastWriteTimeUtc(path) >= cutoff) continue;
                    try
                    {
                        File.Delete(path);
                        removed++;
                    }
                    catch (Exception e)
                    {
                        LogManager.Warn($"[Storage] 만료 파일 삭제 실패({name}): {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                LogManager.Warn($"[Storage] 보존기간 조회 실패({dir}): {e.Message}");
            }
            return removed;
        }

        private static string ResolveComfyRoot(string configured)
        {
            string fromEnvironment = Environment.GetEnvironmentVariable("COMFYUI_HOME");
            if (!string.IsNullOrWhiteSpace(fromEnvironment)) return Path.GetFullPath(fromEnvironment);

            string value = Environment.ExpandEnvironmentVariables(configured ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(value)) return Path.GetFullPath(value);
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ComfyUI");
        }
    }
}

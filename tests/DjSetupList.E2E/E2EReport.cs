using System.Diagnostics;

namespace DjSetupList.E2E;

public static class E2EReport
{
    private const string StartMarker = "<!-- e2e-result:start -->";
    private const string EndMarker = "<!-- e2e-result:end -->";

    public static void Publish(string repoRoot, string recordedWebm)
    {
        var docs = Path.Combine(repoRoot, "docs", "e2e");
        Directory.CreateDirectory(docs);

        var webm = Path.Combine(docs, "setup-list-e2e.webm");
        File.Copy(recordedWebm, webm, overwrite: true);

        var gif = Path.Combine(docs, "setup-list-e2e.gif");
        ConvertToGif(webm, gif);

        var readmePath = Path.Combine(repoRoot, "README.md");
        var readme = File.ReadAllText(readmePath);
        var start = readme.IndexOf(StartMarker, StringComparison.Ordinal);
        var end = readme.IndexOf(EndMarker, StringComparison.Ordinal);
        if (start < 0 || end < start)
            throw new InvalidOperationException("README.md に e2e-result マーカーがありません。");

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var block = string.Join("\n",
        [
            StartMarker,
            $"最終実行: {now:yyyy-MM-dd HH:mm} JST",
            "",
            "![E2E: リスト作成、機材チェック、ローカル音源の添付](docs/e2e/setup-list-e2e.gif)",
            "",
            "[WebM で再生](docs/e2e/setup-list-e2e.webm)",
            EndMarker
        ]);

        end += EndMarker.Length;
        File.WriteAllText(readmePath, readme[..start] + block + readme[end..]);
    }

    private static void ConvertToGif(string webm, string gif)
    {
        var ffmpeg = ResolveFfmpeg();
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(webm);
        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add("fps=6,scale=800:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=96[p];[s1][p]paletteuse");
        psi.ArgumentList.Add(gif);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg を起動できませんでした。");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 || !File.Exists(gif))
            throw new InvalidOperationException($"GIF の作成に失敗しました。{Environment.NewLine}{error}");
    }

    private static string ResolveFfmpeg()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            "ffmpeg",
            Path.Combine(home, ".local", "bin", "ffmpeg"),
            "/usr/local/bin/ffmpeg",
            "/opt/homebrew/bin/ffmpeg"
        };

        foreach (var candidate in candidates)
        {
            if (candidate == "ffmpeg")
            {
                var path = Environment.GetEnvironmentVariable("PATH") ?? "";
                if (path.Split(Path.PathSeparator).Any(dir => File.Exists(Path.Combine(dir, "ffmpeg"))))
                    return "ffmpeg";
                continue;
            }

            if (File.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException("ffmpeg が見つかりません。GIF を README に出すには ffmpeg が必要です。");
    }
}

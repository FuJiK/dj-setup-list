using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DjSetupList.E2E;

public sealed class E2EServer : IAsyncLifetime
{
    private readonly StringBuilder _log = new();
    private Process? _process;
    private string? _workspace;

    public string BaseUrl { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var installExit = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (installExit != 0)
            throw new InvalidOperationException($"Playwright の Chromium インストールに失敗しました (exit {installExit})。");

        var repo = FindRepoRoot();
        var library = Path.Combine(repo, "sample-library");
        _workspace = Directory.CreateTempSubdirectory("dj-e2e-lists").FullName;
        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var psi = new ProcessStartInfo
        {
            FileName = DotnetPath(),
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(Path.Combine(repo, "src", "DjSetupList.Web"));
        psi.ArgumentList.Add("--no-build");
        psi.ArgumentList.Add("--urls");
        psi.ArgumentList.Add(BaseUrl);
        psi.Environment["DJ_LOCAL_LIBRARY"] = library;
        psi.Environment["Workspace__Root"] = _workspace;
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => AppendLog(e.Data);
        _process.ErrorDataReceived += (_, e) => AppendLog(e.Data);
        if (!_process.Start())
            throw new InvalidOperationException("Web サーバーを起動できませんでした。");
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
                throw new InvalidOperationException($"Web サーバーが終了しました (exit {_process.ExitCode}).{Environment.NewLine}{_log}");

            try
            {
                var response = await http.GetAsync($"{BaseUrl}/api/health");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Web サーバーが {BaseUrl} で応答しませんでした。{Environment.NewLine}{_log}");
    }

    public Task DisposeAsync()
    {
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _process?.Dispose();
            if (_workspace is not null && Directory.Exists(_workspace))
                Directory.Delete(_workspace, recursive: true);
        }

        return Task.CompletedTask;
    }

    private void AppendLog(string? line)
    {
        if (line is null) return;
        lock (_log) _log.AppendLine(line);
    }

    private static string DotnetPath()
    {
        var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(root))
        {
            var candidate = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (File.Exists(candidate)) return candidate;
        }

        return "dotnet";
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DjSetupList.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("DjSetupList.sln が見つかりません。");
    }
}

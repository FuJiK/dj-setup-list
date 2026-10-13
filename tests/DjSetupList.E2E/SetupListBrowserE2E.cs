using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace DjSetupList.E2E;

public sealed class SetupListBrowserE2E : IClassFixture<E2EServer>
{
    private readonly E2EServer _server;

    public SetupListBrowserE2E(E2EServer server) => _server = server;

    [Fact]
    public async Task Dj_can_build_a_list_check_gear_and_attach_local_music()
    {
        var artifacts = Path.Combine(FindRepoRoot(), "e2e-artifacts");
        Directory.CreateDirectory(artifacts);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = 350
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            RecordVideoDir = artifacts,
            RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 800 }
        });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15_000);

        await page.GotoAsync(_server.BaseUrl);
        await Expect(page.Locator("#view-home h1")).ToContainTextAsync("現場前のセットアップ");
        await page.WaitForTimeoutAsync(600);

        await page.Locator("#view-home [data-nav=create]").ClickAsync();
        await page.Locator("#create-form select[name=templateId]").SelectOptionAsync("club");
        await page.Locator("#create-form input[name=eventName]").FillAsync("Friday Night");
        await page.Locator("#create-form input[name=venue]").FillAsync("Club Neon");
        await page.Locator("#create-form input[name=date]").FillAsync("2026-10-15");
        await page.Locator("#create-form input[name=startTime]").FillAsync("22:00");
        await page.Locator("#create-form textarea[name=notes]").FillAsync("搬入口は裏手");
        await page.Locator("#create-form button[type=submit]").ClickAsync();

        var detail = page.Locator("#view-detail");
        await Expect(detail.Locator("h2")).ToHaveTextAsync("Friday Night");
        await Expect(detail).ToContainTextAsync("Club Neon");
        await Expect(detail).ToContainTextAsync("進捗 0/15");
        await Expect(detail).ToContainTextAsync("音源はまだ添付されていません");
        await page.WaitForTimeoutAsync(700);

        await detail.Locator("[data-item]").Filter(new() { HasText = "USB（メイン）" }).ClickAsync();
        await Expect(detail).ToContainTextAsync("進捗 1/15");
        await page.WaitForTimeoutAsync(500);

        await page.Locator("nav [data-nav=music]").ClickAsync();
        await page.Locator("#search-form select[name=source]").SelectOptionAsync("local");
        await page.Locator("#search-form input[name=q]").FillAsync("demo");
        await page.Locator("#search-form button[type=submit]").ClickAsync();

        var results = page.Locator("#search-results");
        await Expect(results).ToContainTextAsync("demo-set");
        await Expect(results).ToContainTextAsync("Warmup");
        await results.Locator(".attach-pl").ClickAsync();

        await Expect(detail).ToContainTextAsync("Opening Track");
        await Expect(detail).ToContainTextAsync("Peak Time");
        await Expect(detail).ToContainTextAsync("offline");
        await page.WaitForTimeoutAsync(800);

        await page.Locator("nav [data-nav=lists]").ClickAsync();
        var lists = page.Locator("#lists");
        await Expect(lists).ToContainTextAsync("Friday Night");
        await Expect(lists).ToContainTextAsync("Club Neon");
        await Expect(lists).ToContainTextAsync("1/15");
        await Expect(lists).ToContainTextAsync("音源 2曲");

        await lists.Locator(".list-row").ClickAsync();
        await Expect(detail.Locator("h2")).ToHaveTextAsync("Friday Night");
        await Expect(detail.Locator(".check.on")).ToHaveCountAsync(1);
        await page.WaitForTimeoutAsync(600);

        await page.Locator("nav [data-nav=sources]").ClickAsync();
        var sources = page.Locator("#sources");
        await Expect(sources).ToContainTextAsync("ローカル");
        await Expect(sources).ToContainTextAsync("利用可");
        await Expect(sources).ToContainTextAsync("Spotify");
        await Expect(sources).ToContainTextAsync("未設定");
        await page.WaitForTimeoutAsync(800);

        await page.Locator("nav [data-nav=home]").ClickAsync();
        await Expect(page.Locator("#view-home")).ToBeVisibleAsync();
        await page.WaitForTimeoutAsync(400);

        var video = page.Video ?? throw new InvalidOperationException("動画が開始されていません。");
        await context.CloseAsync();
        var recorded = await video.PathAsync();
        if (!ShouldSkipE2EReport())
            E2EReport.Publish(FindRepoRoot(), recorded);
        if (File.Exists(recorded))
            File.Delete(recorded);
    }

    /// <summary>CI の PR ゲートでは README / GIF を更新しない（<c>DJ_E2E_SKIP_REPORT=1</c>）。</summary>
    private static bool ShouldSkipE2EReport()
    {
        var value = Environment.GetEnvironmentVariable("DJ_E2E_SKIP_REPORT");
        return string.Equals(value, "1", StringComparison.Ordinal)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
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

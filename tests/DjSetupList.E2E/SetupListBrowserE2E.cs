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
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();
        page.SetDefaultTimeout(15_000);

        await page.GotoAsync(_server.BaseUrl);
        await Expect(page.Locator("#view-home h1")).ToContainTextAsync("現場前のセットアップ");

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

        await detail.Locator("[data-item]").Filter(new() { HasText = "USB（メイン）" }).ClickAsync();
        await Expect(detail).ToContainTextAsync("進捗 1/15");

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

        await page.Locator("nav [data-nav=lists]").ClickAsync();
        var lists = page.Locator("#lists");
        await Expect(lists).ToContainTextAsync("Friday Night");
        await Expect(lists).ToContainTextAsync("Club Neon");
        await Expect(lists).ToContainTextAsync("1/15");
        await Expect(lists).ToContainTextAsync("音源 2曲");

        await lists.Locator(".list-row").ClickAsync();
        await Expect(detail.Locator("h2")).ToHaveTextAsync("Friday Night");
        await Expect(detail.Locator(".check.on")).ToHaveCountAsync(1);

        await page.Locator("nav [data-nav=sources]").ClickAsync();
        var sources = page.Locator("#sources");
        await Expect(sources).ToContainTextAsync("ローカル");
        await Expect(sources).ToContainTextAsync("利用可");
        await Expect(sources).ToContainTextAsync("Spotify");
        await Expect(sources).ToContainTextAsync("未設定");

        await page.Locator("nav [data-nav=home]").ClickAsync();
        await Expect(page.Locator("#view-home")).ToBeVisibleAsync();
    }
}

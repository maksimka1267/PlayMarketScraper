using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlayMarketScraper.Application.UseCases;
using PlayMarketScraper.Domain.Models;
using PlayMarketScraper.Infrastructure.Gateway;
using PlayMarketScraper.Infrastructure.Http;
using PlayMarketScraper.Infrastructure.Options;
using PlayMarketScraper.Infrastructure.Parsing;
using PlayMarketScraper.Infrastructure.Payload;
using PlayMarketScraper.Infrastructure.Session;

internal static class Program
{
    private enum RunMode
    {
        Store,
        Enterprise
    }

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        try
        {
            var mode = ParseMode(args);
            var cfg = BuildConfig();

            // Спільні налаштування
            var baseUrl = cfg["PlayMarket:BaseUrl"] ?? "https://play.google.com";
            var hl = cfg["PlayMarket:Hl"] ?? "en";
            var defaultCountry = (cfg["PlayMarket:DefaultCountry"] ?? "US").ToUpperInvariant();

            var defaultMaxPages = TryInt(cfg["Cli:DefaultMaxPages"], 50);
            var requestTimeoutSeconds = TryInt(cfg["Cli:RequestTimeoutSeconds"], 60);

            // Ввід
            Console.Write("Keyword (наприклад: home, vpn, music): ");
            var keyword = (Console.ReadLine() ?? "").Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                Console.Error.WriteLine("Keyword is empty.");
                return 1;
            }

            Console.Write($"Country (ISO2, напр. US, UA) (default {defaultCountry}): ");
            var country = (Console.ReadLine() ?? "").Trim();
            if (string.IsNullOrWhiteSpace(country)) country = defaultCountry;
            country = country.ToUpperInvariant();

            Console.Write($"Max pages (default {defaultMaxPages}): ");
            var maxPagesText = (Console.ReadLine() ?? "").Trim();
            var maxPages = string.IsNullOrWhiteSpace(maxPagesText)
                ? defaultMaxPages
                : (int.TryParse(maxPagesText, out var mp) ? mp : defaultMaxPages);

            if (maxPages <= 0) maxPages = defaultMaxPages;

            // Запуск у вибраному режимі
            return mode switch
            {
                RunMode.Store => await RunStoreAsync(baseUrl, hl, keyword, country, maxPages, requestTimeoutSeconds, cfg),
                RunMode.Enterprise => await RunEnterpriseAsync(keyword, country, maxPages, requestTimeoutSeconds),
                _ => 1
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static RunMode ParseMode(string[] args)
    {
        // Режим можна задати як:
        // --mode store
        // --mode enterprise
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var v = (args[++i] ?? "").Trim().ToLowerInvariant();
                if (v == "enterprise") return RunMode.Enterprise;
                return RunMode.Store;
            }
        }

        // Якщо не передали — за замовчуванням store (щоб без cookie працювало одразу)
        return RunMode.Store;
    }

    private static async Task<int> RunStoreAsync(
        string baseUrl,
        string hl,
        string keyword,
        string country,
        int maxPages,
        int requestTimeoutSeconds,
        IConfiguration cfg)
    {
        // Публічний режим: парсимо HTML з /store/search
        var cookie = cfg["PlayMarket:Cookie"]; // optional

        using var http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds)
        };

        // Заголовки “як браузер”
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", $"{hl},{hl.Split('-')[0]};q=0.9,en;q=0.8");

        if (!string.IsNullOrWhiteSpace(cookie))
            http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);

        const string sourcePath = "/store/search";
        const int pageSize = 48;

        var ordered = new List<string>(capacity: 512);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int page = 0; page < maxPages; page++)
        {
            var start = page * pageSize;
            var url = $"{sourcePath}?q={Uri.EscapeDataString(keyword)}&c=apps&hl={Uri.EscapeDataString(hl)}&gl={Uri.EscapeDataString(country)}&start={start}";

            var html = await http.GetStringAsync(url);

            var ids = ExtractPackageIdsFromHtml(html);

            var added = 0;
            foreach (var id in ids)
            {
                if (seen.Add(id))
                {
                    ordered.Add(id);
                    added++;
                }
            }

            if (added == 0)
                break;
        }

        Console.WriteLine(JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static async Task<int> RunEnterpriseAsync(
        string keyword,
        string country,
        int maxPages,
        int requestTimeoutSeconds)
    {
        // Enterprise режим: працюємо “як у тестовому” через UseCase + Gateway (batchexecute)
        using var host = CreateEnterpriseHost();

        var useCase = host.Services.GetRequiredService<SearchAppsUseCase>();
        var query = SearchQuery.Create(keyword, country);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(requestTimeoutSeconds));

        var packages = await useCase.ExecuteAsync(query, cts.Token, maxPages);

        Console.WriteLine(JsonSerializer.Serialize(packages, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static IHost CreateEnterpriseHost()
    {
        // ✅ Вирівнюємо середовище: для console-Host за замовчуванням важливіше DOTNET_ENVIRONMENT,
        // але ми підтримуємо і ASPNETCORE_ENVIRONMENT.
        var env =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";

        return Host.CreateDefaultBuilder()
            .UseEnvironment(env)
            .ConfigureAppConfiguration((context, config) =>
            {
                // ✅ Читаємо appsettings*.json з папки запуску (bin/Debug/...)
                // і підставляємо саме env, який ми визначили вище.
                config.Sources.Clear();

                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                config.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Options
                var opt = context.Configuration.GetSection("PlayMarket").Get<PlayMarketOptions>()
                          ?? new PlayMarketOptions();
                services.AddSingleton(opt);

                // HttpClient для POST batchexecute
                services.AddHttpClient<IPlayMarketHttpClient, PlayMarketHttpClient>(http =>
                {
                    http.BaseAddress = new Uri(opt.BaseUrl);
                    http.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);

                    if (!string.IsNullOrWhiteSpace(opt.Cookie))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", opt.Cookie);
                });

                // HttpClient для GET /work/search (витяг токенів)
                services.AddHttpClient<IPlayMarketSessionProvider, PlayMarketSessionProvider>(http =>
                {
                    http.BaseAddress = new Uri(opt.BaseUrl);
                    http.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);

                    if (!string.IsNullOrWhiteSpace(opt.Cookie))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", opt.Cookie);
                });

                // Infrastructure
                services.AddSingleton<IRequestBodyFactory, RequestBodyFactory>();
                services.AddSingleton<IResponseParser, ResponseParser>();

                services.AddSingleton<PlayMarketSearchGateway>();
                services.AddSingleton<PlayMarketScraper.Application.Interface.IPlayMarketSearchGateway>(sp =>
                    sp.GetRequiredService<PlayMarketSearchGateway>());

                // Application
                services.AddSingleton<SearchAppsUseCase>();
            })
            .Build();
    }
    private static IConfiguration BuildConfig()
    {
        // Читаємо конфіг з папки запуску (bin/Debug/...)
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    /// <summary>
    /// Витягує package id з HTML видачі /store/search.
    /// Порядок зберігаємо як у HTML.
    /// </summary>
    private static List<string> ExtractPackageIdsFromHtml(string html)
    {
        var rx = new Regex(@"/store/apps/details\?id=([a-zA-Z0-9\._]+)", RegexOptions.Compiled);
        var m = rx.Matches(html);

        var list = new List<string>(m.Count);
        foreach (Match match in m)
        {
            if (!match.Success) continue;

            var id = match.Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(id))
                list.Add(id);
        }

        return list;
    }

    private static int TryInt(string? s, int def) => int.TryParse(s, out var v) ? v : def;
}

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using PlayMarketScraper.Application.UseCases;
using PlayMarketScraper.Domain.Models;
using PlayMarketScraper.Infrastructure.Gateway;
using PlayMarketScraper.Infrastructure.Http;
using PlayMarketScraper.Infrastructure.Options;
using PlayMarketScraper.Infrastructure.Parsing;
using PlayMarketScraper.Infrastructure.Payload;
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            using var host = CreateHost();
            var cfg = host.Services.GetRequiredService<IConfiguration>();

            // 1) Беремо значення за замовчуванням з appsettings.json
            var defaultCountry = cfg["PlayMarket:DefaultCountry"] ?? "US";
            var defaultMaxPages = TryInt(cfg["Cli:DefaultMaxPages"], 50);
            var requestTimeoutSeconds = TryInt(cfg["Cli:RequestTimeoutSeconds"], 60);

            // 2) Пробуємо взяти параметри з аргументів командного рядка.
            //    Якщо їх немає — запитуємо у користувача в консолі.
            var parsed = ParseArgs(args);

            var keyword = string.IsNullOrWhiteSpace(parsed.keyword)
                ? AskRequired("Keyword (наприклад: home, vpn, music): ")
                : parsed.keyword!;

            var country = string.IsNullOrWhiteSpace(parsed.country)
                ? AskWithDefault("Country (ISO2, напр. US, UA)", defaultCountry)
                : parsed.country!;

            var maxPages = parsed.maxPages ?? AskIntWithDefault("Max pages", defaultMaxPages);

            // Формуємо доменну модель запиту (із валідацією всередині Domain)
            var query = SearchQuery.Create(keyword, country);

            // Беремо use-case з DI контейнера
            var useCase = host.Services.GetRequiredService<SearchAppsUseCase>();

            // Таймаут усього запиту (щоб не “висіти” вічно)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(requestTimeoutSeconds));

            // Запускаємо бізнес-логіку: збір усіх сторінок + повернення списку package names
            var packages = await useCase.ExecuteAsync(query, cts.Token, maxPages);

            // Виводимо результат як JSON-масив у stdout
            Console.WriteLine(JsonSerializer.Serialize(packages, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            return 0;
        }
        catch (Exception ex)
        {
            // Усі помилки пишемо в stderr, щоб зручно було дебажити/логувати
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // IConfiguration вже зареєстрований Host'ом, але можна додати явно
                services.AddSingleton(context.Configuration);

                // Зчитуємо PlayMarketOptions з секції "PlayMarket" в appsettings.json
                var opt = context.Configuration.GetSection("PlayMarket").Get<PlayMarketOptions>()
                          ?? new PlayMarketOptions();

                // Реєструємо options як singleton (для RequestBodyFactory/Gateway тощо)
                services.AddSingleton(opt);

                // Реєструємо HttpClient, налаштовуємо BaseAddress і Timeout
                services.AddHttpClient<IPlayMarketHttpClient, PlayMarketHttpClient>(http =>
                {
                    http.BaseAddress = new Uri(opt.BaseUrl);
                    http.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
                });
                // Infrastructure: фабрика тіла запиту, парсер відповіді, gateway (реалізація інтерфейсу Application)
                services.AddSingleton<IRequestBodyFactory, RequestBodyFactory>();
                services.AddSingleton<IResponseParser, ResponseParser>();
                services.AddSingleton<PlayMarketSearchGateway>();
                services.AddSingleton<PlayMarketScraper.Application.Interface.IPlayMarketSearchGateway>(sp =>
                    sp.GetRequiredService<PlayMarketSearchGateway>());

                // Application: use-case (основна бізнес-логіка)
                services.AddSingleton<SearchAppsUseCase>();
            })
            .Build();
    }

    private static (string? keyword, string? country, int? maxPages) ParseArgs(string[] args)
    {
        string? keyword = null;
        string? country = null;
        int? maxPages = null;

        // Парсимо аргументи виду:
        // --keyword "home" --country "US" --maxPages 20
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];

            if (a.Equals("--keyword", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                keyword = args[++i];

            else if (a.Equals("--country", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                country = args[++i];

            else if (a.Equals("--maxPages", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var v)) maxPages = v;
            }
        }

        return (keyword, country, maxPages);
    }

    private static string AskRequired(string prompt)
    {
        // Просимо ввести значення, доки користувач не введе непорожній рядок
        while (true)
        {
            Console.Write(prompt);
            var v = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(v)) return v!;
        }
    }

    private static string AskWithDefault(string prompt, string def)
    {
        // Просимо значення з можливістю натиснути Enter і взяти значення за замовчуванням
        Console.Write($"{prompt} (default {def}): ");
        var v = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(v) ? def : v!;
    }

    private static int AskIntWithDefault(string prompt, int def)
    {
        // Просимо ціле число > 0, або Enter для значення за замовчуванням
        while (true)
        {
            Console.Write($"{prompt} (default {def}): ");
            var v = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(v)) return def;
            if (int.TryParse(v, out var n) && n > 0) return n;
        }
    }

    private static int TryInt(string? s, int def)
        => int.TryParse(s, out var v) ? v : def;
}

namespace PlayMarketScraper.Infrastructure.Options;

public sealed class PlayMarketOptions
{
    public string BaseUrl { get; init; } = "https://play.google.com";

    // Як у тестовому: /work/search :contentReference[oaicite:3]{index=3}
    public string SourcePath { get; init; } = "/work/search";

    // bl як у прикладі :contentReference[oaicite:4]{index=4}
    public string Bl { get; init; } = "boq_playenterprisewebuiserver_20251214.04_p0";

    public string Hl { get; init; } = "en-US";
    public int AuthUser { get; init; } = 0;
    public string Rt { get; init; } = "c";
    public int TimeoutSeconds { get; init; } = 30;

    // Cookie (опційно). Без нього /work може не віддати токени.
    public string? Cookie { get; init; }

    // ✅ Шаблон першого f.req (з тестового). {keyword} підставляємо. :contentReference[oaicite:5]{index=5}
    public string FirstInnerTemplate { get; init; } =
        @"[[[null,null,null,null,[null,1]],[[10,[10,50]],null,null,[96,108,72,100,27,177,183,222,8,57,169,110,11,184,16,1,139,152,194,165,68,163,211,9,71,31,195,12,64,151,150,148,113,104,55,56,145,32,34,10,122]],[\""{{keyword}}\""],4,null,null,null,[null,1]]]";

    // ✅ Шаблон наступного f.req.
    // У qnKh0b “важливі” метод і token :contentReference[oaicite:6]{index=6}
    // Якщо цей шаблон треба “1 в 1” — можна один раз зняти з DevTools і вставити сюди, лишивши {token}.
    public string NextInnerTemplate { get; init; } =
        @"[[[10,[10,50]],null,[96,108,72,100,27,177,183,222,8,57,169,110,11,184,16,1,139,152,194,165,68,163,211,9,71,31,195,12,64,151,150,148,113,104,55,56,145,32,34,10,122],[\""{{token}}\""]]]";
}

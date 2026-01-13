# PlayMarketScraper

Консольний застосунок на .NET, який за заданими `keyword` (пошуковий запит) та `country` (країна) повертає **список package names** (ідентифікаторів застосунків) у **правильному порядку**.

Проєкт має **2 режими запуску**:
- **store** — публічний режим через `GET /store/search` (парсинг HTML).
- **enterprise** — режим “як у тестовому”: **ланцюжок POST-запитів** до `/_/PlayEnterpriseWebStoreUi/data/batchexecute` з пагінацією через **token** попередньої відповіді.

> Вивід у обох режимах однаковий: **JSON-масив** package names.

---

## Швидкий старт

### 1) Store-режим (публічний, без cookie)
```powershell
dotnet run --project PlayMarketScraper.Cli -- --mode store
````

### 2) Enterprise-режим (як у тестовому, потрібна cookie)

1. Заповни `appsettings.Development.json` (не коміть у git):

```json
{
  "PlayMarket": {
    "SourcePath": "/work/search",
    "AuthUser": 0,
    "Hl": "en-US",
    "Cookie": "ВАША_РЕАЛЬНА_COOKIE_З_БРАУЗЕРА"
  }
}
```

2. Запуск:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project PlayMarketScraper.Cli -- --mode enterprise
```

> Для console-Host середовище може читатися через `DOTNET_ENVIRONMENT`. Якщо потрібно:

```powershell
$env:DOTNET_ENVIRONMENT="Development"
```

---

## Налаштування (appsettings)

### `appsettings.json` (за замовчуванням, store)

* `PlayMarket:BaseUrl` — базовий домен (зазвичай `https://play.google.com`)
* `PlayMarket:SourcePath` — шлях сторінки пошуку (`/store/search` або `/work/search`)
* `PlayMarket:Hl` — мова інтерфейсу (`en`, `en-US` тощо)
* `PlayMarket:DefaultCountry` — країна за замовчуванням
* `PlayMarket:TimeoutSeconds` — таймаут HTTP
* `Cli:DefaultMaxPages` — дефолтна кількість “сторінок”
* `Cli:RequestTimeoutSeconds` — таймаут всього запиту

### `appsettings.Development.json` (enterprise)

* Перевизначає `SourcePath` на `/work/search`
* Додає `Cookie`
* Виставляє `AuthUser` згідно активного акаунта в браузері

---

## Архітектура та відповідальність підпроєктів

### 1) `PlayMarketScraper.Domain`

**Призначення:** доменні моделі + інваріанти (валідація).

Основні сутності:

* `Keyword` (value object) — валідний пошуковий запит (trim, не порожній, ліміт довжини).
* `CountryCode` (value object) — ISO2 країна (`US`, `UA`, …).
* `SearchQuery` — агрегат, який об’єднує `Keyword` + `CountryCode`.

> Domain не має залежностей від HTTP/JSON/DI. Це “ядро”, яке не знає, звідки ми беремо дані.

---

### 2) `PlayMarketScraper.Application`

**Призначення:** бізнес-логіка (use-case) без деталей інфраструктури.

Ключові компоненти:

* `SearchAppsUseCase` — оркеструє процес пошуку:

  1. отримує першу сторінку,
  2. дістає `NextToken`,
  3. поки є токен — завантажує наступні сторінки,
  4. повертає список package names у правильному порядку.
* `IPlayMarketSearchGateway` — абстракція “отримати сторінку пошуку” (першу та наступні).
* `SearchPageResult` — DTO сторінки (`Packages` + `NextToken`).

> Application залежить від **абстракцій**, а не від HttpClient/парсерів (принцип Dependency Inversion).

---

### 3) `PlayMarketScraper.Infrastructure`

**Призначення:** реалізація того, як саме ми ходимо в PlayMarket.

Складові:

* `PlayMarketHttpClient` (`IPlayMarketHttpClient`) — низькорівневий HTTP POST до `batchexecute`.
* `PlayMarketSessionProvider` (`IPlayMarketSessionProvider`) — завантажує HTML з `/work/search` або `/store/search` і витягує потрібні токени (наприклад, `SNlM0e` / `thykhd`, `f.sid`, `bl`).
* `RequestBodyFactory` (`IRequestBodyFactory`) — збирає `f.req` для:

  * першого запиту (`lGYRle`)
  * наступних сторінок (`qnKh0b` + token)
* `ResponseParser` (`IResponseParser`) — дістає з відповіді:

  * package names
  * token наступної сторінки
* `PlayMarketSearchGateway` — реалізує `IPlayMarketSearchGateway` з Application:

  * викликає `SessionProvider` (токени)
  * формує URL + body
  * виконує POST
  * парсить результат

---

### 4) `PlayMarketScraper.Cli`

**Призначення:** точка входу (консоль), парсинг аргументів і взаємодія з користувачем.

Функції:

* питає `keyword`, `country`, `maxPages`
* підтримує `--mode store|enterprise`
* у `store` робить публічний HTML-пошук і парсинг
* у `enterprise` піднімає DI Host і запускає `SearchAppsUseCase`
* друкує результат у stdout як JSON

---

## Чому record, а не “звичайні класи”

У проєкті частина моделей зроблена через `record`/`record struct` (наприклад DTO та value objects), тому що:

1. **Семантика “значення”, а не “ідентичності”**
   `Keyword`, `CountryCode`, `SearchQuery`, `SearchPageResult` — це сутності, які логічно порівнюються за значенням (даними), а не за посиланням у пам’яті. `record` саме це і підкреслює.

2. **Коректне порівняння та hashing з коробки**
   Для value objects важливо мати правильні `Equals()`/`GetHashCode()` без ручного кодування та помилок.

3. **Іммутабельність і безпека**
   `record` за замовчуванням стимулює незмінність. Це зменшує ризик “побічних ефектів” при передачі даних між шарами (Domain → Application → Infrastructure).

4. **Зручність для DTO**
   DTO типу `SearchPageResult` коротко описує контракт даних, не потребує зайвого boilerplate-коду.

> Там, де потрібна поведінка/стан/інваріанти або DI-сервіс — використовуються звичайні класи.

---

## SOLID (коротко)

* **S**: кожен клас має одну відповідальність (HTTP-клієнт не парсить, парсер не робить запити, use-case не знає про HTTP).
* **O**: можна додати новий спосіб пошуку (інший gateway) без переписування use-case.
* **I**: інтерфейси дрібні (`IResponseParser`, `IRequestBodyFactory`, `IPlayMarketSessionProvider`).
* **D**: Application працює з абстракціями, а Infrastructure надає реалізації.

---

## Приклади запуску

### Store

```powershell
dotnet run --project PlayMarketScraper.Cli -- --mode store
```

### Enterprise

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project PlayMarketScraper.Cli -- --mode enterprise
```

Очікуваний вивід:

```json
[
  "com.example.app1",
  "com.example.app2"
]
```

---

## Примітки

* `Cookie` — чутливі дані. Не додавай у репозиторій. Краще зберігати у `appsettings.Development.json` (ігнорити git) або через змінні середовища.
* Google може змінювати внутрішні формати `batchexecute`. Тому частини, які відповідають за `f.req`/парсинг, винесені окремо (легше підтримувати та замінювати).

```
::contentReference[oaicite:0]{index=0}
```

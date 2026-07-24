# CanvasiaSocial


Canvasia ürünlerini senkronize eden, OpenRouter ile sosyal içerik hazırlayan, onaylayan, takvimleyen ve resmî platform API'leri üzerinden yayımlamaya hazırlayan .NET uygulaması.

`AUTO_PUBLISH_ENABLED` varsayılan ve önerilen başlangıç değeri `false` değeridir. Bu değer açıkça değiştirilmeden hiçbir zamanlanmış gönderi provider API'sine gönderilmez.

## Gereksinimler

- .NET SDK 10
- PostgreSQL 17+
- Docker Desktop ve Docker Compose, Docker kurulumu için
- Canvasia API anahtarı
- OpenRouter API anahtarı
- Yalnızca bağlanacak platformlara ait resmî developer uygulaması kimlik bilgileri

## Yerel Kurulum

PowerShell:

```powershell
Copy-Item .env.example .env
dotnet tool restore
dotnet restore CanvasiaSocial.sln
dotnet ef database update --project src/CanvasiaSocial.Infrastructure --startup-project src/CanvasiaSocial.Web
dotnet run --project src/CanvasiaSocial.Web
```

Worker'ı ayrı terminalde başlatın:

```powershell
dotnet run --project src/CanvasiaSocial.Worker
```

İlk admin yalnızca ilk kurulum için `INITIAL_ADMIN_EMAIL` ve `INITIAL_ADMIN_PASSWORD` ile oluşturulur. İlk girişten sonra üst menüdeki `Parola değiştir` bağlantısıyla başlangıç parolasını değiştirin ve `INITIAL_ADMIN_PASSWORD` deployment secret'ını kaldırın. Uygulama kapalı bir yönetim panelidir; herkese açık kullanıcı kaydı bilinçli olarak sunulmaz.

## Docker

`.env.example` dosyasını `.env` olarak kopyalayın, gerçek değerleri yalnızca `.env` içine girin. `.env` Git ve Docker build context dışında tutulur.

```powershell
Copy-Item .env.example .env
docker compose build
docker compose up -d
docker compose ps
docker compose logs -f web worker
```

Web varsayılan olarak `http://localhost:8080`, bu workspace'in yerel ayarında `http://localhost:18080` üzerinden açılabilir.

Health endpoint'leri:

- Web: `/health/live`, `/health/ready`
- Worker: `http://worker:8081/health/live`, `http://worker:8081/health/ready`

Web ve Worker aynı kalıcı `data-protection-keys` volume'unu kullanır. Bu volume silinirse mevcut OAuth tokenları çözülemez.

## Sosyal Providerlar

### Instagram

- Resmî Instagram Login kullanılır.
- Yalnızca Business veya Creator profesyonel hesap desteklenir.
- Scopelar: `instagram_business_basic`, `instagram_business_content_publish`.
- Eski `business_basic` ve `business_content_publish` izinleri kullanılmaz.
- Yayın akışı: `/{ig-user-id}/media`, ardından `/{ig-user-id}/media_publish`.

### Facebook

- Resmî Facebook Login ve Graph API v25.0 kullanılır.
- Yalnızca `CREATE_CONTENT` görevi bulunan Facebook Sayfaları bağlanır.
- Scopelar: `pages_show_list`, `pages_read_engagement`, `pages_manage_posts`.
- Fotoğraf yayını: `/{page-id}/photos`.

### TikTok ve Pinterest

Providerlar güvenli iskelet olarak kayıtlıdır. Kimlik bilgisi yokken veya provider tamamlanmadan uygulama çökmez ve arayüzde `Yapılandırılmadı` gösterilir. Sahte OAuth veya yayın çağrısı yapılmaz.

## Callback URL'leri

Production developer uygulamalarında URL'leri birebir kaydedin:

- Instagram: `https://sosyalmedya.canvasia.com.tr/SocialAccounts/Instagram/Callback`
- Facebook: `https://sosyalmedya.canvasia.com.tr/SocialAccounts/Facebook/Callback`
- TikTok: `https://sosyalmedya.canvasia.com.tr/SocialAccounts/TikTok/Callback`
- Pinterest: `https://sosyalmedya.canvasia.com.tr/SocialAccounts/Pinterest/Callback`

TikTok ve Pinterest callback'leri providerlar tamamlanana kadar bağlantı başlatmak için kullanılmaz.

## Environment Variables

Değerleri repoya yazmayın. Railway ve Docker'da secret olarak tanımlayın.

Temel:

- `ConnectionStrings__DefaultConnection`
- `INITIAL_ADMIN_EMAIL`
- `INITIAL_ADMIN_PASSWORD`
- `LOGIN_RATE_LIMIT_PER_MINUTE` (varsayılan `10`, izin verilen aralık `5-100`)
- `GENERAL_WORKER_COUNT` (varsayılan `4`, izin verilen aralık `1-32`)
- `CANVASIA_API_BASE_URL`
- `CANVASIA_API_KEY`
- `OPENROUTER_BASE_URL`
- `OPENROUTER_API_KEY`
- `OPENROUTER_MODEL`
- `DATA_PROTECTION_KEYS_PATH`
- `DATA_PROTECTION_STORE`
- `TRUST_FORWARDED_HEADERS`
- `RUN_MIGRATIONS`
- `AUTO_PUBLISH_ENABLED`
- `PUBLISH_MAX_RETRY_COUNT`
- `CANVASIA_ALLOWED_IMAGE_HOSTS`
- `SOCIAL_IMAGE_MAX_BYTES`
- `SOCIAL_IMAGE_TIMEOUT_SECONDS`

Instagram:

- `INSTAGRAM_ENABLED`
- `INSTAGRAM_CLIENT_ID`
- `INSTAGRAM_CLIENT_SECRET`
- `INSTAGRAM_REDIRECT_URI`
- `INSTAGRAM_SCOPES`
- `INSTAGRAM_API_BASE_URL`

Facebook:

- `FACEBOOK_ENABLED`
- `FACEBOOK_CLIENT_ID`
- `FACEBOOK_CLIENT_SECRET`
- `FACEBOOK_REDIRECT_URI`
- `FACEBOOK_SCOPES`
- `FACEBOOK_API_BASE_URL`

TikTok ve Pinterest aynı isim düzenini kullanır: `TIKTOK_*` ve `PINTEREST_*`.

## Railway Deployment

Web ve Worker için aynı repository'den iki servis oluşturun. PostgreSQL servisini ekleyin ve iki uygulama servisine aynı bağlantı dizesini verin.

Web:

- Dockerfile varsayılan final stage: `web-final`
- Start command: image varsayılan entrypoint'i
- Health path: `/health/ready`
- Public domain: `sosyalmedya.canvasia.com.tr`
- `Security__UseHttpsRedirection=true`
- `RUN_MIGRATIONS=false`
- `DATA_PROTECTION_STORE=database`
- `TRUST_FORWARDED_HEADERS=true`

Worker:

- `CANVASIA_RUNTIME_TARGET=worker-final` build/service variable
- Health path: `/health/ready`, port `8081`
- Public domain gerekmez
- `DATA_PROTECTION_STORE=database` ile Web ile aynı PostgreSQL key ring'i kullanılmalıdır

Migration için deploy/release adımı:

```powershell
dotnet CanvasiaSocial.Web.dll --migrate-only
```

Migration başarılı olduktan sonra Web ve Worker başlatılır. Birden fazla Web replica'sında startup migration çalıştırmayın.

Railway'de ayrı servislerin file volume paylaşamadığı için ortak PostgreSQL key ring kullanılır. Daha güçlü production güvenliği için Data Protection key ring'i haricî KMS/secret storage ile at-rest koruyun.

## Reverse Proxy ve HTTPS

Uygulama yalnızca `TRUST_FORWARDED_HEADERS=true` olduğunda `X-Forwarded-For` ve `X-Forwarded-Proto` başlıklarını tek proxy hop'u için işler. Railway TLS sonlandırmasından sonra `Security__UseHttpsRedirection=true` olmalıdır. Authentication ve antiforgery cookie'leri production'da Secure ve HttpOnly olur. Uygulamayı güvenilir proxy dışında doğrudan internete açmayın.

`/giris` endpoint'i IP başına dakikalık sabit pencereyle sınırlandırılır. Limit aşımında HTTP 429 ve `Retry-After: 60` döner. Identity hesap kilitlemesi ayrıca beş başarısız denemeden sonra 15 dakika uygulanır.

Detaylı domain kontrol listesi: [`docs/production-sosyalmedya.canvasia.com.tr.md`](docs/production-sosyalmedya.canvasia.com.tr.md).

## Migration ve Backup

Migration oluşturma:

```powershell
dotnet ef migrations add MigrationName --project src/CanvasiaSocial.Infrastructure --startup-project src/CanvasiaSocial.Web --output-dir Persistence/Migrations
```

Migration uygulama:

```powershell
dotnet ef database update --project src/CanvasiaSocial.Infrastructure --startup-project src/CanvasiaSocial.Web
```

PostgreSQL backup:

```powershell
docker compose exec -T postgres pg_dump -U canvasia -d canvasia_social -Fc > canvasia-social.backup
```

Restore işlemini production dışında doğrulayın ve OAuth tokenlarının çözülebilmesi için PostgreSQL backup ile Data Protection key volume backup'ını birlikte saklayın.

## Log Kontrolü

```powershell
docker compose logs --tail 200 web
docker compose logs --tail 200 worker
docker compose logs -f worker
```

Console logları JSON'dur. Authorization header, access token, refresh token ve client secret loglanmaz. OAuth callback request logu query string içermez.

## Token Yenileme

- Instagram uzun süreli token, geçerliyken ve en az 24 saatlikken resmî refresh endpoint'iyle yenilenir.
- Facebook Page tokenlarında standart OAuth refresh token yoktur. Geçersiz bağlantıda hesap yeniden OAuth ile bağlanır.
- Arayüzden `Bağlantıyı doğrula` ve `Bağlantıyı yenile` kullanılabilir.
- Tokenlar veritabanında ASP.NET Core Data Protection ile şifreli saklanır ve HTML/JSON response'lara gönderilmez.

## Yayın Güvenliği

- İlk kurulumda `AUTO_PUBLISH_ENABLED=false` kalmalıdır.
- Plan, taslak ve OAuth bağlantıları bu değer kapalıyken provider yayın endpoint'ini çağırmaz.
- Gerçek provider başarısı ve haricî post kimliği olmadan kayıt `Published` yapılmaz.
- 401 bir token yenileme denemesi yapar; 403 ve geçersiz içerik tekrar denenmez; 429 `Retry-After`, ağ/5xx exponential backoff kullanır.
- Görseller domain, DNS/IP, redirect, MIME, dosya boyutu ve timeout kontrollerinden geçer.

## Test ve Doğrulama

```powershell
dotnet build CanvasiaSocial.sln
dotnet test CanvasiaSocial.sln
docker compose build
docker compose up -d
```

Testler gerçek sosyal platforma gönderi yapmaz; provider davranışları sahte providerlarla doğrulanır.

## Resmî Dokümantasyon

- Meta Graph API changelog: https://developers.facebook.com/docs/graph-api/changelog/
- Instagram Login: https://developers.facebook.com/docs/instagram-platform/instagram-api-with-instagram-login/business-login/
- Instagram content publishing: https://developers.facebook.com/docs/instagram-platform/content-publishing/
- Facebook Login manual flow: https://developers.facebook.com/docs/facebook-login/guides/advanced/manual-flow/
- Facebook long-lived tokens: https://developers.facebook.com/docs/facebook-login/guides/access-tokens/get-long-lived/
- Facebook Pages posts: https://developers.facebook.com/docs/pages-api/posts/
- TikTok Login Kit: https://developers.tiktok.com/doc/login-kit-web
- TikTok token management: https://developers.tiktok.com/doc/oauth-user-access-token-management
- Pinterest OAuth: https://developers.pinterest.com/docs/getting-started/set-up-authentication-and-authorization/

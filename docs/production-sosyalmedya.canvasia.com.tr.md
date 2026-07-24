# sosyalmedya.canvasia.com.tr Production Kontrol Listesi

1. DNS kaydını Railway Web servisine yönlendirin ve TLS sertifikasının aktif olduğunu doğrulayın.
2. Web'de `Security__UseHttpsRedirection=true`, `TRUST_FORWARDED_HEADERS=true`, `RUN_MIGRATIONS=false`, `AUTO_PUBLISH_ENABLED=false` ve `LOGIN_RATE_LIMIT_PER_MINUTE=10` tanımlayın.
3. Instagram ve Facebook developer uygulamalarına README'deki HTTPS callback URL'lerini birebir kaydedin.
4. Meta App Review ve Advanced Access tamamlanmadan yalnızca app role/tester hesaplarını bağlayın.
5. Instagram hesabının Business veya Creator olduğundan emin olun.
6. Facebook kullanıcısının Sayfada `CREATE_CONTENT` görevine sahip olduğunu doğrulayın.
7. Web ve Worker'da `DATA_PROTECTION_STORE=database` kullanarak aynı PostgreSQL key ring'ini kullandığını doğrulayın.
8. Release aşamasında `dotnet CanvasiaSocial.Web.dll --migrate-only` çalıştırın.
9. `/health/live` ve `/health/ready` endpoint'lerini Railway healthcheck olarak tanımlayın.
10. PostgreSQL ve Data Protection key volume için birlikte backup/restore prosedürü uygulayın.
11. Client secret ve API tokenlarının yalnızca Railway secret variables içinde bulunduğunu doğrulayın.
12. İlk migration sırasında `INITIAL_ADMIN_EMAIL` ve güçlü bir `INITIAL_ADMIN_PASSWORD` tanımlayın. İlk girişte `Parola değiştir` ekranından parolayı değiştirip başlangıç parola secret'ını kaldırın.
13. İlk gerçek yayın öncesinde `/SocialAccounts` üzerinden OAuth bağlantısını kullanıcı eliyle tamamlayın.
14. Tek bir onaylı test gönderisi planlayın; ilk test boyunca `AUTO_PUBLISH_ENABLED=false` bırakın.
15. Kullanıcı açıkça onay verdikten sonra ayrı deployment değişikliğiyle yayını etkinleştirin ve ilk gönderiyi gözlemleyin.

Uygulama doğrudan internete açılmamalı; forwarded header güveni Railway'in tek reverse proxy hop'u varsayımıyla yapılandırılmıştır.

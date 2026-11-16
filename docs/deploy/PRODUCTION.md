# راه‌اندازی Tarantula روی سرور و روشن‌کردن ردیابی webapp پاستیل

وضعیت (۲۰۲۶-۱۱-۱۶): همه‌چیز آماده و روی ماشین محلی با کلیدهای آزمایشی امتحان شد (ساخت image، بالا آمدن ClickHouse + API، رد درخواست بدون کلید، خطای «پایگاه‌داده وصل نیست»، و امتناع از شروع با کلیدهای توسعه). **چیزی روی سرور واقعی اجرا نشده**: من به SSH سرور دسترسی ندارم، و `webapp` با اسکریپت خودتان (`Deploy-Webapp.ps1`، رمز SSH می‌خواهد) deploy می‌شود.

## چه چیزی آماده است

| فایل | کار |
|---|---|
| `Dockerfile` | ساخت API (بدون هیچ راز داخل image) |
| `deploy/docker-compose.prod.yml` | ClickHouse (سقف حافظه‌ی ۲GB) + API؛ هر دو فقط روی `127.0.0.1` |
| `deploy/.env.prod` | **کلیدها و رمز تازه ساخته شد** (git-ignored). مقدارها هرگز چاپ نشدند |
| `deploy/Caddyfile.example`، `deploy/nginx.example.conf` | فقط `POST /v1/collect` را عمومی می‌کند؛ خواندن (`/v1/live`، `/v1/business`) داخلی می‌ماند |
| `ProductionGuard` | بیرون از Development اگر کلید/رمز توسعه یا `AllowedOrigins` خالی باشد اصلاً بالا نمی‌آید |

## مراحل (به ترتیب)

### ۱) یک ساب‌دامنه
DNS: `t.pastil.pet` (یا هر نام دیگر) → IP همان سرور برنامه. اگر نام دیگری می‌خواهید، در Caddyfile/nginx و `NUXT_PUBLIC_TARANTULA_ENDPOINT` عوض کنید.

### ۲) فایل‌ها را به سرور ببرید (ترمینال خودتان، PowerShell)
```powershell
scp -r D:\WorkSpace\Projects\Tarantula\Dockerfile D:\WorkSpace\Projects\Tarantula\Directory.Build.props D:\WorkSpace\Projects\Tarantula\Tarantula.sln D:\WorkSpace\Projects\Tarantula\src D:\WorkSpace\Projects\Tarantula\deploy root@<server>:/root/tarantula/
```
(پوشه‌های `bin`/`obj` را اگر دارید پیش از آن پاک کنید؛ `.dockerignore` هم در سرور لازم است: `scp D:\WorkSpace\Projects\Tarantula\.dockerignore root@<server>:/root/tarantula/`.)

### ۳) روی سرور (ترمینال SSH سرور، bash)
```bash
cd /root/tarantula/deploy
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
curl -s http://127.0.0.1:5080/v1/live -H "X-Tarantula-Read-Key: $(grep PASTIL_READ_KEY .env.prod | cut -d= -f2)"
```
باید `{"onlineUsers":0,...}` ببینید.

### ۴) پروکسی
مثال Caddy را در Caddyfile سرور اضافه کنید (یا nginx)، سپس reload. تست از بیرون:
```bash
curl -i -X POST https://t.pastil.pet/v1/collect   # باید 401 بدهد (کلید ندارد) نه 404
curl -i https://t.pastil.pet/v1/live               # باید 404 بدهد (خواندن عمومی نیست)
```

### ۵) وب‌اپ را با ردیابی بسازید و deploy کنید (ترمینال خودتان)
در `webapp\.env` (روی ماشین build) این‌ها را بگذارید، **به‌جای مقدارهای localhost فعلی**:
```
NUXT_PUBLIC_TARANTULA_ENABLED=true
NUXT_PUBLIC_TARANTULA_KEY=<مقدار PASTIL_WRITE_KEY از deploy\.env.prod>
NUXT_PUBLIC_TARANTULA_ENDPOINT=https://t.pastil.pet
NUXT_PUBLIC_TARANTULA_CONSENT=analytics
```
سپس همان فرایند همیشگی: `webapp\scripts\Deploy-Webapp.ps1`. (آدرس collect باید هنگام build تنظیم باشد چون به CSP می‌رود.)

### ۶) کنسول
کنسول فعلاً فقط محلی است. برای دیدن داده‌ی زنده‌ی سرور، در `console` متغیرهای `NUXT_API_BASE` را به API سرور (از طریق تونل SSH یا شبکه‌ی داخلی) و `NUXT_READ_KEY` را به مقدار `PASTIL_READ_KEY` بدهید؛ آن را عمومی نکنید.

## کنترل بعد از روشن‌شدن
- در مرورگر وب‌اپ واقعی یک صفحه باز کنید → در Network درخواست `POST https://t.pastil.pet/v1/collect` با 202.
- کنسول → نمای کلی → «همین لحظه آنلاین‌اند» ≥ ۱.
- اگر کنسول خطای CORS داد: `AllowedOrigins` در `.env.prod` باید دقیقاً origin وب‌اپ باشد (`https://app.pastil.pet`).

## حریم خصوصی (مصوب فعلی)
رضایت `analytics` برای همه: شناسه‌ی ناشناس و session، **بدون پیوند به User واردشده**؛ آدرس‌ها بدون query/fragment؛ کلیک بدون متن. برای پیوند با User و Vendor بعداً بنر رضایت + بررسی حقوقی لازم است.

## بازگشت
وب‌اپ: `NUXT_PUBLIC_TARANTULA_ENABLED=false` و deploy دوباره (یا برگرداندن `.output-backup-<stamp>` که اسکریپت می‌سازد). سرور: `docker compose -f docker-compose.prod.yml down` (داده‌ی ClickHouse در volume می‌ماند؛ `-v` آن را پاک می‌کند).

# اتصال Tarantula به دیتابیس واقعی پاستیل (فقط‌خواندنی)

وضعیت (۲۰۲۶-۱۱-۱۶): اسکریپت روی دیتابیس آزمایشی تست و سپس به دستور مالک **روی دیتابیس واقعی (`pastil_db`) اجرا شد**؛ لاگین `tarantula_reader` و ۱۰ View ساخته شده‌اند. رشته‌ی اتصال فقط در User Secrets پروژه‌ی `Tarantula.Ingest.Api` است (کلید `Pastil:ConnectionString`)، نه در Git. جدول‌ها و ستون‌ها پیش از اجرا با `INFORMATION_SCHEMA` دیتابیس واقعی مطابقت داده شد (۰ مغایرت).

## چه چیزی ساخته می‌شود

| مورد | توضیح |
|---|---|
| اسکریپت | [`pastil-readonly.sql`](pastil-readonly.sql) |
| Schema | `tarantula` (مالک: `dbo`) |
| ۱۰ View | `Users`, `Providers`, `Cities`, `Vendors`, `ShopOrders`, `ShopOrderStores`, `Carts`, `CartStores`, `Reserves`, `Payments` |
| کاربر | `tarantula_reader` — فقط `SELECT` روی schema `tarantula`؛ روی `dbo` همه‌چیز `DENY` |

View ها فقط شناسه، تاریخ، وضعیت، مبلغ و شهر را نشان می‌دهند. **هیچ‌کدام** این‌ها نیستند: موبایل، ایمیل، نام، کد ملی، رمز، آدرس، توکن/شماره‌ی مرجع درگاه، متن آزاد (توضیح سفارش/لغو)، لوکیشن، IP.

- `Vendors` همه‌ی انواع را یک‌جا می‌آورد: ۱ فروشگاه، ۲ کلینیک، ۳ آرایشگاه (Groomer)، ۴ مربی، ۵ پانسیون، ۶ مدرسه. پانسیون و مدرسه ستون‌های `Approved`/`CreatedAt` ندارند (NULL).
- `Reserves` انواع ۲ تا ۵ را یک‌جا می‌آورد. `Status` برای پانسیون NULL است (مدل وضعیت دیگری دارد).
- به‌جای شماره‌ی کاربر در `Users`، همان `UserId` پاستیل است؛ Tarantula فقط وقتی با رویدادهای SDK گره می‌خورد که رضایت `full` و `identify(userId)` باشد (تصمیم فعلی: همه `analytics`، بدون پیوند).

## اجرا (مالک دیتابیس، یک‌بار)

۱. یک رمز بلند تصادفی بساز (در هیچ فایلی ذخیره نشود):

```powershell
-join ((48..57)+(65..90)+(97..122) | Get-Random -Count 40 | % {[char]$_})
```

۲. با کاربر ادمین روی **دیتابیس پاستیل** اجرا کن:

```bash
sqlcmd -S <server> -d <PastilDb> -U <admin> -P <adminPassword> -i pastil-readonly.sql -v ReaderPassword="<رمز مرحله ۱>"
```

اسکریپت یک `LOGIN` سطح سرور (بدون هیچ نقش) و یک `USER` در همین دیتابیس می‌سازد، چون دیتابیس پاستیل contained نیست. ادمین باید اجازه‌ی `CREATE LOGIN` داشته باشد.

۳. خروجی آخر اسکریپت فهرست ستون‌های هر View است؛ مطمئن شو چیزی شخصی در آن نیست.

۴. رشته‌ی اتصال را فقط در محیط/User Secrets مدل Tarantula بگذار (هرگز در Git):
`Server=<server>;Database=<PastilDb>;User Id=tarantula_reader;Password=<…>;Encrypt=True;ApplicationIntent=ReadOnly`

## چیزی که هنگام تست پیدا شد

`DENY CONTROL` روی schema، `SELECT` را هم می‌بندد؛ در نسخه‌ی نهایی فقط `INSERT/UPDATE/DELETE/EXECUTE/ALTER` رد می‌شود و `REVOKE CONTROL` برای اجرای مجدد گذاشته شده.

## تست انجام‌شده (دیتابیس آزمایشی)

- اجرای اسکریپت دو بار پشت‌سرهم: بدون خطا (idempotent).
- با `tarantula_reader`: خواندن View ها موفق؛ `SELECT dbo.Users`، `UPDATE` روی View/جدول و `CREATE VIEW` همه رد شدند؛ ستون `Mobile` در View وجود ندارد.
- تست روی **نام جدول‌های EF** انجام شد (از `DataBaseContext`)؛ اگر نام واقعی یکی فرق داشت، همان‌جا موقع اجرا خطای «Invalid object name» می‌دهد و چیزی عوض نمی‌شود.

## برای ابطال

```sql
DROP USER [tarantula_reader];
DROP LOGIN [tarantula_reader];
```

(View ها بدون کاربر چیزی را در معرض دید قرار نمی‌دهند.)

## سمت Tarantula (ساخته‌شده)

- پورت `IBusinessReader` (Application) ← آداپتر `PastilBusinessReader` (Infrastructure، Microsoft.Data.SqlClient) + دکوراتور `CachedBusinessReader` (۶۰ ثانیه، یک کوئری مشترک).
- API با کلید خواندن: `GET /v1/business/overview`، `/v1/business/users`، `/v1/business/vendors?kind=store|clinic|groomer|coach|pansion|school`. بدون رشته‌ی اتصال: `503 business_not_connected`.
- کنسول: صفحه‌های `/users` و `/vendors` و بخش «کسب‌وکار» در نمای کلی، از مسیر سرور `/api/business/[section]` (کلید خواندن به مرورگر نمی‌رسد).
- علامت‌های Vendor: `not_approved`، `inactive`، `hidden`، `no_sales_30d`، `low_rating`. مدرسه داده‌ی فروش ندارد و علامت فروش نمی‌گیرد.
- `Microsoft.Data.SqlClient` با `InvariantGlobalization` کار نمی‌کند؛ در `Tarantula.Ingest.Api.csproj` خاموش شده (روی Docker/Linux به ICU نیاز است).
- ستون `VendorKind` در View واقعی `int` است (نتیجه‌ی UNION)، نه `tinyint`.

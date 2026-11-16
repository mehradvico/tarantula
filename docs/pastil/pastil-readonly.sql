/*
===============================================================================
 Tarantula -> Pastil: READ-ONLY access, limited to non-personal views
===============================================================================
 WHO RUNS THIS : the Pastil database owner (me/Claude never runs it on production).
 WHERE         : the Pastil database (the real one), connected as an admin
                 (db_owner / the Liara managed-SQL admin user).
 WHAT IT DOES  : 1. creates the schema [tarantula] with 10 views that expose
                    only ids, dates, status flags, amounts and city. NO mobile,
                    e-mail, name, national code, password, address, token,
                    gateway reference, free text or location.
                 2. creates a database user [tarantula_reader] that can SELECT
                    the [tarantula] schema ONLY, and is explicitly DENIED on [dbo].
 WHAT IT NEVER : create/alter/drop any existing table, touch data, or store a
                 password. Re-running is safe (idempotent).

 HOW TO RUN (sqlcmd; the password is typed into the variable, never saved here):
     sqlcmd -S <server> -d <PastilDb> -U <admin> -P <adminPassword> ^
            -i pastil-readonly.sql -v ReaderPassword="<long random password>"

 Generate the password with e.g. PowerShell:
     -join ((48..57)+(65..90)+(97..122) | Get-Random -Count 40 | % {[char]$_})

 The admin account must be allowed to CREATE LOGIN (sysadmin / securityadmin).
 If the host forbids that, create the login in the hosting panel first; the script
 then only runs ALTER LOGIN + CREATE USER.

 TO REVOKE EVERYTHING LATER:
     DROP USER [tarantula_reader];  DROP LOGIN [tarantula_reader];
 (the views alone expose nothing; drop them with DROP VIEW / DROP SCHEMA tarantula if wanted.)

 ASSUMPTIONS (table names = EF DbSet names, see backend/Persistence/Context/DataBaseContext.cs):
 Users, Providers, Stores, Clinics, Groomers, Coaches, Pansions, Schools, Cities,
 Carts, CartStores, ProductOrders, ProductOrderStores, ClinicReserves,
 GroomerReserves, CoachReserves, PansionReserves, Payments — all in schema dbo.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- 1) Schema (owned by dbo so ownership chaining lets the views read dbo tables
--    without giving the reader any permission on them).
IF SCHEMA_ID(N'tarantula') IS NULL
    EXEC (N'CREATE SCHEMA [tarantula] AUTHORIZATION [dbo];');
GO

-- 2) Views. CREATE OR ALTER keeps the script re-runnable.

/* Users: no mobile, e-mail, name, national code, password, referral codes, picture. */
CREATE OR ALTER VIEW [tarantula].[Users] AS
SELECT u.Id                       AS UserId,
       u.CreateDate               AS CreatedAt,
       u.RoleId,
       u.IsProvider,
       u.Locked,
       u.Deleted,
       u.IsFemale,
       CAST(u.RegistrationReferralSource AS int) AS RegistrationSource,
       CAST(CASE WHEN u.ReferredByUserId IS NULL AND u.ReferredByCompanionId IS NULL AND u.ReferredByStoreId IS NULL
                 THEN 0 ELSE 1 END AS bit)        AS WasReferred,
       u.CurrentScore
FROM dbo.Users AS u;
GO

/* Providers (owner accounts of one or more Vendors). */
CREATE OR ALTER VIEW [tarantula].[Providers] AS
SELECT p.Id AS ProviderId, p.UserId, p.Active, p.Approved, p.Deleted, p.CreateDate AS CreatedAt
FROM dbo.Providers AS p;
GO

/* Cities: reference names only. */
CREATE OR ALTER VIEW [tarantula].[Cities] AS
SELECT c.Id AS CityId, c.Name
FROM dbo.Cities AS c;
GO

/* Vendors: one row per business, whatever its kind. Business names are public on the site.
   Kinds: 1 Store, 2 Clinic, 3 Groomer, 4 Coach, 5 Pansion, 6 School.
   Pansion and School have no Approved/CreateDate columns -> NULL. */
CREATE OR ALTER VIEW [tarantula].[Vendors] AS
SELECT CAST(1 AS tinyint) AS VendorKind, s.Id AS VendorId, s.Name, s.ProviderId, s.CityId,
       s.Active, s.Approved, s.ShowToSite, s.Deleted,
       CAST(s.CreateDate AS datetime2(0)) AS CreatedAt,
       s.RateAvg, s.RateCount, CAST(s.CommissionPercent AS decimal(9,4)) AS CommissionPercent
FROM dbo.Stores AS s
UNION ALL
SELECT 2, c.Id, c.Name, c.ProviderId, c.CityId, c.Active, c.Approved, c.ShowToSite, c.Deleted,
       CAST(c.CreateDate AS datetime2(0)), c.RateAvg, c.RateCount, CAST(c.CommissionPercent AS decimal(9,4))
FROM dbo.Clinics AS c
UNION ALL
SELECT 3, g.Id, g.Name, g.ProviderId, g.CityId, g.Active, g.Approved, g.ShowToSite, g.Deleted,
       CAST(g.CreateDate AS datetime2(0)), g.RateAvg, g.RateCount, NULL
FROM dbo.Groomers AS g
UNION ALL
SELECT 4, k.Id, k.Name, k.ProviderId, k.CityId, k.Active, k.Approved, k.ShowToSite, k.Deleted,
       CAST(k.CreateDate AS datetime2(0)), k.RateAvg, k.RateCount, NULL
FROM dbo.Coaches AS k
UNION ALL
SELECT 5, n.Id, n.Name, n.ProviderId, n.CityId, n.Active, CAST(NULL AS bit), n.ShowToSite, n.Deleted,
       CAST(NULL AS datetime2(0)), n.RateAvg, n.RateCount, NULL
FROM dbo.Pansions AS n
UNION ALL
SELECT 6, sc.Id, sc.Name, sc.ProviderId, sc.CityId, sc.Active, CAST(NULL AS bit), sc.ShowToSite, sc.Deleted,
       CAST(NULL AS datetime2(0)), sc.RateAvg, sc.RateCount, NULL
FROM dbo.Schools AS sc;
GO

/* Shop orders: money and state only. No address, notes, tracking code, courier data. */
CREATE OR ALTER VIEW [tarantula].[ShopOrders] AS
SELECT o.Id AS OrderId, o.UserId, o.CreateDate AS CreatedAt,
       o.Price, o.BasePrice, o.DiscountPrice, o.WalletPrice, o.PaymentPrice, o.RebatePrice,
       o.DeliveryPrice, o.SiteShare, o.StoreShare,
       o.IsPaid, o.Deleted,
       o.ProductOrderStatusId AS StatusId, o.ProductOrderStateId AS StateId,
       o.ProcessingDate, o.SentDate, o.CancelledDate, o.UserReceived, o.UserReceivedDate
FROM dbo.ProductOrders AS o;
GO

CREATE OR ALTER VIEW [tarantula].[ShopOrderStores] AS
SELECT os.Id AS OrderStoreId, os.ProductOrderId AS OrderId, os.StoreId,
       os.Price, os.BasePrice, os.DiscountPrice, os.DeliveryPrice, os.PaymentPrice, os.Deleted
FROM dbo.ProductOrderStores AS os;
GO

/* Carts (abandoned-cart analysis). */
CREATE OR ALTER VIEW [tarantula].[Carts] AS
SELECT c.Id AS CartId, c.UserId, c.CreateDate AS CreatedAt, c.UpdateDate AS UpdatedAt,
       c.ItemCount, c.BasePrice, c.Price, c.Discount, c.PaymentPrice
FROM dbo.Carts AS c;
GO

CREATE OR ALTER VIEW [tarantula].[CartStores] AS
SELECT cs.Id AS CartStoreId, cs.CartId, cs.StoreId, cs.ItemCount, cs.Price, cs.Active
FROM dbo.CartStores AS cs;
GO

/* Reserves of every Vendor kind. Kinds: 2 Clinic, 3 Groomer, 4 Coach, 5 Pansion.
   Status is 0 registered / 1 paid / 2 completed for 2-4; NULL for Pansion (it uses IsReserved/StatusId).
   No booker note, cancel text, address, pet, assignee or owner-decision text. */
CREATE OR ALTER VIEW [tarantula].[Reserves] AS
SELECT CAST(2 AS tinyint) AS VendorKind, r.Id AS ReserveId, r.ClinicId AS VendorId, r.BookerId AS UserId,
       r.CreateDate AS CreatedAt, r.PaidDate AS PaidAt, r.DoDate AS StartsAt,
       r.Price, r.PaymentPrice, r.SiteShare, r.ProviderShare,
       r.IsReserved, r.IsCancel, r.CancelDate AS CancelledAt, r.CancelledByBooker, r.Status
FROM dbo.ClinicReserves AS r
UNION ALL
SELECT 3, r.Id, r.GroomerId, r.BookerId, r.CreateDate, r.PaidDate, r.DoDate,
       r.Price, r.PaymentPrice, r.SiteShare, r.ProviderShare,
       r.IsReserved, r.IsCancel, r.CancelDate, r.CancelledByBooker, r.Status
FROM dbo.GroomerReserves AS r
UNION ALL
SELECT 4, r.Id, r.CoachId, r.BookerId, r.CreateDate, r.PaidDate, r.DoDate,
       r.Price, r.PaymentPrice, r.SiteShare, r.ProviderShare,
       r.IsReserved, r.IsCancel, r.CancelDate, r.CancelledByBooker, r.Status
FROM dbo.CoachReserves AS r
UNION ALL
SELECT 5, r.Id, r.PansionId, r.BookerId, r.CreateDate, CAST(NULL AS datetime2(7)), r.FromDate,
       r.Price, r.PaymentPrice, r.SiteShare, r.CompanionShare,
       r.IsReserved, r.IsCancel, r.CancelDate, CAST(NULL AS bit), CAST(NULL AS int)
FROM dbo.PansionReserves AS r;
GO

/* Payments: amounts and outcome only. No gateway token, reference number, URL, IP, idempotency key. */
CREATE OR ALTER VIEW [tarantula].[Payments] AS
SELECT p.Id AS PaymentId, p.UserId, p.CreateDate AS CreatedAt, p.AppliedDate AS AppliedAt,
       p.Amount, p.GrossAmount, p.WalletAmount, p.RebateAmount,
       p.IsSuccess, p.IsOnline, p.TypeId,
       CAST(CASE WHEN p.ProductOrderId IS NOT NULL THEN 1 ELSE 0 END AS bit) AS ForShopOrder,
       CAST(CASE WHEN p.CompanionReserveId IS NOT NULL THEN 1 ELSE 0 END AS bit) AS ForCompanionReserve,
       CAST(CASE WHEN p.TripId IS NOT NULL OR p.CargoId IS NOT NULL THEN 1 ELSE 0 END AS bit) AS ForTrip
FROM dbo.Payments AS p;
GO

-- 3) The reader. Password comes from the sqlcmd variable; it is not stored in this file.
--    Server login + database user (the Pastil database is not "contained", so a login is needed).
--    The login has no server roles and no rights outside this database.
IF SUSER_ID(N'tarantula_reader') IS NULL
    EXEC (N'CREATE LOGIN [tarantula_reader] WITH PASSWORD = N''$(ReaderPassword)'', DEFAULT_DATABASE = [master], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;');
ELSE
    EXEC (N'ALTER LOGIN [tarantula_reader] WITH PASSWORD = N''$(ReaderPassword)'';');
IF DATABASE_PRINCIPAL_ID(N'tarantula_reader') IS NULL
    CREATE USER [tarantula_reader] FOR LOGIN [tarantula_reader];
GO

GRANT SELECT ON SCHEMA::[tarantula] TO [tarantula_reader];
-- Belt and braces: even if someone later adds the user to a role, dbo stays closed.
-- (Views still work: the schemas share an owner, so ownership chaining applies.)
DENY  SELECT, INSERT, UPDATE, DELETE, EXECUTE, ALTER, CONTROL ON SCHEMA::[dbo] TO [tarantula_reader];
REVOKE CONTROL ON SCHEMA::[tarantula] FROM [tarantula_reader];  -- clears a stale DENY from earlier drafts
-- Do NOT deny CONTROL here: DENY CONTROL also removes SELECT (found while validating on a scratch DB).
DENY  INSERT, UPDATE, DELETE, EXECUTE, ALTER ON SCHEMA::[tarantula] TO [tarantula_reader];
GO

-- 4) Self-check: prints what the reader can see; every column list below must contain no PII.
SELECT TABLE_NAME AS [view], STRING_AGG(COLUMN_NAME, N', ') WITHIN GROUP (ORDER BY ORDINAL_POSITION) AS columns_exposed
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'tarantula'
GROUP BY TABLE_NAME
ORDER BY TABLE_NAME;
GO

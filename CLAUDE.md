# Tarantula (تارانتولا)

User-behaviour analytics + marketing platform. Black and orange UI, Persian first, RTL. This repo is **independent from Pastil** (Pastil is only the first project that will connect to it). Never point Tarantula at Pastil's production database with a writable user; read-only copy/replica only.

UI font: **Yekan Bakh (یکان بخ)** everywhere (dashboard, demo pages, future console, slides). It is proprietary (FontIran, user holds the Pro pack), so `assets/fonts/*.woff2` are git-ignored and never redistributed. `assets/fonts/yekan-bakh.css` defines the faces and `--font-ui` (falls back to Vazirmatn, then system fonts). Use `font-family: var(--font-ui)`; do not add other UI fonts.

Console UI rules (iOS Liquid Glass, from `docs/design/IOS_DESIGN_NOTES.md`): glass (`.glass`) only for the navigation/control layer (sidebar, header, tab bar, popovers); content uses plain `.card` surfaces, never glass. Radii are concentric (`--r-*`), touch targets >= 44 px, text styles in `tokens.css` (min 11 px, no Light weights), text on orange is black, `prefers-reduced-transparency/contrast/motion` must keep working. Sidebar max two levels, never hidden by default. Logical CSS properties only (RTL). Items not built yet are shown dimmed with a "به‌زودی" badge, never as fake data. Run: `cd console && npm install && npm run dev` (port 3100; needs the API on 5080).

Wording: write **User** and **Vendor** in English everywhere (code, docs, UI copy), never «کاربر» / «وندار».

## Layout

| Path | What |
|---|---|
| `src/Tarantula.Domain` | Entities and rules. No dependencies. (`TrackedEvent`, `ConsentLevel`, `EventSanitizer`) |
| `src/Tarantula.Application` | Use cases and ports (`ICommandHandler`, `IEventStore`, `IProjectRegistry`). Depends only on Domain. |
| `src/Tarantula.Infrastructure` | Adapters: ClickHouse event store, config project registry. Depends on Application. |
| `src/Tarantula.Ingest.Api` | Minimal API, `POST /v1/collect`. Depends on Application + Infrastructure. |
| `sdk/web` | TypeScript web SDK (esbuild, vitest). |
| `console` | Nuxt 4 SPA (RTL, black/orange, iOS Liquid Glass). Layout copied from the Pastil panel: right sidebar (collapses to an 84 px rail), floating header, popovers, mobile sheet + tab bar. Reaches the API only through its own server routes (`server/api/live/stream.get.ts`) so the read key stays server-side. |
| `docs/design/IOS_DESIGN_NOTES.md` | Apple HIG rules (read from Apple's site 2026-11-16) that the console follows. Read before touching UI. |
| `tests/*` | xUnit: Domain, Application, Ingest.Api (WebApplicationFactory). |
| `deploy/docker-compose.yml` | Local ClickHouse. |
| `demo/index.html` | Manual test page for the SDK. |

## Rules

1. **Clean Architecture + SOLID.** Dependencies point inward only (Api/Infrastructure -> Application -> Domain). Domain and Application never reference ASP.NET, ClickHouse or any framework type. New behaviour = a new use case (command + handler registered by hand in `Application/DependencyInjection.cs`) + a port if it needs I/O.
2. **Privacy is a domain rule.** Consent travels with every event; `EventSanitizer` decides what may be stored. A missing consent value means `none` (drop). The server re-applies consent even though the SDK does too.
3. **Expected failures are `Result<T>` values, not exceptions.** The ingest API uses real HTTP status codes (202/400/401/403/413/429), unlike Pastil's HTTP-200 envelope, because SDKs and `sendBeacon` need them.
4. No MediatR/AutoMapper (keep dependencies few, handlers explicit).
5. .NET 9 (`net9.0`), `TreatWarningsAsErrors` on. SDK 10 is installed but targets net9.0.
6. Tests first for rules (consent, validation); every handler and endpoint has tests. Run: `dotnet test Tarantula.sln` and `cd sdk/web && npm test`.
7. Secrets never in git. The dev ClickHouse password and `tk_dev_local_only` key are for local use only.

## Run locally

```bash
docker compose -f deploy/docker-compose.yml up -d        # ClickHouse
dotnet run --project src/Tarantula.Ingest.Api --urls http://localhost:5080
cd sdk/web && npm install && npm run build               # builds dist/tarantula.min.js
# open demo/index.html through any static server, e.g. npx serve .
```

## Online Users (live count)

- Definition: **online User = distinct `anonymous_id` with at least one event whose server `received_at` is within the window** (default 5 min, 30 s to 30 min). Server time, so a wrong device clock cannot fake it. Users who refused consent are never counted. `signedInUsers` = distinct `user_id` (only exists with `full` consent). Top pages = the path of each online User's most recent event that had a URL.
- The SDK sends `$heartbeat` every 30 s while the tab is visible, so readers who do not click still count; hidden tabs stop counting.
- Read endpoints need the **secret read key** (`X-Tarantula-Read-Key`, or `rk` query for EventSource). The write key (public, in page source) can never read.
  - `GET /v1/live?window=300` one snapshot; `GET /v1/live/stream` Server-Sent Events, one `snapshot` event every 3 s (max 50 open streams).
  - `CachedLiveStatsReader` (decorator) shares one ClickHouse query per project+window for 2 s, so more viewers do not add load.
- Dashboard: `demo/live.html` (black/orange, Persian). The console will replace it, with session auth instead of a key in the URL.
- Known limit: with `anonymous` consent ids change on every page load, so those Users can be over-counted until their old events leave the window.

## Roadmap (Phase 1 = "tar اول")

Done: ingest API + ClickHouse store + consent rules + web SDK (pageview, click/heatmap data, scroll, rage click, sessions, heartbeat) + live online-Users count (snapshot + SSE + dashboard).
Next: session replay (rrweb, `full` consent only, masked by default) -> heatmap query/API -> funnels -> identity graph (PostgreSQL) -> first database connector -> console (Nuxt 4, black/orange).

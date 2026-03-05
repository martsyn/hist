# CLAUDE.md

## Stack
ASP.NET Core 10 + ClickHouse + Vue 3/PrimeVue. Dev: `./dev.sh` (dotnet watch + Docker CH). Prod: `./run.sh`.

## Auth / env
- Tiingo token passed **per-request** via `HttpRequestMessage.Headers.Add("Authorization", $"Token {token}")` — not via `DefaultRequestHeaders` (token may be empty at DI registration time)
- s6 `run` scripts need `#!/command/with-contenv /bin/sh` to inherit Docker env vars
- `.env` loaded by DotNetEnv at startup; also loaded by `dev.sh` for the shell

## ClickHouse gotchas
- Writer network is `::/0` (not localhost-only) — required for dev host access through Docker bridge
- `ReplacingMergeTree` dedup is async; use `FINAL` for immediate consistent reads
- `series_coverage` is the source of truth for the universe — scheduler only re-queues symbols already in it
- Bind-mounting `config.d` requires `docker_related_config.xml` to also be present (copied from official image)
- Named volume `hist-ch-data` shared between dev and prod containers; ClickHouse built-in locking prevents concurrent access
- Stale `/var/lib/clickhouse/status` lock file from unclean shutdown causes exit 76 — `dev.sh` removes it on start

## Tiingo
- IEX intraday returns most recent N bars when no `endDate` — must paginate **backwards** using `endDate = earliest - 1 day`
- Volume comes back as float string (e.g. `"2614.0"`) — parse as `(ulong)decimal.Parse(...)`
- EOD endpoint: `/tiingo/daily/{sym}/prices?startDate=...&format=csv`
- IEX intraday: `/iex/{sym}/prices?startDate=...&resampleFreq=1min&columns=date,open,high,low,close,volume&format=csv`

## Scheduling
- 5 Quartz jobs (one per DataType), cron configurable via `.env` `SCHEDULE_*` vars
- New symbols need one manual enqueue before scheduler picks them up

## Frontend
- PrimeVue `TabPanel` keeps all panels mounted — don't use `v-if` on direct children, use `onActivated`/`onDeactivated`
- Vite dev server on `:5173`, API on `:8088`; vite.config proxies `/api` to backend

## OoplesFinance earnings
- `GetEarningsHistoryAsync` returns only ~4 recent quarters, no revenue data — known limitation of Yahoo's endpoint

# hist

Design and code a single Alpine Linux container hosting both a ClickHouse server and a C# web server, managed by s6-overlay as the process supervisor.

Two ports are exposed:
- **HTTP** on a configurable port (default: 8088) — web UI and REST API
- **ClickHouse native TCP** (port 9000) — external read-only access

The C# web server communicates with ClickHouse locally via Unix domain socket using a write-enabled user. External ClickHouse access is restricted to a read-only user on TCP port 9000. The ClickHouse HTTP interface is disabled. The ClickHouse.Net client library is used (native TCP protocol).

## Data Collection

The C# web server collects historical financial data via adapters (fixed set, compile-time). All data comes from Yahoo Finance using two libraries:
- **YahooQuotesApi** (v7.0.5) — price/corporate action data
- **OoplesFinance.YahooFinanceAPI** (v1.7.1) — earnings data

### Adapters
- **Yahoo** — daily bars, minute bars, dividends, splits (via YahooQuotesApi); earnings (via OoplesFinance.YahooFinanceAPI). Prices are unadjusted (raw, not adjusted for dividends or splits).

Each adapter supports a configurable, potentially dynamic number of threads and maintains its own priority queue of collection tasks.

### Default History Depth (when no prior data exists)
- Daily bars: 1 year
- Minute bars: 1 year (collect whatever Yahoo makes available; depth may be limited by the provider)
- Earnings: 5 years

### Scheduled Collection

The C# app includes a built-in scheduler (Quartz.NET) for automated daily collection runs. Scheduled jobs appear in the web UI alongside manual tasks and can be managed (enabled/disabled, rescheduled) from there.

### Priority Queue

Numeric priority: 0 = highest, 1 = aboveNormal, 2 = normal (default for queued tasks), 3 = belowNormal (default for scheduled tasks), 4 = lowest.

If the same symbol + data type is queued again:
- Priority is updated if the new priority is higher (lower number)
- Start timestamp is updated if the new start is earlier
- Otherwise ignored (no duplicate enqueue)

### Series Integrity

Each data series has a start and end date/time. Series may be prepended (extended to an earlier start) or appended (extended to a later end). Within the series range, there must be no uncollected data — gaps are only acceptable for weekends, market holidays, and other naturally absent periods.

## REST API

Simple REST endpoints for queuing collection tasks. Parameters:
- **data_type** — `daily_bars`, `minute_bars`, `dividends`, `splits`, `earnings`
- **symbols** — list of ticker symbols
- **start** (optional) — accepted formats:
  - `YYYY-MM-DD` (interpreted as UTC midnight)
  - `YYYY-MM-DD HH:MM:SS` (UTC)
  - Integer Unix seconds
  - Full ISO 8601 with timezone (converted to UTC)
  - If omitted: resumes from latest stored data; if no data exists, uses the default depth above
- **priority** (optional, default: 2)

Multiple symbols may be queued in a single request.

## Storage

ClickHouse is used for all data storage:
- Timestamps stored in UTC
- Dates stored as ClickHouse `Date` type

## Configuration

API keys and secrets loaded from a `.env` file.

## Web Interface

Built with Vue + PrimeVue. The UI displays:
- Current symbol universe ()
- Active and pending collection tasks with status
- Scheduled jobs and their next run times
- Controls for reprioritization, reordering, cancellation of queued tasks, and management of scheduled jobs

## Security

- HTTP only (no TLS)
- No authentication
- ClickHouse write access restricted to local Unix socket only
- ClickHouse read-only user available on TCP port 9000 for external clients
- ClickHouse HTTP interface disabled

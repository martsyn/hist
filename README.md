# hist

Financial data collection and API server. Collects historical OHLCV, dividends, splits, and earnings from Tiingo. Stores data in ClickHouse. Exposes a REST API and Vue/PrimeVue web UI.

## Stack

- **Backend**: ASP.NET Core 10 (C#)
- **Database**: ClickHouse (ReplacingMergeTree, adjusted views computed on-the-fly)
- **Frontend**: Vue 3 + PrimeVue
- **Production**: Proxmox LXC container (Debian 12), ClickHouse and app as separate systemd services

## Development

Requires: Docker, .NET 10 SDK, Node 22.

```bash
cp .env.example .env
# edit .env — set TIINGO_TOKEN at minimum
./dev.sh
```

`dev.sh` starts ClickHouse in Docker (named volume `hist-ch-data`) and runs the .NET app with `dotnet watch`. Frontend dev server (`npm run dev` in `frontend/`) proxies `/api` to `:8088`.

## Production deployment (Proxmox LXC)

### First-time setup

Run once on a Proxmox node with a Debian 12 LXC template available:

```bash
# Download template if needed (run on Proxmox host):
# pveam download local debian-12-standard_12.12-1_amd64.tar.zst

cp .env.example .env
# edit .env with real credentials

./setup-lxc.sh [proxmox-host] [lxc-id]
# defaults: root@10.0.1.3, LXC 201
```

This creates LXC container 201 with:
- ClickHouse 26.x as a systemd service
- ASP.NET Core 10 runtime
- App files at `/app/hist/` inside the container
- ClickHouse data at `/var/lib/hist-clickhouse` on the **Proxmox host** (bind-mounted), surviving container replacements

Then deploy the app for the first time:

```bash
./deploy.sh
```

### Deploying updates

```bash
./deploy.sh [proxmox-host] [lxc-id]
```

Builds frontend and .NET app locally, uploads to the LXC container, extracts in-place, and restarts the `hist` systemd service. ClickHouse is not touched.

### ClickHouse data

Data lives at `/var/lib/hist-clickhouse` on the Proxmox host. It is bind-mounted into the LXC at `/var/lib/clickhouse`. Replacing or rebuilding the LXC container does not affect data.

To back up: snapshot or `rsync` `/var/lib/hist-clickhouse` on the Proxmox host.

## Configuration

Copy `.env.example` to `.env` and set:

| Variable | Description |
|---|---|
| `TIINGO_TOKEN` | Tiingo API token |
| `CH_WRITE_PASSWORD` | ClickHouse writer password |
| `CH_READ_PASSWORD` | ClickHouse reader password |
| `SCHEDULE_*` | Quartz cron expressions for each data type |

## API

```
POST   /api/queue              Enqueue collection tasks
GET    /api/queue              List pending + active tasks
DELETE /api/queue/{id}         Cancel a task
GET    /api/universe           Symbol list with coverage per data type
GET    /api/schedules          Quartz job list with next fire time
PATCH  /api/schedules/{id}     Update cron / enable / disable
```

## Data model

Raw prices are stored unadjusted. Adjusted views are computed on-the-fly:

- `daily_bars_adjusted` — split + dividend adjusted daily OHLCV
- `minute_bars_adjusted` — split adjusted 1-minute OHLCV

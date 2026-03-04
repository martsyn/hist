const BASE = (import.meta.env.VITE_API_BASE as string | undefined) ?? ''

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options
  })
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  if (res.status === 204) return undefined as T
  return res.json()
}

// ── Queue ────────────────────────────────────────────────────────────────────

export interface QueueTask {
  id: string
  symbol: string
  data_type: string
  priority: number
  status: string
  enqueued_at: string
  error?: string
}

export interface QueueResponse {
  pending: QueueTask[]
  active: QueueTask[]
}

export function getQueue(): Promise<QueueResponse> {
  return request('/api/queue')
}

export function enqueueTask(payload: {
  data_type: string
  symbols: string[]
  start?: string
  priority?: number
}): Promise<{ enqueued: number }> {
  return request('/api/queue', { method: 'POST', body: JSON.stringify(payload) })
}

export function cancelTask(id: string): Promise<void> {
  return request(`/api/queue/${id}`, { method: 'DELETE' })
}

export function updateTaskPriority(id: string, priority: number): Promise<void> {
  return request(`/api/queue/${id}`, {
    method: 'PATCH',
    body: JSON.stringify({ priority })
  })
}

// ── Universe ─────────────────────────────────────────────────────────────────

export interface CoverageEntry {
  data_type: string
  start_date?: string
  start_ts?: string
  end_date?: string
  end_ts?: string
  updated_at: string
}

export interface UniverseEntry {
  symbol: string
  coverage: CoverageEntry[]
}

export function getUniverse(): Promise<UniverseEntry[]> {
  return request('/api/universe')
}

// ── Schedules ─────────────────────────────────────────────────────────────────

export interface Schedule {
  id: string
  group: string
  cron?: string
  next_fire?: string
  enabled: boolean
  state: string
}

export function getSchedules(): Promise<Schedule[]> {
  return request('/api/schedules')
}

export function updateSchedule(id: string, payload: { enabled?: boolean; cron?: string }): Promise<void> {
  return request(`/api/schedules/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(payload)
  })
}

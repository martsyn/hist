<script setup lang="ts">
import { ref, onMounted, onUnmounted, onActivated, onDeactivated } from 'vue'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Select from 'primevue/select'
import { useToast } from 'primevue/usetoast'
import { getQueue, cancelTask, updateTaskPriority, type QueueTask } from '../api/client'

const toast = useToast()
const loading = ref(false)
const pending = ref<QueueTask[]>([])
const active = ref<QueueTask[]>([])
let interval: ReturnType<typeof setInterval> | null = null

const PRIORITIES = [
  { label: '0 — Highest', value: 0 },
  { label: '1 — High', value: 1 },
  { label: '2 — Normal', value: 2 },
  { label: '3 — Low', value: 3 },
  { label: '4 — Lowest', value: 4 },
]

async function load() {
  loading.value = true
  try {
    const data = await getQueue()
    pending.value = data.pending
    active.value = data.active
    if (data.pending.length === 0 && data.active.length === 0 && interval) {
      clearInterval(interval)
      interval = null
    }
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Error', detail: e.message, life: 4000 })
  } finally {
    loading.value = false
  }
}

async function cancel(id: string) {
  try {
    await cancelTask(id)
    await load()
    toast.add({ severity: 'info', summary: 'Cancelled', life: 2000 })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Error', detail: e.message, life: 4000 })
  }
}

async function setPriority(id: string, priority: number) {
  try {
    await updateTaskPriority(id, priority)
    await load()
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Error', detail: e.message, life: 4000 })
  }
}

function fmtTime(v: string) {
  return new Date(v).toLocaleTimeString()
}

function startPolling() {
  if (!interval) interval = setInterval(load, 5000)
}

function stopPolling() {
  if (interval) { clearInterval(interval); interval = null }
}

onMounted(() => { load(); startPolling() })
onUnmounted(stopPolling)
onActivated(() => { load(); startPolling() })
onDeactivated(stopPolling)
</script>

<template>
  <div>
    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:1rem;">
      <span>
        <strong>{{ active.length }}</strong> active &nbsp;|&nbsp;
        <strong>{{ pending.length }}</strong> pending
      </span>
      <Button icon="pi pi-refresh" severity="secondary" @click="load" :loading="loading" size="small" />
    </div>

    <h3 style="margin-bottom:0.5rem;">Active</h3>
    <DataTable :value="active" size="small" stripedRows style="margin-bottom:1.5rem;">
      <Column field="symbol" header="Symbol" />
      <Column field="data_type" header="Type" />
      <Column field="priority" header="Priority" />
      <Column field="enqueued_at" header="Enqueued">
        <template #body="{ data }">{{ fmtTime(data.enqueued_at) }}</template>
      </Column>
      <Column header="Status">
        <template #body="{ data }">
          <span style="color:#2196f3;">{{ data.status }}</span>
        </template>
      </Column>
    </DataTable>

    <h3 style="margin-bottom:0.5rem;">Pending</h3>
    <DataTable :value="pending" size="small" stripedRows paginator :rows="25">
      <Column field="symbol" header="Symbol" sortable />
      <Column field="data_type" header="Type" sortable />
      <Column header="Priority" sortable field="priority">
        <template #body="{ data }">
          <Select
            :model-value="data.priority"
            :options="PRIORITIES"
            option-label="label"
            option-value="value"
            @update:model-value="v => setPriority(data.id, v)"
            style="min-width:130px;"
          />
        </template>
      </Column>
      <Column field="enqueued_at" header="Enqueued">
        <template #body="{ data }">{{ fmtTime(data.enqueued_at) }}</template>
      </Column>
      <Column header="">
        <template #body="{ data }">
          <Button icon="pi pi-times" severity="danger" size="small" text @click="cancel(data.id)" />
        </template>
      </Column>
    </DataTable>
  </div>
</template>

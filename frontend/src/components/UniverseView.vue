<script setup lang="ts">
import { ref, onMounted } from 'vue'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Dialog from 'primevue/dialog'
import { useToast } from 'primevue/usetoast'
import { getUniverse, enqueueTask, type UniverseEntry } from '../api/client'

const toast = useToast()
const loading = ref(false)
const rows = ref<UniverseEntry[]>([])

const DATA_TYPES = ['daily_bars', 'minute_bars', 'dividends', 'splits', 'earnings']

const showEnqueue = ref(false)
const enqSymbols = ref('')
const enqType = ref('daily_bars')
const enqPriority = ref('2')

async function load() {
  loading.value = true
  try {
    rows.value = await getUniverse()
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Error', detail: e.message, life: 4000 })
  } finally {
    loading.value = false
  }
}

async function doEnqueue() {
  const symbols = enqSymbols.value.split(/[\s,]+/).map(s => s.trim()).filter(Boolean)
  if (!symbols.length) return
  try {
    const result = await enqueueTask({ data_type: enqType.value, symbols, priority: parseInt(enqPriority.value) })
    toast.add({ severity: 'success', summary: 'Enqueued', detail: `${result.enqueued} tasks added`, life: 3000 })
    showEnqueue.value = false
    enqSymbols.value = ''
    setTimeout(load, 8000)
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Error', detail: e.message, life: 4000 })
  }
}

function getCoverage(row: UniverseEntry, type: string) {
  return row.coverage.find(c => c.data_type === type)
}

function fmtDate(v?: string | null) {
  if (!v) return '—'
  return v.substring(0, 10)
}

function fmtCoverage(row: UniverseEntry, type: string) {
  const c = getCoverage(row, type)
  if (!c) return null
  const start = c.start_date ?? c.start_ts
  const end = c.end_date ?? c.end_ts
  return `${fmtDate(start)} → ${fmtDate(end)}`
}

onMounted(load)
</script>

<template>
  <div>
    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:1rem;">
      <span style="font-weight:600;">{{ rows.length }} symbols</span>
      <div style="display:flex; gap:0.5rem;">
        <Button icon="pi pi-refresh" severity="secondary" @click="load" :loading="loading" />
        <Button label="Enqueue" icon="pi pi-plus" @click="showEnqueue = true" />
      </div>
    </div>

    <DataTable :value="rows" :loading="loading" stripedRows size="small"
               paginator :rows="50" scrollable scrollHeight="70vh">
      <Column field="symbol" header="Symbol" frozen sortable style="min-width:90px;" />
      <Column v-for="dt in DATA_TYPES" :key="dt" :header="dt.replace('_', ' ')">
        <template #body="{ data }">
          <span v-if="fmtCoverage(data, dt)" style="font-size:0.8rem;">{{ fmtCoverage(data, dt) }}</span>
          <span v-else style="color:#aaa;">—</span>
        </template>
      </Column>
    </DataTable>

    <Dialog v-model:visible="showEnqueue" header="Enqueue Collection" modal style="width:420px;">
      <div style="display:flex; flex-direction:column; gap:1rem; padding-top:0.5rem;">
        <div>
          <label style="display:block; margin-bottom:0.25rem; font-size:0.85rem;">Symbols (comma or space separated)</label>
          <InputText v-model="enqSymbols" style="width:100%;" placeholder="AAPL MSFT GOOG" />
        </div>
        <div>
          <label style="display:block; margin-bottom:0.25rem; font-size:0.85rem;">Data Type</label>
          <Select v-model="enqType" :options="DATA_TYPES" style="width:100%;" />
        </div>
        <div>
          <label style="display:block; margin-bottom:0.25rem; font-size:0.85rem;">Priority (0=highest, 4=lowest)</label>
          <InputText v-model="enqPriority" type="number" min="0" max="4" style="width:100%;" />
        </div>
        <div style="display:flex; justify-content:flex-end; gap:0.5rem;">
          <Button label="Cancel" severity="secondary" @click="showEnqueue = false" />
          <Button label="Enqueue" @click="doEnqueue" />
        </div>
      </div>
    </Dialog>
  </div>
</template>

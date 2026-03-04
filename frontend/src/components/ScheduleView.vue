<script setup lang="ts">
import { ref, onMounted } from 'vue'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import ToggleSwitch from 'primevue/toggleswitch'
import { useToast } from 'primevue/usetoast'
import { getSchedules, updateSchedule, type Schedule } from '../api/client'

const toast = useToast()
const loading = ref(false)
const schedules = ref<Schedule[]>([])
const editingCron = ref<Record<string, string>>({})

async function load() {
  loading.value = true
  try {
    schedules.value = await getSchedules()
    editingCron.value = Object.fromEntries(schedules.value.map(s => [s.id, s.cron ?? '']))
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Error', detail: e.message, life: 4000 })
  } finally {
    loading.value = false
  }
}

async function toggleEnabled(schedule: Schedule) {
  try {
    await updateSchedule(schedule.id, { enabled: !schedule.enabled })
    await load()
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Error', detail: e.message, life: 4000 })
  }
}

async function saveCron(schedule: Schedule) {
  const cron = editingCron.value[schedule.id]
  if (!cron) return
  try {
    await updateSchedule(schedule.id, { cron })
    await load()
    toast.add({ severity: 'success', summary: 'Saved', life: 2000 })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Error', detail: e.message, life: 4000 })
  }
}

function fmtNextFire(v?: string) {
  if (!v) return '—'
  return new Date(v).toLocaleString()
}

onMounted(load)
</script>

<template>
  <div>
    <div style="display:flex; justify-content:flex-end; margin-bottom:1rem;">
      <Button icon="pi pi-refresh" severity="secondary" @click="load" :loading="loading" size="small" />
    </div>

    <DataTable :value="schedules" :loading="loading" size="small" stripedRows>
      <Column field="id" header="Job" />
      <Column header="Enabled">
        <template #body="{ data }">
          <ToggleSwitch :model-value="data.enabled" @update:model-value="() => toggleEnabled(data)" />
        </template>
      </Column>
      <Column header="Cron Expression" style="min-width:200px;">
        <template #body="{ data }">
          <div style="display:flex; gap:0.5rem; align-items:center;">
            <InputText v-model="editingCron[data.id]" style="font-family:monospace; width:180px;" size="small" />
            <Button icon="pi pi-check" size="small" text @click="saveCron(data)" />
          </div>
        </template>
      </Column>
      <Column header="Next Fire">
        <template #body="{ data }">{{ fmtNextFire(data.next_fire) }}</template>
      </Column>
      <Column field="state" header="State" />
    </DataTable>
  </div>
</template>

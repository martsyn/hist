<script setup lang="ts">
import { ref } from 'vue'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import Toast from 'primevue/toast'
import UniverseView from './components/UniverseView.vue'
import QueueView from './components/QueueView.vue'
import ScheduleView from './components/ScheduleView.vue'

const activeTab = ref('universe')
</script>

<template>
  <div style="padding: 1rem; max-width: 1400px; margin: 0 auto;">
    <h1 style="margin-bottom: 1rem; font-size: 1.5rem; font-weight: 700;">hist</h1>
    <Tabs v-model:value="activeTab">
      <TabList>
        <Tab value="universe">Universe</Tab>
        <Tab value="queue">Queue</Tab>
        <Tab value="schedules">Schedules</Tab>
      </TabList>
      <TabPanels>
        <TabPanel value="universe"><UniverseView /></TabPanel>
        <TabPanel value="queue"><QueueView /></TabPanel>
        <TabPanel value="schedules"><ScheduleView /></TabPanel>
      </TabPanels>
    </Tabs>
    <Toast />
  </div>
</template>

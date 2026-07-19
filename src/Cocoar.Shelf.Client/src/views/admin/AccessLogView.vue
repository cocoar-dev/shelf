<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { CoarDataGrid, CoarGridBuilder } from '@cocoar/vue-data-grid';
import { CoarContextMenu, CoarMenuItem, useContextMenu } from '@cocoar/vue-ui';
import { http } from '@/core/api/http';
import { countryFlag, countryName } from '@/core/geo';

interface AccessLogRow {
  id: string;
  timestamp: string;
  ip: string;
  product: string | null;
  version: string | null;
  path: string | null;
  userAgent: string | null;
  referer: string | null;
  country: string | null;
  city: string | null;
}

const rows = ref<AccessLogRow[]>([]);
const total = ref(0);
const viewportMenu = useContextMenu();

const builder = CoarGridBuilder.create<AccessLogRow>()
  .persistColumnState('admin-access-log-v3')
  .option('getRowId', (p: any) => p.data.id)
  .rowDataRef(rows)
  .searchHighlight()
  .onViewportContextMenu(($event: any) => {
    viewportMenu.open($event);
  })
  .columns([
    (col: any) => col.field('timestamp').header('Time').width(190).option('minWidth', 170)
      .option('valueFormatter', (p: any) => p.value ? new Date(p.value).toLocaleString() : ''),
    (col: any) => col.field('ip').header('IP').width(130).option('minWidth', 120),
    (col: any) => col.field('product').header('Product').width(140).option('minWidth', 120),
    (col: any) => col.field('version').header('Version').width(90).option('minWidth', 85),
    (col: any) => col.field('path').header('Path').flex(1).option('minWidth', 140),
    (col: any) => col.field('country').header('Country').width(160).option('minWidth', 120)
      .option('valueFormatter', (p: any) => p.value ? `${countryFlag(p.value)} ${countryName(p.value)}` : ''),
    (col: any) => col.field('city').header('City').width(120).option('minWidth', 90),
    (col: any) => col.field('userAgent').header('User Agent').flex(1).option('minWidth', 200),
  ]);

async function load() {
  const result = await http.get<{ total: number; entries: AccessLogRow[] }>('/analytics/visits?limit=500');
  rows.value = result.entries;
  total.value = result.total;
}

onMounted(load);
</script>

<template>
  <div class="flex flex-1 flex-col min-w-0 p-4">
    <CoarDataGrid :builder="builder" show-search class="flex-1 min-h-0" bordered elevated>
      <template #toolbar-right>
        <span class="total-hint">{{ rows.length }} of {{ total }} entries</span>
      </template>
    </CoarDataGrid>

    <CoarContextMenu :menu="viewportMenu">
      <CoarMenuItem label="Refresh" icon="refresh-cw" @clicked="load" />
    </CoarContextMenu>
  </div>
</template>

<style scoped>
.total-hint {
  font-size: 0.8rem;
  color: var(--coar-text-neutral-secondary);
  align-self: center;
  white-space: nowrap;
}
</style>

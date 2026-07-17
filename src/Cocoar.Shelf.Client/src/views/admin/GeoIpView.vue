<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { CoarCard, CoarButton, CoarTag, CoarNote } from '@cocoar/vue-ui';
import { http } from '@/core/api/http';

interface GeoStatus {
  loaded: boolean;
  lastUpdated: string | null;
}

const status = ref<GeoStatus | null>(null);
const downloading = ref(false);
const message = ref('');
const error = ref('');

async function loadStatus() {
  try {
    status.value = await http.get<GeoStatus>('/analytics/geo/status');
  } catch {
    error.value = 'Failed to load GeoIP status';
  }
}

async function download() {
  message.value = '';
  error.value = '';
  downloading.value = true;
  try {
    const result = await http.post<GeoStatus & { ok: boolean }>('/analytics/geo/download');
    status.value = { loaded: result.loaded, lastUpdated: result.lastUpdated };
    message.value = 'GeoIP database downloaded and loaded.';
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Download failed';
  } finally {
    downloading.value = false;
  }
}

onMounted(loadStatus);
</script>

<template>
  <div class="p-4 flex flex-col gap-4" style="max-width: 640px">
    <CoarCard title="GeoIP Database">
      <p class="desc">
        Resolves visitor IPs to country and city for the access log, using the free
        DB-IP Lite database. The database is cached in memory — re-download monthly
        for fresh data.
      </p>

      <div class="status-row">
        <span class="status-label">Status</span>
        <CoarTag :variant="status?.loaded ? 'success' : 'neutral'" size="s">
          {{ status?.loaded ? 'Loaded' : 'Not loaded' }}
        </CoarTag>
      </div>
      <div class="status-row">
        <span class="status-label">Last updated</span>
        <span class="status-value">
          {{ status?.lastUpdated ? new Date(status.lastUpdated).toLocaleString() : '—' }}
        </span>
      </div>

      <CoarNote v-if="message" variant="success" class="mt-3">{{ message }}</CoarNote>
      <CoarNote v-if="error" variant="error" class="mt-3">{{ error }}</CoarNote>

      <CoarButton
        variant="primary"
        size="s"
        class="mt-4"
        :loading="downloading"
        @click="download"
      >
        {{ status?.loaded ? 'Update Database' : 'Download Database' }}
      </CoarButton>
    </CoarCard>
  </div>
</template>

<style scoped>
.desc {
  font-size: 0.85rem;
  color: var(--coar-text-neutral-secondary);
  margin: 0 0 16px;
}

.status-row {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 6px 0;
  font-size: 0.9rem;
}

.status-label {
  min-width: 110px;
  color: var(--coar-text-neutral-secondary);
}

.status-value {
  color: var(--coar-text-neutral-primary);
}

.mt-3 { margin-top: 12px; }
.mt-4 { margin-top: 16px; }
</style>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { CoarCard, CoarButton, CoarTag, CoarNote, CoarFormField, CoarTextInput } from '@cocoar/vue-ui';
import { http } from '@/core/api/http';
import type { ShelfSettingsInfo } from '@/core/models/shelf.models';

const settings = ref<ShelfSettingsInfo | null>(null);
const newKey = ref('');
const saving = ref(false);
const message = ref('');
const error = ref('');

async function load() {
  try {
    settings.value = await http.get<ShelfSettingsInfo>('/settings/');
  } catch {
    error.value = 'Failed to load settings';
  }
}

function generateKey() {
  const bytes = new Uint8Array(24);
  crypto.getRandomValues(bytes);
  newKey.value = 'shelf_' + btoa(String.fromCharCode(...bytes))
    .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

async function saveKey() {
  message.value = '';
  error.value = '';
  saving.value = true;
  try {
    await http.put('/settings/api-key', { apiKey: newKey.value });
    message.value = 'Master API key saved. Store it now — it cannot be displayed again.';
    await load();
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to save key';
  } finally {
    saving.value = false;
  }
}

async function removeKey() {
  if (!confirm('Remove the UI-managed master API key? CI pipelines using it will stop working.')) return;
  message.value = '';
  error.value = '';
  saving.value = true;
  try {
    await http.put('/settings/api-key', { apiKey: null });
    newKey.value = '';
    message.value = 'Master API key removed.';
    await load();
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to remove key';
  } finally {
    saving.value = false;
  }
}

onMounted(load);
</script>

<template>
  <div class="p-4 flex flex-col gap-4" style="max-width: 720px">
    <CoarCard>
      <template #header><h2 class="card-title">Master API Key</h2></template>

      <p class="desc">
        Authorizes CI/CD uploads and product management via
        <code>Authorization: Bearer &lt;key&gt;</code> for <strong>all</strong> products.
        Keys are write-only — the value is never shown again after saving. For per-product
        keys, use the API Key field on the product itself.
      </p>

      <div class="status-row">
        <span class="status-label">UI-managed key</span>
        <CoarTag :variant="settings?.hasMasterApiKey ? 'success' : 'neutral'" size="s">
          {{ settings?.hasMasterApiKey ? 'Set' : 'Not set' }}
        </CoarTag>
      </div>
      <div class="status-row">
        <span class="status-label">Config/env key</span>
        <CoarTag :variant="settings?.hasConfigApiKey ? 'success' : 'neutral'" size="s">
          {{ settings?.hasConfigApiKey ? 'Set' : 'Not set' }}
        </CoarTag>
        <span class="status-hint">from deployment config; stays valid alongside</span>
      </div>

      <div class="key-row mt-4">
        <CoarFormField
          :label="settings?.hasMasterApiKey ? 'Replace key' : 'New key'"
          hint="At least 16 characters"
          class="flex-1"
        >
          <CoarTextInput v-model="newKey" placeholder="shelf_…" clearable />
        </CoarFormField>
        <CoarButton variant="secondary" size="s" class="key-row-btn" @click="generateKey">Generate</CoarButton>
      </div>

      <CoarNote v-if="message" variant="success" class="mt-3">{{ message }}</CoarNote>
      <CoarNote v-if="error" variant="error" class="mt-3">{{ error }}</CoarNote>

      <div class="actions mt-4">
        <CoarButton
          v-if="settings?.hasMasterApiKey"
          variant="ghost"
          size="s"
          :disabled="saving"
          @click="removeKey"
        >
          Remove Key
        </CoarButton>
        <CoarButton
          variant="primary"
          size="s"
          :loading="saving"
          :disabled="newKey.trim().length < 16"
          @click="saveKey"
        >
          Save Key
        </CoarButton>
      </div>
    </CoarCard>
  </div>
</template>

<style scoped>
.desc {
  font-size: 0.85rem;
  color: var(--coar-text-neutral-secondary);
  margin: 0 0 16px;
  line-height: 1.5;
}

.desc code {
  font-size: 0.8rem;
  background: var(--coar-background-neutral-secondary);
  padding: 1px 5px;
  border-radius: 4px;
}

.status-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 5px 0;
  font-size: 0.9rem;
}

.status-label {
  min-width: 130px;
  color: var(--coar-text-neutral-secondary);
}

.status-hint {
  font-size: 0.78rem;
  color: var(--coar-text-neutral-tertiary);
}

.key-row {
  display: flex;
  gap: 10px;
  align-items: flex-start;
}

.key-row-btn {
  margin-top: 23px;
}

.actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.mt-3 { margin-top: 12px; }
.mt-4 { margin-top: 16px; }
</style>

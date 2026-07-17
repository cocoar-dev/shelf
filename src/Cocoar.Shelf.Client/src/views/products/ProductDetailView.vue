<template>
  <div v-if="!isLoading" class="flex-1 min-w-0 py-6">
    <CoarNote v-if="error" variant="error" class="mb-4">{{ error }}</CoarNote>
    <CoarNote v-if="successMessage" variant="success" class="mb-4">{{ successMessage }}</CoarNote>

    <!-- Product Info -->
    <CoarCard v-if="product" class="mb-4">
      <template #header><h2 class="card-title">Product Info</h2></template>
      <div class="info-grid">
        <div class="info-row">
          <span class="info-label">Name</span>
          <span class="info-value">{{ product.name }}</span>
        </div>
        <div class="info-row">
          <span class="info-label">Display Name</span>
          <span class="info-value">{{ product.displayName || '—' }}</span>
        </div>
        <div class="info-row">
          <span class="info-label">Description</span>
          <span class="info-value">{{ product.description || '—' }}</span>
        </div>
        <div class="info-row">
          <span class="info-label">Source</span>
          <span class="info-value">{{ product.source }}</span>
        </div>
        <div class="info-row">
          <span class="info-label">Visibility</span>
          <span class="info-value">
            <CoarTag :variant="product.visibility === 'preview' ? 'warning' : 'success'" size="s">
              {{ product.visibility }}
            </CoarTag>
          </span>
        </div>
        <div class="info-row">
          <span class="info-label">Latest Version</span>
          <span class="info-value">
            <CoarTag v-if="product.latest" variant="accent" size="s">{{ product.latest }}</CoarTag>
            <span v-else>—</span>
          </span>
        </div>
      </div>
    </CoarCard>

    <!-- Upload Version -->
    <CoarCard class="mb-4">
      <template #header><h2 class="card-title">Upload Version</h2></template>
      <div class="upload-row">
        <CoarFormField label="Version" class="upload-version-input">
          <CoarTextInput v-model="uploadVersion" placeholder="v1.0.0" />
        </CoarFormField>
        <div class="upload-file">
          <label class="file-label">ZIP File</label>
          <div class="file-picker">
            <CoarButton variant="secondary" size="s" @click="fileInput?.click()">Choose File…</CoarButton>
            <span class="file-name" :class="{ 'file-name--empty': !uploadFile }">
              {{ uploadFile?.name ?? 'No file selected' }}
            </span>
            <input ref="fileInput" type="file" accept=".zip" class="file-input-hidden" @change="onFileSelected" />
          </div>
        </div>
        <CoarButton
          variant="primary"
          :disabled="!uploadVersion || !uploadFile || isUploading"
          :loading="isUploading"
          @click="onUpload"
        >
          Upload
        </CoarButton>
      </div>
    </CoarCard>

    <!-- Versions -->
    <CoarCard v-if="product && product.versions.length > 0">
      <template #header><h2 class="card-title">Versions</h2></template>
      <CoarTable variant="plain" hover>
        <thead>
          <tr>
            <th>Version</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="v in product.versions" :key="v">
            <td>
              <a :href="`/${product.name}/${v}/`" target="_blank" class="version-link">{{ v }}</a>
            </td>
            <td>
              <CoarTag v-if="v === product.latest" variant="accent" size="s">latest</CoarTag>
            </td>
            <td class="cell-actions">
              <CoarButton
                variant="danger"
                size="s"
                :loading="deletingVersion === v"
                @click="onDeleteVersion(v)"
              >
                Delete
              </CoarButton>
            </td>
          </tr>
        </tbody>
      </CoarTable>
    </CoarCard>
  </div>

  <div v-else class="center-content">
    <CoarSpinner size="m" label="Loading product..." />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { CoarCard, CoarButton, CoarTextInput, CoarNote, CoarTable, CoarTag, CoarSpinner, CoarFormField } from '@cocoar/vue-ui';
import { useFragmentNavigation, useRoutedModals } from '@cocoar/vue-fragment-parser';
import { useUI } from '@/composables/useUI';
import { shelfApi } from '@/core/api/shelf-api';
import { ApiError } from '@/core/api/http';
import type { Product } from '@/core/models/shelf.models';

const route = useRoute();
const router = useRouter();
const ui = useUI();
useRoutedModals();
const { navigateToModal } = useFragmentNavigation();

// The edit modal lives on the URL hash; when it closes (hash clears) the data may have changed.
watch(() => route.hash, (hash, oldHash) => {
  if (!hash && oldHash) loadProduct();
});

const name = route.params.name as string;
const product = ref<Product | null>(null);
const isLoading = ref(true);
const error = ref('');
const successMessage = ref('');

const uploadVersion = ref('');
const uploadFile = ref<File | null>(null);
const isUploading = ref(false);
const deletingVersion = ref<string | null>(null);
const fileInput = ref<HTMLInputElement | null>(null);

ui.set(ctx => {
  ctx.header.title = name;
  ctx.header.subTitle = 'Product details and version management';
  ctx.footer.show = true;
  ctx.footer.button1.visible = true;
  ctx.footer.button1.text = 'Back';
  ctx.footer.button1.onClick = () => router.push('/admin/products');
  ctx.footer.button2.visible = true;
  ctx.footer.button2.text = 'Delete Product';
  ctx.footer.button2.onClick = () => onDeleteProduct();
  ctx.footer.button3.visible = true;
  ctx.footer.button3.text = 'Edit';
  ctx.footer.button3.onClick = () => navigateToModal(name);
});

onMounted(loadProduct);

async function loadProduct() {
  isLoading.value = true;
  error.value = '';
  try {
    product.value = await shelfApi.getProduct(name);
  } catch {
    error.value = 'Failed to load product';
  } finally {
    isLoading.value = false;
  }
}

function onFileSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  uploadFile.value = input.files?.[0] ?? null;
}

async function onUpload() {
  if (!uploadVersion.value || !uploadFile.value) return;

  isUploading.value = true;
  error.value = '';
  successMessage.value = '';

  try {
    await shelfApi.uploadVersion(name, uploadVersion.value, uploadFile.value);
    successMessage.value = `Version ${uploadVersion.value} uploaded successfully`;
    uploadVersion.value = '';
    uploadFile.value = null;
    if (fileInput.value) fileInput.value.value = '';
    await loadProduct();
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'Upload failed';
  } finally {
    isUploading.value = false;
  }
}

async function onDeleteVersion(version: string) {
  if (!confirm(`Delete version ${version}? This cannot be undone.`)) return;

  deletingVersion.value = version;
  error.value = '';
  successMessage.value = '';

  try {
    await shelfApi.deleteVersion(name, version);
    successMessage.value = `Version ${version} deleted`;
    await loadProduct();
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'Failed to delete version';
  } finally {
    deletingVersion.value = null;
  }
}

async function onDeleteProduct() {
  if (!confirm(`Delete product "${name}"? This will remove the registration but not the documentation files.`)) return;

  try {
    await shelfApi.deleteProduct(name);
    router.push('/admin/products');
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'Failed to delete product';
  }
}
</script>

<style scoped>
.info-grid {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.info-row {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 6px 0;
}

.info-label {
  width: 140px;
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--coar-text-neutral-secondary);
}

.info-value {
  font-size: 0.9rem;
  color: var(--coar-text-neutral-primary);
}

.upload-row {
  display: flex;
  align-items: flex-end;
  gap: 12px;
  flex-wrap: wrap;
}

.upload-version-input {
  width: 200px;
}

.upload-file {
  flex: 1;
  min-width: 200px;
}

.file-label {
  display: block;
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--coar-text-neutral-secondary);
  margin-bottom: 6px;
}

.file-picker {
  display: flex;
  align-items: center;
  gap: 10px;
}

.file-name {
  font-size: 0.85rem;
  color: var(--coar-text-neutral-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-name--empty {
  color: var(--coar-text-neutral-tertiary);
}

.file-input-hidden {
  display: none;
}

.version-link {
  color: var(--coar-text-accent-primary);
  text-decoration: none;
  font-weight: 500;
}
.version-link:hover { text-decoration: underline; }

.cell-actions { text-align: right; }

.mb-4 { margin-bottom: 16px; }
.center-content { display: flex; justify-content: center; padding: 48px 0; }
</style>

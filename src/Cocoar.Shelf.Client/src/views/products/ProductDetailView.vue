<template>
  <div v-if="!isLoading">
    <CoarNote v-if="error" variant="error" class="mb-4">{{ error }}</CoarNote>
    <CoarNote v-if="successMessage" variant="success" class="mb-4">{{ successMessage }}</CoarNote>

    <!-- Product Info -->
    <CoarCard v-if="product" title="Product Info" class="mb-4">
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
          <span class="info-label">Latest Version</span>
          <span class="info-value">
            <span v-if="product.latest" class="badge badge--accent">{{ product.latest }}</span>
            <span v-else>—</span>
          </span>
        </div>
      </div>
    </CoarCard>

    <!-- Upload Version -->
    <CoarCard title="Upload Version" class="mb-4">
      <div class="upload-row">
        <CoarTextInput
          v-model="uploadVersion"
          label="Version"
          placeholder="v1.0.0"
          class="upload-version-input"
        />
        <div class="upload-file">
          <label class="file-label">ZIP File</label>
          <input type="file" accept=".zip" @change="onFileSelected" ref="fileInput" />
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
    <CoarCard v-if="product && product.versions.length > 0" title="Versions">
      <table class="data-table">
        <thead>
          <tr>
            <th>Version</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="v in product.versions" :key="v">
            <td class="cell-name">
              <a :href="`/${product.name}/${v}/`" target="_blank">{{ v }}</a>
            </td>
            <td>
              <span v-if="v === product.latest" class="badge badge--accent">latest</span>
            </td>
            <td class="cell-actions">
              <CoarButton
                variant="danger"
                size="small"
                :loading="deletingVersion === v"
                @click="onDeleteVersion(v)"
              >
                Delete
              </CoarButton>
            </td>
          </tr>
        </tbody>
      </table>
    </CoarCard>
  </div>

  <div v-else class="loading-text">Loading...</div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { CoarCard, CoarButton, CoarTextInput, CoarNote } from '@cocoar/vue-ui';
import { useUI } from '@/composables/useUI';
import { shelfApi } from '@/core/api/shelf-api';
import { ApiError } from '@/core/api/http';
import type { Product } from '@/core/models/shelf.models';

const route = useRoute();
const router = useRouter();
const ui = useUI();

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
  ctx.footer.button3.onClick = () => router.push(`/admin/products/${name}/edit`);
});

onMounted(loadProduct);

async function loadProduct() {
  isLoading.value = true;
  error.value = '';
  try {
    const products = await shelfApi.getProducts();
    product.value = products.find(p => p.name === name) ?? null;
    if (!product.value) {
      error.value = `Product '${name}' not found`;
    }
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

.data-table {
  width: 100%;
  border-collapse: collapse;
}

.data-table th {
  text-align: left;
  padding: 8px 12px;
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--coar-text-neutral-secondary);
  border-bottom: 1px solid var(--coar-border-neutral-tertiary);
}

.data-table td {
  padding: 10px 12px;
  font-size: 0.9rem;
  color: var(--coar-text-neutral-primary);
  border-bottom: 1px solid var(--coar-border-neutral-tertiary);
}

.data-table tbody tr:last-child td { border-bottom: none; }

.cell-name a {
  color: var(--coar-text-accent-primary);
  text-decoration: none;
  font-weight: 500;
}
.cell-name a:hover { text-decoration: underline; }

.cell-actions { text-align: right; }

.badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 0.78rem;
  font-weight: 500;
  background: var(--coar-background-neutral-secondary);
  color: var(--coar-text-neutral-secondary);
}

.badge--accent {
  background: var(--coar-background-accent-tertiary);
  color: var(--coar-text-accent-primary);
  font-weight: 600;
}

.mb-4 { margin-bottom: 16px; }
.loading-text { color: var(--coar-text-neutral-secondary); padding: 32px 0; text-align: center; }
</style>

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
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { CoarCard, CoarButton, CoarTextInput, CoarNote, CoarTable, CoarTag, CoarSpinner } from '@cocoar/vue-ui';
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

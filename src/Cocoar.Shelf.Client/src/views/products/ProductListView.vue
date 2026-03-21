<template>
  <div>
    <div class="list-header">
      <CoarButton variant="primary" @click="router.push('/admin/products/create')">
        New Product
      </CoarButton>
    </div>

    <div v-if="isLoading" class="center-content">
      <CoarSpinner size="m" label="Loading products..." />
    </div>

    <CoarNote v-if="error" variant="error">{{ error }}</CoarNote>

    <CoarTable v-if="!isLoading && products.length > 0" variant="bordered" hover>
      <thead>
        <tr>
          <th>Name</th>
          <th>Display Name</th>
          <th>Visibility</th>
          <th>Latest</th>
          <th>Versions</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="p in products" :key="p.name" @click="router.push(`/admin/products/${p.name}`)" class="clickable-row">
          <td class="cell-name">{{ p.name }}</td>
          <td>{{ p.displayName || '—' }}</td>
          <td>
            <CoarTag :variant="p.visibility === 'preview' ? 'warning' : 'success'" size="s">
              {{ p.visibility }}
            </CoarTag>
          </td>
          <td>
            <CoarTag v-if="p.latest" variant="accent" size="s">{{ p.latest }}</CoarTag>
            <span v-else class="text-muted">—</span>
          </td>
          <td class="text-muted">{{ p.versions.length }}</td>
        </tr>
      </tbody>
    </CoarTable>

    <div v-if="!isLoading && products.length === 0 && !error" class="empty-state">
      No products registered yet.
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { CoarButton, CoarNote, CoarTable, CoarTag, CoarSpinner } from '@cocoar/vue-ui';
import { useUI } from '@/composables/useUI';
import { shelfApi } from '@/core/api/shelf-api';
import type { Product } from '@/core/models/shelf.models';

const router = useRouter();
const ui = useUI();
const products = ref<Product[]>([]);
const isLoading = ref(true);
const error = ref('');

ui.set(ctx => {
  ctx.header.title = 'Products';
  ctx.header.subTitle = 'Manage registered documentation products';
});

onMounted(async () => {
  try {
    products.value = await shelfApi.getProducts();
  } catch {
    error.value = 'Failed to load products';
  } finally {
    isLoading.value = false;
  }
});
</script>

<style scoped>
.list-header {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 16px;
}

.clickable-row {
  cursor: pointer;
}

.cell-name {
  font-weight: 600;
  color: var(--coar-text-accent-primary);
}

.text-muted { color: var(--coar-text-neutral-secondary); }
.center-content { display: flex; justify-content: center; padding: 48px 0; }
.empty-state { color: var(--coar-text-neutral-secondary); padding: 48px 0; text-align: center; }
</style>

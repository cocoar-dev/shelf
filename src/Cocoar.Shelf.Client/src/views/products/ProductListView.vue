<template>
  <div>
    <div class="list-header">
      <CoarButton variant="primary" @click="router.push('/admin/products/create')">
        New Product
      </CoarButton>
    </div>

    <div v-if="isLoading" class="loading-text">Loading...</div>

    <CoarNote v-if="error" variant="error">{{ error }}</CoarNote>

    <table v-if="!isLoading && products.length > 0" class="data-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>Display Name</th>
          <th>Source</th>
          <th>Latest</th>
          <th>Versions</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="p in products" :key="p.name" @click="router.push(`/admin/products/${p.name}`)" class="clickable-row">
          <td class="cell-name">{{ p.name }}</td>
          <td>{{ p.displayName || '—' }}</td>
          <td><span class="badge">{{ p.source }}</span></td>
          <td>
            <span v-if="p.latest" class="badge badge--accent">{{ p.latest }}</span>
            <span v-else class="text-muted">—</span>
          </td>
          <td class="text-muted">{{ p.versions.length }}</td>
        </tr>
      </tbody>
    </table>

    <div v-if="!isLoading && products.length === 0 && !error" class="empty-state">
      No products registered yet.
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { CoarButton, CoarNote } from '@cocoar/vue-ui';
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

.data-table {
  width: 100%;
  border-collapse: collapse;
  background: var(--coar-background-neutral-primary);
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 8px;
  overflow: hidden;
}

.data-table th {
  text-align: left;
  padding: 10px 16px;
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--coar-text-neutral-secondary);
  background: var(--coar-background-neutral-secondary);
  border-bottom: 1px solid var(--coar-border-neutral-tertiary);
}

.data-table td {
  padding: 12px 16px;
  font-size: 0.9rem;
  color: var(--coar-text-neutral-primary);
  border-bottom: 1px solid var(--coar-border-neutral-tertiary);
}

.data-table tbody tr:last-child td {
  border-bottom: none;
}

.clickable-row {
  cursor: pointer;
  transition: background 0.1s;
}

.clickable-row:hover {
  background: var(--coar-background-neutral-secondary);
}

.cell-name {
  font-weight: 600;
  color: var(--coar-text-accent-primary);
}

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

.text-muted { color: var(--coar-text-neutral-secondary); }
.loading-text { color: var(--coar-text-neutral-secondary); padding: 32px 0; text-align: center; }
.empty-state { color: var(--coar-text-neutral-secondary); padding: 48px 0; text-align: center; }
</style>

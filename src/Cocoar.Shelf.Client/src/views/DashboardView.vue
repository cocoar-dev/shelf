<template>
  <div class="flex-1 min-w-0 py-6">
    <div class="stats-grid">
      <div class="stat-card" @click="router.push('/admin/products')">
        <div class="stat-value">{{ products.length }}</div>
        <div class="stat-label">Products</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">{{ totalVersions }}</div>
        <div class="stat-label">Versions</div>
      </div>
    </div>

    <div v-if="products.length > 0" class="recent-section">
      <h2 class="section-title">Products</h2>
      <div class="product-list">
        <RouterLink
          v-for="product in products"
          :key="product.name"
          :to="`/admin/products/${product.name}`"
          class="product-item"
        >
          <div class="product-name">{{ product.displayName || product.name }}</div>
          <div class="product-meta">
            <span v-if="product.latest" class="product-latest">{{ product.latest }}</span>
            <span class="product-count">{{ product.versions.length }} version{{ product.versions.length !== 1 ? 's' : '' }}</span>
          </div>
        </RouterLink>
      </div>
    </div>

    <CoarNote v-if="error" variant="error">{{ error }}</CoarNote>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { CoarNote } from '@cocoar/vue-ui';
import { useUI } from '@/composables/useUI';
import { shelfApi } from '@/core/api/shelf-api';
import type { Product } from '@/core/models/shelf.models';

const router = useRouter();
const ui = useUI();
const products = ref<Product[]>([]);
const error = ref('');

const totalVersions = computed(() =>
  products.value.reduce((sum, p) => sum + p.versions.length, 0)
);

ui.set(ctx => {
  ctx.header.title = 'Dashboard';
  ctx.header.subTitle = 'Overview of your documentation hosting';
  ctx.header.icon = 'layout-dashboard';
});

onMounted(async () => {
  try {
    products.value = await shelfApi.getProducts();
  } catch {
    error.value = 'Failed to load products';
  }
});
</script>

<style scoped>
.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 16px;
  margin-bottom: 32px;
}

.stat-card {
  background: var(--coar-background-neutral-primary);
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 8px;
  padding: 24px;
  cursor: pointer;
  transition: border-color 0.15s;
}

.stat-card:hover {
  border-color: var(--coar-text-accent-primary);
}

.stat-value {
  font-size: 2rem;
  font-weight: 700;
  color: var(--coar-text-neutral-primary);
}

.stat-label {
  font-size: 0.85rem;
  color: var(--coar-text-neutral-secondary);
  margin-top: 4px;
}

.section-title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--coar-text-neutral-primary);
  margin: 0 0 12px;
}

.product-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.product-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 16px;
  background: var(--coar-background-neutral-primary);
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 6px;
  text-decoration: none;
  transition: border-color 0.15s;
}

.product-item:hover {
  border-color: var(--coar-text-accent-primary);
}

.product-name {
  font-weight: 500;
  color: var(--coar-text-neutral-primary);
}

.product-meta {
  display: flex;
  gap: 12px;
  align-items: center;
}

.product-latest {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--coar-text-accent-primary);
  background: var(--coar-background-accent-tertiary);
  padding: 2px 8px;
  border-radius: 10px;
}

.product-count {
  font-size: 0.8rem;
  color: var(--coar-text-neutral-secondary);
}
</style>

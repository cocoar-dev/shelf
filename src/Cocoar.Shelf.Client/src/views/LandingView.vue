<template>
  <div class="landing">
    <header class="landing-header">
      <h1>Documentation</h1>
    </header>

    <div class="landing-grid">
      <a
        v-for="product in products"
        :key="product.name"
        :href="`/${product.name}/`"
        class="product-card"
        target="_blank"
      >
        <div class="card-name">{{ product.displayName || product.name }}</div>
        <div v-if="product.description" class="card-desc">{{ product.description }}</div>
        <div v-if="product.versions.length > 0" class="card-versions">
          <a
            v-for="v in product.versions"
            :key="v"
            :href="`/${product.name}/${v}/`"
            target="_blank"
            class="version-badge"
            :class="{ 'version-badge--latest': v === product.latest }"
            @click.stop
          >
            {{ v }}
          </a>
        </div>
      </a>
    </div>

    <div v-if="!isLoading && products.length === 0" class="landing-empty">
      No documentation available yet.
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import type { Product } from '@/core/models/shelf.models';

const products = ref<Product[]>([]);
const isLoading = ref(true);

onMounted(async () => {
  try {
    const response = await fetch('/_api/products');
    if (response.ok) {
      const all: Product[] = await response.json();
      products.value = all.filter(p => p.versions.length > 0);
    }
  } finally {
    isLoading.value = false;
  }
});
</script>

<style scoped>
.landing {
  min-height: 100vh;
  background: #f8fafc;
}

.landing-header {
  background: #1183CD;
  color: white;
  padding: 20px 24px;
  text-align: center;
}

.landing-header h1 {
  font-size: 1.5em;
  font-weight: 600;
  margin: 0;
}

.landing-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 20px;
  max-width: 1200px;
  margin: 40px auto;
  padding: 0 24px;
}

.product-card {
  background: white;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 24px;
  text-decoration: none;
  color: inherit;
  display: flex;
  flex-direction: column;
  transition: border-color 0.15s, box-shadow 0.15s;
}

.product-card:hover {
  border-color: #1183CD;
  box-shadow: 0 2px 8px rgba(17, 131, 205, 0.12);
}

.card-name {
  font-size: 1.15em;
  font-weight: 600;
  color: #1183CD;
  margin-bottom: 6px;
}

.product-card:hover .card-name { color: #0E6DB0; }

.card-desc {
  font-size: 0.9em;
  color: #64748b;
  line-height: 1.5;
}

.card-versions {
  margin-top: 16px;
  padding-top: 14px;
  border-top: 1px solid #f1f1f2;
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.version-badge {
  display: inline-block;
  padding: 2px 10px;
  border-radius: 10px;
  font-size: 0.78em;
  font-weight: 500;
  text-decoration: none;
  background: #f6f6f7;
  color: #64748b;
  border: 1px solid #e2e8f0;
  transition: border-color 0.15s, color 0.15s;
}

.version-badge:hover {
  border-color: #1183CD;
  color: #1183CD;
}

.version-badge--latest {
  background: #dbeafe;
  color: #1183CD;
  border-color: #bfdbfe;
  font-weight: 600;
}

.version-badge--latest:hover { background: #bfdbfe; }

.landing-empty {
  text-align: center;
  color: #94a3b8;
  padding: 80px 24px;
  font-size: 1.1em;
}
</style>

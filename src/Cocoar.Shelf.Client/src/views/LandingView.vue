<template>
  <div class="landing">
    <header class="landing-header">
      <h1>Documentation</h1>
    </header>

    <div class="landing-toolbar" v-if="previewCount > 0">
      <label class="filter-toggle">
        <input type="checkbox" v-model="showPreview" />
        <span>Show preview ({{ previewCount }})</span>
      </label>
    </div>

    <div class="landing-grid">
      <a
        v-for="product in visibleProducts"
        :key="product.name"
        :href="`/${product.name}/`"
        class="product-card"
        target="_blank"
      >
        <div class="card-title-row">
          <div class="card-name">{{ product.displayName || product.name }}</div>
          <span v-if="isPreviewProduct(product)" class="preview-badge">preview</span>
        </div>
        <div v-if="product.description" class="card-desc">{{ product.description }}</div>
        <div v-if="visibleVersions(product).length > 0" class="card-versions">
          <a
            v-for="v in visibleVersions(product)"
            :key="v"
            :href="`/${product.name}/${v}/`"
            target="_blank"
            class="version-badge"
            :class="{
              'version-badge--latest': v === product.latest,
              'version-badge--prerelease': isPreRelease(v),
            }"
            @click.stop
          >
            {{ v }}
          </a>
        </div>
      </a>
    </div>

    <div v-if="!isLoading && visibleProducts.length === 0" class="landing-empty">
      No documentation available yet.
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import type { Product } from '@/core/models/shelf.models';

const allProducts = ref<Product[]>([]);
const isLoading = ref(true);
const showPreview = ref(false);

function isPreRelease(version: string): boolean {
  return version.includes('-');
}

function hasStableVersion(product: Product): boolean {
  return product.versions.some(v => !isPreRelease(v));
}

function isPreviewProduct(product: Product): boolean {
  return product.visibility === 'preview' || !hasStableVersion(product);
}

function visibleVersions(product: Product): string[] {
  if (showPreview.value) return product.versions;
  return product.versions.filter(v => !isPreRelease(v));
}

const productsWithVersions = computed(() =>
  allProducts.value.filter(p => p.versions.length > 0)
);

const previewCount = computed(() =>
  productsWithVersions.value.filter(p => isPreviewProduct(p)).length
);

const visibleProducts = computed(() => {
  if (showPreview.value) return productsWithVersions.value;

  return productsWithVersions.value.filter(p => {
    // Show if product is public AND has at least one stable version
    if (p.visibility === 'preview') return false;
    return hasStableVersion(p);
  });
});

onMounted(async () => {
  try {
    const response = await fetch('/_api/products');
    if (response.ok) {
      allProducts.value = await response.json();
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

.landing-toolbar {
  max-width: 1200px;
  margin: 24px auto 0;
  padding: 0 24px;
  display: flex;
  justify-content: flex-end;
}

.filter-toggle {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.85rem;
  color: #64748b;
  cursor: pointer;
  user-select: none;
}

.filter-toggle input {
  cursor: pointer;
}

.landing-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 20px;
  max-width: 1200px;
  margin: 20px auto 40px;
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

.card-title-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.card-name {
  font-size: 1.15em;
  font-weight: 600;
  color: #1183CD;
}

.product-card:hover .card-name { color: #0E6DB0; }

.preview-badge {
  font-size: 0.7em;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 10px;
  background: #fef3c7;
  color: #92400e;
  border: 1px solid #fde68a;
}

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

.version-badge--prerelease {
  border-style: dashed;
}

.landing-empty {
  text-align: center;
  color: #94a3b8;
  padding: 80px 24px;
  font-size: 1.1em;
}
</style>

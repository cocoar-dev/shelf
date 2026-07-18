<template>
  <div class="landing">
    <header class="landing-header">
      <div class="header-side"></div>
      <h1>Documentation</h1>
      <div class="header-side header-auth">
        <template v-if="auth.isAuthenticated">
          <RouterLink v-if="auth.user?.isAdmin" to="/admin" class="header-link">Admin</RouterLink>
          <span class="header-user">{{ auth.userName }}</span>
          <button class="header-link header-link--button" @click="auth.logout()">Sign out</button>
        </template>
        <button v-else class="header-link header-link--button" @click="auth.login()">Sign in</button>
      </div>
    </header>

    <div class="landing-toolbar" v-if="hasAnyPreviewContent || allTags.length > 0">
      <div class="toolbar-tags" v-if="allTags.length > 0">
        <span class="toolbar-label">Filter:</span>
        <button
          v-for="tag in allTags"
          :key="tag"
          class="tag-filter-chip"
          :class="{ active: selectedTags.includes(tag) }"
          @click="toggleTag(tag)"
        >{{ tag }}</button>
        <button v-if="selectedTags.length > 0" class="tag-clear" @click="clearTags">Clear</button>
      </div>

      <label class="filter-toggle" v-if="hasAnyPreviewContent">
        <input type="checkbox" v-model="showPreview" />
        <span>Show preview</span>
      </label>
    </div>

    <div class="landing-grid">
      <template v-for="product in visibleProducts" :key="product.name">
        <!-- Card with versions: clickable link -->
        <a
          v-if="product.versions.length > 0"
          :href="`/${product.name}/`"
          class="product-card"
          target="_blank"
        >
          <div class="card-title-row">
            <div class="card-name">{{ product.displayName || product.name }}</div>
            <span v-if="isPreviewProduct(product)" class="preview-badge">preview</span>
          </div>
          <div v-if="product.description" class="card-desc">{{ product.description }}</div>
          <div v-if="product.tags && product.tags.length > 0" class="card-tags">
            <span v-for="tag in product.tags" :key="tag" class="card-tag">{{ tag }}</span>
          </div>
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

        <!-- Card without versions: teaser (not a link) -->
        <div v-else class="product-card product-card--empty">
          <div class="card-title-row">
            <div class="card-name">{{ product.displayName || product.name }}</div>
            <span v-if="product.visibility === 'preview'" class="preview-badge">preview</span>
          </div>
          <div v-if="product.description" class="card-desc">{{ product.description }}</div>
          <div v-if="product.tags && product.tags.length > 0" class="card-tags">
            <span v-for="tag in product.tags" :key="tag" class="card-tag">{{ tag }}</span>
          </div>
          <div class="card-coming-soon">Coming soon</div>
        </div>
      </template>
    </div>

    <div v-if="!isLoading && visibleProducts.length === 0" class="landing-empty">
      No documentation available yet.
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { storeToRefs } from 'pinia';
import { RouterLink } from 'vue-router';
import type { Product } from '@/core/models/shelf.models';
import { usePreferencesStore } from '@/stores/preferences.store';
import { useAuthStore } from '@/stores/auth.store';

const auth = useAuthStore();
const prefs = usePreferencesStore();
const { showPreview, selectedTags } = storeToRefs(prefs);
const { toggleTag, clearTags } = prefs;

const allProducts = ref<Product[]>([]);
const isLoading = ref(true);

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

// Products eligible to appear on the landing page (have versions, or are opted-in as teaser)
const eligibleProducts = computed(() =>
  allProducts.value.filter(p => p.versions.length > 0 || p.showWhenEmpty)
);

const hasAnyPreviewContent = computed(() =>
  eligibleProducts.value.some(p =>
    isPreviewProduct(p) || p.versions.some(v => isPreRelease(v))
  )
);

// All unique tags across all registered products (sorted)
const allTags = computed(() => {
  const set = new Set<string>();
  allProducts.value.forEach(p => (p.tags ?? []).forEach(t => set.add(t)));
  return [...set].sort();
});

const visibleProducts = computed(() => {
  let products = eligibleProducts.value;

  if (!showPreview.value) {
    products = products.filter(p => {
      if (p.visibility === 'preview') return false;
      // Products with no versions (teaser) pass through — they have no pre-release versions
      if (p.versions.length === 0) return true;
      return hasStableVersion(p);
    });
  }

  if (selectedTags.value.length > 0) {
    products = products.filter(p =>
      selectedTags.value.every(t => (p.tags ?? []).includes(t))
    );
  }

  return products;
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
  display: flex;
  align-items: center;
  gap: 16px;
}

.landing-header h1 {
  font-size: 1.5em;
  font-weight: 600;
  margin: 0;
  text-align: center;
}

.header-side {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 14px;
}

.header-auth {
  justify-content: flex-end;
}

.header-user {
  font-size: 0.88rem;
  opacity: 0.85;
  white-space: nowrap;
}

.header-link {
  color: white;
  font-size: 0.88rem;
  font-weight: 500;
  text-decoration: none;
  opacity: 0.9;
  white-space: nowrap;
}

.header-link:hover {
  opacity: 1;
  text-decoration: underline;
}

.header-link--button {
  background: none;
  border: 1px solid rgba(255, 255, 255, 0.55);
  border-radius: 6px;
  padding: 4px 12px;
  cursor: pointer;
}

.header-link--button:hover {
  background: rgba(255, 255, 255, 0.12);
  text-decoration: none;
}

.landing-toolbar {
  max-width: 1200px;
  margin: 24px auto 0;
  padding: 0 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
}

.toolbar-tags {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
  flex: 1;
}

.toolbar-label {
  font-size: 0.82rem;
  font-weight: 500;
  color: #94a3b8;
  margin-right: 2px;
  white-space: nowrap;
}

.tag-filter-chip {
  display: inline-block;
  padding: 3px 12px;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 500;
  cursor: pointer;
  border: 1px solid #e2e8f0;
  background: #f8fafc;
  color: #64748b;
  transition: background 0.12s, border-color 0.12s, color 0.12s;
}

.tag-filter-chip:hover {
  border-color: #1183CD;
  color: #1183CD;
}

.tag-filter-chip.active {
  background: #dbeafe;
  border-color: #1183CD;
  color: #1183CD;
  font-weight: 600;
}

.tag-clear {
  font-size: 0.78rem;
  color: #94a3b8;
  background: none;
  border: none;
  cursor: pointer;
  padding: 2px 6px;
  text-decoration: underline;
}

.tag-clear:hover { color: #64748b; }

.filter-toggle {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.85rem;
  color: #64748b;
  cursor: pointer;
  user-select: none;
  white-space: nowrap;
  flex-shrink: 0;
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

.card-tags {
  margin-top: 10px;
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
}

.card-tag {
  display: inline-block;
  padding: 1px 9px;
  border-radius: 10px;
  font-size: 0.75em;
  font-weight: 500;
  background: #f1f5f9;
  color: #64748b;
  border: 1px solid #e2e8f0;
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

.product-card--empty {
  opacity: 0.75;
  cursor: default;
  border-style: dashed;
}

.card-coming-soon {
  margin-top: 16px;
  padding-top: 14px;
  border-top: 1px dashed #e2e8f0;
  font-size: 0.78em;
  font-weight: 500;
  color: #94a3b8;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.landing-empty {
  text-align: center;
  color: #94a3b8;
  padding: 80px 24px;
  font-size: 1.1em;
}
</style>

<template>
  <div class="landing">
    <div class="landing-toolbar" v-if="hasAnyPreviewContent || hasAnyOpenness || allTags.length > 0">
      <div class="toolbar-tags" v-if="allTags.length > 0 || hasAnyOpenness">
        <span class="toolbar-label">Filter:</span>
        <button
          v-for="tag in allTags"
          :key="tag"
          class="tag-filter-chip"
          :class="{ active: selectedTags.includes(tag) }"
          @click="toggleTag(tag)"
        >{{ tag }}</button>
        <button v-if="selectedTags.length > 0" class="tag-clear" @click="clearTags">Clear</button>

        <template v-if="hasAnyOpenness">
          <span class="toolbar-divider" v-if="allTags.length > 0" aria-hidden="true"></span>
          <button
            class="tag-filter-chip openness-filter openness-filter--oss"
            :class="{ active: opennessFilter === 'OpenSource' }"
            @click="toggleOpenness('OpenSource')"
          >Open Source</button>
          <button
            class="tag-filter-chip openness-filter openness-filter--prop"
            :class="{ active: opennessFilter === 'Proprietary' }"
            @click="toggleOpenness('Proprietary')"
          >Proprietary</button>
        </template>
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
            <span
              v-if="product.openness && product.openness !== 'Unspecified'"
              class="openness-badge"
              :class="product.openness === 'OpenSource' ? 'openness-badge--oss' : 'openness-badge--prop'"
            >{{ opennessLabel(product.openness) }}</span>
          </div>
          <div v-if="product.description" class="card-desc">{{ product.description }}</div>
          <div v-if="product.tags && product.tags.length > 0" class="card-tags">
            <span v-for="tag in product.tags" :key="tag" class="card-tag">{{ tag }}</span>
          </div>
          <a
            v-if="product.repositoryUrl"
            :href="product.repositoryUrl"
            target="_blank"
            rel="noopener"
            class="card-source"
            @click.stop
          >Source ↗</a>
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
            <span
              v-if="product.openness && product.openness !== 'Unspecified'"
              class="openness-badge"
              :class="product.openness === 'OpenSource' ? 'openness-badge--oss' : 'openness-badge--prop'"
            >{{ opennessLabel(product.openness) }}</span>
          </div>
          <div v-if="product.description" class="card-desc">{{ product.description }}</div>
          <div v-if="product.tags && product.tags.length > 0" class="card-tags">
            <span v-for="tag in product.tags" :key="tag" class="card-tag">{{ tag }}</span>
          </div>
          <a
            v-if="product.repositoryUrl"
            :href="product.repositoryUrl"
            target="_blank"
            rel="noopener"
            class="card-source"
          >Source ↗</a>
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
import type { Product } from '@/core/models/shelf.models';
import { usePreferencesStore, opennessLabel } from '@/stores/preferences.store';
import { useUI } from '@/composables/useUI';

const prefs = usePreferencesStore();
const { showPreview, selectedTags, opennessFilter } = storeToRefs(prefs);
const { toggleTag, clearTags, toggleOpenness } = prefs;

const ui = useUI();
ui.set((ctx) => {
  ctx.header.title = 'Documentation';
  ctx.header.subTitle = 'Cocoar product docs';
  ctx.header.icon = 'book-open';
  ctx.content.container = false;
});

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

// Products eligible to appear on the landing page (have versions, or are opted-in as teaser)
const eligibleProducts = computed(() =>
  allProducts.value.filter(p => p.versions.length > 0 || p.showWhenEmpty)
);

const hasAnyPreviewContent = computed(() =>
  eligibleProducts.value.some(p =>
    isPreviewProduct(p) || p.versions.some(v => isPreRelease(v))
  )
);

// Only surface the open-source/proprietary filter once at least one product is marked.
const hasAnyOpenness = computed(() =>
  eligibleProducts.value.some(p => p.openness && p.openness !== 'Unspecified')
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

  if (opennessFilter.value) {
    products = products.filter(p => p.openness === opennessFilter.value);
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
  flex: 1;
  min-width: 0;
  overflow-y: auto;
  background: var(--coar-background-neutral-secondary);
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
  color: var(--coar-text-neutral-tertiary);
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
  border: 1px solid var(--coar-border-neutral-tertiary);
  background: var(--coar-background-neutral-primary);
  color: var(--coar-text-neutral-secondary);
  transition: background 0.12s, border-color 0.12s, color 0.12s;
}

.tag-filter-chip:hover {
  border-color: var(--coar-text-accent-primary);
  color: var(--coar-text-accent-primary);
}

.tag-filter-chip.active {
  background: var(--coar-background-accent-tertiary);
  border-color: var(--coar-text-accent-primary);
  color: var(--coar-text-accent-primary);
  font-weight: 600;
}

.tag-clear {
  font-size: 0.78rem;
  color: var(--coar-text-neutral-tertiary);
  background: none;
  border: none;
  cursor: pointer;
  padding: 2px 6px;
  text-decoration: underline;
}

.tag-clear:hover { color: var(--coar-text-neutral-secondary); }

.filter-toggle {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.85rem;
  color: var(--coar-text-neutral-secondary);
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
  background: var(--coar-background-neutral-primary);
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 8px;
  padding: 24px;
  text-decoration: none;
  color: inherit;
  display: flex;
  flex-direction: column;
  transition: border-color 0.15s, box-shadow 0.15s;
}

.product-card:hover {
  border-color: var(--coar-text-accent-primary);
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
  color: var(--coar-text-accent-primary);
}

.preview-badge {
  font-size: 0.7em;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 10px;
  background: var(--coar-background-semantic-warning-subtle, #fef3c7);
  color: var(--coar-text-semantic-warning, #92400e);
  border: 1px solid var(--coar-border-semantic-warning-subtle, #fde68a);
}

.openness-badge {
  font-size: 0.7em;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 10px;
  white-space: nowrap;
}

.openness-badge--oss {
  background: var(--coar-background-semantic-success-subtle, #dcfce7);
  color: var(--coar-text-semantic-success, #166534);
  border: 1px solid var(--coar-border-semantic-success-subtle, #bbf7d0);
}

/* Proprietary = deliberately muted (no open source). */
.openness-badge--prop {
  background: var(--coar-background-neutral-secondary);
  color: var(--coar-text-neutral-secondary);
  border: 1px solid var(--coar-border-neutral-tertiary);
}

.card-source {
  margin-top: 12px;
  align-self: flex-start;
  font-size: 0.8em;
  font-weight: 500;
  color: var(--coar-text-neutral-tertiary);
  text-decoration: none;
}

.card-source:hover {
  color: var(--coar-text-accent-primary);
  text-decoration: underline;
}

.toolbar-divider {
  width: 1px;
  align-self: stretch;
  margin: 2px 4px;
  background: var(--coar-border-neutral-tertiary);
}

.openness-filter::before {
  content: "";
  display: inline-block;
  width: 7px;
  height: 7px;
  border-radius: 50%;
  margin-right: 6px;
  vertical-align: middle;
}

.openness-filter--oss::before {
  background: var(--coar-text-semantic-success, #16a34a);
}

.openness-filter--prop::before {
  background: var(--coar-text-neutral-tertiary);
}

.card-desc {
  font-size: 0.9em;
  color: var(--coar-text-neutral-secondary);
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
  background: var(--coar-background-neutral-secondary);
  color: var(--coar-text-neutral-secondary);
  border: 1px solid var(--coar-border-neutral-tertiary);
}

.card-versions {
  margin-top: 16px;
  padding-top: 14px;
  border-top: 1px solid var(--coar-border-neutral-tertiary);
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
  background: var(--coar-background-neutral-secondary);
  color: var(--coar-text-neutral-secondary);
  border: 1px solid var(--coar-border-neutral-tertiary);
  transition: border-color 0.15s, color 0.15s;
}

.version-badge:hover {
  border-color: var(--coar-text-accent-primary);
  color: var(--coar-text-accent-primary);
}

.version-badge--latest {
  background: var(--coar-background-accent-tertiary);
  color: var(--coar-text-accent-primary);
  border-color: var(--coar-background-accent-tertiary);
  font-weight: 600;
}

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
  border-top: 1px dashed var(--coar-border-neutral-tertiary);
  font-size: 0.78em;
  font-weight: 500;
  color: var(--coar-text-neutral-tertiary);
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.landing-empty {
  text-align: center;
  color: var(--coar-text-neutral-tertiary);
  padding: 80px 24px;
  font-size: 1.1em;
}
</style>

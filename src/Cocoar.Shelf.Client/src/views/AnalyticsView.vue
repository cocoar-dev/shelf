<template>
  <div class="analytics-root">
    <!-- Filter bar -->
    <div class="filter-bar">
      <div class="range-group">
        <button
          v-for="r in RANGES"
          :key="r.key"
          class="range-btn"
          :class="{ active: range === r.key }"
          @click="range = r.key; load()"
        >
          {{ r.label }}
        </button>
      </div>
      <div class="filter-spacer" />
      <CoarSelect
        v-model="product"
        :options="productOptions"
        class="product-filter"
        @update:model-value="load"
      />
      <input
        v-model="excludeIps"
        class="ip-filter"
        placeholder="Exclude IPs (comma-separated)"
        @keydown.enter="load"
        @change="load"
      />
    </div>

    <!-- KPI tiles -->
    <div class="stats-grid">
      <div class="stat-card">
        <div class="stat-value">{{ summary?.totalVisits ?? '—' }}</div>
        <div class="stat-label">Total Visits</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">{{ summary?.uniqueIps ?? '—' }}</div>
        <div class="stat-label">Unique Visitors</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">{{ summary?.countriesCount ?? '—' }}</div>
        <div class="stat-label">Countries</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">{{ summary?.citiesCount ?? '—' }}</div>
        <div class="stat-label">Cities</div>
      </div>
      <div class="stat-card">
        <div class="stat-value stat-value--sm">{{ topProductName }}</div>
        <div class="stat-label">Top Product</div>
      </div>
    </div>

    <template v-if="summary && summary.totalVisits > 0">
      <!-- World map -->
      <div class="section">
        <h2 class="section-title">Where visitors come from</h2>
        <div class="map-row">
          <div class="map-wrap">
            <CoarMap v-if="mapData.points.length > 0" :data="mapData" :config="mapConfig" show-legend />
            <div v-else class="map-empty">No geolocated visits yet (GeoIP resolves only public IPs).</div>
          </div>
          <div class="map-cities">
            <h3 class="mini-title">Top Cities</h3>
            <TopList :items="cityItems" />
          </div>
        </div>
      </div>

      <!-- Visits by day -->
      <div v-if="days.length > 0" class="section">
        <h2 class="section-title">Visits by Day</h2>
        <div class="chart-card" @mouseleave="tooltip = null">
          <svg :viewBox="`0 0 ${CHART_W} ${CHART_H}`" class="chart-svg" role="img" aria-label="Visits per day">
            <line
              v-for="g in gridLines"
              :key="g.y"
              :x1="PAD_L" :x2="CHART_W - PAD_R" :y1="g.y" :y2="g.y"
              class="grid-line"
            />
            <text v-for="g in gridLines" :key="'t' + g.y" :x="PAD_L - 6" :y="g.y + 3" class="axis-label" text-anchor="end">
              {{ g.value }}
            </text>
            <path
              v-for="(d, i) in days"
              :key="d.date"
              :d="barPath(i, d.count)"
              class="bar"
              :class="{ dimmed: tooltip !== null && tooltip.index !== i }"
              @mousemove="showTooltip($event, i)"
            />
            <text
              v-for="t in dateTicks"
              :key="t.index"
              :x="barX(t.index) + barWidth / 2"
              :y="CHART_H - 6"
              class="axis-label"
              text-anchor="middle"
            >
              {{ t.label }}
            </text>
          </svg>
          <div
            v-if="tooltip"
            class="chart-tooltip"
            :style="{ left: tooltip.x + 'px', top: tooltip.y + 'px' }"
          >
            <div class="tooltip-date">{{ days[tooltip.index].date }}</div>
            <div class="tooltip-value">{{ days[tooltip.index].count }} visits · {{ days[tooltip.index].uniqueIps }} unique</div>
          </div>
        </div>
      </div>

      <!-- Top lists -->
      <div class="top-grid">
        <div class="top-card">
          <h2 class="section-title">Top Countries</h2>
          <TopList :items="countryItems" />
        </div>
        <div class="top-card">
          <h2 class="section-title">Top Products</h2>
          <TopList :items="itemsOf(summary.topProducts, p => p.product || '—', p => p.count)" />
        </div>
        <div class="top-card">
          <h2 class="section-title">Top Pages</h2>
          <TopList :items="itemsOf(summary.topPages, p => p.page, p => p.count)" />
        </div>
        <div class="top-card">
          <h2 class="section-title">Browsers</h2>
          <TopList :items="itemsOf(summary.byBrowser, b => b.name, b => b.count)" />
        </div>
        <div class="top-card">
          <h2 class="section-title">Operating Systems</h2>
          <TopList :items="itemsOf(summary.byOs, b => b.name, b => b.count)" />
        </div>
        <div class="top-card">
          <h2 class="section-title">Languages</h2>
          <TopList :items="itemsOf(summary.byLanguage, l => l.language ?? '—', l => l.count)" />
        </div>
        <div v-if="summary.topReferrers.length > 0" class="top-card">
          <h2 class="section-title">Referrers</h2>
          <TopList :items="itemsOf(summary.topReferrers, r => r.referrer ?? '—', r => r.count)" />
        </div>
      </div>
    </template>

    <CoarNote v-if="summary && summary.totalVisits === 0" variant="info" class="mt-4">
      No visits recorded in this period. Access logging captures documentation page views.
    </CoarNote>
    <CoarNote v-if="error" variant="error" class="mt-4">{{ error }}</CoarNote>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { CoarNote, CoarSelect } from '@cocoar/vue-ui';
import { CoarMap, type MapData, type MapConfig } from '@cocoar/vue-map';
import { useUI } from '@/composables/useUI';
import { http } from '@/core/api/http';
import { shelfApi } from '@/core/api/shelf-api';
import { countryName } from '@/core/geo';
import TopList from '@/components/TopList.vue';

interface AnalyticsSummary {
  totalVisits: number;
  uniqueIps: number;
  countriesCount: number;
  citiesCount: number;
  visitsByDay: { date: string; count: number; uniqueIps: number }[];
  topPages: { page: string; count: number }[];
  topProducts: { product: string | null; count: number }[];
  topCountries: { country: string | null; count: number }[];
  topCities: { city: string | null; country: string | null; count: number }[];
  locations: { city: string | null; country: string | null; lat: number; lng: number; count: number }[];
  byBrowser: { name: string; count: number }[];
  byOs: { name: string; count: number }[];
  byLanguage: { language: string | null; count: number }[];
  topReferrers: { referrer: string | null; count: number }[];
}

const ui = useUI();
ui.set(ctx => {
  ctx.header.title = 'Analytics';
  ctx.header.subTitle = 'Documentation traffic';
  ctx.header.icon = 'bar-chart-3';
});

const RANGES = [
  { key: '7d', label: '7 days', days: 7 },
  { key: '30d', label: '30 days', days: 30 },
  { key: '90d', label: '90 days', days: 90 },
  { key: 'all', label: 'All time', days: null },
] as const;

type RangeKey = (typeof RANGES)[number]['key'];

const range = ref<RangeKey>('30d');
const product = ref('');
const excludeIps = ref('');
const summary = ref<AnalyticsSummary | null>(null);
const productOptions = ref<{ value: string; label: string }[]>([{ value: '', label: 'All products' }]);
const error = ref('');
const tooltip = ref<{ index: number; x: number; y: number } | null>(null);

const days = computed(() => summary.value?.visitsByDay ?? []);

const topProductName = computed(() => summary.value?.topProducts?.[0]?.product ?? '—');

function itemsOf<T>(list: T[] | undefined, label: (t: T) => string, count: (t: T) => number) {
  return (list ?? []).map(t => ({ label: label(t), count: count(t) }));
}

const countryItems = computed(() =>
  (summary.value?.topCountries ?? []).map(c => ({
    flag: c.country ?? undefined,
    label: countryName(c.country),
    count: c.count,
  })));

const cityItems = computed(() =>
  (summary.value?.topCities ?? []).map(c => ({
    flag: c.country ?? undefined,
    label: c.city ?? '—',
    count: c.count,
  })));

// --- Map ---
const mapConfig: MapConfig = {
  defaultBasemap: 'osm',
  basemaps: [{
    id: 'osm',
    url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
    attribution: '© OpenStreetMap contributors',
    maxZoom: 18,
  }],
  categories: [
    { id: 'low', label: 'Low', emoji: '🔵', color: '#93c5fd' },
    { id: 'mid', label: 'Medium', emoji: '🟣', color: '#3b82f6' },
    { id: 'high', label: 'High', emoji: '🔴', color: '#1e3a8a' },
  ],
};

const mapData = computed<MapData>(() => {
  const locs = summary.value?.locations ?? [];
  const max = Math.max(1, ...locs.map(l => l.count));
  return {
    type: 'multi',
    points: locs.map(l => ({
      lat: l.lat,
      lng: l.lng,
      kind: 'stop' as const,
      label: `${l.city ?? '—'}, ${countryName(l.country)} — ${l.count} visit${l.count === 1 ? '' : 's'}`,
      category: l.count / max > 0.66 ? 'high' : l.count / max > 0.33 ? 'mid' : 'low',
    })),
  };
});

// --- Day chart geometry ---
const CHART_W = 900;
const CHART_H = 220;
const PAD_L = 40;
const PAD_R = 8;
const PAD_T = 10;
const PAD_B = 22;

const maxCount = computed(() => Math.max(1, ...days.value.map(d => d.count)));
const slotWidth = computed(() => (CHART_W - PAD_L - PAD_R) / (days.value.length || 1));
const barWidth = computed(() => Math.min(48, Math.max(2, slotWidth.value - 2)));

function barX(i: number): number {
  return PAD_L + i * slotWidth.value + (slotWidth.value - barWidth.value) / 2;
}

function barPath(i: number, count: number): string {
  const x = barX(i);
  const w = barWidth.value;
  const h = Math.max(1, ((CHART_H - PAD_T - PAD_B) * count) / maxCount.value);
  const y = CHART_H - PAD_B - h;
  const r = Math.min(4, w / 2, h);
  const base = CHART_H - PAD_B;
  return `M ${x} ${base} L ${x} ${y + r} Q ${x} ${y} ${x + r} ${y} L ${x + w - r} ${y} Q ${x + w} ${y} ${x + w} ${y + r} L ${x + w} ${base} Z`;
}

const gridLines = computed(() => {
  const max = maxCount.value;
  const steps = max <= 4 ? max : 4;
  return Array.from({ length: steps }, (_, i) => {
    const value = Math.round(((i + 1) * max) / steps);
    const y = CHART_H - PAD_B - ((CHART_H - PAD_T - PAD_B) * value) / max;
    return { y, value };
  });
});

const dateTicks = computed(() => {
  const n = days.value.length;
  if (n === 0) return [];
  const step = Math.max(1, Math.floor(n / Math.min(6, n)));
  const ticks: { index: number; label: string }[] = [];
  for (let i = 0; i < n; i += step) ticks.push({ index: i, label: days.value[i].date.slice(5) });
  return ticks;
});

function showTooltip(event: MouseEvent, index: number) {
  const card = (event.currentTarget as SVGElement).closest('.chart-card') as HTMLElement;
  const rect = card.getBoundingClientRect();
  tooltip.value = {
    index,
    x: Math.min(event.clientX - rect.left + 12, rect.width - 160),
    y: event.clientY - rect.top - 44,
  };
}

async function load() {
  error.value = '';
  try {
    const params = new URLSearchParams();
    const r = RANGES.find(x => x.key === range.value)!;
    if (r.days !== null) {
      const from = new Date();
      from.setDate(from.getDate() - r.days);
      params.set('from', from.toISOString());
    }
    if (product.value) params.set('product', product.value);
    if (excludeIps.value.trim()) params.set('excludeIps', excludeIps.value.trim());
    summary.value = await http.get<AnalyticsSummary>(`/analytics/summary?${params.toString()}`);
  } catch {
    error.value = 'Failed to load analytics';
  }
}

onMounted(async () => {
  try {
    const products = await shelfApi.getProducts();
    productOptions.value = [
      { value: '', label: 'All products' },
      ...products.map(p => ({ value: p.name, label: p.displayName || p.name })),
    ];
  } catch { /* product filter stays "All products" */ }
  await load();
});
</script>

<style scoped>
.analytics-root {
  flex: 1;
  min-width: 0;
  padding: 24px 0;
}

.filter-bar {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.range-group {
  display: flex;
  gap: 8px;
}

.filter-spacer { flex: 1; }

.range-btn {
  padding: 6px 14px;
  font-size: 0.85rem;
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 16px;
  background: var(--coar-background-neutral-primary);
  color: var(--coar-text-neutral-secondary);
  cursor: pointer;
  transition: border-color 0.15s, color 0.15s;
}

.range-btn:hover { border-color: var(--coar-text-accent-primary); }

.range-btn.active {
  border-color: var(--coar-text-accent-primary);
  color: var(--coar-text-accent-primary);
  font-weight: 600;
}

.product-filter { width: 14rem; }

.ip-filter {
  width: 16rem;
  padding: 6px 12px;
  font-size: 0.85rem;
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 6px;
  background: var(--coar-background-neutral-primary);
  color: var(--coar-text-neutral-primary);
}
.ip-filter:focus { outline: none; border-color: var(--coar-text-accent-primary); }

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 16px;
  margin-bottom: 28px;
}

.stat-card {
  background: var(--coar-background-neutral-primary);
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 8px;
  padding: 20px;
}

.stat-value {
  font-size: 2rem;
  font-weight: 700;
  color: var(--coar-text-neutral-primary);
}

.stat-value--sm {
  font-size: 1.25rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.stat-label {
  font-size: 0.85rem;
  color: var(--coar-text-neutral-secondary);
  margin-top: 4px;
}

.section { margin-bottom: 32px; }

.section-title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--coar-text-neutral-primary);
  margin: 0 0 12px;
}

.mini-title {
  font-size: 0.8rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--coar-text-neutral-secondary);
  margin: 0 0 10px;
}

.map-row {
  display: grid;
  grid-template-columns: 1fr 280px;
  gap: 16px;
}

.map-wrap {
  height: 440px;
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 8px;
  overflow: hidden;
}

.map-wrap :deep(.coar-map),
.map-wrap :deep(.leaflet-container) {
  height: 100%;
  width: 100%;
}

.map-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: var(--coar-text-neutral-tertiary);
  font-size: 0.9rem;
  padding: 24px;
  text-align: center;
}

.map-cities {
  background: var(--coar-background-neutral-primary);
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 8px;
  padding: 16px;
  overflow-y: auto;
  max-height: 440px;
}

.chart-card {
  position: relative;
  background: var(--coar-background-neutral-primary);
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 8px;
  padding: 16px;
}

.chart-svg { display: block; width: 100%; height: auto; }
.grid-line { stroke: var(--coar-border-neutral-tertiary); stroke-width: 1; }
.axis-label { font-size: 10px; fill: var(--coar-text-neutral-tertiary); }
.bar { fill: var(--coar-text-accent-primary); transition: opacity 0.1s; }
.bar.dimmed { opacity: 0.45; }

.chart-tooltip {
  position: absolute;
  pointer-events: none;
  background: var(--coar-background-neutral-primary);
  border: 1px solid var(--coar-border-neutral-secondary);
  border-radius: 6px;
  padding: 6px 10px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.12);
  z-index: 10;
  min-width: 150px;
}

.tooltip-date { font-size: 0.75rem; color: var(--coar-text-neutral-secondary); }
.tooltip-value { font-size: 0.85rem; font-weight: 600; color: var(--coar-text-neutral-primary); }

.top-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 16px;
}

.top-card {
  background: var(--coar-background-neutral-primary);
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 8px;
  padding: 16px;
  min-width: 0;
}

.mt-4 { margin-top: 16px; }

@media (max-width: 860px) {
  .map-row { grid-template-columns: 1fr; }
  .map-cities { max-height: none; }
}
</style>

<template>
  <div class="analytics-root">
    <!-- Filter row -->
    <div class="filter-row">
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

    <!-- KPI tiles -->
    <div class="stats-grid">
      <div class="stat-card">
        <div class="stat-value">{{ summary?.totalVisits ?? '—' }}</div>
        <div class="stat-label">Total Visits</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">{{ summary?.uniqueIps ?? '—' }}</div>
        <div class="stat-label">Unique Visitors (IPs)</div>
      </div>
    </div>

    <!-- Visits by day -->
    <div v-if="days.length > 0" class="chart-section">
      <h2 class="section-title">Visits by Day</h2>
      <div class="chart-card" @mouseleave="tooltip = null">
        <svg :viewBox="`0 0 ${CHART_W} ${CHART_H}`" class="chart-svg" role="img" aria-label="Visits per day">
          <!-- recessive gridlines -->
          <line
            v-for="g in gridLines"
            :key="g.y"
            :x1="PAD_L" :x2="CHART_W - PAD_R" :y1="g.y" :y2="g.y"
            class="grid-line"
          />
          <text v-for="g in gridLines" :key="'t' + g.y" :x="PAD_L - 6" :y="g.y + 3" class="axis-label" text-anchor="end">
            {{ g.value }}
          </text>

          <!-- bars: single series, one hue, rounded top anchored to baseline -->
          <path
            v-for="(d, i) in days"
            :key="d.date"
            :d="barPath(i, d.count)"
            class="bar"
            :class="{ dimmed: tooltip !== null && tooltip.index !== i }"
            @mousemove="showTooltip($event, i)"
          />

          <!-- sparse date ticks -->
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
          <div class="tooltip-value">{{ days[tooltip.index].count }} visits</div>
        </div>
      </div>
    </div>

    <!-- Top lists -->
    <div v-if="summary && summary.totalVisits > 0" class="top-grid">
      <div class="top-card">
        <h2 class="section-title">Top Products</h2>
        <TopList :items="summary.topProducts.map(p => ({ label: p.product || '—', count: p.count }))" />
      </div>
      <div class="top-card">
        <h2 class="section-title">Top Pages</h2>
        <TopList :items="summary.topPages.map(p => ({ label: p.page, count: p.count }))" />
      </div>
      <div class="top-card">
        <h2 class="section-title">Top Countries</h2>
        <TopList :items="summary.topCountries.map(c => ({ label: c.country ?? '—', count: c.count }))" />
      </div>
    </div>

    <CoarNote v-if="summary && summary.totalVisits === 0" variant="info" class="mt-4">
      No visits recorded in this period. Access logging captures documentation page views.
    </CoarNote>
    <CoarNote v-if="error" variant="error" class="mt-4">{{ error }}</CoarNote>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { CoarNote } from '@cocoar/vue-ui';
import { useUI } from '@/composables/useUI';
import { http } from '@/core/api/http';
import TopList from '@/components/TopList.vue';

interface AnalyticsSummary {
  totalVisits: number;
  uniqueIps: number;
  topPages: { page: string; count: number }[];
  topProducts: { product: string | null; count: number }[];
  topCountries: { country: string | null; count: number }[];
  visitsByDay: { date: string; count: number }[];
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
const summary = ref<AnalyticsSummary | null>(null);
const error = ref('');
const tooltip = ref<{ index: number; x: number; y: number } | null>(null);

const days = computed(() => summary.value?.visitsByDay ?? []);

// Chart geometry
const CHART_W = 900;
const CHART_H = 220;
const PAD_L = 40;
const PAD_R = 8;
const PAD_T = 10;
const PAD_B = 22;

const maxCount = computed(() => Math.max(1, ...days.value.map(d => d.count)));

const slotWidth = computed(() => {
  const n = days.value.length || 1;
  return (CHART_W - PAD_L - PAD_R) / n;
});

// 2px surface gap between adjacent bars; capped so few days don't render as one giant block.
const barWidth = computed(() => Math.min(48, Math.max(2, slotWidth.value - 2)));

function barX(i: number): number {
  // Center the (possibly capped) bar in its day slot.
  return PAD_L + i * slotWidth.value + (slotWidth.value - barWidth.value) / 2;
}

function barPath(i: number, count: number): string {
  const x = barX(i);
  const w = barWidth.value;
  const h = Math.max(1, ((CHART_H - PAD_T - PAD_B) * count) / maxCount.value);
  const y = CHART_H - PAD_B - h;
  const r = Math.min(4, w / 2, h); // rounded data-end, anchored to the baseline
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
  const wanted = Math.min(6, n);
  const step = Math.max(1, Math.floor(n / wanted));
  const ticks: { index: number; label: string }[] = [];
  for (let i = 0; i < n; i += step) {
    ticks.push({ index: i, label: days.value[i].date.slice(5) }); // MM-dd
  }
  return ticks;
});

function showTooltip(event: MouseEvent, index: number) {
  const card = (event.currentTarget as SVGElement).closest('.chart-card') as HTMLElement;
  const rect = card.getBoundingClientRect();
  tooltip.value = {
    index,
    x: Math.min(event.clientX - rect.left + 12, rect.width - 130),
    y: event.clientY - rect.top - 44,
  };
}

async function load() {
  error.value = '';
  try {
    const r = RANGES.find(x => x.key === range.value)!;
    let query = '';
    if (r.days !== null) {
      const from = new Date();
      from.setDate(from.getDate() - r.days);
      query = `?from=${encodeURIComponent(from.toISOString())}`;
    }
    summary.value = await http.get<AnalyticsSummary>(`/analytics/summary${query}`);
  } catch {
    error.value = 'Failed to load analytics';
  }
}

onMounted(load);
</script>

<style scoped>
.analytics-root {
  flex: 1;
  min-width: 0;
  padding: 24px 0;
}

.filter-row {
  display: flex;
  gap: 8px;
  margin-bottom: 20px;
}

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

.range-btn:hover {
  border-color: var(--coar-text-accent-primary);
}

.range-btn.active {
  border-color: var(--coar-text-accent-primary);
  color: var(--coar-text-accent-primary);
  font-weight: 600;
}

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

.chart-section {
  margin-bottom: 32px;
}

.chart-card {
  position: relative;
  background: var(--coar-background-neutral-primary);
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 8px;
  padding: 16px;
}

.chart-svg {
  display: block;
  width: 100%;
  height: auto;
}

.grid-line {
  stroke: var(--coar-border-neutral-tertiary);
  stroke-width: 1;
}

.axis-label {
  font-size: 10px;
  fill: var(--coar-text-neutral-tertiary);
}

.bar {
  fill: var(--coar-text-accent-primary);
  transition: opacity 0.1s;
}

.bar.dimmed {
  opacity: 0.45;
}

.chart-tooltip {
  position: absolute;
  pointer-events: none;
  background: var(--coar-background-neutral-primary);
  border: 1px solid var(--coar-border-neutral-secondary);
  border-radius: 6px;
  padding: 6px 10px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.12);
  z-index: 10;
  min-width: 110px;
}

.tooltip-date {
  font-size: 0.75rem;
  color: var(--coar-text-neutral-secondary);
}

.tooltip-value {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--coar-text-neutral-primary);
}

.top-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
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
</style>

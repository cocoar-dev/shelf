<template>
  <div class="top-list">
    <div v-for="item in items" :key="item.label" class="top-item">
      <div class="top-line">
        <span class="top-label" :title="item.label">{{ item.label }}</span>
        <span class="top-count">{{ item.count }}</span>
      </div>
      <div class="top-bar-track">
        <div class="top-bar" :style="{ width: barWidth(item.count) }" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  items: { label: string; count: number }[];
}>();

const max = computed(() => Math.max(1, ...props.items.map(i => i.count)));

function barWidth(count: number): string {
  return `${Math.max(2, (count / max.value) * 100)}%`;
}
</script>

<style scoped>
.top-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.top-line {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  font-size: 0.85rem;
}

.top-label {
  color: var(--coar-text-neutral-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  min-width: 0;
}

.top-count {
  color: var(--coar-text-neutral-secondary);
  font-variant-numeric: tabular-nums;
  flex-shrink: 0;
}

.top-bar-track {
  height: 4px;
  margin-top: 4px;
  border-radius: 2px;
  background: var(--coar-background-neutral-secondary);
  overflow: hidden;
}

.top-bar {
  height: 100%;
  border-radius: 2px;
  background: var(--coar-text-accent-primary);
}
</style>

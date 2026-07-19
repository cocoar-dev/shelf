import { defineStore } from 'pinia';
import { ref, watch } from 'vue';
import type { ProductOpenness } from '@/core/models/shelf.models';

const STORAGE_KEY = 'shelf:preferences';

/** Landing-page filter for the open-source vs proprietary marker; null = show all. */
export type OpennessFilter = 'OpenSource' | 'Proprietary' | null;

interface Preferences {
  showPreview: boolean;
  selectedTags: string[];
  opennessFilter: OpennessFilter;
}

function load(): Preferences {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) return { ...defaults(), ...JSON.parse(raw) };
  } catch { /* ignore corrupt data */ }
  return defaults();
}

function defaults(): Preferences {
  return { showPreview: false, selectedTags: [], opennessFilter: null };
}

export const usePreferencesStore = defineStore('preferences', () => {
  const saved = load();
  const showPreview = ref(saved.showPreview);
  const selectedTags = ref<string[]>(saved.selectedTags);
  const opennessFilter = ref<OpennessFilter>(saved.opennessFilter);

  function persist() {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({
      showPreview: showPreview.value,
      selectedTags: selectedTags.value,
      opennessFilter: opennessFilter.value,
    }));
  }

  watch(showPreview, persist);
  watch(selectedTags, persist, { deep: true });
  watch(opennessFilter, persist);

  function toggleTag(tag: string) {
    const idx = selectedTags.value.indexOf(tag);
    if (idx === -1) selectedTags.value.push(tag);
    else selectedTags.value.splice(idx, 1);
  }

  function clearTags() {
    selectedTags.value = [];
  }

  // Click the active filter again to clear it (toggle behavior).
  function toggleOpenness(value: Exclude<OpennessFilter, null>) {
    opennessFilter.value = opennessFilter.value === value ? null : value;
  }

  return { showPreview, selectedTags, opennessFilter, toggleTag, clearTags, toggleOpenness };
});

/** Human label for an openness value (badge + filter). */
export function opennessLabel(openness: ProductOpenness): string {
  return openness === 'OpenSource' ? 'Open Source'
    : openness === 'Proprietary' ? 'Proprietary'
      : '';
}

import { defineStore } from 'pinia';
import { ref, watch } from 'vue';

const STORAGE_KEY = 'shelf:preferences';

interface Preferences {
  showPreview: boolean;
  selectedTags: string[];
}

function load(): Preferences {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) return { ...defaults(), ...JSON.parse(raw) };
  } catch { /* ignore corrupt data */ }
  return defaults();
}

function defaults(): Preferences {
  return { showPreview: false, selectedTags: [] };
}

export const usePreferencesStore = defineStore('preferences', () => {
  const saved = load();
  const showPreview = ref(saved.showPreview);
  const selectedTags = ref<string[]>(saved.selectedTags);

  function persist() {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({
      showPreview: showPreview.value,
      selectedTags: selectedTags.value,
    }));
  }

  watch(showPreview, persist);
  watch(selectedTags, persist, { deep: true });

  function toggleTag(tag: string) {
    const idx = selectedTags.value.indexOf(tag);
    if (idx === -1) selectedTags.value.push(tag);
    else selectedTags.value.splice(idx, 1);
  }

  function clearTags() {
    selectedTags.value = [];
  }

  return { showPreview, selectedTags, toggleTag, clearTags };
});

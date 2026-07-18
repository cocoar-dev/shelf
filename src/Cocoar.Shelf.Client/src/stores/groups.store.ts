import { defineStore } from 'pinia';
import { ref } from 'vue';
import { shelfApi } from '@/core/api/shelf-api';
import type { GroupSummary, GroupUpsertRequest } from '@/core/models/shelf.models';

// Grids bind to `items` via rowDataRef — every mutation reloads the list so bound
// views refresh without manual wiring (mirrors the products store).
export const useGroupsStore = defineStore('groups', () => {
  const items = ref<GroupSummary[]>([]);
  const loaded = ref(false);

  async function loadAll(): Promise<void> {
    items.value = await shelfApi.getGroups();
    loaded.value = true;
  }

  async function create(req: GroupUpsertRequest): Promise<void> {
    await shelfApi.createGroup(req);
    await loadAll();
  }

  async function update(id: string, req: GroupUpsertRequest): Promise<void> {
    await shelfApi.updateGroup(id, req);
    await loadAll();
  }

  async function remove(id: string): Promise<void> {
    await shelfApi.deleteGroup(id);
    await loadAll();
  }

  async function recalculate(): Promise<void> {
    await shelfApi.recalculateGroups();
    await loadAll();
  }

  return { items, loadAll, create, update, remove, recalculate };
});

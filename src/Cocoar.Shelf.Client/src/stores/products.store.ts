import { defineStore } from 'pinia';
import { ref } from 'vue';
import { shelfApi } from '@/core/api/shelf-api';
import type { Product, CreateProductRequest, UpdateProductRequest } from '@/core/models/shelf.models';

// Grids bind to `items` via rowDataRef — every mutation reloads the list so bound
// views refresh without manual wiring (timetodo pattern, minus SignalR).
export const useProductsStore = defineStore('products', () => {
  const items = ref<Product[]>([]);
  const loaded = ref(false);

  async function loadAll(): Promise<void> {
    items.value = await shelfApi.getProducts();
    loaded.value = true;
  }

  async function initialize(): Promise<void> {
    if (!loaded.value) await loadAll();
  }

  async function create(req: CreateProductRequest): Promise<void> {
    await shelfApi.createProduct(req);
    await loadAll();
  }

  async function update(name: string, req: UpdateProductRequest): Promise<void> {
    await shelfApi.updateProduct(name, req);
    await loadAll();
  }

  async function remove(name: string, deleteData = false): Promise<void> {
    await shelfApi.deleteProduct(name, deleteData);
    await loadAll();
  }

  return { items, loadAll, initialize, create, update, remove };
});

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import { CoarDataGrid, CoarGridBuilder } from '@cocoar/vue-data-grid';
import { CoarButton, CoarContextMenu, CoarMenuItem, CoarMenuDivider, useContextMenu } from '@cocoar/vue-ui';
import { useUI } from '@/composables/useUI';
import { shelfApi } from '@/core/api/shelf-api';
import type { Product } from '@/core/models/shelf.models';

const router = useRouter();
const ui = useUI();
const products = ref<Product[]>([]);
const selectedNames = ref<string[]>([]);
const cellMenu = useContextMenu();
const viewportMenu = useContextMenu();

ui.set((ctx) => {
  ctx.header.title = 'Products';
  ctx.header.subTitle = 'Manage registered documentation products';
  ctx.header.icon = 'book-open';
  ctx.content.container = false;
});

const builder = CoarGridBuilder.create<Product>()
  .persistColumnState('shelf-products')
  .option('getRowId', (p: any) => p.data.name)
  .rowDataRef(products)
  .searchHighlight()
  .rowSelection('single')
  .onCellDoubleClicked((event: any) => {
    if (event.data) router.push(`/admin/products/${event.data.name}`);
  })
  .onCellContextMenu((event: any) => {
    if (!event.node.isSelected()) {
      event.api.deselectAll();
      event.node.setSelected(true);
    }
    selectedNames.value = event.api.getSelectedRows().map((r: Product) => r.name);
    cellMenu.open(event.event as MouseEvent);
  })
  .onViewportContextMenu(($event: any) => {
    viewportMenu.open($event);
  })
  .columns([
    (col: any) => col.field('name').header('Name').width(180),
    (col: any) => col.field('displayName').header('Display Name').flex(1),
    (col: any) => col.field('visibility').header('Visibility').width(110),
    (col: any) => col.field('latest').header('Latest').width(120),
    (col: any) => col.field('versions').header('Versions').width(100)
      .option('valueGetter', (p: any) => p.data?.versions?.length ?? 0),
    (col: any) => col.field('source').header('Source').width(100),
  ]);

async function loadProducts() {
  products.value = await shelfApi.getProducts();
}

async function deleteProduct() {
  const name = selectedNames.value[0];
  if (!name || !confirm(`Delete product "${name}"?`)) return;
  await shelfApi.deleteProduct(name);
  await loadProducts();
}

onMounted(loadProducts);
</script>

<template>
  <div class="flex flex-1 flex-col min-w-0 p-4">
    <CoarDataGrid :builder="builder" show-search class="flex-1 min-h-0" bordered elevated>
      <template #toolbar-right>
        <CoarButton size="s" icon-start="plus" @click="router.push('/admin/products/create')">New Product</CoarButton>
      </template>
    </CoarDataGrid>

    <CoarContextMenu :menu="cellMenu">
      <CoarMenuItem label="Open" icon="external-link" @clicked="router.push(`/admin/products/${selectedNames[0]}`)" />
      <CoarMenuItem label="Edit" icon="pencil" @clicked="router.push(`/admin/products/${selectedNames[0]}/edit`)" />
      <CoarMenuDivider />
      <CoarMenuItem label="Delete" icon="trash-2" @clicked="deleteProduct" />
    </CoarContextMenu>

    <CoarContextMenu :menu="viewportMenu">
      <CoarMenuItem label="New Product" icon="plus" @clicked="router.push('/admin/products/create')" />
      <CoarMenuItem label="Refresh" icon="refresh-cw" @clicked="loadProducts" />
    </CoarContextMenu>
  </div>
</template>

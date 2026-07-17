<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { CoarDataGrid, CoarGridBuilder } from '@cocoar/vue-data-grid';
import { CoarButton, CoarContextMenu, CoarMenuItem, CoarMenuDivider, useContextMenu } from '@cocoar/vue-ui';
import { useFragmentNavigation, useRoutedModals } from '@cocoar/vue-fragment-parser';
import { useUI } from '@/composables/useUI';
import { useProductsStore } from '@/stores/products.store';
import type { Product } from '@/core/models/shelf.models';

const router = useRouter();
const ui = useUI();
useRoutedModals();
const { navigateToModal } = useFragmentNavigation();
const productsStore = useProductsStore();

const contextProduct = ref<Product | undefined>();
const cellMenu = useContextMenu();
const viewportMenu = useContextMenu();

ui.set((ctx) => {
  ctx.header.title = 'Products';
  ctx.header.subTitle = 'Manage registered documentation products';
  ctx.header.icon = 'book-open';
  ctx.content.container = false;
});

const rowData = computed(() => productsStore.items);

const builder = CoarGridBuilder.create<Product>()
  .persistColumnState('shelf-products-v2')
  .option('getRowId', (p: any) => p.data.name)
  .rowDataRef(rowData)
  .searchHighlight()
  .rowSelection('single')
  .onCellDoubleClicked((event: any) => {
    if (event.data) navigateToModal(event.data.name);
  })
  .onCellContextMenu((event: any) => {
    if (!event.node.isSelected()) {
      event.api.deselectAll();
      event.node.setSelected(true);
    }
    contextProduct.value = event.data;
    cellMenu.open(event.event as MouseEvent);
  })
  .onViewportContextMenu(($event: any) => {
    viewportMenu.open($event);
  })
  .columns([
    (col: any) => col.field('name').header('Name').width(200).option('minWidth', 140),
    (col: any) => col.field('displayName').header('Display Name').flex(1).option('minWidth', 180),
    (col: any) => col.field('description').header('Description').flex(2).option('minWidth', 200),
    (col: any) => col.field('visibility').header('Visibility').width(110).option('minWidth', 100),
    (col: any) => col.field('latest').header('Latest').width(110).option('minWidth', 90),
    (col: any) => col.field('versions').header('Versions').width(100).option('minWidth', 95)
      .option('valueGetter', (p: any) => p.data?.versions?.length ?? 0),
    (col: any) => col.field('source').header('Source').width(100).option('minWidth', 90),
  ]);

async function deleteProduct() {
  const name = contextProduct.value?.name;
  if (!name || !confirm(`Delete product "${name}"?`)) return;
  await productsStore.remove(name);
}

function openDocs(product: Product) {
  if (product.versions.length > 0) window.open(`/${product.name}/`, '_blank');
}

onMounted(() => productsStore.loadAll());
</script>

<template>
  <div class="flex flex-1 flex-col min-w-0 p-4">
    <CoarDataGrid :builder="builder" show-search class="flex-1 min-h-0" bordered elevated>
      <template #toolbar-right>
        <CoarButton size="s" icon-start="plus" @click="navigateToModal('create')">New Product</CoarButton>
      </template>
    </CoarDataGrid>

    <CoarContextMenu :menu="cellMenu">
      <CoarMenuItem label="Edit" icon="pencil" @clicked="contextProduct && navigateToModal(contextProduct.name)" />
      <CoarMenuItem label="Open Docs" icon="external-link" @clicked="contextProduct && openDocs(contextProduct)" />
      <CoarMenuItem label="New Product" icon="plus" @clicked="navigateToModal('create')" />
      <CoarMenuDivider />
      <CoarMenuItem label="Delete Product" icon="trash-2" @clicked="deleteProduct" />
    </CoarContextMenu>

    <CoarContextMenu :menu="viewportMenu">
      <CoarMenuItem label="New Product" icon="plus" @clicked="navigateToModal('create')" />
      <CoarMenuItem label="Refresh" icon="refresh-cw" @clicked="productsStore.loadAll()" />
    </CoarContextMenu>
  </div>
</template>

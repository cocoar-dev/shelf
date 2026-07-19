<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { CoarDataGrid, CoarGridBuilder } from '@cocoar/vue-data-grid';
import { CoarButton, CoarContextMenu, CoarMenuItem, CoarMenuDivider, useContextMenu, useDialog } from '@cocoar/vue-ui';
import { useFragmentNavigation, useRoutedModals } from '@cocoar/vue-fragment-parser';
import { useGroupsStore } from '@/stores/groups.store';
import type { GroupSummary } from '@/core/models/shelf.models';

useRoutedModals();
const { navigateToModal } = useFragmentNavigation();
const groupsStore = useGroupsStore();

const contextGroup = ref<GroupSummary | undefined>();
const cellMenu = useContextMenu();
const viewportMenu = useContextMenu();
const dialog = useDialog();
const recalculating = ref(false);

const rowData = computed(() => groupsStore.items);

const builder = CoarGridBuilder.create<GroupSummary>()
  .persistColumnState('shelf-groups-v2')
  .option('getRowId', (p: any) => p.data.id)
  .rowDataRef(rowData)
  .searchHighlight()
  .rowSelection('single')
  .onCellDoubleClicked((event: any) => {
    if (event.data) navigateToModal(event.data.id);
  })
  .onCellContextMenu((event: any) => {
    if (!event.node.isSelected()) {
      event.api.deselectAll();
      event.node.setSelected(true);
    }
    contextGroup.value = event.data;
    cellMenu.open(event.event as MouseEvent);
  })
  .onViewportContextMenu(($event: any) => {
    viewportMenu.open($event);
  })
  .columns([
    (col: any) => col.field('name').header('Name').width(200).option('minWidth', 140),
    (col: any) => col.field('description').header('Description').flex(2).option('minWidth', 200),
    (col: any) => col.field('membershipMode').header('Membership').width(130).option('minWidth', 110),
    (col: any) => col.field('members').header('Members').width(110).option('minWidth', 90)
      .option('valueGetter', (p: any) => (p.data?.memberEmails?.length ?? 0) + (p.data?.autoMemberCount ?? 0)),
    (col: any) => col.field('isAdminGroup').header('Admin').width(90).option('minWidth', 80)
      .option('valueGetter', (p: any) => (p.data?.isAdminGroup ? 'yes' : '')),
  ]);

async function deleteGroup() {
  const group = contextGroup.value;
  if (!group) return;
  const ok = await dialog.confirm({
    title: 'Delete Group',
    message: `Delete group "${group.name}"? Its read grants and adminship are revoked immediately.`,
    confirmText: 'Delete',
    confirmVariant: 'danger',
  }).result;
  if (!ok) return;
  await groupsStore.remove(group.id);
}

async function recalculate() {
  recalculating.value = true;
  try {
    await groupsStore.recalculate();
  } finally {
    recalculating.value = false;
  }
}

onMounted(() => groupsStore.loadAll());
</script>

<template>
  <div class="flex flex-1 flex-col min-w-0 p-4">
    <CoarDataGrid :builder="builder" show-search class="flex-1 min-h-0" bordered elevated>
      <template #toolbar-right>
        <CoarButton size="s" variant="secondary" icon-start="refresh-cw" :loading="recalculating" @click="recalculate">
          Recalculate
        </CoarButton>
        <CoarButton size="s" icon-start="plus" @click="navigateToModal('create')">New Group</CoarButton>
      </template>
    </CoarDataGrid>

    <CoarContextMenu :menu="cellMenu">
      <CoarMenuItem label="Edit" icon="pencil" @clicked="contextGroup && navigateToModal(contextGroup.id)" />
      <CoarMenuItem label="New Group" icon="plus" @clicked="navigateToModal('create')" />
      <CoarMenuDivider />
      <CoarMenuItem label="Delete Group" icon="trash-2" @clicked="deleteGroup" />
    </CoarContextMenu>

    <CoarContextMenu :menu="viewportMenu">
      <CoarMenuItem label="New Group" icon="plus" @clicked="navigateToModal('create')" />
      <CoarMenuItem label="Recalculate membership" icon="refresh-cw" @clicked="recalculate" />
      <CoarMenuItem label="Refresh" icon="refresh-cw" @clicked="groupsStore.loadAll()" />
    </CoarContextMenu>
  </div>
</template>

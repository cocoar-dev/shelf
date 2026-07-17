<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { CoarDataGrid, CoarGridBuilder } from '@cocoar/vue-data-grid';
import { CoarButton, CoarContextMenu, CoarMenuItem, CoarMenuDivider, useContextMenu } from '@cocoar/vue-ui';
import { http } from '@/core/api/http';

interface UserRow {
  id: string;
  userName: string;
  displayName: string | null;
  email: string | null;
  isActive: boolean;
  hasPassword: boolean;
  twoFactorEnabled: boolean;
  emailOtpEnabled: boolean;
  createdAt: string;
}

const users = ref<UserRow[]>([]);
const selectedIds = ref<string[]>([]);
const cellMenu = useContextMenu();
const viewportMenu = useContextMenu();

const builder = CoarGridBuilder.create<UserRow>()
  .persistColumnState('admin-users')
  .option('getRowId', (p: any) => p.data.id)
  .rowDataRef(users)
  .searchHighlight()
  .rowSelection('single')
  .onCellDoubleClicked((_event: any) => {
    // TODO: open user detail modal
  })
  .onCellContextMenu((event: any) => {
    if (!event.node.isSelected()) {
      event.api.deselectAll();
      event.node.setSelected(true);
    }
    selectedIds.value = event.api.getSelectedRows().map((r: UserRow) => r.id);
    cellMenu.open(event.event as MouseEvent);
  })
  .onViewportContextMenu(($event: any) => {
    viewportMenu.open($event);
  })
  .columns([
    (col: any) => col.field('userName').header('Username').width(150),
    (col: any) => col.icon('hasPassword').header('').option('valueGetter', (p: any) => p.data?.hasPassword ? 'key-round' : '').width(38).resizable(false),
    (col: any) => col.field('displayName').header('Display Name').flex(1),
    (col: any) => col.field('email').header('Email').flex(1),
    (col: any) => col.icon('isActive', { color: '#16a34a', size: 's' }).option('valueGetter', (p: any) => p.data?.isActive ? 'check' : '').header('Active').width(80),
    (col: any) => col.icon('twoFactorEnabled', { color: '#2563eb', size: 's' }).option('valueGetter', (p: any) => p.data?.twoFactorEnabled ? 'shield-check' : '').header('2FA').width(60),
  ]);

async function loadUsers() {
  users.value = await http.get<UserRow[]>('/users');
}

async function deleteUser() {
  const id = selectedIds.value[0];
  if (!id) return;
  if (!confirm('Delete this user?')) return;
  await http.delete(`/users/${id}`);
  await loadUsers();
}

onMounted(loadUsers);
</script>

<template>
  <div class="flex flex-1 flex-col min-w-0 p-4">
    <CoarDataGrid :builder="builder" show-search class="flex-1 min-h-0" bordered elevated>
      <template #toolbar-right>
        <CoarButton size="s" icon-start="plus">Create</CoarButton>
      </template>
    </CoarDataGrid>

    <CoarContextMenu :menu="cellMenu">
      <CoarMenuItem label="Delete" icon="trash-2" @clicked="deleteUser" />
    </CoarContextMenu>

    <CoarContextMenu :menu="viewportMenu">
      <CoarMenuItem label="Refresh" icon="refresh-cw" @clicked="loadUsers" />
    </CoarContextMenu>
  </div>
</template>

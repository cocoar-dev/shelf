<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { CoarDataGrid, CoarGridBuilder } from '@cocoar/vue-data-grid';
import { CoarContextMenu, CoarMenuItem, useContextMenu } from '@cocoar/vue-ui';
import { http } from '@/core/api/http';

// Users are thin local mirrors of modgud identities, JIT-created at first login — there is no
// local create/password management. A deleted mirror re-provisions on the user's next login.
interface UserRow {
  id: string;
  userName: string;
  displayName: string | null;
  email: string | null;
  isActive: boolean;
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
    (col: any) => col.field('displayName').header('Display Name').flex(1),
    (col: any) => col.field('email').header('Email').flex(1),
    (col: any) => col.icon('isActive', { color: '#16a34a', size: 's' }).option('valueGetter', (p: any) => p.data?.isActive ? 'check' : '').header('Active').width(80),
    (col: any) => col.field('createdAt').header('First Login').width(160)
      .option('valueFormatter', (p: any) => p.value ? new Date(p.value).toLocaleDateString() : ''),
  ]);

async function loadUsers() {
  users.value = await http.get<UserRow[]>('/users');
}

function selectedUser(): UserRow | undefined {
  return users.value.find(u => u.id === selectedIds.value[0]);
}

async function toggleActive() {
  const user = selectedUser();
  if (!user) return;
  await http.put(`/users/${user.id}/active`, { isActive: !user.isActive });
  await loadUsers();
}

async function deleteUser() {
  const id = selectedIds.value[0];
  if (!id) return;
  if (!confirm('Delete this user? It will be re-created on their next login.')) return;
  await http.delete(`/users/${id}`);
  await loadUsers();
}

onMounted(loadUsers);
</script>

<template>
  <div class="flex flex-1 flex-col min-w-0 p-4">
    <CoarDataGrid :builder="builder" show-search class="flex-1 min-h-0" bordered elevated />

    <CoarContextMenu :menu="cellMenu">
      <CoarMenuItem label="Toggle Active" icon="power" @clicked="toggleActive" />
      <CoarMenuItem label="Delete" icon="trash-2" @clicked="deleteUser" />
    </CoarContextMenu>

    <CoarContextMenu :menu="viewportMenu">
      <CoarMenuItem label="Refresh" icon="refresh-cw" @clicked="loadUsers" />
    </CoarContextMenu>
  </div>
</template>

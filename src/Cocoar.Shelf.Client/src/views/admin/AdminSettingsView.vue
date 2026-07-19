<script setup lang="ts">
import { useRouter, useRoute, RouterView } from 'vue-router';
import { CoarMenu, CoarMenuItem } from '@cocoar/vue-ui';
import { useUI } from '@/composables/useUI';
import { watch } from 'vue';

const router = useRouter();
const route = useRoute();
const ui = useUI();

watch(() => route.path, () => {
  ui.set((ctx) => {
    ctx.header.title = 'Administration';
    ctx.header.icon = 'cog';
    ctx.content.container = false;
    ctx.content.hasSubNav = true;
  });
}, { immediate: true });

function isActive(path: string): boolean {
  return route.path.startsWith(path);
}
</script>

<template>
  <div class="flex min-h-0 flex-1">
    <div class="sub-nav flex-shrink-0 p-4 flex flex-col min-h-0">
      <CoarMenu>
        <CoarMenuItem
          icon="key-round"
          label="General"
          :class="{ 'admin-menu-item--active': isActive('/admin/settings/general') }"
          @clicked="router.push('/admin/settings/general')"
        />
        <CoarMenuItem
          icon="users"
          label="Users"
          :class="{ 'admin-menu-item--active': isActive('/admin/settings/users') }"
          @clicked="router.push('/admin/settings/users')"
        />
        <CoarMenuItem
          icon="shield"
          label="Groups"
          :class="{ 'admin-menu-item--active': isActive('/admin/settings/groups') }"
          @clicked="router.push('/admin/settings/groups')"
        />
        <CoarMenuItem
          icon="bar-chart-3"
          label="Access Log"
          :class="{ 'admin-menu-item--active': isActive('/admin/settings/access-log') }"
          @clicked="router.push('/admin/settings/access-log')"
        />
        <CoarMenuItem
          icon="globe"
          label="GeoIP"
          :class="{ 'admin-menu-item--active': isActive('/admin/settings/geoip') }"
          @clicked="router.push('/admin/settings/geoip')"
        />
      </CoarMenu>
    </div>

    <div class="flex-1 flex justify-center min-w-0">
      <div class="flex w-11/12">
        <RouterView class="flex-1 min-h-0" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.sub-nav {
  width: 13rem;
  height: 100%;
  --coar-background-neutral-primary: var(--coar-background-neutral-secondary, #f7f7f7);
}

.admin-menu-item--active {
  background: var(--coar-menu-item-background-active, #eff6ff);
  color: var(--coar-menu-item-text-active, #1d4ed8);
  font-weight: 500;
  border-radius: 6px;
}
</style>

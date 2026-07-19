<script setup lang="ts">
import { computed, ref } from 'vue';
import { useRouter, useRoute, RouterView } from 'vue-router';
import {
  CoarIcon,
  CoarButton,
  CoarContextMenu,
  CoarMenuItem,
  CoarMenuDivider,
  CoarSidebar,
  CoarSidebarItem,
  CoarSidebarDivider,
  CoarSidebarSpacer,
  useContextMenu,
} from '@cocoar/vue-ui';
import { provideUI } from '@/composables/useUI';
import { useAuthStore } from '@/stores/auth.store';

const router = useRouter();
const route = useRoute();
const { state: ui } = provideUI();
const authStore = useAuthStore();

const collapsed = ref(
  localStorage.getItem('sidebar-collapsed') === 'true',
);

function toggleCollapsed() {
  collapsed.value = !collapsed.value;
  localStorage.setItem('sidebar-collapsed', String(collapsed.value));
}

const darkMode = ref(localStorage.getItem('dark-mode') === 'true');

function toggleDarkMode() {
  darkMode.value = !darkMode.value;
  localStorage.setItem('dark-mode', String(darkMode.value));
  document.documentElement.classList.toggle('dark-mode', darkMode.value);
}

const userInitials = computed(() => {
  const name = authStore.userName;
  if (!name) return '??';
  return name.substring(0, 2).toUpperCase();
});

const userMenu = useContextMenu();

function openUserMenu(event: MouseEvent) {
  const btn = event.currentTarget as HTMLElement;
  const rect = btn.getBoundingClientRect();
  userMenu.open({ clientX: rect.right, clientY: rect.bottom + 4 } as MouseEvent);
}

function logout() {
  authStore.logout(); // full page navigation — server ends cookie + modgud session
}
</script>

<template>
  <div class="flex h-screen flex-col">
    <!-- Header -->
    <header v-if="ui.header.show" class="main-header">
      <button class="header-logo" :style="{ width: collapsed ? '4rem' : '16rem' }" @click="router.push('/')">
        <CoarIcon name="book-open" />
        <span v-if="!collapsed" class="text-sm font-medium tracking-wide opacity-80">Shelf</span>
      </button>

      <div class="header-content">
        <CoarIcon v-if="ui.header.icon" :name="ui.header.icon" class="header-icon" />
        <div class="flex flex-col justify-center" style="line-height: 1.5em">
          <div class="title" :class="{ 'title-only': !ui.header.subTitle }">
            {{ ui.header.title }}
          </div>
          <div v-if="ui.header.subTitle" class="subtitle">
            {{ ui.header.subTitle }}
          </div>
        </div>
        <div class="flex-1" />

        <!-- Dark mode toggle -->
        <button
          class="ml-2 flex h-9 w-9 items-center justify-center rounded-full text-white/70 transition hover:bg-white/20"
          title="Toggle dark mode"
          @click="toggleDarkMode"
        >
          <CoarIcon :name="darkMode ? 'sun' : 'moon'" />
        </button>

        <!-- User Avatar -->
        <button
          class="ml-2 flex h-9 w-9 items-center justify-center rounded-full bg-white/20 text-sm font-bold text-white transition hover:bg-white/30"
          :title="authStore.userName ?? ''" @click="openUserMenu"
        >
          {{ userInitials }}
        </button>
      </div>
    </header>

    <!-- User Menu -->
    <CoarContextMenu :menu="userMenu">
      <CoarMenuItem :label="authStore.userName ?? 'User'" icon="user" @clicked="router.push('/admin/profile')" />
      <CoarMenuDivider />
      <CoarMenuItem label="Profile & Security" icon="circle-user" @clicked="router.push('/admin/profile')" />
      <CoarMenuItem label="Sign Out" icon="log-out" @clicked="logout" />
    </CoarContextMenu>

    <!-- Body -->
    <div class="flex flex-1 overflow-hidden">
      <CoarSidebar v-model:collapsed="collapsed" elevated class="z-10">
        <CoarSidebarSpacer height="4px" />
        <CoarSidebarItem
          icon="layout-dashboard"
          label="Dashboard"
          :active="route.path === '/admin' || route.path === '/admin/'"
          @click="router.push('/admin')"
        />
        <CoarSidebarItem
          v-if="authStore.user?.isAdmin"
          icon="book-open"
          label="Products"
          :active="route.path.startsWith('/admin/products')"
          @click="router.push('/admin/products')"
        />
        <CoarSidebarItem
          v-if="authStore.user?.isAdmin"
          icon="bar-chart-3"
          label="Analytics"
          :active="route.path.startsWith('/admin/analytics')"
          @click="router.push('/admin/analytics')"
        />
        <CoarSidebarItem
          v-if="authStore.user?.isAdmin"
          icon="cog"
          label="Administration"
          :active="route.path.startsWith('/admin/settings')"
          @click="router.push('/admin/settings')"
        />

        <CoarSidebarSpacer grow />

        <template #footer="{ collapsed: c }">
          <CoarSidebarDivider />
          <CoarSidebarItem
            :icon="c ? 'chevron-right' : 'chevron-left'"
            :label="c ? 'Expand' : 'Collapse'"
            @click="toggleCollapsed"
          />
        </template>
      </CoarSidebar>

      <div class="flex flex-1 flex-col overflow-hidden">
        <main class="flex flex-1" :style="{ overflow: ui.content.container ? 'auto' : 'hidden' }">
          <div class="main-container flex" :class="ui.content.container ? 'container-mode' : 'flex-1'">
            <RouterView />
          </div>
        </main>

        <footer v-if="ui.footer.show" class="main-footer">
          <div v-if="ui.content.hasSubNav" class="sub-nav-spacer" />
          <div class="flex flex-1 justify-center min-w-0">
            <div class="flex items-center w-11/12">
              <div class="flex-1" />
              <div class="flex items-center gap-2">
                <CoarButton v-if="ui.footer.button3.visible" variant="ghost" size="s"
                  :disabled="ui.footer.button3.disabled" :loading="ui.footer.button3.loading"
                  @click="ui.footer.button3.onClick?.()">{{ ui.footer.button3.text }}</CoarButton>
                <CoarButton v-if="ui.footer.button2.visible" variant="secondary" size="s"
                  :disabled="ui.footer.button2.disabled" :loading="ui.footer.button2.loading"
                  @click="ui.footer.button2.onClick?.()">{{ ui.footer.button2.text }}</CoarButton>
                <CoarButton v-if="ui.footer.button1.visible" variant="primary" size="s"
                  :disabled="ui.footer.button1.disabled" :loading="ui.footer.button1.loading"
                  @click="ui.footer.button1.onClick?.()">{{ ui.footer.button1.text }}</CoarButton>
              </div>
            </div>
          </div>
        </footer>
      </div>
    </div>
  </div>
</template>

<style scoped>
.main-header {
  min-height: 64px;
  max-height: 64px;
  background-color: var(--color-header);
  color: white;
  display: flex;
  flex-direction: row;
  box-shadow: 0px 2px 6px #00152959;
  position: relative;
  z-index: 30;
}

.header-logo {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  flex-shrink: 0;
  color: white;
  transition: width 0.2s ease;
  border: none;
  background: none;
  cursor: pointer;
}

.header-content {
  display: flex;
  align-items: center;
  flex: 1;
  max-width: 100%;
  padding: 0 1.5rem;
}

.header-icon {
  font-size: xx-large;
  width: 52px;
  text-align: left;
}

.title {
  font-size: 1.5em;
  font-weight: bold;
}

.title.title-only {
  font-size: 2em;
}

.subtitle {
  font-size: 0.9em;
}

.main-footer {
  display: flex;
  align-items: center;
  background: var(--coar-background-neutral-secondary, #f7f7f7);
  border-top: 1px solid #e9e9e9;
  padding: 12px 8px;
}

.sub-nav-spacer {
  width: 13rem;
  flex-shrink: 0;
}

.container-mode {
  max-width: 1200px;
  width: 90%;
  margin-left: auto;
  margin-right: auto;
}
</style>

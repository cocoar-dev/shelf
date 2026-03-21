<template>
  <div class="layout">
    <aside class="sidebar">
      <div class="sidebar-brand">
        <span class="brand-text">Shelf</span>
        <span class="brand-sub">Admin</span>
      </div>

      <nav class="sidebar-nav">
        <RouterLink to="/admin" class="nav-item" exact-active-class="nav-item--active">
          <CoarIcon name="layout-dashboard" :size="18" />
          <span>Dashboard</span>
        </RouterLink>
        <RouterLink to="/admin/products" class="nav-item" active-class="nav-item--active">
          <CoarIcon name="package" :size="18" />
          <span>Products</span>
        </RouterLink>
      </nav>

      <div class="sidebar-footer">
        <button class="nav-item" @click="onLogout">
          <CoarIcon name="log-out" :size="18" />
          <span>Sign Out</span>
        </button>
      </div>
    </aside>

    <div class="main-wrapper">
      <header v-if="ui.state.header.show" class="main-header">
        <div>
          <h1 class="header-title">{{ ui.state.header.title }}</h1>
          <p v-if="ui.state.header.subTitle" class="header-subtitle">{{ ui.state.header.subTitle }}</p>
        </div>
      </header>

      <div v-if="ui.state.content.showLoadingBar" class="loading-bar" />

      <main class="main-content" :class="{ 'main-content--scrollable': ui.state.content.scrollable }">
        <div v-if="ui.state.content.container" class="content-container" :class="{ 'content-container--padded': ui.state.content.padding }">
          <RouterView />
        </div>
        <RouterView v-else />
      </main>

      <footer v-if="ui.state.footer.show" class="main-footer">
        <div class="footer-actions">
          <CoarButton
            v-if="ui.state.footer.button1.visible"
            variant="secondary"
            :disabled="ui.state.footer.button1.disabled"
            :loading="ui.state.footer.button1.loading"
            @click="ui.state.footer.button1.onClick?.()"
          >
            {{ ui.state.footer.button1.text || 'Back' }}
          </CoarButton>
          <div class="footer-spacer" />
          <CoarButton
            v-if="ui.state.footer.button2.visible"
            variant="danger"
            :disabled="ui.state.footer.button2.disabled"
            :loading="ui.state.footer.button2.loading"
            @click="ui.state.footer.button2.onClick?.()"
          >
            {{ ui.state.footer.button2.text || 'Delete' }}
          </CoarButton>
          <CoarButton
            v-if="ui.state.footer.button3.visible"
            variant="primary"
            :disabled="ui.state.footer.button3.disabled"
            :loading="ui.state.footer.button3.loading"
            @click="ui.state.footer.button3.onClick?.()"
          >
            {{ ui.state.footer.button3.text || 'Save' }}
          </CoarButton>
        </div>
      </footer>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router';
import { CoarButton, CoarIcon } from '@cocoar/vue-ui';
import { useUI } from '@/composables/useUI';
import { useAuthStore } from '@/stores/auth.store';

const router = useRouter();
const ui = useUI();
const auth = useAuthStore();

function onLogout() {
  auth.logout();
  router.push('/login');
}
</script>

<style scoped>
.layout {
  display: flex;
  height: 100vh;
  overflow: hidden;
}

.sidebar {
  width: 240px;
  min-width: 240px;
  background: var(--coar-background-neutral-primary);
  border-right: 1px solid var(--coar-border-neutral-tertiary);
  display: flex;
  flex-direction: column;
}

.sidebar-brand {
  padding: 20px 20px 16px;
  border-bottom: 1px solid var(--coar-border-neutral-tertiary);
  display: flex;
  align-items: baseline;
  gap: 6px;
}

.brand-text {
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--coar-text-accent-primary);
}

.brand-sub {
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--coar-text-neutral-secondary);
}

.sidebar-nav {
  flex: 1;
  padding: 12px 8px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.sidebar-footer {
  padding: 8px;
  border-top: 1px solid var(--coar-border-neutral-tertiary);
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: 6px;
  font-size: 0.9rem;
  font-weight: 500;
  color: var(--coar-text-neutral-secondary);
  text-decoration: none;
  border: none;
  background: none;
  cursor: pointer;
  width: 100%;
  transition: background 0.15s, color 0.15s;
}

.nav-item:hover {
  background: var(--coar-background-neutral-secondary);
  color: var(--coar-text-neutral-primary);
}

.nav-item--active {
  background: var(--coar-background-accent-tertiary);
  color: var(--coar-text-accent-primary);
}

.main-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.main-header {
  padding: 20px 32px;
  border-bottom: 1px solid var(--coar-border-neutral-tertiary);
  background: var(--coar-background-neutral-primary);
}

.header-title {
  font-size: 1.25rem;
  font-weight: 600;
  color: var(--coar-text-neutral-primary);
  margin: 0;
}

.header-subtitle {
  font-size: 0.85rem;
  color: var(--coar-text-neutral-secondary);
  margin: 4px 0 0;
}

.loading-bar {
  height: 3px;
  background: var(--coar-text-accent-primary);
  animation: loading 1.5s infinite ease-in-out;
}

@keyframes loading {
  0% { transform: scaleX(0); transform-origin: left; }
  50% { transform: scaleX(1); transform-origin: left; }
  51% { transform-origin: right; }
  100% { transform: scaleX(0); transform-origin: right; }
}

.main-content {
  flex: 1;
  overflow: hidden;
}

.main-content--scrollable {
  overflow-y: auto;
}

.content-container {
  max-width: 1100px;
  margin: 0 auto;
  width: 90%;
}

.content-container--padded {
  padding: 32px 0;
}

.main-footer {
  padding: 12px 32px;
  border-top: 1px solid var(--coar-border-neutral-tertiary);
  background: var(--coar-background-neutral-primary);
}

.footer-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.footer-spacer {
  flex: 1;
}
</style>

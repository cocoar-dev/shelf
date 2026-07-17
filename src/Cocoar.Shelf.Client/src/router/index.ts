import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '@/stores/auth.store';

const pathBase = (window.__SHELF_OPTIONS__?.pathBase ?? '').replace(/\/$/, '');

export const router = createRouter({
  history: createWebHistory(pathBase + '/'),
  routes: [
    {
      path: '/',
      component: () => import('@/views/LandingView.vue'),
      meta: { public: true },
    },
    {
      path: '/login',
      component: () => import('@/views/LoginView.vue'),
      meta: { public: true },
    },
    {
      path: '/setup',
      component: () => import('@/views/SetupView.vue'),
      meta: { public: true },
    },
    {
      path: '/magic-login',
      component: () => import('@/views/MagicLoginView.vue'),
      meta: { public: true },
    },
    {
      path: '/forgot-password',
      component: () => import('@/views/ForgotPasswordView.vue'),
      meta: { public: true },
    },
    {
      path: '/reset-password',
      component: () => import('@/views/ResetPasswordView.vue'),
      meta: { public: true },
    },
    {
      path: '/admin',
      component: () => import('@/layouts/AdminLayout.vue'),
      children: [
        { path: '', component: () => import('@/views/DashboardView.vue') },
        { path: 'products', component: () => import('@/views/products/ProductListView.vue') },
        { path: 'products/create', component: () => import('@/views/products/ProductFormView.vue') },
        { path: 'products/:name', component: () => import('@/views/products/ProductDetailView.vue') },
        { path: 'products/:name/edit', component: () => import('@/views/products/ProductFormView.vue') },
        { path: 'profile', component: () => import('@/views/ProfileView.vue') },
        { path: 'analytics', component: () => import('@/views/DashboardView.vue') }, // placeholder
        {
          path: 'settings',
          component: () => import('@/views/admin/AdminSettingsView.vue'),
          children: [
            { path: '', redirect: '/admin/settings/users' },
            { path: 'users', component: () => import('@/views/admin/UserListView.vue') },
          ],
        },
      ],
    },
  ],
});

let sessionChecked = false;

router.beforeEach(async (to) => {
  const auth = useAuthStore();

  if (!sessionChecked) {
    sessionChecked = true;

    try {
      const status = await auth.fetchSetupStatus();
      if (status.needsSetup && to.path !== '/setup') {
        return '/setup';
      }
    } catch {
      // No DB configured, setup endpoint not available
    }

    await auth.checkSession();
  }

  if (!to.meta.public && !auth.isAuthenticated) {
    return '/login';
  }
  if (to.path === '/login' && auth.isAuthenticated) {
    return '/admin';
  }
});

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
      path: '/admin',
      component: () => import('@/layouts/AdminLayout.vue'),
      children: [
        { path: '', component: () => import('@/views/DashboardView.vue') },
        {
          path: 'products',
          component: () => import('@/views/products/ProductListView.vue'),
          meta: {
            routedFragments: [
              {
                type: 'modal',
                path: ':id',
                component: () => import('@/views/products/ProductFormModal.vue'),
              },
            ],
          },
        },
        { path: 'profile', component: () => import('@/views/ProfileView.vue') },
        {
          path: 'analytics',
          component: () => import('@/views/AnalyticsView.vue'),
          meta: { admin: true },
        },
        {
          path: 'settings',
          component: () => import('@/views/admin/AdminSettingsView.vue'),
          meta: { admin: true },
          children: [
            { path: '', redirect: '/admin/settings/general' },
            { path: 'general', component: () => import('@/views/admin/GeneralSettingsView.vue') },
            { path: 'users', component: () => import('@/views/admin/UserListView.vue') },
            { path: 'access-log', component: () => import('@/views/admin/AccessLogView.vue') },
            { path: 'geoip', component: () => import('@/views/admin/GeoIpView.vue') },
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
    await auth.checkSession();
  }

  if (!to.meta.public && !auth.isAuthenticated) {
    return '/login';
  }
  if (to.path === '/login' && auth.isAuthenticated) {
    return '/admin';
  }
  // The server is the real gate (Admin policy); this only spares non-admins a broken page.
  if (to.matched.some(r => r.meta.admin) && !auth.user?.isAdmin) {
    return '/admin';
  }
});

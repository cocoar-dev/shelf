import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '@/stores/auth.store';

const pathBase = (window.__SHELF_OPTIONS__?.pathBase ?? '').replace(/\/$/, '');

export const router = createRouter({
  history: createWebHistory(pathBase + '/'),
  routes: [
    {
      // Shared shell (header always; sidebar only in the admin area). Public landing and the
      // admin area are both children, so the whole SPA is one consistent view.
      path: '/',
      component: () => import('@/layouts/AppLayout.vue'),
      children: [
        { path: '', component: () => import('@/views/LandingView.vue'), meta: { public: true } },

        { path: 'admin', component: () => import('@/views/DashboardView.vue') },
        {
          path: 'admin/products',
          component: () => import('@/views/products/ProductListView.vue'),
          meta: {
            admin: true,
            routedFragments: [
              {
                type: 'modal',
                path: ':id',
                component: () => import('@/views/products/ProductFormModal.vue'),
                // Fixed modal height — switching tabs must not resize; content scrolls instead.
                overlayOptions: { size: { height: '80vh' } },
              },
            ],
          },
        },
        { path: 'admin/profile', component: () => import('@/views/ProfileView.vue') },
        {
          path: 'admin/analytics',
          component: () => import('@/views/AnalyticsView.vue'),
          meta: { admin: true },
        },
        {
          path: 'admin/settings',
          component: () => import('@/views/admin/AdminSettingsView.vue'),
          meta: { admin: true },
          children: [
            { path: '', redirect: '/admin/settings/general' },
            { path: 'general', component: () => import('@/views/admin/GeneralSettingsView.vue') },
            { path: 'users', component: () => import('@/views/admin/UserListView.vue') },
            {
              path: 'groups',
              component: () => import('@/views/groups/GroupListView.vue'),
              meta: {
                routedFragments: [
                  {
                    type: 'modal',
                    path: ':id',
                    component: () => import('@/views/groups/GroupFormModal.vue'),
                    overlayOptions: { size: { height: '80vh' } },
                  },
                ],
              },
            },
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

  // /login is a SERVER route (OIDC challenge) — leave the SPA entirely. With a modgud browser
  // session this signs in silently and returns; without one, modgud shows its login page.
  if (!to.meta.public && !auth.isAuthenticated) {
    auth.login(pathBase + to.fullPath);
    return false;
  }
  // The server is the real gate (Admin policy); this only spares non-admins a broken page.
  if (to.matched.some(r => r.meta.admin) && !auth.user?.isAdmin) {
    return '/admin';
  }
});

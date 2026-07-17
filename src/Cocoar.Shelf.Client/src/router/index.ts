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
        { path: 'products', component: () => import('@/views/products/ProductListView.vue') },
        { path: 'products/create', component: () => import('@/views/products/ProductFormView.vue') },
        { path: 'products/:name', component: () => import('@/views/products/ProductDetailView.vue') },
        { path: 'products/:name/edit', component: () => import('@/views/products/ProductFormView.vue') },
        { path: 'profile', component: () => import('@/views/ProfileView.vue') },
        { path: 'analytics', component: () => import('@/views/DashboardView.vue') }, // placeholder
        {
          path: 'settings',
          component: () => import('@/views/admin/AdminSettingsView.vue'),
          meta: { admin: true },
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

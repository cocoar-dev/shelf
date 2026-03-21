import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '@/stores/auth.store';

export const router = createRouter({
  history: createWebHistory('/'),
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
      ],
    },
  ],
});

let sessionChecked = false;

router.beforeEach(async (to) => {
  const auth = useAuthStore();

  // Check session once on first navigation (handles page refresh)
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
});

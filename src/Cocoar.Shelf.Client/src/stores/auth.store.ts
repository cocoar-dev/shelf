import { defineStore } from 'pinia';
import { ref, computed } from 'vue';

export const useAuthStore = defineStore('auth', () => {
  const apiKey = ref<string | null>(sessionStorage.getItem('shelf_api_key'));
  const isAuthenticated = computed(() => !!apiKey.value);

  async function login(key: string): Promise<boolean> {
    apiKey.value = key;
    try {
      const response = await fetch('/_api/admin/verify', {
        headers: { 'Authorization': `Bearer ${key}` },
      });
      if (response.ok) {
        sessionStorage.setItem('shelf_api_key', key);
        return true;
      }
      apiKey.value = null;
      return false;
    } catch {
      apiKey.value = null;
      return false;
    }
  }

  function logout() {
    apiKey.value = null;
    sessionStorage.removeItem('shelf_api_key');
  }

  return { apiKey, isAuthenticated, login, logout };
});

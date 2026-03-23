import { defineStore } from 'pinia';
import { ref, computed } from 'vue';

export const useAuthStore = defineStore('auth', () => {
  const isAuthenticated = ref(false);
  const userName = ref<string | null>(null);

  async function checkSession(): Promise<boolean> {
    try {
      const response = await fetch('/_api/auth/me', { credentials: 'include' });
      if (response.ok) {
        const data = await response.json();
        isAuthenticated.value = data.authenticated;
        userName.value = data.name;
        return true;
      }
      isAuthenticated.value = false;
      userName.value = null;
      return false;
    } catch {
      isAuthenticated.value = false;
      userName.value = null;
      return false;
    }
  }

  async function login(apiKey: string): Promise<boolean> {
    try {
      const response = await fetch('/_api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ apiKey }),
      });

      if (response.ok) {
        const data = await response.json();
        isAuthenticated.value = true;
        userName.value = data.name;
        return true;
      }
      return false;
    } catch {
      return false;
    }
  }

  async function logout() {
    try {
      await fetch('/_api/auth/logout', {
        method: 'POST',
        credentials: 'include',
      });
    } finally {
      isAuthenticated.value = false;
      userName.value = null;
    }
  }

  return { isAuthenticated, userName, checkSession, login, logout };
});

import { defineStore } from 'pinia';
import { ref } from 'vue';

export interface AuthUser {
  id: string;
  email?: string;
  displayName: string;
  isAdmin: boolean;
  permissions: string[];
}

// Login is federated to modgud via the OIDC code flow: navigating to /login challenges the IdP,
// the backend mints the httpOnly cookie on callback — no tokens and no credential UI in the client.
export const useAuthStore = defineStore('auth', () => {
  const isAuthenticated = ref(false);
  const userName = ref<string | null>(null);
  const user = ref<AuthUser | null>(null);

  async function checkSession(): Promise<boolean> {
    try {
      const response = await fetch('/_api/auth/me', { credentials: 'include' });
      if (response.ok) {
        const data = await response.json();
        if (data.authenticated) {
          isAuthenticated.value = true;
          userName.value = data.displayName ?? data.email;
          user.value = data;
          return true;
        }
      }
      clearAuth();
      return false;
    } catch {
      clearAuth();
      return false;
    }
  }

  /// Full page navigation: the server ends the app cookie AND the modgud session
  /// (otherwise the next /login would silently sign right back in), then lands on "/".
  function logout(): void {
    clearAuth();
    window.location.href = '/logout';
  }

  /// Full page navigation to the OIDC challenge; returns to `returnTo` after the callback.
  function login(returnTo?: string): void {
    const target = returnTo ?? window.location.pathname + window.location.search + window.location.hash;
    window.location.href = '/login?returnUrl=' + encodeURIComponent(target);
  }

  function clearAuth() {
    isAuthenticated.value = false;
    userName.value = null;
    user.value = null;
  }

  return {
    isAuthenticated,
    userName,
    user,
    checkSession,
    login,
    logout,
  };
});

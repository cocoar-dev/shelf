import { defineStore } from 'pinia';
import { ref } from 'vue';

export interface AuthUser {
  id: string;
  email?: string;
  displayName: string;
  isAdmin: boolean;
  permissions: string[];
}

// Login is federated to modgud: the backend brokers the email-code flow server-to-server and the
// httpOnly cookie is the whole session — no tokens, no MFA branches in the client.
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

  async function requestLoginCode(email: string): Promise<void> {
    const response = await fetch('/_api/auth/otp/request', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ email }),
    });

    if (!response.ok) {
      const data = await response.json();
      throw new Error(data.error ?? 'Failed to send code');
    }
  }

  async function verifyLoginCode(email: string, code: string): Promise<void> {
    const response = await fetch('/_api/auth/otp/verify', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ email, code }),
    });

    if (!response.ok) {
      const data = await response.json();
      throw new Error(data.error ?? 'Invalid or expired code');
    }

    await checkSession();
  }

  async function logout(): Promise<void> {
    try {
      await fetch('/_api/auth/logout', { method: 'POST', credentials: 'include' });
    } finally {
      clearAuth();
    }
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
    requestLoginCode,
    verifyLoginCode,
    logout,
  };
});

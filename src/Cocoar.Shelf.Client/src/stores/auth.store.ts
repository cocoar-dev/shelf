import { defineStore } from 'pinia';
import { ref } from 'vue';

export interface AuthUser {
  id: string;
  userName: string;
  displayName?: string;
  email?: string;
  has2FA: boolean;
  twoFactorMethods: string[];
}

export interface LoginResponse {
  requiresMfa?: boolean;
  mfaMethods?: string[];
  name?: string;
  userName?: string;
}

export interface SetupStatus {
  needsSetup: boolean;
}

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
          userName.value = data.displayName ?? data.userName;
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

  async function login(userNameInput: string, password: string, rememberMe = false): Promise<LoginResponse> {
    const response = await fetch('/_api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ userName: userNameInput, password, rememberMe }),
    });

    const data = await response.json();

    if (!response.ok) {
      throw new Error(data.error ?? 'Login failed');
    }

    if (data.requiresMfa) {
      return data as LoginResponse;
    }

    isAuthenticated.value = true;
    userName.value = data.name ?? data.userName;
    return data as LoginResponse;
  }

  async function mfaLogin(code: string, rememberMe = false, rememberMachine = false): Promise<void> {
    const response = await fetch('/_api/auth/mfa/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ code, rememberMe, rememberMachine }),
    });

    if (!response.ok) {
      const data = await response.json();
      throw new Error(data.error ?? 'Invalid code');
    }

    await checkSession();
  }

  async function requestEmailOtp(): Promise<void> {
    const response = await fetch('/_api/auth/email-otp/login/request', {
      method: 'POST',
      credentials: 'include',
    });

    if (!response.ok) {
      const data = await response.json();
      throw new Error(data.error ?? 'Failed to send code');
    }
  }

  async function emailOtpLogin(code: string, rememberMe = false): Promise<void> {
    const response = await fetch('/_api/auth/email-otp/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ code, rememberMe }),
    });

    if (!response.ok) {
      const data = await response.json();
      throw new Error(data.error ?? 'Invalid code');
    }

    await checkSession();
  }

  async function requestMagicLink(email: string): Promise<void> {
    const response = await fetch('/_api/auth/magic-link/request', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ email }),
    });

    if (!response.ok) {
      const data = await response.json();
      throw new Error(data.error ?? 'Failed to send link');
    }
  }

  async function magicLinkLogin(userId: string, token: string, rememberMe = false): Promise<void> {
    const response = await fetch('/_api/auth/magic-link/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ userId, token, rememberMe }),
    });

    if (!response.ok) {
      const data = await response.json();
      throw new Error(data.error ?? 'Invalid link');
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

  async function fetchSetupStatus(): Promise<SetupStatus> {
    const response = await fetch('/_api/setup/status', { credentials: 'include' });
    return await response.json();
  }

  async function createAdmin(data: {
    userName: string;
    password: string;
    displayName?: string;
    email?: string;
  }): Promise<void> {
    const response = await fetch('/_api/setup/create-admin', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const err = await response.json();
      throw new Error(err.error ?? 'Setup failed');
    }

    await checkSession();
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
    mfaLogin,
    requestEmailOtp,
    emailOtpLogin,
    requestMagicLink,
    magicLinkLogin,
    logout,
    fetchSetupStatus,
    createAdmin,
  };
});

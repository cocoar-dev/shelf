<template>
  <div class="login-container">
    <div class="login-card">
      <div class="login-header">
        <h1>Shelf Admin</h1>
        <p v-if="step === 'credentials'">Sign in to continue</p>
        <p v-else-if="step === 'mfa-choice'">Choose verification method</p>
        <p v-else-if="step === 'totp'">Enter authenticator code</p>
        <p v-else-if="step === 'email-otp'">Enter email code</p>
        <p v-else-if="step === 'magic-link'">Sign in with email link</p>
      </div>

      <!-- Step: Credentials -->
      <form v-if="step === 'credentials'" @submit.prevent="onLogin">
        <CoarTextInput
          v-model="loginUserName"
          label="Username"
          placeholder="Enter username"
          :disabled="isLoading"
          required
        />

        <CoarTextInput
          v-model="password"
          label="Password"
          type="password"
          placeholder="Enter password"
          :disabled="isLoading"
          class="mt-3"
          required
        />

        <label class="remember-me mt-3">
          <input v-model="rememberMe" type="checkbox" :disabled="isLoading" />
          <span>Remember me</span>
        </label>

        <CoarNote v-if="error" variant="error" class="mt-3">{{ error }}</CoarNote>

        <CoarButton
          type="submit"
          variant="primary"
          :loading="isLoading"
          :disabled="!loginUserName || !password"
          class="mt-4 w-full"
        >
          Sign In
        </CoarButton>

        <!-- Passkey login -->
        <div class="divider mt-4">
          <span>or</span>
        </div>

        <CoarButton
          variant="secondary"
          class="mt-3 w-full"
          :loading="passkeyLoading"
          @click="onPasskeyLogin"
        >
          Sign in with Passkey
        </CoarButton>

        <!-- Magic link -->
        <button type="button" class="text-link mt-3" @click="step = 'magic-link'">
          Send me a login link
        </button>

        <!-- Forgot password -->
        <button type="button" class="text-link mt-2" @click="$router.push('/forgot-password')">
          Forgot password?
        </button>
      </form>

      <!-- Step: MFA Choice -->
      <div v-else-if="step === 'mfa-choice'" class="mfa-choices">
        <button v-if="mfaMethods.includes('totp')" class="mfa-choice" @click="step = 'totp'">
          Authenticator App
        </button>
        <button v-if="mfaMethods.includes('email')" class="mfa-choice" @click="onRequestEmailOtp">
          Email Code
        </button>
        <button type="button" class="back-link mt-3" @click="step = 'credentials'">Back</button>
      </div>

      <!-- Step: TOTP -->
      <form v-else-if="step === 'totp'" @submit.prevent="onMfaLogin">
        <CoarTextInput v-model="mfaCode" label="Authenticator Code" placeholder="000 000" :disabled="isLoading" required />
        <CoarNote v-if="error" variant="error" class="mt-3">{{ error }}</CoarNote>
        <CoarButton type="submit" variant="primary" :loading="isLoading" :disabled="!mfaCode" class="mt-4 w-full">Verify</CoarButton>
        <button type="button" class="back-link mt-3" @click="step = mfaMethods.length > 1 ? 'mfa-choice' : 'credentials'">Back</button>
      </form>

      <!-- Step: Email OTP -->
      <form v-else-if="step === 'email-otp'" @submit.prevent="onEmailOtpLogin">
        <CoarTextInput v-model="mfaCode" label="Email Code" placeholder="000000" :disabled="isLoading" required />
        <CoarNote v-if="error" variant="error" class="mt-3">{{ error }}</CoarNote>
        <CoarButton type="submit" variant="primary" :loading="isLoading" :disabled="!mfaCode" class="mt-4 w-full">Verify</CoarButton>
        <button type="button" class="back-link mt-3" @click="step = mfaMethods.length > 1 ? 'mfa-choice' : 'credentials'">Back</button>
      </form>

      <!-- Step: Magic Link -->
      <form v-else-if="step === 'magic-link'" @submit.prevent="onRequestMagicLink">
        <CoarTextInput v-model="magicLinkEmail" label="Email" type="email" placeholder="your@email.com" :disabled="isLoading" required />
        <CoarNote v-if="magicLinkSent" variant="success" class="mt-3">If this email is registered, a login link has been sent. Check your inbox.</CoarNote>
        <CoarNote v-if="error" variant="error" class="mt-3">{{ error }}</CoarNote>
        <CoarButton type="submit" variant="primary" :loading="isLoading" :disabled="!magicLinkEmail || magicLinkSent" class="mt-4 w-full">Send Login Link</CoarButton>
        <button type="button" class="back-link mt-3" @click="step = 'credentials'; magicLinkSent = false">Back to login</button>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { CoarTextInput, CoarButton, CoarNote } from '@cocoar/vue-ui';
import { useAuthStore } from '@/stores/auth.store';

const router = useRouter();
const auth = useAuthStore();

type Step = 'credentials' | 'mfa-choice' | 'totp' | 'email-otp' | 'magic-link';

const step = ref<Step>('credentials');
const loginUserName = ref('');
const password = ref('');
const rememberMe = ref(false);
const mfaCode = ref('');
const mfaMethods = ref<string[]>([]);
const magicLinkEmail = ref('');
const magicLinkSent = ref(false);
const isLoading = ref(false);
const passkeyLoading = ref(false);
const error = ref('');

async function onLogin() {
  error.value = '';
  isLoading.value = true;

  try {
    const result = await auth.login(loginUserName.value, password.value, rememberMe.value);

    if (result.requiresMfa) {
      mfaMethods.value = result.mfaMethods ?? [];
      mfaCode.value = '';

      if (mfaMethods.value.length === 1) {
        if (mfaMethods.value[0] === 'totp') step.value = 'totp';
        else if (mfaMethods.value[0] === 'email') await onRequestEmailOtp();
      } else {
        step.value = 'mfa-choice';
      }
    } else {
      router.push('/admin');
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Login failed';
  } finally {
    isLoading.value = false;
  }
}

async function onMfaLogin() {
  error.value = '';
  isLoading.value = true;

  try {
    await auth.mfaLogin(mfaCode.value, rememberMe.value);
    router.push('/admin');
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Invalid code';
  } finally {
    isLoading.value = false;
  }
}

async function onRequestEmailOtp() {
  error.value = '';
  isLoading.value = true;
  try {
    await auth.requestEmailOtp();
    step.value = 'email-otp';
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to send code';
  } finally {
    isLoading.value = false;
  }
}

async function onEmailOtpLogin() {
  error.value = '';
  isLoading.value = true;
  try {
    await auth.emailOtpLogin(mfaCode.value, rememberMe.value);
    router.push('/admin');
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Invalid code';
  } finally {
    isLoading.value = false;
  }
}

async function onRequestMagicLink() {
  error.value = '';
  isLoading.value = true;
  try {
    await auth.requestMagicLink(magicLinkEmail.value);
    magicLinkSent.value = true;
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to send link';
  } finally {
    isLoading.value = false;
  }
}

async function onPasskeyLogin() {
  error.value = '';
  passkeyLoading.value = true;
  try {
    // 1. Get assertion options from server
    const optionsRes = await fetch('/_api/auth/passkey/login-options', { method: 'POST', credentials: 'include' });
    const options = await optionsRes.json();

    // 2. Convert challenge + allowCredentials from base64
    options.challenge = base64UrlToBuffer(options.challenge);
    if (options.allowCredentials) {
      options.allowCredentials = options.allowCredentials.map((c: any) => ({
        ...c,
        id: base64UrlToBuffer(c.id),
      }));
    }

    // 3. Browser WebAuthn API
    const credential = await navigator.credentials.get({ publicKey: options }) as PublicKeyCredential;
    const assertionResponse = credential.response as AuthenticatorAssertionResponse;

    // 4. Send assertion to server
    const body = {
      id: bufferToBase64Url(credential.rawId),
      rawId: bufferToBase64Url(credential.rawId),
      type: credential.type,
      response: {
        authenticatorData: bufferToBase64Url(assertionResponse.authenticatorData),
        clientDataJSON: bufferToBase64Url(assertionResponse.clientDataJSON),
        signature: bufferToBase64Url(assertionResponse.signature),
        userHandle: assertionResponse.userHandle ? bufferToBase64Url(assertionResponse.userHandle) : null,
      },
    };

    const loginRes = await fetch('/_api/auth/passkey/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(body),
    });

    if (!loginRes.ok) {
      const data = await loginRes.json();
      throw new Error(data.error ?? 'Passkey login failed');
    }

    await auth.checkSession();
    router.push('/admin');
  } catch (e: any) {
    if (e.name !== 'NotAllowedError') {
      error.value = e.message ?? 'Passkey login failed';
    }
  } finally {
    passkeyLoading.value = false;
  }
}

function base64UrlToBuffer(base64url: string): ArrayBuffer {
  const base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
  const pad = base64.length % 4 === 0 ? '' : '='.repeat(4 - (base64.length % 4));
  const binary = atob(base64 + pad);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes.buffer;
}

function bufferToBase64Url(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
</script>

<style scoped>
.login-container {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: var(--coar-background-neutral-secondary);
}

.login-card {
  width: 100%;
  max-width: 400px;
  padding: 40px;
  background: var(--coar-background-neutral-primary);
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 12px;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.06);
}

.login-header {
  text-align: center;
  margin-bottom: 32px;
}

.login-header h1 {
  font-size: 1.5rem;
  font-weight: 600;
  color: var(--coar-text-neutral-primary);
  margin: 0 0 8px;
}

.login-header p {
  font-size: 0.9rem;
  color: var(--coar-text-neutral-secondary);
  margin: 0;
}

.remember-me {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.85rem;
  color: var(--coar-text-neutral-secondary);
  cursor: pointer;
}

.divider {
  display: flex;
  align-items: center;
  gap: 12px;
  color: var(--coar-text-neutral-tertiary);
  font-size: 0.8rem;
}

.divider::before,
.divider::after {
  content: '';
  flex: 1;
  height: 1px;
  background: var(--coar-border-neutral-tertiary);
}

.mfa-choices {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mfa-choice {
  padding: 16px;
  border: 1px solid var(--coar-border-neutral-tertiary);
  border-radius: 8px;
  background: var(--coar-background-neutral-primary);
  color: var(--coar-text-neutral-primary);
  font-size: 0.95rem;
  cursor: pointer;
  text-align: left;
  transition: border-color 0.15s;
}

.mfa-choice:hover {
  border-color: var(--coar-border-neutral-primary);
}

.text-link {
  display: block;
  width: 100%;
  background: none;
  border: none;
  color: var(--coar-text-accent-primary);
  font-size: 0.85rem;
  cursor: pointer;
  text-align: center;
}

.text-link:hover {
  text-decoration: underline;
}

.back-link {
  display: block;
  width: 100%;
  background: none;
  border: none;
  color: var(--coar-text-neutral-secondary);
  font-size: 0.85rem;
  cursor: pointer;
  text-align: center;
}

.back-link:hover {
  color: var(--coar-text-neutral-primary);
}

.mt-2 { margin-top: 8px; }
.mt-3 { margin-top: 12px; }
.mt-4 { margin-top: 16px; }
.w-full { width: 100%; }
</style>

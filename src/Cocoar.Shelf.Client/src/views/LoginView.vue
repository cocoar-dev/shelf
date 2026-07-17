<template>
  <div class="login-container">
    <div class="login-card">
      <div class="login-header">
        <h1>Shelf Admin</h1>
        <p v-if="step === 'email'">Sign in with your email</p>
        <p v-else>Enter the code we sent you</p>
      </div>

      <!-- Step: Email -->
      <form v-if="step === 'email'" @submit.prevent="onRequestCode">
        <CoarTextInput
          v-model="email"
          label="Email"
          type="email"
          placeholder="your@email.com"
          :disabled="isLoading"
          required
        />

        <CoarNote v-if="error" variant="error" class="mt-3">{{ error }}</CoarNote>

        <CoarButton
          type="submit"
          variant="primary"
          :loading="isLoading"
          :disabled="!email"
          class="mt-4 w-full"
        >
          Send Login Code
        </CoarButton>
      </form>

      <!-- Step: Code -->
      <form v-else @submit.prevent="onVerifyCode">
        <CoarNote variant="info" class="mb-3">
          If <strong>{{ email }}</strong> is registered, a 6-digit code is on its way.
        </CoarNote>

        <CoarTextInput
          v-model="code"
          label="Login Code"
          placeholder="000000"
          inputmode="numeric"
          autocomplete="one-time-code"
          :disabled="isLoading"
          required
        />

        <CoarNote v-if="error" variant="error" class="mt-3">{{ error }}</CoarNote>

        <CoarButton
          type="submit"
          variant="primary"
          :loading="isLoading"
          :disabled="!code"
          class="mt-4 w-full"
        >
          Sign In
        </CoarButton>

        <button type="button" class="back-link mt-3" :disabled="isLoading" @click="onBack">
          Use a different email
        </button>
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

type Step = 'email' | 'code';

const step = ref<Step>('email');
const email = ref('');
const code = ref('');
const isLoading = ref(false);
const error = ref('');

async function onRequestCode() {
  error.value = '';
  isLoading.value = true;
  try {
    await auth.requestLoginCode(email.value.trim());
    code.value = '';
    step.value = 'code';
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to send code';
  } finally {
    isLoading.value = false;
  }
}

async function onVerifyCode() {
  error.value = '';
  isLoading.value = true;
  try {
    await auth.verifyLoginCode(email.value.trim(), code.value.trim());
    router.push('/admin');
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Invalid or expired code';
  } finally {
    isLoading.value = false;
  }
}

function onBack() {
  step.value = 'email';
  code.value = '';
  error.value = '';
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

.mb-3 { margin-bottom: 12px; }
.mt-3 { margin-top: 12px; }
.mt-4 { margin-top: 16px; }
.w-full { width: 100%; }
</style>

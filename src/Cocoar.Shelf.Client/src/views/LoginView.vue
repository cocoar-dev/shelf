<template>
  <div class="login-container">
    <div class="login-card">
      <div class="login-header">
        <h1>Shelf Admin</h1>
        <p>Enter your API key to continue</p>
      </div>

      <form @submit.prevent="onLogin">
        <CoarTextInput
          v-model="apiKey"
          label="API Key"
          type="password"
          placeholder="Enter API key"
          :disabled="isLoading"
          required
        />

        <CoarNote v-if="error" variant="error" class="mt-3">{{ error }}</CoarNote>

        <CoarButton
          type="submit"
          variant="primary"
          :loading="isLoading"
          :disabled="!apiKey"
          class="mt-4 w-full"
        >
          Sign In
        </CoarButton>
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

const apiKey = ref('');
const isLoading = ref(false);
const error = ref('');

async function onLogin() {
  error.value = '';
  isLoading.value = true;

  try {
    const success = await auth.login(apiKey.value);
    if (success) {
      router.push('/admin');
    } else {
      error.value = 'Invalid API key';
    }
  } catch {
    error.value = 'Connection failed';
  } finally {
    isLoading.value = false;
  }
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

.mt-3 { margin-top: 12px; }
.mt-4 { margin-top: 16px; }
.w-full { width: 100%; }
</style>

<template>
  <div class="login-container">
    <div class="login-card">
      <div class="login-header">
        <h1>Reset Password</h1>
        <p>Enter your username or email</p>
      </div>

      <form @submit.prevent="onSubmit">
        <CoarTextInput
          v-model="userNameOrEmail"
          label="Username or Email"
          placeholder="admin or admin@example.com"
          :disabled="isLoading || sent"
          required
        />

        <CoarNote v-if="sent" variant="success" class="mt-3">
          If the account exists, a reset link has been sent.
        </CoarNote>
        <CoarNote v-if="error" variant="error" class="mt-3">{{ error }}</CoarNote>

        <CoarButton
          type="submit"
          variant="primary"
          :loading="isLoading"
          :disabled="!userNameOrEmail || sent"
          class="mt-4 w-full"
        >
          Send Reset Link
        </CoarButton>

        <button type="button" class="back-link mt-3" @click="$router.push('/login')">
          Back to login
        </button>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { CoarTextInput, CoarButton, CoarNote } from '@cocoar/vue-ui';
import { http } from '@/core/api/http';

const userNameOrEmail = ref('');
const isLoading = ref(false);
const sent = ref(false);
const error = ref('');

async function onSubmit() {
  error.value = '';
  isLoading.value = true;
  try {
    await http.post('/auth/forgot-password', { userNameOrEmail: userNameOrEmail.value });
    sent.value = true;
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to send reset link';
  } finally {
    isLoading.value = false;
  }
}
</script>

<style scoped>
.login-container { display: flex; align-items: center; justify-content: center; min-height: 100vh; background: var(--coar-background-neutral-secondary); }
.login-card { width: 100%; max-width: 400px; padding: 40px; background: var(--coar-background-neutral-primary); border: 1px solid var(--coar-border-neutral-tertiary); border-radius: 12px; box-shadow: 0 4px 24px rgba(0, 0, 0, 0.06); }
.login-header { text-align: center; margin-bottom: 32px; }
.login-header h1 { font-size: 1.5rem; font-weight: 600; color: var(--coar-text-neutral-primary); margin: 0 0 8px; }
.login-header p { font-size: 0.9rem; color: var(--coar-text-neutral-secondary); margin: 0; }
.back-link { display: block; width: 100%; background: none; border: none; color: var(--coar-text-neutral-secondary); font-size: 0.85rem; cursor: pointer; text-align: center; }
.back-link:hover { color: var(--coar-text-neutral-primary); }
.mt-3 { margin-top: 12px; }
.mt-4 { margin-top: 16px; }
.w-full { width: 100%; }
</style>

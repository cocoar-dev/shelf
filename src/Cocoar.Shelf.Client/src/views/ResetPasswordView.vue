<template>
  <div class="login-container">
    <div class="login-card">
      <div class="login-header">
        <h1>Set New Password</h1>
        <p>Choose a new password for your account</p>
      </div>

      <form v-if="!done" @submit.prevent="onSubmit">
        <CoarTextInput v-model="newPassword" label="New Password" type="password" placeholder="Min. 8 characters" :disabled="isLoading" required />
        <CoarTextInput v-model="confirmPassword" label="Confirm Password" type="password" placeholder="Repeat password" :disabled="isLoading" class="mt-3" required />
        <CoarNote v-if="error" variant="error" class="mt-3">{{ error }}</CoarNote>
        <CoarButton type="submit" variant="primary" :loading="isLoading" :disabled="!isValid" class="mt-4 w-full">Reset Password</CoarButton>
      </form>

      <div v-else>
        <CoarNote variant="success">Your password has been reset. You can now sign in.</CoarNote>
        <CoarButton variant="primary" class="mt-4 w-full" @click="$router.push('/login')">Go to Login</CoarButton>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { useRoute } from 'vue-router';
import { CoarTextInput, CoarButton, CoarNote } from '@cocoar/vue-ui';
import { http } from '@/core/api/http';

const route = useRoute();
const newPassword = ref('');
const confirmPassword = ref('');
const isLoading = ref(false);
const error = ref('');
const done = ref(false);

const isValid = computed(() => newPassword.value.length >= 8 && newPassword.value === confirmPassword.value);

async function onSubmit() {
  error.value = '';
  if (newPassword.value !== confirmPassword.value) { error.value = 'Passwords do not match'; return; }
  isLoading.value = true;
  try {
    await http.post('/auth/reset-password', {
      userId: route.query.userId,
      token: decodeURIComponent(route.query.token as string),
      newPassword: newPassword.value,
    });
    done.value = true;
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Reset failed';
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
.mt-3 { margin-top: 12px; }
.mt-4 { margin-top: 16px; }
.w-full { width: 100%; }
</style>

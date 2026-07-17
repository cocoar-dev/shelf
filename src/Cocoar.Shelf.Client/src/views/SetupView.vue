<template>
  <div class="login-container">
    <div class="login-card">
      <div class="login-header">
        <h1>Shelf Setup</h1>
        <p>Create your admin account to get started</p>
      </div>

      <form @submit.prevent="onSetup">
        <CoarTextInput
          v-model="form.userName"
          label="Username"
          placeholder="admin"
          :disabled="isLoading"
          required
        />

        <CoarTextInput
          v-model="form.displayName"
          label="Display Name"
          placeholder="Admin"
          :disabled="isLoading"
          class="mt-3"
        />

        <CoarTextInput
          v-model="form.email"
          label="Email"
          type="email"
          placeholder="admin@example.com"
          :disabled="isLoading"
          class="mt-3"
        />

        <CoarTextInput
          v-model="form.password"
          label="Password"
          type="password"
          placeholder="Min. 8 characters"
          :disabled="isLoading"
          class="mt-3"
          required
        />

        <CoarTextInput
          v-model="confirmPassword"
          label="Confirm Password"
          type="password"
          placeholder="Repeat password"
          :disabled="isLoading"
          class="mt-3"
          required
        />

        <CoarNote v-if="error" variant="error" class="mt-3">{{ error }}</CoarNote>

        <CoarButton
          type="submit"
          variant="primary"
          :loading="isLoading"
          :disabled="!isValid"
          class="mt-4 w-full"
        >
          Create Admin Account
        </CoarButton>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { CoarTextInput, CoarButton, CoarNote } from '@cocoar/vue-ui';
import { useAuthStore } from '@/stores/auth.store';

const router = useRouter();
const auth = useAuthStore();

const form = ref({
  userName: '',
  password: '',
  displayName: '',
  email: '',
});

const confirmPassword = ref('');
const isLoading = ref(false);
const error = ref('');

const isValid = computed(() =>
  form.value.userName.length > 0 &&
  form.value.password.length >= 8 &&
  form.value.password === confirmPassword.value
);

onMounted(async () => {
  try {
    const status = await auth.fetchSetupStatus();
    if (!status.needsSetup) {
      router.replace('/login');
    }
  } catch {
    // Setup endpoint not available (no DB), redirect to login
    router.replace('/login');
  }
});

async function onSetup() {
  error.value = '';

  if (form.value.password !== confirmPassword.value) {
    error.value = 'Passwords do not match';
    return;
  }

  isLoading.value = true;

  try {
    await auth.createAdmin({
      userName: form.value.userName,
      password: form.value.password,
      displayName: form.value.displayName || undefined,
      email: form.value.email || undefined,
    });
    router.push('/admin');
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Setup failed';
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
  max-width: 440px;
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

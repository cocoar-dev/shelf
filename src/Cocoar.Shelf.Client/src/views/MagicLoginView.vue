<template>
  <div class="login-container">
    <div class="login-card">
      <div class="login-header">
        <h1>Shelf</h1>
        <p v-if="isLoading">Signing you in...</p>
        <p v-else-if="error">{{ error }}</p>
        <p v-else>Redirecting...</p>
      </div>

      <CoarButton
        v-if="error"
        variant="primary"
        class="w-full"
        @click="$router.push('/login')"
      >
        Go to Login
      </CoarButton>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { CoarButton } from '@cocoar/vue-ui';
import { useAuthStore } from '@/stores/auth.store';

const router = useRouter();
const route = useRoute();
const auth = useAuthStore();

const isLoading = ref(true);
const error = ref('');

onMounted(async () => {
  const userId = route.query.userId as string;
  const token = route.query.token as string;

  if (!userId || !token) {
    error.value = 'Invalid magic link';
    isLoading.value = false;
    return;
  }

  try {
    await auth.magicLinkLogin(userId, token);
    router.push('/admin');
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Invalid or expired link';
  } finally {
    isLoading.value = false;
  }
});
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
  text-align: center;
}

.login-header {
  margin-bottom: 24px;
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

.w-full { width: 100%; }
</style>

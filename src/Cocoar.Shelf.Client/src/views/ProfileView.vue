<template>
  <div class="p-6 flex flex-col gap-6">
    <!-- Account Info -->
    <CoarCard title="Account">
      <div class="info-grid">
        <div class="info-row"><span class="info-label">Display Name</span><span>{{ user?.displayName ?? '—' }}</span></div>
        <div class="info-row"><span class="info-label">Email</span><span>{{ user?.email ?? '—' }}</span></div>
        <div class="info-row"><span class="info-label">Role</span><span>{{ user?.isAdmin ? 'Administrator' : 'User' }}</span></div>
      </div>
    </CoarCard>

    <!-- Security -->
    <CoarCard title="Sign-in & Security">
      <p class="security-desc">
        Sign-in and security (login codes, passkeys, two-factor) are managed centrally by the
        Cocoar account service. Changes made there apply to all connected apps.
      </p>
    </CoarCard>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { CoarCard } from '@cocoar/vue-ui';
import { useUI } from '@/composables/useUI';
import { useAuthStore } from '@/stores/auth.store';

const ui = useUI();
const auth = useAuthStore();

ui.set(ctx => {
  ctx.header.title = 'Profile';
  ctx.header.subTitle = 'Account';
  ctx.header.icon = 'circle-user';
});

const user = computed(() => auth.user);
</script>

<style scoped>
.info-grid {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.info-row {
  display: flex;
  gap: 16px;
  font-size: 0.9rem;
}

.info-label {
  min-width: 120px;
  color: var(--coar-text-neutral-secondary);
}

.security-desc {
  font-size: 0.85rem;
  color: var(--coar-text-neutral-secondary);
  margin: 0;
}

.p-6 { padding: 24px; }
.gap-6 { gap: 24px; }
</style>

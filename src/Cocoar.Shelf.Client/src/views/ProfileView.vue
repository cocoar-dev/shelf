<template>
  <div class="p-6 flex flex-col gap-6">
    <!-- Account Info -->
    <CoarCard title="Account">
      <div class="info-grid">
        <div class="info-row"><span class="info-label">Username</span><span>{{ user?.userName }}</span></div>
        <div class="info-row"><span class="info-label">Display Name</span><span>{{ user?.displayName ?? '—' }}</span></div>
        <div class="info-row"><span class="info-label">Email</span><span>{{ user?.email ?? '—' }}</span></div>
      </div>
    </CoarCard>

    <!-- Security -->
    <CoarCard title="Security">
      <div class="security-grid">
        <!-- Change Password -->
        <div class="security-card">
          <div class="security-header">
            <strong>Password</strong>
          </div>
          <p class="security-desc">Change your account password</p>
          <CoarButton size="s" variant="secondary" @click="showChangePassword = true">Change Password</CoarButton>
        </div>

        <!-- TOTP -->
        <div class="security-card">
          <div class="security-header">
            <strong>Authenticator App (TOTP)</strong>
            <CoarTag :variant="mfaEnabled ? 'success' : 'neutral'" size="s">{{ mfaEnabled ? 'Enabled' : 'Disabled' }}</CoarTag>
          </div>
          <p class="security-desc">Use an authenticator app for two-factor authentication</p>
          <CoarButton v-if="!mfaEnabled" size="s" variant="secondary" @click="showMfaSetup = true">Enable</CoarButton>
          <CoarButton v-else size="s" variant="ghost" @click="onDisableTotp">Disable</CoarButton>
        </div>

        <!-- Email OTP -->
        <div class="security-card">
          <div class="security-header">
            <strong>Email OTP</strong>
            <CoarTag :variant="emailOtpEnabled ? 'success' : 'neutral'" size="s">{{ emailOtpEnabled ? 'Enabled' : 'Disabled' }}</CoarTag>
          </div>
          <p class="security-desc">Receive a login code via email</p>
          <CoarButton v-if="!emailOtpEnabled" size="s" variant="secondary" :disabled="!user?.email" @click="onEnableEmailOtp">Enable</CoarButton>
          <CoarButton v-else size="s" variant="ghost" @click="onDisableEmailOtp">Disable</CoarButton>
        </div>

        <!-- Passkeys -->
        <div class="security-card">
          <div class="security-header">
            <strong>Passkeys</strong>
            <CoarTag variant="neutral" size="s">{{ passkeys.length }} registered</CoarTag>
          </div>
          <p class="security-desc">Sign in with biometrics or security keys</p>
          <div v-if="passkeys.length > 0" class="passkey-list">
            <div v-for="pk in passkeys" :key="pk.id" class="passkey-item">
              <span>{{ pk.displayName }}</span>
              <button class="delete-btn" @click="onDeletePasskey(pk.id)">Remove</button>
            </div>
          </div>
          <CoarButton size="s" variant="secondary" :loading="passkeyRegistering" @click="onRegisterPasskey">Register Passkey</CoarButton>
        </div>
      </div>
    </CoarCard>

    <!-- Change Password Modal -->
    <Teleport to="body">
      <div v-if="showChangePassword" class="modal-overlay" @click.self="showChangePassword = false">
        <div class="modal-box">
          <h3>Change Password</h3>
          <form @submit.prevent="onChangePassword">
            <CoarTextInput v-model="currentPassword" label="Current Password" type="password" required />
            <CoarTextInput v-model="newPassword" label="New Password" type="password" class="mt-3" required />
            <CoarTextInput v-model="confirmNewPassword" label="Confirm New Password" type="password" class="mt-3" required />
            <CoarNote v-if="passwordError" variant="error" class="mt-3">{{ passwordError }}</CoarNote>
            <CoarNote v-if="passwordSuccess" variant="success" class="mt-3">Password changed successfully.</CoarNote>
            <div class="modal-actions mt-4">
              <CoarButton variant="ghost" size="s" @click="showChangePassword = false">Cancel</CoarButton>
              <CoarButton type="submit" variant="primary" size="s" :loading="passwordLoading" :disabled="!newPassword || newPassword !== confirmNewPassword">Save</CoarButton>
            </div>
          </form>
        </div>
      </div>
    </Teleport>

    <!-- MFA Setup Modal -->
    <Teleport to="body">
      <div v-if="showMfaSetup" class="modal-overlay" @click.self="showMfaSetup = false">
        <div class="modal-box">
          <h3>Setup Authenticator</h3>
          <div v-if="mfaSetupData">
            <p class="security-desc">Scan this QR code with your authenticator app:</p>
            <div class="qr-container">
              <img :src="`https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(mfaSetupData.authenticatorUri)}`" alt="QR Code" width="200" height="200" />
            </div>
            <p class="security-desc mt-3">Or enter this key manually:</p>
            <code class="shared-key">{{ mfaSetupData.sharedKey }}</code>
            <form @submit.prevent="onVerifyTotp" class="mt-4">
              <CoarTextInput v-model="totpVerifyCode" label="Verification Code" placeholder="000 000" required />
              <CoarNote v-if="mfaError" variant="error" class="mt-3">{{ mfaError }}</CoarNote>
              <div class="modal-actions mt-4">
                <CoarButton variant="ghost" size="s" @click="showMfaSetup = false">Cancel</CoarButton>
                <CoarButton type="submit" variant="primary" size="s" :loading="mfaLoading" :disabled="!totpVerifyCode">Verify & Enable</CoarButton>
              </div>
            </form>
          </div>
          <div v-else class="center-content"><CoarSpinner size="m" /></div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { CoarCard, CoarButton, CoarTag, CoarTextInput, CoarNote, CoarSpinner } from '@cocoar/vue-ui';
import { useUI } from '@/composables/useUI';
import { useAuthStore } from '@/stores/auth.store';
import { http } from '@/core/api/http';

const ui = useUI();
const auth = useAuthStore();

ui.set(ctx => {
  ctx.header.title = 'Profile';
  ctx.header.subTitle = 'Account & Security';
  ctx.header.icon = 'circle-user';
});

const user = ref(auth.user);
const mfaEnabled = ref(false);
const emailOtpEnabled = ref(false);
const passkeys = ref<{ id: string; displayName: string; createdAt: string }[]>([]);

// Change password
const showChangePassword = ref(false);
const currentPassword = ref('');
const newPassword = ref('');
const confirmNewPassword = ref('');
const passwordLoading = ref(false);
const passwordError = ref('');
const passwordSuccess = ref(false);

// MFA setup
const showMfaSetup = ref(false);
const mfaSetupData = ref<{ sharedKey: string; authenticatorUri: string } | null>(null);
const totpVerifyCode = ref('');
const mfaLoading = ref(false);
const mfaError = ref('');

// Passkeys
const passkeyRegistering = ref(false);

async function loadSecurityStatus() {
  const [mfa, otp, pks] = await Promise.all([
    http.get<{ enabled: boolean }>('/auth/mfa/status'),
    http.get<{ enabled: boolean; hasEmail: boolean }>('/auth/email-otp/status'),
    http.get<{ id: string; displayName: string; createdAt: string }[]>('/auth/passkey'),
  ]);
  mfaEnabled.value = mfa.enabled;
  emailOtpEnabled.value = otp.enabled;
  passkeys.value = pks;
}

async function onChangePassword() {
  passwordError.value = '';
  passwordSuccess.value = false;
  passwordLoading.value = true;
  try {
    await http.post('/auth/change-password', { currentPassword: currentPassword.value, newPassword: newPassword.value });
    passwordSuccess.value = true;
    currentPassword.value = '';
    newPassword.value = '';
    confirmNewPassword.value = '';
    setTimeout(() => { showChangePassword.value = false; passwordSuccess.value = false; }, 1500);
  } catch (e) {
    passwordError.value = e instanceof Error ? e.message : 'Failed';
  } finally {
    passwordLoading.value = false;
  }
}

watch(showMfaSetup, async (show) => {
  if (show) {
    mfaSetupData.value = null;
    totpVerifyCode.value = '';
    mfaError.value = '';
    mfaSetupData.value = await http.post<{ sharedKey: string; authenticatorUri: string }>('/auth/mfa/setup');
  }
});

async function onVerifyTotp() {
  mfaError.value = '';
  mfaLoading.value = true;
  try {
    await http.post('/auth/mfa/verify', { code: totpVerifyCode.value });
    mfaEnabled.value = true;
    showMfaSetup.value = false;
  } catch (e) {
    mfaError.value = e instanceof Error ? e.message : 'Invalid code';
  } finally {
    mfaLoading.value = false;
  }
}

async function onDisableTotp() {
  if (!confirm('Disable TOTP?')) return;
  await http.post('/auth/mfa/disable');
  mfaEnabled.value = false;
}

async function onEnableEmailOtp() {
  await http.post('/auth/email-otp/enable');
  emailOtpEnabled.value = true;
}

async function onDisableEmailOtp() {
  if (!confirm('Disable Email OTP?')) return;
  await http.post('/auth/email-otp/disable');
  emailOtpEnabled.value = false;
}

async function onRegisterPasskey() {
  passkeyRegistering.value = true;
  try {
    const optionsRes = await fetch('/_api/auth/passkey/register-options', { method: 'POST', credentials: 'include' });
    const options = await optionsRes.json();

    options.challenge = base64UrlToBuffer(options.challenge);
    options.user.id = base64UrlToBuffer(options.user.id);
    if (options.excludeCredentials) {
      options.excludeCredentials = options.excludeCredentials.map((c: any) => ({ ...c, id: base64UrlToBuffer(c.id) }));
    }

    const credential = await navigator.credentials.create({ publicKey: options }) as PublicKeyCredential;
    const attestation = credential.response as AuthenticatorAttestationResponse;

    const body = {
      id: bufferToBase64Url(credential.rawId),
      rawId: bufferToBase64Url(credential.rawId),
      type: credential.type,
      response: {
        attestationObject: bufferToBase64Url(attestation.attestationObject),
        clientDataJSON: bufferToBase64Url(attestation.clientDataJSON),
      },
    };

    await fetch('/_api/auth/passkey/register', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(body),
    });

    await loadSecurityStatus();
  } catch (e: any) {
    if (e.name !== 'NotAllowedError') alert(e.message ?? 'Registration failed');
  } finally {
    passkeyRegistering.value = false;
  }
}

async function onDeletePasskey(id: string) {
  if (!confirm('Remove this passkey?')) return;
  await http.delete(`/auth/passkey/${id}`);
  await loadSecurityStatus();
}

function base64UrlToBuffer(b64: string): ArrayBuffer {
  const base64 = b64.replace(/-/g, '+').replace(/_/g, '/');
  const pad = base64.length % 4 === 0 ? '' : '='.repeat(4 - (base64.length % 4));
  const bin = atob(base64 + pad);
  const bytes = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
  return bytes.buffer;
}

function bufferToBase64Url(buf: ArrayBuffer): string {
  const bytes = new Uint8Array(buf);
  let bin = '';
  for (const b of bytes) bin += String.fromCharCode(b);
  return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

onMounted(loadSecurityStatus);
</script>

<style scoped>
.info-grid { display: flex; flex-direction: column; gap: 8px; }
.info-row { display: flex; gap: 16px; }
.info-label { font-weight: 500; color: var(--coar-text-neutral-secondary); min-width: 120px; }

.security-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 16px; }
.security-card { padding: 20px; border: 1px solid var(--coar-border-neutral-tertiary); border-radius: 8px; background: var(--coar-background-neutral-primary); }
.security-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; }
.security-desc { font-size: 0.85rem; color: var(--coar-text-neutral-secondary); margin: 0 0 12px; }

.passkey-list { display: flex; flex-direction: column; gap: 4px; margin-bottom: 12px; }
.passkey-item { display: flex; justify-content: space-between; align-items: center; padding: 8px; border-radius: 4px; background: var(--coar-background-neutral-secondary); font-size: 0.85rem; }
.delete-btn { background: none; border: none; color: var(--coar-text-semantic-error); font-size: 0.8rem; cursor: pointer; }

.modal-overlay { position: fixed; inset: 0; z-index: 1000; display: flex; align-items: center; justify-content: center; background: rgba(0,0,0,0.4); }
.modal-box { background: var(--coar-background-neutral-primary); border-radius: 12px; padding: 24px; width: 100%; max-width: 420px; box-shadow: 0 24px 48px -12px rgba(0,0,0,0.18); }
.modal-box h3 { margin: 0 0 16px; font-size: 1.125rem; font-weight: 600; }
.modal-actions { display: flex; justify-content: flex-end; gap: 8px; }

.qr-container { display: flex; justify-content: center; margin: 16px 0; }
.shared-key { display: block; padding: 8px 12px; background: var(--coar-background-neutral-secondary); border-radius: 4px; font-family: monospace; font-size: 0.85rem; word-break: break-all; user-select: all; }

.center-content { display: flex; justify-content: center; padding: 32px; }
.mt-3 { margin-top: 12px; }
.mt-4 { margin-top: 16px; }
.p-6 { padding: 24px; }
</style>

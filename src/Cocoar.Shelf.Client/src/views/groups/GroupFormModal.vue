<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import {
  CoarTextInput, CoarSelect, CoarCheckbox, CoarNote, CoarButton,
  CoarFormField, CoarTabGroup, CoarTab, CoarTable,
} from '@cocoar/vue-ui';
import { CoarScriptEditor } from '@cocoar/vue-script-editor';
import ModalLayout from '@/components/ModalLayout.vue';
import { useGroupsStore } from '@/stores/groups.store';
import { shelfApi } from '@/core/api/shelf-api';
import { ApiError } from '@/core/api/http';
import type { MembershipMode, GroupMember } from '@/core/models/shelf.models';

const props = defineProps<{
  id: string
  close: (result?: unknown) => void
}>();

const groupsStore = useGroupsStore();
const isCreate = computed(() => props.id === 'create');
const loading = ref(false);
const saving = ref(false);
const error = ref('');
const activeTab = ref('general');

const modeOptions = [
  { value: 'Manual', label: 'Manual — explicit members only' },
  { value: 'Auto', label: 'Auto — computed from a script' },
];

const form = ref({
  name: '',
  description: '',
  membershipMode: 'Manual' as MembershipMode,
  memberEmails: [] as string[],
  membershipScript: '',
  isAdminGroup: false,
});

const emailInput = ref('');
const autoMembers = ref<GroupMember[]>([]);
const lastError = ref<string | null>(null);

// Dry-run
const testEmail = ref('');
const testing = ref(false);
const testResult = ref<{ matched: boolean; error: string | null; email: string | null } | null>(null);

const isAuto = computed(() => form.value.membershipMode === 'Auto');

// Hidden type context so the Monaco editor gives IntelliSense on `user` without diagnostics noise.
const scriptPreamble =
  'declare const user: { email: string; permissions: string[]; claims: Record<string, string | string[]> };';

const modalTitle = computed(() => (isCreate.value ? 'New Group' : form.value.name || props.id));

const footerButton = computed(() => ({
  visible: true,
  text: isCreate.value ? 'Create' : 'Save',
  disabled: !form.value.name.trim() || (isAuto.value && !form.value.membershipScript.trim()) || saving.value,
  loading: saving.value,
  onClick: save,
}));

async function loadGroup() {
  const group = await shelfApi.getGroup(props.id);
  form.value = {
    name: group.name,
    description: group.description ?? '',
    membershipMode: group.membershipMode,
    memberEmails: [...group.memberEmails],
    membershipScript: group.membershipScript ?? '',
    isAdminGroup: group.isAdminGroup,
  };
  autoMembers.value = group.autoMembers;
  lastError.value = group.membershipLastError;
}

onMounted(async () => {
  if (isCreate.value) return;
  loading.value = true;
  try {
    await loadGroup();
  } catch {
    error.value = 'Failed to load group';
  } finally {
    loading.value = false;
  }
});

function addEmail() {
  const email = emailInput.value.trim();
  if (email && !form.value.memberEmails.includes(email)) form.value.memberEmails.push(email);
  emailInput.value = '';
}

function removeEmail(email: string) {
  form.value.memberEmails = form.value.memberEmails.filter(e => e !== email);
}

async function runTest() {
  if (!form.value.membershipScript.trim() || !testEmail.value.trim()) return;
  testing.value = true;
  testResult.value = null;
  try {
    const result = await shelfApi.testGroupScript({
      script: form.value.membershipScript,
      email: testEmail.value.trim(),
    });
    testResult.value = { matched: result.matched, error: result.error, email: result.user.email };
  } catch (err) {
    testResult.value = { matched: false, error: err instanceof ApiError ? err.message : 'Test failed', email: null };
  } finally {
    testing.value = false;
  }
}

async function save() {
  if (!form.value.name.trim()) return;
  error.value = '';
  saving.value = true;
  try {
    const payload = {
      name: form.value.name.trim(),
      description: form.value.description || undefined,
      membershipMode: form.value.membershipMode,
      memberEmails: form.value.memberEmails,
      membershipScript: isAuto.value ? form.value.membershipScript : undefined,
      isAdminGroup: form.value.isAdminGroup,
    };
    if (isCreate.value) {
      await groupsStore.create(payload);
    } else {
      await groupsStore.update(props.id, payload);
    }
    props.close();
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'Failed to save group';
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <ModalLayout
    :close="close"
    :title="modalTitle"
    :sub-title="isCreate ? 'Create a permission group' : `Editing ${props.id}`"
    icon="shield"
    width="46rem"
    :footer-button="footerButton"
  >
    <div v-if="!loading" class="tabs-host flex flex-col flex-1 min-h-0">
      <CoarNote v-if="error" variant="error" class="mb-3">{{ error }}</CoarNote>

      <CoarTabGroup v-model="activeTab">
        <CoarTab id="general">
          General
          <template #content>
            <form class="tab-panel flex flex-col gap-4" @submit.prevent="save">
              <div class="flex gap-4">
                <CoarFormField label="Name" required class="flex-1">
                  <CoarTextInput v-model="form.name" placeholder="engineering" clearable />
                </CoarFormField>
                <CoarFormField label="Membership" class="mode-select">
                  <CoarSelect v-model="form.membershipMode" :options="modeOptions" />
                </CoarFormField>
              </div>

              <CoarFormField label="Description">
                <CoarTextInput v-model="form.description" placeholder="What this group grants" :rows="2" />
              </CoarFormField>

              <CoarCheckbox
                v-model="form.isAdminGroup"
                label="Members are Shelf admins"
              />
              <p class="section-desc">
                Admin groups grant full access to the admin UI and product management — use sparingly.
              </p>
            </form>
          </template>
        </CoarTab>

        <CoarTab id="members">
          Members
          <template #content>
            <div class="tab-panel flex flex-col gap-4">
              <section>
                <div class="section-heading">Explicit members (by email)</div>
                <p class="section-desc">
                  Email-based, so grants can be staged before a user's first login.
                </p>
                <div class="tag-input-row">
                  <CoarTextInput
                    v-model="emailInput"
                    placeholder="user@cocoar.dev"
                    class="flex-1"
                    @keydown.enter.prevent="addEmail"
                  />
                  <CoarButton variant="secondary" size="s" @click="addEmail">Add</CoarButton>
                </div>
                <div v-if="form.memberEmails.length > 0" class="tag-chips">
                  <span v-for="email in form.memberEmails" :key="email" class="tag-chip">
                    {{ email }}
                    <button class="tag-chip-remove" type="button" aria-label="Remove" @click="removeEmail(email)">×</button>
                  </span>
                </div>
                <p v-else class="section-desc">No explicit members.</p>
              </section>

              <section v-if="isAuto && !isCreate">
                <div class="section-heading">Auto-matched members (read-only)</div>
                <CoarTable v-if="autoMembers.length > 0" variant="plain" hover>
                  <thead>
                    <tr><th>Name</th><th>Email</th></tr>
                  </thead>
                  <tbody>
                    <tr v-for="m in autoMembers" :key="m.id">
                      <td>{{ m.displayName }}</td>
                      <td>{{ m.email }}</td>
                    </tr>
                  </tbody>
                </CoarTable>
                <p v-else class="section-desc">No users currently match the script.</p>
              </section>
            </div>
          </template>
        </CoarTab>

        <CoarTab v-if="isAuto" id="auto">
          Auto-membership
          <template #content>
            <div class="tab-panel flex flex-col gap-4">
              <section>
                <div class="section-heading">Membership script</div>
                <p class="section-desc">
                  A JavaScript expression over <code>user</code> returning a boolean, e.g.
                  <code>user.email.endsWith('@cocoar.dev')</code> or
                  <code>user.permissions.includes('shelf:internal')</code>. Available:
                  <code>user.email</code>, <code>user.permissions</code>, <code>user.claims</code>.
                  Evaluated at each login and on save; a broken script simply matches no one.
                </p>
                <CoarScriptEditor
                  v-model="form.membershipScript"
                  language="typescript"
                  script-mode
                  variant="editor"
                  :preamble="scriptPreamble"
                  height="180px"
                  placeholder="user.email.endsWith('@cocoar.dev')"
                />
                <CoarNote v-if="lastError" variant="error" class="mt-2">
                  Last evaluation error: {{ lastError }}
                </CoarNote>
              </section>

              <section>
                <div class="section-heading">Test against a user</div>
                <div class="tag-input-row">
                  <CoarTextInput
                    v-model="testEmail"
                    placeholder="user@cocoar.dev"
                    class="flex-1"
                    @keydown.enter.prevent="runTest"
                  />
                  <CoarButton
                    variant="secondary"
                    size="s"
                    :disabled="!form.membershipScript.trim() || !testEmail.trim() || testing"
                    :loading="testing"
                    @click="runTest"
                  >
                    Test
                  </CoarButton>
                </div>
                <CoarNote v-if="testResult && testResult.error" variant="error" class="mt-2">
                  {{ testResult.error }}
                </CoarNote>
                <CoarNote v-else-if="testResult" :variant="testResult.matched ? 'success' : 'info'" class="mt-2">
                  {{ testResult.matched ? 'Matches' : 'Does not match' }} — {{ testResult.email }}
                </CoarNote>
              </section>
            </div>
          </template>
        </CoarTab>

      </CoarTabGroup>
    </div>
    <div v-else class="flex flex-1 items-center justify-center p-8">
      <span class="loading-text">Loading…</span>
    </div>
  </ModalLayout>
</template>

<style scoped>
.tabs-host :deep(.coar-tab-group) {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}

.tabs-host :deep(.coar-tab-content) {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}

.tab-panel {
  padding-top: 16px;
}

.mode-select {
  width: 18rem;
}

.section-heading {
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--coar-text-neutral-secondary);
  border-bottom: 1px solid var(--coar-border-neutral-tertiary);
  padding-bottom: 4px;
  margin-bottom: 10px;
}

.section-desc {
  font-size: 0.8rem;
  color: var(--coar-text-neutral-secondary);
  margin: 0 0 10px;
  line-height: 1.5;
}

.section-desc code {
  font-size: 0.75rem;
  background: var(--coar-background-neutral-secondary);
  padding: 1px 5px;
  border-radius: 4px;
}

.mt-2 { margin-top: 8px; }
.mb-3 { margin-bottom: 12px; }

.tag-input-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.tag-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 10px;
}

.tag-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 10px;
  border-radius: 10px;
  font-size: 0.8rem;
  font-weight: 500;
  background: var(--coar-background-accent-tertiary, #dbeafe);
  color: var(--coar-text-accent-primary, #1183CD);
}

.tag-chip-remove {
  border: none;
  background: none;
  cursor: pointer;
  color: inherit;
  padding: 0;
  font-size: 1rem;
  line-height: 1;
  opacity: 0.6;
}

.tag-chip-remove:hover { opacity: 1; }

.loading-text {
  color: var(--coar-text-neutral-tertiary);
}
</style>

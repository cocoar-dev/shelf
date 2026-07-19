<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import {
  CoarTextInput, CoarSelect, CoarCheckbox, CoarNote, CoarButton, CoarFormField,
  CoarTabGroup, CoarTab, CoarTable, CoarTag, CoarIcon, useDialog,
} from '@cocoar/vue-ui';
import ModalLayout from '@/components/ModalLayout.vue';
import { useProductsStore } from '@/stores/products.store';
import { shelfApi } from '@/core/api/shelf-api';
import { ApiError } from '@/core/api/http';
import { generateApiKey, copyToClipboard } from '@/core/api-key-utils';
import type { Principal, PrincipalRef, ProductOpenness } from '@/core/models/shelf.models';

const props = defineProps<{
  id: string
  close: (result?: unknown) => void
}>();

const productsStore = useProductsStore();
const dialog = useDialog();
const isCreate = computed(() => props.id === 'create');
const loading = ref(false);
const saving = ref(false);
const error = ref('');
const activeTab = ref('general');

const visibilityOptions = [
  { value: 'public', label: 'Public' },
  { value: 'preview', label: 'Preview' },
];

const opennessOptions = [
  { value: 'Unspecified', label: 'Unspecified' },
  { value: 'OpenSource', label: 'Open Source' },
  { value: 'Proprietary', label: 'Proprietary' },
];

const form = ref({
  name: '',
  displayName: '',
  description: '',
  source: 'upload',
  visibility: 'public',
  openness: 'Unspecified' as ProductOpenness,
  repositoryUrl: '',
  restricted: false,
  readPrincipals: [] as PrincipalRef[],
  tags: [] as string[],
  showWhenEmpty: false,
});

// Access: assignable principals (groups + users) for the read-grant picker.
const principals = ref<Principal[]>([]);
const principalEmail = ref('');

function principalKey(p: PrincipalRef | Principal): string {
  return `${p.kind}:${p.id}`;
}

const availablePrincipalOptions = computed(() => {
  const chosen = new Set(form.value.readPrincipals.map(principalKey));
  return principals.value
    .filter(p => !chosen.has(principalKey(p)))
    .map(p => ({ value: principalKey(p), label: `${p.displayName} · ${p.kind}` }));
});

function principalLabel(p: PrincipalRef): string {
  return principals.value.find(x => x.kind === p.kind && x.id === p.id)?.displayName ?? p.id;
}

function addPrincipalByKey(key: string | null | undefined) {
  if (!key) return;
  const match = principals.value.find(p => principalKey(p) === key);
  if (match && !form.value.readPrincipals.some(p => principalKey(p) === key)) {
    form.value.readPrincipals.push({ kind: match.kind, id: match.id });
  }
}

function addPrincipalEmail() {
  const email = principalEmail.value.trim();
  if (email && !form.value.readPrincipals.some(p => p.kind === 'User' && p.id.toLowerCase() === email.toLowerCase())) {
    form.value.readPrincipals.push({ kind: 'User', id: email });
  }
  principalEmail.value = '';
}

function removePrincipal(p: PrincipalRef) {
  form.value.readPrincipals = form.value.readPrincipals.filter(x => principalKey(x) !== principalKey(p));
}

const tagInput = ref('');
const hasApiKey = ref(false);
const newApiKey = ref('');
const removeApiKey = ref(false);
// Admins get the key revealed and edit it in place; non-admins fall back to write-only.
const keyRevealed = ref(false);
const keyCopied = ref(false);

// Versions tab state (edit mode only)
const versions = ref<string[]>([]);
const latest = ref<string | null>(null);
const uploadVersion = ref('');
const uploadFile = ref<File | null>(null);
const fileInput = ref<HTMLInputElement | null>(null);
const uploading = ref(false);
const deletingVersion = ref<string | null>(null);
const versionsMessage = ref('');
const versionsError = ref('');

const modalTitle = computed(() => {
  if (isCreate.value) return 'New Product';
  return form.value.displayName || props.id;
});

const footerButton = computed(() => ({
  visible: true,
  text: isCreate.value ? 'Create' : 'Save',
  disabled: !form.value.name.trim() || saving.value,
  loading: saving.value,
  onClick: save,
}));

async function loadProduct() {
  const product = await shelfApi.getProduct(props.id);
  form.value = {
    name: product.name,
    displayName: product.displayName ?? '',
    description: product.description ?? '',
    source: product.source,
    visibility: product.visibility,
    openness: product.openness ?? 'Unspecified',
    repositoryUrl: product.repositoryUrl ?? '',
    restricted: product.restricted ?? false,
    readPrincipals: [...(product.readPrincipals ?? [])],
    tags: [...(product.tags ?? [])],
    showWhenEmpty: product.showWhenEmpty ?? false,
  };
  hasApiKey.value = product.hasApiKey ?? false;
  versions.value = [...(product.versions ?? [])];
  latest.value = product.latest;

  try {
    const reveal = await shelfApi.getProductApiKey(props.id);
    newApiKey.value = reveal.apiKey ?? '';
    keyRevealed.value = true;
  } catch {
    keyRevealed.value = false; // not admin — keep the write-only flow
  }
}

onMounted(async () => {
  // Access picker options (groups + users). Best-effort — the modal still works without them.
  shelfApi.getPrincipals().then(p => { principals.value = p; }).catch(() => { /* ignore */ });

  if (isCreate.value) return;
  loading.value = true;
  try {
    await loadProduct();
  } catch {
    error.value = 'Failed to load product';
  } finally {
    loading.value = false;
  }
});

function addTag() {
  const tag = tagInput.value.trim();
  if (tag && !form.value.tags.includes(tag)) form.value.tags.push(tag);
  tagInput.value = '';
}

function removeTag(tag: string) {
  form.value.tags = form.value.tags.filter(t => t !== tag);
}

async function copyKey() {
  if (!newApiKey.value) return;
  keyCopied.value = await copyToClipboard(newApiKey.value);
  setTimeout(() => { keyCopied.value = false; }, 1500);
}

async function save() {
  if (!form.value.name.trim()) return;
  error.value = '';
  saving.value = true;
  try {
    // Revealed (admin): the field IS the key — empty removes it.
    // Write-only fallback: undefined = keep, '' = remove, value = set/replace.
    const apiKey = keyRevealed.value
      ? newApiKey.value.trim()
      : (removeApiKey.value ? '' : (newApiKey.value.trim() || undefined));
    const payload = {
      displayName: form.value.displayName || undefined,
      description: form.value.description || undefined,
      source: form.value.source || undefined,
      visibility: form.value.visibility,
      openness: form.value.openness,
      // Always sent: '' clears the repo link, a value sets it.
      repositoryUrl: form.value.repositoryUrl.trim(),
      restricted: form.value.restricted,
      readPrincipals: form.value.readPrincipals,
      tags: form.value.tags,
      showWhenEmpty: form.value.showWhenEmpty,
      apiKey,
    };
    if (isCreate.value) {
      await productsStore.create({ name: form.value.name.trim(), ...payload });
    } else {
      await productsStore.update(props.id, payload);
    }
    props.close();
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'Failed to save product';
  } finally {
    saving.value = false;
  }
}

// --- Versions tab actions (immediate, independent of Save) ---

function onFileSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  uploadFile.value = input.files?.[0] ?? null;
}

async function onUpload() {
  if (!uploadVersion.value || !uploadFile.value) return;
  uploading.value = true;
  versionsError.value = '';
  versionsMessage.value = '';
  try {
    await shelfApi.uploadVersion(props.id, uploadVersion.value, uploadFile.value);
    versionsMessage.value = `Version ${uploadVersion.value} uploaded`;
    uploadVersion.value = '';
    uploadFile.value = null;
    if (fileInput.value) fileInput.value.value = '';
    await loadProduct();
    await productsStore.loadAll();
  } catch (err) {
    versionsError.value = err instanceof ApiError ? err.message : 'Upload failed';
  } finally {
    uploading.value = false;
  }
}

async function onDeleteVersion(version: string) {
  const ok = await dialog.confirm({
    title: 'Delete Version',
    message: `Delete version ${version} of ${props.id}? The deployed files are removed — this cannot be undone.`,
    confirmText: 'Delete',
    confirmVariant: 'danger',
  }).result;
  if (!ok) return;
  deletingVersion.value = version;
  versionsError.value = '';
  versionsMessage.value = '';
  try {
    await shelfApi.deleteVersion(props.id, version);
    versionsMessage.value = `Version ${version} deleted`;
    await loadProduct();
    await productsStore.loadAll();
  } catch (err) {
    versionsError.value = err instanceof ApiError ? err.message : 'Failed to delete version';
  } finally {
    deletingVersion.value = null;
  }
}
</script>

<template>
  <ModalLayout
    :close="close"
    :title="modalTitle"
    :sub-title="isCreate ? 'Register a new documentation product' : `Editing ${props.id}`"
    icon="book-open"
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
                <CoarFormField
                  label="Name"
                  hint="Lowercase letters, numbers, hyphens — used in URLs"
                  :disabled="!isCreate"
                  required
                  class="flex-1"
                >
                  <CoarTextInput v-model="form.name" placeholder="my-product" clearable />
                </CoarFormField>
                <CoarFormField label="Visibility" class="visibility-select">
                  <CoarSelect v-model="form.visibility" :options="visibilityOptions" />
                </CoarFormField>
              </div>

              <CoarFormField label="Display Name">
                <CoarTextInput v-model="form.displayName" placeholder="My Product" clearable />
              </CoarFormField>

              <CoarFormField label="Description">
                <CoarTextInput v-model="form.description" placeholder="Short description of this product" :rows="2" />
              </CoarFormField>

              <div class="flex gap-4">
                <CoarFormField
                  label="Source"
                  hint="Shown as a badge on the landing page"
                  class="openness-select"
                >
                  <CoarSelect v-model="form.openness" :options="opennessOptions" />
                </CoarFormField>
                <CoarFormField
                  label="Repository URL"
                  hint="Optional — adds a “Source ↗” link; leave empty for none"
                  class="flex-1"
                >
                  <CoarTextInput v-model="form.repositoryUrl" placeholder="https://github.com/org/repo" clearable />
                </CoarFormField>
              </div>

              <CoarCheckbox
                v-model="form.showWhenEmpty"
                label="Show on landing page even without published versions"
              />
            </form>
          </template>
        </CoarTab>

        <CoarTab id="access-control">
          Access
          <template #content>
            <div class="tab-panel flex flex-col gap-4">
              <CoarCheckbox
                v-model="form.restricted"
                label="Restricted — only assigned groups/users may view"
              />
              <p class="section-desc">
                Restricted products are hidden from users without access and return 404 on
                unauthorized requests. Orthogonal to visibility (a product can be preview + restricted).
              </p>

              <section v-if="form.restricted">
                <div class="section-heading">Who may read</div>
                <div class="grant-add-row">
                  <CoarSelect
                    :model-value="null"
                    :options="availablePrincipalOptions"
                    placeholder="Add a group or user…"
                    searchable
                    class="flex-1"
                    @update:model-value="addPrincipalByKey"
                  />
                </div>
                <div class="grant-add-row">
                  <CoarTextInput
                    v-model="principalEmail"
                    placeholder="…or add a user by email (before first login)"
                    class="flex-1"
                    @keydown.enter.prevent="addPrincipalEmail"
                  />
                  <CoarButton variant="secondary" size="s" @click="addPrincipalEmail">Add</CoarButton>
                </div>
                <div v-if="form.readPrincipals.length > 0" class="tag-chips">
                  <span v-for="p in form.readPrincipals" :key="p.kind + ':' + p.id" class="tag-chip">
                    <CoarIcon :name="p.kind === 'Group' ? 'users' : 'user'" class="chip-icon" />
                    {{ principalLabel(p) }}
                    <button class="tag-chip-remove" type="button" aria-label="Remove" @click="removePrincipal(p)">×</button>
                  </span>
                </div>
                <p v-else class="section-desc">No one assigned yet — this product is admin-only.</p>
              </section>
            </div>
          </template>
        </CoarTab>

        <CoarTab id="access">
          Tags &amp; API
          <template #content>
            <div class="tab-panel flex flex-col gap-4">
              <section>
                <div class="section-heading">Tags</div>
                <div class="tag-input-row">
                  <CoarTextInput
                    v-model="tagInput"
                    placeholder="Add tag…"
                    class="flex-1"
                    @keydown.enter.prevent="addTag"
                  />
                  <CoarButton variant="secondary" size="s" @click="addTag">Add</CoarButton>
                </div>
                <div v-if="form.tags.length > 0" class="tag-chips">
                  <span v-for="tag in form.tags" :key="tag" class="tag-chip">
                    {{ tag }}
                    <button class="tag-chip-remove" type="button" aria-label="Remove tag" @click="removeTag(tag)">×</button>
                  </span>
                </div>
              </section>

              <section>
                <div class="section-heading">API Key</div>
                <p class="section-desc">
                  Per-product upload key for CI/CD (<code>Authorization: Bearer</code>), valid only for
                  this product.
                </p>
                <template v-if="keyRevealed || isCreate">
                  <div class="key-edit-row">
                    <CoarFormField label="API Key" hint="Leave empty for no key" class="flex-1">
                      <CoarTextInput v-model="newApiKey" placeholder="shelf_…" clearable />
                    </CoarFormField>
                    <CoarButton variant="secondary" size="s" class="key-edit-btn" @click="newApiKey = generateApiKey()">Generate</CoarButton>
                    <CoarButton
                      v-if="newApiKey"
                      variant="ghost"
                      size="s"
                      class="key-edit-btn"
                      @click="copyKey"
                    >
                      {{ keyCopied ? 'Copied!' : 'Copy' }}
                    </CoarButton>
                  </div>
                </template>
                <template v-else>
                  <p class="section-desc" v-if="hasApiKey">A key is currently set.</p>
                  <CoarFormField :label="hasApiKey ? 'Replace key' : 'Set key (optional)'">
                    <CoarTextInput v-model="newApiKey" placeholder="Leave empty to keep unchanged" clearable :disabled="removeApiKey" />
                  </CoarFormField>
                  <CoarCheckbox
                    v-if="hasApiKey"
                    v-model="removeApiKey"
                    label="Remove the existing key"
                    class="mt-2"
                  />
                </template>
              </section>
            </div>
          </template>
        </CoarTab>

        <CoarTab v-if="!isCreate" id="versions">
          Versions
          <template #content>
            <div class="tab-panel flex flex-col gap-4">
              <section>
                <div class="section-heading">Upload Version</div>
                <div class="upload-row">
                  <CoarFormField label="Version" class="upload-version-input">
                    <CoarTextInput v-model="uploadVersion" placeholder="v1.0.0" />
                  </CoarFormField>
                  <div class="file-field">
                    <span class="file-label">ZIP File</span>
                    <div class="file-picker">
                      <CoarButton variant="secondary" size="s" @click="fileInput?.click()">Choose File…</CoarButton>
                      <span class="file-name" :class="{ 'file-name--empty': !uploadFile }">
                        {{ uploadFile?.name ?? 'No file selected' }}
                      </span>
                      <input ref="fileInput" type="file" accept=".zip" class="file-input-hidden" @change="onFileSelected" />
                    </div>
                  </div>
                  <CoarButton
                    variant="primary"
                    size="s"
                    class="upload-btn"
                    :disabled="!uploadVersion || !uploadFile || uploading"
                    :loading="uploading"
                    @click="onUpload"
                  >
                    Upload
                  </CoarButton>
                </div>
              </section>

              <CoarNote v-if="versionsMessage" variant="success">{{ versionsMessage }}</CoarNote>
              <CoarNote v-if="versionsError" variant="error">{{ versionsError }}</CoarNote>

              <section>
                <div class="section-heading">Existing Versions</div>
                <CoarTable v-if="versions.length > 0" variant="plain" hover>
                  <thead>
                    <tr>
                      <th>Version</th>
                      <th>Status</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="v in versions" :key="v">
                      <td>
                        <a :href="`/${props.id}/${v}/`" target="_blank" class="version-link">{{ v }}</a>
                      </td>
                      <td>
                        <CoarTag v-if="v === latest" variant="accent" size="s">latest</CoarTag>
                      </td>
                      <td class="cell-actions">
                        <CoarButton
                          variant="ghost"
                          size="s"
                          :loading="deletingVersion === v"
                          @click="onDeleteVersion(v)"
                        >
                          Delete
                        </CoarButton>
                      </td>
                    </tr>
                  </tbody>
                </CoarTable>
                <p v-else class="section-desc">No versions deployed yet.</p>
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
/* Only the tab CONTENT scrolls — the tab bar stays fixed in view. */
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

.visibility-select {
  width: 10rem;
}

.openness-select {
  width: 11rem;
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

.grant-add-row {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 8px;
}

.chip-icon {
  font-size: 0.85rem;
  opacity: 0.75;
}

.tag-input-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.key-edit-row {
  display: flex;
  gap: 8px;
  align-items: flex-start;
}

.key-edit-btn {
  margin-top: 23px;
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

.upload-row {
  display: flex;
  align-items: flex-end;
  gap: 12px;
  flex-wrap: wrap;
}

.upload-version-input {
  width: 160px;
}

.file-field {
  flex: 1;
  min-width: 220px;
}

.file-label {
  display: block;
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--coar-text-neutral-secondary);
  margin-bottom: 6px;
}

.file-picker {
  display: flex;
  align-items: center;
  gap: 10px;
}

.file-name {
  font-size: 0.85rem;
  color: var(--coar-text-neutral-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-name--empty {
  color: var(--coar-text-neutral-tertiary);
}

.file-input-hidden {
  display: none;
}

.upload-btn {
  margin-bottom: 2px;
}

.version-link {
  color: var(--coar-text-accent-primary);
  text-decoration: none;
  font-weight: 500;
}
.version-link:hover { text-decoration: underline; }

.cell-actions { text-align: right; }

.loading-text {
  color: var(--coar-text-neutral-tertiary);
}
</style>

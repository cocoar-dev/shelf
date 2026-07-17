<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { CoarTextInput, CoarSelect, CoarCheckbox, CoarNote, CoarButton, CoarFormField } from '@cocoar/vue-ui';
import ModalLayout from '@/components/ModalLayout.vue';
import { useProductsStore } from '@/stores/products.store';
import { shelfApi } from '@/core/api/shelf-api';
import { ApiError } from '@/core/api/http';

const props = defineProps<{
  id: string
  close: (result?: unknown) => void
}>();

const productsStore = useProductsStore();
const isCreate = computed(() => props.id === 'create');
const loading = ref(false);
const saving = ref(false);
const error = ref('');

const visibilityOptions = [
  { value: 'public', label: 'Public' },
  { value: 'preview', label: 'Preview' },
];

const form = ref({
  name: '',
  displayName: '',
  description: '',
  source: 'upload',
  visibility: 'public',
  tags: [] as string[],
  showWhenEmpty: false,
});

const tagInput = ref('');
const hasApiKey = ref(false);
const newApiKey = ref('');
const removeApiKey = ref(false);

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

onMounted(async () => {
  if (isCreate.value) return;
  loading.value = true;
  try {
    const product = await shelfApi.getProduct(props.id);
    form.value = {
      name: product.name,
      displayName: product.displayName ?? '',
      description: product.description ?? '',
      source: product.source,
      visibility: product.visibility,
      tags: [...(product.tags ?? [])],
      showWhenEmpty: product.showWhenEmpty ?? false,
    };
    hasApiKey.value = product.hasApiKey ?? false;
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

async function save() {
  if (!form.value.name.trim()) return;
  error.value = '';
  saving.value = true;
  try {
    // API key semantics: undefined = keep, '' = remove, value = set/replace.
    const apiKey = removeApiKey.value ? '' : (newApiKey.value.trim() || undefined);
    const payload = {
      displayName: form.value.displayName || undefined,
      description: form.value.description || undefined,
      source: form.value.source || undefined,
      visibility: form.value.visibility,
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
</script>

<template>
  <ModalLayout
    :close="close"
    :title="modalTitle"
    :sub-title="isCreate ? 'Register a new documentation product' : `Editing ${props.id}`"
    icon="book-open"
    :footer-button="footerButton"
  >
    <form v-if="!loading" class="flex flex-col gap-4" @submit.prevent="save">
      <CoarNote v-if="error" variant="error">{{ error }}</CoarNote>

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
          this product. Write-only — the value is never shown again.
          <template v-if="hasApiKey">A key is currently set.</template>
        </p>
        <CoarFormField :label="hasApiKey ? 'Replace key' : 'Set key (optional)'">
          <CoarTextInput v-model="newApiKey" placeholder="Leave empty to keep unchanged" clearable :disabled="removeApiKey" />
        </CoarFormField>
        <CoarCheckbox
          v-if="hasApiKey"
          v-model="removeApiKey"
          label="Remove the existing key"
          class="mt-2"
        />
      </section>

      <section>
        <div class="section-heading">Options</div>
        <CoarCheckbox
          v-model="form.showWhenEmpty"
          label="Show on landing page even without published versions"
        />
      </section>
    </form>
    <div v-else class="flex flex-1 items-center justify-center p-8">
      <span class="loading-text">Loading…</span>
    </div>
  </ModalLayout>
</template>

<style scoped>
.visibility-select {
  width: 10rem;
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

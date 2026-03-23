<template>
  <div>
    <CoarNote v-if="error" variant="error" class="mb-4">{{ error }}</CoarNote>

    <div v-if="isLoading" class="center-content">
      <CoarSpinner size="m" />
    </div>

    <div v-if="!isLoading" class="form-grid">
      <div class="form-main">
        <CoarCard title="Product Details">
          <div class="form-fields">
            <CoarTextInput
              v-model="form.name"
              label="Name"
              placeholder="my-product"
              :disabled="isEditMode"
              required
            />
            <CoarTextInput
              v-model="form.displayName"
              label="Display Name"
              placeholder="My Product"
            />
            <CoarTextInput
              v-model="form.description"
              label="Description"
              placeholder="Short description of this product"
            />
          </div>
        </CoarCard>
      </div>

      <div class="form-side">
        <CoarCard title="Settings">
          <div class="form-fields">
            <CoarSelect
              v-model="form.visibility"
              label="Visibility"
              :options="visibilityOptions"
              hint="Preview products are hidden on the landing page by default"
            />
            <CoarTextInput
              v-model="form.source"
              label="Source"
              placeholder="upload"
              hint="Deployment source type (e.g., &quot;upload&quot;)"
            />
            <label class="checkbox-field">
              <input type="checkbox" v-model="form.showWhenEmpty" />
              <span>
                <strong>Show when empty</strong>
                <small>Display on landing page even without any published versions</small>
              </span>
            </label>
          </div>
        </CoarCard>

        <CoarCard title="Tags" class="mt-4">
          <div class="form-fields">
            <div class="tag-input-row">
              <CoarTextInput
                v-model="tagInput"
                placeholder="Add tag…"
                @keydown.enter.prevent="addTag"
              />
              <CoarButton variant="secondary" size="s" @click="addTag">Add</CoarButton>
            </div>
            <div v-if="form.tags.length > 0" class="tag-chips">
              <span v-for="tag in form.tags" :key="tag" class="tag-chip">
                {{ tag }}
                <button class="tag-chip-remove" @click="removeTag(tag)" type="button" aria-label="Remove tag">×</button>
              </span>
            </div>
            <p v-else class="tag-hint">No tags yet. Tags help users filter documentation on the landing page.</p>
          </div>
        </CoarCard>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { CoarCard, CoarTextInput, CoarSelect, CoarNote, CoarSpinner, CoarButton } from '@cocoar/vue-ui';
import { useUI } from '@/composables/useUI';
import { shelfApi } from '@/core/api/shelf-api';
import { ApiError } from '@/core/api/http';

const route = useRoute();
const router = useRouter();
const ui = useUI();

const name = computed(() => route.params.name as string | undefined);
const isEditMode = computed(() => !!name.value);
const isLoading = ref(false);
const isSaving = ref(false);
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

function addTag() {
  const tag = tagInput.value.trim();
  if (tag && !form.value.tags.includes(tag)) {
    form.value.tags.push(tag);
  }
  tagInput.value = '';
}

function removeTag(tag: string) {
  form.value.tags = form.value.tags.filter(t => t !== tag);
}

ui.set(ctx => {
  ctx.header.title = isEditMode.value ? 'Edit Product' : 'New Product';
  ctx.header.subTitle = isEditMode.value ? `Editing ${name.value}` : 'Register a new documentation product';
  ctx.footer.show = true;
  ctx.footer.button1.visible = true;
  ctx.footer.button1.text = 'Cancel';
  ctx.footer.button1.onClick = () => router.back();
  ctx.footer.button3.visible = true;
  ctx.footer.button3.text = isEditMode.value ? 'Save' : 'Create';
  ctx.footer.button3.onClick = () => onSubmit();
});

watch(isSaving, val => {
  ui.state.footer.button3.loading = val;
  ui.state.footer.button3.disabled = val;
});

onMounted(async () => {
  if (!isEditMode.value) return;

  isLoading.value = true;
  try {
    const product = await shelfApi.getProduct(name.value!);
    form.value.name = product.name;
    form.value.displayName = product.displayName ?? '';
    form.value.description = product.description ?? '';
    form.value.source = product.source;
    form.value.visibility = product.visibility;
    form.value.tags = [...(product.tags ?? [])];
    form.value.showWhenEmpty = product.showWhenEmpty ?? false;
  } catch {
    error.value = 'Failed to load product';
  } finally {
    isLoading.value = false;
  }
});

async function onSubmit() {
  error.value = '';
  isSaving.value = true;

  try {
    if (isEditMode.value) {
      await shelfApi.updateProduct(name.value!, {
        displayName: form.value.displayName || undefined,
        description: form.value.description || undefined,
        source: form.value.source || undefined,
        visibility: form.value.visibility,
        tags: form.value.tags,
        showWhenEmpty: form.value.showWhenEmpty,
      });
      router.push(`/admin/products/${name.value}`);
    } else {
      await shelfApi.createProduct({
        name: form.value.name,
        displayName: form.value.displayName || undefined,
        description: form.value.description || undefined,
        source: form.value.source || undefined,
        visibility: form.value.visibility,
        tags: form.value.tags,
        showWhenEmpty: form.value.showWhenEmpty,
      });
      router.push(`/admin/products/${form.value.name}`);
    }
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'Failed to save product';
  } finally {
    isSaving.value = false;
  }
}
</script>

<style scoped>
.form-grid {
  display: grid;
  grid-template-columns: 1fr 300px;
  gap: 20px;
}

@media (max-width: 860px) {
  .form-grid { grid-template-columns: 1fr; }
}

.form-fields {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.mt-4 { margin-top: 16px; }

.tag-input-row {
  display: flex;
  gap: 8px;
  align-items: flex-end;
}

.tag-input-row > :first-child {
  flex: 1;
}

.tag-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.tag-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 10px;
  border-radius: 10px;
  font-size: 0.8rem;
  font-weight: 500;
  background: #dbeafe;
  color: #1183CD;
  border: 1px solid #bfdbfe;
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

.tag-hint {
  font-size: 0.82rem;
  color: #94a3b8;
  margin: 0;
}

.mb-4 { margin-bottom: 16px; }
.center-content { display: flex; justify-content: center; padding: 48px 0; }

.checkbox-field {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  cursor: pointer;
  padding: 4px 0;
}

.checkbox-field input { margin-top: 3px; cursor: pointer; flex-shrink: 0; }

.checkbox-field span {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.checkbox-field strong {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--coar-text-neutral-primary, #1e293b);
}

.checkbox-field small {
  font-size: 0.78rem;
  color: var(--coar-text-neutral-secondary, #64748b);
}
</style>

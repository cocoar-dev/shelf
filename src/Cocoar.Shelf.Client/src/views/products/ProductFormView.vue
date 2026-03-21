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
          </div>
        </CoarCard>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { CoarCard, CoarTextInput, CoarSelect, CoarNote, CoarSpinner } from '@cocoar/vue-ui';
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
});

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
    const products = await shelfApi.getProducts();
    const product = products.find(p => p.name === name.value);
    if (product) {
      form.value.name = product.name;
      form.value.displayName = product.displayName ?? '';
      form.value.description = product.description ?? '';
      form.value.source = product.source;
      form.value.visibility = product.visibility;
    } else {
      error.value = `Product '${name.value}' not found`;
    }
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
      });
      router.push(`/admin/products/${name.value}`);
    } else {
      await shelfApi.createProduct({
        name: form.value.name,
        displayName: form.value.displayName || undefined,
        description: form.value.description || undefined,
        source: form.value.source || undefined,
        visibility: form.value.visibility,
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

.mb-4 { margin-bottom: 16px; }
.center-content { display: flex; justify-content: center; padding: 48px 0; }
</style>

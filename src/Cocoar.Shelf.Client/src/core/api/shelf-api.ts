import { http } from './http';
import type {
  Product, ProductVersions, CreateProductRequest, UpdateProductRequest,
  GroupSummary, GroupDetail, GroupUpsertRequest, ScriptTestResult,
} from '../models/shelf.models';

export const shelfApi = {
  getProducts: () => http.get<Product[]>('/products'),
  getProduct: (name: string) => http.get<Product>(`/products/${name}`),
  getVersions: (product: string) => http.get<ProductVersions>(`/products/${product}/versions`),
  getProductApiKey: (name: string) => http.get<{ apiKey: string | null }>(`/products/${name}/api-key`),
  createProduct: (req: CreateProductRequest) => http.post<Product>('/products', req),
  updateProduct: (name: string, req: UpdateProductRequest) => http.put<Product>(`/products/${name}`, req),
  deleteProduct: (name: string, deleteData = false) =>
    http.delete<void>(`/products/${name}${deleteData ? '?deleteData=true' : ''}`),

  deleteVersion: (product: string, version: string) =>
    http.delete<void>(`/products/${product}/versions/${version}`),
  uploadVersion: (product: string, version: string, file: File | Blob) =>
    http.upload<void>(`/products/${product}/versions/${version}`, file),

  // Access Control v2: groups (admin-only)
  getGroups: () => http.get<GroupSummary[]>('/groups'),
  getGroup: (id: string) => http.get<GroupDetail>(`/groups/${id}`),
  createGroup: (req: GroupUpsertRequest) => http.post<GroupSummary>('/groups', req),
  updateGroup: (id: string, req: GroupUpsertRequest) => http.put<GroupSummary>(`/groups/${id}`, req),
  deleteGroup: (id: string) => http.delete<void>(`/groups/${id}`),
  recalculateGroups: () => http.post<{ ok: boolean }>('/groups/recalculate'),
  testGroupScript: (req: { script: string; userId?: string; email?: string }) =>
    http.post<ScriptTestResult>('/groups/test-script', req),
};

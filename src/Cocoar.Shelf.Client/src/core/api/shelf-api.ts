import { http } from './http';
import type { Product, ProductVersions, CreateProductRequest, UpdateProductRequest } from '../models/shelf.models';

export const shelfApi = {
  verifyApiKey: () => http.get<{ ok: boolean }>('/admin/verify'),

  getProducts: () => http.get<Product[]>('/products'),
  getVersions: (product: string) => http.get<ProductVersions>(`/products/${product}/versions`),
  createProduct: (req: CreateProductRequest) => http.post<Product>('/products', req),
  updateProduct: (name: string, req: UpdateProductRequest) => http.put<Product>(`/products/${name}`, req),
  deleteProduct: (name: string) => http.delete<void>(`/products/${name}`),

  deleteVersion: (product: string, version: string) =>
    http.delete<void>(`/products/${product}/versions/${version}`),
  uploadVersion: (product: string, version: string, file: File | Blob) =>
    http.upload<void>(`/products/${product}/versions/${version}`, file),
};

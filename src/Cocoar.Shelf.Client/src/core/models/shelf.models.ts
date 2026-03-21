export interface Product {
  name: string;
  displayName: string | null;
  description: string | null;
  source: string;
  latest: string | null;
  versions: string[];
}

export interface ProductVersions {
  name: string;
  latest: string | null;
  versions: string[];
}

export interface CreateProductRequest {
  name: string;
  displayName?: string;
  description?: string;
  source?: string;
}

export interface UpdateProductRequest {
  displayName?: string;
  description?: string;
  source?: string;
}

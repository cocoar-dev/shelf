export interface Product {
  name: string;
  displayName: string | null;
  description: string | null;
  source: string;
  visibility: string;
  tags: string[];
  showWhenEmpty: boolean;
  hasApiKey: boolean;
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
  visibility?: string;
  tags?: string[];
  showWhenEmpty?: boolean;
  /** Per-product upload key. Write-only: responses only carry hasApiKey. */
  apiKey?: string;
}

export interface UpdateProductRequest {
  displayName?: string;
  description?: string;
  source?: string;
  visibility?: string;
  tags?: string[];
  showWhenEmpty?: boolean;
  /** undefined = keep, '' = remove, value = replace. */
  apiKey?: string;
}

export interface ShelfSettingsInfo {
  hasMasterApiKey: boolean;
  masterApiKey: string | null;
  hasConfigApiKey: boolean;
}

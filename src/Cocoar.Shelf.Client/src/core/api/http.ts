import { useAuthStore } from '@/stores/auth.store';
import { router } from '@/router';

export class ApiError extends Error {
  constructor(public readonly status: number, public readonly body: unknown) {
    const message = (body as { error?: string })?.error ?? `HTTP ${status}`;
    super(message);
    this.name = 'ApiError';
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = {
    ...(init.headers as Record<string, string>),
  };

  const auth = useAuthStore();
  if (auth.apiKey) {
    headers['Authorization'] = `Bearer ${auth.apiKey}`;
  }

  if (init.body && typeof init.body === 'string') {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(`/_api${path}`, { ...init, headers });

  if (!response.ok) {
    if (response.status === 401) {
      auth.logout();
      router.push('/login');
    }
    const contentType = response.headers.get('content-type') ?? '';
    const errData = contentType.includes('application/json')
      ? await response.json().catch(() => null)
      : await response.text().catch(() => null);
    throw new ApiError(response.status, errData);
  }

  if (response.status === 204 || response.headers.get('content-length') === '0') {
    return undefined as T;
  }

  return await response.json() as T;
}

export const http = {
  get: <T>(path: string) => request<T>(path, { method: 'GET' }),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body !== undefined ? JSON.stringify(body) : undefined }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PUT', body: body !== undefined ? JSON.stringify(body) : undefined }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
  upload: <T>(path: string, file: File | Blob) => {
    const headers: Record<string, string> = { 'Content-Type': 'application/zip' };
    const auth = useAuthStore();
    if (auth.apiKey) headers['Authorization'] = `Bearer ${auth.apiKey}`;
    return request<T>(path, { method: 'POST', body: file, headers });
  },
};

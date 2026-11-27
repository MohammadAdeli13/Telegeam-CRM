import type { components } from '@/types/api';
import { getAccessToken } from './session';

type ProductImageDto = components['schemas']['ProductImageDto'];

export interface UploadResult {
  data?: ProductImageDto;
  error?: { code?: string; detail?: string; title?: string; status?: number };
}

/**
 * Uploads a product image via multipart/form-data to POST /api/v1/merchant/products/{id}/images.
 */
export async function uploadProductImage(
  productId: string,
  file: File,
  altText?: string,
): Promise<UploadResult> {
  const form = new FormData();
  form.append('file', file);
  if (altText && altText.trim().length > 0) {
    form.append('altText', altText.trim());
  }

  const headers = new Headers();
  const token = getAccessToken();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(`/api/v1/merchant/products/${encodeURIComponent(productId)}/images`, {
    method: 'POST',
    headers,
    body: form,
  });

  if (response.ok) {
    const data = (await response.json()) as ProductImageDto;
    return { data };
  }

  try {
    const problem = (await response.json()) as {
      code?: string;
      detail?: string;
      title?: string;
      status?: number;
    };
    return { error: problem };
  } catch {
    return { error: { status: response.status, title: response.statusText } };
  }
}

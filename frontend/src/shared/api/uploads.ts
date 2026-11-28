import type { components } from '@/types/api';
import { getAccessToken } from './session';

type ProductImageDto = components['schemas']['ProductImageDto'];
type MerchantShopDto = components['schemas']['MerchantShopDto'];

export interface UploadProblem {
  code?: string;
  detail?: string;
  title?: string;
  status?: number;
}

export interface UploadResult<T = ProductImageDto> {
  data?: T;
  error?: UploadProblem;
}

async function postMultipart<T>(url: string, form: FormData): Promise<UploadResult<T>> {
  const headers = new Headers();
  const token = getAccessToken();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(url, {
    method: 'POST',
    headers,
    body: form,
  });

  if (response.ok) {
    const data = (await response.json()) as T;
    return { data };
  }

  try {
    const problem = (await response.json()) as UploadProblem;
    return { error: problem };
  } catch {
    return { error: { status: response.status, title: response.statusText } };
  }
}

/**
 * Uploads a product image via multipart/form-data to POST /api/v1/merchant/products/{id}/images.
 */
export async function uploadProductImage(
  productId: string,
  file: File,
  altText?: string,
): Promise<UploadResult<ProductImageDto>> {
  const form = new FormData();
  form.append('file', file);
  if (altText && altText.trim().length > 0) {
    form.append('altText', altText.trim());
  }

  return postMultipart<ProductImageDto>(
    `/api/v1/merchant/products/${encodeURIComponent(productId)}/images`,
    form,
  );
}

/**
 * Uploads or replaces the active shop logo via POST /api/v1/merchant/shop/logo.
 */
export async function uploadShopLogo(file: File): Promise<UploadResult<MerchantShopDto>> {
  const form = new FormData();
  form.append('file', file);
  return postMultipart<MerchantShopDto>('/api/v1/merchant/shop/logo', form);
}

/**
 * Uploads or replaces the active shop's default product placeholder image.
 */
export async function uploadDefaultProductImage(
  file: File,
): Promise<UploadResult<MerchantShopDto>> {
  const form = new FormData();
  form.append('file', file);
  return postMultipart<MerchantShopDto>('/api/v1/merchant/shop/default-product-image', form);
}

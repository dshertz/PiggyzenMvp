import { CategoryDto, ImportResult, TransactionDto } from './types';

const baseUrl = (() => {
  const raw = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5142/';
  if (raw.endsWith('/')) {
    return raw;
  }
  return `${raw}/`;
})();

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`HTTP ${response.status} ${response.statusText}\n${text}`);
  }

  const contentType = response.headers.get('content-type');
  if (contentType?.includes('application/json')) {
    return (await response.json()) as T;
  }

  // For endpoints returning empty body
  return undefined as T;
}

export async function fetchTransactions(): Promise<TransactionDto[]> {
  const response = await fetch(`${baseUrl}api/transactions`);
  return handleResponse<TransactionDto[]>(response);
}

export async function fetchCategories(): Promise<CategoryDto[]> {
  const response = await fetch(`${baseUrl}api/categories`);
  return handleResponse<CategoryDto[]>(response);
}

export async function createCategory(payload: {
  name: string;
  parentCategoryId: number;
}): Promise<void> {
  const response = await fetch(`${baseUrl}api/categories`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  await handleResponse(response);
}

export async function updateCategory(
  id: number,
  payload: { name?: string; parentCategoryId?: number | null }
): Promise<void> {
  const response = await fetch(`${baseUrl}api/categories/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  await handleResponse(response);
}

export async function deleteCategory(id: number): Promise<void> {
  const response = await fetch(`${baseUrl}api/categories/${id}`, {
    method: 'DELETE'
  });
  await handleResponse(response);
}

export async function updateTransactionCategory(
  transactionId: number,
  categoryId: number
): Promise<void> {
  const response = await fetch(`${baseUrl}api/transactions/${transactionId}/category`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ categoryId })
  });
  await handleResponse(response);
}

export async function importTransactions(rawText: string): Promise<ImportResult> {
  const response = await fetch(`${baseUrl}api/transactions/import`, {
    method: 'POST',
    headers: { 'Content-Type': 'text/plain; charset=utf-8' },
    body: rawText
  });
  return handleResponse<ImportResult>(response);
}

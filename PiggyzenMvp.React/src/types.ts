export interface CategoryDto {
  id: number;
  name: string;
  parentCategoryId: number | null;
  isSystemCategory: boolean;
}

export interface TransactionDto {
  id: number;
  bookingDate: string | null;
  transactionDate: string;
  description: string;
  amount: number;
  balance: number | null;
  categoryId: number | null;
  categoryName: string | null;
  typeId: number | null;
  typeName: string | null;
}

export interface TransactionImportDto {
  bookingDate: string | null;
  transactionDate: string;
  description: string;
  amount: number;
  balance: number | null;
}

export interface ImportResult {
  transactions: TransactionImportDto[];
  errors: string[];
}

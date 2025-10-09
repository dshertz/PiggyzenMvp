import { useEffect, useMemo, useState } from 'react';
import { fetchTransactions, updateTransactionCategory } from '../api';
import { CategoryDto, TransactionDto } from '../types';

type Props = {
  categories: CategoryDto[];
  isLoadingCategories: boolean;
};

type Message = { type: 'success' | 'error'; text: string } | null;

export function TransactionsSection({ categories, isLoadingCategories }: Props) {
  const [transactions, setTransactions] = useState<TransactionDto[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<Message>(null);
  const [savingIds, setSavingIds] = useState<number[]>([]);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const data = await fetchTransactions();
        if (!cancelled) {
          setTransactions(data);
        }
      } catch (error) {
        console.error('Unable to load transactions', error);
        if (!cancelled) {
          setTransactions([]);
          setMessage({ type: 'error', text: 'Kunde inte läsa in transaktioner.' });
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, []);

  const typeLookup = useMemo(() => {
    const lookup = new Map<number, CategoryDto>();
    categories.forEach((category) => lookup.set(category.id, category));
    return lookup;
  }, [categories]);

  const getCategoryLabel = (categoryId: number | null) => {
    if (categoryId == null) {
      return '—';
    }
    const category = typeLookup.get(categoryId);
    if (!category) {
      return '—';
    }

    if (category.parentCategoryId != null) {
      const parent = typeLookup.get(category.parentCategoryId);
      if (parent) {
        return `${parent.name} / ${category.name}`;
      }
    }

    return category.name;
  };

  const formatDate = (value: string | null) => {
    if (!value) {
      return '—';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }
    return date.toLocaleDateString('sv-SE');
  };

  const formatAmount = (value: number | null) => {
    if (value == null) {
      return '—';
    }
    return value.toLocaleString('sv-SE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  };

  const updateLocalTransaction = (id: number, categoryId: number) => {
    setTransactions((prev) => {
      if (!prev) return prev;
      return prev.map((tx) => {
        if (tx.id !== id) return tx;
        const category = typeLookup.get(categoryId) ?? null;
        const parent = category?.parentCategoryId != null ? typeLookup.get(category.parentCategoryId) ?? null : null;

        return {
          ...tx,
          categoryId,
          categoryName: category?.name ?? null,
          typeId: category?.isSystemCategory ? category.id : parent?.id ?? null,
          typeName: category?.isSystemCategory ? category?.name ?? null : parent?.name ?? null
        };
      });
    });
  };

  const handleChange = async (tx: TransactionDto, rawValue: string) => {
    if (!rawValue) {
      return;
    }
    const categoryId = Number(rawValue);
    if (Number.isNaN(categoryId)) {
      return;
    }

    setSavingIds((prev) => (prev.includes(tx.id) ? prev : [...prev, tx.id]));
    setMessage(null);

    try {
      await updateTransactionCategory(tx.id, categoryId);
      updateLocalTransaction(tx.id, categoryId);
      setMessage({ type: 'success', text: 'Kategori uppdaterad!' });
    } catch (error) {
      console.error('Unable to update category', error);
      setMessage({ type: 'error', text: 'Kunde inte uppdatera kategori.' });
    } finally {
      setSavingIds((prev) => prev.filter((id) => id !== tx.id));
    }
  };

  return (
    <section id="transactions" className="space-y-4">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900">Transaktioner</h2>
        <p className="text-sm text-slate-600">Visa och kategorisera dina transaktioner.</p>
      </div>

      {message && (
        <div
          className={`rounded border px-4 py-3 text-sm ${
            message.type === 'success'
              ? 'border-green-200 bg-green-50 text-green-700'
              : 'border-red-200 bg-red-50 text-red-700'
          }`}
        >
          {message.text}
        </div>
      )}

      {loading ? (
        <p className="text-slate-600">Laddar transaktioner…</p>
      ) : transactions && transactions.length > 0 ? (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
            <thead className="bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-600">
              <tr>
                <th className="px-4 py-3">Bokf.datum</th>
                <th className="px-4 py-3">Trans.datum</th>
                <th className="px-4 py-3">Beskrivning</th>
                <th className="px-4 py-3 text-right">Belopp</th>
                <th className="px-4 py-3 text-right">Saldo</th>
                <th className="px-4 py-3">Typ</th>
                <th className="px-4 py-3">Kategori</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {transactions.map((tx) => (
                <tr key={tx.id} className="hover:bg-slate-50/70">
                  <td className="px-4 py-2 text-slate-700">{formatDate(tx.bookingDate)}</td>
                  <td className="px-4 py-2 text-slate-700">{formatDate(tx.transactionDate)}</td>
                  <td className="px-4 py-2 text-slate-800">{tx.description}</td>
                  <td className="px-4 py-2 text-right font-medium text-slate-900">{formatAmount(tx.amount)}</td>
                  <td className="px-4 py-2 text-right text-slate-700">{formatAmount(tx.balance)}</td>
                  <td className="px-4 py-2 text-slate-700">{getCategoryLabel(tx.typeId)}</td>
                  <td className="px-4 py-2">
                    <select
                      className="w-full rounded border border-slate-300 bg-white px-2 py-1 text-sm"
                      value={tx.categoryId ?? ''}
                      onChange={(event) => handleChange(tx, event.target.value)}
                      disabled={savingIds.includes(tx.id) || isLoadingCategories}
                    >
                      <option value="">—</option>
                      {categories.map((category) => (
                        <option key={category.id} value={category.id}>
                          {getCategoryLabel(category.id)}
                        </option>
                      ))}
                    </select>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="text-slate-600">Inga transaktioner hittades.</p>
      )}
    </section>
  );
}

import { useCallback, useEffect, useState } from 'react';
import { fetchCategories } from './api';
import { CategoriesSection } from './components/CategoriesSection';
import { ImportSection } from './components/ImportSection';
import { TransactionsSection } from './components/TransactionsSection';
import { CategoryDto } from './types';

export default function App() {
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [categoriesError, setCategoriesError] = useState<string | null>(null);
  const [loadingCategories, setLoadingCategories] = useState(true);

  const loadCategories = useCallback(async () => {
    setLoadingCategories(true);
    try {
      const data = await fetchCategories();
      setCategories(data);
      setCategoriesError(null);
    } catch (error) {
      console.error('Unable to load categories', error);
      setCategories([]);
      setCategoriesError('Kunde inte läsa in kategorier.');
    } finally {
      setLoadingCategories(false);
    }
  }, []);

  useEffect(() => {
    loadCategories();
  }, [loadCategories]);

  return (
    <div className="min-h-screen bg-slate-100">
      <header className="border-b border-slate-200 bg-white/80 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-5">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.3em] text-brand-500">Piggyzen</p>
            <h1 className="text-2xl font-bold text-slate-900">Ekonomiöversikt</h1>
          </div>
          <nav className="hidden gap-6 text-sm font-medium text-slate-600 md:flex">
            <a className="hover:text-brand-600" href="#transactions">
              Transaktioner
            </a>
            <a className="hover:text-brand-600" href="#categories">
              Kategorier
            </a>
            <a className="hover:text-brand-600" href="#import">
              Import
            </a>
          </nav>
        </div>
      </header>

      <main className="mx-auto flex max-w-6xl flex-col gap-16 px-4 py-12">
        <TransactionsSection categories={categories} isLoadingCategories={loadingCategories} />
        <CategoriesSection
          categories={categories}
          isLoading={loadingCategories}
          error={categoriesError}
          reload={loadCategories}
        />
        <ImportSection />
      </main>

      <footer className="border-t border-slate-200 bg-white/60">
        <div className="mx-auto flex max-w-6xl flex-col gap-2 px-4 py-6 text-xs text-slate-500 sm:flex-row sm:items-center sm:justify-between">
          <p>&copy; {new Date().getFullYear()} Piggyzen. Alla rättigheter förbehållna.</p>
          <p>
            API-bas: <code className="rounded bg-slate-100 px-2 py-1">{import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5142/'}</code>
          </p>
        </div>
      </footer>
    </div>
  );
}

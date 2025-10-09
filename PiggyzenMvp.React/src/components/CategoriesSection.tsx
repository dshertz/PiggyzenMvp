import { Fragment, useMemo, useState } from 'react';
import { createCategory, deleteCategory, updateCategory } from '../api';
import { CategoryDto } from '../types';

type Props = {
  categories: CategoryDto[];
  isLoading: boolean;
  error: string | null;
  reload: () => Promise<void>;
};

type Message = { type: 'info' | 'error'; text: string } | null;

export function CategoriesSection({ categories, isLoading, error, reload }: Props) {
  const [createName, setCreateName] = useState('');
  const [createParentId, setCreateParentId] = useState<string>('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<Message>(null);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editName, setEditName] = useState('');
  const [editParentId, setEditParentId] = useState<string>('');

  const systemCategories = useMemo(
    () => categories.filter((category) => category.isSystemCategory),
    [categories]
  );

  const parentName = (category: CategoryDto) => {
    if (category.parentCategoryId == null) {
      return '—';
    }
    const parent = categories.find((c) => c.id === category.parentCategoryId);
    return parent ? `${parent.name} (${parent.id})` : `${category.parentCategoryId}`;
  };

  const startEdit = (category: CategoryDto) => {
    setEditingId(category.id);
    setEditName(category.name);
    setEditParentId(category.parentCategoryId?.toString() ?? '');
    setMessage(null);
  };

  const cancelEdit = () => {
    setEditingId(null);
    setEditName('');
    setEditParentId('');
  };

  const handleCreate = async () => {
    if (!createName.trim()) {
      setMessage({ type: 'error', text: 'Ange namn.' });
      return;
    }
    if (!createParentId) {
      setMessage({ type: 'error', text: 'Välj parent (systemkategori).' });
      return;
    }

    setBusy(true);
    setMessage(null);

    try {
      await createCategory({
        name: createName.trim(),
        parentCategoryId: Number(createParentId)
      });
      setCreateName('');
      setCreateParentId('');
      await reload();
      setMessage({ type: 'info', text: 'Kategori skapad.' });
    } catch (err) {
      console.error('Unable to create category', err);
      setMessage({ type: 'error', text: 'Skapande misslyckades.' });
    } finally {
      setBusy(false);
    }
  };

  const handleSave = async (category: CategoryDto) => {
    setBusy(true);
    setMessage(null);

    try {
      await updateCategory(category.id, {
        name: editName.trim() || undefined,
        parentCategoryId: editParentId ? Number(editParentId) : null
      });
      await reload();
      cancelEdit();
      setMessage({ type: 'info', text: 'Kategori uppdaterad.' });
    } catch (err) {
      console.error('Unable to update category', err);
      setMessage({ type: 'error', text: 'Uppdatering misslyckades.' });
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async (category: CategoryDto) => {
    if (category.isSystemCategory) {
      return;
    }
    const confirmed = window.confirm(`Ta bort kategori ${category.id}?`);
    if (!confirmed) {
      return;
    }

    setBusy(true);
    setMessage(null);

    try {
      await deleteCategory(category.id);
      await reload();
      setMessage({ type: 'info', text: 'Kategori borttagen.' });
    } catch (err) {
      console.error('Unable to delete category', err);
      setMessage({ type: 'error', text: 'Radering misslyckades.' });
    } finally {
      setBusy(false);
    }
  };

  return (
    <section id="categories" className="space-y-4">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900">Kategorier</h2>
        <p className="text-sm text-slate-600">Hantera transaktionskategorier och underkategorier.</p>
      </div>

      <div className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <h3 className="text-lg font-medium text-slate-900">Skapa ny kategori</h3>
        <div className="mt-4 grid gap-4 md:grid-cols-[1fr_1fr_auto] md:items-end">
          <label className="space-y-2 text-sm font-medium text-slate-700">
            <span>Namn</span>
            <input
              type="text"
              className="w-full rounded border border-slate-300 px-3 py-2"
              value={createName}
              onChange={(event) => setCreateName(event.target.value)}
              placeholder="t.ex. Livsmedel"
              disabled={busy}
            />
          </label>

          <label className="space-y-2 text-sm font-medium text-slate-700">
            <span>Parent (krävs)</span>
            <select
              className="w-full rounded border border-slate-300 px-3 py-2"
              value={createParentId}
              onChange={(event) => setCreateParentId(event.target.value)}
              disabled={busy}
            >
              <option value="">— välj systemkategori —</option>
              {systemCategories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name} ({category.id})
                </option>
              ))}
            </select>
            <span className="block text-xs font-normal text-slate-500">
              Underkategorier måste ligga under en systemkategori.
            </span>
          </label>

          <button
            type="button"
            className="h-11 rounded bg-brand-600 px-4 text-sm font-semibold text-white transition hover:bg-brand-700 disabled:cursor-not-allowed disabled:opacity-60"
            onClick={handleCreate}
            disabled={busy}
          >
            Skapa
          </button>
        </div>
        {message && (
          <p
            className={`mt-3 text-sm ${
              message.type === 'error' ? 'text-red-600' : 'text-slate-600'
            }`}
          >
            {message.text}
          </p>
        )}
      </div>

      {error && (
        <div className="rounded border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      {isLoading ? (
        <p className="text-slate-600">Laddar kategorier…</p>
      ) : categories.length === 0 ? (
        <p className="text-slate-600">Inga kategorier hittades.</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
            <thead className="bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-600">
              <tr>
                <th className="px-4 py-3">Id</th>
                <th className="px-4 py-3">Namn</th>
                <th className="px-4 py-3">Parent</th>
                <th className="px-4 py-3">System</th>
                <th className="px-4 py-3 text-right">Åtgärder</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {categories.map((category) => {
                const isEditing = editingId === category.id;
                return (
                  <Fragment key={category.id}>
                    <tr className="hover:bg-slate-50/70">
                      <td className="px-4 py-2 font-mono text-xs text-slate-600">{category.id}</td>
                      <td className="px-4 py-2 text-slate-800">
                        {isEditing ? (
                          <input
                            className="w-full rounded border border-slate-300 px-2 py-1 text-sm"
                            value={editName}
                            onChange={(event) => setEditName(event.target.value)}
                            disabled={busy}
                          />
                        ) : (
                          category.name
                        )}
                      </td>
                      <td className="px-4 py-2 text-slate-700">
                        {isEditing ? (
                          <select
                            className="w-full rounded border border-slate-300 px-2 py-1 text-sm"
                            value={editParentId}
                            onChange={(event) => setEditParentId(event.target.value)}
                            disabled={busy || category.isSystemCategory}
                          >
                            <option value="">— ingen —</option>
                            {systemCategories
                              .filter((sys) => sys.id !== category.id)
                              .map((sys) => (
                                <option key={sys.id} value={sys.id}>
                                  {sys.name} ({sys.id})
                                </option>
                              ))}
                          </select>
                        ) : (
                          parentName(category)
                        )}
                      </td>
                      <td className="px-4 py-2">
                        <span
                          className={`inline-flex rounded-full px-2 py-1 text-xs font-semibold ${
                            category.isSystemCategory
                              ? 'bg-slate-200 text-slate-700'
                              : 'bg-emerald-100 text-emerald-700'
                          }`}
                        >
                          {category.isSystemCategory ? 'Ja' : 'Nej'}
                        </span>
                      </td>
                      <td className="px-4 py-2 text-right">
                        {isEditing ? (
                          <div className="flex justify-end gap-2 text-sm">
                            <button
                              type="button"
                              className="rounded border border-emerald-500 px-3 py-1 font-medium text-emerald-600 transition hover:bg-emerald-50 disabled:cursor-not-allowed disabled:opacity-60"
                              onClick={() => handleSave(category)}
                              disabled={busy}
                            >
                              Spara
                            </button>
                            <button
                              type="button"
                              className="rounded border border-slate-300 px-3 py-1 font-medium text-slate-600 transition hover:bg-slate-50 disabled:cursor-not-allowed"
                              onClick={cancelEdit}
                              disabled={busy}
                            >
                              Avbryt
                            </button>
                          </div>
                        ) : (
                          <div className="flex justify-end gap-2 text-sm">
                            <button
                              type="button"
                              className="rounded border border-brand-500 px-3 py-1 font-medium text-brand-600 transition hover:bg-brand-50 disabled:cursor-not-allowed disabled:opacity-60"
                              onClick={() => startEdit(category)}
                              disabled={category.isSystemCategory || busy}
                            >
                              Redigera
                            </button>
                            <button
                              type="button"
                              className="rounded border border-red-400 px-3 py-1 font-medium text-red-600 transition hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-60"
                              onClick={() => handleDelete(category)}
                              disabled={category.isSystemCategory || busy}
                            >
                              Ta bort
                            </button>
                          </div>
                        )}
                      </td>
                    </tr>
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

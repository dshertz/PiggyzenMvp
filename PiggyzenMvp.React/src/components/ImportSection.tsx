import { useState } from 'react';
import { importTransactions } from '../api';
import { ImportResult } from '../types';

type Message = { type: 'error' | 'info'; text: string } | null;

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

export function ImportSection() {
  const [rawText, setRawText] = useState('');
  const [result, setResult] = useState<ImportResult | null>(null);
  const [message, setMessage] = useState<Message>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!rawText.trim()) {
      setMessage({ type: 'error', text: 'Du måste ange transaktionstext.' });
      setResult(null);
      return;
    }

    setIsSubmitting(true);
    setMessage(null);

    try {
      const response = await importTransactions(rawText);
      setResult(response);
      if (response.errors.length > 0) {
        setMessage({ type: 'info', text: 'Import klar med vissa fel.' });
      } else {
        setMessage({ type: 'info', text: 'Import genomförd!' });
      }
    } catch (error) {
      console.error('Unable to import transactions', error);
      setMessage({ type: 'error', text: (error as Error).message });
      setResult(null);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <section id="import" className="space-y-4">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900">Importera transaktioner</h2>
        <p className="text-sm text-slate-600">
          Klistra in råtext från din bank och låt backend tolka transaktionerna.
        </p>
      </div>

      <form
        onSubmit={handleSubmit}
        className="space-y-4 rounded-lg border border-slate-200 bg-white p-6 shadow-sm"
      >
        <label className="flex flex-col gap-2 text-sm font-medium text-slate-700">
          <span>Klistra in transaktionstext</span>
          <textarea
            className="min-h-[220px] w-full rounded border border-slate-300 px-3 py-2 font-mono text-xs"
            value={rawText}
            onChange={(event) => setRawText(event.target.value)}
            placeholder="Klistra in din kontoutdragstext här…"
            disabled={isSubmitting}
          />
        </label>
        <button
          type="submit"
          className="inline-flex items-center gap-2 rounded bg-brand-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-700 disabled:cursor-not-allowed disabled:opacity-60"
          disabled={isSubmitting}
        >
          {isSubmitting ? 'Importerar…' : 'Importera'}
        </button>
        {message && (
          <p
            className={`text-sm ${
              message.type === 'error' ? 'text-red-600' : 'text-slate-600'
            }`}
          >
            {message.text}
          </p>
        )}
      </form>

      {result?.errors.length ? (
        <div className="space-y-2 rounded border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
          <p className="font-semibold">⚠️ Fel i importen</p>
          <ul className="list-disc space-y-1 pl-5">
            {result.errors.map((error) => (
              <li key={error}>{error}</li>
            ))}
          </ul>
        </div>
      ) : null}

      {result?.transactions.length ? (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
            <thead className="bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-600">
              <tr>
                <th className="px-4 py-3">Bokf.datum</th>
                <th className="px-4 py-3">Trans.datum</th>
                <th className="px-4 py-3">Beskrivning</th>
                <th className="px-4 py-3 text-right">Belopp</th>
                <th className="px-4 py-3 text-right">Saldo</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {result.transactions.map((tx, index) => (
                <tr key={`${tx.transactionDate}-${index}`} className="hover:bg-slate-50/70">
                  <td className="px-4 py-2 text-slate-700">{formatDate(tx.bookingDate)}</td>
                  <td className="px-4 py-2 text-slate-700">{formatDate(tx.transactionDate)}</td>
                  <td className="px-4 py-2 text-slate-800">{tx.description}</td>
                  <td className="px-4 py-2 text-right font-medium text-slate-900">{formatAmount(tx.amount)}</td>
                  <td className="px-4 py-2 text-right text-slate-700">{formatAmount(tx.balance)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </section>
  );
}

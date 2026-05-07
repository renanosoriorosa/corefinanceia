'use client';

import { useEffect, useState } from 'react';
import { contasFixasService, ContaFixa } from '@/services/contasFixasService';
import { Pagamento, CriarPagamentoRequest, AtualizarPagamentoRequest } from '@/services/pagamentosService';

interface Props {
  aberto: boolean;
  pagamentoEditando: Pagamento | null;
  mesPadrao: number;
  anoPadrao: number;
  salvando: boolean;
  onFechar: () => void;
  onSalvar: (dados: CriarPagamentoRequest | AtualizarPagamentoRequest) => void;
}

const MESES = [
  { valor: 1, label: 'Janeiro' }, { valor: 2, label: 'Fevereiro' },
  { valor: 3, label: 'Março' }, { valor: 4, label: 'Abril' },
  { valor: 5, label: 'Maio' }, { valor: 6, label: 'Junho' },
  { valor: 7, label: 'Julho' }, { valor: 8, label: 'Agosto' },
  { valor: 9, label: 'Setembro' }, { valor: 10, label: 'Outubro' },
  { valor: 11, label: 'Novembro' }, { valor: 12, label: 'Dezembro' },
];

function estadoInicial(mesPadrao: number, anoPadrao: number) {
  return {
    isFixedAccount: true,
    fixedAccountId: '',
    description: '',
    amount: '',
    month: mesPadrao,
    year: anoPadrao,
  };
}

export default function PagamentoFormModal({
  aberto,
  pagamentoEditando,
  mesPadrao,
  anoPadrao,
  salvando,
  onFechar,
  onSalvar,
}: Props) {
  const [form, setForm] = useState(estadoInicial(mesPadrao, anoPadrao));
  const [contasFixas, setContasFixas] = useState<ContaFixa[]>([]);

  useEffect(() => {
    if (!aberto) return;
    contasFixasService.listar().then((lista) => setContasFixas(lista.filter((c) => c.active)));

    if (pagamentoEditando) {
      setForm({
        isFixedAccount: pagamentoEditando.isFixedAccount,
        fixedAccountId: pagamentoEditando.fixedAccountId ?? '',
        description: pagamentoEditando.description,
        amount: pagamentoEditando.amount.toFixed(2).replace('.', ','),
        month: pagamentoEditando.month,
        year: pagamentoEditando.year,
      });
    } else {
      setForm(estadoInicial(mesPadrao, anoPadrao));
    }
  }, [aberto, pagamentoEditando, mesPadrao, anoPadrao]);

  function handleTipo(isFixed: boolean) {
    setForm((f) => ({ ...f, isFixedAccount: isFixed, fixedAccountId: '', description: '' }));
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const amount = parseFloat(form.amount.replace(',', '.'));
    if (isNaN(amount) || amount <= 0) return;

    const contaSelecionada = form.isFixedAccount
      ? contasFixas.find((c) => c.id === form.fixedAccountId)
      : null;

    onSalvar({
      isFixedAccount: form.isFixedAccount,
      fixedAccountId: form.isFixedAccount ? form.fixedAccountId || undefined : undefined,
      description: form.isFixedAccount ? (contaSelecionada?.name ?? form.description) : form.description.trim(),
      amount,
      month: Number(form.month),
      year: Number(form.year),
    });
  }

  const anoAtual = new Date().getFullYear();
  const anos = Array.from({ length: 5 }, (_, i) => anoAtual - 2 + i);

  const formValido =
    parseFloat(form.amount.replace(',', '.')) > 0 &&
    (form.isFixedAccount ? !!form.fixedAccountId : !!form.description.trim()) &&
    form.month >= 1 && form.month <= 12 &&
    form.year >= 2000;

  if (!aberto) return null;

  return (
    <div
      className="modal d-block"
      style={{ backgroundColor: 'rgba(0,0,0,0.6)' }}
      onClick={onFechar}
    >
      <div
        className="modal-dialog modal-dialog-centered"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="modal-content bg-dark border border-secondary">
          <div className="modal-header border-secondary">
            <h5 className="modal-title text-white">
              {pagamentoEditando ? 'Editar Pagamento' : 'Novo Pagamento'}
            </h5>
            <button type="button" className="btn-close btn-close-white" onClick={onFechar} />
          </div>

          <form onSubmit={handleSubmit}>
            <div className="modal-body d-flex flex-column gap-3">

              <div>
                <label className="form-label text-secondary small mb-2">Tipo</label>
                <div className="d-flex gap-2">
                  <button
                    type="button"
                    className={`btn btn-sm flex-fill ${form.isFixedAccount ? 'btn-primary' : 'btn-outline-secondary'}`}
                    onClick={() => handleTipo(true)}
                  >
                    <i className="bi bi-receipt-cutoff me-1" />
                    Conta Fixa
                  </button>
                  <button
                    type="button"
                    className={`btn btn-sm flex-fill ${!form.isFixedAccount ? 'btn-primary' : 'btn-outline-secondary'}`}
                    onClick={() => handleTipo(false)}
                  >
                    <i className="bi bi-tag me-1" />
                    Avulso
                  </button>
                </div>
              </div>

              {form.isFixedAccount ? (
                <div>
                  <label className="form-label text-secondary small">Conta Fixa *</label>
                  <select
                    className="form-select bg-dark-subtle text-white border-secondary"
                    value={form.fixedAccountId}
                    onChange={(e) => setForm({ ...form, fixedAccountId: e.target.value })}
                    required
                  >
                    <option value="">Selecione uma conta...</option>
                    {contasFixas.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}{c.description ? ` — ${c.description}` : ''}
                      </option>
                    ))}
                  </select>
                </div>
              ) : (
                <div>
                  <label className="form-label text-secondary small">Descrição *</label>
                  <input
                    type="text"
                    className="form-control bg-dark-subtle text-white border-secondary"
                    value={form.description}
                    onChange={(e) => setForm({ ...form, description: e.target.value })}
                    placeholder="Ex: Mercado, Farmácia..."
                    required
                  />
                </div>
              )}

              <div>
                <label className="form-label text-secondary small">Valor (R$) *</label>
                <input
                  type="text"
                  inputMode="decimal"
                  className="form-control bg-dark-subtle text-white border-secondary"
                  value={form.amount}
                  onChange={(e) => setForm({ ...form, amount: e.target.value })}
                  placeholder="0,00"
                  required
                />
              </div>

              <div className="row g-2">
                <div className="col-7">
                  <label className="form-label text-secondary small">Mês *</label>
                  <select
                    className="form-select bg-dark-subtle text-white border-secondary"
                    value={form.month}
                    onChange={(e) => setForm({ ...form, month: Number(e.target.value) })}
                    required
                  >
                    {MESES.map((m) => (
                      <option key={m.valor} value={m.valor}>{m.label}</option>
                    ))}
                  </select>
                </div>
                <div className="col-5">
                  <label className="form-label text-secondary small">Ano *</label>
                  <select
                    className="form-select bg-dark-subtle text-white border-secondary"
                    value={form.year}
                    onChange={(e) => setForm({ ...form, year: Number(e.target.value) })}
                    required
                  >
                    {anos.map((a) => (
                      <option key={a} value={a}>{a}</option>
                    ))}
                  </select>
                </div>
              </div>

            </div>

            <div className="modal-footer border-secondary">
              <button type="button" className="btn btn-outline-secondary" onClick={onFechar}>
                Cancelar
              </button>
              <button type="submit" className="btn btn-primary" disabled={salvando || !formValido}>
                {salvando ? (
                  <>
                    <span className="spinner-border spinner-border-sm me-2" />
                    Salvando...
                  </>
                ) : (
                  'Salvar'
                )}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}

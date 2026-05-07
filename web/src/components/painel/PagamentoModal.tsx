'use client';

import { useEffect, useState } from 'react';
import { contasFixasService, ContaFixa } from '@/services/contasFixasService';
import { pagamentosService } from '@/services/pagamentosService';

interface Props {
  aberto: boolean;
  mes: number;
  ano: number;
  onFechar: () => void;
  onSalvo: () => void;
}

const estadoInicial = {
  isFixedAccount: true,
  fixedAccountId: '',
  description: '',
  amount: '',
};

export default function PagamentoModal({ aberto, mes, ano, onFechar, onSalvo }: Props) {
  const [form, setForm] = useState(estadoInicial);
  const [contasFixas, setContasFixas] = useState<ContaFixa[]>([]);
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!aberto) return;
    setForm(estadoInicial);
    setErro(null);
    contasFixasService.listar().then((lista) =>
      setContasFixas(lista.filter((c) => c.active))
    );
  }, [aberto]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);

    const amount = parseFloat(form.amount.replace(',', '.'));
    if (isNaN(amount) || amount <= 0) {
      setErro('Informe um valor válido.');
      return;
    }

    if (form.isFixedAccount && !form.fixedAccountId) {
      setErro('Selecione a conta fixa.');
      return;
    }

    if (!form.isFixedAccount && !form.description.trim()) {
      setErro('Informe a descrição do pagamento.');
      return;
    }

    const contaSelecionada = form.isFixedAccount
      ? contasFixas.find((c) => c.id === form.fixedAccountId)
      : null;

    setSalvando(true);
    try {
      await pagamentosService.criar({
        isFixedAccount: form.isFixedAccount,
        fixedAccountId: form.isFixedAccount ? form.fixedAccountId : undefined,
        description: form.isFixedAccount ? (contaSelecionada?.name ?? '') : form.description.trim(),
        amount,
        month: mes,
        year: ano,
      });
      onSalvo();
      onFechar();
    } catch {
      setErro('Erro ao registrar pagamento. Tente novamente.');
    } finally {
      setSalvando(false);
    }
  }

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
              <i className="bi bi-plus-circle me-2 text-primary" />
              Registrar Pagamento
            </h5>
            <button type="button" className="btn-close btn-close-white" onClick={onFechar} />
          </div>

          <form onSubmit={handleSubmit}>
            <div className="modal-body d-flex flex-column gap-3">
              <div className="p-2 rounded border border-secondary bg-dark-subtle text-secondary small">
                <i className="bi bi-calendar3 me-1" />
                Mês de referência:{' '}
                <strong className="text-white">
                  {String(mes).padStart(2, '0')}/{ano}
                </strong>
              </div>

              <div>
                <label className="form-label text-secondary small mb-2">Tipo de pagamento</label>
                <div className="d-flex gap-2">
                  <button
                    type="button"
                    className={`btn btn-sm flex-fill ${form.isFixedAccount ? 'btn-primary' : 'btn-outline-secondary'}`}
                    onClick={() => setForm({ ...estadoInicial, isFixedAccount: true })}
                  >
                    <i className="bi bi-receipt-cutoff me-1" />
                    Conta Fixa
                  </button>
                  <button
                    type="button"
                    className={`btn btn-sm flex-fill ${!form.isFixedAccount ? 'btn-primary' : 'btn-outline-secondary'}`}
                    onClick={() => setForm({ ...estadoInicial, isFixedAccount: false })}
                  >
                    <i className="bi bi-tag me-1" />
                    Outra Despesa
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
                    autoFocus
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
                    autoFocus
                  />
                </div>
              )}

              <div>
                <label className="form-label text-secondary small">Valor Pago (R$) *</label>
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

              {erro && (
                <div className="alert alert-danger py-2 mb-0 small d-flex align-items-center gap-2">
                  <i className="bi bi-exclamation-triangle-fill" />
                  {erro}
                </div>
              )}
            </div>

            <div className="modal-footer border-secondary">
              <button type="button" className="btn btn-outline-secondary" onClick={onFechar}>
                Cancelar
              </button>
              <button type="submit" className="btn btn-primary" disabled={salvando}>
                {salvando ? (
                  <>
                    <span className="spinner-border spinner-border-sm me-2" />
                    Salvando...
                  </>
                ) : (
                  <>
                    <i className="bi bi-check-lg me-1" />
                    Registrar
                  </>
                )}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}

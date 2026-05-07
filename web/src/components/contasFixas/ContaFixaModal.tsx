'use client';

import { useEffect, useState } from 'react';
import { ContaFixa, CriarContaFixaRequest, AtualizarContaFixaRequest } from '@/services/contasFixasService';

interface Props {
  aberto: boolean;
  contaEditando: ContaFixa | null;
  salvando: boolean;
  onFechar: () => void;
  onSalvar: (dados: CriarContaFixaRequest | AtualizarContaFixaRequest) => void;
}

const estadoInicial = {
  name: '',
  description: '',
  isRequiredMonthly: false,
  active: true,
};

export default function ContaFixaModal({ aberto, contaEditando, salvando, onFechar, onSalvar }: Props) {
  const [form, setForm] = useState(estadoInicial);

  useEffect(() => {
    if (contaEditando) {
      setForm({
        name: contaEditando.name,
        description: contaEditando.description ?? '',
        isRequiredMonthly: contaEditando.isRequiredMonthly,
        active: contaEditando.active,
      });
    } else {
      setForm(estadoInicial);
    }
  }, [contaEditando, aberto]);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    onSalvar({
      name: form.name.trim(),
      description: form.description.trim() || undefined,
      isRequiredMonthly: form.isRequiredMonthly,
      active: form.active,
    });
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
              {contaEditando ? 'Editar Conta Fixa' : 'Nova Conta Fixa'}
            </h5>
            <button type="button" className="btn-close btn-close-white" onClick={onFechar} />
          </div>

          <form onSubmit={handleSubmit}>
            <div className="modal-body">
              <div className="mb-3">
                <label className="form-label text-secondary small">Nome *</label>
                <input
                  type="text"
                  className="form-control bg-dark-subtle text-white border-secondary"
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                  placeholder="Ex: Energia Elétrica"
                  required
                  autoFocus
                />
              </div>

              <div className="mb-3">
                <label className="form-label text-secondary small">Descrição</label>
                <input
                  type="text"
                  className="form-control bg-dark-subtle text-white border-secondary"
                  value={form.description}
                  onChange={(e) => setForm({ ...form, description: e.target.value })}
                  placeholder="Opcional"
                />
              </div>

              <div className="mb-3 d-flex flex-column gap-2">
                <div className="form-check form-switch">
                  <input
                    className="form-check-input"
                    type="checkbox"
                    id="isRequiredMonthly"
                    checked={form.isRequiredMonthly}
                    onChange={(e) => setForm({ ...form, isRequiredMonthly: e.target.checked })}
                  />
                  <label className="form-check-label text-secondary" htmlFor="isRequiredMonthly">
                    Obrigatória mensalmente
                  </label>
                </div>

                <div className="form-check form-switch">
                  <input
                    className="form-check-input"
                    type="checkbox"
                    id="active"
                    checked={form.active}
                    onChange={(e) => setForm({ ...form, active: e.target.checked })}
                  />
                  <label className="form-check-label text-secondary" htmlFor="active">
                    Ativa
                  </label>
                </div>
              </div>
            </div>

            <div className="modal-footer border-secondary">
              <button type="button" className="btn btn-outline-secondary" onClick={onFechar}>
                Cancelar
              </button>
              <button type="submit" className="btn btn-primary" disabled={salvando || !form.name.trim()}>
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

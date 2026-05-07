'use client';

import { ContaFixa } from '@/services/contasFixasService';

interface Props {
  contas: ContaFixa[];
  onEditar: (conta: ContaFixa) => void;
  onRemover: (conta: ContaFixa) => void;
}

export default function ContasFixasTabela({ contas, onEditar, onRemover }: Props) {
  if (contas.length === 0) {
    return (
      <div className="text-center py-5 text-secondary">
        <i className="bi bi-inbox fs-1 d-block mb-2" />
        Nenhuma conta fixa cadastrada.
      </div>
    );
  }

  return (
    <div className="table-responsive">
      <table className="table table-dark table-hover align-middle mb-0">
        <thead>
          <tr className="border-secondary text-secondary small text-uppercase">
            <th>Nome</th>
            <th>Descrição</th>
            <th>Obrigatória</th>
            <th>Status</th>
            <th className="text-end">Ações</th>
          </tr>
        </thead>
        <tbody>
          {contas.map((conta) => (
            <tr key={conta.id} className="border-secondary">
              <td className="text-white fw-medium">{conta.name}</td>
              <td className="text-secondary">{conta.description || '—'}</td>
              <td>
                {conta.isRequiredMonthly ? (
                  <span className="badge bg-warning text-dark">
                    <i className="bi bi-star-fill me-1" />
                    Sim
                  </span>
                ) : (
                  <span className="badge bg-secondary">Não</span>
                )}
              </td>
              <td>
                {conta.active ? (
                  <span className="badge bg-success">
                    <i className="bi bi-check-circle me-1" />
                    Ativa
                  </span>
                ) : (
                  <span className="badge bg-danger">
                    <i className="bi bi-x-circle me-1" />
                    Inativa
                  </span>
                )}
              </td>
              <td className="text-end">
                <button
                  className="btn btn-sm btn-outline-secondary me-2"
                  onClick={() => onEditar(conta)}
                  title="Editar"
                >
                  <i className="bi bi-pencil" />
                </button>
                <button
                  className="btn btn-sm btn-outline-danger"
                  onClick={() => onRemover(conta)}
                  title="Remover"
                >
                  <i className="bi bi-trash" />
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

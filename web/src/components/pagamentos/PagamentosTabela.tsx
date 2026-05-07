'use client';

import { Pagamento } from '@/services/pagamentosService';

interface Props {
  pagamentos: Pagamento[];
  onEditar: (p: Pagamento) => void;
  onRemover: (p: Pagamento) => void;
}

const MESES = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];

export default function PagamentosTabela({ pagamentos, onEditar, onRemover }: Props) {
  if (pagamentos.length === 0) {
    return (
      <div className="text-center py-5 text-secondary">
        <i className="bi bi-inbox fs-1 d-block mb-2" />
        Nenhum pagamento encontrado para este período.
      </div>
    );
  }

  const total = pagamentos.reduce((acc, p) => acc + p.amount, 0);

  return (
    <>
      <div className="table-responsive">
        <table className="table table-dark table-hover align-middle mb-0">
          <thead>
            <tr className="border-secondary text-secondary small text-uppercase">
              <th>Descrição</th>
              <th>Tipo</th>
              <th>Conta Vinculada</th>
              <th>Período</th>
              <th className="text-end">Valor</th>
              <th className="text-end">Ações</th>
            </tr>
          </thead>
          <tbody>
            {pagamentos.map((p) => (
              <tr key={p.id} className="border-secondary">
                <td className="text-white fw-medium">{p.description}</td>
                <td>
                  {p.isFixedAccount ? (
                    <span className="badge bg-primary bg-opacity-75">
                      <i className="bi bi-receipt-cutoff me-1" />
                      Fixa
                    </span>
                  ) : (
                    <span className="badge bg-secondary">
                      <i className="bi bi-tag me-1" />
                      Avulso
                    </span>
                  )}
                </td>
                <td className="text-secondary">
                  {p.fixedAccountName ?? <span className="text-secondary fst-italic">—</span>}
                </td>
                <td className="text-secondary">
                  {MESES[p.month - 1]}/{p.year}
                </td>
                <td className="text-end text-success fw-semibold">
                  {p.amount.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                </td>
                <td className="text-end">
                  <button
                    className="btn btn-sm btn-outline-secondary me-2"
                    onClick={() => onEditar(p)}
                    title="Editar"
                  >
                    <i className="bi bi-pencil" />
                  </button>
                  <button
                    className="btn btn-sm btn-outline-danger"
                    onClick={() => onRemover(p)}
                    title="Remover"
                  >
                    <i className="bi bi-trash" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-secondary border-top">
              <td colSpan={4} className="text-secondary small pt-3">
                {pagamentos.length} {pagamentos.length === 1 ? 'registro' : 'registros'}
              </td>
              <td className="text-end text-white fw-bold pt-3">
                {total.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
              </td>
              <td />
            </tr>
          </tfoot>
        </table>
      </div>
    </>
  );
}

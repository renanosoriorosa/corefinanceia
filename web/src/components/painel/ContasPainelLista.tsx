'use client';

import { ContaPainel } from '@/services/painelService';

interface Props {
  contas: ContaPainel[];
  tipo: 'pagas' | 'pendentes';
}

export default function ContasPainelLista({ contas, tipo }: Props) {
  if (contas.length === 0) {
    return (
      <p className="text-secondary small mb-0 py-2">
        {tipo === 'pagas' ? 'Nenhuma conta paga ainda.' : 'Nenhuma pendência. '}
        {tipo === 'pendentes' && <span className="text-success">Tudo em dia!</span>}
      </p>
    );
  }

  const isPendente = tipo === 'pendentes';

  return (
    <ul className="list-unstyled mb-0">
      {contas.map((conta) => (
        <li
          key={conta.contaFixaId}
          className={`d-flex align-items-center justify-content-between py-2 border-bottom border-secondary border-opacity-25`}
        >
          <div className="d-flex align-items-center gap-2">
            <i
              className={`bi ${isPendente ? 'bi-clock text-warning' : 'bi-check-circle-fill text-success'}`}
            />
            <div>
              <div className="text-white small fw-medium">{conta.nome}</div>
              {conta.descricao && (
                <div className="text-secondary" style={{ fontSize: '0.75rem' }}>
                  {conta.descricao}
                </div>
              )}
            </div>
          </div>
          <div className="text-end">
            {conta.valorPago !== undefined && conta.valorPago !== null ? (
              <span className="text-success small fw-semibold">
                {conta.valorPago.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
              </span>
            ) : (
              <span className="badge bg-warning text-dark small">Pendente</span>
            )}
          </div>
        </li>
      ))}
    </ul>
  );
}

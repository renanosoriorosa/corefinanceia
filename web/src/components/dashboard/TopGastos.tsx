'use client';

import { TopGasto } from '@/services/dashboardService';
import { NOMES_MESES, formatarBRL } from './format';

interface Props {
  gastos: TopGasto[];
  maiorValor: number;
}

export default function TopGastos({ gastos, maiorValor }: Props) {
  if (gastos.length === 0) {
    return (
      <div className="text-center py-5 text-secondary small">
        <i className="bi bi-receipt d-block fs-3 mb-2 opacity-50" />
        Nenhum lançamento no período.
      </div>
    );
  }

  return (
    <ul className="list-unstyled mb-0">
      {gastos.map((gasto, i) => (
        <li key={gasto.id} className="d-flex align-items-center gap-3 py-2 border-bottom border-secondary border-opacity-25">
          <span
            className="badge bg-primary bg-opacity-10 text-primary flex-shrink-0"
            style={{ width: 26 }}
          >
            {i + 1}
          </span>

          <div className="flex-grow-1" style={{ minWidth: 0 }}>
            <div className="text-white small text-truncate">{gasto.descricao}</div>
            <div className="text-secondary" style={{ fontSize: 11 }}>
              {gasto.conta} · {NOMES_MESES[gasto.mes - 1]}
            </div>
            <div className="bg-secondary rounded mt-1" style={{ height: 4 }}>
              <div
                className="rounded"
                style={{
                  height: 4,
                  width: `${maiorValor > 0 ? (gasto.valor / maiorValor) * 100 : 0}%`,
                  backgroundColor: '#3b82f6',
                  transition: 'width 0.4s ease',
                }}
              />
            </div>
          </div>

          <span className="text-white fw-semibold small flex-shrink-0">{formatarBRL(gasto.valor)}</span>
        </li>
      ))}
    </ul>
  );
}

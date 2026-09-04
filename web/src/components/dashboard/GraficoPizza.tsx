'use client';

import { PieChart, Pie, Cell, Tooltip, ResponsiveContainer } from 'recharts';
import { ContaValor } from '@/services/dashboardService';
import { formatarBRL } from './format';

interface Props {
  contas: ContaValor[];
}

const CORES = ['#3b82f6', '#22d3ee', '#a78bfa', '#f59e0b', '#4ade80', '#f87171', '#94a3b8'];

function TooltipCustom({ active, payload }: { active?: boolean; payload?: { payload: ContaValor }[] }) {
  if (!active || !payload?.length) return null;

  const { nome, total, percentual } = payload[0].payload;

  return (
    <div
      style={{
        backgroundColor: '#1f2937',
        border: '1px solid #374151',
        borderRadius: 8,
        padding: '10px 14px',
      }}
    >
      <p style={{ color: '#e5e7eb', margin: 0, fontSize: 12 }}>{nome}</p>
      <p style={{ color: '#60a5fa', margin: '4px 0 0', fontWeight: 600, fontSize: 15 }}>
        {formatarBRL(total)}
      </p>
      <p style={{ color: '#9ca3af', margin: '2px 0 0', fontSize: 12 }}>
        {percentual.toFixed(1).replace('.', ',')}% do total
      </p>
    </div>
  );
}

export default function GraficoPizza({ contas }: Props) {
  if (contas.length === 0) {
    return (
      <div className="text-center py-5 text-secondary small">
        <i className="bi bi-pie-chart d-block fs-3 mb-2 opacity-50" />
        Nenhum lançamento no período.
      </div>
    );
  }

  return (
    <div className="row align-items-center g-3">
      <div className="col-lg-5">
        <ResponsiveContainer width="100%" height={220}>
          <PieChart>
            <Pie
              data={contas}
              dataKey="total"
              nameKey="nome"
              cx="50%"
              cy="50%"
              innerRadius={52}
              outerRadius={90}
              paddingAngle={2}
              stroke="#111827"
              strokeWidth={2}
            >
              {contas.map((_, i) => (
                <Cell key={i} fill={CORES[i % CORES.length]} />
              ))}
            </Pie>
            <Tooltip content={<TooltipCustom />} />
          </PieChart>
        </ResponsiveContainer>
      </div>

      <div className="col-lg-7">
        <ul className="list-unstyled mb-0">
          {contas.map((conta, i) => (
            <li key={conta.nome} className="d-flex align-items-center gap-2 py-1">
              <span
                className="rounded-circle flex-shrink-0"
                style={{ width: 10, height: 10, backgroundColor: CORES[i % CORES.length] }}
              />
              <span className="text-white small text-truncate flex-grow-1">{conta.nome}</span>
              <span className="text-secondary small">{conta.percentual.toFixed(1).replace('.', ',')}%</span>
              <span className="text-white small fw-semibold" style={{ minWidth: 96, textAlign: 'right' }}>
                {formatarBRL(conta.total)}
              </span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}

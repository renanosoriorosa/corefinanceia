'use client';

import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Cell,
} from 'recharts';
import { MesValor } from '@/services/dashboardService';

interface Props {
  meses: MesValor[];
  maiorMes: number;
}

const NOMES_MESES = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];

function formatarBRL(valor: number) {
  return valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
}

function TooltipCustom({ active, payload, label }: { active?: boolean; payload?: { value: number }[]; label?: string }) {
  if (!active || !payload?.length) return null;
  return (
    <div
      style={{
        backgroundColor: '#1f2937',
        border: '1px solid #374151',
        borderRadius: 8,
        padding: '10px 14px',
      }}
    >
      <p style={{ color: '#9ca3af', margin: 0, fontSize: 12 }}>{label}</p>
      <p style={{ color: '#60a5fa', margin: '4px 0 0', fontWeight: 600, fontSize: 15 }}>
        {formatarBRL(payload[0].value)}
      </p>
    </div>
  );
}

export default function GraficoBarras({ meses, maiorMes }: Props) {
  const dados = meses.map((m) => ({
    mes: NOMES_MESES[m.mes - 1],
    total: m.total,
  }));

  return (
    <ResponsiveContainer width="100%" height={320}>
      <BarChart data={dados} margin={{ top: 8, right: 16, left: 8, bottom: 0 }} barCategoryGap="30%">
        <CartesianGrid strokeDasharray="3 3" stroke="#1f2937" vertical={false} />
        <XAxis
          dataKey="mes"
          tick={{ fill: '#6b7280', fontSize: 12 }}
          axisLine={false}
          tickLine={false}
        />
        <YAxis
          tickFormatter={(v) => `R$ ${(v / 1000).toFixed(0)}k`}
          tick={{ fill: '#6b7280', fontSize: 11 }}
          axisLine={false}
          tickLine={false}
          width={60}
        />
        <Tooltip content={<TooltipCustom />} cursor={{ fill: 'rgba(255,255,255,0.04)' }} />
        <Bar dataKey="total" radius={[6, 6, 0, 0]} maxBarSize={52}>
          {dados.map((d, i) => (
            <Cell
              key={i}
              fill={d.total === maiorMes && maiorMes > 0 ? '#3b82f6' : '#1d4ed8'}
              fillOpacity={d.total === 0 ? 0.2 : 1}
            />
          ))}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}

'use client';

import {
  ComposedChart,
  Bar,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ReferenceLine,
  Legend,
  ResponsiveContainer,
  Cell,
} from 'recharts';
import { MesValor } from '@/services/dashboardService';
import { NOMES_MESES, formatarBRL } from './format';

interface Props {
  meses: MesValor[];
  maiorMes: number;
  media: number;
}

interface PontoDoGrafico {
  mes: string;
  total: number;
  projetado: number | null;
  mediaMovel: number | null;
}

const COR_BARRA = '#1d4ed8';
const COR_MAIOR_MES = '#3b82f6';
const COR_MEDIA = '#f59e0b';
const COR_MEDIA_MOVEL = '#22d3ee';

function TooltipCustom({
  active,
  payload,
  label,
  media,
}: {
  active?: boolean;
  payload?: { payload: PontoDoGrafico }[];
  label?: string;
  media: number;
}) {
  if (!active || !payload?.length) return null;

  const ponto = payload[0].payload;
  const diferenca = ponto.total - media;
  const acimaDaMedia = diferenca > 0;

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

      {ponto.projetado !== null ? (
        <p style={{ color: '#9ca3af', margin: '4px 0 0', fontWeight: 600, fontSize: 15 }}>
          {formatarBRL(ponto.projetado)} <span style={{ fontSize: 11, fontWeight: 400 }}>projetado</span>
        </p>
      ) : (
        <>
          <p style={{ color: '#60a5fa', margin: '4px 0 0', fontWeight: 600, fontSize: 15 }}>
            {formatarBRL(ponto.total)}
          </p>
          {media > 0 && ponto.total > 0 && (
            <p style={{ color: acimaDaMedia ? '#f87171' : '#4ade80', margin: '2px 0 0', fontSize: 12 }}>
              {formatarBRL(Math.abs(diferenca))} {acimaDaMedia ? 'acima' : 'abaixo'} da média
            </p>
          )}
        </>
      )}

      {ponto.mediaMovel !== null && (
        <p style={{ color: COR_MEDIA_MOVEL, margin: '2px 0 0', fontSize: 12 }}>
          Média móvel: {formatarBRL(ponto.mediaMovel)}
        </p>
      )}
    </div>
  );
}

export default function GraficoBarras({ meses, maiorMes, media }: Props) {
  const dados: PontoDoGrafico[] = meses.map((m) => ({
    mes: NOMES_MESES[m.mes - 1],
    total: m.total,
    projetado: m.projetado ?? null,
    mediaMovel: m.mediaMovel ?? null,
  }));

  const temProjecao = dados.some((d) => d.projetado !== null);
  const temMediaMovel = dados.some((d) => d.mediaMovel !== null);

  return (
    <ResponsiveContainer width="100%" height={340}>
      {/* A margem à direita reserva espaço para o rótulo da linha de média. */}
      <ComposedChart data={dados} margin={{ top: 8, right: 104, left: 8, bottom: 0 }} barCategoryGap="30%">
        <CartesianGrid strokeDasharray="3 3" stroke="#1f2937" vertical={false} />
        <XAxis
          dataKey="mes"
          tick={{ fill: '#6b7280', fontSize: 12 }}
          axisLine={false}
          tickLine={false}
        />
        <YAxis
          tickFormatter={(v) => `R$ ${(v / 1000).toFixed(0)}k`}
          tick={{ fill: '#6b7280', fontSize: 11 }}
          axisLine={false}
          tickLine={false}
          width={60}
        />
        <Tooltip content={<TooltipCustom media={media} />} cursor={{ fill: 'rgba(255,255,255,0.04)' }} />
        <Legend
          wrapperStyle={{ fontSize: 12, color: '#9ca3af', paddingTop: 8 }}
          iconType="plainline"
          iconSize={14}
        />

        {/* Barra real e barra projetada compartilham a mesma pilha para ocuparem o mesmo espaço no mês. */}
        <Bar dataKey="total" name="Gasto do mês" stackId="valor" radius={[6, 6, 0, 0]} maxBarSize={52}>
          {dados.map((d, i) => (
            <Cell
              key={i}
              fill={d.total === maiorMes && maiorMes > 0 ? COR_MAIOR_MES : COR_BARRA}
              fillOpacity={d.total === 0 ? 0.2 : 1}
            />
          ))}
        </Bar>

        {temProjecao && (
          <Bar
            dataKey="projetado"
            name="Projeção"
            stackId="valor"
            radius={[6, 6, 0, 0]}
            maxBarSize={52}
            fill={COR_BARRA}
            fillOpacity={0.25}
            stroke={COR_MAIOR_MES}
            strokeDasharray="4 3"
          />
        )}

        {temMediaMovel && (
          <Line
            type="monotone"
            dataKey="mediaMovel"
            name="Média móvel (3 meses)"
            stroke={COR_MEDIA_MOVEL}
            strokeWidth={2}
            dot={{ r: 3, fill: COR_MEDIA_MOVEL, strokeWidth: 0 }}
            activeDot={{ r: 5 }}
            connectNulls
          />
        )}

        {media > 0 && (
          <ReferenceLine
            y={media}
            stroke={COR_MEDIA}
            strokeDasharray="4 4"
            strokeWidth={2}
            ifOverflow="extendDomain"
            label={{
              value: `Média ${formatarBRL(media)}`,
              position: 'right',
              fill: COR_MEDIA,
              fontSize: 11,
            }}
          />
        )}
      </ComposedChart>
    </ResponsiveContainer>
  );
}

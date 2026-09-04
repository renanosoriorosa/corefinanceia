'use client';

import { Comparativo } from '@/services/dashboardService';
import { NOMES_MESES, formatarBRL, formatarPercentual } from './format';

interface Props {
  comparativo: Comparativo;
  ano: number;
}

/** Gastar menos que o período anterior é bom, então a variação negativa aparece em verde. */
function BadgeVariacao({ variacao }: { variacao?: number | null }) {
  if (variacao === null || variacao === undefined) {
    return <span className="badge bg-secondary bg-opacity-25 text-secondary">sem base</span>;
  }

  const subiu = variacao > 0;
  const classe = subiu ? 'bg-danger bg-opacity-10 text-danger' : 'bg-success bg-opacity-10 text-success';
  const icone = subiu ? 'bi-arrow-up-right' : 'bi-arrow-down-right';

  return (
    <span className={`badge ${classe} d-inline-flex align-items-center gap-1`}>
      <i className={`bi ${icone}`} />
      {formatarPercentual(variacao)}
    </span>
  );
}

function Card({
  icone,
  cor,
  titulo,
  valor,
  referencia,
  variacao,
}: {
  icone: string;
  cor: string;
  titulo: string;
  valor: number;
  referencia: string;
  variacao?: number | null;
}) {
  return (
    <div className="col-md-6">
      <div className="card bg-dark border-secondary h-100">
        <div className="card-body d-flex align-items-center gap-3">
          <div
            className={`rounded-circle d-flex align-items-center justify-content-center bg-${cor} bg-opacity-10 flex-shrink-0`}
            style={{ width: 48, height: 48 }}
          >
            <i className={`bi ${icone} text-${cor} fs-5`} />
          </div>

          <div className="flex-grow-1" style={{ minWidth: 0 }}>
            <div className="d-flex align-items-center gap-2 mb-1">
              <span className="text-white fw-bold fs-6">{formatarBRL(valor)}</span>
              <BadgeVariacao variacao={variacao} />
            </div>
            <div className="text-secondary small">{titulo}</div>
            <div className="text-secondary text-truncate" style={{ fontSize: 11 }}>
              {referencia}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default function ComparativoCards({ comparativo, ano }: Props) {
  const mes = NOMES_MESES[comparativo.mesReferencia - 1];
  const mesAnterior = comparativo.mesReferencia > 1 ? NOMES_MESES[comparativo.mesReferencia - 2] : 'Dez';

  return (
    <div className="row g-3 mb-4">
      <Card
        icone="bi-calendar-range"
        cor="primary"
        titulo={`${mes} vs ${mesAnterior}`}
        valor={comparativo.totalMesReferencia}
        referencia={`${mesAnterior}: ${formatarBRL(comparativo.totalMesAnterior)}`}
        variacao={comparativo.variacaoMensalPercentual}
      />

      <Card
        icone="bi-arrow-left-right"
        cor="info"
        titulo={`Acumulado até ${mes} vs ${ano - 1}`}
        valor={comparativo.totalAcumuladoAno}
        referencia={`${ano - 1} no mesmo período: ${formatarBRL(comparativo.totalAcumuladoAnoAnterior)}`}
        variacao={comparativo.variacaoAnualPercentual}
      />
    </div>
  );
}

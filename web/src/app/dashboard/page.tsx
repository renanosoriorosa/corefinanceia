'use client';

import { useEffect, useState } from 'react';
import { useDashboard } from '@/hooks/useDashboard';
import { contasFixasService, ContaFixa } from '@/services/contasFixasService';
import GraficoBarras from '@/components/dashboard/GraficoBarras';
import GraficoPizza from '@/components/dashboard/GraficoPizza';
import TopGastos from '@/components/dashboard/TopGastos';
import ComparativoCards from '@/components/dashboard/ComparativoCards';
import { NOMES_MESES, formatarBRL } from '@/components/dashboard/format';

export default function DashboardPage() {
  const anoAtual = new Date().getFullYear();
  const anos = Array.from({ length: 5 }, (_, i) => anoAtual - 2 + i);

  const [ano, setAno] = useState(anoAtual);
  const [contaFixaId, setContaFixaId] = useState('');
  const [incluirNaoFixas, setIncluirNaoFixas] = useState(true);
  const [contasFixas, setContasFixas] = useState<ContaFixa[]>([]);

  const { dados, carregando, erro, carregar } = useDashboard();

  useEffect(() => {
    contasFixasService.listar().then((lista) => setContasFixas(lista.filter((c) => c.active)));
  }, []);

  useEffect(() => {
    carregar({ ano, contaFixaId: contaFixaId || undefined, incluirNaoFixas });
  }, [ano, contaFixaId, incluirNaoFixas, carregar]);

  const anoEmAndamento = ano === anoAtual;

  return (
    <>
      <div className="d-flex align-items-center justify-content-between mb-4">
        <div>
          <h4 className="text-white mb-1 fw-semibold">
            <i className="bi bi-bar-chart-line me-2 text-primary" />
            Dashboard Financeiro
          </h4>
          <p className="text-secondary mb-0 small">Visão anual dos seus gastos</p>
        </div>
      </div>

      {/* Filtros */}
      <div className="card bg-dark border-secondary mb-4">
        <div className="card-body py-3">
          <div className="d-flex align-items-center gap-3 flex-wrap">
            <span className="text-secondary small">
              <i className="bi bi-funnel me-1" />
              Filtros:
            </span>

            <div className="d-flex align-items-center gap-2">
              <label className="text-secondary small mb-0">Ano</label>
              <select
                className="form-select form-select-sm bg-dark-subtle text-white border-secondary"
                style={{ width: 90 }}
                value={ano}
                onChange={(e) => setAno(Number(e.target.value))}
              >
                {anos.map((a) => <option key={a} value={a}>{a}</option>)}
              </select>
            </div>

            <div className="d-flex align-items-center gap-2">
              <label className="text-secondary small mb-0">Conta fixa</label>
              <select
                className="form-select form-select-sm bg-dark-subtle text-white border-secondary"
                style={{ width: 200 }}
                value={contaFixaId}
                onChange={(e) => setContaFixaId(e.target.value)}
              >
                <option value="">Todas</option>
                {contasFixas.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </div>

            <div className="form-check form-switch mb-0 d-flex align-items-center gap-2 ms-1">
              <input
                className="form-check-input"
                type="checkbox"
                id="incluirNaoFixas"
                checked={incluirNaoFixas}
                disabled={!!contaFixaId}
                onChange={(e) => setIncluirNaoFixas(e.target.checked)}
              />
              <label
                className={`form-check-label small ${contaFixaId ? 'text-secondary opacity-50' : 'text-secondary'}`}
                htmlFor="incluirNaoFixas"
              >
                Incluir despesas avulsas
              </label>
            </div>
          </div>
        </div>
      </div>

      {/* Cards resumo */}
      {dados && (
        <div className="row g-3 mb-4">
          <div className="col-sm-6 col-lg-3">
            <div className="card bg-dark border-secondary h-100">
              <div className="card-body d-flex align-items-center gap-3">
                <div className="rounded-circle d-flex align-items-center justify-content-center bg-primary bg-opacity-10 flex-shrink-0" style={{ width: 48, height: 48 }}>
                  <i className="bi bi-calendar-year text-primary fs-5" />
                </div>
                <div>
                  <div className="text-white fw-bold fs-6">{formatarBRL(dados.totalAno)}</div>
                  <div className="text-secondary small">Total {dados.ano}</div>
                </div>
              </div>
            </div>
          </div>

          <div className="col-sm-6 col-lg-3">
            <div className="card bg-dark border-secondary h-100">
              <div className="card-body d-flex align-items-center gap-3">
                <div className="rounded-circle d-flex align-items-center justify-content-center bg-success bg-opacity-10 flex-shrink-0" style={{ width: 48, height: 48 }}>
                  <i className="bi bi-graph-up text-success fs-5" />
                </div>
                <div>
                  <div className="text-white fw-bold fs-6">{formatarBRL(dados.maiorMes)}</div>
                  <div className="text-secondary small">Maior mês</div>
                </div>
              </div>
            </div>
          </div>

          <div className="col-sm-6 col-lg-3">
            <div className="card bg-dark border-secondary h-100">
              <div className="card-body d-flex align-items-center gap-3">
                <div className="rounded-circle d-flex align-items-center justify-content-center bg-info bg-opacity-10 flex-shrink-0" style={{ width: 48, height: 48 }}>
                  <i className="bi bi-calculator text-info fs-5" />
                </div>
                <div>
                  <div className="text-white fw-bold fs-6">{formatarBRL(dados.mediaMensal)}</div>
                  <div className="text-secondary small">Média mensal</div>
                </div>
              </div>
            </div>
          </div>

          <div className="col-sm-6 col-lg-3">
            <div className="card bg-dark border-secondary h-100">
              <div className="card-body d-flex align-items-center gap-3">
                <div className="rounded-circle d-flex align-items-center justify-content-center bg-warning bg-opacity-10 flex-shrink-0" style={{ width: 48, height: 48 }}>
                  <i className="bi bi-graph-up-arrow text-warning fs-5" />
                </div>
                <div>
                  <div className="text-white fw-bold fs-6">{formatarBRL(dados.projecaoAno)}</div>
                  <div className="text-secondary small">
                    {anoEmAndamento ? `Projeção ${dados.ano}` : `Fechamento ${dados.ano}`}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Comparativos */}
      {dados && <ComparativoCards comparativo={dados.comparativo} ano={dados.ano} />}

      {/* Gráfico */}
      <div className="card bg-dark border-secondary mb-4">
        <div className="card-header border-secondary d-flex align-items-center justify-content-between">
          <span className="text-white fw-medium">
            <i className="bi bi-bar-chart me-2 text-primary" />
            Gastos por mês — {ano}
          </span>
          {carregando && <div className="spinner-border spinner-border-sm text-primary" />}
        </div>
        <div className="card-body pt-3 pb-2">
          {erro ? (
            <div className="alert alert-danger d-flex align-items-center gap-2">
              <i className="bi bi-exclamation-triangle-fill" />
              {erro}
            </div>
          ) : dados ? (
            <GraficoBarras meses={dados.meses} maiorMes={dados.maiorMes} media={dados.mediaMensal} />
          ) : (
            <div className="text-center py-5 text-secondary">
              <div className="spinner-border text-primary mb-2" />
              <div className="small">Carregando...</div>
            </div>
          )}
        </div>
      </div>

      {/* Distribuição e maiores gastos */}
      {dados && (
        <div className="row g-3 mb-4">
          <div className="col-xl-7">
            <div className="card bg-dark border-secondary h-100">
              <div className="card-header border-secondary">
                <span className="text-white fw-medium">
                  <i className="bi bi-pie-chart me-2 text-primary" />
                  Distribuição por conta — {ano}
                </span>
              </div>
              <div className="card-body">
                <GraficoPizza contas={dados.porConta} />
              </div>
            </div>
          </div>

          <div className="col-xl-5">
            <div className="card bg-dark border-secondary h-100">
              <div className="card-header border-secondary">
                <span className="text-white fw-medium">
                  <i className="bi bi-trophy me-2 text-warning" />
                  Top 5 gastos — {ano}
                </span>
              </div>
              <div className="card-body">
                <TopGastos gastos={dados.topGastos} maiorValor={dados.topGastos[0]?.valor ?? 0} />
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Tabela detalhe mensal */}
      {dados && (
        <div className="card bg-dark border-secondary">
          <div className="card-header border-secondary">
            <span className="text-white fw-medium">
              <i className="bi bi-table me-2 text-secondary" />
              Detalhe por mês
            </span>
          </div>
          <div className="card-body p-0">
            <div className="table-responsive">
              <table className="table table-dark table-hover align-middle mb-0">
                <thead>
                  <tr className="border-secondary text-secondary small text-uppercase">
                    <th>Mês</th>
                    <th className="text-end">Total</th>
                    <th className="text-end">vs média</th>
                    <th>Proporção</th>
                  </tr>
                </thead>
                <tbody>
                  {dados.meses.map((m) => {
                    const proporcao = dados.maiorMes > 0 ? (m.total / dados.maiorMes) * 100 : 0;
                    const isMesAtual = m.mes === new Date().getMonth() + 1 && dados.ano === anoAtual;
                    const diferenca = m.total - dados.mediaMensal;

                    return (
                      <tr key={m.mes} className="border-secondary">
                        <td className={isMesAtual ? 'text-primary fw-semibold' : 'text-white'}>
                          {NOMES_MESES[m.mes - 1]}
                          {isMesAtual && <span className="badge bg-primary ms-2 small">atual</span>}
                        </td>
                        <td className={`text-end fw-semibold ${m.total === 0 ? 'text-secondary' : 'text-white'}`}>
                          {m.total > 0 ? formatarBRL(m.total) : '—'}
                        </td>
                        <td className={`text-end small ${diferenca > 0 ? 'text-danger' : 'text-success'}`}>
                          {m.total > 0 && dados.mediaMensal > 0
                            ? `${diferenca > 0 ? '+' : '−'}${formatarBRL(Math.abs(diferenca))}`
                            : <span className="text-secondary">—</span>}
                        </td>
                        <td style={{ width: '35%' }}>
                          <div className="d-flex align-items-center gap-2">
                            <div className="flex-grow-1 bg-secondary rounded" style={{ height: 6 }}>
                              <div
                                className="rounded"
                                style={{
                                  height: 6,
                                  width: `${proporcao}%`,
                                  backgroundColor: m.total === dados.maiorMes && m.total > 0 ? '#3b82f6' : '#1d4ed8',
                                  transition: 'width 0.4s ease',
                                }}
                              />
                            </div>
                            <span className="text-secondary small" style={{ minWidth: 36 }}>
                              {proporcao > 0 ? `${proporcao.toFixed(0)}%` : ''}
                            </span>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

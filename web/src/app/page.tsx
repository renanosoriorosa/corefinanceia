'use client';

import { useEffect, useState } from 'react';
import { usePainelMesAtual } from '@/hooks/usePainelMesAtual';
import ContasPainelLista from '@/components/painel/ContasPainelLista';
import PagamentoModal from '@/components/painel/PagamentoModal';

const MESES = [
  'Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
  'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro',
];

export default function PainelPage() {
  const { painel, carregando, erro, carregar } = usePainelMesAtual();
  const [modalPagamentoAberto, setModalPagamentoAberto] = useState(false);

  useEffect(() => {
    carregar();
  }, [carregar]);

  const mesLabel = painel ? `${MESES[painel.mes - 1]} ${painel.ano}` : '...';

  return (
    <>
      <div className="d-flex align-items-center justify-content-between mb-4">
        <div>
          <h4 className="text-white mb-1 fw-semibold">
            <i className="bi bi-calendar2-check me-2 text-primary" />
            Painel do Mês
          </h4>
          <p className="text-secondary mb-0 small">{mesLabel}</p>
        </div>
        <div className="d-flex gap-2">
          <button
            className="btn btn-primary btn-sm d-flex align-items-center gap-2"
            onClick={() => setModalPagamentoAberto(true)}
          >
            <i className="bi bi-plus-lg" />
            Registrar Pagamento
          </button>
          <button
            className="btn btn-outline-secondary btn-sm d-flex align-items-center gap-2"
            onClick={carregar}
            disabled={carregando}
          >
            <i className={`bi bi-arrow-clockwise ${carregando ? 'spin' : ''}`} />
            Atualizar
          </button>
        </div>
      </div>

      {erro && (
        <div className="alert alert-danger d-flex align-items-center gap-2 mb-3">
          <i className="bi bi-exclamation-triangle-fill" />
          {erro}
        </div>
      )}

      {carregando && !painel ? (
        <div className="text-center py-5 text-secondary">
          <div className="spinner-border text-primary mb-2" />
          <div className="small">Carregando painel...</div>
        </div>
      ) : painel ? (
        <>
          <div className="row g-3 mb-4">
            <div className="col-sm-6 col-lg-3">
              <div className="card bg-dark border-secondary h-100">
                <div className="card-body d-flex align-items-center gap-3">
                  <div
                    className="rounded-circle d-flex align-items-center justify-content-center bg-primary bg-opacity-10 flex-shrink-0"
                    style={{ width: 48, height: 48 }}
                  >
                    <i className="bi bi-cash-stack text-primary fs-5" />
                  </div>
                  <div>
                    <div className="text-white fw-bold fs-5">
                      {painel.totalPago.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                    </div>
                    <div className="text-secondary small">Total Pago no Mês</div>
                  </div>
                </div>
              </div>
            </div>

            <div className="col-sm-6 col-lg-3">
              <div className="card bg-dark border-secondary h-100">
                <div className="card-body d-flex align-items-center gap-3">
                  <div
                    className="rounded-circle d-flex align-items-center justify-content-center bg-success bg-opacity-10 flex-shrink-0"
                    style={{ width: 48, height: 48 }}
                  >
                    <i className="bi bi-check-circle text-success fs-5" />
                  </div>
                  <div>
                    <div className="text-white fw-bold fs-5">{painel.contasPagas.length}</div>
                    <div className="text-secondary small">Contas Pagas</div>
                  </div>
                </div>
              </div>
            </div>

            <div className="col-sm-6 col-lg-3">
              <div
                className={`card h-100 border-${painel.totalPendentes > 0 ? 'warning' : 'secondary'}`}
                style={{ backgroundColor: painel.totalPendentes > 0 ? 'rgba(255,193,7,0.07)' : undefined }}
              >
                <div className="card-body d-flex align-items-center gap-3">
                  <div
                    className="rounded-circle d-flex align-items-center justify-content-center bg-warning bg-opacity-10 flex-shrink-0"
                    style={{ width: 48, height: 48 }}
                  >
                    <i className="bi bi-clock text-warning fs-5" />
                  </div>
                  <div>
                    <div className={`fw-bold fs-5 ${painel.totalPendentes > 0 ? 'text-warning' : 'text-white'}`}>
                      {painel.totalPendentes}
                    </div>
                    <div className="text-secondary small">Pendências</div>
                  </div>
                </div>
              </div>
            </div>

            <div className="col-sm-6 col-lg-3">
              <div className="card bg-dark border-secondary h-100">
                <div className="card-body d-flex align-items-center gap-3">
                  <div
                    className="rounded-circle d-flex align-items-center justify-content-center bg-info bg-opacity-10 flex-shrink-0"
                    style={{ width: 48, height: 48 }}
                  >
                    <i className="bi bi-list-check text-info fs-5" />
                  </div>
                  <div>
                    <div className="text-white fw-bold fs-5">
                      {painel.contasPagas.length + painel.totalPendentes}
                    </div>
                    <div className="text-secondary small">Total Obrigatórias</div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div className="row g-3">
            <div className="col-lg-6">
              <div className="card bg-dark border-secondary h-100">
                <div className="card-header border-secondary d-flex align-items-center gap-2">
                  <i className="bi bi-clock text-warning" />
                  <span className="text-white fw-medium">Pendentes</span>
                  {painel.totalPendentes > 0 && (
                    <span className="badge bg-warning text-dark ms-auto">
                      {painel.totalPendentes}
                    </span>
                  )}
                </div>
                <div className="card-body">
                  <ContasPainelLista contas={painel.contasPendentes} tipo="pendentes" />
                </div>
              </div>
            </div>

            <div className="col-lg-6">
              <div className="card bg-dark border-secondary h-100">
                <div className="card-header border-secondary d-flex align-items-center gap-2">
                  <i className="bi bi-check-circle-fill text-success" />
                  <span className="text-white fw-medium">Pagas</span>
                  {painel.contasPagas.length > 0 && (
                    <span className="badge bg-success ms-auto">
                      {painel.contasPagas.length}
                    </span>
                  )}
                </div>
                <div className="card-body">
                  <ContasPainelLista contas={painel.contasPagas} tipo="pagas" />
                </div>
              </div>
            </div>
          </div>
        </>
      ) : null}

      <PagamentoModal
        aberto={modalPagamentoAberto}
        mes={painel?.mes ?? new Date().getMonth() + 1}
        ano={painel?.ano ?? new Date().getFullYear()}
        onFechar={() => setModalPagamentoAberto(false)}
        onSalvo={carregar}
      />
    </>
  );
}

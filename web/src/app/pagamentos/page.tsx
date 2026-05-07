'use client';

import { useEffect, useState } from 'react';
import { usePagamentos } from '@/hooks/usePagamentos';
import { Pagamento, CriarPagamentoRequest, AtualizarPagamentoRequest } from '@/services/pagamentosService';
import PagamentosTabela from '@/components/pagamentos/PagamentosTabela';
import PagamentoFormModal from '@/components/pagamentos/PagamentoFormModal';

const MESES = [
  { valor: 1, label: 'Janeiro' }, { valor: 2, label: 'Fevereiro' },
  { valor: 3, label: 'Março' }, { valor: 4, label: 'Abril' },
  { valor: 5, label: 'Maio' }, { valor: 6, label: 'Junho' },
  { valor: 7, label: 'Julho' }, { valor: 8, label: 'Agosto' },
  { valor: 9, label: 'Setembro' }, { valor: 10, label: 'Outubro' },
  { valor: 11, label: 'Novembro' }, { valor: 12, label: 'Dezembro' },
];

export default function PagamentosPage() {
  const {
    pagamentos, filtroMes, filtroAno, setFiltroMes, setFiltroAno,
    carregando, erro, listar, criar, atualizar, remover,
  } = usePagamentos();

  const [modalAberto, setModalAberto] = useState(false);
  const [pagamentoEditando, setPagamentoEditando] = useState<Pagamento | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [confirmandoRemocao, setConfirmandoRemocao] = useState<Pagamento | null>(null);
  const [removendo, setRemovendo] = useState(false);

  useEffect(() => {
    listar(filtroMes, filtroAno);
  }, [filtroMes, filtroAno, listar]);

  function abrirModalNovo() {
    setPagamentoEditando(null);
    setModalAberto(true);
  }

  function abrirModalEditar(p: Pagamento) {
    setPagamentoEditando(p);
    setModalAberto(true);
  }

  function fecharModal() {
    setModalAberto(false);
    setPagamentoEditando(null);
  }

  async function handleSalvar(dados: CriarPagamentoRequest | AtualizarPagamentoRequest) {
    setSalvando(true);
    let sucesso: boolean;
    if (pagamentoEditando) {
      sucesso = await atualizar(pagamentoEditando.id, dados as AtualizarPagamentoRequest);
    } else {
      sucesso = await criar(dados as CriarPagamentoRequest);
    }
    setSalvando(false);
    if (sucesso) fecharModal();
  }

  async function handleConfirmarRemocao() {
    if (!confirmandoRemocao) return;
    setRemovendo(true);
    await remover(confirmandoRemocao.id);
    setRemovendo(false);
    setConfirmandoRemocao(null);
  }

  const anoAtual = new Date().getFullYear();
  const anos = Array.from({ length: 5 }, (_, i) => anoAtual - 2 + i);

  const totalMes = pagamentos.reduce((acc, p) => acc + p.amount, 0);
  const qtdFixas = pagamentos.filter((p) => p.isFixedAccount).length;
  const qtdAvulsas = pagamentos.filter((p) => !p.isFixedAccount).length;

  return (
    <>
      <div className="d-flex align-items-center justify-content-between mb-4">
        <div>
          <h4 className="text-white mb-1 fw-semibold">
            <i className="bi bi-cash-coin me-2 text-primary" />
            Pagamentos
          </h4>
          <p className="text-secondary mb-0 small">Gerencie os pagamentos por período</p>
        </div>
        <button className="btn btn-primary d-flex align-items-center gap-2" onClick={abrirModalNovo}>
          <i className="bi bi-plus-lg" />
          Novo Pagamento
        </button>
      </div>

      {/* Filtro */}
      <div className="card bg-dark border-secondary mb-4">
        <div className="card-body py-3">
          <div className="d-flex align-items-center gap-3 flex-wrap">
            <span className="text-secondary small">
              <i className="bi bi-funnel me-1" />
              Filtrar por período:
            </span>
            <div className="d-flex gap-2 align-items-center">
              <select
                className="form-select form-select-sm bg-dark-subtle text-white border-secondary"
                style={{ width: 140 }}
                value={filtroMes}
                onChange={(e) => setFiltroMes(Number(e.target.value))}
              >
                {MESES.map((m) => (
                  <option key={m.valor} value={m.valor}>{m.label}</option>
                ))}
              </select>
              <select
                className="form-select form-select-sm bg-dark-subtle text-white border-secondary"
                style={{ width: 90 }}
                value={filtroAno}
                onChange={(e) => setFiltroAno(Number(e.target.value))}
              >
                {anos.map((a) => (
                  <option key={a} value={a}>{a}</option>
                ))}
              </select>
            </div>
            {!carregando && (
              <span className="text-secondary small ms-auto">
                {pagamentos.length} {pagamentos.length === 1 ? 'registro' : 'registros'}
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Cards resumo */}
      <div className="row g-3 mb-4">
        <div className="col-sm-4">
          <div className="card bg-dark border-secondary">
            <div className="card-body d-flex align-items-center gap-3">
              <div className="rounded-circle d-flex align-items-center justify-content-center bg-primary bg-opacity-10 flex-shrink-0" style={{ width: 44, height: 44 }}>
                <i className="bi bi-cash-stack text-primary" />
              </div>
              <div>
                <div className="text-white fw-bold">
                  {totalMes.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                </div>
                <div className="text-secondary small">Total do período</div>
              </div>
            </div>
          </div>
        </div>
        <div className="col-sm-4">
          <div className="card bg-dark border-secondary">
            <div className="card-body d-flex align-items-center gap-3">
              <div className="rounded-circle d-flex align-items-center justify-content-center bg-primary bg-opacity-10 flex-shrink-0" style={{ width: 44, height: 44 }}>
                <i className="bi bi-receipt-cutoff text-primary" />
              </div>
              <div>
                <div className="text-white fw-bold">{qtdFixas}</div>
                <div className="text-secondary small">Contas fixas</div>
              </div>
            </div>
          </div>
        </div>
        <div className="col-sm-4">
          <div className="card bg-dark border-secondary">
            <div className="card-body d-flex align-items-center gap-3">
              <div className="rounded-circle d-flex align-items-center justify-content-center bg-secondary bg-opacity-25 flex-shrink-0" style={{ width: 44, height: 44 }}>
                <i className="bi bi-tag text-secondary" />
              </div>
              <div>
                <div className="text-white fw-bold">{qtdAvulsas}</div>
                <div className="text-secondary small">Avulsos</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {erro && (
        <div className="alert alert-danger d-flex align-items-center gap-2 mb-3">
          <i className="bi bi-exclamation-triangle-fill" />
          {erro}
        </div>
      )}

      <div className="card bg-dark border-secondary">
        <div className="card-body p-0">
          {carregando ? (
            <div className="text-center py-5 text-secondary">
              <div className="spinner-border text-primary mb-2" />
              <div className="small">Carregando...</div>
            </div>
          ) : (
            <PagamentosTabela
              pagamentos={pagamentos}
              onEditar={abrirModalEditar}
              onRemover={setConfirmandoRemocao}
            />
          )}
        </div>
      </div>

      <PagamentoFormModal
        aberto={modalAberto}
        pagamentoEditando={pagamentoEditando}
        mesPadrao={filtroMes}
        anoPadrao={filtroAno}
        salvando={salvando}
        onFechar={fecharModal}
        onSalvar={handleSalvar}
      />

      {confirmandoRemocao && (
        <div
          className="modal d-block"
          style={{ backgroundColor: 'rgba(0,0,0,0.6)' }}
          onClick={() => setConfirmandoRemocao(null)}
        >
          <div className="modal-dialog modal-dialog-centered modal-sm" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content bg-dark border border-danger">
              <div className="modal-body text-center py-4">
                <i className="bi bi-exclamation-triangle text-danger fs-2 mb-3 d-block" />
                <p className="text-white mb-1 fw-medium">Remover pagamento?</p>
                <p className="text-secondary small mb-0">
                  <strong className="text-white">{confirmandoRemocao.description}</strong> será removido permanentemente.
                </p>
              </div>
              <div className="modal-footer border-secondary justify-content-center gap-2">
                <button className="btn btn-outline-secondary btn-sm" onClick={() => setConfirmandoRemocao(null)}>
                  Cancelar
                </button>
                <button className="btn btn-danger btn-sm" onClick={handleConfirmarRemocao} disabled={removendo}>
                  {removendo ? (
                    <><span className="spinner-border spinner-border-sm me-1" />Removendo...</>
                  ) : 'Remover'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

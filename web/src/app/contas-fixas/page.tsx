'use client';

import { useEffect, useState } from 'react';
import { useContasFixas } from '@/hooks/useContasFixas';
import { ContaFixa, CriarContaFixaRequest, AtualizarContaFixaRequest } from '@/services/contasFixasService';
import ContaFixaModal from '@/components/contasFixas/ContaFixaModal';
import ContasFixasTabela from '@/components/contasFixas/ContasFixasTabela';

export default function ContasFixasPage() {
  const { contas, carregando, erro, listar, criar, atualizar, remover } = useContasFixas();

  const [modalAberto, setModalAberto] = useState(false);
  const [contaEditando, setContaEditando] = useState<ContaFixa | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [confirmandoRemocao, setConfirmandoRemocao] = useState<ContaFixa | null>(null);
  const [removendo, setRemovendo] = useState(false);

  useEffect(() => {
    listar();
  }, [listar]);

  function abrirModalNovo() {
    setContaEditando(null);
    setModalAberto(true);
  }

  function abrirModalEditar(conta: ContaFixa) {
    setContaEditando(conta);
    setModalAberto(true);
  }

  function fecharModal() {
    setModalAberto(false);
    setContaEditando(null);
  }

  async function handleSalvar(dados: CriarContaFixaRequest | AtualizarContaFixaRequest) {
    setSalvando(true);
    let sucesso: boolean;
    if (contaEditando) {
      sucesso = await atualizar(contaEditando.id, dados as AtualizarContaFixaRequest);
    } else {
      sucesso = await criar(dados as CriarContaFixaRequest);
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

  const ativas = contas.filter((c) => c.active).length;
  const inativas = contas.filter((c) => !c.active).length;
  const obrigatorias = contas.filter((c) => c.isRequiredMonthly).length;

  return (
    <>
      <div className="d-flex align-items-center justify-content-between mb-4">
        <div>
          <h4 className="text-white mb-1 fw-semibold">
            <i className="bi bi-receipt-cutoff me-2 text-primary" />
            Contas Fixas
          </h4>
          <p className="text-secondary mb-0 small">Gerencie suas contas de recorrência fixa</p>
        </div>
        <button className="btn btn-primary d-flex align-items-center gap-2" onClick={abrirModalNovo}>
          <i className="bi bi-plus-lg" />
          Nova Conta
        </button>
      </div>

      <div className="row g-3 mb-4">
        <div className="col-sm-4">
          <div className="card bg-dark border-secondary h-100">
            <div className="card-body d-flex align-items-center gap-3">
              <div
                className="rounded-circle d-flex align-items-center justify-content-center bg-primary bg-opacity-10"
                style={{ width: 44, height: 44 }}
              >
                <i className="bi bi-list-ul text-primary" />
              </div>
              <div>
                <div className="text-white fw-bold fs-5">{contas.length}</div>
                <div className="text-secondary small">Total de Contas</div>
              </div>
            </div>
          </div>
        </div>
        <div className="col-sm-4">
          <div className="card bg-dark border-secondary h-100">
            <div className="card-body d-flex align-items-center gap-3">
              <div
                className="rounded-circle d-flex align-items-center justify-content-center bg-success bg-opacity-10"
                style={{ width: 44, height: 44 }}
              >
                <i className="bi bi-check-circle text-success" />
              </div>
              <div>
                <div className="text-white fw-bold fs-5">{ativas}</div>
                <div className="text-secondary small">Ativas</div>
              </div>
            </div>
          </div>
        </div>
        <div className="col-sm-4">
          <div className="card bg-dark border-secondary h-100">
            <div className="card-body d-flex align-items-center gap-3">
              <div
                className="rounded-circle d-flex align-items-center justify-content-center bg-warning bg-opacity-10"
                style={{ width: 44, height: 44 }}
              >
                <i className="bi bi-star text-warning" />
              </div>
              <div>
                <div className="text-white fw-bold fs-5">{obrigatorias}</div>
                <div className="text-secondary small">Obrigatórias</div>
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
            <ContasFixasTabela
              contas={contas}
              onEditar={abrirModalEditar}
              onRemover={setConfirmandoRemocao}
            />
          )}
        </div>
      </div>

      <ContaFixaModal
        aberto={modalAberto}
        contaEditando={contaEditando}
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
                <p className="text-white mb-1 fw-medium">Remover conta?</p>
                <p className="text-secondary small mb-0">
                  <strong className="text-white">{confirmandoRemocao.name}</strong> será removida permanentemente.
                </p>
              </div>
              <div className="modal-footer border-secondary justify-content-center gap-2">
                <button
                  className="btn btn-outline-secondary btn-sm"
                  onClick={() => setConfirmandoRemocao(null)}
                >
                  Cancelar
                </button>
                <button
                  className="btn btn-danger btn-sm"
                  onClick={handleConfirmarRemocao}
                  disabled={removendo}
                >
                  {removendo ? (
                    <>
                      <span className="spinner-border spinner-border-sm me-1" />
                      Removendo...
                    </>
                  ) : (
                    'Remover'
                  )}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

'use client';

import { useState, useCallback } from 'react';
import {
  pagamentosService,
  Pagamento,
  CriarPagamentoRequest,
  AtualizarPagamentoRequest,
} from '@/services/pagamentosService';

export function usePagamentos() {
  const hoje = new Date();
  const [filtroMes, setFiltroMes] = useState(hoje.getMonth() + 1);
  const [filtroAno, setFiltroAno] = useState(hoje.getFullYear());
  const [pagamentos, setPagamentos] = useState<Pagamento[]>([]);
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const listar = useCallback(async (mes: number, ano: number) => {
    setCarregando(true);
    setErro(null);
    try {
      const dados = await pagamentosService.listarPorMesAno(mes, ano);
      setPagamentos(dados);
    } catch {
      setErro('Erro ao carregar pagamentos.');
    } finally {
      setCarregando(false);
    }
  }, []);

  const criar = useCallback(
    async (request: CriarPagamentoRequest): Promise<boolean> => {
      setErro(null);
      try {
        const novo = await pagamentosService.criar(request);
        if (novo.month === filtroMes && novo.year === filtroAno) {
          setPagamentos((prev) => [novo, ...prev]);
        }
        return true;
      } catch {
        setErro('Erro ao criar pagamento.');
        return false;
      }
    },
    [filtroMes, filtroAno]
  );

  const atualizar = useCallback(
    async (id: string, request: AtualizarPagamentoRequest): Promise<boolean> => {
      setErro(null);
      try {
        const atualizado = await pagamentosService.atualizar(id, request);
        setPagamentos((prev) => {
          const semAntigo = prev.filter((p) => p.id !== id);
          if (atualizado.month === filtroMes && atualizado.year === filtroAno) {
            return [atualizado, ...semAntigo];
          }
          return semAntigo;
        });
        return true;
      } catch {
        setErro('Erro ao atualizar pagamento.');
        return false;
      }
    },
    [filtroMes, filtroAno]
  );

  const remover = useCallback(async (id: string): Promise<boolean> => {
    setErro(null);
    try {
      await pagamentosService.remover(id);
      setPagamentos((prev) => prev.filter((p) => p.id !== id));
      return true;
    } catch {
      setErro('Erro ao remover pagamento.');
      return false;
    }
  }, []);

  return {
    pagamentos,
    filtroMes,
    filtroAno,
    setFiltroMes,
    setFiltroAno,
    carregando,
    erro,
    listar,
    criar,
    atualizar,
    remover,
  };
}

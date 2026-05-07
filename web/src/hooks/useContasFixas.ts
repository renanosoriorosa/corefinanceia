'use client';

import { useState, useCallback } from 'react';
import {
  contasFixasService,
  ContaFixa,
  CriarContaFixaRequest,
  AtualizarContaFixaRequest,
} from '@/services/contasFixasService';

export function useContasFixas() {
  const [contas, setContas] = useState<ContaFixa[]>([]);
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const listar = useCallback(async () => {
    setCarregando(true);
    setErro(null);
    try {
      const dados = await contasFixasService.listar();
      setContas(dados);
    } catch {
      setErro('Erro ao carregar contas fixas.');
    } finally {
      setCarregando(false);
    }
  }, []);

  const criar = useCallback(async (request: CriarContaFixaRequest): Promise<boolean> => {
    setErro(null);
    try {
      const nova = await contasFixasService.criar(request);
      setContas((prev) => [...prev, nova]);
      return true;
    } catch {
      setErro('Erro ao criar conta fixa.');
      return false;
    }
  }, []);

  const atualizar = useCallback(
    async (id: string, request: AtualizarContaFixaRequest): Promise<boolean> => {
      setErro(null);
      try {
        const atualizada = await contasFixasService.atualizar(id, request);
        setContas((prev) => prev.map((c) => (c.id === id ? atualizada : c)));
        return true;
      } catch {
        setErro('Erro ao atualizar conta fixa.');
        return false;
      }
    },
    []
  );

  const remover = useCallback(async (id: string): Promise<boolean> => {
    setErro(null);
    try {
      await contasFixasService.remover(id);
      setContas((prev) => prev.filter((c) => c.id !== id));
      return true;
    } catch {
      setErro('Erro ao remover conta fixa.');
      return false;
    }
  }, []);

  return { contas, carregando, erro, listar, criar, atualizar, remover };
}

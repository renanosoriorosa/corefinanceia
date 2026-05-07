'use client';

import { useState, useCallback } from 'react';
import { painelService, PainelMesAtual } from '@/services/painelService';

export function usePainelMesAtual() {
  const [painel, setPainel] = useState<PainelMesAtual | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const carregar = useCallback(async () => {
    setCarregando(true);
    setErro(null);
    try {
      const dados = await painelService.obterMesAtual();
      setPainel(dados);
    } catch {
      setErro('Erro ao carregar o painel do mês.');
    } finally {
      setCarregando(false);
    }
  }, []);

  return { painel, carregando, erro, carregar };
}

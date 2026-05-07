'use client';

import { useState, useCallback } from 'react';
import { dashboardService, DashboardAnual, FiltrosDashboard } from '@/services/dashboardService';

export function useDashboard() {
  const [dados, setDados] = useState<DashboardAnual | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const carregar = useCallback(async (filtros: FiltrosDashboard) => {
    setCarregando(true);
    setErro(null);
    try {
      const resultado = await dashboardService.obterAnual(filtros);
      setDados(resultado);
    } catch {
      setErro('Erro ao carregar dados do dashboard.');
    } finally {
      setCarregando(false);
    }
  }, []);

  return { dados, carregando, erro, carregar };
}

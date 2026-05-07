import api from './api';

export interface MesValor {
  mes: number;
  total: number;
}

export interface DashboardAnual {
  ano: number;
  totalAno: number;
  maiorMes: number;
  meses: MesValor[];
}

export interface FiltrosDashboard {
  ano: number;
  contaFixaId?: string;
  incluirNaoFixas: boolean;
}

export const dashboardService = {
  async obterAnual(filtros: FiltrosDashboard): Promise<DashboardAnual> {
    const params: Record<string, string | boolean | number> = {
      ano: filtros.ano,
      incluirNaoFixas: filtros.incluirNaoFixas,
    };
    if (filtros.contaFixaId) params.contaFixaId = filtros.contaFixaId;

    const { data } = await api.get<DashboardAnual>('/api/dashboard/anual', { params });
    return data;
  },
};

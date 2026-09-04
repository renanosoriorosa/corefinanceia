import api from './api';

export interface MesValor {
  mes: number;
  total: number;
  mediaMovel?: number | null;
  projetado?: number | null;
}

export interface Comparativo {
  mesReferencia: number;
  totalMesReferencia: number;
  totalMesAnterior: number;
  variacaoMensalPercentual?: number | null;
  totalAcumuladoAno: number;
  totalAcumuladoAnoAnterior: number;
  variacaoAnualPercentual?: number | null;
}

export interface ContaValor {
  nome: string;
  total: number;
  percentual: number;
}

export interface TopGasto {
  id: string;
  descricao: string;
  conta: string;
  mes: number;
  valor: number;
}

export interface DashboardAnual {
  ano: number;
  totalAno: number;
  maiorMes: number;
  mediaMensal: number;
  projecaoAno: number;
  meses: MesValor[];
  comparativo: Comparativo;
  porConta: ContaValor[];
  topGastos: TopGasto[];
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

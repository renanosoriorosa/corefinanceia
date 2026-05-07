import api from './api';

export interface ContaPainel {
  contaFixaId: string;
  nome: string;
  descricao?: string;
  pagamentoId?: string;
  valorPago?: number;
}

export interface PainelMesAtual {
  mes: number;
  ano: number;
  totalPago: number;
  totalPendentes: number;
  contasPagas: ContaPainel[];
  contasPendentes: ContaPainel[];
}

export const painelService = {
  async obterMesAtual(): Promise<PainelMesAtual> {
    const { data } = await api.get<PainelMesAtual>('/api/painel/mes-atual');
    return data;
  },
};

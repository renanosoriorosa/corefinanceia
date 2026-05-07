import api from './api';

export interface Pagamento {
  id: string;
  fixedAccountId?: string;
  fixedAccountName?: string;
  description: string;
  amount: number;
  month: number;
  year: number;
  isFixedAccount: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CriarPagamentoRequest {
  fixedAccountId?: string;
  description: string;
  amount: number;
  month: number;
  year: number;
  isFixedAccount: boolean;
}

export interface AtualizarPagamentoRequest {
  fixedAccountId?: string;
  description: string;
  amount: number;
  month: number;
  year: number;
  isFixedAccount: boolean;
}

export const pagamentosService = {
  async listarPorMesAno(mes: number, ano: number): Promise<Pagamento[]> {
    const { data } = await api.get<Pagamento[]>('/api/payments/por-mes', { params: { mes, ano } });
    return data;
  },

  async criar(request: CriarPagamentoRequest): Promise<Pagamento> {
    const { data } = await api.post<Pagamento>('/api/payments', request);
    return data;
  },

  async atualizar(id: string, request: AtualizarPagamentoRequest): Promise<Pagamento> {
    const { data } = await api.put<Pagamento>(`/api/payments/${id}`, request);
    return data;
  },

  async remover(id: string): Promise<void> {
    await api.delete(`/api/payments/${id}`);
  },
};

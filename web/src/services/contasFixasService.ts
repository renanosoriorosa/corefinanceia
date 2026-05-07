import api from './api';

export interface ContaFixa {
  id: string;
  name: string;
  description?: string;
  isRequiredMonthly: boolean;
  active: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CriarContaFixaRequest {
  name: string;
  description?: string;
  isRequiredMonthly: boolean;
  active: boolean;
}

export interface AtualizarContaFixaRequest {
  name: string;
  description?: string;
  isRequiredMonthly: boolean;
  active: boolean;
}

const BASE = '/api/fixedaccounts';

export const contasFixasService = {
  async listar(): Promise<ContaFixa[]> {
    const { data } = await api.get<ContaFixa[]>(BASE);
    return data;
  },

  async obterPorId(id: string): Promise<ContaFixa> {
    const { data } = await api.get<ContaFixa>(`${BASE}/${id}`);
    return data;
  },

  async criar(request: CriarContaFixaRequest): Promise<ContaFixa> {
    const { data } = await api.post<ContaFixa>(BASE, request);
    return data;
  },

  async atualizar(id: string, request: AtualizarContaFixaRequest): Promise<ContaFixa> {
    const { data } = await api.put<ContaFixa>(`${BASE}/${id}`, request);
    return data;
  },

  async remover(id: string): Promise<void> {
    await api.delete(`${BASE}/${id}`);
  },
};

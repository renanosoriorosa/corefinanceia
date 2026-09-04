import api from './api';

export interface Usuario {
  id: string;
  nome: string;
  email: string;
}

export interface AuthResponse {
  token: string;
  expiraEm: string;
  usuario: Usuario;
}

export interface LoginRequest {
  email: string;
  senha: string;
}

export interface RegistrarRequest {
  nome: string;
  email: string;
  senha: string;
}

export const authService = {
  async login(request: LoginRequest): Promise<AuthResponse> {
    const { data } = await api.post<AuthResponse>('/api/auth/login', request);
    return data;
  },

  async registrar(request: RegistrarRequest): Promise<AuthResponse> {
    const { data } = await api.post<AuthResponse>('/api/auth/registrar', request);
    return data;
  },

  async perfil(): Promise<Usuario> {
    const { data } = await api.get<Usuario>('/api/auth/eu');
    return data;
  },
};

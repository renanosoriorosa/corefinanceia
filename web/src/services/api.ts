import axios from 'axios';

export const TOKEN_KEY = 'corefinance.token';
export const USUARIO_KEY = 'corefinance.usuario';

const api = axios.create({
  baseURL: '',
  headers: {
    'Content-Type': 'application/json',
  },
});

export function obterToken(): string | null {
  if (typeof window === 'undefined') return null;
  return window.localStorage.getItem(TOKEN_KEY);
}

export function limparSessao(): void {
  if (typeof window === 'undefined') return;
  window.localStorage.removeItem(TOKEN_KEY);
  window.localStorage.removeItem(USUARIO_KEY);
}

api.interceptors.request.use((config) => {
  const token = obterToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const rotaDeLogin = ['/login', '/registrar'].includes(
      typeof window !== 'undefined' ? window.location.pathname : ''
    );

    if (error.response?.status === 401 && !rotaDeLogin) {
      limparSessao();
      window.location.href = '/login';
    }

    return Promise.reject(error);
  }
);

/** Extrai a mensagem devolvida pela API no formato { erro: "..." }. */
export function mensagemDeErro(error: unknown, padrao: string): string {
  if (axios.isAxiosError(error)) {
    const erro = error.response?.data?.erro;
    if (typeof erro === 'string' && erro.length > 0) return erro;
  }
  return padrao;
}

export default api;

'use client';

import { createContext, useCallback, useEffect, useMemo, useState } from 'react';
import { authService, AuthResponse, Usuario } from '@/services/authService';
import { TOKEN_KEY, USUARIO_KEY, limparSessao, obterToken } from '@/services/api';

interface AuthContextValor {
  usuario: Usuario | null;
  autenticado: boolean;
  carregando: boolean;
  entrar: (resposta: AuthResponse) => void;
  sair: () => void;
}

export const AuthContext = createContext<AuthContextValor | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [usuario, setUsuario] = useState<Usuario | null>(null);
  const [carregando, setCarregando] = useState(true);

  // Restaura a sessão salva antes de renderizar as telas protegidas.
  useEffect(() => {
    const token = obterToken();
    const usuarioSalvo = window.localStorage.getItem(USUARIO_KEY);

    if (!token || !usuarioSalvo) {
      limparSessao();
      setCarregando(false);
      return;
    }

    setUsuario(JSON.parse(usuarioSalvo) as Usuario);

    authService
      .perfil()
      .then((perfil) => {
        setUsuario(perfil);
        window.localStorage.setItem(USUARIO_KEY, JSON.stringify(perfil));
      })
      .catch(() => {
        limparSessao();
        setUsuario(null);
      })
      .finally(() => setCarregando(false));
  }, []);

  const entrar = useCallback((resposta: AuthResponse) => {
    window.localStorage.setItem(TOKEN_KEY, resposta.token);
    window.localStorage.setItem(USUARIO_KEY, JSON.stringify(resposta.usuario));
    setUsuario(resposta.usuario);
  }, []);

  const sair = useCallback(() => {
    limparSessao();
    setUsuario(null);
  }, []);

  const valor = useMemo(
    () => ({ usuario, autenticado: usuario !== null, carregando, entrar, sair }),
    [usuario, carregando, entrar, sair]
  );

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>;
}

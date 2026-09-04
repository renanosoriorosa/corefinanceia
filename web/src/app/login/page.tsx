'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import AuthCard from '@/components/auth/AuthCard';
import { useAuth } from '@/hooks/useAuth';
import { authService } from '@/services/authService';
import { mensagemDeErro } from '@/services/api';

export default function LoginPage() {
  const router = useRouter();
  const { entrar } = useAuth();

  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function submeter(evento: React.FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);

    try {
      const resposta = await authService.login({ email, senha });
      entrar(resposta);
      router.replace('/');
    } catch (error) {
      setErro(mensagemDeErro(error, 'Não foi possível entrar. Tente novamente.'));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <AuthCard
      titulo="Entrar"
      subtitulo="Acesse sua conta para continuar"
      rodapeTexto="Ainda não tem conta?"
      rodapeLinkTexto="Criar conta"
      rodapeHref="/registrar"
    >
      <form onSubmit={submeter}>
        <div className="mb-3">
          <label className="form-label text-secondary small">E-mail</label>
          <input
            type="email"
            className="form-control bg-dark-subtle text-white border-secondary"
            placeholder="voce@email.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="email"
            required
          />
        </div>

        <div className="mb-3">
          <label className="form-label text-secondary small">Senha</label>
          <input
            type="password"
            className="form-control bg-dark-subtle text-white border-secondary"
            placeholder="••••••"
            value={senha}
            onChange={(e) => setSenha(e.target.value)}
            autoComplete="current-password"
            required
          />
        </div>

        {erro && (
          <div className="alert alert-danger d-flex align-items-center gap-2 py-2 small">
            <i className="bi bi-exclamation-triangle-fill" />
            {erro}
          </div>
        )}

        <button type="submit" className="btn btn-primary w-100 d-flex align-items-center justify-content-center gap-2" disabled={enviando}>
          {enviando ? <span className="spinner-border spinner-border-sm" /> : <i className="bi bi-box-arrow-in-right" />}
          Entrar
        </button>
      </form>
    </AuthCard>
  );
}

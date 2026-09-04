'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import AuthCard from '@/components/auth/AuthCard';
import { useAuth } from '@/hooks/useAuth';
import { authService } from '@/services/authService';
import { mensagemDeErro } from '@/services/api';

export default function RegistrarPage() {
  const router = useRouter();
  const { entrar } = useAuth();

  const [nome, setNome] = useState('');
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function submeter(evento: React.FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);

    try {
      const resposta = await authService.registrar({ nome, email, senha });
      entrar(resposta);
      router.replace('/');
    } catch (error) {
      setErro(mensagemDeErro(error, 'Não foi possível criar a conta. Tente novamente.'));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <AuthCard
      titulo="Criar conta"
      subtitulo="Comece a organizar suas finanças"
      rodapeTexto="Já tem uma conta?"
      rodapeLinkTexto="Entrar"
      rodapeHref="/login"
    >
      <form onSubmit={submeter}>
        <div className="mb-3">
          <label className="form-label text-secondary small">Nome</label>
          <input
            type="text"
            className="form-control bg-dark-subtle text-white border-secondary"
            placeholder="Seu nome"
            value={nome}
            onChange={(e) => setNome(e.target.value)}
            maxLength={120}
            required
          />
        </div>

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
            placeholder="Mínimo de 6 caracteres"
            value={senha}
            onChange={(e) => setSenha(e.target.value)}
            autoComplete="new-password"
            minLength={6}
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
          {enviando ? <span className="spinner-border spinner-border-sm" /> : <i className="bi bi-person-plus" />}
          Criar conta
        </button>
      </form>
    </AuthCard>
  );
}

'use client';

import { useEffect } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import Sidebar from '@/components/Sidebar';
import { useAuth } from '@/hooks/useAuth';

const ROTAS_PUBLICAS = ['/login', '/registrar'];

export default function AppShell({ children }: { children: React.ReactNode }) {
  const { autenticado, carregando } = useAuth();
  const pathname = usePathname();
  const router = useRouter();

  const rotaPublica = ROTAS_PUBLICAS.includes(pathname);

  useEffect(() => {
    if (carregando) return;

    if (!autenticado && !rotaPublica) {
      router.replace('/login');
    }

    if (autenticado && rotaPublica) {
      router.replace('/');
    }
  }, [autenticado, carregando, rotaPublica, router]);

  // As telas públicas não dependem da sessão, então aparecem sem espera.
  if (rotaPublica) {
    return <>{children}</>;
  }

  if (carregando) {
    return (
      <div className="d-flex align-items-center justify-content-center" style={{ minHeight: '100vh' }}>
        <div className="spinner-border text-primary" />
      </div>
    );
  }

  if (!autenticado) {
    return null;
  }

  return (
    <div className="d-flex" style={{ minHeight: '100vh' }}>
      <Sidebar />
      <main className="flex-grow-1 p-4" style={{ backgroundColor: '#111827' }}>
        {children}
      </main>
    </div>
  );
}

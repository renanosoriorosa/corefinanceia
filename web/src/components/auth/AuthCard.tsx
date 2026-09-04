'use client';

import Link from 'next/link';

interface Props {
  titulo: string;
  subtitulo: string;
  children: React.ReactNode;
  rodapeTexto: string;
  rodapeLinkTexto: string;
  rodapeHref: string;
}

export default function AuthCard({
  titulo,
  subtitulo,
  children,
  rodapeTexto,
  rodapeLinkTexto,
  rodapeHref,
}: Props) {
  return (
    <div className="d-flex align-items-center justify-content-center p-3" style={{ minHeight: '100vh' }}>
      <div className="w-100" style={{ maxWidth: 420 }}>
        <div className="text-center mb-4">
          <span className="text-white fw-bold fs-4">
            <i className="bi bi-wallet2 me-2 text-primary" />
            CoreFinance
          </span>
        </div>

        <div className="card bg-dark border-secondary">
          <div className="card-body p-4">
            <h5 className="text-white fw-semibold mb-1">{titulo}</h5>
            <p className="text-secondary small mb-4">{subtitulo}</p>

            {children}
          </div>
        </div>

        <p className="text-secondary small text-center mt-3 mb-0">
          {rodapeTexto}{' '}
          <Link href={rodapeHref} className="text-primary text-decoration-none">
            {rodapeLinkTexto}
          </Link>
        </p>
      </div>
    </div>
  );
}

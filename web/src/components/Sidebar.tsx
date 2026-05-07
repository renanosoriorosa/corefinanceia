'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

const itens = [
  { href: '/', label: 'Painel do Mês', icone: 'bi-calendar2-check' },
  { href: '/dashboard', label: 'Dashboard', icone: 'bi-bar-chart-line' },
  { href: '/pagamentos', label: 'Pagamentos', icone: 'bi-cash-coin' },
  { href: '/contas-fixas', label: 'Contas Fixas', icone: 'bi-receipt-cutoff' },
];

export default function Sidebar() {
  const pathname = usePathname();

  return (
    <nav
      className="d-flex flex-column bg-dark border-end border-secondary"
      style={{ width: '220px', minHeight: '100vh', padding: '1.5rem 0' }}
    >
      <div className="px-3 mb-4">
        <span className="text-white fw-bold fs-5">
          <i className="bi bi-wallet2 me-2 text-primary" />
          CoreFinance
        </span>
      </div>

      <ul className="nav flex-column px-2">
        {itens.map((item) => {
          const ativo = pathname === item.href;
          return (
            <li key={item.href} className="nav-item">
              <Link
                href={item.href}
                className={`nav-link d-flex align-items-center gap-2 rounded px-3 py-2 ${
                  ativo
                    ? 'bg-primary text-white'
                    : 'text-secondary'
                }`}
              >
                <i className={`bi ${item.icone}`} />
                {item.label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

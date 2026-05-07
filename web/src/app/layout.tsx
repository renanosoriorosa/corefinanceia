import type { Metadata } from 'next';
import 'bootstrap/dist/css/bootstrap.min.css';
import 'bootstrap-icons/font/bootstrap-icons.css';
import './globals.css';
import Sidebar from '@/components/Sidebar';

export const metadata: Metadata = {
  title: 'CoreFinance',
  description: 'Sistema de controle financeiro pessoal',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR" data-bs-theme="dark">
      <body>
        <div className="d-flex" style={{ minHeight: '100vh' }}>
          <Sidebar />
          <main className="flex-grow-1 p-4" style={{ backgroundColor: '#111827' }}>
            {children}
          </main>
        </div>
      </body>
    </html>
  );
}

import type { Metadata, Viewport } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'RED AI — Campaign Engine',
  description: 'Transforme uma marca em uma campanha social completa.',
  manifest: '/manifest.webmanifest',
  appleWebApp: { capable: true, title: 'RED AI', statusBarStyle: 'black-translucent' },
  icons: { icon: '/icon.svg', apple: '/icon.svg' }
};
export const viewport: Viewport = { themeColor: '#090909', colorScheme: 'dark' };

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="pt-BR"><body>{children}</body></html>;
}

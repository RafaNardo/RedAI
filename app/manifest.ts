import type { MetadataRoute } from 'next';
export default function manifest(): MetadataRoute.Manifest {
  return { name: 'RED AI', short_name: 'RED AI', description: 'Campanhas sociais criadas com IA.', start_url: '/', display: 'standalone', background_color: '#090909', theme_color: '#090909', icons: [{ src: '/icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' }] };
}

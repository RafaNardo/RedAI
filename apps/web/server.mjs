import { createReadStream, existsSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { extname, join, normalize } from 'node:path';

const root = join(process.cwd(), 'out');
const types = { '.css': 'text/css', '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.svg': 'image/svg+xml', '.webmanifest': 'application/manifest+json', '.png': 'image/png' };
const port = Number(process.env.PORT || 3000);
createServer((request, response) => {
  const pathname = decodeURIComponent(new URL(request.url || '/', 'http://localhost').pathname);
  const relative = pathname === '/' ? 'index.html' : normalize(pathname).replace(/^[/\\]+/, '');
  const cachePath = relative.replaceAll('\\', '/');
  const candidate = join(root, relative);
  const file = candidate.startsWith(root) && existsSync(candidate) && statSync(candidate).isFile() ? candidate : join(root, 'index.html');
  const extension = extname(file);
  const cacheControl = cachePath.endsWith('.html') || cachePath === 'sw.js' || cachePath === 'manifest.webmanifest'
    ? 'no-cache, no-store, must-revalidate'
    : cachePath.startsWith('_next/static/')
      ? 'public, max-age=31536000, immutable'
      : 'public, max-age=3600, must-revalidate';
  response.writeHead(200, { 'Content-Type': types[extension] || 'application/octet-stream', 'Cache-Control': cacheControl });
  createReadStream(file).pipe(response);
}).listen(port, '0.0.0.0');

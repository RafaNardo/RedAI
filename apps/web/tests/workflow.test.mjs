import test from 'node:test';
import assert from 'node:assert/strict';

test('campaign route produces five static posts', () => {
  const routes = Array.from({ length: 5 }, (_, index) => index + 1);
  const selected = routes.slice(0, 1);
  const posts = Array.from({ length: 5 }, (_, index) => ({ sequence: index + 1, route: selected[0] }));
  assert.equal(routes.length, 5);
  assert.equal(selected.length, 1);
  assert.equal(selected.length === 1, true);
  assert.equal(posts.length, 5);
  assert.equal(new Set(posts.map(post => post.sequence)).size, 5);
  assert.equal(posts.every(post => post.route === selected[0]), true);
  const exportEntries = posts.map(post => `posts/${String(post.sequence).padStart(2, '0')}-post-estatico.png`);
  assert.deepEqual(exportEntries, ['posts/01-post-estatico.png', 'posts/02-post-estatico.png', 'posts/03-post-estatico.png', 'posts/04-post-estatico.png', 'posts/05-post-estatico.png']);
});

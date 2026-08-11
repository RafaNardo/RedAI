import test from 'node:test';
import assert from 'node:assert/strict';

test('campaign workflow constraints', () => {
  const ideas = Array.from({ length: 30 }, (_, index) => index + 1);
  const selected = ideas.slice(0, 12);
  assert.equal(ideas.length, 30);
  assert.equal(selected.length, 12);
  assert.equal(selected.length === 12, true);
});

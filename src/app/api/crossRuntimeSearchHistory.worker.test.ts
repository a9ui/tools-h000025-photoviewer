import { expect, it } from 'vitest';

import { PUT as putSearchHistory } from './search-history/route';

const iterations = Number.parseInt(process.env.CROSS_RUNTIME_ITERATIONS ?? '', 10);
const target = process.env.PVU_SEARCH_HISTORY_PATH ?? '';
const enabled = Number.isInteger(iterations) && iterations > 0 && target !== '';

function request(query: string) {
  return new Request('http://isolated.test/api/search-history', {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ query }),
  });
}

it.skipIf(!enabled)('writes Browser search history concurrently with the WPF process', async () => {
  const dottedI = await putSearchHistory(request('cat, i\u0307'));
  const nonAscii = await putSearchHistory(request('москва, οσ'));
  expect(dottedI.status).toBe(200);
  expect(nonAscii.status).toBe(200);
  for (let index = 0; index < iterations; index += 1) {
    const response = await putSearchHistory(request(`browser query ${index.toString().padStart(2, '0')}`));
    expect(response.status).toBe(200);
    expect(await response.json()).toMatchObject({ ok: true, malformed: false, futureVersion: false });
  }
});

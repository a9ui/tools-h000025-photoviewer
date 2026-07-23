import { describe, expect, it } from 'vitest';

import { guardLocalApiRequest, guardLocalImageRequest } from './localApiGuard';

function request(url = 'http://127.0.0.1:3001/api/settings', headers: Record<string, string> = {}) {
  return new Request(url, { method: 'PUT', headers });
}

async function expectForbidden(candidate: Request) {
  const response = guardLocalApiRequest(candidate);
  expect(response?.status).toBe(403);
  await expect(response?.json()).resolves.toEqual({
    error: 'Forbidden local API request.',
  });
}

function imageRequest(
  headers: Record<string, string> = {},
  url = 'http://127.0.0.1:3001/api/image',
  method = 'GET',
) {
  return new Request(url, { method, headers });
}

async function expectImageForbidden(candidate: Request) {
  const response = guardLocalImageRequest(candidate);
  expect(response?.status).toBe(403);
  await expect(response?.json()).resolves.toEqual({
    error: 'Forbidden local API request.',
  });
}

describe('local API request guard', () => {
  it.each([
    ['127.0.0.1', 'http://127.0.0.1:3001/api/settings', '127.0.0.1:3001', 'http://127.0.0.1:3001'],
    ['localhost', 'http://localhost:3001/api/settings', 'localhost:3001', 'http://localhost:3001'],
    ['IPv6 loopback', 'http://[::1]:3001/api/settings', '[::1]:3001', 'http://[::1]:3001'],
  ])('allows a same-origin browser request on %s', (_name, url, host, origin) => {
    expect(
      guardLocalApiRequest(
        request(url, {
          host,
          origin,
          'sec-fetch-site': 'same-origin',
          'sec-fetch-mode': 'cors',
        }),
      ),
    ).toBeNull();
  });

  it('allows a direct loopback client with no Origin or Fetch Metadata', () => {
    expect(guardLocalApiRequest(request())).toBeNull();
  });

  it('allows an Origin-less same-origin browser request', () => {
    expect(
      guardLocalApiRequest(
        request(undefined, {
          host: '127.0.0.1:3001',
          'sec-fetch-site': 'same-origin',
          'sec-fetch-mode': 'cors',
        }),
      ),
    ).toBeNull();
  });

  it.each([
    ['foreign hostname', 'evil.example:3001'],
    ['DNS-rebinding hostname', '127.0.0.1.attacker.example:3001'],
    ['userinfo authority confusion', 'evil.example@127.0.0.1:3001'],
  ])('rejects a Host header with %s', async (_name, host) => {
    await expectForbidden(request(undefined, { host }));
  });

  it.each([
    ['foreign Origin', 'https://evil.example'],
    ['loopback hostname mismatch', 'http://localhost:3001'],
    ['loopback port mismatch', 'http://127.0.0.1:3002'],
    ['opaque Origin', 'null'],
  ])('rejects %s', async (_name, origin) => {
    await expectForbidden(
      request(undefined, {
        host: '127.0.0.1:3001',
        origin,
        'sec-fetch-site': 'same-origin',
      }),
    );
  });

  it.each(['cross-site', 'same-site', 'none'])('rejects Sec-Fetch-Site: %s', async (site) => {
    await expectForbidden(
      request(undefined, {
        host: '127.0.0.1:3001',
        'sec-fetch-site': site,
      }),
    );
  });

  it('rejects a no-cors request even when its other headers claim same-origin', async () => {
    await expectForbidden(
      request(undefined, {
        host: '127.0.0.1:3001',
        origin: 'http://127.0.0.1:3001',
        'sec-fetch-site': 'same-origin',
        'sec-fetch-mode': 'no-cors',
      }),
    );
  });
});

describe('local image request guard', () => {
  const sameOriginImageHeaders = {
    host: '127.0.0.1:3001',
    'sec-fetch-site': 'same-origin',
    'sec-fetch-mode': 'no-cors',
    'sec-fetch-dest': 'image',
  };

  it('allows the normal same-origin no-cors image request shape', () => {
    expect(guardLocalImageRequest(imageRequest(sameOriginImageHeaders))).toBeNull();
    expect(guardLocalImageRequest(imageRequest({
      ...sameOriginImageHeaders,
      origin: 'http://127.0.0.1:3001',
    }))).toBeNull();
  });

  it('keeps same-origin cors requests on the shared strict guard', () => {
    expect(guardLocalImageRequest(imageRequest({
      host: '127.0.0.1:3001',
      origin: 'http://127.0.0.1:3001',
      'sec-fetch-site': 'same-origin',
      'sec-fetch-mode': 'cors',
      'sec-fetch-dest': 'empty',
    }))).toBeNull();
  });

  it.each(['empty', 'script', 'document'])('rejects no-cors destination %s', async (destination) => {
    await expectImageForbidden(imageRequest({
      ...sameOriginImageHeaders,
      'sec-fetch-dest': destination,
    }));
  });

  it.each(['cross-site', 'same-site', 'none'])('rejects no-cors image site %s', async (site) => {
    await expectImageForbidden(imageRequest({
      ...sameOriginImageHeaders,
      'sec-fetch-site': site,
    }));
  });

  it('rejects missing or non-loopback authority', async () => {
    const { host: _host, ...withoutHost } = sameOriginImageHeaders;
    await expectImageForbidden(imageRequest(withoutHost));
    await expectImageForbidden(imageRequest({
      ...sameOriginImageHeaders,
      host: 'evil.example:3001',
    }));
    await expectImageForbidden(imageRequest(
      sameOriginImageHeaders,
      'http://evil.example:3001/api/image',
    ));
  });

  it('allows the framework loopback alias when the browser Host and port remain authoritative', () => {
    expect(guardLocalImageRequest(imageRequest({
      ...sameOriginImageHeaders,
      host: 'localhost:3001',
    }))).toBeNull();
  });

  it.each([
    'http://localhost:3001',
    'http://127.0.0.1:3002',
    'https://127.0.0.1:3001',
    'null',
  ])('rejects mismatched or invalid image Origin %s', async (origin) => {
    await expectImageForbidden(imageRequest({
      ...sameOriginImageHeaders,
      origin,
    }));
  });

  it('rejects non-GET methods before evaluating the image exception', async () => {
    const response = guardLocalImageRequest(imageRequest(sameOriginImageHeaders, undefined, 'POST'));
    expect(response?.status).toBe(405);
    expect(response?.headers.get('allow')).toBe('GET');
  });
});

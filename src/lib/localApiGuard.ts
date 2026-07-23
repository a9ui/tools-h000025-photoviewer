const LOOPBACK_HOSTNAMES = new Set(['127.0.0.1', 'localhost', '::1']);

type LocalAuthority = {
  hostname: string;
  port: string;
};

function parseLocalAuthority(value: string): LocalAuthority | null {
  const trimmed = value.trim();
  if (!trimmed || trimmed !== value || /[\\/@?#,]/.test(trimmed)) return null;

  try {
    const parsed = new URL(`http://${trimmed}`);
    if (parsed.username || parsed.password || parsed.pathname !== '/' || parsed.search || parsed.hash) {
      return null;
    }

    const hostname = parsed.hostname.toLowerCase().replace(/^\[|\]$/g, '');
    if (!LOOPBACK_HOSTNAMES.has(hostname)) return null;
    return { hostname, port: parsed.port };
  } catch {
    return null;
  }
}

function sameAuthority(left: LocalAuthority, right: LocalAuthority) {
  return left.hostname === right.hostname && left.port === right.port;
}

function forbiddenResponse() {
  return Response.json(
    { error: 'Forbidden local API request.' },
    {
      status: 403,
      headers: {
        'Cache-Control': 'no-store',
      },
    },
  );
}

function methodNotAllowedResponse() {
  return Response.json(
    { error: 'Method not allowed.' },
    {
      status: 405,
      headers: {
        Allow: 'GET',
        'Cache-Control': 'no-store',
      },
    },
  );
}

/**
 * Reject browser and DNS-rebinding requests before a local API reaches any
 * filesystem, shared-state, worker, or OS side effect. Direct loopback clients
 * (the launcher, WPF/CLI probes, and route tests) may omit browser-only Origin
 * and Fetch Metadata headers.
 */
export function guardLocalApiRequest(request: Request): Response | null {
  let requestUrl: URL;
  try {
    requestUrl = new URL(request.url);
  } catch {
    return forbiddenResponse();
  }

  const requestAuthority = parseLocalAuthority(requestUrl.host);
  const hostHeader = request.headers.get('host');
  const hostAuthority = hostHeader === null ? requestAuthority : parseLocalAuthority(hostHeader);

  if (!requestAuthority || !hostAuthority) return forbiddenResponse();

  const fetchSite = request.headers.get('sec-fetch-site');
  if (fetchSite !== null && fetchSite.toLowerCase() !== 'same-origin') {
    return forbiddenResponse();
  }

  const fetchMode = request.headers.get('sec-fetch-mode');
  if (fetchMode !== null && fetchMode.toLowerCase() === 'no-cors') {
    return forbiddenResponse();
  }

  const origin = request.headers.get('origin');
  if (origin === null) return null;

  try {
    const parsedOrigin = new URL(origin);
    if (parsedOrigin.origin !== origin || parsedOrigin.protocol !== requestUrl.protocol) {
      return forbiddenResponse();
    }
    const originAuthority = parseLocalAuthority(parsedOrigin.host);
    if (!originAuthority || !sameAuthority(originAuthority, hostAuthority)) {
      return forbiddenResponse();
    }
  } catch {
    return forbiddenResponse();
  }

  return null;
}

/**
 * Preserve the strict local API guard while allowing the browser's normal
 * same-origin <img> request shape for the read-only image route.
 */
export function guardLocalImageRequest(request: Request): Response | null {
  if (request.method.toUpperCase() !== 'GET') return methodNotAllowedResponse();

  const fetchMode = request.headers.get('sec-fetch-mode')?.toLowerCase();
  if (fetchMode !== 'no-cors') return guardLocalApiRequest(request);

  let requestUrl: URL;
  try {
    requestUrl = new URL(request.url);
  } catch {
    return forbiddenResponse();
  }

  const requestAuthority = parseLocalAuthority(requestUrl.host);
  const hostHeader = request.headers.get('host');
  const hostAuthority = hostHeader === null ? null : parseLocalAuthority(hostHeader);
  // Next normalizes loopback aliases and may omit the original port in its
  // internal request URL. Treat the browser-supplied Host as authoritative,
  // while independently requiring both authorities to remain loopback.
  if (!requestAuthority || !hostAuthority) {
    return forbiddenResponse();
  }

  if (request.headers.get('sec-fetch-site')?.toLowerCase() !== 'same-origin') {
    return forbiddenResponse();
  }
  if (request.headers.get('sec-fetch-dest')?.toLowerCase() !== 'image') {
    return forbiddenResponse();
  }

  const origin = request.headers.get('origin');
  if (origin === null) return null;

  try {
    const parsedOrigin = new URL(origin);
    const originAuthority = parseLocalAuthority(parsedOrigin.host);
    if (
      parsedOrigin.origin !== origin
      || parsedOrigin.protocol !== requestUrl.protocol
      || !originAuthority
      || !sameAuthority(originAuthority, hostAuthority)
    ) {
      return forbiddenResponse();
    }
  } catch {
    return forbiddenResponse();
  }

  return null;
}

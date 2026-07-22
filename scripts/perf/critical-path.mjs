import crypto from 'node:crypto';
import fs from 'node:fs';
import http from 'node:http';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';
import { execFileSync, spawn, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { performance } from 'node:perf_hooks';

import { chromium } from '@playwright/test';
import sharp from 'sharp';

import { parseArgs } from './lib/args.mjs';
import {
  diagnoseCriticalPath,
  parseServerTiming,
  percentile,
  regressionPercent,
  summarizeRuns,
} from './lib/criticalPathAnalysis.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const NEXT_BIN = path.join(ROOT, 'node_modules', 'next', 'dist', 'bin', 'next');
const BASE_SHA = '32f3183f0b2e225cbd0456100cd46b7f87bb5f7d';
const FOCUSED_SHA = '5fe9da971f9df552af242eb4c30c927a219c54ff';
const SAME_ORIGIN_SHA = 'ac599f19af7d680402045615b11ed59bc8f812ce';
const SCHEMA = 'h25.browser-critical-path/v1';
const START_PORT = 3200;
const END_PORT = 3299;
const DEFAULT_VIEWPORT = { width: 1440, height: 900 };

function positiveInt(value, fallback) {
  const parsed = Number.parseInt(String(value ?? ''), 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function booleanArg(value, fallback = false) {
  if (value === undefined) return fallback;
  if (value === true) return true;
  return !['0', 'false', 'no'].includes(String(value).toLowerCase());
}

function assertTempChild(target) {
  const tempRoot = path.resolve(os.tmpdir());
  const resolved = path.resolve(target);
  const relative = path.relative(tempRoot, resolved);
  if (!relative || relative.startsWith('..') || path.isAbsolute(relative)) {
    throw new Error('Performance fixtures must be a child of OS TEMP.');
  }
  return resolved;
}

function round(value) {
  return Number.isFinite(value) ? Math.round(value * 10) / 10 : null;
}

function sha256(value) {
  return crypto.createHash('sha256').update(value).digest('hex');
}

function gitOutput(args) {
  const result = spawnSync('git', args, { cwd: ROOT, encoding: 'utf8', windowsHide: true });
  return result.status === 0 ? result.stdout.trim() : 'unknown';
}

function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit += 1) {
      crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
    }
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function pngChunk(type, data) {
  const typeBytes = Buffer.from(type, 'ascii');
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length, 0);
  const checksum = Buffer.alloc(4);
  checksum.writeUInt32BE(crc32(Buffer.concat([typeBytes, data])), 0);
  return Buffer.concat([length, typeBytes, data, checksum]);
}

function addPngText(png, keyword, text) {
  const iend = Buffer.from('0000000049454e44ae426082', 'hex');
  const offset = png.lastIndexOf(iend);
  if (offset < 0) throw new Error('Generated PNG has no IEND chunk.');
  const textChunk = pngChunk('tEXt', Buffer.from(`${keyword}\0${text}`, 'latin1'));
  return Buffer.concat([png.subarray(0, offset), textChunk, png.subarray(offset)]);
}

async function createFixture(runRoot, fileCount, largeCount) {
  const fixtureRoot = path.join(runRoot, 'fixture');
  const roots = [path.join(fixtureRoot, 'root-a'), path.join(fixtureRoot, 'root-b')];
  fs.mkdirSync(roots[0], { recursive: true });
  fs.mkdirSync(roots[1], { recursive: true });

  const metadata = 'synthetic performance fixture, steps: 20, sampler: deterministic';
  const smallPng = addPngText(
    await sharp({
      create: { width: 96, height: 64, channels: 4, background: '#25344fff' },
    }).png({ compressionLevel: 6 }).toBuffer(),
    'parameters',
    metadata,
  );
  const largePng = addPngText(
    await sharp({
      create: { width: 3072, height: 2048, channels: 4, background: '#385f88ff' },
    }).png({ compressionLevel: 6 }).toBuffer(),
    'parameters',
    metadata,
  );

  const oldBaseSeconds = Date.UTC(2020, 0, 1) / 1000;
  const names = [];
  let totalBytes = 0;
  for (let index = 0; index < fileCount; index += 1) {
    const rootIndex = index % roots.length;
    const filename = `image-${String(index).padStart(6, '0')}.png`;
    const target = path.join(roots[rootIndex], filename);
    const bytes = index >= fileCount - largeCount ? largePng : smallPng;
    fs.writeFileSync(target, bytes);
    fs.utimesSync(target, oldBaseSeconds + index, oldBaseSeconds + index);
    names.push(`${rootIndex}/${filename}`);
    totalBytes += bytes.length;
  }
  for (const root of roots) fs.utimesSync(root, oldBaseSeconds, oldBaseSeconds);

  return {
    roots,
    dirSet: roots.join('\n'),
    publicManifest: {
      fileCount,
      largeImageCount: largeCount,
      rootCount: roots.length,
      totalBytes,
      dimensions: { representative: '3072x2048', compact: '96x64' },
      metadata: 'deterministic PNG tEXt parameters',
      relativeNameSha256: sha256(names.join('\n')),
      smallPngSha256: sha256(smallPng),
      largePngSha256: sha256(largePng),
    },
  };
}

function findPort(port = START_PORT) {
  return new Promise((resolve, reject) => {
    if (port > END_PORT) {
      reject(new Error(`No available loopback port in ${START_PORT}-${END_PORT}.`));
      return;
    }
    const server = net.createServer();
    server.once('error', () => resolve(findPort(port + 1)));
    server.once('listening', () => server.close(() => resolve(port)));
    server.listen(port, '127.0.0.1');
  });
}

function requestUntilReady(url, timeoutMs = 120_000) {
  const startedAt = Date.now();
  return new Promise((resolve, reject) => {
    const attempt = () => {
      const request = http.get(url, (response) => {
        response.resume();
        if ((response.statusCode ?? 500) < 500) resolve(Date.now());
        else setTimeout(attempt, 100);
      });
      request.setTimeout(1_000, () => request.destroy(new Error('timeout')));
      request.once('error', () => {
        if (Date.now() - startedAt >= timeoutMs) reject(new Error(`Timed out waiting for ${url}.`));
        else setTimeout(attempt, 100);
      });
    };
    attempt();
  });
}

function privateStateEnvironment(runRoot) {
  const stateRoot = path.join(runRoot, 'state');
  return {
    PVU_DERIVED_CACHE_ROOT: path.join(runRoot, 'derived'),
    PVU_FAVORITES_PATH: path.join(stateRoot, 'favorites.json'),
    PVU_SEEN_PATH: path.join(stateRoot, 'seen.json'),
    PVU_SETTINGS_PATH: path.join(stateRoot, 'settings.json'),
    PVU_RECENT_FOLDERS_PATH: path.join(stateRoot, 'recent-folders.json'),
    PVU_SEARCH_HISTORY_PATH: path.join(stateRoot, 'search-history.json'),
    PVU_ALBUMS_PATH: path.join(stateRoot, 'albums.json'),
    PVU_ENHANCE_ROOT: path.join(runRoot, 'enhance'),
    PV_LEGACY_PHOTOVIEWER_DIR: path.join(runRoot, 'legacy-empty'),
    PVU_COMFY_AUTOSTART: '0',
  };
}

function startServer(runRoot, traceEnabled) {
  return findPort().then(async (port) => {
    const logs = [];
    const env = {
      ...process.env,
      ...privateStateEnvironment(runRoot),
      PV_PERF_TRACE: traceEnabled ? '1' : '0',
      PVU_SERVER_HOST: '127.0.0.1',
      PVU_SERVER_PORT: String(port),
      PVU_SERVER_STARTED_AT_UTC: new Date().toISOString(),
    };
    const spawnEpochMs = Date.now();
    const spawnMonotonicMs = performance.now();
    const child = spawn(
      process.execPath,
      [NEXT_BIN, 'start', '--hostname', '127.0.0.1', '--port', String(port)],
      { cwd: ROOT, env, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] },
    );
    const capture = (chunk) => {
      if (logs.join('').length < 64 * 1024) logs.push(chunk.toString());
    };
    child.stdout.on('data', capture);
    child.stderr.on('data', capture);
    const baseUrl = `http://127.0.0.1:${port}`;
    try {
      const readyEpochMs = await requestUntilReady(`${baseUrl}/api/runtime`);
      return {
        baseUrl,
        child,
        logs,
        spawnEpochMs,
        spawnMonotonicMs,
        readyEpochMs,
        stop: async () => {
          if (child.exitCode !== null) return;
          child.kill();
          await Promise.race([
            new Promise((resolve) => child.once('exit', resolve)),
            new Promise((resolve) => setTimeout(resolve, 5_000)),
          ]);
          if (child.exitCode === null) child.kill('SIGKILL');
        },
      };
    } catch (error) {
      child.kill();
      throw new Error(`${error.message}\n${logs.join('')}`);
    }
  });
}

function sampleProcess(processId) {
  if (process.platform !== 'win32') return null;
  try {
    const command = [
      `$p=Get-Process -Id ${processId} -ErrorAction Stop`,
      `$c=Get-CimInstance Win32_Process -Filter "ProcessId=${processId}" -ErrorAction Stop`,
      '[pscustomobject]@{cpuMs=$p.TotalProcessorTime.TotalMilliseconds; peakWorkingSetBytes=$p.PeakWorkingSet64; workingSetBytes=$p.WorkingSet64; readBytes=[double]$c.ReadTransferCount; writeBytes=[double]$c.WriteTransferCount} | ConvertTo-Json -Compress',
    ].join('; ');
    return JSON.parse(execFileSync('powershell.exe', ['-NoProfile', '-Command', command], {
      encoding: 'utf8',
      windowsHide: true,
    }));
  } catch {
    return null;
  }
}

function runtimeDelta(before, after) {
  if (!after) return null;
  return {
    cpuMs: round(after.cpuMs - (before?.cpuMs ?? 0)),
    peakWorkingSetBytes: after.peakWorkingSetBytes,
    workingSetBytes: after.workingSetBytes,
    readBytes: Math.max(0, after.readBytes - (before?.readBytes ?? 0)),
    writeBytes: Math.max(0, after.writeBytes - (before?.writeBytes ?? 0)),
  };
}

async function scanViaApi(baseUrl, dirSet) {
  const response = await fetch(`${baseUrl}/api/scan?dir=${encodeURIComponent(dirSet)}&full=1`, {
    headers: { Accept: 'text/event-stream' },
  });
  if (!response.ok) throw new Error(`Template scan failed with ${response.status}.`);
  const body = await response.text();
  const events = body
    .split(/\n\n+/)
    .flatMap((chunk) => chunk.split(/\r?\n/))
    .filter((line) => line.startsWith('data: '))
    .map((line) => JSON.parse(line.slice(6)));
  const complete = events.findLast((event) => event.type === 'complete');
  if (!complete?.indexToken) throw new Error('Template scan did not return an index token.');
  return complete;
}

async function prewarmTemplates(runRoot, fixture) {
  const prepRoot = path.join(runRoot, 'template-prep');
  fs.mkdirSync(prepRoot, { recursive: true });
  const server = await startServer(prepRoot, false);
  try {
    const complete = await scanViaApi(server.baseUrl, fixture.dirSet);
    const scanTemplate = path.join(runRoot, 'template-warm-scan');
    fs.cpSync(path.join(prepRoot, 'derived'), scanTemplate, { recursive: true });
    fs.rmSync(path.join(scanTemplate, 'thumbs'), { recursive: true, force: true });
    fs.rmSync(path.join(scanTemplate, 'display'), { recursive: true, force: true });

    const searchUrl = new URL('/api/search', server.baseUrl);
    searchUrl.searchParams.set('q', '');
    searchUrl.searchParams.set('page', '0');
    searchUrl.searchParams.set('size', '100');
    searchUrl.searchParams.set('dir', fixture.dirSet);
    searchUrl.searchParams.set('indexToken', complete.indexToken);
    const searchResponse = await fetch(searchUrl);
    if (!searchResponse.ok) throw new Error(`Template search failed with ${searchResponse.status}.`);
    const search = await searchResponse.json();
    const images = Array.isArray(search.results) ? search.results.slice(0, 64) : [];
    for (let index = 0; index < images.length; index += 8) {
      await Promise.all(images.slice(index, index + 8).map(async (image) => {
        const imageUrl = new URL(image.fileUrl, server.baseUrl);
        imageUrl.searchParams.set('indexToken', complete.indexToken);
        imageUrl.searchParams.set('priority', 'visible');
        const response = await fetch(imageUrl);
        if (!response.ok) throw new Error(`Template thumbnail failed with ${response.status}.`);
        await response.arrayBuffer();
      }));
    }
    const thumbTemplate = path.join(runRoot, 'template-warm-thumbs');
    fs.cpSync(path.join(prepRoot, 'derived'), thumbTemplate, { recursive: true });
    return { scanTemplate, thumbTemplate, warmedThumbnailCount: images.length };
  } finally {
    await server.stop();
  }
}

function prepareConditionRoot(runRoot, templates, condition, iteration, traceEnabled) {
  const target = path.join(
    runRoot,
    'runs',
    `${String(iteration).padStart(3, '0')}-${condition}-${traceEnabled ? 'trace' : 'control'}`,
  );
  fs.mkdirSync(target, { recursive: true });
  const derived = path.join(target, 'derived');
  if (condition === 'B') fs.cpSync(templates.scanTemplate, derived, { recursive: true });
  if (condition === 'C') fs.cpSync(templates.thumbTemplate, derived, { recursive: true });
  return target;
}

function installBrowserTrace(context) {
  return context.addInitScript(() => {
    const trace = {
      scanComplete: null,
      firstCardEpochMs: null,
      paints: [],
      longTasks: [],
    };
    Object.defineProperty(window, '__PV_CRITICAL_TRACE__', { value: trace });

    const NativeEventSource = window.EventSource;
    window.EventSource = class TracedEventSource extends NativeEventSource {
      constructor(url, options) {
        super(url, options);
        this.addEventListener('message', (event) => {
          try {
            const payload = JSON.parse(event.data);
            if (payload.type === 'complete') {
              trace.scanComplete = {
                epochMs: performance.timeOrigin + performance.now(),
                total: payload.total,
                perf: payload.perf ?? null,
              };
            }
          } catch {}
        });
      }
    };

    const observeCards = () => {
      if (trace.firstCardEpochMs != null) return;
      if (document.querySelector('.image-card:not(.placeholder)')) {
        trace.firstCardEpochMs = performance.timeOrigin + performance.now();
      }
    };
    const installCardObserver = () => {
      if (!document.documentElement) {
        requestAnimationFrame(installCardObserver);
        return;
      }
      observeCards();
      new MutationObserver(observeCards).observe(document.documentElement, { childList: true, subtree: true });
    };
    installCardObserver();

    window.addEventListener('load', (event) => {
      const image = event.target;
      if (!(image instanceof HTMLImageElement)) return;
      const card = image.closest('.image-card:not(.placeholder)');
      if (!card) return;
      const gridIndex = Number(card.getAttribute('data-grid-index'));
      requestAnimationFrame(() => {
        const paintedAt = performance.now();
        const resource = performance.getEntriesByName(image.currentSrc).at(-1);
        trace.paints.push({
          gridIndex: Number.isFinite(gridIndex) ? gridIndex : null,
          epochMs: performance.timeOrigin + paintedAt,
          requestToPaintMs: resource ? paintedAt - resource.startTime : null,
          responseToPaintMs: resource ? paintedAt - resource.responseEnd : null,
        });
      });
    }, true);

    try {
      new PerformanceObserver((list) => {
        for (const entry of list.getEntries()) {
          trace.longTasks.push({ startTime: entry.startTime, duration: entry.duration });
        }
      }).observe({ type: 'longtask', buffered: true });
    } catch {}
  });
}

function endpointKind(responseUrl) {
  const url = new URL(responseUrl);
  if (url.pathname === '/api/search') return 'search';
  if (url.pathname === '/api/scan') return 'scan';
  if (url.pathname !== '/api/image') return null;
  if (url.searchParams.get('display') === 'true') return 'display';
  if (url.searchParams.get('thumb') === 'true') return 'thumb';
  return 'original';
}

async function visibleGridIndexes(page) {
  return page.evaluate(() => {
    const scroll = document.querySelector('.viewer-main');
    if (!(scroll instanceof HTMLElement)) return [];
    const viewport = scroll.getBoundingClientRect();
    return [...document.querySelectorAll('.image-card:not(.placeholder)')]
      .filter((card) => {
        const rect = card.getBoundingClientRect();
        return rect.bottom > viewport.top && rect.top < viewport.bottom;
      })
      .map((card) => Number(card.getAttribute('data-grid-index')))
      .filter(Number.isFinite);
  });
}

async function waitForViewportFill(page, indexes, timeoutMs = 90_000) {
  return page.evaluate(({ targetIndexes, timeout }) => new Promise((resolve, reject) => {
    const required = Math.max(1, Math.ceil(targetIndexes.length * 0.9));
    const startedAt = performance.now();
    const recorded = new Set();
    const paints = [];
    const sample = () => {
      const now = performance.now();
      let loaded = 0;
      for (const index of targetIndexes) {
        const image = document.querySelector(`.image-card[data-grid-index="${index}"] img`);
        if (!(image instanceof HTMLImageElement) || !image.complete || image.naturalWidth <= 0) continue;
        loaded += 1;
        if (recorded.has(index)) continue;
        recorded.add(index);
        const resource = performance.getEntriesByName(image.currentSrc).at(-1);
        paints.push({
          epochMs: performance.timeOrigin + now,
          requestToPaintMs: resource ? now - resource.startTime : null,
          responseToPaintMs: resource ? now - resource.responseEnd : null,
        });
      }
      if (loaded >= required) {
        const epochs = paints.map((paint) => paint.epochMs).sort((a, b) => a - b);
        const gaps = epochs.slice(1).map((value, index) => value - epochs[index]);
        resolve({
          epochMs: performance.timeOrigin + now,
          expected: targetIndexes.length,
          loaded,
          paints,
          maxInterPaintGapMs: gaps.length > 0 ? Math.max(...gaps) : 0,
        });
        return;
      }
      if (now - startedAt >= timeout) {
        reject(new Error(`Timed out at ${loaded}/${required} painted viewport images.`));
        return;
      }
      requestAnimationFrame(sample);
    };
    requestAnimationFrame(sample);
  }), { targetIndexes: indexes, timeout: timeoutMs });
}

function summarizeServerEvents(events) {
  const summarizeKind = (kind) => {
    const selected = events.filter((event) => event.kind === kind);
    const timingNames = new Set(selected.flatMap((event) => Object.keys(event.timing)));
    return {
      count: selected.length,
      statusErrors: selected.filter((event) => event.status >= 400).length,
      cache: Object.fromEntries([...new Set(selected.map((event) => event.cache).filter(Boolean))]
        .map((cache) => [cache, selected.filter((event) => event.cache === cache).length])),
      timings: Object.fromEntries([...timingNames].map((name) => [name, {
        median: percentile(selected.map((event) => event.timing[name]), 50),
        p95: percentile(selected.map((event) => event.timing[name]), 95),
        max: selected.length > 0 ? Math.max(...selected.map((event) => event.timing[name] ?? 0)) : null,
      }])),
    };
  };
  return Object.fromEntries(['scan', 'search', 'thumb', 'display', 'original'].map((kind) => [kind, summarizeKind(kind)]));
}

async function measureRun(browser, fixture, runRoot, condition, iteration, traceEnabled) {
  const server = await startServer(runRoot, traceEnabled);
  const processBefore = null;
  const context = await browser.newContext({ viewport: DEFAULT_VIEWPORT });
  await installBrowserTrace(context);
  const page = await context.newPage();
  const serverEvents = [];
  const responseTasks = [];
  let scanAcceptedEpochMs = null;
  let consoleErrorCount = 0;
  let consoleWarningCount = 0;

  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrorCount += 1;
    if (message.type() === 'warning') consoleWarningCount += 1;
  });
  page.on('response', (response) => {
    const kind = endpointKind(response.url());
    if (!kind) return;
    if (kind === 'scan' && scanAcceptedEpochMs == null) scanAcceptedEpochMs = Date.now();
    responseTasks.push((async () => {
      serverEvents.push({
        kind,
        status: response.status(),
        timing: parseServerTiming(await response.headerValue('server-timing')),
        cache: await response.headerValue(kind === 'search' ? 'x-pv-search-cache' : 'x-pv-image-cache'),
      });
    })());
  });

  try {
    const navigationStartedEpochMs = Date.now();
    await page.goto(server.baseUrl, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { level: 1, name: 'PhotoViewer' }).waitFor({ timeout: 30_000 });
    const shellReadyEpochMs = Date.now();

    await page.getByPlaceholder('Paste one absolute path per line...').fill(fixture.dirSet);
    await page.getByRole('button', { name: 'Add pasted' }).click();
    const scanClickEpochMs = Date.now();
    await page.getByRole('button', { name: 'Open folder set' }).click();
    await page.waitForFunction(() => window.__PV_CRITICAL_TRACE__?.scanComplete, undefined, { timeout: 120_000 });
    const scanTrace = await page.evaluate(() => window.__PV_CRITICAL_TRACE__.scanComplete);
    await page.locator('.image-card:not(.placeholder)').first().waitFor({ timeout: 60_000 });
    const firstCardEpochMs = Date.now();
    await page.waitForFunction(() => {
      const image = document.querySelector('.image-card:not(.placeholder) img');
      return image instanceof HTMLImageElement && image.complete && image.naturalWidth > 0;
    }, undefined, { timeout: 90_000 });
    const firstPaintEpochMs = await page.evaluate(() => performance.timeOrigin + performance.now());

    const initialIndexes = await visibleGridIndexes(page);
    if (initialIndexes.length === 0) throw new Error('No initial viewport cards were measurable.');
    const initialFill = await waitForViewportFill(page, initialIndexes);

    await page.evaluate(() => {
      const scroll = document.querySelector('.viewer-main');
      if (scroll instanceof HTMLElement) scroll.scrollTop += Math.max(300, Math.floor(scroll.clientHeight * 0.9));
    });
    await page.waitForFunction((largestInitialIndex) => {
      const scroll = document.querySelector('.viewer-main');
      if (!(scroll instanceof HTMLElement) || scroll.scrollTop <= 0) return false;
      const viewport = scroll.getBoundingClientRect();
      return [...document.querySelectorAll('.image-card:not(.placeholder)')].some((card) => {
        const rect = card.getBoundingClientRect();
        return rect.bottom > viewport.top
          && rect.top < viewport.bottom
          && Number(card.getAttribute('data-grid-index')) > largestInitialIndex;
      });
    }, Math.max(...initialIndexes), { timeout: 30_000 });
    const nextIndexes = await visibleGridIndexes(page);
    if (nextIndexes.length === 0) throw new Error('No second viewport cards were measurable.');
    const nextFill = await waitForViewportFill(page, nextIndexes);

    const modalClickEpochMs = Date.now();
    await page.locator(`.image-card[data-grid-index="${nextIndexes[0]}"] [data-image-primary="true"]`).dblclick();
    await page.waitForFunction(() => {
      const image = document.querySelector('.modal-full-image.is-full-ready');
      return image instanceof HTMLImageElement && image.complete && image.naturalWidth > 0;
    }, undefined, { timeout: 90_000 });
    const modalPaintEpochMs = await page.evaluate(() => performance.timeOrigin + performance.now());

    const clientTrace = await page.evaluate(() => ({
      longTasks: window.__PV_CRITICAL_TRACE__.longTasks,
    }));
    await Promise.all(responseTasks);
    const processAfter = sampleProcess(server.child.pid);
    const t2 = scanAcceptedEpochMs ?? scanClickEpochMs;
    const t3 = scanTrace.epochMs;
    const t7 = nextFill.epochMs;
    const viewportPaints = [...initialFill.paints, ...nextFill.paints];
    return {
      condition,
      iteration,
      traceEnabled,
      counts: {
        scanResults: scanTrace.total,
        initialViewport: initialFill.expected,
        initialViewportPainted: initialFill.loaded,
        nextViewport: nextFill.expected,
        nextViewportPainted: nextFill.loaded,
      },
      timingsMs: {
        startup: round(server.readyEpochMs - server.spawnEpochMs),
        shellNavigation: round(shellReadyEpochMs - navigationStartedEpochMs),
        scanAccept: round(t2 - scanClickEpochMs),
        scan: round(scanTrace.perf?.routeMs ?? t3 - t2),
        scanStreamAfterHeaders: round(t3 - t2),
        scanWall: round(t3 - scanClickEpochMs),
        postScan: round(firstCardEpochMs - t3),
        firstThumbnailPaint: round(firstPaintEpochMs - firstCardEpochMs),
        initialThumbnailFill: round(initialFill.epochMs - firstCardEpochMs),
        continuedThumbnailFill: round(t7 - initialFill.epochMs),
        modalFocusedDisplay: round(modalPaintEpochMs - modalClickEpochMs),
        totalPerceived: round(t7 - server.spawnEpochMs),
        maxInterPaintGap: round(Math.max(initialFill.maxInterPaintGapMs, nextFill.maxInterPaintGapMs)),
        responseToPaintMedian: percentile(viewportPaints.map((paint) => paint.responseToPaintMs), 50),
      },
      scanBreakdown: scanTrace.perf ?? null,
      server: summarizeServerEvents(serverEvents),
      client: {
        thumbnailPaintCount: viewportPaints.length,
        longTaskCount: clientTrace.longTasks.length,
        longTaskTotalMs: round(clientTrace.longTasks.reduce((sum, task) => sum + task.duration, 0)),
        longTaskMaxMs: percentile(clientTrace.longTasks.map((task) => task.duration), 100),
        consoleErrorCount,
        consoleWarningCount,
      },
      runtime: runtimeDelta(processBefore, processAfter),
    };
  } finally {
    await context.close();
    await server.stop();
  }
}

function runBuild(buildRoot) {
  const buildEnv = { ...process.env, ...privateStateEnvironment(buildRoot), PV_PERF_TRACE: '0' };
  const result = spawnSync(process.execPath, [NEXT_BIN, 'build'], {
    cwd: ROOT,
    env: buildEnv,
    stdio: 'inherit',
    windowsHide: true,
  });
  if (result.status !== 0) throw new Error('Production build failed before critical-path measurement.');
}

function copyTemplateOrEmpty(runRoot, templates, condition, iteration, traceEnabled) {
  return prepareConditionRoot(runRoot, templates, condition, iteration, traceEnabled);
}

function overheadSummary(controlRuns, tracedRuns) {
  const metrics = ['totalPerceived', 'scan', 'initialThumbnailFill', 'continuedThumbnailFill'];
  return {
    metrics: Object.fromEntries(metrics.map((metric) => {
      const control = percentile(controlRuns.map((run) => run.timingsMs?.[metric]), 95);
      const traced = percentile(tracedRuns.map((run) => run.timingsMs?.[metric]), 95);
      return [metric, { controlP95: control, tracedP95: traced, regressionPercent: regressionPercent(control, traced) }];
    })),
    runtime: Object.fromEntries(['cpuMs', 'peakWorkingSetBytes', 'readBytes', 'writeBytes'].map((metric) => {
      const control = percentile(controlRuns.map((run) => run.runtime?.[metric]), 95);
      const traced = percentile(tracedRuns.map((run) => run.runtime?.[metric]), 95);
      return [metric, { controlP95: control, tracedP95: traced, regressionPercent: regressionPercent(control, traced) }];
    })),
  };
}

async function main() {
  const args = parseArgs(process.argv);
  const runCount = positiveInt(args.runs, 1);
  const overheadRuns = positiveInt(args['overhead-runs'], 0);
  const fileCount = positiveInt(args['file-count'], 10_000);
  const largeCount = Math.min(fileCount, positiveInt(args['large-count'], 96));
  const keepTemp = booleanArg(args['keep-temp'], false);
  const buildRequested = booleanArg(args.build, false);
  if (buildRequested && booleanArg(args['skip-build'], false)) {
    throw new Error('Use either --build or --skip-build, not both.');
  }
  const requestedConditions = new Set(String(args.conditions ?? 'A,B,C').split(',').map((value) => value.trim().toUpperCase()));
  for (const condition of requestedConditions) {
    if (!['A', 'B', 'C'].includes(condition)) throw new Error(`Unsupported condition ${condition}.`);
  }

  const tempRunRoot = assertTempChild(fs.mkdtempSync(path.join(os.tmpdir(), 'h25-critical-path-')));
  const outputPath = path.resolve(String(args.output ?? path.join(tempRunRoot, 'result.json')));
  const outputInsideRunRoot = path.relative(tempRunRoot, outputPath);
  const shouldPreserveOutput = outputInsideRunRoot && !outputInsideRunRoot.startsWith('..') && !path.isAbsolute(outputInsideRunRoot);
  let completed = false;
  let browser;
  try {
    const fixture = await createFixture(tempRunRoot, fileCount, largeCount);
    if (buildRequested) {
      process.stderr.write(
        'Building the shared repository .next output. Stop any normal PhotoViewer server before continuing.\n',
      );
      runBuild(path.join(tempRunRoot, 'build-state'));
    }
    const needsTemplates = requestedConditions.has('B') || requestedConditions.has('C') || overheadRuns > 0;
    const templates = needsTemplates
      ? await prewarmTemplates(tempRunRoot, fixture)
      : { scanTemplate: '', thumbTemplate: '', warmedThumbnailCount: 0 };
    browser = await chromium.launch();
    const runs = [];
    let sequence = 0;
    for (let roundIndex = 0; roundIndex < runCount; roundIndex += 1) {
      const order = roundIndex % 2 === 0 ? ['A', 'B', 'C'] : ['C', 'B', 'A'];
      for (const condition of order.filter((value) => requestedConditions.has(value))) {
        sequence += 1;
        const conditionRoot = copyTemplateOrEmpty(tempRunRoot, templates, condition, sequence, true);
        runs.push(await measureRun(browser, fixture, conditionRoot, condition, roundIndex + 1, true));
      }
    }

    const controlRuns = [];
    const tracedOverheadRuns = [];
    for (let index = 0; index < overheadRuns; index += 1) {
      const pair = index % 2 === 0 ? [false, true] : [true, false];
      for (const traceEnabled of pair) {
        sequence += 1;
        const conditionRoot = copyTemplateOrEmpty(tempRunRoot, templates, 'B', sequence, traceEnabled);
        const run = await measureRun(browser, fixture, conditionRoot, 'B', index + 1, traceEnabled);
        (traceEnabled ? tracedOverheadRuns : controlRuns).push(run);
      }
    }

    const summary = summarizeRuns(runs);
    const artifact = {
      schema: SCHEMA,
      recordedAtUtc: new Date().toISOString(),
      exactInputs: {
        baseSha: BASE_SHA,
        focusedCandidateSha: FOCUSED_SHA,
        sameOriginCandidateSha: SAME_ORIGIN_SHA,
        localHead: gitOutput(['rev-parse', 'HEAD']),
        localTree: gitOutput(['rev-parse', 'HEAD^{tree}']),
        workingDiffSha256: sha256(gitOutput(['diff', '--binary', 'HEAD'])),
      },
      environment: {
        node: process.version,
        platform: process.platform,
        release: os.release(),
        arch: process.arch,
        cpu: os.cpus()[0]?.model ?? 'unknown',
        logicalCpuCount: os.cpus().length,
        totalMemoryBytes: os.totalmem(),
        viewport: DEFAULT_VIEWPORT,
      },
      fixture: fixture.publicManifest,
      execution: {
        buildMode: buildRequested ? 'explicit-shared-root-build' : 'existing-build',
        runCountPerCondition: runCount,
        overheadRuns,
        order: 'A-B-C / C-B-A alternating',
        warmedThumbnailCount: templates.warmedThumbnailCount,
      },
      runs,
      summary,
      diagnosis: diagnoseCriticalPath(summary),
      overhead: overheadRuns > 0 ? overheadSummary(controlRuns, tracedOverheadRuns) : null,
      safety: {
        syntheticOsTempOnly: true,
        pathsRetainedInArtifact: false,
        enhancementJobsExpected: 0,
        originalDirtyCheckoutTouched: false,
        sharedBuildExplicitlyRequested: buildRequested,
      },
    };
    fs.mkdirSync(path.dirname(outputPath), { recursive: true });
    fs.writeFileSync(outputPath, `${JSON.stringify(artifact, null, 2)}\n`, 'utf8');
    completed = true;
    process.stdout.write(`${JSON.stringify({
      outputPath,
      runCount: runs.length,
      diagnosis: artifact.diagnosis,
      overhead: artifact.overhead,
    }, null, 2)}\n`);
  } finally {
    if (browser) await browser.close();
    if (!keepTemp && completed && !shouldPreserveOutput) {
      fs.rmSync(assertTempChild(tempRunRoot), { recursive: true, force: true });
    }
  }
}

await main();

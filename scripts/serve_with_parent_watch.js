/**
 * Starts `pnpm start` and stops it when the launcher process disappears.
 *
 * This protects Windows users from orphaned Next.js servers when the launcher
 * console is closed abruptly.
 */
const { spawn, spawnSync } = require('child_process');
const path = require('path');

const ROOT = path.join(__dirname, '..');
const WATCHDOG_SCRIPT = path.join(__dirname, 'kill_when_parent_exits.js');
const NEXT_BIN = require.resolve('next/dist/bin/next', { paths: [ROOT] });
const parentPid = Number(process.argv[2]);
const port = process.argv[3];
let serverChild = null;
let cleanedUp = false;

process.stdout.on('error', () => {});
process.stderr.on('error', () => {});

function isProcessAlive(pid) {
  if (!pid || !Number.isFinite(pid)) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

function killProcessTree(pid) {
  if (!pid) return;
  if (process.platform === 'win32') {
    spawnSync('taskkill.exe', ['/pid', String(pid), '/t', '/f'], {
      stdio: 'ignore',
      windowsHide: true,
    });
    return;
  }

  try {
    process.kill(pid, 'SIGTERM');
  } catch {
    // Already stopped.
  }
}

function safeWrite(stream, data) {
  try {
    stream.write(data);
  } catch {
    // Parent pipe may already be gone.
  }
}

function cleanup() {
  if (cleanedUp) return;
  cleanedUp = true;
  if (serverChild && !serverChild.killed) {
    killProcessTree(serverChild.pid);
  }
}

if (!port) {
  console.error('[Photoviewer] Missing port for server wrapper.');
  process.exit(1);
}

serverChild = spawn(process.execPath, [NEXT_BIN, 'start', '-p', String(port)], {
  cwd: ROOT,
  stdio: ['ignore', 'pipe', 'pipe'],
  windowsHide: true,
});

const watchdog = spawn(process.execPath, [
  WATCHDOG_SCRIPT,
  String(parentPid),
  String(serverChild.pid),
  String(port),
], {
  cwd: ROOT,
  detached: true,
  stdio: 'ignore',
  windowsHide: true,
});
watchdog.unref();

serverChild.stdout.on('data', (data) => safeWrite(process.stdout, data));
serverChild.stderr.on('data', (data) => safeWrite(process.stderr, data));

serverChild.on('error', (err) => {
  console.error(`[Photoviewer] Failed to launch server: ${err.message}`);
  process.exit(1);
});

serverChild.on('close', (code) => {
  serverChild = null;
  cleanedUp = true;
  process.exit(code ?? 0);
});

const parentWatch = setInterval(() => {
  if (!isProcessAlive(parentPid)) {
    cleanup();
    clearInterval(parentWatch);
    process.exit(0);
  }
}, 1000);

process.once('exit', cleanup);
for (const signal of ['SIGINT', 'SIGTERM', 'SIGHUP']) {
  process.once(signal, () => {
    cleanup();
    process.exit(signal === 'SIGINT' ? 130 : 0);
  });
}

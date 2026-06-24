/**
 * Detached Windows-friendly watchdog.
 *
 * It survives if the launcher console is closed, then kills the server process
 * tree when the launcher PID disappears.
 */
const { spawnSync } = require('child_process');

const parentPid = Number(process.argv[2]);
const targetPid = Number(process.argv[3]);
const port = Number(process.argv[4]);

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
  if (!pid || !Number.isFinite(pid)) return;
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

function killPortOwner(portNumber) {
  if (!portNumber || !Number.isFinite(portNumber) || process.platform !== 'win32') return;
  const script = [
    `$port = ${portNumber}`,
    'Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |',
    '  Select-Object -ExpandProperty OwningProcess -Unique |',
    '  Where-Object { $_ -gt 0 } |',
    '  ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }',
  ].join('\n');
  spawnSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', script], {
    stdio: 'ignore',
    windowsHide: true,
  });
}

if (!isProcessAlive(parentPid)) {
  killProcessTree(targetPid);
  killPortOwner(port);
  process.exit(0);
}

const timer = setInterval(() => {
  if (!isProcessAlive(parentPid)) {
    killProcessTree(targetPid);
    killPortOwner(port);
    clearInterval(timer);
    process.exit(0);
  }

  if (!isProcessAlive(targetPid)) {
    clearInterval(timer);
    process.exit(0);
  }
}, 1000);

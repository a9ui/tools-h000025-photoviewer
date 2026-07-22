import fs from 'node:fs';
import path from 'node:path';

import { parseArgs, requireArg } from './lib/args.mjs';
import { diagnoseCriticalPath, summarizeRuns } from './lib/criticalPathAnalysis.mjs';

const args = parseArgs(process.argv);
const inputPath = path.resolve(requireArg(args, 'input'));
const outputPath = path.resolve(String(args.output ?? inputPath));
const artifact = JSON.parse(fs.readFileSync(inputPath, 'utf8'));

if (artifact.schema !== 'h25.browser-critical-path/v1' || !Array.isArray(artifact.runs)) {
  throw new Error('Unsupported critical-path artifact.');
}

for (const run of artifact.runs) {
  const routeMs = run.scanBreakdown?.routeMs;
  if (!Number.isFinite(routeMs)) continue;
  if (!Number.isFinite(run.timingsMs.scanStreamAfterHeaders)) {
    run.timingsMs.scanStreamAfterHeaders = run.timingsMs.scan;
  }
  run.timingsMs.scan = routeMs;
}
artifact.analysisRevision = 2;
artifact.summary = summarizeRuns(artifact.runs);
artifact.diagnosis = diagnoseCriticalPath(artifact.summary);

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, `${JSON.stringify(artifact, null, 2)}\n`, 'utf8');
process.stdout.write(`${JSON.stringify({ outputPath, diagnosis: artifact.diagnosis }, null, 2)}\n`);

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { parseArgs, requireArg } from './lib/args.mjs';
import { collectEnvironment } from './lib/env.mjs';
import { loadFixtureManifest } from './lib/fixture.mjs';
import { buildScenarios, resolveScenarioNames } from './lib/scenarios.mjs';
import { ARTIFACT_KIND, ARTIFACT_SCHEMA, BUDGETS_PATH } from './lib/schema.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');

function loadBudgets() {
  const budgetsPath = path.join(ROOT, BUDGETS_PATH);
  return JSON.parse(fs.readFileSync(budgetsPath, 'utf8'));
}

function ensureParentDir(filePath) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
}

export function buildBaselineArtifact(options) {
  const budgets = loadBudgets();
  const scenarioNames = resolveScenarioNames(options.scenario);
  const fixture = loadFixtureManifest(options.fixtureManifest ?? null);
  const environment = collectEnvironment({
    cacheState: options.cacheState ?? 'unknown',
    buildMode: options.buildMode ?? 'production',
    buildCommand: options.buildCommand ?? 'pnpm build',
  });

  const scenarios = buildScenarios(scenarioNames, budgets);

  return {
    schema: ARTIFACT_SCHEMA,
    kind: ARTIFACT_KIND,
    recordedAt: environment.timestamp,
    budgetsRef: BUDGETS_PATH,
    environment,
    fixture,
    scenarioNames,
    scenarios,
    rawObservations: [],
    summary: {
      overall: 'pending_fixture',
      measuredScenarioCount: 0,
      pendingScenarioCount: scenarioNames.length,
      fixtureStatus: fixture.status,
    },
  };
}

function main() {
  const parsed = parseArgs(process.argv);
  const outputPath = path.resolve(requireArg(parsed, 'output'));
  const artifact = buildBaselineArtifact({
    scenario: typeof parsed.scenario === 'string' ? parsed.scenario : 'all',
    fixtureManifest: typeof parsed['fixture-manifest'] === 'string' ? parsed['fixture-manifest'] : null,
    cacheState: typeof parsed['cache-state'] === 'string' ? parsed['cache-state'] : 'unknown',
    buildMode: typeof parsed['build-mode'] === 'string' ? parsed['build-mode'] : 'production',
    buildCommand: typeof parsed['build-command'] === 'string' ? parsed['build-command'] : 'pnpm build',
  });

  ensureParentDir(outputPath);
  fs.writeFileSync(outputPath, `${JSON.stringify(artifact, null, 2)}\n`, 'utf8');

  console.log(
    JSON.stringify(
      {
        ok: true,
        output: outputPath,
        scenarioNames: artifact.scenarioNames,
        fixtureStatus: artifact.fixture.status,
        summary: artifact.summary,
      },
      null,
      2,
    ),
  );
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    main();
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  }
}

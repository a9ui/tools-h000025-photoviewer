import path from 'path';

export const DERIVED_CACHE_ROOT_ENV = 'PVU_DERIVED_CACHE_ROOT';

export function resolveDerivedCacheRoot(
  environment: Readonly<Record<string, string | undefined>> = process.env,
  workingDirectory = process.cwd(),
) {
  const configured = environment[DERIVED_CACHE_ROOT_ENV];
  if (configured === undefined) {
    return path.join(workingDirectory, '.cache');
  }
  if (!configured || configured.includes('\0') || !path.isAbsolute(configured)) {
    throw new Error(`${DERIVED_CACHE_ROOT_ENV} must be an absolute directory path.`);
  }
  return path.resolve(configured);
}

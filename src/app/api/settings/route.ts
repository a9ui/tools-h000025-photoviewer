import { NextResponse } from 'next/server';
import { promises as fs } from 'fs';
import path from 'path';

import { withFileWriteLock } from '@/lib/fileWriteLock';
import { hasKeyBindingConflicts, normalizeKeyBinding } from '@/lib/keyBindings';
import type { AppSettings, KeyBindings } from '@/lib/types';
import { DEFAULT_KEY_BINDINGS } from '@/lib/types';

export const dynamic = 'force-dynamic';

type SettingsDocument = Record<string, unknown>;

const KEY_BINDING_NAMES = Object.keys(DEFAULT_KEY_BINDINGS) as Array<keyof KeyBindings>;
const MAX_KEY_BINDING_LENGTH = 64;
const FILMSTRIP_MIGRATION_KEYS = [DEFAULT_KEY_BINDINGS.toggleFilmstrip, 'b', 'g', 'v', 'y'] as const;

function settingsPath() {
  return process.env.PVU_SETTINGS_PATH
    ? path.resolve(process.env.PVU_SETTINGS_PATH)
    : path.join(/*turbopackIgnore: true*/ process.cwd(), '.cache', 'settings.json');
}

function isObject(value: unknown): value is SettingsDocument {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function validateKeyBindings(value: unknown): value is Partial<KeyBindings> & SettingsDocument {
  if (!isObject(value)) return false;
  for (const name of KEY_BINDING_NAMES) {
    if (!Object.hasOwn(value, name)) continue;
    const key = value[name];
    if (typeof key !== 'string' || key.length === 0 || key.length > MAX_KEY_BINDING_LENGTH) {
      return false;
    }
  }
  return true;
}

function validateStoredDocument(value: unknown): value is SettingsDocument {
  if (!isObject(value)) return false;
  if (Object.hasOwn(value, 'confirmBeforeDelete') && typeof value.confirmBeforeDelete !== 'boolean') {
    return false;
  }
  if (Object.hasOwn(value, 'keyBindings') && !validateKeyBindings(value.keyBindings)) {
    return false;
  }
  return true;
}

function publicSettings(document: SettingsDocument): AppSettings {
  const storedBindings = validateKeyBindings(document.keyBindings)
    ? document.keyBindings
    : {};
  const keyBindings = Object.fromEntries(KEY_BINDING_NAMES.map((name) => [
    name,
    typeof storedBindings[name] === 'string'
      ? storedBindings[name]
      : DEFAULT_KEY_BINDINGS[name],
  ])) as unknown as KeyBindings;

  // `toggleFilmstrip` was added after the original settings schema. Preserve a
  // user's existing T assignment instead of silently making two modal actions
  // fire from the same key. The fallback is only for an old document that has
  // no explicit filmstrip binding; an explicitly saved collision remains
  // visible to the normal Settings conflict repair flow.
  if (typeof storedBindings.toggleFilmstrip !== 'string') {
    const usedKeys = new Set(KEY_BINDING_NAMES
      .filter((name) => name !== 'toggleFilmstrip')
      .map((name) => normalizeKeyBinding(keyBindings[name])));
    keyBindings.toggleFilmstrip = FILMSTRIP_MIGRATION_KEYS.find(
      (candidate) => !usedKeys.has(normalizeKeyBinding(candidate)),
    ) ?? DEFAULT_KEY_BINDINGS.toggleFilmstrip;
  }

  return {
    keyBindings,
    confirmBeforeDelete: typeof document.confirmBeforeDelete === 'boolean'
      ? document.confirmBeforeDelete
      : true,
  };
}

async function readSettings(): Promise<
  { ok: true; document: SettingsDocument; settings: AppSettings; malformed: false } |
  { ok: false; document: SettingsDocument; settings: AppSettings; malformed: true; error: string }
> {
  const target = settingsPath();
  try {
    const raw = await fs.readFile(target, 'utf8');
    const parsed: unknown = JSON.parse(raw);
    if (!validateStoredDocument(parsed)) {
      return {
        ok: false,
        document: {},
        settings: publicSettings({}),
        malformed: true,
        error: 'settings.json does not match the supported schema.',
      };
    }
    return {
      ok: true,
      document: parsed,
      settings: publicSettings(parsed),
      malformed: false,
    };
  } catch (error) {
    if ((error as NodeJS.ErrnoException)?.code === 'ENOENT') {
      return {
        ok: true,
        document: {},
        settings: publicSettings({}),
        malformed: false,
      };
    }
    return {
      ok: false,
      document: {},
      settings: publicSettings({}),
      malformed: true,
      error: error instanceof Error ? error.message : String(error),
    };
  }
}

function validateIncomingDocument(value: unknown):
  { ok: true; update: SettingsDocument } |
  { ok: false; error: string } {
  if (!isObject(value)) {
    return { ok: false, error: 'Request body must be an object.' };
  }
  if (Object.hasOwn(value, 'confirmBeforeDelete') && typeof value.confirmBeforeDelete !== 'boolean') {
    return { ok: false, error: 'confirmBeforeDelete must be a boolean.' };
  }
  if (Object.hasOwn(value, 'keyBindings') && !validateKeyBindings(value.keyBindings)) {
    return { ok: false, error: 'keyBindings must contain only bounded string bindings.' };
  }
  if (!Object.hasOwn(value, 'confirmBeforeDelete') && !Object.hasOwn(value, 'keyBindings')) {
    return { ok: false, error: 'Request body must include a supported setting.' };
  }
  return { ok: true, update: value };
}

async function writeSettings(document: SettingsDocument) {
  const target = settingsPath();
  const dir = path.dirname(target);
  const temp = path.join(dir, `settings-${process.pid}-${Date.now()}.tmp`);
  await fs.mkdir(dir, { recursive: true });
  try {
    await fs.writeFile(temp, `${JSON.stringify(document, null, 2)}\n`, 'utf8');
    await fs.rename(temp, target);
  } finally {
    await fs.unlink(temp).catch(() => {});
  }
}

export async function GET() {
  const result = await readSettings();
  return NextResponse.json({
    ...result.settings,
    malformed: result.malformed,
    error: result.ok ? undefined : result.error,
  });
}

export async function PUT(request: Request) {
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ ok: false, error: 'Request body must be valid JSON.' }, { status: 400 });
  }
  const incoming = validateIncomingDocument(body);
  if (!incoming.ok) {
    return NextResponse.json({ ok: false, error: incoming.error }, { status: 400 });
  }

  try {
    return await withFileWriteLock(settingsPath(), async () => {
      const current = await readSettings();
      if (!current.ok) {
        return NextResponse.json({
          ok: false,
          error: 'Shared settings JSON is malformed; refusing to overwrite it.',
          ...current.settings,
          malformed: true,
        }, { status: 409 });
      }

      const incomingBindings = isObject(incoming.update.keyBindings)
        ? incoming.update.keyBindings
        : {};
      const currentBindings = isObject(current.document.keyBindings)
        ? current.document.keyBindings
        : {};
      const updated: SettingsDocument = {
        ...current.document,
        ...incoming.update,
        keyBindings: {
          ...currentBindings,
          ...incomingBindings,
        },
      };
      if (Object.hasOwn(incoming.update, 'keyBindings')
        && hasKeyBindingConflicts(publicSettings(updated).keyBindings)) {
        return NextResponse.json({
          ok: false,
          error: 'Key bindings must not assign the same key to multiple actions.',
          ...current.settings,
          malformed: false,
        }, { status: 409 });
      }
      await writeSettings(updated);
      return NextResponse.json({
        ok: true,
        ...publicSettings(updated),
        malformed: false,
      });
    });
  } catch (error) {
    return NextResponse.json({
      ok: false,
      error: error instanceof Error ? error.message : String(error),
      malformed: false,
    }, { status: 503 });
  }
}

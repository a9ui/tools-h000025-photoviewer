import { NextRequest, NextResponse } from 'next/server';
import fs from 'fs';
import path from 'path';
import type { AppSettings } from '@/lib/types';
import { DEFAULT_KEY_BINDINGS } from '@/lib/types';

export const dynamic = 'force-dynamic';

const SETTINGS_PATH = path.join(/*turbopackIgnore: true*/ process.cwd(), '.cache', 'settings.json');

function loadSettings(): AppSettings {
  try {
    if (fs.existsSync(SETTINGS_PATH)) {
      const parsed = JSON.parse(fs.readFileSync(SETTINGS_PATH, 'utf-8')) as AppSettings;
      return {
        keyBindings: { ...DEFAULT_KEY_BINDINGS, ...(parsed.keyBindings || {}) },
        confirmBeforeDelete: parsed.confirmBeforeDelete ?? true,
      };
    }
  } catch { /* use defaults */ }
  return { keyBindings: DEFAULT_KEY_BINDINGS, confirmBeforeDelete: true };
}

function saveSettings(settings: AppSettings) {
  const dir = path.dirname(SETTINGS_PATH);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  fs.writeFileSync(SETTINGS_PATH, JSON.stringify(settings, null, 2), 'utf-8');
}

export async function GET() {
  return NextResponse.json(loadSettings());
}

export async function PUT(request: NextRequest) {
  const body = await request.json();
  const current = loadSettings();
  const updated: AppSettings = {
    ...current,
    ...body,
    keyBindings: { ...current.keyBindings, ...(body.keyBindings || {}) },
  };
  saveSettings(updated);
  return NextResponse.json(updated);
}

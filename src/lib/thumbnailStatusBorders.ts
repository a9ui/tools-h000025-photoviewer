import {
  DEFAULT_THUMBNAIL_STATUS_BORDERS,
  type ThumbnailStatusBorderPreference,
  type ThumbnailStatusBorderSettings,
} from './types';

const HEX_COLOR_PATTERN = /^#[0-9a-f]{6}$/i;

function isObject(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function isValidPreference(value: unknown): value is Partial<ThumbnailStatusBorderPreference> & Record<string, unknown> {
  if (!isObject(value)) return false;
  if (Object.hasOwn(value, 'enabled') && typeof value.enabled !== 'boolean') return false;
  if (Object.hasOwn(value, 'color') && (typeof value.color !== 'string' || !HEX_COLOR_PATTERN.test(value.color))) {
    return false;
  }
  return true;
}

export function isValidThumbnailStatusBordersDocument(value: unknown): value is Record<string, unknown> {
  if (!isObject(value)) return false;
  if (Object.hasOwn(value, 'favorite') && !isValidPreference(value.favorite)) return false;
  if (Object.hasOwn(value, 'enhanced') && !isValidPreference(value.enhanced)) return false;
  return true;
}

function normalizePreference(
  value: unknown,
  fallback: ThumbnailStatusBorderPreference,
): ThumbnailStatusBorderPreference {
  if (!isValidPreference(value)) return { ...fallback };
  return {
    enabled: typeof value.enabled === 'boolean' ? value.enabled : fallback.enabled,
    color: typeof value.color === 'string' ? value.color.toLowerCase() : fallback.color,
  };
}

export function normalizeThumbnailStatusBorders(value: unknown): ThumbnailStatusBorderSettings {
  const document = isValidThumbnailStatusBordersDocument(value) ? value : {};
  return {
    favorite: normalizePreference(document.favorite, DEFAULT_THUMBNAIL_STATUS_BORDERS.favorite),
    enhanced: normalizePreference(document.enhanced, DEFAULT_THUMBNAIL_STATUS_BORDERS.enhanced),
  };
}

export interface ThumbnailStatusBorderPresentation {
  favoriteColor: string | null;
  enhancedColor: string | null;
}

export function getThumbnailStatusBorderPresentation({
  favorite,
  enhanced,
  settings,
}: {
  favorite: boolean;
  enhanced: boolean;
  settings: ThumbnailStatusBorderSettings;
}): ThumbnailStatusBorderPresentation {
  return {
    favoriteColor: favorite && settings.favorite.enabled ? settings.favorite.color : null,
    enhancedColor: enhanced && settings.enhanced.enabled ? settings.enhanced.color : null,
  };
}

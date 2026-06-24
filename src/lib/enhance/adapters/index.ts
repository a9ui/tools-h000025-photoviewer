import type { EnhancementAdapter } from '../types';
import { comfyUiAdapter } from './comfyUiAdapter';
import { ncnnVulkanAdapter } from './ncnnVulkanAdapter';
import { sharpTestAdapter } from './sharpTestAdapter';

const ADAPTERS: Record<string, EnhancementAdapter> = {
  [sharpTestAdapter.id]: sharpTestAdapter,
  [ncnnVulkanAdapter.id]: ncnnVulkanAdapter,
  [comfyUiAdapter.id]: comfyUiAdapter,
};

export function getEnhancementAdapter(id: string) {
  return ADAPTERS[id] ?? null;
}

export function listEnhancementAdapters() {
  return Object.values(ADAPTERS);
}

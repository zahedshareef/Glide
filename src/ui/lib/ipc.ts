import { invoke } from '@tauri-apps/api/core';

export type BackendStatus = 'connected' | 'disconnected';

export interface BackendInfo {
  status: BackendStatus;
  version?: string;
  protocolVersion?: string;
}

export interface ScrollProfileSnapshot {
  StepSize?: number;
  AnimationTime?: number;
  AccelerationDelta?: number;
  AccelerationMax?: number;
  TailToHeadRatio?: number;
  AnimationEasing?: boolean;
  ShiftKeyHorizontal?: boolean;
  HorizontalSmoothness?: boolean;
  ReverseDirection?: boolean;
  SpeedThresholdForSmooth?: number;
  ClickToStop?: boolean;
}

export interface GlobalSettingsSnapshot {
  IsEnabled: boolean;
  StartMinimized: boolean;
  AutoStart: boolean;
  DisableTouchpad: boolean;
  FrameRateAdaptive: boolean;
  ScaleWithDpi: boolean;
  DiagnosticsMode: boolean;
  BypassHotkeyVk: number;
  GlobalProfile: ScrollProfileSnapshot;
  AppOverrides: Record<string, ScrollProfileSnapshot>;
  FilterMode: 'Blacklist' | 'Whitelist';
  FilterList: string[];
}

export async function getBackendInfo(): Promise<BackendInfo> {
  try {
    return await invoke<BackendInfo>('get_backend_status');
  } catch {
    return { status: 'disconnected' };
  }
}

export async function getSettings(): Promise<GlobalSettingsSnapshot | null> {
  if (!('__TAURI_INTERNALS__' in window)) return null;

  try {
    return await invoke<GlobalSettingsSnapshot>('get_settings');
  } catch {
    return null;
  }
}

export type BooleanSetting =
  | 'IsEnabled'
  | 'StartMinimized'
  | 'AutoStart'
  | 'DisableTouchpad'
  | 'FrameRateAdaptive'
  | 'ScaleWithDpi'
  | 'DiagnosticsMode';

export async function updateSetting(field: BooleanSetting, value: boolean): Promise<GlobalSettingsSnapshot | null> {
  if (!('__TAURI_INTERNALS__' in window)) return null;

  try {
    return await invoke<GlobalSettingsSnapshot>('update_setting', { field, value });
  } catch {
    return null;
  }
}

export type ProfileSetting =
  | 'StepSize'
  | 'AnimationTime'
  | 'AccelerationDelta'
  | 'AccelerationMax'
  | 'TailToHeadRatio'
  | 'SpeedThresholdForSmooth'
  | 'AnimationEasing'
  | 'ShiftKeyHorizontal'
  | 'HorizontalSmoothness'
  | 'ReverseDirection'
  | 'ClickToStop';

export async function updateProfileSetting(
  field: ProfileSetting,
  value: number | boolean,
): Promise<GlobalSettingsSnapshot | null> {
  if (!('__TAURI_INTERNALS__' in window)) return null;

  try {
    return await invoke<GlobalSettingsSnapshot>('update_profile_setting', { field, value });
  } catch {
    return null;
  }
}

export type PerAppOperation = 'setFilterMode' | 'addFilter' | 'addOverride' | 'remove';

export async function updatePerApp(
  operation: PerAppOperation,
  options: { appName?: string; filterMode?: 'Blacklist' | 'Whitelist' } = {},
): Promise<GlobalSettingsSnapshot | null> {
  if (!('__TAURI_INTERNALS__' in window)) return null;

  try {
    return await invoke<GlobalSettingsSnapshot>('update_per_app', {
      operation,
      appName: options.appName,
      filterMode: options.filterMode,
    });
  } catch {
    return null;
  }
}

export type DiagnosticsAction = 'checkForUpdates' | 'openLogFolder';

export async function runDiagnosticsAction(action: DiagnosticsAction): Promise<boolean> {
  if (!('__TAURI_INTERNALS__' in window)) return false;

  try {
    await invoke('run_diagnostics_action', { action });
    return true;
  } catch {
    return false;
  }
}

export interface DiagnosticsLog {
  text: string;
  lineCount: number;
  truncated: boolean;
}

export async function getDiagnosticsLog(): Promise<DiagnosticsLog | null> {
  if (!('__TAURI_INTERNALS__' in window)) return null;

  try {
    return await invoke<DiagnosticsLog>('get_diagnostics_log');
  } catch {
    return null;
  }
}

export async function clearDiagnosticsLog(): Promise<boolean> {
  if (!('__TAURI_INTERNALS__' in window)) return false;

  try {
    await invoke('clear_diagnostics_log');
    return true;
  } catch {
    return false;
  }
}

export async function exportSettings(): Promise<boolean> {
  if (!('__TAURI_INTERNALS__' in window)) return false;

  try {
    return await invoke<boolean>('export_settings');
  } catch {
    return false;
  }
}

export async function importSettings(): Promise<GlobalSettingsSnapshot | null> {
  if (!('__TAURI_INTERNALS__' in window)) return null;

  try {
    return await invoke<GlobalSettingsSnapshot | null>('import_settings');
  } catch {
    return null;
  }
}

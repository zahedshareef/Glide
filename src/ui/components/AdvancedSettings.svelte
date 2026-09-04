<script lang="ts">
  import { onMount } from 'svelte';
  import { clearDiagnosticsLog, getDiagnosticsLog, runDiagnosticsAction, updateProfileSetting, updateSetting, type BooleanSetting, type DiagnosticsLog, type GlobalSettingsSnapshot } from '../lib/ipc';

  export let settings: GlobalSettingsSnapshot;
  export let onSaved: (settings: GlobalSettingsSnapshot) => void;
  export let onError: () => void;
  let actionMessage = '';
  let diagnosticsLog: DiagnosticsLog | null = null;
  let logError = false;

  const booleanFields: Array<{ field: BooleanSetting; label: string; description: string }> = [
    { field: 'DisableTouchpad', label: 'Disable for touchpad', description: 'Skip smooth scrolling when a precision touchpad is detected.' },
    { field: 'FrameRateAdaptive', label: 'Frame-rate adaptive animation', description: 'Tie animation ticks to the monitor refresh rate.' },
    { field: 'ScaleWithDpi', label: 'Scale step size with DPI', description: 'Scale scroll distance on high-DPI displays.' },
    { field: 'DiagnosticsMode', label: 'Diagnostics mode', description: 'Write runtime events to the Glide diagnostics log.' },
  ];

  async function saveBoolean(field: BooleanSetting, value: boolean) {
    const updated = await updateSetting(field, value);
    if (updated) onSaved(updated);
    else onError();
  }

  async function saveThreshold(event: Event) {
    const value = Number((event.currentTarget as HTMLInputElement).value);
    const updated = await updateProfileSetting('SpeedThresholdForSmooth', value);
    if (updated) onSaved(updated);
    else onError();
  }

  async function runAction(action: 'checkForUpdates' | 'openLogFolder', successMessage: string) {
    actionMessage = '';
    if (await runDiagnosticsAction(action)) actionMessage = successMessage;
    else onError();
  }

  async function refreshLog() {
    logError = false;
    const log = await getDiagnosticsLog();
    if (log) diagnosticsLog = log;
    else logError = true;
  }

  async function clearLog() {
    if (await clearDiagnosticsLog()) {
      diagnosticsLog = { text: '', lineCount: 0, truncated: false };
      actionMessage = 'Diagnostics log cleared.';
    } else {
      logError = true;
    }
  }

  onMount(refreshLog);
</script>

<section class="advanced-settings" aria-label="Advanced settings">
  <div class="advanced-section">
    <h3>Input</h3>
    <div class="advanced-list">
      {#each booleanFields.slice(0, 3) as control}
        <label class="advanced-row">
          <span><strong>{control.label}</strong><small>{control.description}</small></span>
          <input type="checkbox" checked={settings[control.field]} onchange={(event) => saveBoolean(control.field, (event.currentTarget as HTMLInputElement).checked)} />
        </label>
      {/each}
    </div>
  </div>

  <div class="advanced-section">
    <h3>Behavior</h3>
    <label class="threshold-card">
      <span class="control-heading"><span>Speed threshold for smooth scroll</span><strong>{settings.GlobalProfile.SpeedThresholdForSmooth ?? 0} delta/ms</strong></span>
      <small>Set to 0 to always smooth. Higher values let fast flicks scroll natively.</small>
      <input type="range" min="0" max="100" step="0.1" value={settings.GlobalProfile.SpeedThresholdForSmooth ?? 0} aria-label="Speed threshold for smooth scroll" onchange={saveThreshold} />
    </label>
  </div>

  <div class="advanced-section">
    <h3>Diagnostics</h3>
    <div class="advanced-list">
      {#each booleanFields.slice(3) as control}
        <label class="advanced-row">
          <span><strong>{control.label}</strong><small>{control.description}</small></span>
          <input type="checkbox" checked={settings[control.field]} onchange={(event) => saveBoolean(control.field, (event.currentTarget as HTMLInputElement).checked)} />
        </label>
      {/each}
    </div>
    <div class="diagnostics-actions">
      <button type="button" onclick={() => runAction('checkForUpdates', 'Opened the releases page.')}>Check for updates</button>
      <button type="button" class="secondary" onclick={() => runAction('openLogFolder', 'Opened the log folder.')}>Open log folder</button>
    </div>
    {#if actionMessage}<p class="action-message" role="status">{actionMessage}</p>{/if}
    <div class="log-view" aria-label="Diagnostics log">
      <div class="log-toolbar">
        <span>{diagnosticsLog ? `${diagnosticsLog.lineCount} lines` : 'Loading log…'}</span>
        <span>
          <button type="button" class="text-button" onclick={refreshLog}>Refresh</button>
          <button type="button" class="text-button" onclick={clearLog}>Clear log</button>
        </span>
      </div>
      {#if logError}
        <p class="log-empty" role="alert">Could not load the diagnostics log.</p>
      {:else if diagnosticsLog?.text}
        {#if diagnosticsLog.truncated}<p class="log-notice">Showing the most recent 500 lines.</p>{/if}
        <pre>{diagnosticsLog.text}</pre>
      {:else}
        <p class="log-empty">No diagnostics entries yet. Enable Diagnostics Mode to record runtime events.</p>
      {/if}
    </div>
  </div>
</section>

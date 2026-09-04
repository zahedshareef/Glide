<script lang="ts">
  import { exportSettings, getBackendInfo, getSettings, importSettings, updateSetting, type BackendInfo, type BooleanSetting, type GlobalSettingsSnapshot } from './lib/ipc';
  import ScrollingSettings from './components/ScrollingSettings.svelte';
  import PerAppSettings from './components/PerAppSettings.svelte';
  import AdvancedSettings from './components/AdvancedSettings.svelte';

  type Page = 'General' | 'Scrolling' | 'Per-App' | 'Advanced';

  const pages: Page[] = ['General', 'Scrolling', 'Per-App', 'Advanced'];
  const descriptions: Record<Page, string> = {
    General: 'Control how Glide starts and runs in the background.',
    Scrolling: 'Tune the feel of smooth scrolling across Windows.',
    'Per-App': 'Choose where Glide is enabled and customize exceptions.',
    Advanced: 'Fine-tune input handling, diagnostics, and compatibility.',
  };

  let activePage: Page = 'General';
  let backend: BackendInfo = { status: 'disconnected' };
  let settings: GlobalSettingsSnapshot | null = null;
  let settingsError = '';
  let settingsMessage = '';

  getBackendInfo().then((info) => {
    backend = info;
    if (info.status === 'connected') getSettings().then((value) => (settings = value));
  });

  async function changeSetting(field: BooleanSetting, value: boolean) {
    settingsError = '';
    const updated = await updateSetting(field, value);
    if (updated) settings = updated;
    else settingsError = 'Could not save this setting.';
  }

  function acceptSettings(updated: GlobalSettingsSnapshot) {
    settings = updated;
    settingsError = '';
  }

  function rejectSettings() {
    settingsError = 'Could not save this setting.';
  }

  async function exportCurrentSettings() {
    settingsError = '';
    settingsMessage = await exportSettings() ? 'Settings exported.' : 'Settings export was cancelled or failed.';
  }

  async function importSavedSettings() {
    settingsError = '';
    const updated = await importSettings();
    if (updated) {
      settings = updated;
      settingsMessage = 'Settings imported.';
    } else {
      settingsError = 'Settings import was cancelled or failed.';
    }
  }
</script>

<svelte:head>
  <title>{activePage} · Glide Settings</title>
</svelte:head>

<div class="window-shell">
  <div class="menu-bar" aria-label="Application menu">
    <span>File</span>
    <span>Settings</span>
    <span>View</span>
    <span>Help</span>
  </div>
  <header class="titlebar">
    <div class="brand-mark" aria-hidden="true">G</div>
    <div>
      <p class="eyebrow">GLIDE</p>
      <h1>Settings</h1>
    </div>
    <div class:connected={backend.status === 'connected'} class="header-status">
      <span class="status-dot" aria-hidden="true"></span>
      <span>{backend.status === 'connected' ? 'Connected' : 'Connecting'}</span>
    </div>
  </header>

  <div class="app-layout">
    <nav class="sidebar" aria-label="Settings sections">
      <div class="nav-label">Configure</div>
      {#each pages as page}
        <button class:active={activePage === page} class="nav-item" onclick={() => (activePage = page)}>
          <span class="nav-icon" aria-hidden="true">
            {page === 'General' ? '◷' : page === 'Scrolling' ? '↕' : page === 'Per-App' ? '▣' : '✦'}
          </span>
          <span>{page}</span>
        </button>
      {/each}

      <div class="sidebar-footer">
        <span class:connected={backend.status === 'connected'} class="status-dot" aria-hidden="true"></span>
        <span>{backend.status === 'connected' ? 'Backend connected' : 'Backend bridge pending'}</span>
      </div>
    </nav>

    <main class="content" aria-live="polite">
      <div class="content-heading">
        <div>
          <p class="eyebrow">SETTINGS / {activePage.toUpperCase()}</p>
          <h2>{activePage === 'General' ? 'General Settings' : activePage}</h2>
          <p class="description">{descriptions[activePage]}</p>
        </div>
        <span class="phase-badge">{backend.status === 'connected' ? 'Live settings' : 'Read-only preview'}</span>
      </div>

      {#if activePage === 'General' && settings}
        <section class="settings-panel" aria-label="Loaded general settings">
          <label class="setting-row"><span>Enable Glide</span><input type="checkbox" checked={settings.IsEnabled} onchange={(event) => changeSetting('IsEnabled', event.currentTarget.checked)} /></label>
          <label class="setting-row"><span>Start with Windows</span><input type="checkbox" checked={settings.AutoStart} onchange={(event) => changeSetting('AutoStart', event.currentTarget.checked)} /></label>
          <label class="setting-row"><span>Start minimized</span><input type="checkbox" checked={settings.StartMinimized} onchange={(event) => changeSetting('StartMinimized', event.currentTarget.checked)} /></label>
          <label class="setting-row"><span>Disable for touchpad</span><input type="checkbox" checked={settings.DisableTouchpad} onchange={(event) => changeSetting('DisableTouchpad', event.currentTarget.checked)} /></label>
          <div class="setting-row"><span>Filter mode</span><strong>{settings.FilterMode}</strong></div>
          <div class="setting-row"><span>Configured app entries</span><strong>{settings.FilterList.length + Object.keys(settings.AppOverrides).length}</strong></div>
          <div class="settings-actions">
            <button type="button" onclick={exportCurrentSettings}>Export JSON</button>
            <button type="button" class="secondary" onclick={importSavedSettings}>Import JSON</button>
          </div>
        </section>
        {#if settingsMessage}<p class="settings-message" role="status">{settingsMessage}</p>{/if}
        {#if settingsError}<p class="settings-error" role="alert">{settingsError}</p>{/if}
      {:else if activePage === 'Scrolling' && settings}
        <ScrollingSettings settings={settings} onSaved={acceptSettings} onError={rejectSettings} />
        {#if settingsError}<p class="settings-error" role="alert">{settingsError}</p>{/if}
      {:else if activePage === 'Per-App' && settings}
        <PerAppSettings settings={settings} onSaved={acceptSettings} onError={rejectSettings} />
        {#if settingsError}<p class="settings-error" role="alert">{settingsError}</p>{/if}
      {:else if activePage === 'Advanced' && settings}
        <AdvancedSettings settings={settings} onSaved={acceptSettings} onError={rejectSettings} />
        {#if settingsError}<p class="settings-error" role="alert">{settingsError}</p>{/if}
      {:else}
        <section class="placeholder-panel" aria-label={`${activePage} settings placeholder`}>
          <div class="placeholder-icon" aria-hidden="true">{activePage === 'General' ? '◷' : '✦'}</div>
          <h3>{activePage === 'General' ? 'Waiting for backend settings' : `${activePage} controls are next`}</h3>
          <p>{activePage === 'General' ? 'The settings window will show the saved C# configuration when running inside Tauri.' : 'This Svelte surface is connected to the navigation shell. Its controls will be wired through the typed C# bridge in a later slice.'}</p>
        </section>
      {/if}
    </main>
  </div>
</div>

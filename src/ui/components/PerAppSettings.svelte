<script lang="ts">
  import { updatePerApp, type GlobalSettingsSnapshot } from '../lib/ipc';

  export let settings: GlobalSettingsSnapshot;
  export let onSaved: (settings: GlobalSettingsSnapshot) => void;
  export let onError: () => void;

  let appName = '';

  $: entries = [
    ...settings.FilterList.map((name) => ({ name, kind: 'Filter', profile: undefined })),
    ...Object.entries(settings.AppOverrides).map(([name, profile]) => ({ name, kind: 'Override', profile })),
  ];

  async function save(operation: 'addFilter' | 'addOverride' | 'remove', name = appName) {
    const trimmed = name.trim();
    if (!trimmed) return;
    const updated = await updatePerApp(operation, { appName: trimmed });
    if (updated) {
      appName = '';
      onSaved(updated);
    } else onError();
  }

  async function changeFilterMode(filterMode: 'Blacklist' | 'Whitelist') {
    const updated = await updatePerApp('setFilterMode', { filterMode });
    if (updated) onSaved(updated);
    else onError();
  }
</script>

<section class="per-app-settings" aria-label="Per-app settings">
  <div class="filter-mode" role="group" aria-label="Filter mode">
    <span class="section-label">Filter mode</span>
    <label><input type="radio" name="filter-mode" value="Blacklist" checked={settings.FilterMode === 'Blacklist'} onchange={() => changeFilterMode('Blacklist')} /> Blacklist</label>
    <label><input type="radio" name="filter-mode" value="Whitelist" checked={settings.FilterMode === 'Whitelist'} onchange={() => changeFilterMode('Whitelist')} /> Whitelist</label>
  </div>

  <div class="add-app">
    <label for="app-name">Application process</label>
    <div class="add-controls">
      <input id="app-name" bind:value={appName} placeholder="e.g. chrome or notepad" autocomplete="off" />
      <button type="button" onclick={() => save('addFilter')}>Add to filter</button>
      <button type="button" class="secondary" onclick={() => save('addOverride')}>Add override</button>
    </div>
  </div>

  {#if entries.length === 0}
    <div class="empty-list" role="status">No per-app entries configured yet.</div>
  {:else}
    <div class="app-table" role="table" aria-label="Configured applications">
      <div class="app-table-row app-table-header" role="row">
        <span>Application</span><span>Type</span><span>Step</span><span>Animation</span><span></span>
      </div>
      {#each entries as entry}
        <div class="app-table-row" role="row">
          <span>{entry.name}</span>
          <span>{entry.kind}</span>
          <span>{entry.profile?.StepSize ?? '—'}</span>
          <span>{entry.profile?.AnimationTime ?? '—'}</span>
          <button type="button" class="remove-button" aria-label={`Remove ${entry.name}`} onclick={() => save('remove', entry.name)}>Remove</button>
        </div>
      {/each}
    </div>
  {/if}
</section>

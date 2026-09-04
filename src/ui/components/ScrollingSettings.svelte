<script lang="ts">
  import { updateProfileSetting, type GlobalSettingsSnapshot, type ProfileSetting } from '../lib/ipc';

  export let settings: GlobalSettingsSnapshot;
  export let onSaved: (settings: GlobalSettingsSnapshot) => void;
  export let onError: () => void;

  const numericFields: Array<{ field: ProfileSetting; label: string; min: number; max: number; step: number; unit: string }> = [
    { field: 'StepSize', label: 'Step size', min: 20, max: 500, step: 1, unit: 'px' },
    { field: 'AnimationTime', label: 'Animation time', min: 50, max: 2000, step: 10, unit: 'ms' },
    { field: 'AccelerationDelta', label: 'Acceleration window', min: 1, max: 500, step: 1, unit: 'ms' },
    { field: 'AccelerationMax', label: 'Acceleration maximum', min: 1, max: 20, step: 0.1, unit: '×' },
    { field: 'TailToHeadRatio', label: 'Tail/head ratio', min: 1, max: 10, step: 0.1, unit: '×' },
    { field: 'SpeedThresholdForSmooth', label: 'Speed threshold', min: 0, max: 100, step: 0.1, unit: 'delta/ms' },
  ];

  const booleanFields: Array<{ field: ProfileSetting; label: string }> = [
    { field: 'AnimationEasing', label: 'Animation easing' },
    { field: 'ShiftKeyHorizontal', label: 'Shift for horizontal scrolling' },
    { field: 'HorizontalSmoothness', label: 'Smooth horizontal scrolling' },
    { field: 'ReverseDirection', label: 'Reverse direction' },
    { field: 'ClickToStop', label: 'Click to stop animation' },
  ];

  function value(field: ProfileSetting): number | boolean {
    return settings.GlobalProfile[field as keyof typeof settings.GlobalProfile] as number | boolean;
  }

  async function save(field: ProfileSetting, next: number | boolean) {
    const updated = await updateProfileSetting(field, next);
    if (updated) onSaved(updated);
    else onError();
  }
</script>

<section class="scrolling-settings" aria-label="Scrolling settings">
  <div class="control-grid">
    {#each numericFields as control}
      <label class="control-card">
        <span class="control-heading">
          <span>{control.label}</span>
          <strong>{value(control.field)} {control.unit}</strong>
        </span>
        <input
          type="range"
          min={control.min}
          max={control.max}
          step={control.step}
          value={value(control.field) as number}
          aria-label={control.label}
          onchange={(event) => save(control.field, Number((event.currentTarget as HTMLInputElement).value))}
        />
      </label>
    {/each}
  </div>

  <div class="toggle-list">
    {#each booleanFields as control}
      <label class="toggle-row">
        <span>{control.label}</span>
        <input
          type="checkbox"
          checked={value(control.field) as boolean}
          onchange={(event) => save(control.field, (event.currentTarget as HTMLInputElement).checked)}
        />
      </label>
    {/each}
  </div>
</section>

# Fish CRT UI

Reusable client-side controls for CM-SS13-inspired interfaces. The library provides locally scoped palettes,
semantic colors, icons, and optional display effects without changing the global SS14 stylesheet.

## Usage

Wrap CRT controls in an `FishCrtThemeScope`:

```xml
<crt:FishCrtThemeScope Palette="Blue" Effects="HorizontalScanlines">
    <BoxContainer Orientation="Vertical">
        <crt:FishCrtLabel Text="{Loc example-title}" Heading="True" />
        <crt:FishCrtSeparator />
        <crt:FishCrtActionButton Text="{Loc example-action}"
                                IconState="warning"
                                Variant="Filled" />
    </BoxContainer>
</crt:FishCrtThemeScope>
```

All user-facing text must use Fluent localization. Normal Robust properties such as `Disabled`, `MinSize`,
`HorizontalExpand`, margins, and alignment continue to work.

Available reusable controls:

- `FishCrtThemeScope` - local palette, stylesheet, background, border, and root effects;
- `FishCrtPanel` - surface, inset, transparent, or warning panel;
- `FishCrtActionButton` - outline, filled, navigation, or danger action;
- `FishCrtLabel` - normal, heading, or semantic status text;
- `FishCrtSeparator` - horizontal or vertical separator;
- `FishCrtIcon` - palette-aware RSI icon.

Prefer semantic properties such as `Tone`, `Variant`, and `Selected` over directly changing child colors. Buttons use
`/Textures/_Fish/Interface/CRT/crt_icons.rsi` by default; matching state constants are available in `FishCrtIcons`.

## Appearance preferences

The Accessibility tab provides two archived client preferences, both enabled by default:

- `fish.crt_theme_enabled` switches the library between CRT and standard Nano presentation;
- `fish.crt_effects_enabled` controls scanlines, RGB subpixels, and diagonal warning stripes.

Disabling the theme always suppresses effects without overwriting the effects preference. Re-enabling it restores the
previous effects choice. Applied preference changes update open windows, and controls added later inherit the current
appearance from their nearest scope. Nested scopes keep their own palettes.

Nano mode preserves layout, content, icons, semantic tones, and interaction state while replacing CRT palettes,
fonts, borders, color overrides, and effects with standard Nano styling.

## Adding controls

New controls in this library must:

1. remain independent from a specific console, BUI, component, or localization key;
2. support both CRT and Nano presentation;
3. implement `IFishCrtThemedControl` when consuming theme or semantic palette state;
4. resolve the nearest context through `FishCrtThemeHelpers` when entering the UI tree;
5. avoid configuration lookups and allocations in per-frame drawing code;
6. preserve normal Robust measurement and invalidation behavior.

Use `FishCrtPanel`, `FishCrtLabel`, and standard Robust containers through composition before adding another control.

# Task: export a palette

Designers want to take a ToolsApp palette into their own tools: a stylesheet, a script, or GIMP and
Inkscape. Add one endpoint that exports a stored palette in three formats.

## Endpoint

`GET /api/palettes/{id}/export?format=css|json|gpl`

| `format` | Content-Type | Download name | Body |
|----------|--------------|---------------|------|
| `css` | `text/css; charset=utf-8` | `<slug>.css` | CSS custom properties in one `:root` block |
| `json` | `application/json; charset=utf-8` | `<slug>.json` | The palette name and its colors |
| `gpl` | `application/x-gimp-palette` | `<slug>.gpl` | A GIMP palette, version 2 format |

`<slug>` is the palette name in lower case, with every run of characters other than `a-z` and `0-9`
replaced by one hyphen, and no leading or trailing hyphen (`"Deep Sea"` becomes `deep-sea`).

Every response is a download: it has a `Content-Disposition: attachment` header with the download name.
Colors appear in the order they were added (by `Id`).

### CSS

One custom property per color, named `--<palette-slug>-<color-slug>`, with the stored `#RRGGBB` value:

```css
/* Ocean palette exported from ToolsApp */
:root {
  --ocean-deep-sea: #003B5C;
  --ocean-lagoon: #00A6A6;
  --ocean-foam: #E0F7F5;
}
```

### JSON

camelCase, indented, with each color's RGB channels:

```json
{
  "name": "Ocean",
  "colors": [
    { "name": "Deep Sea", "hex": "#003B5C", "rgb": { "r": 0, "g": 59, "b": 92 } }
  ]
}
```

### GPL (GIMP palette)

The format GIMP documents at <https://developer.gimp.org/core/standards/gpl>: a `GIMP Palette` magic line,
a `Name:` line, a `Columns:` line (use the number of colors, at most 16), then one line per color with the
red, green, and blue values right-aligned in three-character columns, a tab, and the color name. Lines end
with `\n` (LF) only, on every operating system.

```text
GIMP Palette
Name: Ocean
Columns: 3
#
  0  59  92	Deep Sea
  0 166 166	Lagoon
224 247 245	Foam
```

## Errors

- Unknown palette id: `404 Not Found`.
- Missing or unknown `format`: `400` validation problem whose `format` error lists the allowed values. The
  value is case-insensitive (`CSS` works).

## Acceptance criteria

- [ ] For the seeded **Ocean** palette, the CSS and GPL bodies match the examples above exactly, and the
      JSON has the shape shown.
- [ ] Each format has the content type and download name in the table.
- [ ] GPL output uses LF line endings, even on Windows.
- [ ] Unknown id returns 404; missing or unknown format returns 400 naming `format`.
- [ ] The formatting logic lives in `ToolsApp.Domain` and has unit tests; the endpoint has API tests.
- [ ] `dotnet build` has no warnings and `dotnet test --solution ToolsApp.slnx` passes.
- [ ] No new NuGet packages.

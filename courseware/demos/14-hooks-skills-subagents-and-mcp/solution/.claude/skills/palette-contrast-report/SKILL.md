---
name: palette-contrast-report
description: Builds a WCAG contrast report for one ToolsApp palette, using the toolsapp MCP server's find_colors and check_contrast tools. Use it when someone asks which colors in a palette are readable on white or on black, or wants an accessibility check of a palette.
---

# Palette contrast report

Produce a contrast report for the palette the user names (for example "Ocean"). Don't change any files.

1. Call `find_colors` with the palette name. Keep only the colors whose `palette` field equals that name.
   If none match, say so and list the palettes you did find; don't guess.
2. For each color, call `check_contrast` twice: the color as text on `#FFFFFF`, and on `#000000`.
3. Reply with a Markdown table, one row per color, in this shape:

   | Color | Hex | On white | On black |
   |-------|-----|----------|----------|
   | Deep Sea | #003B5C | 11.79:1 AAA | 1.78:1 Fail |

4. Under the table, list:
   - colors that pass AA for body text on white, and on black;
   - colors that only pass for large text (AALarge), with a note that they suit headings, not paragraphs.

Report the ratios exactly as the tool returns them (it truncates, so 2.998:1 shows as 2.99). Never round a
ratio up to a threshold: Lagoon at 2.99:1 on white fails even for large text.

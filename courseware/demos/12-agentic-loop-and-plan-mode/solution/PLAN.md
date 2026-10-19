# Plan: palette export (as approved)

This is the plan the agent proposed in plan mode, after the instructor's edits (marked **Edited**). It was
approved before any file changed. Your agent's plan will be worded differently.

## Approach

1. **Domain: `PaletteExporter` in `src/ToolsApp.Domain/PaletteExporter.cs`.**
   - `enum PaletteExportFormat { Css, Json, Gpl }` and `record PaletteExport(FileName, ContentType, Content)`.
   - `TryParseFormat(string?)`: case-insensitive lookup of `css`, `json`, `gpl`.
     **Edited:** the first draft used `Enum.TryParse`, which also accepts `"1"` and `"99"`. Use an explicit
     lookup table instead.
   - `Slugify(name)`: lower case, runs of non `a-z0-9` become one hyphen, no leading or trailing hyphen.
   - `ToCss`, `ToJson`, `ToGpl`, each ordering colors by `Id`.
   - **Edited:** GPL lines end with `\n`, never `Environment.NewLine`; format numbers with
     `CultureInfo.InvariantCulture`.
2. **API: `GET /api/palettes/{id}/export` in `PaletteEndpoints`.**
   - Validate `format` first: 400 `ValidationProblem` keyed `format`, listing the allowed values.
   - Load the palette with its colors (`AsNoTracking`); 404 if missing.
   - Return `TypedResults.File(bytes, contentType, fileName)` so `Content-Disposition: attachment` is set.
   - Return type `Results<FileContentHttpResult, NotFound, ValidationProblem>`.
3. **Tests.**
   - `PaletteExporterTests`: slug rules, format parsing (including `"1"` rejected), exact CSS and GPL text,
     JSON shape, file names and media types.
   - `PaletteExportApiTests`: media type and `Content-Disposition` per format, GPL body, JSON color order,
     404, and 400 for missing, unknown, and numeric formats. Look up Ocean's id through `/api/palettes`
     rather than assuming it is 2.
4. **Extras:** add the export requests to `ToolsApp.Api.http`.

## Not doing

- No new packages, no changes to existing endpoints or tests.
- **Edited:** no Swagger or Scalar UI (the first draft proposed adding one).

## Verify

```powershell
dotnet build
dotnet test --solution ToolsApp.slnx
```

# Add tints for a stored color

<!-- Paste everything below this line into a new GitHub issue. -->

## Goal

Designers build hover and background states by mixing a brand color with white. Add an endpoint that
returns evenly spaced **tints** of a stored color, so they stop doing the arithmetic by hand.

## Contract

`GET /api/colors/{id}/tints?count=3`

- `count` is optional, defaults to `5`, and must be between `1` and `10`.
- Tint `k` of `count` (for `k` = 1 to `count`) moves each RGB channel `k / (count + 1)` of the way to 255:
  `channel + (255 - channel) * k / (count + 1)`, rounded to the nearest whole number with midpoints rounded
  away from zero. The base color and pure white are not included.
- Tints are ordered from darkest to lightest, as upper-case `#RRGGBB`.

Response `200 OK`:

```json
{
  "colorId": 4,
  "baseHex": "#003B5C",
  "tints": ["#406C85", "#809DAE", "#BFCED6"]
}
```

Errors:

- Unknown color id: `404 Not Found`.
- `count` outside 1 to 10: `400` validation problem keyed `count`.

## Acceptance criteria

- [ ] `GET /api/colors/{id}/tints?count=3` for Deep Sea (`#003B5C`) returns exactly the tints above.
- [ ] Without `count`, five tints come back; for Deep Sea the middle one is `#809DAE`.
- [ ] `count=0` and `count=11` return 400; an unknown id returns 404.
- [ ] The tint math lives in `ToolsApp.Domain` (next to `ColorMath`) with unit tests, and the endpoint has
      API tests through `ToolsAppApiFactory`.
- [ ] `dotnet build` has no warnings and `dotnet test --solution ToolsApp.slnx` passes.
- [ ] A request for the new endpoint is added to `src/ToolsApp.Api/ToolsApp.Api.http`.

## Constraints

- Follow `AGENTS.md`. No new NuGet packages, and no changes to existing endpoints or tests.
- Use decimal or integer arithmetic for the rounding, so a midpoint like 127.5 always rounds the same way.

## Pointers

- `src/ToolsApp.Domain/ColorMath.cs`: `ParseHex`, `ToHex`, and the `Rgb` record.
- `src/ToolsApp.Api/Endpoints/ColorEndpoints.cs`: `GetContrastAsync` is the closest pattern to copy.

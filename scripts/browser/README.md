# Lumina Browser Acceptance

Development-only checks for a freshly generated Showcase site. This package is
not bundled with the CLI or emitted into generated sites.

## Prepare and Run

From the repository root, build and generate the Showcase with the local CLI:

```powershell
dotnet build -m:1
Push-Location samples/showcase
dotnet run --no-build --no-launch-profile --project ../../src/Cli.Embedded -- generate all
dotnet run --no-build --no-launch-profile --project ../../src/Cli.Embedded -- serve --port 8098
```

Keep that server terminal open; use another terminal at the repository root:

```powershell
npm ci --prefix scripts/browser --ignore-scripts
node scripts/browser/verify-lumina.cjs http://localhost:8098/ edge
```

The Edge run requires locally installed Microsoft Edge. Alternatively install
Playwright-managed engines and select one explicitly:

```powershell
node scripts/browser/node_modules/playwright/cli.js install chromium firefox webkit
node scripts/browser/verify-lumina.cjs http://localhost:8098/ chromium
node scripts/browser/verify-lumina.cjs http://localhost:8098/ firefox
node scripts/browser/verify-lumina.cjs http://localhost:8098/ webkit
```

Use an unused port if 8098 is occupied. The supplied URL must be local and point
to the site root, ending in `/`. Stop a server using the build output before
rebuilding on Windows, or serve from a separate runtime copy. Agents must reserve
the generated directory/server and use isolated project copies when sharing a
workspace; never regenerate somebody else's actively tested output.

## Coverage

- Existing Showcase routes: Canon lightbox, Sony noninteractive, Landscapes photo pages.
- Desktop/mobile, light/dark, actual JavaScript settings, normal/reduced motion.
- All Canon triggers open the correct modal and selected image resource, close
  via native commands/Escape, and restore keyboard focus. No-JS pages hide
  unavailable previous/next controls; JS pages switch to the intended next dialog.
- Photo links navigate through the visible image, preserve canonical destination,
  show the selected photo, provide a real OG image variant and return occurrence.
- Loaded image data, unique IDs, horizontal overflow and failed HTTP responses.
- Screenshots under `artifacts/browser-checks/<engine>/` for visual review.

Contexts and their request interception are disposed in `finally`; external
requests are blocked. No remote forms are submitted. A failed check exits nonzero.
Unit/integration tests cover missing templates, malformed configuration, hostile
metadata and additional URI combinations; this browser check complements them.

## Limits

Screenshots still need inspection; DOM assertions are not a complete accessibility
audit. The suite does not certify real-device touch/zoom, assistive technology,
every navigation context or every asset prefix. Windows WebKit is not shipping
Safari on macOS/iOS. Report the actual tested engine/version and remaining gaps,
not a claim that every supported browser was tested.
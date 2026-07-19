# Microsoft Store submission — every field, with the exact value

Paste-ready. Every technical number below is read from `AppxManifest.xml` and the built
package, not guessed. Listing *text* (description, search terms) is in `LISTING.md`; this
file is the **Properties / requirements / configuration** side.

The short version of "what does it need to run": **a 64-bit Windows 10/11 desktop and about
250 MB of disk. Nothing else.** It is a self-contained package — no .NET to install, no
runtime, no internet, no configuration. That is the honest answer, and it is also a selling
point.

---

## 0. One-time: the developer account (first publish only)
| Step | Value |
|------|-------|
| Where | https://partner.microsoft.com/dashboard → register |
| Account type | **Individual** (company adds verification steps you don't need) |
| Fee | one-time **~19 USD** (no yearly renewal) |
| Then | Apps and games → New product → reserve the name **Vecto** |

If plain "Vecto" is taken at reservation time, fall back to **"Vecto — PNG to SVG"**
(the display name in the app itself stays Vecto either way).

## 1. Product reservation
| Field | Value |
|-------|-------|
| Product type | **MSIX or PWA app** (so the Store signs it — not "EXE or MSI app") |
| Name | **Vecto** |

## 2. Properties page
| Field | Value |
|-------|-------|
| Category | **Photo & video** (fits better than Productivity for a design tool) |
| Subcategory | (leave blank) |
| Secondary category | (optional; leave blank) |
| Privacy policy URL | `https://danielmevit.github.io/vecto/privacy/` |
| Website | `https://danielmevit.github.io/vecto/` |
| Support contact info | `https://github.com/danielmevit/vecto/issues` |

**Product declarations** (checkboxes — all of these are **No / unchecked** for Vecto):
- Uses the Microsoft commerce platform for purchases — no (it's free)
- Depends on non-Microsoft drivers or NT services — no
- Accesses, collects, or transmits personal information — **no** (there is no network code)
- Is a screen reader / accessibility tool — no
- Uses a Bluetooth/USB peripheral — no

> **Privacy policy note.** The app collects nothing, but the Store's certification
> routinely requires a URL for full-trust (`runFullTrust`) desktop apps regardless.
> The site already serves the one-paragraph page at `/privacy/` — nothing to do here.

## 3. Age ratings (IARC questionnaire)
Answer **No** to everything — no violence, no user-to-user content, no data collection, no
controlled-substance/gambling references. Result: **rated for everyone (3+/E)** in all regions.

## 4. Pricing and availability
| Field | Value |
|-------|-------|
| Base price | **Free** |
| Markets | **All markets** |
| Visibility | **Public** |
| Discoverability | Available in Store, discoverable |
| Release schedule | **As soon as it passes certification** |
| Device families | **Windows 10/11 Desktop** only (the package is Desktop-targeted) |

## 5. System requirements — the real numbers
In Partner Center these live under Properties → "Minimum hardware" / "Recommended hardware"
(mostly optional for desktop, but fill them — they set buyer expectations).

| Requirement | Minimum | Recommended |
|-------------|---------|-------------|
| **OS** | Windows 10 version 1809 (build **17763**) | Windows 11 |
| **Architecture** | **x64 (64-bit)** only — not x86, not ARM | x64 |
| **Processor** | Any 64-bit x86-64 CPU, 1 GHz+ | Multi-core 2 GHz+ (tracing is parallelized) |
| **Memory (RAM)** | **4 GB** | 8 GB (for 4K-photo tracing) |
| **Free disk space** | **250 MB** (installed footprint is ~161 MB, measured; self-contained .NET) | 300 MB |
| **Graphics** | Any; a desktop session (no headless/server core) | — |
| **Display resolution** | **1280 × 800** (default window is 1280 × 780) | 1920 × 1080 |
| **DirectX / GPU** | Not required (tracing is pure CPU) | Not required |
| **Internet** | **Not required** — fully offline | Not required |
| **Runtime prerequisites** | **None** — .NET 8 is bundled | None |
| **Touch** | Not required (mouse + keyboard app) | — |

**Why x64 only:** it's the only architecture built and tested today. Unlike Laydown
(whose UI stack publishes no 32-bit package), nothing blocks a win-arm64 build later —
.NET 8 WPF supports it; it's one more `dotnet publish -r` when there's a reason.

## 6. What the package declares (auto-read from the manifest — for your reference)
The Store reads these from the uploaded `.msix`; you don't type them, but this is what it
will show.

| Manifest field | Value | Meaning |
|----------------|-------|---------|
| Identity Name | `VectoTeam.Vecto` → **replaced** by the Store identity you paste me | Package identity |
| Publisher | `CN=VectoTeam` → **replaced** by the Store's `CN=<GUID>` | Who signs it |
| ProcessorArchitecture | `x64` | 64-bit |
| Min OS | `10.0.17763.0` | Windows 10 1809 |
| Max tested | `10.0.26100.0` | Windows 11 24H2 |
| Capability | `runFullTrust` | Ordinary desktop app (full trust). The **only** capability — no camera, mic, location, network, files-beyond-picker, etc. |
| File association | `.png .jpg .jpeg .bmp .gif` | "Open with → Vecto" from Explorer (never the default handler) |
| Entry point | `Windows.FullTrustApplication` | A packaged Win32 app |

There are **no restricted capabilities beyond `runFullTrust`**, which keeps certification
simple: the app asks for nothing sensitive.

## 7. Configuration required to run — none
There is nothing to configure. It's a self-contained MSIX:
- No .NET, Visual C++ redistributable, or any runtime to install — bundled.
- No account, license key, activation, or first-run setup.
- No environment variables or config files required.
- Settings the user *chooses* (colors/style/detail, window size) are one JSON file in the
  per-user profile (`%APPDATA%\Vecto\settings.json`); nothing system-wide, nothing that
  needs pre-provisioning.

## 8. Packages tab
Build the upload with:

```
powershell -ExecutionPolicy Bypass -File packaging\windows\build.ps1 -Store
```

(after setting the three `VECTO_STORE_*` variables below). Upload
`publish\Vecto-<version>-store.msix` — **unsigned; the Store signs it**. One package covers
x64 Windows 10 1809+ and Windows 11. On upload you'll see a **warning** (not an error):
"restricted capabilities require approval… runFullTrust". That's normal for every packaged
desktop app and does not block submission — it just needs the justification below.

## 8a. Submission Options — restricted capability justification (REQUIRED)
`runFullTrust` is a restricted capability, so the Store requires a written justification on
the **Submission Options** page. Paste this:

> Vecto is a full-trust Win32 desktop application (C# / .NET 8 / WPF, packaged as MSIX).
> It declares `runFullTrust` because that capability is required for any packaged desktop
> application to launch its native executable — it is not a UWP app. The capability is
> used solely to run the application's own bundled code: opening an image the user selects
> from local disk, tracing it into vector form on the CPU, and saving or copying the
> resulting SVG/PNG to a location the user chooses. Vecto makes no network connections,
> installs no services or drivers, and accesses no data beyond the files the user
> explicitly opens and saves.

A human tester reviews this, which can add a little time to certification, but it is
routinely approved for genuine desktop tools. (You do **not** need this for sideloading —
only for the Store.)

## 9. Store listing (text + images)
All the wording — description, short description, the 7 search terms, "what's new" — is in
`docs/store/LISTING.md`. Screenshot: `assets/screenshot.png` (1600 × 975, meets the ≥768 px
rule).

---

## What I still need from you
Three values from **Partner Center → your product → Product management → Product identity**
(they exist as soon as the name is reserved):
1. `VECTO_STORE_IDENTITY_NAME` — e.g. `12345DanielMevit.Vecto`
2. `VECTO_STORE_PUBLISHER` — e.g. `CN=A1B2C3D4-1234-...`
3. `VECTO_STORE_PUBLISHER_DISPLAY` — your publisher display name

Paste those three and I build the Store `.msix` you upload on the Packages tab.

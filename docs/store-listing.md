# SlickDash Microsoft Store listing pack (v1.0.1)

Paste these into Partner Center. Do not put GT, Gran, or Turismo in the **title**. Package: [SlickDash.msix](https://github.com/robFraser1111/gran-turismo-telemetry-windows/releases/download/v1.0.1/SlickDash.msix). Store-sized images are in `docs/store/`.

Privacy policy URL (after GitHub Pages is live):

`https://robfraser1111.github.io/gran-turismo-telemetry-windows/privacy.html`

Support: `https://github.com/robFraser1111/gran-turismo-telemetry-windows/issues`

---

## Properties

| Field | Value |
| --- | --- |
| Product name | SlickDash (already reserved) |
| Category | Sports |
| Subcategory | Racing (if offered; otherwise leave blank) |
| Secondary category | Utilities & tools |
| Privacy policy URL | https://robfraser1111.github.io/gran-turismo-telemetry-windows/privacy.html |
| Website | https://github.com/robFraser1111/gran-turismo-telemetry-windows |
| Support contact | https://github.com/robFraser1111/gran-turismo-telemetry-windows/issues |
| Developed by | Rob Fraser |
| Copyright | © 2026 SlickDash |
| Markets | All markets you can sell in |
| Pricing | Free |
| Free trial | None |
| Device families | Windows 10 / Windows 11 desktop only. Do **not** check Xbox, HoloLens, IoT, Surface Hub, Team. |
| Architecture | x64 |
| Minimum OS | Windows 10 version 2004 (build 19041) |
| System requirements | PC and PlayStation 5 on the same LAN. Gran Turismo 7 running, in a car. |

### Product declarations

- Accessibility: leave unchecked (do not claim)
- Independent security review: no
- Copilot / AI: no
- 3rd-party analytics: yes (Sentry crash reports only)

---

## Store listing (English)

**Description** (required; no URLs in this field):

SlickDash is a live telemetry overlay for Gran Turismo 7. Put it on a second monitor. It reads the race from your PlayStation 5 on the local network and shows tire temps, fuel, delta, and lap times while you drive.

Three views:

Simple (default) for HUD-on: tires and fuel only.

Driving for HUD-off: gear, speed, RPM, delta, throttle and brake, tires, fuel.

Pit wall: session-best delta, last 100 laps this session, tires, fuel.

Find PS5 on launch. Manual IP if your network needs it. Live session only. No map. No account.

Not affiliated with Sony Interactive Entertainment or Polyphony Digital. Gran Turismo 7 is a trademark of Sony Interactive Entertainment Inc.

**What's new in this version:**

First Microsoft Store release. Find PS5 on launch, this-session ghost delta, 100-lap pit-wall table.

**App features** (one per line, max 20):

Live tire temperatures and fuel
Find PS5 on your LAN, or connect by IP
Simple, Driving, and Pit wall views
This-session delta vs your best flying lap
Last 100 laps this session on pit wall
Second-monitor companion, no HUD map
Works with Gran Turismo 7 on PlayStation 5

**Screenshot captions** (upload Desktop screenshots only, in this order):

1. docs/store/simple.png — Simple view: tire temps and fuel for HUD-on driving
2. docs/store/driving.png — Driving view: gear, speed, delta, and traces for HUD-off
3. docs/store/pit-wall.png — Pit wall: session delta and this-session lap table

**Store logos**

- 1:1 app tile: docs/store/tile-300.png (300×300)
- Optional 16:9 super hero (no text): docs/store/hero-1920x1080.png

Do not upload Xbox, Holographic, or 2:3 poster art.

**Search terms** (skip any Partner Center rejects as trademarks):

telemetry
dashboard
racing
sim racing
playstation
tires
fuel

**Notes for certification:**

SlickDash is a Win32 desktop companion packaged as MSIX. It uses the runFullTrust restricted capability and privateNetworkClientServer so it can bind UDP port 33740 and send LAN heartbeats to a PlayStation 5 running Gran Turismo 7. internetClient is for optional crash reports to Sentry. There is no Microsoft account, no Xbox, no in-app purchase. To test: Windows 10/11 PC and a PS5 on the same LAN, start Gran Turismo 7 and enter a car, launch SlickDash; it should find the console without typing an IP. Unsigned package is expected; Store signing happens at certification.

---

## Age ratings (IARC questionnaire)

Answer **No** to violence, sexual content, language, drugs, alcohol, gambling, horror, online player communication, user-generated content, and in-app purchases.

| Topic | Answer |
| --- | --- |
| Users can interact / chat / UGC | No |
| Shares physical location | No |
| Unrestricted internet access | Yes (crash reports; not a browser) |
| Collects personal info | Yes — crash diagnostics (see privacy policy) |
| In-app products / ads | No |
| Digital goods trading | No |

Expected result: **Everyone** / PEGI 3 / similar.

---

## Partner Center order

1. Apps and games → SlickDash → Start a new submission (first one is always this form).
2. Pricing and availability: Free, all markets you want, public visibility.
3. Properties: category, privacy URL, support URL, desktop only.
4. Age ratings: run the questionnaire with the answers above.
5. Packages: upload SlickDash.msix from the v1.0.0 GitHub Release. If Publisher display name mismatches, Partner Center will say so; the package Publisher CN is `CN=780EEB07-AD4C-450B-9032-B746B84CCBC5` and PublisherDisplayName is `Rob Fraser`.
6. Store listing: paste description, features, three screenshots, 300×300 tile, optional hero.
7. Submission options: no limited audience unless you want a private flight first.
8. Submit.

Certification is usually a few days. You will get email from Partner Center. Do not submit a second package until this one is in or rejected; Store versions cannot go backwards.

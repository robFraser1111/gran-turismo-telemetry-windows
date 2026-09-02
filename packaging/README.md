# SlickDash MSIX

Unsigned `SlickDash.msix` for GitHub Releases (sideload). The Store upload is a separate manual workflow.

## Identity

`AppxManifest.xml` uses placeholder identity `SlickDash` / `CN=SlickDash`. That is fine for a GitHub Release sideload.

Before the first Store submit, Partner Center must already have a **live** SlickDash listing (first submission is always manual). Then set GitHub Actions secrets and optional identity overrides so the package matches Partner Center exactly:

| Secret | Used by |
| --- | --- |
| `AZURE_AD_TENANT_ID` | Store submit |
| `AZURE_AD_APPLICATION_CLIENT_ID` | Store submit |
| `AZURE_AD_APPLICATION_SECRET` | Store submit |
| `SELLER_ID` | Store submit (Partner Center seller / publisher id) |
| `STORE_PRODUCT_ID` | Store submit (SlickDash Store product id) |
| `STORE_IDENTITY_NAME` | Optional. Package `Identity Name` from Partner Center |
| `STORE_PUBLISHER` | Optional. Package `Publisher` (`CN=...`) from Partner Center |

## Sideload

Windows 11: `Add-AppxPackage -AllowUnsigned .\SlickDash.msix`

The package is full-trust Win32 (LAN UDP to GT7 plus Sentry). Not Authenticode-signed.

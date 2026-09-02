# SlickDash MSIX

Unsigned `SlickDash.msix` for GitHub Releases (sideload). Store upload is manual in Partner Center.

## Identity

Package identity matches Partner Center:

- Name: `RobFraser.SlickDash`
- Publisher: `CN=780EEB07-AD4C-450B-9032-B746B84CCBC5`

`Pack.ps1` stamps those by default. Override with `STORE_IDENTITY_NAME` / `STORE_PUBLISHER` only if Partner Center changes them.

The optional **store-submit** Action still needs Azure/Partner Center API secrets (see below). Manual Partner Center upload does not.

| Secret | Used by |
| --- | --- |
| `AZURE_AD_TENANT_ID` | Store submit |
| `AZURE_AD_APPLICATION_CLIENT_ID` | Store submit |
| `AZURE_AD_APPLICATION_SECRET` | Store submit |
| `SELLER_ID` | Store submit (Partner Center seller / publisher id) |
| `STORE_PRODUCT_ID` | Store submit (SlickDash Store product id) |
| `STORE_IDENTITY_NAME` | Optional pack override |
| `STORE_PUBLISHER` | Optional pack override (`CN=...`) |

## Sideload

Windows 11: `Add-AppxPackage -AllowUnsigned .\SlickDash.msix`

The package is full-trust Win32 (LAN UDP to GT7 plus Sentry). Not Authenticode-signed. Microsoft Store re-signs on certification.

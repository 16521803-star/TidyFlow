# Privacy Policy — TidyFlow

**Effective Date:** 2026-11-24  
**Developer:** Thang Huynh  
**Contact / Support:** https://github.com/16521803-star/TidyFlow/issues

## 1. Local Processing & No Data Collection
TidyFlow is a desktop utility that runs locally on your Windows PC. The developer does **not** collect, sell, store, or transmit your personal data, files, filenames, or usage telemetry.

- **File Organization, Batch Renaming, Duplicate Scanning, Space Cleaning, and Image Resizing** operate entirely on your local disk.
- **Settings, Custom Rules, Name Lists, and Undo Logs** are stored locally on your machine under `%LocalAppData%\TidyFlow\`.
- **Windows Native OCR** uses the built-in Windows `Windows.Media.Ocr` API offline on your device.

## 2. Optional Network Features
TidyFlow only makes network requests when you explicitly trigger or enable optional features:
- **Update Checks:** Checking for updates queries the public GitHub Releases feed at `https://github.com/16521803-star/TidyFlow`.
- **External AI / Ollama Endpoint (Optional):** By default, the *Smart OCR & AI* organizer runs 100% offline. If you explicitly enable *Use External AI / Local Ollama Endpoint*, TidyFlow sends your prompt instruction, filenames, and optional short text snippets solely to the endpoint URL you configured (for example, your local Ollama server or your chosen AI provider).

## 3. Changes to This Policy
Updates to this Privacy Policy will be posted in this repository (`PRIVACY.md`).

# Pre-launch testing checklist — AI File Butler

Run this on a **clean Windows 10/11** that does **NOT** have the .NET SDK/runtime
installed (a fresh VM, or a spare/new PC, or a new Windows user account). The goal
is to prove the app works for a real first-time user — not just on the dev machine.

> Already verified on the dev machine: builds clean, runs, self-contained single
> exe (no external DLLs), proper version metadata, Bitdefender did not flag it,
> first-run is safe (manual mode), uncertain files go to `_Review`.

## 1. Self-contained — runs without .NET ⭐ most important
- [ ] Copy **`AIFileButler-Setup.exe`** to the clean machine.
- [ ] Do **not** install .NET. Run the installer.
- [ ] App launches and the 🤵 tray icon appears.
      → If it launches, the bundled runtime works. (If it complains about .NET,
      the publish wasn't self-contained — re-check the publish flags.)

## 2. SmartScreen (expected — the app is unsigned for now)
- [ ] On first run you'll likely see **"Windows protected your PC"**.
- [ ] Click **More info → Run anyway**. This is expected for an unsigned app.
- [ ] 📝 Add a short line to the README/FAQ telling users this is normal until the
      EV certificate is purchased (after donations). Example:
      *"Windows may warn 'Unknown publisher' — click More info → Run anyway. The
      app is safe and 100% offline; a code-signing certificate is coming soon."*

## 3. Antivirus / false positives
- [ ] Keep the machine's antivirus (Defender/Bitdefender/etc.) **on** during install
      and first run. Confirm nothing is quarantined.
- [ ] Recommended: upload **both** `AIFileButler.exe` and `AIFileButler-Setup.exe`
      to **https://www.virustotal.com** for a multi-engine check.
      A couple of obscure engines flagging an unsigned self-contained .NET exe is
      common (heuristics); 0–2 detections is usually fine, many is a red flag.
      *(Note: VirusTotal makes the file shareable with security vendors — only do
      this when you're OK with that.)*

## 4. Core features (smoke test)
- [ ] **First run** shows the Welcome screen; app is in **manual** mode (nothing moves).
- [ ] **Settings**: change Language → whole UI updates. Pick a watched folder & destination.
- [ ] Drop a few files in the watched folder → click **Organize now** → they sort correctly.
- [ ] **Undo last batch** puts them back.
- [ ] **Low-confidence** file (e.g. a weird `.xyz`) lands in `_Sorted\_Review\`.
- [ ] **Auto-organize** toggle persists after you quit and relaunch.
- [ ] **Start with Windows** → tick it, reboot, confirm it auto-starts.

## 5. AI mode (optional, needs Ollama)
- [ ] Install Ollama + `ollama pull llama3.1:8b`. Status line shows **AI (Ollama)**.
- [ ] A generic-named invoice PDF/photo gets a descriptive name + correct category.
- [ ] Without Ollama installed → app still runs in **rules** mode (no crash).

## 6. OCR / language packs
- [ ] On a machine **without** an OCR language pack, photos still sort (just no OCR
      text) — confirm no crash. (OCR uses the built-in Windows engine when present.)

## 7. Uninstall
- [ ] Uninstall from **Settings → Apps** (or the Start-menu uninstall shortcut).
- [ ] Confirm the install folder, Start-menu shortcut and the `Run` startup entry
      are removed; the app no longer auto-starts.

---

When all boxes pass, the app is ready for an unsigned public launch. Signing with
an EV certificate (after donations) removes the SmartScreen warning in step 2.

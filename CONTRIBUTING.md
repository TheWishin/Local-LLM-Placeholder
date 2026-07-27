# Contributing & making your own version

This project is **open source under the MIT license** – you may copy it, change
it, rebrand it and ship your own version, commercially or not. A star or a link
back is appreciated but not required.

## Ways to help

- Report a bug or a false detection (an issue with a short example text helps a lot).
- Add detection patterns for your country/domain.
- Improve a translation or add a new UI language.
- Improve the design.

## Project layout

```
src/DataAnonymizer/          Blazor web app (UI, 4 languages)  — C#
src/DataAnonymizer.Core/     Detection + Ollama client (no web deps) — C#
tests/DataAnonymizer.Tests/  C# test suite
extension/                   Chrome/Edge extension — same engine in JavaScript
installers/                  One-click setup scripts bundled into releases
.github/workflows/           Build/test + release automation
```

The **C# core** (`AnonymizerService.cs`, `LocalLlmClient.cs`) and the
**JavaScript engine** (`extension/engine.js`, `extension/ollama.js`) implement
the *same* logic. When you change one, change the other and run both test suites.

## Build & test

```bash
# App + C# tests (needs the .NET 8 SDK)
dotnet build DataAnonymizer.sln -c Release
dotnet run --project tests/DataAnonymizer.Tests -c Release

# Extension engine tests (needs Node 18+)
node extension/engine.test.mjs

# Run the app locally
dotnet run --project src/DataAnonymizer      # then open http://localhost:5100
```

## Make your own version (fork guide)

1. **Fork** the repo on GitHub (button top-right) or just download it.
2. **Rebrand:** change the name/emoji in `extension/manifest.json`,
   `extension/i18n.js`, `src/DataAnonymizer/Localization.cs` and the icons in
   `extension/icons/`.
3. **Add your own detection rules:** add a regex + category in
   `AnonymizerService.cs` *and* `extension/engine.js`, add a test in both suites.
4. **Change the default AI model** in `src/DataAnonymizer/appsettings.json`
   (`LocalLlm.Model`) and `extension/ollama.js` (`DEFAULT_MODEL`).
5. **Release your build:** push a tag `vX.Y.Z` (or run the *Release* workflow
   from the Actions tab) and GitHub builds the Windows/Mac/Linux packages and
   the extension ZIP for you.

## Pull requests

Keep PRs focused, run both test suites, and describe the change. By contributing
you agree your contribution is licensed under the MIT license of this project.

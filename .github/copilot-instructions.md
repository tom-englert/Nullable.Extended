# Nullable.Extended – instructions for AI assistants

Tools to deal with *null-forgiving operators* (`!`) in C# code: find them, tell whether they are still required, and make sure they are justified.
Owner: tom-englert. Repository: `tom-englert/Nullable.Extended`, branch `master`.

## Products (all built from `src\Nullable.Extended.sln`)

| Project | Target | What it is |
| --- | --- | --- |
| `src\Nullable.Extended.Analyzer\Nullable.Extended.Analyzer` | netstandard2.0 | Roslyn analyzer + code fix, NuGet `Nullable.Extended.Analyzer`. Rules `NX0001`–`NX0004`: a null-forgiving operator has no justification comment. Code fix inserts a `// ! TODO:` placeholder. Signed in Release (`Key.snk`). |
| `src\Nullable.Extended.Extension\Nullable.Extended.Extension` | net48, WPF | Visual Studio extension (VSIX): a tool window that lists the operators of the solution and evaluates which are still required. |
| `src\Nullable.Extended.Mcp` | net10.0 | MCP server (stdio) packed as a .NET tool, NuGet `Nullable.Extended.Mcp`, command `nullable-extended-mcp`, also runs via `dnx`. For AI assistants working on large solutions (50+ projects). |
| `skills\nullable-extended\SKILL.md` | – | Agent skill that teaches the workflow (fix the code first, justify only as the last resort). Installed separately from the MCP server; the two are not connected. |

Libraries (not packaged on their own, `IsPackable=false`):
- `src\Nullable.Extended` (netstandard2.0): the analyzer framework used by the VS extension and the MCP server.
- `src\Nullable.Shared` (shared project, `.projitems`): code compiled into the analyzer **and** the framework, mainly `Justification` (comment prefix `// ! `, placeholder `TODO:`, how a justification is located above a node).

Other: `src\Test` (xunit v3 tests for the framework and the MCP server), `src\Nullable.Extended.Analyzer\Nullable.Extended.Analyzer.Test` (analyzer + code fix tests), `src\Sample` (small project used for manual and smoke tests), `src\IntegrationTest` (screenshots), `src\AI\*.md` (the concept document of the MCP server: milestones, decisions, open questions – keep it current when behavior changes).

## Core concepts

- **Justification:** a block of `// ! ...` comment lines directly above the statement/member containing the operator. The `TODO:` placeholder counts as a justification for the analyzer but must be replaced by a real one; the MCP `annotate` tool rejects placeholders.
- **Required:** an operator is *required* if removing it causes a new nullable warning (CS8600–CS8900). In multi-targeted projects, the operator is required if it is required in **at least one** target framework; it may only be removed if it is not required in all of them (`AnalysisResult` composes the per-framework `DocumentAnalysisResult` variants; always use `AnalysisSession.Results`, not the raw document results).
- **Two tiers in the MCP server:**
  - Tier 1 (scan, `list_null_forgiving`, `get_summary`, `get_code_context`, `annotate`): pure syntax, reads the files directly, no restore/build/MSBuild; fast.
  - Tier 2 (`check_required`, `reload`): loads projects with `MSBuildWorkspace` (out-of-process BuildHost of the installed .NET SDK; no MSBuildLocator, no `Microsoft.Build.*` references) and uses the analyzer framework's semantic post-processing. Slow, so it is limited to a few files / one project per call (`--max-check-files`, default 25), runs at most 4 projects at a time (LRU), results are valid per project generation, and the call returns `pending` after `waitSeconds` while the work continues in the background.
- **On demand only:** nothing is scanned until a tool is called. Only `annotate` writes source files (justification comments only).
- **Workspace root:** `--root` argument, else the client's MCP roots (`file://` URIs are converted to local paths; evaluated per call), else the working directory. `--solution` optionally restricts to the projects of a `.sln`/`.slnx`.

## MCP server layout (`src\Nullable.Extended.Mcp`)

`Program.cs` (host, DI, composition of the framework exports via `TomsToolbox.Composition`) · `ServerOptions` (CLI) · `WorkspaceProvider` (root resolution, logging) · `SourceLocator` (projects/files in scope) · `NullForgivingScanner` (Tier 1 scan, file-content cache) · `CodeContextReader` · `Annotator` · `RequiredChecker` (Tier 2) · `*Tools.cs` (MCP tool surface; the `[Description]` texts are the API documentation for the model) · `*Models.cs`/`Models.cs` (result DTOs) · `ServerInstructions` (short workflow text sent to the client) · `.mcp\server.json` (template; `$version$` is replaced at pack time; must match the package version).

Rules of thumb:
- stdout belongs to the protocol; log to stderr only. Log the root and parameters of every call.
- Tool results must stay small and paged (`limit`/`offset`); never return a whole solution.
- Errors the model can act on are thrown as `McpException` with a helpful message.
- Keep tool descriptions, `ServerInstructions.cs`, `skills\nullable-extended\SKILL.md`, `Documentation\McpServer.md` and `src\Nullable.Extended.Mcp\README.md` consistent when changing the tool surface.

## Build, test, package

```
cd src
dotnet build Test\Test.csproj                      # Debug; framework + MCP tests
dotnet test --project Test\Test.csproj --no-build  # xunit v3 on Microsoft.Testing.Platform (see global.json); filter e.g. --filter-class
dotnet test --project Nullable.Extended.Analyzer\Nullable.Extended.Analyzer.Test\Nullable.Extended.Analyzer.Test.csproj
dotnet build Nullable.Extended.Mcp\Nullable.Extended.Mcp.csproj -c Release   # also produces bin\Release\*.nupkg (GeneratePackageOnBuild)
```
- CI (`.github\workflows\build.yml`) builds the whole solution with **`msbuild`** (needed for the VSIX), runs `dotnet test --solution`, uploads the nupkgs and VSIX; tags `v<Version>` publish to nuget.org, the VS Marketplace and the Open VSIX Gallery. The tag must equal `<Version>` in `src\Directory.Build.props`, `NOTICE.txt` must be current (`UpdateNotice.cmd`), and `RelaseNotes.md` must have a section for the version.
- Tests of `check_required` create real temporary SDK-style projects on disk and load them with MSBuild (2–5 s each); keep such tests few and targeted.
- Package checks worth doing after packaging changes: all managed binaries must have the `Reproducible` debug-directory entry (NuGet Package Explorer shows "Deterministic"), and the PDBs must contain Source Link. The `BuildHost-net472` folder is deliberately removed from the MCP package (non-reproducible third-party binary; consequence: `check_required` cannot load old-style non-SDK projects).

## Conventions and constraints

- **Roslyn versions:** the production projects (framework, analyzer, VS extension) are pinned to Roslyn **4.5.0** (VS 17.5 / SDK 7.0.2xx compatibility); the MCP server and the tests use `$(TestRoslynVersion)` (5.9.0) via `VersionOverride`, because they parse the latest C#. Do not use APIs newer than 4.5 in the framework, the analyzer or the shared project.
- Central package management (`src\Directory.Packages.props`); add versions there, not in the csproj. Pinned packages carry a `Justification`.
- `src\Directory.Build.props`: shared `Version`, `LangVersion latest`, nullable enabled, implicit usings. Don't add `Microsoft.SourceLink.GitHub` as `GlobalPackageReference` – it disables the SDK's built-in Source Link and no source link file is generated.
- The framework (`Nullable.Extended`) is netstandard2.0 with Fody weavers (`PropertyChanged`, `ConfigureAwait`); `AnalysisResult` is the only type with change notification.
- The project's own code should avoid unjustified `!`; where one is needed, use a `// ! reason` comment.
- Comment only what needs clarification; no planning/notes markdown files in the repo. Docs live in `Documentation\`, the concept in `src\AI\`.
- Preserve each file's existing line endings and encoding. Edit files in place; don't normalize.

## Release notes and documentation

- `RelaseNotes.md` (sic): newest section first. The `[Next]` section is **always the first section** of the file, it collects the unreleased changes; below it come the version sections (`1.18.2`), newest first. Lines start with `New:`, `Fix:` etc. Leave out purely internal/technical improvements. Turning the content of `[Next]` into a version section is part of preparing a release (see below).
- User docs: `README.md` (root), `Documentation\McpServer.md`, package READMEs (`src\Nullable.Extended.Mcp\README.md`, `src\Nullable.Extended.Analyzer\README.md`, `src\Nullable.Extended.Extension\README.md`).
- Version bumps, commits, tags and GitHub settings are the maintainer's decision; don't do them unasked.

## Preparing a release

Only when the maintainer asks for it. All packages and the VSIX share one version. The assistant prepares the files; the maintainer does the manual steps.

**Prepare (can be done by the assistant):**
1. Set `<Version>` in `src\Directory.Build.props` to the new version (e.g. `1.19.0`). The MCP `server.json` and the package versions are derived from it; nothing else needs a version edit. Don't leave stale pinned versions in the docs (the docs use unpinned `dnx` configs, with the version only as an example).
2. In `RelaseNotes.md` move the entries of `[Next]` into a new section for the version: a line containing only the version (`1.19.0`), directly below `[Next]` and above the previous version section. Keep the `[Next]` heading on top of the file, now empty, ready for the next changes. Keep the entries user-facing (see above) and make sure the new section isn't empty; the release job fails otherwise.
3. Run `UpdateNotice.cmd` (updates the global tool `TomsToolbox.LicenseGenerator` and regenerates `NOTICE.TXT` from the solution's packages). The release job regenerates the file and fails if it differs from the committed one, so always run it after dependency changes and commit the result.
4. Verify: build and run all tests (see *Build, test, package*), and optionally inspect the Release packages (reproducible, Source Link, expected content).

**Manual only (the maintainer; don't do these):**
1. Commit and push the changes (version, release notes, `NOTICE.TXT`).
2. Create and push the release tag: the version prefixed with `v`, e.g. `v1.19.0`. The tag triggers the publish workflow, which checks that the tag matches `<Version>`, extracts the release notes of that version, verifies `NOTICE.TXT`, then publishes the NuGet packages (nuget.org), the VSIX (VS Marketplace, Open VSIX Gallery) and the GitHub release.

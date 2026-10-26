# AI Integration for Nullable.Extended: MCP, Skill or Agent

Oct 18, 2026 · @Tom · updated Oct 25, 2026 (framework extraction done, concept aligned with the code and the requirements below)

Build an **MCP server** as the core, and add a **Skill** on top. You don't need a custom Agent.

## Requirements and what they mean for the design

- **Primary use case:** find unjustified null-forgiving operators and fix them. The AI should **fix the code** (better annotations and null handling) wherever possible; a justification comment is only the **last resort** when no better fix is available.
- **Target size:** solutions with more than 50 projects.
- **Secondary, "very nice" feature:** the *required* check. It runs **on demand only**, never on the whole solution by default, but on a specific set of documents (or one project).

Consequences:

1. **Detection and justification are purely syntactic** (`NullForgivingDetectionAnalyzer` only uses the syntax root, `Justification` only looks at trivia). So the primary use case needs **no MSBuildWorkspace, no SDK, no restore and no compilation**: scan the `.cs` files, parse them, report. That is fast and independent of the size of the solution. This is **Tier 1**.
2. **The required check is Tier 2**: it needs a real compilation of the owning project, so it loads only the project(s) of the requested files, only when asked.
3. **Scale needs summaries first, paging, and batch edits.** At hundreds or thousands of occurrences, an AI that edits line by line with its own tools is slow, expensive and error-prone (line numbers shift with every inserted comment). See the `annotate` tool.
4. A plain `dotnet build` with the analyzer package is the alternative for detection, but on 50+ projects it needs a restore and a full build, can list the same file several times for multi-targeted projects, and typically does not repeat the warnings of up-to-date projects unless the build is forced. A syntax scan avoids all of that.

## Why MCP is the right foundation

An MCP server exposes the project's capabilities as tools that any AI client (Claude Desktop, Claude Code, VS Code, GitHub Copilot and others) can call. The logic that finds null-forgiving operators and decides whether they are still required used to be locked inside a Visual Studio extension (net48, WPF, `VisualStudioWorkspace`). It now lives in a reusable library, so the MCP server is a thin host around it.

## Current state

| Area | State |
| --- | --- |
| Core library `Nullable.Extended` (`src\Nullable.Extended`, netstandard2.0, `IsPackable=false`) | **Done.** Contains the analyzer framework and the null-forgiving analyzer. Referenced by the VS extension and by `Test`. No Visual Studio dependency. |
| Visual Studio extension | Done. Only `AnalyzerViewModel` is VS bound (`VisualStudioWorkspace`); it drives the core through `IAnalyzerEngine` / `AnalysisSession`. |
| Detection of `!`, its context and justification | Done. `Nullable.Shared` (shared project, source-included in the analyzer and the core). |
| Tests | `src\Test` (xunit v3, Microsoft.Testing.Platform, net10.0-windows) covers the session, the post-processor, requirement detection and justification. `UnitTest1` is the MSBuildWorkspace prototype. |
| MCP server project | **M1 done** (`src\Nullable.Extended.Mcp`: `get_summary`, `list_null_forgiving`, `get_code_context`, scopes `project`/`path`/`changedSince`, workspace folder from the client's MCP roots, optional `--root`/`--solution` overrides; tests in `src\Test\McpScannerTests.cs`). Open: `annotate` (M2), `check_required`, `required` fields and filters, `reload` (M3). User documentation: `Documentation\McpServer.md`. |
| Skill | **M1 done** (`skills\nullable-extended\SKILL.md` in the repository root, without `annotate` and `check_required` yet). Short server `instructions` give clients without skill support the same workflow. |

### What the core provides (this is what the MCP server builds on)

- `IAnalyzerEngine.CreateSession(documents, cancellationToken)` returns an `AnalysisSession`. Nothing runs until `StartAsync()`.
  - `await StartAsync()`: all syntax-level results are available (`NullForgivingAnalysisResult`: `Line`, `Column`, `Context`, `Justification`, `IsJustified`, `FilePath`, `ProjectName`), `IsRequired` is still `null`.
  - The post-processing (`IsRequired`) then runs **in the background**, one task per document, limited to `Environment.ProcessorCount` concurrent documents. `session.Completion` completes when all of it is done; `DocumentContext.Completion` does the same per document.
  - `session.UpdateAsync(document, ct)` re-analyzes a single changed document against a newer solution snapshot, cancels the outdated context, and handles concurrent updates of the same document (latest wins; superseded calls return `null`).
  - `DocumentContext.Cancel()` cancels the post-processing of one document.
- `solution.GetDocumentsToAnalyze()` (public extension) selects the C# documents and skips generated files (`.g.cs`, `.Designer.cs`, `<auto-generated>` header, generated `AssemblyInfo`).
- `NullForgivingAnalysisResult.IsRequired` is a tri-state:
  - `true`: removing the `!` introduces a new CS8600–CS8900 diagnostic.
  - `false`: removing it does not, so it is a removal candidate.
  - `null`: not (yet) determined. Either still pending, or `HasCompilationErrors` is `true`.
- `HasCompilationErrors` is set when the document itself has compile errors (errors in other documents don't matter), when the project cannot be compiled, or when a post-processor throws. Existing nullable diagnostics, even when reported as errors, do **not** count as compilation errors.
- Every operator is judged individually with all other operators left in place. Removing several "not required" operators in one scope can therefore make others required.
- Required check per operator: replace the operator by a space in the syntax tree, swap the tree into the cached compilation, and compute the semantic diagnostics only for the enclosing method/accessor/operator/type (`GetAnalysisScope`). The project baseline (`ProjectContext`) is computed once per project snapshot and shared. If the project references any `DiagnosticSuppressor`, the whole compilation is analyzed with analyzers for every operator, which is much slower.
- Context values: `General` (NX0001), `NullOrDefault` (NX0002), `Lambda` (NX0003), `Init` (NX0004).
- Justification rules (`Nullable.Shared\Justification.cs`): a block of single line comments starting with `// ! ` directly above the closest enclosing statement or declaration (for variable declarations the parent declaration). Multiple lines are joined with `\r\n`. A comment without text, a comment in the middle of an expression, or behind the operator is not a justification.

### Constraints that follow from the current structure

- The core is compiled against the **old pinned Roslyn 4.5.0** (VS 17.5 compatibility) and must stay netstandard2.0; any change to the core must keep the VS extension working. The tests use `$(TestRoslynVersion)` (5.9.0) at runtime, which already works in `Test`; the MCP server would do the same.
`InternalsVisibleTo` is granted to `Test` and `Nullable.Extended.Mcp` (the server uses the internal `Nullable.Shared` helpers of the core instead of compiling them a second time).
- `AnalysisResult.RelativeFilePath` keeps the leading directory separator and is relative to the solution folder; the MCP server should compute its own normalized path (forward slashes, no leading separator, relative to the root).

## Suggested architecture

1. **Core library: done.** Keep `Nullable.Extended` as the single shared library. The small additions the MCP server needs are listed under "Core changes needed".
2. **Create `src\Nullable.Extended.Mcp`** (net10.0 console app, cross-platform, no `-windows` suffix) using the official `ModelContextProtocol` C# SDK with the generic host and the **stdio** transport. Reference the core by project reference.
   - Central package management is on: add `ModelContextProtocol`, `Microsoft.Extensions.Hosting` and, if needed, `Microsoft.Build.Locator` to `src\Directory.Packages.props`, and use `VersionOverride="$(TestRoslynVersion)"` for the Roslyn packages like `Test.csproj` does (the property name is test-specific today).
   - **Tier 1 (default) uses no MSBuildWorkspace.** Files are enumerated from the root folder (and optionally restricted to the projects of a `.sln`/`.slnx`), `bin`/`obj` and generated files are skipped, and the documents are put into an `AdhocWorkspace` without references. The project of a file is the nearest `.csproj` upward, which is exact for SDK-style projects with default globbing.
   - **Tier 2 (on demand) uses `MSBuildWorkspace`** (`Microsoft.CodeAnalysis.Workspaces.MSBuild`) and opens **only the owning project** (`OpenProjectAsync`), not the solution. `Test\UnitTest1.cs` is the prototype (it sets `LoadMetadataForReferencedProjects`). Add a `WorkspaceFailed` handler that collects problems, and no `Directory.SetCurrentDirectory` hack.
   - MSBuild packages are compile-time references only (`ExcludeAssets="runtime"`); at run time the assemblies of the located MSBuild are used. `MSBuildLocator.RegisterDefaults()` is only needed for Tier 2 and must run before any MSBuild-dependent Roslyn type is touched, so register it lazily in a separate non-inlined method. Verify in M3 whether Roslyn's out-of-process build host still needs it (decision 9).
   - **stdout belongs to the protocol.** All logging goes to stderr.
   - Tier 1 needs nothing but the sources. Tier 2 requires an installed .NET SDK, a restored project, and (depending on decision 6) built outputs of the referenced projects; otherwise its documents report compile errors and end up with `HasCompilationErrors`.
3. **Package it as a dotnet tool** (`PackAsTool`, `ToolCommandName`) with MCP package metadata (`PackageType` `McpServer`, `.mcp\server.json`), so it can be run with `dnx` or installed with `dotnet tool install -g` and registered in a client with one line. The core is not packable, which is fine: the project reference is copied into the tool package.
4. **Add tests to `src\Test`** using `src\Sample`: it contains `!` in lambda context and is built with `AnalyzeSample=true`, so the nullable warnings are not silenced by `NoWarn`.

### Server design

**Tier 1 – syntax scan (default, no compilation)**

- Create the session **without post-processing** (core change 1): `AnalysisSession` then does a purely syntactic `StartAsync`, and `IsRequired` stays `null` (reported as `notChecked`).
- **No staleness problem:** cache the results per file by path + last write time + length, and re-parse changed files on every call. After the AI has edited a file, the next call simply sees it; no refresh tool is needed.
- **Scope** is always a filter (project, path/glob), never a mode. The unfiltered call returns the summary only.
- **IDs** are `file:line:column`. They change after edits, so re-list after every change.
- **Output size:** default `limit` (e.g. 100) with `total` and `nextOffset`. `get_summary` (grouped by project, then by folder) is the entry point, so the AI plans its work per project or folder instead of paging through thousands of results.
- **Known limits of a syntax scan:** code in inactive `#if` regions is not seen (like the analyzer in a build, which only sees the active region per target framework); `Compile Remove`/linked files and files of shared projects (`.shproj`) are attributed to the nearest `.csproj` (or to no project). These are acceptable for a justification audit, but must be documented.

**Tier 2 – required check (on demand, scoped)**

- The core already supports a precisely scoped run: `CreateSession(documents)` only analyzes the documents it is given, and `UpdateAsync(document)` adds a single document to a running session, which is exactly how the VS extension handles changed documents.
- Keep **one long-lived, initially empty `AnalysisSession` per project** (for the owning project of the files). `check_required(files)` loads the project if needed, takes the documents of the requested files, and calls `UpdateAsync` for them. Only the requested documents are post-processed. The expensive per-project baseline (`ProjectContext`) is computed once per project snapshot and shared by all later requests for that project.
- The session token is tied to the server lifetime, **not** to the MCP request token: a cancelled or timed-out request must not abort the shared project calculation.
- **Never block a tool call on the background work.** Wait for the documents' `Completion` only up to a timeout (`waitSeconds`, default about 20 s, below typical client timeouts) and return what is there. Pending results are reported as `pending`; asking again returns the finished values.
- **Explicit result state** instead of a bare nullable: `required` is one of `notChecked`, `pending`, `yes`, `no`, `unknown` (`HasCompilationErrors`, with a reason).
- **Guardrail:** `check_required` needs an explicit scope (files or one project) and refuses more than a configurable number of documents (e.g. 25) per call.
- After files changed on disk, the project snapshot is outdated: create the new snapshot with `Solution.WithDocumentText` (kept in the server; **never** `TryApplyChanges`, which would write to disk) and call `UpdateAsync` again. Only files that have changed since the last check are re-run; if a project file or a referenced project changed, discard the project's session.

### Tools to expose

| Tool | Tier | Purpose |
| --- | --- | --- |
| `get_summary(path?, project?, changedSince?)` | 1 | Counts by project/folder, context (`NX0001`–`NX0004`) and status (`unjustified`, `placeholder`, `justified`), plus the number of files scanned and skipped. If a required check has been run, also the `required` counts. `changedSince` (a git ref) restricts the scope to files changed relative to that ref, including uncommitted and untracked files. |
| `list_null_forgiving(project?, path?, changedSince?, context?, status?, required?, limit?, offset?)` | 1 (+2 results if present) | The occurrences. Each has `id`, `project`, `file` (relative to the root, forward slashes), `line`, `column` (1-based), `context`, `status`, `justification`, `required` and the source line. |
| `get_code_context(file, line, radius?)` | 1 | Surrounding source, so the AI can write a meaningful justification. Also returns the line and indentation where the comment has to be inserted (the start of the closest enclosing statement or declaration, i.e. what `Justification.FindSuppressionCommentTarget` computes), so the AI doesn't have to know the placement rules. Only files below the root can be read. |
| `annotate(items[])` | 1 (write) | Inserts justification comments, `items` = `{file, line, column, text}`. See below. |
| `check_required(files[] or project, waitSeconds?)` | 2 | Runs the required check for the given documents, returns their occurrences with the `required` state and, if available, the diagnostics a removal would cause. |
| `reload()` | 1+2 | Drops all caches and project sessions (e.g. after a package restore or project changes). |

**`annotate` rationale.** Annotating is the **last resort**, used only when the code can't reasonably be fixed (the skill makes fixing the code the default). For those cases in a large solution, the reliable way to add many comments is a deterministic batch insertion, not many line-based edits by the AI. The server only receives the *text*; it computes the placement itself (same target, indentation and line ending logic as the analyzer's code fix), processes each file bottom-up so the positions stay valid, and **validates** each item before writing: the operator must still be at the given position (and the line must match what the AI has seen), the text must be non-empty, must not be a placeholder (`TODO:`), and must be a plain single-line (or multi-line, one comment per line) text without `*/`, newlines in the middle of a line, etc. It returns the new status of every item. It is the **only** tool that writes, and it only ever inserts `// ! ` comment lines.

**What "fix" means.** The preferred outcome for an unjustified `!` is a **real fix in the code**: correct nullability annotations, better null checks, or a restructuring so the operator isn't needed (see the skill workflow). That is a semantic change, so the AI does it with the normal editing tools of its host and verifies it with a build, and, for removal candidates, with `check_required` (Tier 2). The server never changes code; its only write operation is inserting justification comments (`annotate`), the fallback for cases that can't be fixed.

### Core changes needed (keep netstandard2.0 and Roslyn 4.5 compatible)

1. **An overload to skip the required check** for Tier 1 (**done**). Prefer
2. **Path-based file selection** (**done**: `ExtensionMethods.ShouldBeAnalyzed(string? filePath, string projectName)`).
3. **Insert logic in the shared project.** The placement of the comment (target, indent, line ending) lives in the analyzer's code fix provider. Move it to `Nullable.Shared` so that the analyzer and `annotate` share one implementation and the same tests.
4. **Expose the justification placeholder** (**done**: `Justification.PlaceholderText` / `IsPlaceholder`, used by the code fix and the server).
5. **Merge results of multi-targeted projects** (**done**, it was a general bug that also affected the VS extension). MSBuildWorkspace (like VS) creates one `Project` per target framework, so the same file position is analyzed once per framework, and the results can differ, e.g. because the nullable annotations of the BCL differ (`ToString()` on .NET Framework vs. .NET). Before the fix, results of the same position from several target frameworks were not merged and showed up as duplicates, and the old engine (commit `445208d`) picked the *not required* result. *Rule:* an operator is **required if it is required in at least one target framework**, so it may only be removed (or reported as removable) if it is not required in **all** of them. *Implementation:* there are two result types. A `DocumentAnalysisResult` (`NullForgivingDocumentResult`) is what the analyzer finds in one document and what the post-processor evaluates; it has no change notification and lives in `DocumentContext.Results`. An `AnalysisResult` (`NullForgivingAnalysisResult`) is what consumers see: it is composed of the document results at the same position (file path and span, `Variants`) and evaluates them as a whole: `IsRequired` is `true` if any variant requires the operator, else `null` (unknown, e.g. compile errors in one framework) if any is unknown, else `false`; `HasCompilationErrors` is true if any variant has errors. It is the only type that implements `INotifyPropertyChanged`, and is notified when a variant is evaluated, so late post-processing results are reflected live. `AnalysisSession.Results` returns one composed result per position, and is what the VS extension and the server must use; results of unchanged positions are reused when the session is updated. Covered by `AnalysisResultVariantsTests`. Tier 1 doesn't have this problem, because every file is scanned once. Consequence for Tier 2: checking **only one** target framework per call is not safe for "not required" answers, see decision 6.
6. **Optional: scope the baseline.** `ProjectContext` computes the diagnostics of the **whole project compilation** once per project, but only the diagnostics of the requested documents are used (`IsNewDiagnosticInCurrentDocument`, `HasErrors(filePath)`). For very large projects a per-document baseline could be much cheaper. Only do this after measuring.
7. **Optional: expose why an operator is required.** `NullForgivingDetectionPostProcessor` already has the new diagnostics when it sets `IsRequired`; keeping a small copy (id, message, line) on the result is cheap and makes the AI's decision verifiable.

## Where a Skill and an Agent fit

- **Skill (second step, recommended):** a `SKILL.md` in `skills\nullable-extended\` (the `skills/<name>/SKILL.md` convention that `gh skill install` and `npx skills add` discover; folders like `.github/skills` are for skills that work on the repository itself) that teaches the AI the workflow and the conventions:
  1. `get_summary`, then work **project by project** (or folder by folder), so each step stays reviewable and small enough for the context window.
  2. `list_null_forgiving(project, status=unjustified|placeholder)`, look at the code with `get_code_context`, and treat each occurrence as a **code problem first**: **the default is to fix the code, not to annotate it.** A justification comment is the **last resort**, only for when there is no better fix.
  3. Fix strategies, in this order: (a) fix the nullability annotations at the source (declare the parameter, return value, field or property as nullable or non-nullable correctly, mark required members `required`, use attributes such as `[NotNull]`, `[NotNullWhen]`, `[MemberNotNull]`); (b) improve the null handling in the code (null check with early return or throw, `ArgumentNullException.ThrowIfNull`, pattern matching, `??`, `?.`, `is not null`) so the compiler can see that the value is not null; (c) restructure so that the situation can't occur (constructor initialization instead of later assignment, avoid `default`/`null` placeholders). Check with a build that the change is correct and that it doesn't introduce new warnings elsewhere (changing annotations of public API affects callers).
  4. Only when none of this is practical (e.g. a framework or serializer guarantees the value, an API without annotations, a test or lifecycle pattern the compiler can't follow), justify it: write a **specific** justification that says *why* the value can't be null or why the warning is a false positive; never generic text such as "needed" or "suppress warning", never `TODO:`. Submit the justifications in batches with `annotate`.
  5. Contexts that are usually hard to avoid: lambdas (`NX0003`) and `null!`/`default!` on init-only properties (`NX0004`). General usages (`NX0001`) can mostly be fixed in the code.
  6. Optionally, for operators the AI suspects to be obsolete, call `check_required` for that file set only; never remove an operator because it is `unknown`, `pending` or `notChecked`. Verify removals with a build.

  The skill is instructions; the MCP server is capability. Consider also publishing the workflow as an **MCP prompt** from the server, so clients without skill support get it too.
- **Agent or subagent:** not needed up front. It is only worthwhile later for an autonomous "clean up all null-forgiving operators in this repo" loop. That is a configuration of the pieces above, not new code.

## Caveats and risks

- **The Required check is slow**, which is why it is on demand and scoped. It needs one baseline compilation per project (of the whole project, see core change 6), plus per operator a semantic analysis of the enclosing member or type. Documents run in parallel (processor count). Projects with `DiagnosticSuppressor` analyzers are much slower, because the whole compilation is analyzed per operator. On a 50+ project solution never load or compile everything.
- **Quality of generated justifications, and annotating as an escape hatch.** An AI can silence thousands of warnings with plausible but meaningless comments, which defeats the purpose of the justification and hides code that should be fixed. The skill must make fixing the code the default and annotating the last resort, must demand specific reasons, `annotate` must reject placeholders, and the result should be reviewed in a diff, ideally project by project.
- **The `TODO:` placeholder** inserted by the analyzer's code fix is accepted as a justification by the analyzer. Reports must show it as `placeholder`, otherwise a cleanup based on warnings alone misses it.
- **Concurrent edits.** `annotate` writes files that the user or other tools may also change; it must verify the expected line content right before writing, and fail the item instead of guessing.
- **Staleness.** Tier 1 re-reads changed files on every call. Tier 2 results are only valid for the file content and project state of the check; they are dropped when the file changes, and `reload` resets everything.
- **Restore and SDK (Tier 2).** MSBuildWorkspace needs a restored project and a matching installed SDK. Load problems must be returned by `check_required`, not swallowed.
- **Concurrency.** Clients may call tools in parallel; serialize loads/reloads and make session creation per scope thread-safe.
- **Platform.** The path handling (`RelativeFilePath`, `Path` comparisons) has only been used on Windows so far.

## Open questions and decisions

1. **Two tiers: syntax-only by default, compilation only on demand.** This follows from the requirements and removes MSBuildWorkspace, restore and SDK from the primary path. *Decided:* yes. Implement milestone after milestone (decision 14).
2. **How does the MCP server get an engine?** Use the same service provider as the VS extension: `BindExports(typeof(IAnalyzerEngine).Assembly)` and `ExportProviderAdapter`, registered in the server's `Microsoft.Extensions.Hosting` container, which the MCP SDK uses anyway. *Decided in favor of this* after review: the alternatives (a public factory, or `InternalsVisibleTo` plus `new`) duplicate the wiring and drift when another analyzer or post-processor is exported. Costs are negligible: a few reflection calls at startup, packages the core and the host already reference, and none of the VS-specific assembly loading problems (those only exist in the VSIX). The remaining requirement is the Tier 1 switch (core change 1).
3. **May the server write files (`annotate`)?** Read-only is simpler and safer. But as the last-resort fallback, at this scale line-based comment edits by the AI are slow and fragile. One write tool that only inserts validated `// ! ` comment lines, nothing else; fixes, removals and refactorings stay with the host's editing tools. Because fixing the code is the preferred path, `annotate` belongs to the later milestone (M2) and doesn't affect the design of the read tools. *Decided:* yes, in milestone 2.
4. **What counts as "justified"?** The analyzer is not changed: it keeps accepting `// ! TODO:` (the code fix placeholder) as a justification. The server reports it as `placeholder`, and `annotate` rejects the placeholder and requires an individual text. *Decided.*
5. **Root and solution scope.** Scan below the folder the client reports as MCP root (resolved per tool call; override with `--root`, fallback: working directory), with an optional `--solution` filter that restricts the scan to the projects of a `.sln`/`.slnx` (`Microsoft.VisualStudio.SolutionPersistence` handles both formats). Repositories with several solutions pass the one to use. *Decided:* as recommended; roots added so no arguments are needed.
6. **How does Tier 2 load a project?** `OpenProjectAsync` with `LoadMetadataForReferencedProjects`: cheap, but references are the **built outputs**, so the solution must have been built. The server detects missing or outdated outputs of referenced projects (missing reference outputs, load diagnostics, compile errors caused by them) and returns a clear warning to build the solution first, instead of results that look like "unknown". Opening the full solution (references compiled from source) is not used: too slow and memory hungry on 50+ projects. All target frameworks are loaded and merged (core change 5), because an operator is required if it is required in any framework, and a "not required" answer is only valid if all frameworks were checked. A single selected framework is only an explicit, labeled option ("checked for <tfm> only"). *Decided:* as described, including the detect-and-warn behavior.
7. **Guardrail for the required check:** 25 documents per call by default (configurable), whole project only by explicit request, never the whole solution. *Decided:* as recommended.
8. **Baseline scope (core change 6).** Measure on a large project before touching the core. *Decided:* postponed until really needed.
9. **Roslyn version and `MSBuildLocator`.** The MSBuild packages are referenced for compile time only (interface descriptions, `ExcludeAssets="runtime"`/`PrivateAssets`), so no MSBuild assemblies are shipped; at run time the assemblies of the located MSBuild/SDK are used (`MSBuildLocator`, Tier 2 only). The Roslyn version of the server and how it is managed (the core is built against 4.5.0, the tests use `TestRoslynVersion`) still has to be verified in M3 together with the out-of-process build host. *Decided:* compile-time-only MSBuild references.
10. **Packaging.** Published like the other packages (NuGet.org, with MCP metadata for `dnx`/VS), with the same version as everything else (`Directory.Build.props`). *Decided:* package id and project name `Nullable.Extended.Mcp`.
The skill lives in `skills\nullable-extended` in the repository root, so it is discoverable by `gh skill` and `npx skills`, and is advertised in the "AI assistants" section of `README.md`. *Decided* for name, documentation and skill location.
12. **Cross-platform.** Tier 1 has no Windows dependency, so the server targets Windows, Linux and macOS (net10.0, no `-windows` suffix). This needs a review of the path handling (`RelativeFilePath`, `Path` comparisons, case sensitivity, the server's own normalized forward-slash paths) and tests that don't assume Windows paths. *Decided:* cross-platform.
13. **"Changed files only" scope** (e.g. via `git diff` against a base branch), to use the server in PR-review workflows. Since it is cheap with Tier 1, it is included in M1 as an additional scope filter (e.g. `changedSince` on `get_summary` and `list_null_forgiving`: files changed relative to a git ref, plus uncommitted and untracked files). It needs `git` on the path and must report a clear error if the root isn't a git repository. *Decided:* include it.
14. **Milestones.** **M1** Tier 1 (`get_summary`, `list_null_forgiving`, `get_code_context`, including the `changedSince` scope) plus the skill; **M2** `annotate` with the shared insert logic; **M3** Tier 2 (`check_required`, project loading, guardrails). Each milestone is useful on its own and is implemented and reviewed separately. *Decided:* milestone after milestone.

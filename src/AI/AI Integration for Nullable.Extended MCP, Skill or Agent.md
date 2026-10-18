# AI Integration for Nullable.Extended: MCP, Skill or Agent

Oct 18, 2026 · @Tom

Build an **MCP server** as the core, and optionally add a **Skill** on top. You probably don't need a custom Agent.

## Why MCP is the right foundation

An MCP server exposes your project's capabilities as tools that any AI client (Claude Desktop, Claude Code, VS Code and others) can call. Your project already has the logic, but it is locked inside a Visual Studio extension (net48, WPF, `VisualStudioWorkspace`), which an AI client can't reach. The MCP server makes that logic callable.

Your code is already well separated:

- `Nullable.Shared` (`Justification.cs`, `ExtensionMethods.cs`) detects the `!` operator, its context (general, null-or-default, lambda, init) and its justification comment. It has no Visual Studio dependency.
- `AnalyzerEngine` and `NullForgivingDetectionPostProcessor` compute `IsRequired` by rewriting the node without `!` and checking for new CS8600–CS8900 diagnostics. They depend only on Roslyn `Document` and `Project`, not on Visual Studio itself.
- Only `AnalyzerViewModel` is Visual Studio bound (`VisualStudioWorkspace`, `GetGlobalService`).

## Suggested architecture

1. **Extract a core library**, e.g. `Nullable.Extended.Core` (netstandard2.0 or net8+), containing `AnalysisResult`, `AnalysisContext`, `AnalyzerEngine`, the analyzer and the post-processor. Both the Visual Studio extension and the MCP server reference it.
2. **Create a console app `Nullable.Extended.Mcp`** (net8 or net10) using the official `ModelContextProtocol` C# SDK over stdio. Load the solution with `MSBuildWorkspace` and `Microsoft.Build.Locator`. Your `Test/UnitTest1.cs` already does exactly this, so you have a working prototype of the loading step.
3. **Package it as a dotnet tool** (`dotnet tool install -g ...`) so users can register it with one line in their MCP config.

## Tools to expose

| Tool | Purpose |
| --- | --- |
| `list_null_forgiving(filter?)` | All occurrences with file, line, column, context, justification, `isRequired` and `hasCompilationErrors`. Filter by project, file, context, justified or not, required or not. |
| `list_unjustified()` | Occurrences with no ` // !  ` comment (the NX000x set). |
| `list_not_required()` | Occurrences whose removal produces no new nullable warning, i.e. removal candidates. |
| `verify_occurrence(file, line, column)` | Re-runs the removal check for one occurrence and returns the new diagnostics it would cause. This makes the AI's verification trustworthy rather than guessed. |
| `get_summary()` | Counts by context, justified vs. unjustified, required vs. not. |
| `get_code_context(file, line, radius)` | Surrounding source so the AI can propose a meaningful justification text. |

Keep the MCP server **read-only at first**. Let the AI propose edits (justification comments, removing `!`) using the normal file-editing tools of its host (e.g. Claude Code), and use `verify_occurrence` to validate the change.

## Where a Skill and an Agent fit

- **Skill (optional, recommended second step):** a `SKILL.md` that teaches the AI your workflow, such as "list unjustified occurrences, read context, write a `// ! <reason>` comment above the statement, then re-verify". It also encodes your conventions: the comment prefix ` // !  `, placement above the statement or declaration, and which contexts are usually unavoidable. The skill is instructions; the MCP server is capability.
- **Agent or subagent:** not needed up front. It is only worthwhile later if you want an autonomous "clean up all null-forgiving operators in this repo" loop. That is a configuration of the pieces above, not new code.

## Caveat: the Required check is slow

The `IsRequired` analysis recompiles the project once per occurrence, which is slow on large solutions. For an MCP tool, cache results, analyze per project lazily, and return `isRequired: null` until a deeper check is requested, so tool calls stay responsive.

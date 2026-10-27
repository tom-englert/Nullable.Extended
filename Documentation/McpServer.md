# Nullable Extended MCP server

`Nullable.Extended.Mcp` is an [MCP](https://modelcontextprotocol.io) server that lets an AI assistant
(GitHub Copilot, Claude, and any other MCP client) find the [null-forgiving operators](../README.md#managing-null-forgiving-operators)
in a C# code base, and see which of them are still unjustified, so it can fix them.

It is made for large solutions (50+ projects): it reads the source files directly and needs no restore, build or SDK,
and it only looks at the part of the code you ask for.

## Installation

The server is a .NET tool (.NET 10 or later, runs on Windows, Linux and macOS):

```
dotnet tool install -g Nullable.Extended.Mcp
```

Register it in your MCP client, e.g. in `.vscode/mcp.json` or `.mcp.json`:

```json
{
  "servers": {
	"nullable-extended": {
	  "type": "stdio",
	  "command": "nullable-extended-mcp"
	}
  }
}
```

No arguments are needed: the server asks the client for the folder that is open (MCP roots) whenever a tool is called, so it works on whatever workspace the session has open. If the client does not provide roots, the working directory of the server is used.

Optional arguments:

| Argument | Meaning |
| --- | --- |
| `--root <folder>` | Overrides the folder that is scanned; by default the workspace folder of the client, else the working directory. Only files below this folder are read. |
| `--solution <file>` | Optional `.sln` or `.slnx`; restricts the scan to the projects of this solution, e.g. to ignore other projects in a mono repo. |
| `--max-check-files <n>` | The maximum number of files a single `check_required` call may cover; default 25. |

## Tools

All tools run on demand; nothing is scanned until a tool is called. Only `annotate` writes source files, and it only inserts or replaces justification comments. `check_required` loads projects with MSBuild, which can write generated files into their `obj` folders.

### Scope

`get_summary` and `list_null_forgiving` accept a scope; only the files in scope are scanned.

| Parameter | Meaning |
| --- | --- |
| `project` | A project name, or the path of the project file relative to the root. |
| `path` | A folder or file relative to the root, or a glob pattern like `src/**/Services/*.cs`. |
| `changedSince` | A git ref, e.g. `main`. Files changed relative to the ref, including uncommitted and untracked files. Requires `git`. |

The parameters are combined with *and*. A file belongs to the project of the nearest `.csproj` above it.

### `get_summary`

Counts of the operators by project and folder, by status and by context. Start here to plan the work.

- **Status:** `unjustified` (no justification comment), `placeholder` (only the `// ! TODO:` scaffold that the analyzer's code fix inserts), `justified`.
- **Context:** `NX0001` general, `NX0002` null/default, `NX0003` lambda, `NX0004` init-only property (see [Diagnostics](../README.md#diagnostics)).
- **Required:** if operators in scope have been checked with `check_required`, the counts of the states (see below).

### `list_null_forgiving`

The occurrences with `id`, `project`, `file` (relative to the root, forward slashes), `line`, `column` (1-based), context, status,
justification and the source line. Additional filters are `context`, `status` and `required`.
The `required` field shows the result of a previous `check_required` (`yes`, `no`, `unknown`, `pending`), or `notChecked`; it never triggers a check.
The result is paged (`limit`, default 100, maximum 500; use `nextOffset` for the next page).
The `id` is `file:line:column` and changes when the file is edited, so list again after changing code.

### `get_code_context`

The source lines around a position and, for each operator in them, its status and justification, and where a justification comment
has to be inserted (line, indentation, and the line ending of the file).

### `annotate`

Writes justification comments for null-forgiving operators; the last resort, when the code can't reasonably be fixed.
The parameter is a list of `items` (at most 500 per call), each with `file`, `line` and `column` (as returned by `list_null_forgiving`),
the justification `text`, and optionally the `sourceLine` the AI has seen.

- The comment `// ! <text>` is placed above the statement or declaration, with the same indentation and line ending logic as the analyzer's code fix;
  a multi-line text becomes one comment line per line. A `// ! TODO:` placeholder is replaced.
- Several operators in the same statement share one comment block.
- Every item is validated before anything is written, and rejected items are reported with the reason, without changing the file:
  the operator must still be at the position (and the line must match `sourceLine`), it must not be justified already,
  the text must be specific (not empty, not the `TODO:` placeholder, no control characters, no leading `//`),
  and the file must be a non-generated `.cs` file of the scope (below the root, in the solution if `--solution` is given, not in `bin`/`obj`).
- A file is changed all-or-nothing: the result is parsed again and checked, and the file is not written if it changed meanwhile.
  The BOM and the line endings are kept. Files that are not UTF-8 are rejected.
- The result lists the outcome (`inserted`, `replaced`, `rejected`) and the new status of every item. Positions in the edited files have changed, so list again afterwards.

### `check_required`

Tells whether the operators are still *required*, i.e. whether removing the operator causes a new nullable warning (CS8600 to CS8900).
This is the only tool that compiles: the projects are loaded with MSBuild (in a separate build host process, using the installed .NET SDK),
and every file needs a semantic analysis. It is much slower than the other tools, so use it deliberately.

- Specify either `files` (relative to the root, or absolute), or a `project` (name or project file) with an optional `path` to restrict it to a folder.
  A call is limited to `--max-check-files` files (default 25); narrow the scope or check in several calls.
- The result per operator is `yes` (required), `no` (not required, a candidate for removal), `unknown` (e.g. the file has compile errors; the `reason` tells which) or `pending`.
  For multi-targeted projects an operator is `yes` if it is required in at least one target framework.
- The call waits at most `waitSeconds` (default 20, maximum 60). Loading and compiling can take longer, then the remaining operators are `pending` and `complete` is `false`;
  call again with the same arguments, the work continues in the background.
- The results stay valid while the source files of the project don't change; any change of a file in the project discards the results of the whole project, as they depend on the whole compilation.
  If the project file or its references change, e.g. after a build, the project is loaded again automatically. New files are not detected; call `reload`.
- Projects that reference other projects are loaded together with them, so they can be checked without a build. Packages must have been restored; a build is recommended (`buildRecommended`) if references are missing.
- A `no` is a strong hint, not a proof; verify a removal with a build of all target frameworks.

### `reload`

Discards everything that is cached: the scan results and the loaded projects. Use it after files were added or removed, or after switching branches.

## How it works and what to keep in mind

- The scan is **syntactic**: it finds every `!` operator in the active code of a file, without building the project.
  It does **not** tell whether an operator is still *required*; use `check_required` for a small scope to find out.
  Code in inactive `#if` regions is not seen; generated files (`.g.cs`, `.Designer.cs`, `<auto-generated>`, `AssemblyInfo`) and `bin`/`obj` folders are skipped.
- Results are cached per file (path, size, modification time) and files are re-read when they change, so there is no stale data and no refresh needed.
- Linked files and files of shared projects are attributed to the nearest `.csproj` above the file, or to *(no project)*.
- An operator that is not needed in one target framework may be required in another one, e.g. because the nullable annotations of the
  .NET Framework and .NET base libraries differ. Don't remove operators without building all target frameworks.

## Skill

The [skill](../skills/nullable-extended/SKILL.md) teaches an AI assistant the workflow: plan with the summary, work project by project,
**fix the code first** (better annotations, better null handling) and use a justification comment only as the last resort.

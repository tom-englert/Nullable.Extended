# Nullable.Extended.Mcp

An [MCP](https://modelcontextprotocol.io) server that finds the null-forgiving operators (`!`) in a C# code base, and shows which of them have no justification yet.

It scans the sources syntactically, so it needs no restore, build or SDK, and is fast even for solutions with many projects.

See the [documentation](https://github.com/tom-englert/Nullable.Extended/tree/master/Documentation) for details.

## Usage

```
nullable-extended-mcp [--root <folder>] [--solution <file>]
```

Both arguments are optional. Normally the server asks the client for the open workspace folder (MCP roots), and falls back to its working directory.

- `--root`: overrides the folder to scan.
- `--solution`: restricts the scan to the projects of a `.sln` or `.slnx` file.

## Tools

- `get_summary`: counts by project, folder, status and context.
- `list_null_forgiving`: the occurrences, paged and filtered.
- `get_code_context`: the source around an occurrence, and where a justification comment belongs.
- `annotate`: writes justification comments in batches (the only tool that changes files), and replaces `// ! TODO:` placeholders.

All tools accept a scope (`project`, `path`, `changedSince`), so only a part of the solution is scanned.

## Skill

A skill that teaches the AI assistant the workflow (fix the code first, justify only as the last resort) is not part of this package;
install it from the repository, into your project or user profile:

```
gh skill install tom-englert/Nullable.Extended nullable-extended
npx skills add tom-englert/Nullable.Extended
```

Without the skill, the server sends a short version of the workflow to the client when it connects.

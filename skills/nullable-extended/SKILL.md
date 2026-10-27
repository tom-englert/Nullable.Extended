---
name: nullable-extended
description: Find and fix unjustified null-forgiving operators (the '!' suffix) in C# code with the Nullable.Extended MCP server. Use when asked to clean up, review, audit or justify null-forgiving operators, 'null!', 'default!' or '!.' usages, or nullable warnings that were silenced with '!'.
---

# Cleaning up null-forgiving operators

Uses the tools of the `nullable-extended` MCP server: `get_summary`, `list_null_forgiving`, `get_code_context`, `annotate`, `check_required`, `reload`.
The scan is syntactic; it needs no build and no restore. Only `annotate` writes files, and only justification comments.

**Fix the code. Annotating is the last resort.**
A justification comment hides the problem; a real fix removes it.
Never add generic comments just to make the list shorter.

## Workflow

1. **Plan.** Call `get_summary` (with `project` or `path` if the scope is already known, or `changedSince` for the files of a branch or PR).
   Work **project by project** (or folder by folder), so each step stays reviewable. Don't page through a whole solution.
2. **List.** `list_null_forgiving(project=..., status=unjustified)`. Also handle `status=placeholder`:
   these only have the `// ! TODO:` scaffold from the analyzer's code fix and are not justified yet.
   The `id` (`file:line:column`) is only valid until the file is edited: list again after every change.
3. **Understand.** `get_code_context(file, line)` shows the surrounding code. Read the declarations the value comes from, not only the line.
4. **Fix in this order:**
   1. *Annotations at the source:* declare the parameter, return value, field or property correctly nullable or non-nullable;
      use `required`, `[NotNull]`, `[NotNullWhen]`, `[MemberNotNull]`, `[MaybeNull]` and similar attributes.
   2. *Null handling in the code:* null check with early return or throw, `ArgumentNullException.ThrowIfNull`, pattern matching, `??`, `?.`, `is not null`,
      so the compiler can see the value is not null.
   3. *Restructure* so the situation can't occur: initialize in the constructor instead of later, avoid `null`/`default` placeholders.
   Be careful with public API: changing an annotation affects every caller.
5. **Justify, only if none of this is practical** (a framework or serializer guarantees the value, an API without annotations,
   a lifecycle the compiler can't follow). Write a **specific** reason why the value can't be null, or why the warning is a false positive.
   Not "needed", "suppress warning" or `TODO:`.
   Don't edit the comment into the file yourself, call `annotate(items=[{file, line, column, text, sourceLine}])` with the `file`, `line` and `column` from the list;
   pass the `sourceLine` of the list as well, so a stale position is rejected instead of annotating the wrong operator.
   `annotate` writes the `// ! ` comment above the statement or declaration, keeps the file's line endings and encoding, and replaces a `// ! TODO:` placeholder:
   ```csharp
   // ! The serializer always sets this property, the setter is only used for deserialization
   public string Name { get; set; } = null!;
   ```
   Annotate a batch per project or folder (at most 500 items per call), and review each outcome: rejected items are not written and tell the reason.
   Several operators in the same statement share one comment, so write a text that is true for all of them.
   An operator that already has a justification is not overwritten.
6. **Verify.**

## Checking whether an operator is still required

`check_required` compiles the project and tells if removing an operator causes a new nullable warning. It is slow and heavy: use it for a small scope
(a few files, or one project that is not huge), only when operators look obsolete, e.g. left over after a nullability annotation was fixed. Never run it on everything.

- `check_required(files=[...])` or `check_required(project=..., path=...)`; the call is limited to a few files. If `complete` is `false`, call again with the same arguments.
- `no`: the operator is probably not needed. Remove it, and verify with a build of all target frameworks, an operator may be needed in only one of them.
- `yes`: the operator is required; treat it like any other (fix the code, or justify it).
- `unknown` or `pending`, or `required=notChecked` in the list: never remove the operator on this basis.
- If the result says that references are missing, build first. After files were added or removed, call `reload`.
- The results are dropped when a file of the project changes, so check again after editing.

## Notes

- Hard to avoid contexts: lambdas (`NX0003`) and `null!`/`default!` for init-only members (`NX0004`).
  General usages (`NX0001`) can mostly be fixed in the code. `NX0002` is `null!`/`default!`.
- The scan is syntactic: it does not tell if an operator is still *required*; `check_required` does, for a small scope. Don't remove an operator unless `check_required` says `no` and the build proves it is not needed
  (for multi-targeted projects, build all target frameworks: an operator may be required in only one of them, e.g. because of different BCL annotations).
- Code in inactive `#if` regions and generated files (`.g.cs`, `.Designer.cs`, `<auto-generated>`) are not scanned.
- Review the result as a diff per project.

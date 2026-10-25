# Nullable.Extended.Analyzer

A Roslyn analyzer that warns about every usage of the [null-forgiving operator `!`](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving)
that is not justified by a comment, so everyone knows why it is needed. It works in any IDE and on CI.

```
dotnet add package Nullable.Extended.Analyzer
```

## Justification comments

A justification comment is one or more single line comments directly above the statement or declaration.
The first comment line must start with `// ! `, followed by the justification text:

```c#
// ! This is only needed to demo the feature
var text = value1!.ToString();
```

A code fix is offered that adds the scaffold for the comment.

## Diagnostics

| Id | Context |
|---|---|
| NX0001 | Any other usage of the null-forgiving operator. |
| NX0002 | Null-forgiving operator on the `null` or `default` literals at any other statement. |
| NX0003 | Null-forgiving operator inside lambda expressions. |
| NX0004 | Null-forgiving operator on the `null` or `default` literals at init only property assignment. |

The severity of each category can be configured in your `.editorconfig` file:

```ini
[*.cs]
dotnet_diagnostic.NX0001.severity = error
dotnet_diagnostic.NX0003.severity = suggestion
```

## More

See the [project page](https://github.com/tom-englert/Nullable.Extended) for the Visual Studio extension, which also detects
operators that are no longer needed, and for an MCP server that helps AI assistants to find and fix unjustified operators in large solutions.

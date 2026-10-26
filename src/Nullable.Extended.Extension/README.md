# Nullable Extended

A Visual Studio extension that lists all usages of the [null-forgiving operator `!`](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving) in your solution,
categorizes them, and detects those that are still present in code but no longer needed.

## Usage

Start the tool window from the tools menu:

![menu](https://raw.githubusercontent.com/tom-englert/Nullable.Extended/master/assets/NullForgivingMenu.png)

The tool window shows all occurrences of the null-forgiving operator in your solution and their justification comments.
It also shows if the null-forgiving operator is required to suppress nullable warnings.
If the "Required" column is not checked, the null-forgiving operator might be obsolete and can probably be removed.

![window](https://raw.githubusercontent.com/tom-englert/Nullable.Extended/master/assets/NullForgivingToolWindow.png)

- Click the refresh button to scan your solution.
- Double click any entry to navigate to the corresponding source code.

## Justification comments

A justification comment is one or more single line comments directly above the statement or declaration.
The first comment line must start with `// ! `, followed by the justification text:

```c#
// ! This is only needed to demo the feature
var text = value1!.ToString();
```

Occurrences are categorized like the analyzer does:

| Id | Context |
|---|---|
| NX0001 | Any other usage of the null-forgiving operator. |
| NX0002 | Null-forgiving operator on the `null` or `default` literals at any other statement. |
| NX0003 | Null-forgiving operator inside lambda expressions. |
| NX0004 | Null-forgiving operator on the `null` or `default` literals at init only property assignment. |

## More

See the [project page](https://github.com/tom-englert/Nullable.Extended) for the
[Roslyn analyzer](https://www.nuget.org/packages/Nullable.Extended.Analyzer/), which warns about unjustified usages in the IDE and in your builds,
and for an MCP server that helps AI assistants to find and fix unjustified operators in large solutions.

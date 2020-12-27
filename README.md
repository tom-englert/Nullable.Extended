# Nullable Extended Analyzer

### A Roslyn analyzer to improve the experience when working with nullable reference types.

[Nullable reference types](https://docs.microsoft.com/en-us/dotnet/csharp/nullable-references) 
are a great feature introduced with C# 8.0. It really helps in writing better code.

However when working with it a while you'll may notice some false positive `CS8602` warnings that may be not expected.
E.g. when working with [ReSharper's](https://www.jetbrains.com/resharper/) static analysis, it will enforce to use some 
patterns that are not covered by the built-ins flow analysis. (see e.g. issues [49653](https://github.com/dotnet/roslyn/issues/49653) 
or [48354](https://github.com/dotnet/roslyn/issues/48354)). While [ReSharper's](https://www.jetbrains.com/resharper/) analyzer based on 
it's own nullability annotations correctly handled those patterns, the C# Roslyn analyzer will raise a CS8602 warning:

```c#
public void Method(object? a) 
{
    var b = a?.ToString();

    if (b == null)
        return;

    // If b is not null, a can't be null here, but a CS8602 is shown:
    var c = a.ToString();
}
``` 

The **Nullable Extended Analyzer** addresses these issues by double checking `CS8602` warnings.
It leverages the flow analysis of the [Sonar Analyzer](https://github.com/SonarSource/sonar-dotnet) and suppresses the 
warning if flow analysis reports that access is safe.
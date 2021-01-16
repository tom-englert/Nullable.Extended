# Nullable Extended Analyzer 
[![Build Status](https://dev.azure.com/tom-englert/Open%20Source/_apis/build/status/Nullable.Extended.Analyzer?branchName=master)](https://dev.azure.com/tom-englert/Open%20Source/_build/latest?definitionId=39&branchName=master)
[![NuGet Status](https://img.shields.io/nuget/v/Nullable.Extended.Analyzer.svg)](https://www.nuget.org/packages/Nullable.Extended.Analyzer/)

## A Roslyn analyzer to improve the experience when working with nullable reference types.

[Nullable reference types](https://docs.microsoft.com/en-us/dotnet/csharp/nullable-references) 
are a great feature introduced with C# 8.0. It really helps in writing better code.

This analyzer helps to even more improve the experience with nullable reference types.

### Installation

Simply install the [NuGet Package](https://www.nuget.org/packages/Nullable.Extended.Analyzer/) in your projects.

### Usage

#### Suppressing False Positives

After using nullable reference types and the nullable analysis for a while you'll may notice some false positive `CS8602`, `CS8603` or `CS8604` warnings that may be not expected.

E.g. [IDE0031](https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0031) will enforce you to use null propagation, 
but this pattern is not fully covered by the flow analysis of the Roslyn nullable analyzer (see e.g. issues [49653](https://github.com/dotnet/roslyn/issues/49653) 
or [48354](https://github.com/dotnet/roslyn/issues/48354)), and you will see a `CS8602` warning:

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

The **Nullable Extended Analyzer** addresses these issues by double checking `CS8602`, `CS8603` and `CS8604` warnings.
It leverages the flow analysis of the [Sonar Analyzer](https://github.com/SonarSource/sonar-dotnet) and suppresses the 
warning if flow analysis reports that access is safe.

- This may not cover all cases, but the most ubiquitous.
- You may need at least MSBuild 16.8 to get suppressions work correctly.

#### Managing Null Forgiving Operators

Sometimes it is necessary to use the [null forgiving operator "!"](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving) to suppress a false warning.
However using too many of them will render the value of nullable checks almost useless.
Also since this analyzer will suppress many common false positives, some of them may be obsolete after installing the analyzer.

Since it's hard to find the null forgiving operators in code, this analyzer comes with a set of checks to locate all of them, so you can easily judge if they are still needed.

Occurrences are grouped into three diagnostics, to reflect their different contexts - e.g. general usages can be mostly avoided, while inside lambda expressions they are often unavoidable.
- NX0001 Find general usages of the NullForgiving operator.
- NX0002 Find usages of the NullForgiving operator on the `null` or `default` literals.
- NX0003 Find usages of the NullForgiving operator inside lambda expressions.

Simply turn the severity, which is `None` by default, to e.g. `Warning`, to list all usages of the null forgiving operator.

![image](assets/NX_0001.png)

#### Configuration

You can configure the analyzer by specifying a property named `<NullableExtendedAnalyzer>` in your project file:

```xml
<PropertyGroup>
  ...
  <Nullable>enable</Nullable>
  <WarningsAsErrors>nullable</WarningsAsErrors>
  <NullableExtendedAnalyzer>
    <LogFile>c:\temp\NullableExtendedAnalyzer.$(MSBuildProjectName).log</LogFile>
  </NullableExtendedAnalyzer>
</PropertyGroup>
```

##### Available configuration properties
- `LogFile`: The full path to a file where diagnostic messages will be written to.
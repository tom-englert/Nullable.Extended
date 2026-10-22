# Integration Test

A small, standalone project with deliberate nullable-forgiving operators (`!`) to manually check the current build of the
analyzer and of the Visual Studio extension. It is not part of `Nullable.Extended.sln`, and it is not run by CI;
the automated tests are in `src\Test` and `src\Nullable.Extended.Analyzer\Nullable.Extended.Analyzer.Test`.

## How it works

`IntegrationTest.sln` references the `Nullable.Extended.Analyzer` NuGet package. `NuGet.config` makes the package
sources resolve to:

1. `..\Nullable.Extended.Analyzer\Nullable.Extended.Analyzer.Package\bin\Debug`, the local build output of the analyzer package.
2. nuget.org, as a fallback.

The package version is the floating `1.*`, and the global packages folder is redirected to `.\packages` in this folder, so the
restore never touches the shared NuGet cache.

## Testing the analyzer

1. Build the analyzer package (`Nullable.Extended.Analyzer.Package`) in `src\Nullable.Extended.sln`.
2. Delete the `.\packages` folder next to this file. Otherwise a previously restored package is reused instead of the new build.
3. Open `IntegrationTest.sln` in Visual Studio and rebuild it. The warnings of the analyzer
   appear in the Error List, e.g. for the samples in `ScreenShots.cs`.

## Testing the Visual Studio extension

Start the `Nullable.Extended.Extension` project from `src\Nullable.Extended.sln` (it launches the experimental instance of Visual Studio),
open `IntegrationTest.sln` there and use the "Null Forgiving Operators" tool window to analyze the project.

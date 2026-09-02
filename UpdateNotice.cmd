dotnet tool update TomsToolbox.LicenseGenerator --global
build-license -i "%~dp0src\Nullable.Extended.sln" -o ".\NOTICE.TXT"

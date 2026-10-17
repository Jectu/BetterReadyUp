# Building Better Ready Up

Requires the .NET SDK and internet access for NuGet. 

Run from the folder containing `BetterReadyUp.csproj` and `BetterReadyUp.cs`:

```bash
dotnet restore
dotnet build -c Release --no-restore
```

The resulting plugin should be at `bin/Release/netstandard2.1/BetterReadyUp.dll`.

To use, copy `BetterReadyUp.dll`, `icon.png` and `translations.txt` into your plugin folder.

This was built using Kubuntu 26.04. Windows or other distros have not been tested for building but I see no reason they wouldn't work.

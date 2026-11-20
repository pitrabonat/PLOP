# DotNet harness

Plain `dotnet` projects that compile the engine-independent Unity code (Assets/Scripts/Core, Solver,
LevelGeneration) for fast iteration. Unity remains the source of truth; these projects only reference
the same `.cs` files. Language version is pinned to C# 9 and the library targets netstandard2.1 to match Unity 6.

```bash
export NUGET_PACKAGES=/Volumes/PLOP_DEV/Cache/NuGet   # keep NuGet cache off the internal SSD
dotnet test DotNet/Plop.Tests/Plop.Tests.csproj
dotnet run -c Release --project DotNet/Plop.Bench -- --count 100
```

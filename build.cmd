for %%f in (*.csproj) do (
    msbuild %%f /p:"configuration=release"
    .nuget\NuGet.exe pack %%f -Prop Configuration=Release
    .nuget\NuGet.exe pack %%f -Prop Configuration=Release -Symbols
)
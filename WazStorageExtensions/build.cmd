for %%f in (*.csproj) do (
    msbuild %%f /p:"configuration=release"
    nuget pack %%f -Prop Configuration=Release
    nuget pack %%f -Prop Configuration=Release -Symbols
)
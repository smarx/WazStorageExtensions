del WazStorageExtensions\bin\*.nupkg
msbuild WazStorageExtensions.sln /p:"configuration=release"
.nuget\NuGet.exe pack WazStorageExtensions\WazStorageExtensions.csproj -Prop Configuration=Release -Symbols -OutputDirectory WazStorageExtensions\bin 

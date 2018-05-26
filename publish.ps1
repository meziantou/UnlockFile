$NuGetApiKey=$env:NuGetApiKey

Push-Location $PSScriptRoot

& dotnet pack UnlockFile\UnlockFile.csproj --output ..\NuGet

$files = Get-ChildItem .\NuGet -Filter *.nupkg
foreach($file in $files) {
    Write-Host "Pushing NuGet package: $($file.FullName)"
    & dotnet nuget push "$($file.FullName)" --api-key="$($NuGetApiKey)" --source=https://www.nuget.org/api/v2/package --force-english-output
}

Pop-Location
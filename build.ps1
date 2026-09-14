$version = "0.1.0"
$publishDir = "artifacts\AiPetGarden-$version-win-x64"
$zipPath = "artifacts\AiPetGarden-$version-win-x64.zip"

Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue

dotnet publish AiPetGarden\AiPetGarden.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o $publishDir

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item -LiteralPath README.md -Destination "$publishDir\README.md"
#Copy-Item -LiteralPath RELEASE_NOTES.md -Destination "$publishDir\RELEASE_NOTES.md"

Compress-Archive -Path "$publishDir\*" `
  -DestinationPath $zipPath `
  -CompressionLevel Optimal

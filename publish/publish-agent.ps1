# Version numbers parts can have a max of 65534, the total seconds of a day could add up until 86400
# therefor we divide the elapsed seconds by 2
$elapsedSecondsHalf = [math]::Truncate( (Get-Date).TimeOfDay.TotalSeconds / 2)
$buildVersion = (Get-Date -Format "yyyy.MM.dd") + "." + $elapsedSecondsHalf
$buildVersion
# Create directory if not exists
New-Item -ItemType Directory -Force -Path ".\published"
# Remove files/folders inside the published folder
Remove-Item ".\published\*" -Recurse -Force
dotnet publish ../src/QF.BySoft.Integration -r win-x64 -p:PublishSingleFile=true /p:Version=$buildVersion -f net9.0 -p:IncludeAllContentForSelfExtract=true -c Release /p:DebugType=none /p:DebugSymbols=false -o ./published/Integration
Copy-Item "../src/QF.BySoft.Integration/appsettings.json" -Destination ".\published\Integration"
Copy-Item ".\data" -Destination ".\published\Integration" -Recurse
# Copy the manual
Copy-Item "../../Documentation\Manual Rhodium24 BySoft Integration.pdf" -Destination ".\published\Integration"
# Copy changes.txt
Copy-Item ".\Changes.txt" -Destination ".\published\Integration"
# Create zip file
Compress-Archive -DestinationPath ".\published\QF.BySoft.Integration.$buildVersion.zip" -Path ".\published\Integration\*" -CompressionLevel Optimal -Force
# Remove files/folders inside the Integration folder
Remove-Item ".\published\Integration*" -Recurse -Force
# Write the version to text file, this can be added to the source control
Set-Content -Path ".\published-latest-version.txt" -Value $buildVersion

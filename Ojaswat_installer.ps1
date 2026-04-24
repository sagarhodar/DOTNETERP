# Build and create installer for Ojaswat

Clear-Host

# 1. Ask version
$version = Read-Host "Enter the version (e.g., 30.0.0)"

# Validate version
try {
    [version]$version | Out-Null
}
catch {
    Write-Host "Invalid version format! Use format like 30.0.0"
    pause
    exit
}

# 2. Change version in .csproj
$csprojPath = "Ojaswat.csproj"

[xml]$csprojXml = Get-Content $csprojPath

$propertyGroup = $csprojXml.Project.PropertyGroup

if ($propertyGroup.Version) {
    $propertyGroup.Version = $version
} else {
    $newVersion = $csprojXml.CreateElement("Version")
    $newVersion.InnerText = $version
    $propertyGroup.AppendChild($newVersion) | Out-Null
}

$csprojXml.Save($csprojPath)

# 2 (continued). Change version in .iss
$issPath = "Ojaswat.iss"
$issContent = Get-Content $issPath -Raw

$issContent = $issContent -replace "AppVersion=.*", "AppVersion=$version"
$issContent = $issContent -replace "OutputBaseFilename=.*", "OutputBaseFilename=ojaswat_V_$($version -replace '\.', '_')"

Set-Content $issPath $issContent

# 3. Success message
Write-Host "Version updated successfully to $version"

# 4. Ask for build app
$buildApp = Read-Host "Do you want to build the app? (y/n)"

if ($buildApp -match "^(y|yes)$") {
    Write-Host "Building application..."

    dotnet publish -c Release -r win-x64 `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:EnableCompressionInSingleFile=true

    # 5. Success message
    Write-Host "Application build completed"
}
else {
    Write-Host "Skipped app build"
}

# 6. Ask for installer build
$buildInstaller = Read-Host "Do you want to build the installer? (y/n)"

if ($buildInstaller -match "^(y|yes)$") {
    Write-Host "Building installer..."

    ISCC.exe Ojaswat.iss

    # 7. Success message
    Write-Host "Installer created successfully"
}
else {
    Write-Host "Skipped installer build"
}

# 8. Press any key to close
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
param (
	[switch] $DryRun,   # print new version without saving
	[switch] $Major,    # increment major version
	[switch] $Minor,    # increment minor version
	[switch] $Patch     # increment patch version
)

# expects XML structured like
#    <Project>
#      <PropertyGroup>
#        <AssemblyVersion>2.0.0.0</AssemblyVersion>

set-psdebug -strict
$ErrorActionPreference = "stop"

$local = "@"
$remote = "@{u}"

git remote update
$localHash = git rev-parse $local
$remoteHash = git rev-parse $remote
if ($localHash -ne $remoteHash) {
    Write-Error "`origin` contains commits added since checkout. Cannot increment version."
}

$projFile = "$PSScriptRoot/../src/FeatureFlags.Client/FeatureFlags.Client.csproj"
[xml]$projFileXml = Get-Content $projFile
$fileVersion = [version]$projFileXml.Project.PropertyGroup.AssemblyVersion

if ($Major) {
    $version = "{0}.{1}.{2}.{3}" -f ($fileVersion.Major + 1), 0, 0, 0
} elseif ($Minor) {
    $version = "{0}.{1}.{2}.{3}" -f $fileVersion.Major, ($fileVersion.Minor + 1), 0, 0
} elseif ($Patch) {
    $version = "{0}.{1}.{2}.{3}" -f $fileVersion.Major, $fileVersion.Minor, ($fileVersion.Build + 1), 0
} else {
    $version = "{0}.{1}.{2}.{3}" -f $fileVersion.Major, $fileVersion.Minor, $fileVersion.Build, ($fileVersion.Revision + 1)
}

if (!$DryRun) {
    $projFileXml.Project.PropertyGroup.AssemblyVersion = $version
    $projFileXml.save($projFile)
}

Write-Host "Version {$version}"

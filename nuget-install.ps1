param($rootPath, $toolsPath, $package, $project)

function Get-ScriptDirectory
{
    $Invocation = (Get-Variable MyInvocation -Scope 1).Value
    Split-Path $Invocation.MyCommand.Path
}
$scriptDir = (Get-ScriptDirectory)

if((Get-Module template-builder)){
    Remove-Module template-builder
}

Import-Module (Join-Path -Path ($scriptDir) -ChildPath 'template-builder.psm1')

$projFile = $project.FullName

# Make sure that the project file exists
if(!(Test-Path $projFile)){
    throw ("Project file not found at [{0}]" -f $projFile)
}

# Before modifying the project save everything so that nothing is lost
$DTE.ExecuteCommand("File.SaveAll")

UpdateVsixManifest -project $project

[string]$propsfilepath = (join-path (Split-Path $projFile -Parent) 'Properties\pecan-waffle-settings.props')
if(Test-Path $propsfilepath){
  $DTE.ExecuteCommand("File.OpenFile", $propsfilepath)
}
"    pecan-waffle has been installed into project [{0}]" -f $project.FullName| Write-Host -ForegroundColor DarkGreen
param (
    [Parameter(Mandatory,HelpMessage="Configuration file of which lanugage repositories to download.")] 
    [System.IO.FileInfo] $LanguageConfigFile,
    [Parameter(Mandatory,HelpMessage="The output path for the cloned repositories. E.G. 'C:/project/downloads/<repo_name_here>'")] 
    [System.IO.DirectoryInfo] $OutputDir
)

Write-Output "${scriptName}: STARTING"

$scriptName = Split-Path $PSCommandPath -leaf   # get the name of this script
$scriptDir = Split-Path -parent $PSCommandPath  # find the directory this script is in; expect the 'clone_git_repo.ps1' to be neighboring
$clone_git_repo_scriptName = 'clone_git_repo.ps1';
$clone_git_repo_scriptPath = Join-Path -Path $scriptDir -ChildPath $clone_git_repo_scriptName # get the path to 'clone_git_repo.ps1'

# check that 'clone_git_repo.ps1' exists. Otherwise exit out.
if(-not (Test-Path -Path $clone_git_repo_scriptPath -PathType Leaf))
{
    Write-Output "${scriptName}: ERROR. Expected $clone_git_repo_scriptName at $scriptDir"
    Write-Output "${scriptName}: FINISHED with Errors"
    exit 1
}

if ([string]::IsNullOrEmpty($LanguageConfigFile.FullName))
{
    Write-Output "${scriptName}: ERROR. LanguageConfigFile was not provided."
    Write-Output "${scriptName}: FINISHED with Errors"
    exit 1
}

$Header = 'repository_uri', 'version_tag'
$languageReposCsv = Get-Content -Path $LanguageConfigFile | Where-Object { $_ -notmatch "^#" } | ConvertFrom-Csv -Header $Header 

Write-Output "${scriptName}: Executing $scriptName to download repositories defined by $LanguageConfigFile into $OutputDir."

foreach ($entry in $languageReposCsv)
{
    & $clone_git_repo_scriptPath -GitRepoAddress $entry.repository_uri -VersionTag $entry.version_tag -OutputDir $OutputDir -CreateSubDir

    if (-not $?) # did that succeed?
    {
        Write-Output "${scriptName}: FINISHED with Errors"
        exit 1
    }
}

Write-Output "${scriptName}: FINISHED"
exit 0
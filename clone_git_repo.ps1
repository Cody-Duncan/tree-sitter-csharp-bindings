<#
.SYNOPSIS
    Creates a clean clone of a target repository.

.DESCRIPTION
    Clone a repository from the given GitRepoAddress into the given OutputDir.
    If the OutputDir already exists, and is not a clean copy of the target repository, it will be deleted and cloned anew.

.PARAMETER GitRepoAddress
    An address to a git repository. E.G. https://github.com/tree-sitter/tree-sitter.git

.PARAMETER OutputDir
    The output path for the cloned repository. E.G. 'C:/project/tree-sitter/'

.PARAMETER VersionTag
    A version tag to check out from the repository. E.G. 'v0.20.8'

.PARAMETER CreateSubDir
    If set, will create a subdirectory for the cloned repo under OutputDir. '<OutpuDir>/<name_of_repo>'

.EXAMPLE
    ./clone_git_repo.ps1 -GitRepoAddress='https://github.com/tree-sitter/tree-sitter.git' -VersionTag 'v0.20.8' -OutputDir='C:/project/downloads/' -CreateSubDir
    
    Will create and clone the target repository 'tree-sitter.git' into 'C:/project/downloads/tree-sitter' at the commit for tag v0.20.8, and will create a branch named 'v0.20.8' at that commit.
    If 'C:/project/downloads/tree-sitter' already exists and has no changes, it will skip cloning. (this does not check if it's the same commit at the version tag)
    If 'C:/project/downloads/tree-sitter' exists and has changes, it will delete the directory and clone a new copy.
#>
param (
    [Parameter(HelpMessage="A version tag to check out from the repository. E.G. v0.20.8")][
    string] $VersionTag,
    [Parameter(Mandatory,HelpMessage="An address to a git repository. E.G. https://github.com/tree-sitter/tree-sitter.git")] 
    [uri] $GitRepoAddress,
    [Parameter(Mandatory,HelpMessage="The output path for the cloned repository. E.G. 'C:/project/downloads/'")] 
    [System.IO.DirectoryInfo] $OutputDir,
    [Parameter(Mandatory,HelpMessage="If set, will create a subdirectory for the cloned repo. E.G.")] 
    [switch]$CreateSubDir = $false
)

$scriptName = Split-Path $PSCommandPath -leaf

$repoName = $GitRepoAddress.Segments[-1] # get the name of the git repo. E.G. 'https://github.com/tree-sitter/tree-sitter.git' -> tree-sitter.git
$repoName = $repoName.Substring(0, $repoName.LastIndexOf('.')) # cut off the extension '.git'

# if -CreateSubDir, then create a subdirectory of the repository name
if ($CreateSubDir)
{
    $OutputDir = Join-Path -Path $OutputDir -ChildPath $repoName
}

Write-Output "${scriptName}: STARTING"
Write-Output "${scriptName}: Executing $scriptName to download repository from $GitRepoAddress."

Write-Output "${scriptName}: Checking if $OutputDir exists"
if (Test-Path -Path $OutputDir)
{
    Write-Output "${scriptName}: $repoName repo already exists at $OutputDir"
    Write-Output "${scriptName}: Checking if repository is clean"
    $repo_has_changes = git -C $OutputDir status --porcelain
    if ($repo_has_changes)
    {
        Write-Output "${scriptName}: UNCLEAN: $repoName repo has local changes. Deleting (with Force) and re-cloning repository."
        Remove-Item $OutputDir -Recurse -Force
    }
    else 
    {
        Write-Output "${scriptName}: CLEAN: $repoName repo exists and has no changes. Skipping clone."
    }
}

if (-not(Test-Path -Path $OutputDir))
{
    Write-Output "${scriptName}: CLONING: Cloning $repoName repostiory from $GitRepoAddress into $OutputDir"
    
    # clone the repository
    $gitCommand= "git -c advice.detachedHead=false clone -b $VersionTag --depth 1 $GitRepoAddress $OutputDir"
    Write-Output "${scriptName}: Executing ``$gitCommand``"
    Invoke-Expression $gitCommand 

    if (-not ([string]::IsNullOrEmpty($VersionTag)))
    {
        # create a new branch with the name of the version tag. Otherwise the repo is in a detached HEAD state.
        git -C $OutputDir switch -c $VersionTag 
    }

    # output the summary so the user can double check that it's on the right commit
    Write-Output "$repoName @ commit"
    git log -n 1 --oneline
}

Write-Output "${scriptName}: FINISHED"
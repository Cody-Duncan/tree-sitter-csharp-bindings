$versionTag = "v0.20.8"
$repoAddress = "https://github.com/tree-sitter/tree-sitter.git"
# set the output directory to ./tree-sitter from where the script is located (in the solution directory)
$outputDir = Join-Path -Path $PSScriptRoot -ChildPath tree-sitter

$scriptName = Split-Path $PSCommandPath -leaf
Write-Output "${scriptName}: STARTING"
Write-Output "${scriptName}: Executing $scriptName to download Tree-Sitter repository."

Write-Output "${scriptName}: Checking if $outputDir exists"
if (Test-Path -Path $outputDir)
{
    Write-Output "${scriptName}: Tree-Sitter repo already exists at $outputDir"
    Write-Output "${scriptName}: Checking if repository is clean"
    $repo_has_changes = git -C $outputDir status --porcelain
    if ($repo_has_changes)
    {
        Write-Output "${scriptName}: UNCLEAN: Tree-Sitter repo has local changes. Deleting (with Force) and re-cloning repository."
        Remove-Item $outputDir -Recurse -Force
    }
}

if (-not(Test-Path -Path $outputDir))
{
    Write-Output "${scriptName}: CLONING: Cloning Tree-Sitter repostiory from $repoAddress into $outputDir"
    
    # clone the repository
    $gitCommand= "git -c advice.detachedHead=false clone -b $versionTag --depth 1 $repoAddress $outputDir"
    Write-Output "${scriptName}: Executing ``$gitCommand``"
    Invoke-Expression $gitCommand 

    # create a new branch with the name of the version tag. Otherwise the repo is in a detached HEAD state.
    git -C $outputDir switch -c $versionTag 

    # output the git log so the user can double check that it's on the right commit
    git -C $outputDir log 
}

Write-Output "${scriptName}: FINISHED"
#!/usr/bin/env pwsh

# Roughly the same as
# ```cmd
# rmdir build
# mkdir build
# cd build
# cmake ..
# cmake --build --config Release .
# ```
# but avoiding the 'cd' operations so it doesn't have to deal with restoring the working directory afterward.

$projectname = "BindingsGenerator"
$currentpath = Get-Location
$buildpath = Join-Path -Path $PSScriptRoot -ChildPath "build"
$outputBinaryPath = Join-Path -Path $PSScriptRoot -ChildPath "build/Release/${projectname}.exe"

# Clean the /build directory
if (Test-Path -Path $buildpath)
{
	Write-Host "${projectname}: Cleaning build directory $buildpath"
	Remove-Item -LiteralPath $buildpath -Force -Recurse
}

# Make sure the /build directory is gone
if (Test-Path -Path $buildpath)
{
	Write-Host "${projectname}: Cannot generate because build directory is already present."
	exit 1
}

# Create a new /build directory
Write-Host "${projectname}: Creating build directory"
mkdir $buildpath

# Generate the vcsproj
Write-Host "${projectname}: Running cmake configuration step"
cmake -B $buildpath -S $PSScriptRoot

if(-not $?) # failed to generate
{
	Write-Host "${projectname}: Failed to configure ${projectname}"
	exit 1
}

# This runs a quick replacement in the .sln file, 'Any CPU' -> 'x64'
# This is a workaround for Issue #23513 https://gitlab.kitware.com/cmake/cmake/-/issues/23513
# Problem: 
#     The generated .sln file specifies all the C# project default build configurations to 'Any CPU', even if that configuration hasn't been defined!
#     This results in Visual Studio reporting 
#         "Current solution contains incorrect configurations mappings. It May cause projects to not work correctly. Open the Configuration Manager to fix them."
# Fix:
#     This text replacement overrides the default build platform for the generated C# projects.
(Get-Content $buildpath/BindingsGenerator.sln) -replace 'Any CPU', 'x64' | Out-File $buildpath/BindingsGenerator.sln

# Build the BindingsGenerator.exe
Write-Host "${projectname}: Running cmake build step"
cmake --build $buildpath --config Release

# Check that the executable was succesfully generated
if (Test-Path -Path $outputBinaryPath)
{
	Write-Host "${projectname}: Successfully compiled ${projectname}.exe at $outputBinaryPath"
}

exit 0
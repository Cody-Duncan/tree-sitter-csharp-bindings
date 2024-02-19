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

$scriptName = Split-Path $PSCommandPath -leaf
$buildpath = Join-Path -Path $PSScriptRoot -ChildPath "build"

# Make sure the /build directory is gone
if (-not (Test-Path -Path $buildpath))
{
    Write-Host "${scriptName}: Creating build directory"
	mkdir $buildpath
}

# Run cmake
Write-Host "${scriptName}: Running cmake configuration step"
cmake -B $buildpath -S $PSScriptRoot

if(-not $?) # failed to generate
{
	Write-Host "${scriptName}: Failed to configure ${scriptName}"
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
$slnFile = Get-ChildItem -Path $buildpath -Filter *.sln | Select-Object -First 1
Write-Host "${scriptName}: Fixing up the contents of $slnFile to fix 'Current solution contains incorrect configurations mappings' error."
(Get-Content $slnFile) -replace 'Any CPU', 'x64' | Out-File $slnFile

# Build the solution
Write-Host "${scriptName}: Running cmake build step for Debug"
cmake --build $buildpath --config Debug
Write-Host "${scriptName}: Running cmake build step for Release"
cmake --build $buildpath --config Release

Write-Host "${scriptName}: Copying /lib/Debug to /bin/Debug directory"
Get-ChildItem -Path "$buildpath\lib\Debug\*" -Include * | Copy-Item -Destination "$buildpath\bin\Debug"
Write-Host "${scriptName}: Copying /lib/Release to /bin/Release directory"
Get-ChildItem -Path "$buildpath\lib\Release\*" -Include * | Copy-Item -Destination "$buildpath\bin\Release" 

Write-Host "${scriptName}: DONE! Success"


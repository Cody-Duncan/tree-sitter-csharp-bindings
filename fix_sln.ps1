Start-Sleep 1
$sln_file = $args[0]
$scriptName = Split-Path $PSCommandPath -leaf
Write-Host "${scriptName}: Fixing Up ${sln_file}"
if (!(Test-Path $sln_file)) {
    Write-Host "${this_file_name}: Could not find file: ${sln_file}"
}
else {
    (Get-Content $sln_file) -replace 'Any CPU', 'x64' | Out-File $sln_file
}
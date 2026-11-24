<#
Deletes the project's Azure resources, except the Foundry resource and its model deployments.
Dry run by default. Add -Delete to delete.

  .\scripts\teardown.ps1                 # lists what would be deleted
  .\scripts\teardown.ps1 -Delete         # deletes, after you type DELETE
#>
param(
    [string]$ResourceGroup = "rg-claims-dev",
    [switch]$Delete
)

$filter = "[?!starts_with(type, 'Microsoft.CognitiveServices')]"
$resources = az resource list -g $ResourceGroup --query "$filter.{name:name, type:type}" -o json | ConvertFrom-Json

if (-not $resources) {
    Write-Host "Nothing to delete in $ResourceGroup."
    return
}

Write-Host "These resources would be deleted (the Foundry resource is kept):"
$resources | ForEach-Object { Write-Host ("  {0}  ({1})" -f $_.name, $_.type) }

if (-not $Delete) {
    Write-Host ""
    Write-Host "Dry run only. Run again with -Delete to delete them."
    return
}

$answer = Read-Host "Type DELETE to confirm"
if ($answer -ne "DELETE") {
    Write-Host "Cancelled."
    return
}

# Resources that others depend on fail at first and succeed on a later pass.
for ($pass = 1; $pass -le 4; $pass++) {
    $ids = az resource list -g $ResourceGroup --query "$filter.id" -o tsv
    if (-not $ids) { break }
    Write-Host "Pass $pass"
    foreach ($id in $ids) {
        az resource delete --ids $id --only-show-errors | Out-Null
    }
}

Write-Host "Done. Check the resource group in the portal. If API Management was deleted, it stays"
Write-Host "soft-deleted for a while. Purge it with: az apim deletedservice list"

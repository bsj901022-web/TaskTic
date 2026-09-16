param([Parameter(Mandatory=$true)][string]$RequestPath)
$ErrorActionPreference = 'Stop'
$encrypted = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'token.dpapi') -Raw
$secure = ConvertTo-SecureString $encrypted
$credential = New-Object System.Management.Automation.PSCredential('pixellab', $secure)
$request = Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json -AsHashtable
$request.token = $credential.GetNetworkCredential().Password
$request | ConvertTo-Json -Depth 40 -Compress | & node (Join-Path $PSScriptRoot 'bridge.mjs')
$request.token = $null

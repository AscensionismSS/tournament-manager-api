$ErrorActionPreference = 'Stop'
$destDir = 'C:\Users\gabri\Desktop\dota2_heroes_icons'

New-Item -ItemType Directory -Force -Path "$destDir\by-id" | Out-Null
New-Item -ItemType Directory -Force -Path "$destDir\by-name" | Out-Null

Write-Host "Fetching Dota 2 heroes metadata from OpenDota / Valve..."
$heroesJson = Invoke-RestMethod -Uri 'https://raw.githubusercontent.com/odota/dotaconstants/master/build/heroes.json'
$heroesList = @()

foreach ($prop in $heroesJson.PSObject.Properties) {
    $heroesList += $prop.Value
}

Write-Host "Total heroes found: $($heroesList.Count)"

$baseUrl = 'https://cdn.cloudflare.steamstatic.com'
$successCount = 0
$failedCount = 0

foreach ($h in $heroesList) {
    $id = $h.id
    $shortName = $h.name.Replace('npc_dota_hero_', '')
    $iconPath = $h.icon.TrimEnd('?')
    $url = $baseUrl + $iconPath

    $idFile = "$destDir\by-id\$id.png"
    $nameFile = "$destDir\by-name\$shortName.png"

    try {
        Invoke-WebRequest -Uri $url -OutFile $idFile -TimeoutSec 10 -UseBasicParsing
        Copy-Item -Path $idFile -Destination $nameFile -Force
        $successCount++
        Write-Host "[$successCount/$($heroesList.Count)] Downloaded $shortName (ID: $id)"
    } catch {
        Write-Warning "Failed to download $shortName (ID: $id): $($_.Exception.Message)"
        $failedCount++
    }
}

$heroesJson | ConvertTo-Json -Depth 4 | Set-Content -Path "$destDir\heroes.json" -Encoding UTF8
Write-Host "=== FINALIZADO COM SUCESSO ==="
Write-Host "Total baixados: $successCount"
Write-Host "Falhas: $failedCount"
Write-Host "Pasta destino: $destDir"


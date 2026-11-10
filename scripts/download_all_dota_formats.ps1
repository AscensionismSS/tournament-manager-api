$ErrorActionPreference = 'Stop'
$destDir = 'C:\Users\gabri\Desktop\dota2_heroes_assets'

New-Item -ItemType Directory -Force -Path "$destDir\icons" | Out-Null
New-Item -ItemType Directory -Force -Path "$destDir\horizontal" | Out-Null
New-Item -ItemType Directory -Force -Path "$destDir\vertical" | Out-Null

Write-Host "Fetching Dota 2 heroes metadata from OpenDota / Valve..."
$heroesJson = Invoke-RestMethod -Uri 'https://raw.githubusercontent.com/odota/dotaconstants/master/build/heroes.json'
$heroesList = @()

foreach ($prop in $heroesJson.PSObject.Properties) {
    $heroesList += $prop.Value
}

Write-Host "Total heroes found: $($heroesList.Count)"

$baseUrl = 'https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes'

$countIcons = 0
$countHoriz = 0
$countVert = 0
$errors = 0

foreach ($h in $heroesList) {
    $id = $h.id
    $shortName = $h.name.Replace('npc_dota_hero_', '')
    
    # 1. Icon (1:1)
    $iconUrl = "$baseUrl/icons/$shortName.png"
    $iconFile = "$destDir\icons\$id.png"
    try {
        Invoke-WebRequest -Uri $iconUrl -OutFile $iconFile -TimeoutSec 10 -UseBasicParsing
        $countIcons++
    } catch {
        Write-Warning "Icon failed for $shortName (ID: $id)"
        $errors++
    }

    # 2. Horizontal (16:9)
    $horizUrl = "$baseUrl/$shortName.png"
    $horizFile = "$destDir\horizontal\$id.png"
    try {
        Invoke-WebRequest -Uri $horizUrl -OutFile $horizFile -TimeoutSec 10 -UseBasicParsing
        $countHoriz++
    } catch {
        Write-Warning "Horizontal failed for $shortName (ID: $id)"
        $errors++
    }

    # 3. Vertical Poster (3:4)
    $vertUrl = "$baseUrl/vert/$shortName.png"
    $vertFile = "$destDir\vertical\$id.png"
    try {
        Invoke-WebRequest -Uri $vertUrl -OutFile $vertFile -TimeoutSec 10 -UseBasicParsing
        $countVert++
    } catch {
        # Some heroes might not have /vert/ or might use /crops/
        try {
            $cropUrl = "$baseUrl/crops/$shortName.png"
            Invoke-WebRequest -Uri $cropUrl -OutFile $vertFile -TimeoutSec 10 -UseBasicParsing
            $countVert++
        } catch {
            Write-Warning "Vertical failed for $shortName (ID: $id)"
            $errors++
        }
    }

    Write-Host "[$id] $shortName -> Icons, Horiz, Vert processados."
}

# Also save heroes_metadata.json
$heroesJson | ConvertTo-Json -Depth 4 | Set-Content -Path "$destDir\heroes_metadata.json" -Encoding UTF8

Write-Host "=== CONCLUÍDO COM SUCESSO ==="
Write-Host "Ícones (1:1): $countIcons"
Write-Host "Horizontais (16:9): $countHoriz"
Write-Host "Verticais (3:4): $countVert"
Write-Host "Erros: $errors"
Write-Host "Destino: $destDir"


param(
    [Parameter(Mandatory = $true)][string]$Nombre
)
# Genera el ZIP de entrega sin binarios, bases de datos, logs ni salidas generadas.
$raiz = Split-Path -Parent $PSScriptRoot
$destino = Join-Path $raiz "APE04_LogiMarket360_$Nombre.zip"
$temporal = Join-Path ([System.IO.Path]::GetTempPath()) ("lm-entrega-" + [guid]::NewGuid().ToString("N"))
$excluir = @('bin', 'obj', '.vs', '.git', '.claude', 'datos', 'logs', 'salidas', 'TestResults')
# carpetas que solo se excluyen en la raiz (src/LogiMarket.Application/Entrega es codigo y si va)
$excluirRaiz = @('entrega')
# Archivos internos de trabajo que no forman parte de la entrega: el enunciado del docente, el contexto del
# asistente y una configuración local (se entrega solo appsettings.ejemplo.json).
$archivosExcluidos = @('GUIA_APE_04.docx', 'CLAUDE.md', 'appsettings.json')

New-Item -ItemType Directory -Force $temporal | Out-Null
Get-ChildItem $raiz -Recurse -File | Where-Object {
    $relativa = $_.FullName.Substring($raiz.Length + 1)
    $partes = $relativa.Split([System.IO.Path]::DirectorySeparatorChar)
    -not ($partes | Where-Object { $excluir -contains $_ }) -and
    -not ($partes.Count -gt 1 -and $excluirRaiz -contains $partes[0]) -and
    $archivosExcluidos -notcontains $_.Name -and
    $_.Extension -notin @('.db', '.db-shm', '.db-wal', '.zip', '.user')
} | ForEach-Object {
    $relativa = $_.FullName.Substring($raiz.Length + 1)
    $copia = Join-Path $temporal $relativa
    New-Item -ItemType Directory -Force (Split-Path -Parent $copia) | Out-Null
    Copy-Item $_.FullName $copia
}
if (Test-Path $destino) { Remove-Item $destino -Confirm:$false }
Compress-Archive -Path (Join-Path $temporal '*') -DestinationPath $destino
Remove-Item $temporal -Recurse -Force -Confirm:$false
Write-Output "ZIP generado: $destino"

#verifica a mano si una contrasena corresponde a un hash guardado en la tabla Usuario
#formato del hash: PBKDF2$iteraciones$salBase64$hashBase64 (igual que HasherContrasenaPbkdf2)
#uso: .\scripts\verificar-hash.ps1 -Clave 'Admin123!' -Hash 'PBKDF2$100000$...$...'
param(
    [Parameter(Mandatory)] [string] $Clave,
    [Parameter(Mandatory)] [string] $Hash
)

$partes = $Hash.Split('$')
if ($partes.Length -ne 4 -or $partes[0] -ne 'PBKDF2') {
    Write-Output 'formato de hash no valido'
    exit 1
}

#se repite el calculo con la misma sal e iteraciones y se compara el resultado
$iteraciones = [int]$partes[1]
$sal = [Convert]::FromBase64String($partes[2])
$esperado = [Convert]::FromBase64String($partes[3])
$derivador = New-Object Security.Cryptography.Rfc2898DeriveBytes($Clave, $sal, $iteraciones,
    [Security.Cryptography.HashAlgorithmName]::SHA256)
$calculado = $derivador.GetBytes($esperado.Length)

if ([Convert]::ToBase64String($calculado) -eq $partes[3]) {
    Write-Output 'COINCIDE: la contrasena corresponde a este hash'
} else {
    Write-Output 'NO COINCIDE'
}

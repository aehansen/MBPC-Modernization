# ==============================================================================
# Script de Verificación y Testing de Operaciones en Lote (Hito 2 - Masivos)
# ==============================================================================
# Este script se encarga de iniciar sesión, obtener un token JWT, buscar un viaje
# y convoy activos, y probar los endpoints masivos de Separación y Fondeo.
#
# Uso:
#   .\testear-masivos.ps1 [-BaseUrl "http://localhost:5000"]
# ==============================================================================

param (
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " Iniciando Script de Verificación de Endpoints Masivos de Convoyes" -ForegroundColor Cyan
Write-Host " Base URL: $BaseUrl" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# ------------------------------------------------------------------------------
# 1. Iniciar sesión para obtener token JWT
# ------------------------------------------------------------------------------
Write-Host "`n[1/5] Autenticando en el sistema..." -ForegroundColor Yellow
$loginBody = @{
    CosteraId = 0
    Password  = "AdminPassword123"
} | ConvertTo-Json

try {
    $authResult = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $token = $authResult.token
    Write-Host "✔ Autenticación exitosa. Token obtenido." -ForegroundColor Green
} catch {
    Write-Error "❌ Error al iniciar sesión. ¿El backend está corriendo en $BaseUrl? Detalle: $_"
    exit
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

# ------------------------------------------------------------------------------
# 2. Buscar un viaje activo de forma automática
# ------------------------------------------------------------------------------
Write-Host "`n[2/5] Buscando un viaje activo en el sistema..." -ForegroundColor Yellow
try {
    $viajesResponse = Invoke-RestMethod -Uri "$BaseUrl/api/viajes?page=1&size=5" -Method Get -Headers $headers
    $viaje = $viajesResponse.items | Select-Object -First 1

    if ($null -eq $viaje) {
        Write-Host "⚠ No se encontraron viajes activos en el sistema. Creando datos de prueba..." -ForegroundColor Yellow
        # Creamos un viaje de prueba
        $nuevoViajeBody = @{
            buqueId = 1
            rioCanalKmPar = 120
            fechaEstimadaArribo = (Get-Date).ToString("o")
            costeraId = 0
        } | ConvertTo-Json
        $viaje = Invoke-RestMethod -Uri "$BaseUrl/api/viajes" -Method Post -Body $nuevoViajeBody -Headers $headers -ContentType "application/json"
        Write-Host "✔ Viaje de prueba creado con Id: $($viaje.id)" -ForegroundColor Green
    } else {
        Write-Host "✔ Viaje activo seleccionado: $($viaje.nombreBuque) (Id: $($viaje.id))" -ForegroundColor Green
    }
    $viajeId = $viaje.id
} catch {
    Write-Error "❌ Error al recuperar viajes activos: $_"
    exit
}

# ------------------------------------------------------------------------------
# 3. Recuperar composición del convoy
# ------------------------------------------------------------------------------
Write-Host "`n[3/5] Consultando la composición del convoy..." -ForegroundColor Yellow
try {
    $convoy = Invoke-RestMethod -Uri "$BaseUrl/api/convoyes/viaje/$viajeId" -Method Get -Headers $headers
    Write-Host "✔ Convoy recuperado. Remolcador: $($convoy.nombreBuque)" -ForegroundColor Green
    Write-Host "  Barcazas actuales en el convoy: $($convoy.barcazas.Count)" -ForegroundColor Gray

    # Si no hay barcazas en el convoy, adjuntamos algunas para la prueba
    if ($convoy.barcazas.Count -eq 0) {
        Write-Host "⚠ El convoy no tiene barcazas. Adjuntando barcazas de prueba..." -ForegroundColor Yellow
        $adjuntarBody = @{
            barcazasIds = @("BCZ-TEST-99", "BCZ-TEST-88")
            ubicacion = "Zona de Prueba Km 120"
        } | ConvertTo-Json

        $adjuntarRes = Invoke-RestMethod -Uri "$BaseUrl/api/convoyes/viaje/$viajeId/adjuntar" -Method Post -Body $adjuntarBody -Headers $headers -ContentType "application/json"
        Write-Host "✔ Barcazas adjuntadas: BCZ-TEST-99, BCZ-TEST-88" -ForegroundColor Green

        # Recargamos la composición del convoy
        $convoy = Invoke-RestMethod -Uri "$BaseUrl/api/convoyes/viaje/$viajeId" -Method Get -Headers $headers
    }

    foreach ($b in $convoy.barcazas) {
        Write-Host "    - Barcaza Id: $($b.id) | Nombre: $($b.nombre) | Estado: $($b.estado) | Muelle: $($b.muelleActual)" -ForegroundColor Gray
    }
} catch {
    Write-Error "❌ Error al consultar o preparar el convoy: $_"
    exit
}

# ------------------------------------------------------------------------------
# 4. Probar Endpoint Masivo de Fondeo: POST /api/convoyes/viaje/{viajeId}/fondear-masivo
# ------------------------------------------------------------------------------
Write-Host "`n[4/5] Probando Fondeo Masivo en lote..." -ForegroundColor Yellow
$barcazasAFondear = $convoy.barcazas | Select-Object -ExpandProperty id
$zonaFondeo = "Zona Fondeadero Especial Km 125"

$fondearBody = @{
    barcazasIds = $barcazasAFondear
    zonaFondeo  = $zonaFondeo
} | ConvertTo-Json

try {
    $fondearResponse = Invoke-RestMethod -Uri "$BaseUrl/api/convoyes/viaje/$viajeId/fondear-masivo" -Method Post -Body $fondearBody -Headers $headers -ContentType "application/json"
    Write-Host "✔ Endpoint de fondeo masivo respondió: $($fondearResponse.mensaje)" -ForegroundColor Green

    # Verificar que el estado cambió en MongoDB
    $convoyActualizado = Invoke-RestMethod -Uri "$BaseUrl/api/convoyes/viaje/$viajeId" -Method Get -Headers $headers
    Write-Host "  Verificación del estado post-fondeo:" -ForegroundColor Gray
    foreach ($b in $convoyActualizado.barcazas) {
        if ($barcazasAFondear -contains $b.id) {
            if ($b.estado -eq "Fondeada" -and $b.muelleActual -eq $zonaFondeo) {
                Write-Host "    - Barcaza $($b.id): ✔ Fondeada correctamente en $zonaFondeo" -ForegroundColor Green
            } else {
                Write-Host "    - Barcaza $($b.id): ❌ Error en estado ($($b.estado)) o ubicación ($($b.muelleActual))" -ForegroundColor Red
            }
        }
    }
} catch {
    Write-Host "❌ Error al ejecutar fondeo masivo: $_" -ForegroundColor Red
}

# ------------------------------------------------------------------------------
# 5. Probar Endpoint Masivo de Separación: POST /api/convoyes/viaje/{viajeId}/separar
# ------------------------------------------------------------------------------
Write-Host "`n[5/5] Probando Separación Masiva en lote..." -ForegroundColor Yellow
$barcazasASeparar = $convoy.barcazas | Select-Object -First 1 | Select-Object -ExpandProperty id
if ($null -eq $barcazasASeparar) { $barcazasASeparar = @("BCZ-TEST-99") }

$separarBody = @{
    barcazasIds = @($barcazasASeparar)
    ubicacion   = "Punto de Corte Km 130"
} | ConvertTo-Json

try {
    $separarResponse = Invoke-RestMethod -Uri "$BaseUrl/api/convoyes/viaje/$viajeId/separar" -Method Post -Body $separarBody -Headers $headers -ContentType "application/json"
    Write-Host "✔ Endpoint de separación masiva respondió: $($separarResponse.mensaje)" -ForegroundColor Green

    # Verificar que ya no figuran en el convoy en MongoDB
    $convoySeparado = Invoke-RestMethod -Uri "$BaseUrl/api/convoyes/viaje/$viajeId" -Method Get -Headers $headers
    $contieneSeparada = $convoySeparado.barcazas.id -contains $barcazasASeparar

    if (-not $contieneSeparada) {
        Write-Host "✔ Verificación exitosa: La barcaza $barcazasASeparar fue retirada del convoy activo." -ForegroundColor Green
    } else {
        Write-Host "❌ Error: La barcaza $barcazasASeparar sigue presente en el convoy activo." -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Error al ejecutar separación masiva: $_" -ForegroundColor Red
}

Write-Host "`n======================================================================" -ForegroundColor Cyan
Write-Host " Pruebas finalizadas." -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

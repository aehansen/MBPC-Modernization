@echo off
setlocal enabledelayedexpansion

set BASE_URL=http://localhost:5000

echo ======================================================================
echo  Script de Verificacion de Endpoints Masivos de Convoyes (CMD)
echo  Base URL: %BASE_URL%
echo ======================================================================

:: 1. Autenticacion
echo.
echo [1/5] Autenticando en el sistema...
curl -s -X POST -H "Content-Type: application/json" -d "{\"CosteraId\":0,\"Password\":\"AdminPassword123\"}" %BASE_URL%/api/auth/login > auth.json

if not exist auth.json (
    echo [ERROR] No se pudo conectar al servidor en %BASE_URL%. ¿Esta corriendo el backend?
    goto :error_exit
)

:: Crear parser JScript temporal para extraer el Token JWT
echo var fs=new ActiveXObject("Scripting.FileSystemObject");var f=fs.OpenTextFile("auth.json",1);var s=f.ReadAll();f.Close();var j=eval("("+s+")");WScript.Echo(j.token); > parse.js
for /f "tokens=*" %%i in ('cscript //nologo parse.js 2^>nul') do set token=%%i
del parse.js

if "%token%"=="" (
    echo [ERROR] No se pudo obtener el token del JSON de respuesta.
    type auth.json
    del auth.json
    goto :error_exit
)

del auth.json
echo ✔ Autenticacion exitosa. Token JWT obtenido.

:: 2. Crear viaje de prueba
echo.
echo [2/5] Creando viaje de prueba...
curl -s -X POST -H "Authorization: Bearer %token%" -H "Content-Type: application/json" -d "{\"costeraId\":\"0\",\"buqueId\":1,\"nombreBuque\":\"BUQUE TEST\",\"origen\":\"Buenos Aires\",\"destino\":\"Rosario\",\"proximoPuntoControl\":\"Km 120\",\"fechaPartida\":\"2026-06-16T12:00:00Z\",\"eta\":\"2026-06-16T15:00:00Z\",\"declaracionMalvinas\":0}" %BASE_URL%/api/viajes > nuevo_viaje.json

if not exist nuevo_viaje.json (
    echo [ERROR] No se recibio respuesta al intentar crear el viaje.
    goto :error_exit
)

type nuevo_viaje.json
del nuevo_viaje.json
echo.

set viajeId=BUQUE%%20TEST
echo ✔ Viaje de prueba creado y asignado a ID/VesselName: %viajeId%

:: 3. Adjuntar barcazas de prueba al convoy
echo.
echo [3/5] Adjuntando barcazas de prueba (BCZ-TEST-99, BCZ-TEST-88) al convoy...
curl -s -X POST -H "Authorization: Bearer %token%" -H "Content-Type: application/json" -d "{\"barcazasIds\":[\"BCZ-TEST-99\",\"BCZ-TEST-88\"],\"ubicacion\":\"Zona de Prueba Km 120\"}" "%BASE_URL%/api/convoyes/viaje/%viajeId%/adjuntar" > adjuntar.json

if not exist adjuntar.json (
    echo [ERROR] No se recibio respuesta del endpoint adjuntar.
    goto :error_exit
)

type adjuntar.json
del adjuntar.json
echo.
echo ✔ Barcazas adjuntadas.

:: 4. Probar Fondeo Masivo en lote
echo.
echo [4/5] Probando Fondeo Masivo en lote (Zona Fondeadero Especial Km 125)...
curl -s -X POST -H "Authorization: Bearer %token%" -H "Content-Type: application/json" -d "{\"barcazasIds\":[\"BCZ-TEST-99\",\"BCZ-TEST-88\"],\"zonaFondeo\":\"Zona Fondeadero Especial Km 125\"}" "%BASE_URL%/api/convoyes/viaje/%viajeId%/fondear-masivo" > fondear.json

if not exist fondear.json (
    echo [ERROR] No se recibio respuesta del endpoint fondear-masivo.
    goto :error_exit
)

type fondear.json
del fondear.json
echo.

:: Consultar composición del convoy para verificar estados en MongoDB
echo.
echo [4.1] Consultando convoy en MongoDB para verificar estados...
curl -s -H "Authorization: Bearer %token%" "%BASE_URL%/api/convoyes/viaje/%viajeId%" > convoy_status.json
echo.
echo Estado de las barcazas post-fondeo:
findstr /I "id nombre estado muelleActual" convoy_status.json
del convoy_status.json
echo.

:: 5. Probar Separacion Masiva en lote
echo.
echo [5/5] Probando Separacion Masiva en lote...
curl -s -X POST -H "Authorization: Bearer %token%" -H "Content-Type: application/json" -d "{\"barcazasIds\":[\"BCZ-TEST-99\"],\"ubicacion\":\"Punto de Corte Km 130\"}" "%BASE_URL%/api/convoyes/viaje/%viajeId%/separar" > separar.json

if not exist separar.json (
    echo [ERROR] No se recibio respuesta del endpoint separar.
    goto :error_exit
)

type separar.json
del separar.json
echo.

:: Consultar composición final para verificar remoción
echo [5.1] Consultando convoy en MongoDB para verificar separacion...
curl -s -H "Authorization: Bearer %token%" "%BASE_URL%/api/convoyes/viaje/%viajeId%" > convoy_status_final.json
echo.
echo Estado de las barcazas final:
findstr /I "id nombre estado muelleActual" convoy_status_final.json
del convoy_status_final.json
echo.

echo ======================================================================
echo  ✔ Pruebas Finalizadas Exitosamente.
echo ======================================================================
pause
exit /b 0

:error_exit
echo.
echo [FALLO] Las pruebas no pudieron completarse debido a un error.
echo ======================================================================
pause
exit /b 1

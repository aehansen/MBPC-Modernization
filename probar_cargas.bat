@echo off
title MBPC - Comprobaciones de Carga y Capacidad
echo =======================================================
echo MBPC: Corriendo comprobaciones de API via Node.js
echo =======================================================
echo.

:: 1. Navegamos a la carpeta raíz del proyecto
cd C:\proyectos\MBPC\prototipoMBPC

:: 2. Agregamos el motor de Node.js portátil al PATH
set PATH=%PATH%;C:\proyectos\MBPC\prototipoMBPC\node-v24.14.0-win-x64

:: 3. Bypass de Proxy para llamadas a localhost (vital en redes corporativas)
set NO_PROXY=localhost,127.0.0.1,::1
set no_proxy=localhost,127.0.0.1,::1

:: 4. Ejecutamos el script de pruebas en JavaScript
node probar_cargas.js

:: 5. Mantenemos la consola abierta para ver los resultados
echo.
pause

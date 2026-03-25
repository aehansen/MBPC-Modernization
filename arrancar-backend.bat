@echo off
title MBPC Backend - .NET 8 API
echo =======================================================
echo Iniciando el entorno Backend de MBPC (.NET 8)
echo =======================================================
echo.

:: 1. Navegamos a la subcarpeta EXACTA donde está el .csproj
cd C:\proyectos\MBPC\prototipoMBPC\Mbpc.Api

:: 2. Ejecutamos el servidor de .NET
dotnet run

:: Mantenemos la ventana abierta por si hay algún error
pause
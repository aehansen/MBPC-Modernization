@echo off
title MBPC Frontend - Vite Dev Server
echo =======================================================
echo Iniciando el entorno Frontend de MBPC (React + Vite)
echo Utilizando motor Node.js Portable (Bypass Corporativo)
echo =======================================================
echo.

:: 1. Navegamos a la carpeta del frontend
cd C:\proyectos\MBPC\prototipoMBPC\frontend-mbpc

:: 2. Inyectamos temporalmente Node en el PATH de esta ventana
set PATH=%PATH%;C:\proyectos\MBPC\prototipoMBPC\node-v24.14.0-win-x64

:: 3. Ejecutamos Vite
npm run dev

:: Mantenemos la ventana abierta por si hay algún error
pause
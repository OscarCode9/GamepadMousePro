@echo off
title Compilador Gamepad Mouse Pro
echo ========================================================
echo Compilando Gamepad Mouse Pro...
echo ========================================================
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:GamepadMousePro.exe Program.cs
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Hubo un problema al compilar el codigo.
    pause
    exit /b %errorlevel%
)
echo.
echo COMPILADO CON EXITO: Se ha creado "GamepadMousePro.exe".
pause

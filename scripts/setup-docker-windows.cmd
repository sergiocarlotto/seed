@echo off
setlocal
chcp 65001 >nul

rem ============================================================
rem  Setup WSL2 + Docker Desktop  -  Projeto Seed (Windows)
rem  Este script se eleva para administrador automaticamente,
rem  instala o WSL2 (motor Linux usado pelo Docker) e o
rem  Docker Desktop. Ao final, pede um reinicio do computador.
rem ============================================================

rem --- Auto-elevacao: se nao estiver como admin, reabre elevado ---
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Solicitando permissao de administrador...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo ==================================================
echo   Setup WSL2 + Docker Desktop  -  Projeto Seed
echo ==================================================
echo.

echo [1/2] Instalando o WSL2 (motor Linux do Docker, sem distro Linux extra)...
wsl --install --no-distribution
if %errorlevel% neq 0 (
    echo.
    echo Aviso: modo "--no-distribution" nao aceito. Tentando modo padrao...
    wsl --install
)
echo.

echo [2/2] Instalando o Docker Desktop via winget...
winget install --id Docker.DockerDesktop --source winget --accept-package-agreements --accept-source-agreements
echo.

echo ==================================================
echo   PROXIMOS PASSOS (leia com atencao)
echo ==================================================
echo   1. REINICIE o computador agora (necessario para o WSL2).
echo   2. Depois de reiniciar, abra o programa "Docker Desktop".
echo   3. Aceite os termos e espere o status ficar "Engine running".
echo   4. Abra um terminal e rode:  docker --version
echo   5. Volte na conversa e me avise que terminou.
echo ==================================================
echo.
pause

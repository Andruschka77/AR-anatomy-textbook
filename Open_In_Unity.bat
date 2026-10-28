@echo off
chcp 65001 > nul
echo ========================================================
echo   Запуск проекта "AR-пособие по анатомии человека" в Unity
echo ========================================================
echo.
set "UNITY_PATH=C:\Program Files\Unity\Hub\Editor\2022.3.30f1\Editor\Unity.exe"
set "PROJECT_PATH=%~dp0"

if not exist "%UNITY_PATH%" (
    echo [ОШИБКА] Не найден Unity 2022.3.30f1 по пути:
    echo %UNITY_PATH%
    echo Пожалуйста, запустите Unity Hub и откройте проект вручную из папки:
    echo %PROJECT_PATH%
    pause
    exit /b
)

echo Открываем Unity Editor 2022.3.30f1...
start "" "%UNITY_PATH%" -projectPath "%PROJECT_PATH%"
echo Готово! Unity запускается в фоне.
pause

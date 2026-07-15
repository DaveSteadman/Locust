@echo off
setlocal

set "ROOT=%~dp0"
cd /d "%ROOT%"

set "API_URL=http://localhost:5000"
set "WORLD_URL=http://localhost:5100"
set "SENSOR_URL=http://localhost:5200"
set "SENSOR2_URL=http://localhost:5201"
set "HEATMAP_OUTPUT=%ROOT%heatmap_level7.png"
set "HEATMAP_INTERVAL_MS=1000"

echo Starting Locust stack from "%ROOT%"
echo API: %API_URL%
echo WorldSim: %WORLD_URL%
echo SensorSimulator001: %SENSOR_URL%
echo SensorSimulator002: %SENSOR2_URL%
echo Heatmap Output: %HEATMAP_OUTPUT%
echo Heatmap Interval: %HEATMAP_INTERVAL_MS%ms
echo.

start "Locust.Api" cmd /k "cd /d "%ROOT%" && set ASPNETCORE_URLS=%API_URL% && dotnet run --project src\Locust.Api\Locust.Api.csproj"
start "Locust.WorldSim" cmd /k "cd /d "%ROOT%" && set ASPNETCORE_URLS=%WORLD_URL% && dotnet run --project src\Locust.WorldSim\Locust.WorldSim.csproj"
start "Locust.SensorSimulator001" cmd /k "cd /d "%ROOT%" && set ASPNETCORE_URLS=%SENSOR_URL% && dotnet run --project src\Locust.SensorSimulator001\Locust.SensorSimulator001.csproj -- --world %WORLD_URL% --api %API_URL% --decay-secs 8"
start "Locust.SensorSimulator002" cmd /k "cd /d "%ROOT%" && set ASPNETCORE_URLS=%SENSOR2_URL% && dotnet run --project src\Locust.SensorSimulator001\Locust.SensorSimulator001.csproj -- --world %WORLD_URL% --api %API_URL% --lon -0.727 --lat 50.170 --direction-degs 90 --range-degs 1.125 --decay-secs 8"
start "Locust.Heatmap" cmd /k "cd /d "%ROOT%" && dotnet run --project src\Locust.Heatmap\Locust.Heatmap.csproj -- %API_URL% %WORLD_URL% %SENSOR_URL% "%HEATMAP_OUTPUT%" %HEATMAP_INTERVAL_MS%"

echo All startup commands have been launched.
echo.
echo Sensor API:
echo   GET %SENSOR_URL%/sensor
echo   PUT %SENSOR_URL%/sensor
echo Second Sensor API:
echo   GET %SENSOR2_URL%/sensor
echo   PUT %SENSOR2_URL%/sensor
echo Heatmap:
echo   %HEATMAP_OUTPUT%

endlocal

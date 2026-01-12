@echo off
echo Creating test DLL for injection testing...
echo.

REM Create test.cpp
(
echo #include ^<windows.h^>
echo.
echo BOOL APIENTRY DllMain^(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved^) {
echo     if ^(ul_reason_for_call == DLL_PROCESS_ATTACH^) {
echo         MessageBoxW^(NULL, L"DLL Injected Successfully!", L"Netherit Test", MB_OK ^| MB_ICONINFORMATION^);
echo     }
echo     return TRUE;
echo }
) > test.cpp

echo Test DLL source created: test.cpp
echo.
echo To compile, you need Visual Studio Build Tools installed.
echo Then run: cl /LD test.cpp /link user32.lib
echo.
echo Or use MinGW: gcc -shared -o test.dll test.cpp -luser32
echo.
pause

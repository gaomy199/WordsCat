#define WIN32_LEAN_AND_MEAN
#include <windows.h>

/* Native, no-CRT entry point: the launcher itself needs no .NET installation.
   All application behavior stays in the existing C# / .NET application. */
static WCHAR executable[32768];
static WCHAR application[32768];
static STARTUPINFOW startup;
static PROCESS_INFORMATION process;

void WINAPI LauncherMain(void)
{
    DWORD length = GetModuleFileNameW(NULL, executable, 32768);
    DWORD result = ERROR_BAD_PATHNAME;
    WCHAR *arguments, *command;
    SIZE_T argumentLength, capacity;
    DWORD directoryLength = length;
    if (!length || length >= 32768) goto failed;
    while (directoryLength && executable[directoryLength - 1] != L'\\') directoryLength--;
    if (!directoryLength || directoryLength + 18 >= 32768) goto failed;
    executable[directoryLength] = L'\0';
    lstrcpyW(application, executable);
    lstrcatW(application, L"app\\WordsCat.exe");
    if (GetFileAttributesW(application) == INVALID_FILE_ATTRIBUTES) {
        result = ERROR_FILE_NOT_FOUND;
        goto failed;
    }

    /* Preserve the original argument tail, including quoting and empty values.
       --data-dir and the offline diagnostic arguments pass through unchanged. */
    arguments = GetCommandLineW();
    if (*arguments == L'"') {
        arguments++;
        while (*arguments && *arguments != L'"') arguments++;
        if (*arguments) arguments++;
    } else {
        while (*arguments && *arguments != L' ' && *arguments != L'\t') arguments++;
    }
    while (*arguments == L' ' || *arguments == L'\t') arguments++;
    argumentLength = (SIZE_T)lstrlenW(arguments);
    capacity = (SIZE_T)lstrlenW(application) + argumentLength + 5;
    if (capacity > 32767) {
        result = ERROR_BAD_ARGUMENTS;
        goto failed;
    }
    command = HeapAlloc(GetProcessHeap(), 0, capacity * sizeof(WCHAR));
    if (!command) {
        result = ERROR_NOT_ENOUGH_MEMORY;
        goto failed;
    }
    lstrcpyW(command, L"\"");
    lstrcatW(command, application);
    lstrcatW(command, L"\" ");
    lstrcatW(command, arguments);
    startup.cb = sizeof(startup);
    /* The child resolves resources from app/ and Data from the portable root.
       Do not use a shell, search PATH, elevate, or alter the user's Data. */
    if (!CreateProcessW(application, command, NULL, NULL, FALSE, 0, NULL, executable, &startup, &process)) {
        result = GetLastError();
        HeapFree(GetProcessHeap(), 0, command);
        goto failed;
    }
    HeapFree(GetProcessHeap(), 0, command);
    CloseHandle(process.hThread);
    /* Preserve exit codes for verify.ps1 and automated release validation. */
    if (WaitForSingleObject(process.hProcess, INFINITE) == WAIT_OBJECT_0) {
        if (!GetExitCodeProcess(process.hProcess, &result)) result = GetLastError();
    } else result = GetLastError();
    CloseHandle(process.hProcess);
    ExitProcess(result);

failed:
    MessageBoxW(NULL,
        L"WordsCat could not start. Extract the complete ZIP and keep the app folder beside WordsCat.exe.\n"
        L"If Windows blocked the files, review the file properties or contact the publisher.",
        L"WordsCat", MB_OK | MB_ICONERROR);
    ExitProcess(result);
}

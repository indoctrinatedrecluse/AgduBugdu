using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace AgduBugdu.Infrastructure.Terminal;

/// <summary>
/// Windows ConPTY (pseudo-console) host. Provides a real terminal so interactive TUI
/// programs receive raw keyboard input and can query terminal size — plain redirected
/// pipes starve child processes of input because the shell's stdin reader reads ahead.
/// </summary>
public sealed class WindowsPseudoConsole : IDisposable
{
    private IntPtr _hPc = IntPtr.Zero;
    private IntPtr _hProcess = IntPtr.Zero;
    private IntPtr _hThread = IntPtr.Zero;
    private Process? _process;
    private SafeFileHandle? _inputWrite;
    private SafeFileHandle? _outputRead;
    private FileStream? _inputStream;
    private FileStream? _outputStream;
    private int _processId;

    public event EventHandler<int>? Exited;

    public bool IsRunning { get; private set; }
    public Stream InputStream => _inputStream!;
    public Stream OutputStream => _outputStream!;

    public void Start(string commandLine, string? workingDirectory, short columns, short rows)
    {
        if (IsRunning) return;

        var size = new NativeMethods.COORD { X = columns, Y = rows };

        if (!NativeMethods.CreatePipe(out var inputRead, out var inputWrite, IntPtr.Zero, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe(input) failed");
        if (!NativeMethods.CreatePipe(out var outputRead, out var outputWrite, IntPtr.Zero, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe(output) failed");

        const int handleFlagInherit = 1;
        NativeMethods.SetHandleInformation(inputWrite, handleFlagInherit, 0);
        NativeMethods.SetHandleInformation(outputRead, handleFlagInherit, 0);

        int hr = NativeMethods.CreatePseudoConsole(size, inputRead, outputWrite, 0, out _hPc);
        if (hr != 0)
        {
            inputRead.Dispose(); inputWrite.Dispose(); outputRead.Dispose(); outputWrite.Dispose();
            throw new Win32Exception(hr, "CreatePseudoConsole failed");
        }

        var startup = new NativeMethods.STARTUPINFOEX();
        startup.StartupInfo.cb = Marshal.SizeOf<NativeMethods.STARTUPINFOEX>();

        IntPtr attrList = IntPtr.Zero;
        IntPtr attrListSize = IntPtr.Zero;
        NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrListSize);
        attrList = Marshal.AllocHGlobal(attrListSize);
        startup.lpAttributeList = attrList;

        try
        {
            if (!NativeMethods.InitializeProcThreadAttributeList(attrList, 1, 0, ref attrListSize))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "InitializeProcThreadAttributeList failed");

            if (!NativeMethods.UpdateProcThreadAttribute(
                    attrList, 0, (IntPtr)NativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    _hPc, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateProcThreadAttribute failed");

            var cmd = new StringBuilder(commandLine);
            var pi = new NativeMethods.PROCESS_INFORMATION();
            bool created = NativeMethods.CreateProcess(
                null, cmd, IntPtr.Zero, IntPtr.Zero, false,
                NativeMethods.EXTENDED_STARTUPINFO_PRESENT,
                IntPtr.Zero, workingDirectory, ref startup, out pi);

            if (!created)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess failed");

            _hProcess = pi.hProcess;
            _hThread = pi.hThread;
            _processId = pi.dwProcessId;
        }
        finally
        {
            if (attrList != IntPtr.Zero)
            {
                NativeMethods.DeleteProcThreadAttributeList(attrList);
                Marshal.FreeHGlobal(attrList);
            }
        }

        inputRead.Dispose();
        outputWrite.Dispose();

        _inputWrite = inputWrite;
        _outputRead = outputRead;
        _inputStream = new FileStream(inputWrite, FileAccess.Write, 4096, isAsync: false);
        _outputStream = new FileStream(outputRead, FileAccess.Read, 4096, isAsync: false);

        IsRunning = true;

        try
        {
            _process = Process.GetProcessById(_processId);
            _process.EnableRaisingEvents = true;
            _process.Exited += OnProcessExited;
        }
        catch
        {
            _process = null;
            _ = WatchForExitAsync();
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        int code = 0;
        try { code = _process?.ExitCode ?? 0; } catch { }
        IsRunning = false;
        Exited?.Invoke(this, code);
    }

    private async Task WatchForExitAsync()
    {
        await Task.Run(() => NativeMethods.WaitForSingleObject(_hProcess, NativeMethods.INFINITE));
        int code = 0;
        NativeMethods.GetExitCodeProcess(_hProcess, out code);
        IsRunning = false;
        Exited?.Invoke(this, code);
    }

    public Task WriteAsync(string text)
    {
        if (_inputStream == null || !IsRunning) return Task.CompletedTask;
        var bytes = Encoding.UTF8.GetBytes(text);
        _inputStream.Write(bytes, 0, bytes.Length);
        _inputStream.Flush();
        return Task.CompletedTask;
    }

    public void Resize(short columns, short rows)
    {
        if (_hPc != IntPtr.Zero && columns > 0 && rows > 0)
        {
            NativeMethods.ResizePseudoConsole(_hPc, new NativeMethods.COORD { X = columns, Y = rows });
        }
    }

    public void Kill()
    {
        try { _process?.Kill(entireProcessTree: true); }
        catch { /* process may already be gone */ }

        if (_hProcess != IntPtr.Zero)
        {
            NativeMethods.TerminateProcess(_hProcess, 1);
        }
    }

    public void Dispose()
    {
        Kill();
        _process?.Dispose();
        _process = null;

        if (_hThread != IntPtr.Zero) { NativeMethods.CloseHandle(_hThread); _hThread = IntPtr.Zero; }
        if (_hProcess != IntPtr.Zero) { NativeMethods.CloseHandle(_hProcess); _hProcess = IntPtr.Zero; }
        if (_hPc != IntPtr.Zero) { NativeMethods.ClosePseudoConsole(_hPc); _hPc = IntPtr.Zero; }

        _inputStream?.Dispose();
        _outputStream?.Dispose();
        _inputWrite?.Dispose();
        _outputRead?.Dispose();

        IsRunning = false;
    }

    private static class NativeMethods
    {
        public const int STARTF_USESTDHANDLES = 0x00000100;
        public const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        public const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        public const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x20016;
        public const uint INFINITE = 0xFFFFFFFF;

        [StructLayout(LayoutKind.Sequential)]
        public struct COORD { public short X; public short Y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetHandleInformation(SafeFileHandle hObject, int dwMask, int dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CreateProcess(
            string? lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out int lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);
    }
}

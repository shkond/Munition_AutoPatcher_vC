using System.Runtime.InteropServices;
using System.IO;
using System.Text;

namespace MunitionAutoPatcher;

public static class DebugConsole
{
#if DEBUG
    // WinAPI: AllocConsole
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    // WinAPI: FreeConsole
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    /// <summary>
    /// Show console window for debug builds and redirect Console streams to it.
    /// </summary>
    public static void Show()
    {
        if (!AllocConsole())
        {
            return;
        }

        try
        {
            // Redirect stdout/stderr to the new console
            var stdout = new FileStream("CONOUT$", FileMode.Open, FileAccess.Write);
            var writer = new StreamWriter(stdout, Console.OutputEncoding ?? Encoding.UTF8) { AutoFlush = true };
            Console.SetOut(writer);
            Console.SetError(writer);

            // Redirect stdin to the console
            var stdin = new FileStream("CONIN$", FileMode.Open, FileAccess.Read);
            var reader = new StreamReader(stdin, Console.InputEncoding ?? Encoding.UTF8);
            Console.SetIn(reader);
        }
        catch
        {
            // Ignore redirection errors; AllocConsole still provides a visible console window.
        }
    }

    /// <summary>
    /// Hide/close the console window.
    /// </summary>
    public static void Hide()
    {
        try
        {
            // Reset Console streams to avoid using closed handles
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            Console.SetIn(TextReader.Null);
        }
        catch { }

        FreeConsole();
    }
#else
    public static void Show() { }
    public static void Hide() { }
#endif
}

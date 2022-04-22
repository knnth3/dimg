using System.Diagnostics;

namespace dimg
{
    static internal class ConsoleHandle
    {
        static internal bool RunCommand(string command)
        {
            bool success = false;
            var platformId = Environment.OSVersion.Platform;
            switch (platformId)
            {
                case PlatformID.Win32NT:
                    success = RunExternalExe("cmd.exe", $"/C {command}");
                    break;
                default:
                    Console.WriteLine($"Platform: '{platformId}' is not supported by dimg.");
                    break;
            }

            return success;
        }

        private static bool RunExternalExe(string filename, string arguments = null)
        {
            var process = new Process();

            process.StartInfo.FileName = filename;
            if (!string.IsNullOrEmpty(arguments))
            {
                process.StartInfo.Arguments = arguments;
            }

            process.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            process.StartInfo.UseShellExecute = false;

            process.StartInfo.RedirectStandardError = false;
            process.StartInfo.RedirectStandardOutput = false;
            try
            {
                process.Start();
                process.WaitForExit();
                Console.WriteLine();
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(e.Message);
                Console.ResetColor();
                return false;
            }

            return (process.ExitCode == 0);
        }
    }
}

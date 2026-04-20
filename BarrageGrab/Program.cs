namespace BarrageGrab
{
    internal static class Program
    {
        private static readonly string[] HeadlessArgs = ["--headless", "--no-ui", "-n"];
        private static readonly string[] UiArgs = ["--ui", "-u"];
        private static readonly string[] HeadlessConsoleArgs = ["--headless-console", "--console", "-c"];

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            //注册服务
            ServiceRegistrar.BuildServices();

            if (ShouldRunHeadless(args))
            {
                if (ShouldShowHeadlessConsole(args))
                {
                    EnsureConsoleForHeadless();
                    Console.WriteLine("[BarrageGrab] running in headless mode.");
                    Console.WriteLine("[BarrageGrab] press Ctrl+C to stop.");
                }

                // 无界面模式：保持 WinForms 消息循环，服务持续运行
                Application.Run();
                return;
            }

            // 界面模式：运行主窗体
            Application.Run(ApplicationRuntime.MainForm);
        }

        private static bool ShouldRunHeadless(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return false;
            }

            foreach (string arg in args)
            {
                if (UiArgs.Contains(arg, StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            foreach (string arg in args)
            {
                if (HeadlessArgs.Contains(arg, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldShowHeadlessConsole(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return false;
            }

            foreach (string arg in args)
            {
                if (HeadlessConsoleArgs.Contains(arg, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureConsoleForHeadless()
        {
            if (GetConsoleWindow() != IntPtr.Zero)
            {
                return;
            }

            AllocConsole();
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
    }
}
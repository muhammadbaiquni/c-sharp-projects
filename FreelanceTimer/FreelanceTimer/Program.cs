namespace Waktu
{
    internal static class Program
    {
        // Mutex to ensure only one instance runs
        private static Mutex mutex = null;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            const string appName = "FreelancerTimer";
            mutex = new Mutex(true, appName, out bool isNewInstance);

            if (isNewInstance)
            {
                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                ApplicationConfiguration.Initialize();
                Application.Run(new formTimer());
            }
            else
            {
                Application.Exit();
            }
        }
    }
}
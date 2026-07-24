#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace RemoteDesktopServer.Services
{
    public static class FontInstallerService
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

        private const uint FR_PRIVATE = 0x10;

        public static void InstallFontFiles()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string fontsDir = Path.Combine(baseDir, "Assets", "Fonts");
                Directory.CreateDirectory(fontsDir);

                string winFontsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Windows", "Fonts");
                
                if (!Directory.Exists(winFontsDir))
                {
                    Directory.CreateDirectory(winFontsDir);
                }

                string[] fontFiles = new[] { "SF-Pro-Display-Regular.otf", "SF-Pro-Display-Semibold.otf", "SF-Pro-Display-Bold.otf" };

                foreach (var fontFile in fontFiles)
                {
                    string sourcePath = Path.Combine(fontsDir, fontFile);
                    string userFontPath = Path.Combine(winFontsDir, fontFile);

                    if (File.Exists(sourcePath))
                    {
                        if (!File.Exists(userFontPath))
                        {
                            try
                            {
                                File.Copy(sourcePath, userFontPath, true);
                            }
                            catch { }
                        }
                        AddFontResourceEx(sourcePath, FR_PRIVATE, IntPtr.Zero);
                        if (File.Exists(userFontPath))
                        {
                            AddFontResourceEx(userFontPath, FR_PRIVATE, IntPtr.Zero);
                        }
                    }
                    else if (File.Exists(userFontPath))
                    {
                        AddFontResourceEx(userFontPath, FR_PRIVATE, IntPtr.Zero);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FontInstaller error: {ex.Message}");
            }
        }
    }
}

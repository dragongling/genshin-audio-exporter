using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace genshin_audio_exporter
{
    public class PckToWem
    {
        private readonly string quickBmsPath, waveScanBmsPath;

        public PckToWem(string quickBmsPath, string waveScanBmsPath)
        {
            this.quickBmsPath = quickBmsPath;
            this.waveScanBmsPath = waveScanBmsPath;
        }

        public void StartPckToWem(string inputFile, string outputDirectory)
        {
            Process pckToWemProcess;
            Directory.CreateDirectory(outputDirectory);
            var startInfo = new ProcessStartInfo(quickBmsPath)
            {
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Arguments = $"\"{waveScanBmsPath}\" \"{inputFile}\" \"{outputDirectory}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            using (pckToWemProcess = new Process())
            {
                try
                {
                    pckToWemProcess.StartInfo = startInfo;
                    pckToWemProcess.Start();
                    pckToWemProcess.WaitForExit();
                }
                catch (Exception ex)
                {
                    LogManager.GetCurrentClassLogger().Error($"Could not start quickbms.exe process:\n\n{ex.Message}\n\nIn case of a permissions issue try running this program as Administrator.");
                    MessageBox.Show($"Could not start quickbms.exe process:\n\n{ex.Message}\n\nIn case of a permissions issue try running this program as Administrator.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}

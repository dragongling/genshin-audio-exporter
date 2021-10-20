using genshin_audio_exporter.Classes;
using Microsoft.WindowsAPICodePack.Dialogs;
using NLog;
using NLog.Config;
using NLog.Targets.Wrappers;
using NLog.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace genshin_audio_exporter
{
    public partial class MainForm : Form
    {
        private bool doUpdateFormatSettings = false;
        private bool isBusy = false;
        private readonly GenshinExporter exporter = new GenshinExporter();
        private readonly Logger logger = LogManager.GetCurrentClassLogger();
        private CancellationTokenSource exportTokenSource;

        public MainForm()
        {
            InitializeComponent();
            OutputDirTextBox.Text = Properties.Settings.Default.OutputDirectory;
            FormatWavCheckBox.Checked = Properties.Settings.Default.CreateWav;
            FormatMp3CheckBox.Checked = Properties.Settings.Default.CreateMp3;
            FormatOggCheckBox.Checked = Properties.Settings.Default.CreateOgg;
            FormatFlacCheckBox.Checked = Properties.Settings.Default.CreateFlac;
            doUpdateFormatSettings = true;
            UpdateCanExportStatus();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            RichTextBoxTarget target = new RichTextBoxTarget
            {
                Name = "RichTextBox",
                Layout = "${message}",
                ControlName = "StatusTextBox",
                FormName = "MainForm",
                AutoScroll = true,
                MaxLines = 10000,
                UseDefaultRowColoringRules = false
            };
            target.RowColoringRules.Add(
                new RichTextBoxRowColoringRule(
                    "level == LogLevel.Trace", // condition
                    "DarkGray", // font color
                    "Control", // background color
                    FontStyle.Regular
                )
            );
            target.RowColoringRules.Add(new RichTextBoxRowColoringRule("level == LogLevel.Debug", "Gray", "Control"));
            target.RowColoringRules.Add(new RichTextBoxRowColoringRule("level == LogLevel.Info", "ControlText", "Control"));
            target.RowColoringRules.Add(new RichTextBoxRowColoringRule("level == LogLevel.Warn", "DarkRed", "Control"));
            target.RowColoringRules.Add(new RichTextBoxRowColoringRule("level == LogLevel.Error", "White", "DarkRed", FontStyle.Bold));
            target.RowColoringRules.Add(new RichTextBoxRowColoringRule("level == LogLevel.Fatal", "Yellow", "DarkRed", FontStyle.Bold));

            AsyncTargetWrapper asyncWrapper = new AsyncTargetWrapper
            {
                Name = "AsyncRichTextBox",
                WrappedTarget = target
            };
            SimpleConfigurator.ConfigureForTargetLogging(asyncWrapper, LogLevel.Info);
        }

        public void WriteStatus(string text, bool prefix = true)
        {
            logger.Info($"{((text.Length > 0 && prefix) ? "> " + text : "  " + text)}");
        }

        private void BrowsePckFiles(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "PCK files (*.pck)|*.pck",
                Multiselect = true
            };
            var ofdResult = ofd.ShowDialog();
            if (ofdResult == DialogResult.OK)
            {
                AppVariables.PckFiles = ofd.FileNames.ToList();
                PckFileDirTextBox.Text = string.Join(" ", ofd.SafeFileNames);
            }
            UpdateCanExportStatus();
        }

        private void BrowseOutputFolder(object sender, EventArgs e)
        {
            CommonOpenFileDialog fbd = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };
            CommonFileDialogResult fbdResult = fbd.ShowDialog();
            if (fbdResult == CommonFileDialogResult.Ok)
            {
                AppVariables.OutputDir = fbd.FileName;
                OutputDirTextBox.Text = fbd.FileName;
            }
            UpdateCanExportStatus();
        }

        private void UpdateAudioFormatStatus(object sender, EventArgs e)
        {
            if (doUpdateFormatSettings)
                UpdateCanExportStatus();
        }

        private void UpdateCanExportStatus()
        {
            bool canExport = true;

            AppVariables.ExportFormats["wav"] = FormatWavCheckBox.Checked;
            AppVariables.ExportFormats["mp3"] = FormatMp3CheckBox.Checked;
            AppVariables.ExportFormats["ogg"] = FormatOggCheckBox.Checked;
            AppVariables.ExportFormats["flac"] = FormatFlacCheckBox.Checked;

            if(!AppVariables.ExportFormats.Values.Any(fmtChecked => fmtChecked == true))
                canExport = false;
            if (string.IsNullOrEmpty(PckFileDirTextBox.Text) || !Directory.Exists(OutputDirTextBox.Text))
                canExport = false;
            if (canExport)
            {
                Properties.Settings.Default.CreateWav = AppVariables.ExportFormats["wav"];
                Properties.Settings.Default.CreateMp3 = AppVariables.ExportFormats["mp3"];
                Properties.Settings.Default.CreateOgg = AppVariables.ExportFormats["ogg"];
                Properties.Settings.Default.CreateFlac = AppVariables.ExportFormats["flac"];

                AppVariables.OutputDir = OutputDirTextBox.Text;
                Properties.Settings.Default.OutputDirectory = OutputDirTextBox.Text;
                ExportButton.Enabled = canExport;
                AppVariables.UpdateProcessingFolder();
            }
            else
                ExportButton.Enabled = false;
        }

        private async void ExportOrAbort(object sender, EventArgs e)
        {   
            if (!isBusy)
            {
                isBusy = true;
                try
                {
                    exportTokenSource = new CancellationTokenSource();
                    await Export(exportTokenSource.Token);
                    WriteStatus("");
                    WriteStatus($"{exporter.exportedAudioFiles} audio files have been exported ({AppVariables.WavFiles.Count} unique sounds)", prefix: false);
                    WriteStatus("");
                }
                catch (OperationCanceledException)
                {
                    WriteStatus("");
                    WriteStatus($"Task has been aborted, {exporter.exportedAudioFiles} audio files were exported", prefix: false);
                    WriteStatus("");
                }
                finally
                {
                    CurrentExportProgressBar.Value = 0;
                    OverallExportProgressBar.Value = 0;
                    OverallExportProgressBar.Style = ProgressBarStyle.Blocks;
                    ExportButton.Enabled = true;
                    ExportButton.Text = "Export";
                    SettingsGroupBox.Enabled = true;
                    ClearTempDirectories();
                    isBusy = false;
                    exporter.exportedAudioFiles = 0;
                }
            }
            else
            {
                exportTokenSource.Cancel();
                ExportButton.Text = "Aborting...";
                ExportButton.Enabled = false;
                return;
            }
            Application.DoEvents();
        }

        private async Task Export(CancellationToken? ct)
        {
            ExportButton.Text = "Abort";
            SettingsGroupBox.Enabled = false;
            Directory.CreateDirectory(AppVariables.ProcessingDir);
            Directory.CreateDirectory(AppVariables.LibsDir);
            ClearTempDirectories();
            StatusTextBox.Clear();
            if (!AppResources.IsUnpacked)
            {
                WriteStatus("Unpacking libraries", prefix: false);
                WriteStatus("");
                await Task.Run(() =>
                {
                    AppResources.UnpackResources();
                });
            }
            OverallExportProgressBar.Value = 0;
            OverallExportProgressBar.Style = ProgressBarStyle.Marquee;
            CurrentExportProgressBar.Value = 0;
            CurrentExportProgressBar.Maximum = 0;
            int overallIndex = 0;
            CheckMissingFiles();
            if (AppVariables.PckFiles.Count == 0)
            {
                WriteStatus("No .PCK files to process");
                return;
            }
            Directory.CreateDirectory(AppVariables.ProcessingDir);
            Directory.CreateDirectory(Path.Combine(AppVariables.ProcessingDir, "wem"));
            foreach (var format in AppVariables.ExportFormats)
            {
                if (format.Value)
                {
                    Directory.CreateDirectory(Path.Combine(AppVariables.ProcessingDir, format.Key));
                }
            }

            ct?.ThrowIfCancellationRequested();
            IProgress<int> progress = new Progress<int>(value =>
            {
                CurrentExportProgressBar.Maximum = AppVariables.PckFiles.Count;
                CurrentExportProgressBar.Value = value;
            });
            await exporter.ExportPcksToWem(progress, ct);

            ct?.ThrowIfCancellationRequested();
            AppVariables.WemFiles = Directory.GetFiles(Path.Combine(AppVariables.ProcessingDir, "wem"), "*.wem").ToList();
            OverallExportProgressBar.Style = ProgressBarStyle.Blocks;
            int overallMaximum = AppVariables.WemFiles.Count;
            foreach (CheckBox formatCheckBox in SettingsGroupBox.Controls.OfType<CheckBox>())
            {
                if (formatCheckBox.Checked)
                    overallMaximum += AppVariables.WemFiles.Count;
            }
            OverallExportProgressBar.Maximum = overallMaximum;
            progress = new Progress<int>(value =>
            {
                CurrentExportProgressBar.Maximum = AppVariables.WemFiles.Count;
                CurrentExportProgressBar.Value = value;
                OverallExportProgressBar.Value = overallIndex;
            });
            await exporter.ExportWemsToWavs(overallIndex, progress, ct);

            AppVariables.WavFiles = Directory.GetFiles(Path.Combine(AppVariables.ProcessingDir, "wav"), "*.wav").ToList();

            foreach (var format in AppVariables.ExportFormats)
            {
                ct?.ThrowIfCancellationRequested();
                if (format.Value)
                {
                    progress = new Progress<int>(value =>
                    {
                        CurrentExportProgressBar.Maximum = AppVariables.WavFiles.Count;
                        CurrentExportProgressBar.Value = value;
                        OverallExportProgressBar.Value = overallIndex;
                    });
                    await exporter.ExportAudioFormat(format.Key, progress, ct);
                }
            }
        }

        private void CheckMissingFiles()
        {
            List<string> missingFiles = new List<string>();
            foreach (string filePath in AppVariables.PckFiles)
            {
                if (!File.Exists(filePath))
                {
                    missingFiles.Add(filePath);
                }
            }
            if (missingFiles.Count == AppVariables.PckFiles.Count)
            {
                MessageBox.Show(this, "All .PCK files are missing", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                PckFileDirTextBox.Clear();
            }
            else if (missingFiles.Count > 0)
            {
                MessageBox.Show(this, "The following .PCK files are missing:\n\n" + string.Join("\n", missingFiles), "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            foreach (string missingFile in missingFiles)
            {
                AppVariables.PckFiles.Remove(missingFile);
            }
        }

        private static void ClearTempDirectories()
        {
            while (Process.GetProcessesByName("quickbms").Length>0)
                foreach (var process in Process.GetProcessesByName("quickbms"))
                    process.Kill();

            while (Process.GetProcessesByName("vgmstream-cli").Length>0)
                foreach (var process in Process.GetProcessesByName("vgmstream-cli"))
                    process.Kill();

            while (Process.GetProcessesByName("ffmpeg").Length>0)
                foreach (var process in Process.GetProcessesByName("ffmpeg"))
                    process.Kill();

            DirectoryInfo processingDir = new DirectoryInfo(AppVariables.ProcessingDir);
            foreach (FileInfo file in processingDir.GetFiles()) file.Delete();
            foreach (DirectoryInfo subDirectory in processingDir.GetDirectories()) subDirectory.Delete(true);
        }

        private void CloseApplication(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Properties.Settings.Default.Save();

            if (isBusy)
                ExportOrAbort(null, null);

            try
            {
                Directory.Delete(AppVariables.LibsDir, true);
                Directory.Delete(AppVariables.ProcessingDir, true);
            }
            catch { }
            Application.DoEvents();
            Environment.Exit(0);
        }        
    }
}
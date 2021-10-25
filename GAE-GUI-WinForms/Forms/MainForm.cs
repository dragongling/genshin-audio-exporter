using GenshinAudioExportLib;
using Microsoft.WindowsAPICodePack.Dialogs;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Targets.Wrappers;
using NLog.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace genshin_audio_exporter
{
    public partial class MainForm : Form
    {
        private bool doUpdateFormatSettings = false;
        private bool isBusy = false;

        private static readonly string LibsDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "libs");
        private readonly GenshinExporter exporter = new GenshinExporter(LibsDir);
        private readonly Logger logger = LogManager.GetCurrentClassLogger();
        private CancellationTokenSource exportTokenSource;

        public static Dictionary<string, bool> ExportFormats = new Dictionary<string, bool> {
            { "wem", false },
            { "wav", false },
            { "mp3", false },
            { "ogg", false },
            { "flac", false }
        };

        List<string> PckFiles;
        string OutputDir;

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
            LayoutRenderer.Register("prefix", (logEvent) => logEvent.Level == LogLevel.Debug ? "> " : "  ");
            RichTextBoxTarget target = new RichTextBoxTarget
            {
                Name = "RichTextBox",
                Layout = "${prefix}${message}",
                ControlName = "StatusTextBox",
                FormName = "MainForm",
                AutoScroll = true,
                MaxLines = 10000,
                UseDefaultRowColoringRules = false
            };
            target.RowColoringRules.Add(new RichTextBoxRowColoringRule("level == LogLevel.Warn", "DarkRed", "Control"));
            target.RowColoringRules.Add(new RichTextBoxRowColoringRule("level == LogLevel.Error", "White", "DarkRed", FontStyle.Bold));
            target.RowColoringRules.Add(new RichTextBoxRowColoringRule("level == LogLevel.Fatal", "Yellow", "DarkRed", FontStyle.Bold));

            AsyncTargetWrapper asyncWrapper = new AsyncTargetWrapper
            {
                Name = "AsyncRichTextBox",
                WrappedTarget = target
            };
            SimpleConfigurator.ConfigureForTargetLogging(asyncWrapper, LogLevel.Debug);
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
                PckFiles = ofd.FileNames.ToList();
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
                OutputDir = fbd.FileName;
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

            ExportFormats["wav"] = FormatWavCheckBox.Checked;
            ExportFormats["mp3"] = FormatMp3CheckBox.Checked;
            ExportFormats["ogg"] = FormatOggCheckBox.Checked;
            ExportFormats["flac"] = FormatFlacCheckBox.Checked;

            if(!ExportFormats.Values.Any(fmtChecked => fmtChecked == true))
                canExport = false;
            if (string.IsNullOrEmpty(PckFileDirTextBox.Text) || !Directory.Exists(OutputDirTextBox.Text))
                canExport = false;
            if (canExport)
            {
                Properties.Settings.Default.CreateWav = ExportFormats["wav"];
                Properties.Settings.Default.CreateMp3 = ExportFormats["mp3"];
                Properties.Settings.Default.CreateOgg = ExportFormats["ogg"];
                Properties.Settings.Default.CreateFlac = ExportFormats["flac"];

                OutputDir = OutputDirTextBox.Text;
                Properties.Settings.Default.OutputDirectory = OutputDirTextBox.Text;
                ExportButton.Enabled = canExport;
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
                    exporter.CleanFromMissingFiles(ref PckFiles);
                    if (PckFiles.Count == 0)
                    {
                        logger.Warn("Task has been aborted, no .PCK files to process");
                        PckFileDirTextBox.Clear();
                        return;
                    }
                    exportTokenSource = new CancellationTokenSource();
                    int wavCount = await Export(
                        PckFiles,
                        OutputDir,
                        GetFormatsToExport(),
                        exportTokenSource.Token);
                    logger.Info("");
                    logger.Info($"{exporter.AudioFilesExported} audio files have been exported ({wavCount} unique sounds)");
                    logger.Info("");
                }
                catch (OperationCanceledException)
                {
                    logger.Info("");
                    logger.Info($"Task has been aborted, {exporter.AudioFilesExported} audio files were exported");
                    logger.Info("");
                    exporter.KillProcesses();
                }
                catch(Exception ex)
                {
                    logger.Error(ex.Message);
                }
                finally
                {
                    CurrentExportProgressBar.Value = 0;
                    OverallExportProgressBar.Value = 0;
                    OverallExportProgressBar.Style = ProgressBarStyle.Blocks;
                    ExportButton.Enabled = true;
                    ExportButton.Text = "Export";
                    SettingsGroupBox.Enabled = true;
                    exporter.KillProcesses();
                    exporter.ClearTempDirectories();
                    isBusy = false;
                    exporter.AudioFilesExported = 0;
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

        private List<string> GetFormatsToExport()
        {
            return ExportFormats.Where(fmt => fmt.Value == true).Select(x => x.Key).ToList();
        }

        private async Task<int> Export(List<string> PckFiles, string OutputDir, ICollection<string> ExportFormats, CancellationToken? ct)
        {
            ExportButton.Text = "Abort";
            SettingsGroupBox.Enabled = false;
            StatusTextBox.Clear();
            OverallExportProgressBar.Value = 0;
            OverallExportProgressBar.Style = ProgressBarStyle.Marquee;
            CurrentExportProgressBar.Value = 0;
            CurrentExportProgressBar.Maximum = 0;

            exporter.OutputDir = OutputDir;
            exporter.ProcessingDir = Path.Combine(OutputDir, "processing");
            Directory.CreateDirectory(exporter.ProcessingDir);
            exporter.CancelToken = ct;
            exporter.KillProcesses();
            await UnpackLibs();

            ct?.ThrowIfCancellationRequested();

            exporter.progress = new Progress<int>(value =>
            {
                CurrentExportProgressBar.Maximum = PckFiles.Count;
                CurrentExportProgressBar.Value = value;
            });
            
            await exporter.ExportPcksToWem(PckFiles);

            ct?.ThrowIfCancellationRequested();

            List<string> WemFiles = Directory.GetFiles(Path.Combine(exporter.ProcessingDir, "wem"), "*.wem").ToList();
            int overallIndex = 0;

            exporter.progress = new Progress<int>(value =>
            {
                CurrentExportProgressBar.Maximum = WemFiles.Count;
                CurrentExportProgressBar.Value = value;
                OverallExportProgressBar.Style = ProgressBarStyle.Blocks;
                OverallExportProgressBar.Maximum = WemFiles.Count * (ExportFormats.Count + 1);
                OverallExportProgressBar.Value = overallIndex;
            });

            int wavCount = await exporter.ExportWemsToWavs(overallIndex);

            List<string> WavFiles = Directory.GetFiles(Path.Combine(exporter.ProcessingDir, "wav"), "*.wav").ToList();
            foreach (var format in ExportFormats)
            {
                ct?.ThrowIfCancellationRequested();
                exporter.progress = new Progress<int>(value =>
                {
                    CurrentExportProgressBar.Maximum = WavFiles.Count;
                    CurrentExportProgressBar.Value = value;
                    OverallExportProgressBar.Value = overallIndex;
                });
                await exporter.ExportAudioFormat(format);
            }

            return wavCount;
        }

        public async Task UnpackLibs()
        {
            Directory.CreateDirectory(LibsDir);
            if (!AppResources.IsUnpacked)
            {
                logger.Info("Unpacking libraries");
                logger.Info("");
                await Task.Run(() =>
                {
                    AppResources.UnpackResources();
                });
            }
        }

        private void CloseApplication(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Properties.Settings.Default.Save();

            if (isBusy)
                ExportOrAbort(null, null);

            exporter.Dispose();
            Application.DoEvents();
            Environment.Exit(0);
        }        
    }
}
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace genshin_audio_exporter
{
    public partial class MainForm : Form
    {
        private readonly bool _doUpdateFormatSettings;
        private bool _isBusy;

        private static readonly string LibsDir = Path.Combine(Program.GetAppLocation(), "libs");
        private readonly GenshinExporter _exporter = new GenshinExporter(LibsDir);
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private CancellationTokenSource _exportTokenSource;

        protected static readonly Dictionary<string, bool> ExportFormats = new Dictionary<string, bool> {
            { "wem", false },
            { "wav", false },
            { "mp3", false },
            { "ogg", false },
            { "flac", false }
        };

        private List<string> _pckFiles;
        private string _outputDir;

        public MainForm()
        {
            InitializeComponent();
            OutputDirTextBox.Text = Properties.Settings.Default.OutputDirectory;
            FormatWavCheckBox.Checked = Properties.Settings.Default.CreateWav;
            FormatMp3CheckBox.Checked = Properties.Settings.Default.CreateMp3;
            FormatOggCheckBox.Checked = Properties.Settings.Default.CreateOgg;
            FormatFlacCheckBox.Checked = Properties.Settings.Default.CreateFlac;
            _doUpdateFormatSettings = true;
            UpdateCanExportStatus();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            LayoutRenderer.Register("prefix", (logEvent) => logEvent.Level == LogLevel.Debug ? "> " : "  ");
            var target = new RichTextBoxTarget
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

            var asyncWrapper = new AsyncTargetWrapper
            {
                Name = "AsyncRichTextBox",
                WrappedTarget = target
            };
            SimpleConfigurator.ConfigureForTargetLogging(asyncWrapper, LogLevel.Debug);
        }

        private void BrowsePckFiles(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "PCK files (*.pck)|*.pck",
                Multiselect = true
            };
            var ofdResult = ofd.ShowDialog();
            if (ofdResult == DialogResult.OK)
            {
                _pckFiles = ofd.FileNames.ToList();
                PckFileDirTextBox.Text = string.Join(" ", ofd.SafeFileNames);
            }
            UpdateCanExportStatus();
        }

        private void BrowseOutputFolder(object sender, EventArgs e)
        {
            var fbd = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };
            var fbdResult = fbd.ShowDialog();
            if (fbdResult == CommonFileDialogResult.Ok)
            {
                _outputDir = fbd.FileName;
                OutputDirTextBox.Text = fbd.FileName;
            }
            UpdateCanExportStatus();
        }

        private void UpdateAudioFormatStatus(object sender, EventArgs e)
        {
            if (_doUpdateFormatSettings)
                UpdateCanExportStatus();
        }

        private void UpdateCanExportStatus()
        {
            var canExport = true;

            ExportFormats["wav"] = FormatWavCheckBox.Checked;
            ExportFormats["mp3"] = FormatMp3CheckBox.Checked;
            ExportFormats["ogg"] = FormatOggCheckBox.Checked;
            ExportFormats["flac"] = FormatFlacCheckBox.Checked;

            if(ExportFormats.Values.All(fmtChecked => fmtChecked != true))
                canExport = false;
            if (string.IsNullOrEmpty(PckFileDirTextBox.Text) || !Directory.Exists(OutputDirTextBox.Text))
                canExport = false;
            if (canExport)
            {
                Properties.Settings.Default.CreateWav = ExportFormats["wav"];
                Properties.Settings.Default.CreateMp3 = ExportFormats["mp3"];
                Properties.Settings.Default.CreateOgg = ExportFormats["ogg"];
                Properties.Settings.Default.CreateFlac = ExportFormats["flac"];

                _outputDir = OutputDirTextBox.Text;
                Properties.Settings.Default.OutputDirectory = OutputDirTextBox.Text;
                
            }
            ExportButton.Enabled = canExport;
        }

        private async void ExportOrAbort(object sender, EventArgs e)
        {   
            if (!_isBusy)
            {
                _isBusy = true;
                try
                {
                    _exporter.CleanFromMissingFiles(ref _pckFiles);
                    if (_pckFiles.Count == 0)
                    {
                        _logger.Warn("Task has been aborted, no .PCK files to process");
                        PckFileDirTextBox.Clear();
                        return;
                    }
                    _exportTokenSource = new CancellationTokenSource();
                    var wavCount = await Export(
                        _pckFiles,
                        _outputDir,
                        GetFormatsToExport(),
                        _exportTokenSource.Token);
                    _logger.Info("");
                    _logger.Info($"{_exporter.AudioFilesExported} audio files have been exported ({wavCount} unique sounds)");
                    _logger.Info("");
                }
                catch (OperationCanceledException)
                {
                    _logger.Info("");
                    _logger.Info($"Task has been aborted, {_exporter.AudioFilesExported} audio files were exported");
                    _logger.Info("");
                    _exporter.KillProcesses();
                }
                catch(Exception ex)
                {
                    _logger.Error(ex.Message);
                }
                finally
                {
                    CurrentExportProgressBar.Value = 0;
                    OverallExportProgressBar.Value = 0;
                    OverallExportProgressBar.Style = ProgressBarStyle.Blocks;
                    ExportButton.Enabled = true;
                    ExportButton.Text = "Export";
                    SettingsGroupBox.Enabled = true;
                    _exporter.KillProcesses();
                    _exporter.ClearTempDirectories();
                    _isBusy = false;
                    _exporter.AudioFilesExported = 0;
                }
            }
            else
            {
                _exportTokenSource.Cancel();
                ExportButton.Text = "Aborting...";
                ExportButton.Enabled = false;
                return;
            }
            Application.DoEvents();
        }

        private static List<string> GetFormatsToExport()
        {
            return ExportFormats.Where(fmt => fmt.Value).Select(x => x.Key).ToList();
        }

        private async Task<int> Export(
            ICollection<string> pckFiles, 
            string outputDir, 
            ICollection<string> exportFormats, 
            CancellationToken? ct)
        {
            ExportButton.Text = "Abort";
            SettingsGroupBox.Enabled = false;
            StatusTextBox.Clear();
            OverallExportProgressBar.Value = 0;
            OverallExportProgressBar.Style = ProgressBarStyle.Marquee;
            CurrentExportProgressBar.Value = 0;
            CurrentExportProgressBar.Maximum = 0;

            _exporter.OutputDir = outputDir;
            _exporter.ProcessingDir = Path.Combine(outputDir, "processing");
            Directory.CreateDirectory(_exporter.ProcessingDir);
            _exporter.CancelToken = ct;
            _exporter.KillProcesses();
            await UnpackLibs();

            ct?.ThrowIfCancellationRequested();

            _exporter.Progress = new Progress<int>(value =>
            {
                CurrentExportProgressBar.Maximum = pckFiles.Count;
                CurrentExportProgressBar.Value = value;
            });
            
            await _exporter.ExportPcksToWem(pckFiles);

            ct?.ThrowIfCancellationRequested();

            var wemFiles = Directory.GetFiles(Path.Combine(_exporter.ProcessingDir, "wem"), "*.wem").ToList();
            const int overallIndex = 0;

            _exporter.Progress = new Progress<int>(value =>
            {
                CurrentExportProgressBar.Maximum = wemFiles.Count;
                CurrentExportProgressBar.Value = value;
                OverallExportProgressBar.Style = ProgressBarStyle.Blocks;
                OverallExportProgressBar.Maximum = wemFiles.Count * (exportFormats.Count + 1);
                OverallExportProgressBar.Value = overallIndex;
            });

            var wavCount = await _exporter.ExportWemsToWavs(overallIndex);

            var wavFiles = Directory.GetFiles(Path.Combine(_exporter.ProcessingDir, "wav"), "*.wav").ToList();
            foreach (var format in exportFormats)
            {
                ct?.ThrowIfCancellationRequested();
                _exporter.Progress = new Progress<int>(value =>
                {
                    CurrentExportProgressBar.Maximum = wavFiles.Count;
                    CurrentExportProgressBar.Value = value;
                    OverallExportProgressBar.Value = overallIndex;
                });
                await _exporter.ExportAudioFormat(format);
            }

            return wavCount;
        }

        public async Task UnpackLibs()
        {
            Directory.CreateDirectory(LibsDir);
            if (!AppResources.IsUnpacked)
            {
                _logger.Info("Unpacking libraries");
                _logger.Info("");
                await Task.Run(AppResources.UnpackResources);
            }
        }

        private void CloseApplication(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Properties.Settings.Default.Save();

            if (_isBusy)
                ExportOrAbort(null, null);

            _exporter.Dispose();
            Application.DoEvents();
            Environment.Exit(0);
        }        
    }
}
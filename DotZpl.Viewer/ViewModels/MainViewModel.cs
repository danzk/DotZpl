using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Threading;

using Microsoft.Win32;

using DotZpl.Rendering;

// BinaryKits.Zpl.Viewer is the Skia renderer's namespace; PrinterStorage / ZplAnalyzer are non-
// colliding utilities, but ZplElementDrawer / DrawerOptions / FontManager collide with our types.
// Alias just what we need so the unqualified colliding names below resolve to DotZpl.
using PrinterStorage = BinaryKits.Zpl.Viewer.PrinterStorage;
using ZplAnalyzer = BinaryKits.Zpl.Viewer.ZplAnalyzer;

namespace DotZpl.Viewer.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private const double Inch2Mm = 25.4;

        // Debounces live rendering so we re-render shortly after the user stops typing, not per keystroke.
        private readonly DispatcherTimer _renderTimer;

        public MainViewModel()
        {
            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _renderTimer.Tick += (_, _) => RenderNow();

            RenderCommand = new RelayCommand(RenderNow);
            SavePngCommand = new RelayCommand(SavePng);
            SaveZplCommand = new RelayCommand(SaveZpl);
            ResetTransformCommand = new RelayCommand(ResetTransform);

            LoadLabels();

            // Show something on startup (mirrors a friendlier default than the empty web page).
            LabelItem? first = ExampleLabels.FirstOrDefault() ?? TestLabels.FirstOrDefault();
            if (first != null)
            {
                LoadLabel(first);
            }
        }

        #region Label browser

        public ObservableCollection<LabelItem> TestLabels { get; } = new();
        public ObservableCollection<LabelItem> ExampleLabels { get; } = new();

        private LabelItem? _selectedTestLabel;
        public LabelItem? SelectedTestLabel
        {
            get => _selectedTestLabel;
            set { if (SetProperty(ref _selectedTestLabel, value) && value != null) LoadLabel(value); }
        }

        private LabelItem? _selectedExampleLabel;
        public LabelItem? SelectedExampleLabel
        {
            get => _selectedExampleLabel;
            set { if (SetProperty(ref _selectedExampleLabel, value) && value != null) LoadLabel(value); }
        }

        #endregion

        #region Editor / parameters

        private string _zplText = string.Empty;
        public string ZplText { get => _zplText; set { if (SetProperty(ref _zplText, value)) ScheduleRender(); } }

        public IReadOnlyList<LabelFormat> LabelFormats { get; } = new[]
        {
            new LabelFormat(101.6, 152.4),
            new LabelFormat(54, 86),
        };

        private LabelFormat? _selectedFormat;
        public LabelFormat? SelectedFormat
        {
            get => _selectedFormat;
            set
            {
                if (SetProperty(ref _selectedFormat, value) && value != null)
                {
                    LabelWidth = value.Width;
                    LabelHeight = value.Height;
                    RenderNow();
                }
            }
        }

        private double _labelWidth = 101.6;
        public double LabelWidth
        {
            get => _labelWidth;
            set { if (SetProperty(ref _labelWidth, value)) { OnPropertyChanged(nameof(LabelWidthInch)); ScheduleRender(); } }
        }

        private double _labelHeight = 152.4;
        public double LabelHeight
        {
            get => _labelHeight;
            set { if (SetProperty(ref _labelHeight, value)) { OnPropertyChanged(nameof(LabelHeightInch)); ScheduleRender(); } }
        }

        private int _printDensityDpmm = 8;
        public int PrintDensityDpmm
        {
            get => _printDensityDpmm;
            set { if (SetProperty(ref _printDensityDpmm, value)) { OnPropertyChanged(nameof(Dpi)); ScheduleRender(); } }
        }

        public double LabelWidthInch => Math.Round(LabelWidth / Inch2Mm, 1);
        public double LabelHeightInch => Math.Round(LabelHeight / Inch2Mm, 1);
        public int Dpi => (int)Math.Round(PrintDensityDpmm * Inch2Mm);

        public IReadOnlyList<double> RotationAngles { get; } = new double[] { 0, 90, 180, 270 };

        private double _rotationAngle;
        public double RotationAngle { get => _rotationAngle; set => SetProperty(ref _rotationAngle, value); }

        private double _offsetX;
        public double OffsetX { get => _offsetX; set => SetProperty(ref _offsetX, value); }

        private double _offsetY;
        public double OffsetY { get => _offsetY; set => SetProperty(ref _offsetY, value); }

        #endregion

        #region Preview (what the control renders) + diagnostics

        private string? _previewZpl;
        public string? PreviewZpl { get => _previewZpl; set => SetProperty(ref _previewZpl, value); }

        private double _previewWidth = 101.6;
        public double PreviewWidth { get => _previewWidth; set => SetProperty(ref _previewWidth, value); }

        private double _previewHeight = 152.4;
        public double PreviewHeight { get => _previewHeight; set => SetProperty(ref _previewHeight, value); }

        private int _previewDpmm = 8;
        public int PreviewDpmm { get => _previewDpmm; set => SetProperty(ref _previewDpmm, value); }

        public ObservableCollection<string> UnknownCommands { get; } = new();
        public bool HasUnknownCommands => UnknownCommands.Count > 0;

        private string? _renderError;
        public string? RenderError { get => _renderError; set { if (SetProperty(ref _renderError, value)) OnPropertyChanged(nameof(HasRenderError)); } }
        public bool HasRenderError => !string.IsNullOrEmpty(RenderError);

        #endregion

        #region Commands

        public ICommand RenderCommand { get; }
        public ICommand SavePngCommand { get; }
        public ICommand SaveZplCommand { get; }
        public ICommand ResetTransformCommand { get; }

        #endregion

        private void ResetTransform()
        {
            RotationAngle = 0;
            OffsetX = 0;
            OffsetY = 0;
        }

        private void LoadLabel(LabelItem item)
        {
            ZplText = item.Content;

            string[] parts = item.Format.Split('x');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], out double w) &&
                double.TryParse(parts[1], out double h))
            {
                LabelWidth = w;
                LabelHeight = h;
            }

            RenderNow();
        }

        /// <summary>Debounced live render: re-render shortly after the last edit.</summary>
        private void ScheduleRender()
        {
            _renderTimer.Stop();
            _renderTimer.Start();
        }

        /// <summary>Render immediately (explicit actions: label/format selection, the Render button).</summary>
        private void RenderNow()
        {
            _renderTimer.Stop();
            Render();
        }

        private void Render()
        {
            PreviewWidth = LabelWidth;
            PreviewHeight = LabelHeight;
            PreviewDpmm = PrintDensityDpmm;
            PreviewZpl = ZplText;
            Analyze();
        }

        /// <summary>Parse the current ZPL to surface unknown commands / errors (mirrors the web preview panel).</summary>
        private void Analyze()
        {
            RenderError = null;
            UnknownCommands.Clear();

            try
            {
                var storage = new PrinterStorage();
                var analyzer = new ZplAnalyzer(storage);
                var info = analyzer.Analyze(PreviewZpl ?? string.Empty);

                foreach (string command in info.UnknownCommands ?? Array.Empty<string>())
                {
                    UnknownCommands.Add(command);
                }

                if (info.Errors is { Length: > 0 })
                {
                    RenderError = string.Join(Environment.NewLine, info.Errors);
                }

                // Surface draw-time failures too (the control itself renders empty on error).
                if (info.LabelInfos.Length > 0)
                {
                    new ZplElementDrawer(storage, new DrawerOptions { OpaqueBackground = true })
                        .CreateDrawing(info.LabelInfos[0].ZplElements, PreviewWidth, PreviewHeight, PreviewDpmm);
                }
            }
            catch (Exception ex)
            {
                RenderError = ex.Message;
            }

            OnPropertyChanged(nameof(HasUnknownCommands));
        }

        private void SavePng()
        {
            if (string.IsNullOrWhiteSpace(ZplText))
            {
                return;
            }

            var dialog = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = "label.png" };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var storage = new PrinterStorage();
                var info = new ZplAnalyzer(storage).Analyze(ZplText);
                if (info.LabelInfos.Length == 0)
                {
                    return;
                }

                byte[] png = new ZplElementDrawer(storage, new DrawerOptions { OpaqueBackground = true })
                    .DrawPng(info.LabelInfos[0].ZplElements, LabelWidth, LabelHeight, PrintDensityDpmm);
                File.WriteAllBytes(dialog.FileName, png);
            }
            catch (Exception ex)
            {
                RenderError = ex.Message;
            }
        }

        private void SaveZpl()
        {
            var dialog = new SaveFileDialog { Filter = "ZPL (*.zpl)|*.zpl|All files (*.*)|*.*", FileName = "label.zpl" };
            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, ZplText ?? string.Empty);
            }
        }

        private void LoadLabels()
        {
            foreach (LabelItem item in ReadLabelFolder("Example"))
            {
                ExampleLabels.Add(item);
            }

            foreach (LabelItem item in ReadLabelFolder("Test"))
            {
                TestLabels.Add(item);
            }
        }

        private static IEnumerable<LabelItem> ReadLabelFolder(string folder)
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Labels", folder);
            if (!Directory.Exists(dir))
            {
                yield break;
            }

            IEnumerable<string> files = Directory.GetFiles(dir, "*.zpl2")
                .OrderBy(f => NaturalKey(Path.GetFileNameWithoutExtension(f)), StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string[] parts = fileName.Split('-');
                yield return new LabelItem
                {
                    Name = parts[0],
                    Category = folder,
                    Format = parts.Length > 1 ? parts[1] : string.Empty,
                    Content = File.ReadAllText(file),
                };
            }
        }

        /// <summary>Pad digit runs so "Example2" sorts before "Example10" (natural sort).</summary>
        private static string NaturalKey(string s) => Regex.Replace(s, @"\d+", m => m.Value.PadLeft(10, '0'));
    }
}

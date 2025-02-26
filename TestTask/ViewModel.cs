using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;

namespace TestTask
{
    class ViewModel : ObservableObject
    {
        private const int DataArraySize = 1024;
        private PseudoData[] points = new PseudoData[DataArraySize];

        private Stopwatch stopwatch = new();
        LinearGradientBrush gradiend = new();

        //refresh 20 times per second: 1000 ms / 20 = 50
        DispatcherTimer updateTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 50) };

        private const int SpectrumGraphHeight = 200;
        private const int SpectrumGraphWidth = DataArraySize;
        private const int WaterfallGraphHeight = 500; //1000 ms * number of seconds (10) / refresh rate (20)
        private const int WaterfallGraphWidth = DataArraySize;

        const int INT_SIZE = 4; //bytes in int32
        private uint[,] waterfallBuffer = new uint[WaterfallGraphHeight, WaterfallGraphWidth];

        int ZoomLevel = 5;
        int ZoomStartInd = 0, ZoomEndInd = DataArraySize;

        #region Commands
        public ICommand StartDrawingCommand { get; }
        public ICommand StopDrawingCommand { get; }
        public ICommand ZoomChangedCommand { get; }
        #endregion

        #region ImageSource
        private ImageSource _spectrum;
        public ImageSource Spectrum
        {
            get => _spectrum;
            set => SetProperty(ref _spectrum, value);
        }

        private ImageSource _waterfall;
        public ImageSource Waterfall
        {
            get => _waterfall;
            set => SetProperty(ref _waterfall, value);
        }
        private ImageSource _XAxisValueLabels;
        public ImageSource XAxisValueLabels
        {
            get => _XAxisValueLabels;
            set => SetProperty(ref _XAxisValueLabels, value);
        }

        private ImageSource _YAxisValueLabels;
        public ImageSource YAxisValueLabels
        {
            get => _YAxisValueLabels;
            set => SetProperty(ref _YAxisValueLabels, value);
        }
        #endregion

        public ViewModel()
        {
            StartDrawingCommand = new RelayCommand(StartDrawing);
            StopDrawingCommand = new RelayCommand(StopDrawing);
            ZoomChangedCommand = new RelayCommand<object>(ZoomChanged);
            updateTimer.Tick += new EventHandler(UpdateHandler);

            //generate gradient brush for waterfall graph to take colors from
            gradiend.StartPoint = new System.Windows.Point(0, 0);
            gradiend.EndPoint = new System.Windows.Point(1, 1);
            gradiend.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(255, 0, 0), 1));
            gradiend.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(255, 255, 0), 0.75));
            gradiend.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(0, 255, 0), 0.5));
            gradiend.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(0, 255, 255), 0.25));
            gradiend.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(0, 0, 255), 0));
            gradiend.Freeze();

            DrawXAxisLabels();
            DrawYAxisLabels();
        }

        public void ZoomChanged(object? arg)
        {
            if (arg == null)
                return;
           // var data = (RoutedPropertyChangedEventArgs<double>)arg;
           // var newZoom = (float)data.NewValue;
           // float newZoomLevel = newZoom / 25F;
           // float step = PseudoDataGenerator.FrequencyRange / ZoomLevel;

           // //calculate new frequency bounds 
           // PseudoDataGenerator.FrequencyMinValue = PseudoDataGenerator.FrequencyMinValueAbs + (step * newZoomLevel) / 2;
           // PseudoDataGenerator.FrequencyMaxValue = PseudoDataGenerator.FrequencyMaxValueAbs - (step * newZoomLevel) / 2;
           // PseudoDataGenerator.FrequencyRange = Math.Abs(PseudoDataGenerator.FrequencyMinValue - PseudoDataGenerator.FrequencyMaxValue);
           //new idezes for iteration
           // ZoomStartInd = 0 + ;
           // ZoomEndInd = DataArraySize - ;

           // //draw graphs minding new bounds
           //if (updateTimer.IsEnabled)
           // {
           //     GeneratePseudoData();
           //     RedrawSpectrumGraph();
           //     RedrawWaterfallGraph();
           // }
        }

        private void StartDrawing()
        {
            GeneratePseudoData();
            RedrawSpectrumGraph();
            RedrawWaterfallGraph();
            updateTimer.Start();
        }

        private void StopDrawing()
        {
            updateTimer.Stop();
        }

        private void UpdateHandler(object? sender, EventArgs e)
        {
            GeneratePseudoData();
            RedrawSpectrumGraph();
            RedrawWaterfallGraph();
        }

        private void GeneratePseudoData()
        {
            for (int i = 0; i < DataArraySize; i++)
            {
                points[i] = PseudoDataGenerator.Generate(i, DataArraySize);
                Normalize(points[i]);
            }
        }

        private void Normalize(PseudoData data)
        {
            data.Magnitude -= PseudoDataGenerator.MagnitudeMinValue;
            data.Frequency -= PseudoDataGenerator.FrequencyMinValue;
        }

        private void RedrawSpectrumGraph()
        {
            var writeableBitmap = CreateImage(SpectrumGraphWidth, SpectrumGraphHeight);
            DrawSpectrumGraph(writeableBitmap);
            Spectrum = writeableBitmap;
        }

        private void RedrawWaterfallGraph()
        {
            var writeableBitmap = CreateImage(WaterfallGraphWidth, WaterfallGraphHeight);
            DrawWaterfallGraph(writeableBitmap);
            Waterfall = writeableBitmap;
        }

        private WriteableBitmap CreateImage(int width, int height)
        {
            var writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
            return writeableBitmap;
        }

        private void DrawSpectrumGraph(WriteableBitmap writeableBitmap)
        {
            int width = (int)writeableBitmap.Width,
            height = (int)writeableBitmap.Height;
            writeableBitmap.Lock();
            var skImageInfo = new SKImageInfo()
            {
                Width = width,
                Height = height,
                ColorType = SKColorType.Bgra8888,
                AlphaType = SKAlphaType.Premul,
                ColorSpace = SKColorSpace.CreateSrgb()
            };
            using var surface = SKSurface.Create(skImageInfo, writeableBitmap.BackBuffer);
            SKCanvas canvas = surface.Canvas;
            canvas.Translate(0, height);
            canvas.Clear(SKColors.Black);

            var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Color = SKColors.BlanchedAlmond;
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 0.5F;

            //draw grid lines
            float count = SpectrumGraphHeight / PseudoDataGenerator.MagnitudeRange * 2.5F;
            float step = SpectrumGraphHeight / count;
            for (int i = 0; i < count; i++)
            {
                canvas.DrawLine(new SKPoint(0, i * step * -1), new SKPoint(SpectrumGraphWidth, i * step * -1), paint);
            }

            count = SpectrumGraphWidth / PseudoDataGenerator.FrequencyRange * 0.25F;
            step = SpectrumGraphWidth / count;
            for (int i = 0; i < count; i++)
            {
                canvas.DrawLine(new SKPoint(i * step, 0), new SKPoint(i * step, SpectrumGraphHeight * -1), paint);
            }

            //graw graph
            paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Color = SKColors.Coral;
            paint.StrokeWidth = 1;
            paint.Style = SKPaintStyle.Stroke;

            var path = new SKPath();
            for (int i = 0; i < DataArraySize - 1; i++)
            {
                path.MoveTo(points[i].Frequency * SpectrumGraphWidth / PseudoDataGenerator.FrequencyRange, points[i].Magnitude * SpectrumGraphHeight / PseudoDataGenerator.MagnitudeRange * -1);
                path.LineTo(points[i + 1].Frequency * SpectrumGraphWidth / PseudoDataGenerator.FrequencyRange, points[i + 1].Magnitude * SpectrumGraphHeight / PseudoDataGenerator.MagnitudeRange * -1);
            }
            path.Close();
            canvas.DrawPath(path, paint);

            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            writeableBitmap.Unlock();
        }


        private void DrawWaterfallGraph(WriteableBitmap writeableBitmap)
        {
            int width = (int)writeableBitmap.Width,
            height = (int)writeableBitmap.Height;
            writeableBitmap.Lock();

            var skImageInfo = new SKImageInfo()
            {
                Width = width,
                Height = height,
                ColorType = SKColorType.Bgra8888,
                AlphaType = SKAlphaType.Premul,
                ColorSpace = SKColorSpace.CreateSrgb()
            };

            using var surface = SKSurface.Create(skImageInfo, writeableBitmap.BackBuffer);
            SKCanvas canvas = surface.Canvas;
            canvas.Clear();
            SKBitmap bitmap = new SKBitmap(width, height);

            Buffer.BlockCopy(waterfallBuffer, 0 * INT_SIZE, waterfallBuffer, width * INT_SIZE, (width * height - width) * INT_SIZE);

            for (int i = 0; i < width; i++)
            {
                double offset = points[i].Magnitude / PseudoDataGenerator.MagnitudeRange;
                var pixelColor = gradiend.GradientStops.GetRelativeColor(offset);
                waterfallBuffer[0, i] = (uint)new SKColor(pixelColor.R, pixelColor.G, pixelColor.B);
            }

            unsafe
            {
                fixed (uint* ptr = waterfallBuffer)
                {
                    bitmap.SetPixels((IntPtr)ptr);
                }
            }

            canvas.DrawBitmap(bitmap, 0, 0);
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            writeableBitmap.Unlock();
        }

        private void DrawXAxisLabels()
        {

        }

        private void DrawYAxisLabels()
        {

        }
    }
}

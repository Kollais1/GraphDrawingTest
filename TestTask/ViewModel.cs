using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace TestTask
{
    public delegate void DrawContent(SKCanvas Canvas);

    class ViewModel : ObservableObject
    {
        private const int DataArraySize = 1024;
        private PseudoData[] points = new PseudoData[DataArraySize];

        //refresh 20 times per second: 1000 ms / 20 = 50
        DispatcherTimer updateTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 50) };

        private const int SpectrumGraphHeight = 200;
        private const int SpectrumGraphWidth = DataArraySize;
        private const int WaterfallGraphHeight = 500; //1000 ms * number of seconds (10) / refresh rate (20)
        private const int WaterfallGraphWidth = DataArraySize;
        private const int XAxisGraphWidth = SpectrumGraphWidth;
        public const int XAxisGraphHeight = 30;
        public const int YAxisGraphWidth = 30;
        private const int YAxisGraphHeight = SpectrumGraphHeight;

        int HorizontalLinesCount = 5, VerticalLinesCount = 14;
        LinearGradientBrush gradiend = new();
        const int INT_SIZE = 4; //bytes in int32
        private uint[,] waterfallBuffer = new uint[WaterfallGraphHeight, WaterfallGraphWidth];

        int ZoomLevel = 0;
        float ZoomValue = 0.1F; //10%
        int ZoomStartInd = 0, ZoomEndInd = DataArraySize;

        WriteableBitmap WaterfallBitmap;
        WriteableBitmap SpectrumBitmap;
        WriteableBitmap XAxisBitmap;
        WriteableBitmap YAxisBitmap;

        #region Commands
        public ICommand StartDrawingCommand { get; }
        public ICommand StopDrawingCommand { get; }
        public ICommand ZoomChangedCommand { get; }
        public ICommand OnPaintSurfaceCommand { get; }
        #endregion

        #region ImageSource
        private ImageSource? _spectrum;
        public ImageSource Spectrum
        {
            get => _spectrum;
            set => SetProperty(ref _spectrum, value);
        }

        private ImageSource? _waterfall;
        public ImageSource Waterfall
        {
            get => _waterfall;
            set => SetProperty(ref _waterfall, value);
        }
        private ImageSource? _XAxisValueLabels;
        public ImageSource XAxisValueLabels
        {
            get => _XAxisValueLabels;
            set => SetProperty(ref _XAxisValueLabels, value);
        }

        private ImageSource? _YAxisValueLabels;
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
            OnPaintSurfaceCommand = new RelayCommand<SKPaintSurfaceEventArgs>(OnPaintSurface);
            updateTimer.Tick += new EventHandler(UpdateHandler);

            WaterfallBitmap = DrawingHelper.CreateImage(WaterfallGraphWidth, WaterfallGraphHeight);
            SpectrumBitmap = DrawingHelper.CreateImage(SpectrumGraphWidth, SpectrumGraphHeight);
            XAxisBitmap = DrawingHelper.CreateImage(XAxisGraphWidth, XAxisGraphHeight);
            YAxisBitmap = DrawingHelper.CreateImage(YAxisGraphWidth, YAxisGraphHeight);

            //generate gradient brush for waterfall graph to take colors from
            gradiend.StartPoint = new System.Windows.Point(0, 0);
            gradiend.EndPoint = new System.Windows.Point(1, 1);
            gradiend.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 1));
            gradiend.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 0), 0.75));
            gradiend.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 0), 0.5));
            gradiend.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 255), 0.25));
            gradiend.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 255), 0));
            gradiend.Freeze();

            DrawXAxisLabels();
            DrawYAxisLabels();
        }

        public void ZoomChanged(object? arg)
        {
            if (arg == null)
                return;
            var data = (RoutedPropertyChangedEventArgs<double>)arg;
            var newZoom = (int)data.NewValue;
            ZoomLevel = newZoom;
            float step = PseudoDataGenerator.FrequencyRange / ZoomLevel;

            //calculate new frequency bounds 
            PseudoDataGenerator.FrequencyMinValue = PseudoDataGenerator.FrequencyMinValueAbs + (PseudoDataGenerator.FrequencyRangeAbs * ZoomValue * ZoomLevel);
            PseudoDataGenerator.FrequencyMaxValue = PseudoDataGenerator.FrequencyMaxValueAbs - (PseudoDataGenerator.FrequencyRangeAbs * ZoomValue * ZoomLevel);
            PseudoDataGenerator.FrequencyRange = Math.Abs(PseudoDataGenerator.FrequencyMinValue - PseudoDataGenerator.FrequencyMaxValue);
            //new indexes for iteration
            ZoomStartInd = 0 + (int)(DataArraySize * ZoomValue * ZoomLevel);
            ZoomEndInd = DataArraySize - (int)(DataArraySize * ZoomValue * ZoomLevel);

            DrawXAxisLabels();
            DrawYAxisLabels();
            //draw graphs minding new bounds
            if (updateTimer.IsEnabled)
            {
                GetPseudoData();
                DrawSpectrumGraph();
                DrawWaterfallGraph();
            }
        }

        private void StartDrawing()
        {
            GetPseudoData();
            DrawSpectrumGraph();
            DrawWaterfallGraph();
            updateTimer.Start();
        }

        private void StopDrawing()
        {
            updateTimer.Stop();
        }

        private void UpdateHandler(object? sender, EventArgs e)
        {
            GetPseudoData();
            DrawSpectrumGraph();
            DrawWaterfallGraph();
        }

        private void GetPseudoData()
        {
            for (int i = 0; i < DataArraySize; i++)
            {
                points[i] = PseudoDataGenerator.Generate(i, DataArraySize);
                NormalizeData(points[i]);
            }
        }

        private void NormalizeData(PseudoData data)
        {
            data.Magnitude -= PseudoDataGenerator.MagnitudeMinValue;
            data.Frequency -= PseudoDataGenerator.FrequencyMinValue;
        }

        private void DrawSpectrumGraph()
        {
            DrawingHelper.DrawOnCanvas(SpectrumBitmap, DrawSpectrumGraphContents);
            Spectrum = SpectrumBitmap;
        }

        private void DrawWaterfallGraph()
        {
            DrawingHelper.DrawOnCanvas(WaterfallBitmap, DrawWaterfallGraphContents);
            Waterfall = WaterfallBitmap;
        }

        private void DrawXAxisLabels()
        {
            DrawingHelper.DrawOnCanvas(XAxisBitmap, DrawXAxisGraphContents);
            XAxisValueLabels = XAxisBitmap;
        }

        private void DrawYAxisLabels()
        {
            DrawingHelper.DrawOnCanvas(YAxisBitmap, DrawYAxisGraphContents);
            YAxisValueLabels = YAxisBitmap;
        }

        private void DrawSpectrumGraphContents(SKCanvas canvas)
        {
            canvas.Translate(0, SpectrumGraphHeight);
            canvas.Clear(SKColors.Black);

            var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Color = SKColors.BlanchedAlmond;
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 0.5F;

            //draw grid lines
            float step = SpectrumGraphHeight / HorizontalLinesCount;
            for (int i = 0; i < HorizontalLinesCount; i++)
            {
                canvas.DrawLine(new SKPoint(0, i * step * -1), new SKPoint(SpectrumGraphWidth, i * step * -1), paint);
            }

            step = SpectrumGraphWidth / VerticalLinesCount;
            for (int i = 0; i < VerticalLinesCount; i++)
            {
                canvas.DrawLine(new SKPoint(i * step, 0), new SKPoint(i * step, SpectrumGraphHeight * -1), paint);
            }

            //draw graph
            paint.Color = SKColors.Coral;
            paint.StrokeWidth = 1;

            var path = new SKPath();
            for (int i = ZoomStartInd; i < ZoomEndInd - 1; i++)
            {
                path.MoveTo(points[i].Frequency * SpectrumGraphWidth / PseudoDataGenerator.FrequencyRange, points[i].Magnitude * SpectrumGraphHeight / PseudoDataGenerator.MagnitudeRange * -1);
                path.LineTo(points[i + 1].Frequency * SpectrumGraphWidth / PseudoDataGenerator.FrequencyRange, points[i + 1].Magnitude * SpectrumGraphHeight / PseudoDataGenerator.MagnitudeRange * -1);
            }
            path.Close();
            canvas.DrawPath(path, paint);
        }

        private void DrawWaterfallGraphContents(SKCanvas canvas)
        {
            var width = WaterfallGraphWidth;
            var height = WaterfallGraphHeight;
            SKBitmap bitmap = new SKBitmap(width, height);

            //shift pixels in bitmap one row down
            Buffer.BlockCopy(waterfallBuffer, 0 * INT_SIZE, waterfallBuffer, width * INT_SIZE, (width * height - width) * INT_SIZE);

            if (ZoomLevel == 0)
            {
                // add a line with new pixels onto the first row
                for (int i = 0; i < DataArraySize; i++)
                {
                    double offset = points[i].Magnitude / PseudoDataGenerator.MagnitudeRange;
                    var pixelColor = gradiend.GradientStops.GetRelativeColor(offset);
                    waterfallBuffer[0, i] = (uint)new SKColor(pixelColor.R, pixelColor.G, pixelColor.B);
                }
            }

            else
            {
                //extrapolate from cropped data
                var pixelCount = ZoomEndInd - ZoomStartInd;
                var croppedData = new uint[pixelCount];
                for (int i = ZoomStartInd; i < ZoomEndInd; i++)
                {
                    double offset = points[i].Magnitude / PseudoDataGenerator.MagnitudeRange;
                    var pixelColor = gradiend.GradientStops.GetRelativeColor(offset);
                    croppedData[i- ZoomStartInd] = (uint)new SKColor(pixelColor.R, pixelColor.G, pixelColor.B);
                }

                var extrapolatedData = NearestNeighbor(croppedData, DataArraySize);
                Buffer.BlockCopy(extrapolatedData, 0, waterfallBuffer, 0, width * INT_SIZE);
            }

            unsafe
            {
                fixed (uint* ptr = waterfallBuffer)
                {
                    bitmap.SetPixels((IntPtr)ptr);
                }
            }

            canvas.DrawBitmap(bitmap, 0, 0);
        }

        static uint[] NearestNeighbor(uint[] data, int newWidth)
        {
            var newArr = new uint[newWidth];
            var oldWidth = data.Length;
            var ratio = (double)oldWidth / newWidth;

            for (int i = 0; i < newWidth; i++)
            {
                var unscaledIndex = (int)(i * ratio);
                newArr[i] = data[unscaledIndex];
            }

            return newArr;
        }

        private void DrawXAxisGraphContents(SKCanvas canvas)
        {
            var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Color = SKColors.Black;
            paint.Style = SKPaintStyle.StrokeAndFill;
            var font = new SKFont();
            font.Size = 12;

            float step = SpectrumGraphWidth / VerticalLinesCount;
            float value = 0;
            float offset = 15;

            canvas.DrawLine(new SKPoint(0, 1), new SKPoint(SpectrumGraphWidth, 1), paint);
            for (int i = 0; i < VerticalLinesCount; i++)
            {
                value = PseudoDataGenerator.FrequencyMinValue + i * (PseudoDataGenerator.FrequencyRange / VerticalLinesCount);
                canvas.DrawCircle(new SKPoint(i * step + 1, 2), 1, paint);
                canvas.DrawText(String.Format("{0:0.##}", value), new SKPoint(Math.Max(0,i * step - offset) , font.Size), SKTextAlign.Left, font, paint);
            }
            value = PseudoDataGenerator.FrequencyMaxValue;
            canvas.DrawCircle(new SKPoint(XAxisGraphWidth - 1, 2), 1, paint);
            canvas.DrawText(String.Format("{0:0.##}", value), new SKPoint(XAxisGraphWidth - offset/2 - font.Size, font.Size), SKTextAlign.Left, font, paint);
        }

        private void DrawYAxisGraphContents(SKCanvas canvas)
        {
            var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Color = SKColors.Black;
            paint.Style = SKPaintStyle.StrokeAndFill;
            var font = new SKFont();
            font.Size = 12;

            canvas.Translate(0, SpectrumGraphHeight);

            float step = SpectrumGraphHeight / HorizontalLinesCount;
            float value = 0;
            float offset = 5;
            canvas.DrawLine(new SKPoint(YAxisGraphWidth - 1, 0), new SKPoint(YAxisGraphWidth - 1, SpectrumGraphHeight * -1), paint);
            for (int i = 0; i < HorizontalLinesCount; i++)
            {
                value = PseudoDataGenerator.MagnitudeMinValue + i * (PseudoDataGenerator.MagnitudeRange / HorizontalLinesCount);
                canvas.DrawCircle(new SKPoint(YAxisGraphWidth - 2, i * step * -1), 1, paint);
                canvas.DrawText(String.Format("{0:0.##}", value), new SKPoint(0, Math.Min(0, i * step * -1 + offset)), SKTextAlign.Left, font, paint);
            }
            value = PseudoDataGenerator.MagnitudeMaxValue;
            canvas.DrawCircle(new SKPoint(YAxisGraphWidth - 2, YAxisGraphHeight * -1), 1, paint);
            canvas.DrawText(String.Format("{0:0.##}", value), new SKPoint(0, YAxisGraphHeight * -1 + font.Size), SKTextAlign.Left, font, paint);
        }
    }
}

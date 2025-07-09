using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Skia;
using Avalonia.Media;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Threading;

namespace Trail.Views;

public partial class TrailCanvas : UserControl
{
    private SKBitmap? _trailBitmap; // This bitmap holds the cumulative trail
    private SKBitmap? _echoBitmap; // This bitmap holds the cumulative trail
    private SKPoint _currentCirclePosition;
    private Random _random;
    private int _circleRadius = 20;

    // Colors
    private SKColor _echoColor = SKColors.Red;
    private SKColor _backgroundColor = SKColors.Black;
    private SKColor _trailColor = SKColors.Yellow;
    private SKColor _fadeToBackgroundColor;

    // Timer for updates
    private int frameTime = 30;     // in miliseconds
    private int trailLength = 3;   // in seconds
    private DispatcherTimer _animationTimer;

    // lock to prevent memory violation
    private Object drawLock = new();

    public TrailCanvas()
    {
        InitializeComponent();
        _random = new Random();

        // Setup the LayoutUpdated event to handle bitmap sizing
        LayoutUpdated += (sender, e) =>
        {
            // Get the actual pixel size of the control
            // Avalonia's Bounds are in device-independent pixels. CanvasSize needs physical pixels.
            // We need to scale by RenderScaling for the bitmap.
            double scaling = VisualRoot?.RenderScaling ?? 1.0;
            int pixelWidth = (int)Math.Ceiling(Bounds.Width * scaling);
            int pixelHeight = (int)Math.Ceiling(Bounds.Height * scaling);

            if (pixelWidth > 0 && pixelHeight > 0)
            {
                Console.WriteLine("{0,20} Called {1} trail = {2} echo = {3}", "LayoutA", DateTime.Now.ToString("mm:ss.fff"), _trailBitmap?.GetHashCode(), _echoBitmap?.GetHashCode());
                lock (drawLock)
                {
                    if (_trailBitmap is null || _trailBitmap.Width != pixelWidth || _trailBitmap.Height != pixelHeight)
                    {
                        _trailBitmap?.Dispose(); // Dispose previous if size changed
                        _trailBitmap = new SKBitmap(pixelWidth, pixelHeight, SKColorType.Rgba8888, SKAlphaType.Premul); // Use RGBA8888 for color
                    }
                    if (_echoBitmap is null || _echoBitmap.Width != pixelWidth || _echoBitmap.Height != pixelHeight)
                    {
                            _echoBitmap?.Dispose(); // Dispose previous if size changed
                            _echoBitmap = new SKBitmap(pixelWidth, pixelHeight, SKColorType.Rgba8888, SKAlphaType.Premul); // Use RGBA8888 for color
                    }
                    InitializeTrailBitmap();
                }
                Console.WriteLine("{0,20} Called {1} trail = {2} echo = {3}", "LayoutB", DateTime.Now.ToString("mm:ss.fff"), _trailBitmap?.GetHashCode(), _echoBitmap?.GetHashCode());
                // Set initial circle position to center, scaled by render scaling
                _currentCirclePosition = new SKPoint((float)(Bounds.Width / 2), (float)(Bounds.Height / 2));
            }
        };

        // Set up the DispatcherTimer
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(frameTime)
        };
        _animationTimer.Tick += AnimationTimer_Tick;
        _animationTimer.Start();

        TrailLength.SelectionChanged += (sender, e) =>
        {
            if (TrailLength.SelectedItem is ComboBoxItem selectedItem)
            {
                if (int.TryParse(selectedItem!.Content!.ToString(), out int newLength))
                {
                    trailLength = newLength;
                    Console.WriteLine("Trail length set to: {0} seconds", trailLength);
                    UpdateTrailLengthColor();
                }
            }
        };
        TrailLength.SelectedIndex = 1; // Default to 3 seconds

        BackgroundColor.SelectionChanged += (sender, e) =>
        {
            if (BackgroundColor.SelectedItem is ComboBoxItem selectedItem)
            {
                if (Color.TryParse(selectedItem!.Content!.ToString(), out Color parsedColor))
                {
                    _backgroundColor = parsedColor.ToSKColor();
                    Console.WriteLine("Background color set to: {0} {1}", _backgroundColor, parsedColor);
                    UpdateTrailLengthColor();
                }
            }
        };
        EchoColor.SelectionChanged += (sender, e) =>
        {
            if (EchoColor.SelectedItem is ComboBoxItem selectedItem)
            {
                if (Color.TryParse(selectedItem!.Content!.ToString(), out Color parsedColor))
                {
                    _echoColor = parsedColor.ToSKColor();
                    Console.WriteLine("Echo color set to: {0} {1}", _echoColor, parsedColor);
                }
            }
        };
        TrailColor.SelectionChanged += (sender, e) =>
        {
            if (TrailColor.SelectedItem is ComboBoxItem selectedItem)
            {
                if (Color.TryParse(selectedItem!.Content!.ToString(), out Color parsedColor))
                {
                    _trailColor = parsedColor.ToSKColor();
                    Console.WriteLine("Trail color set to: {0} {1}", _trailColor, parsedColor);
                }
            }
        };
        FpsComboBox.SelectionChanged += (sender, e) =>
        {
            if (FpsComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                if (int.TryParse(selectedItem!.Content!.ToString(), out int fps))
                {
                    frameTime = 1000 / fps; // Convert FPS to milliseconds per frame
                    _animationTimer.Interval = TimeSpan.FromMilliseconds(frameTime);
                    Console.WriteLine("FPS set to: {0} ({1} ms per frame)", fps, frameTime);
                    UpdateTrailLengthColor(); // Update fade color based on new frame time
                }
            }
        };
        // using skiacontrol
        TrailSKCanvas.Draw += (s, e) => { lock (drawLock) { DrawTrail(s, e.Canvas); } };
    }

    private void UpdateTrailLengthColor()
    {
        // calculate alpha Af = 255x(1-(TargetAlpha/InitialAlpha)^(1/N)))
        var noOfFrames = trailLength * 1000 / frameTime;
        byte Af = (byte)Math.Round(255 * ( 1 - Math.Pow(1.0/255.0, 1.0 / noOfFrames)), 0, MidpointRounding.AwayFromZero);
        _fadeToBackgroundColor = _backgroundColor.WithAlpha(Af); // Semi-transparent to background (alpha 5)
    }

    private void InitializeTrailBitmap()
    {
        if (_trailBitmap is { }) new SKCanvas(_trailBitmap).Clear(_backgroundColor);
        if (_echoBitmap is { }) new SKCanvas(_echoBitmap).Clear(_backgroundColor);
    }

    private void UpdateCirclePosition()
    {
        float moveAmount = _circleRadius; // Max pixels to move per step (in device-independent pixels)

        _currentCirclePosition.X += (float)(_random.NextDouble() * 2 - 1) * moveAmount;
        _currentCirclePosition.Y += (float)(_random.NextDouble() * 2 - 1) * moveAmount;

        // Clamp position to stay within control bounds (device-independent pixels)
        _currentCirclePosition.X = Math.Max(_circleRadius, Math.Min((float)Bounds.Width - _circleRadius, _currentCirclePosition.X));
        _currentCirclePosition.Y = Math.Max(_circleRadius, Math.Min((float)Bounds.Height - _circleRadius, _currentCirclePosition.Y));
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        // Console.WriteLine("{0,20} Called {1} canvas = {2} - {3}", "TimerTickA", DateTime.Now.ToString("mm:ss.fff"), _trailCanvas?.GetHashCode(), _echoCanvas?.GetHashCode());
        UpdateCirclePosition();
        TrailSKCanvas.InvalidateVisual();   // Request a redraw of this control
        // Console.WriteLine("{0,20} Called {1} canvas = {2} - {3}", "TimerTickB", DateTime.Now.ToString("mm:ss.fff"), _trailCanvas?.GetHashCode(), _echoCanvas?.GetHashCode());
    }

    // Override the Render method to perform custom drawing
    public override void Render(DrawingContext context)
    {
        base.Render(context); // Call base implementation
        Console.WriteLine("{0,20} Called {1} context = {2}", "Render", DateTime.Now.ToString("mm:ss.fff"), context?.GetHashCode());
    }

    private void DrawTrail(object? source, SKCanvas canvas)
    {
        // Console.WriteLine("{0,20} Called {1} canvas = {2} source = {3}", "DrawTrailA", DateTime.Now.ToString("mm:ss.fff"), _trailCanvas?.GetHashCode(), canvas.GetHashCode());
        if (canvas is null) return;

        // Ensure bitmap is initialized and has valid dimensions
        if (_echoBitmap is null || _echoBitmap.Width == 0 || _echoBitmap.Height == 0) return;
        if (_trailBitmap is null || _trailBitmap.Width == 0 || _trailBitmap.Height == 0) return;

        // --- Core Trail Logic (Applied to _trailBitmap) ---
        // Console.WriteLine("{0,20} Called {1} canvas = {2} bitmap = {3}", "LockBitmapA", DateTime.Now.ToString("mm:ss.fff"), canvas?.GetHashCode(), bitmap.GetHashCode());
        // Draw trail dulu lepas tu baru draw echo
        foreach (var (bitmap, color, fading) in new[] { (_trailBitmap, _trailColor, true), (_echoBitmap, _echoColor, false) })
        {
            using var drawingCanvas = new SKCanvas(bitmap);

            // 1. Apply the fade-to-black layer: This makes old trail parts fade out.
            //    OR clear the bitmap using the background color
            if (fading)
                using (var fadePaint = new SKPaint { Color = _fadeToBackgroundColor, BlendMode = SKBlendMode.SrcOver })
                {
                    drawingCanvas.DrawRect(0, 0, bitmap.Width, bitmap.Height, fadePaint);
                }
            else
                drawingCanvas.Clear(SKColors.Transparent);

            // 2. Draw the new Trail circle: This creates the new "head" of the trail.
                using (var circlePaint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill })
                {
                    // When drawing to the SKBitmap, the coordinates need to be in physical pixels.
                    // We stored _currentCirclePosition in device-independent pixels, so we need to scale.
                    double scaling = VisualRoot?.RenderScaling ?? 1.0;
                    float scaledX = (float)(_currentCirclePosition.X * scaling);
                    float scaledY = (float)(_currentCirclePosition.Y * scaling);
                    float scaledRadius = (float)(_circleRadius * scaling);

                    drawingCanvas.DrawCircle(scaledX, scaledY, scaledRadius, circlePaint);
                }
            drawingCanvas.Flush(); // Ensure all drawing commands are executed
            // --- End Core Trail Logic ---

            // 3. Draw the accumulated trail bitmap onto Avalonia's DrawingContext
            // Console.WriteLine("{0,20} Called {1} canvas = {2} bitmap = {3}", "LockCanvasA", DateTime.Now.ToString("mm:ss.fff"), canvas?.GetHashCode(), bitmap.GetHashCode());
            canvas.DrawBitmap(bitmap, 0, 0);
        }
        // Console.WriteLine("{0,20} Called {1} canvas = {2} bitmap = {3}", "LockBitmapB", DateTime.Now.ToString("mm:ss.fff"), canvas?.GetHashCode(), bitmap.GetHashCode());
        // Console.WriteLine("{0,20} Called {1} canvas = {2}", "DrawTrailB", DateTime.Now.ToString("mm:ss.fff"), _trailCanvas?.GetHashCode());
    }

    // Proper disposal of the bitmap when the control is no longer used
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animationTimer?.Stop(); // Stop the timer
        _trailBitmap?.Dispose();
        _trailBitmap = null;
        _echoBitmap?.Dispose();
        _echoBitmap = null;
        base.OnDetachedFromVisualTree(e);
    }
}
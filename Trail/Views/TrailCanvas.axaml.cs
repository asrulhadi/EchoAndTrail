using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.Threading;
//using Avalonia.Controls.Skia;
using SkiaSharp;
using System;
using System.Diagnostics; // For Debug.WriteLine

namespace Trail.Views;

public partial class TrailCanvas : UserControl
{
    private SKBitmap _trailBitmap; // This bitmap holds the cumulative trail
    private SKPoint _currentCirclePosition;
    private Random _random;
    private int _circleRadius = 20;

    // Colors
    private SKColor _circleColor = SKColors.Yellow;
    //private SKColor _trailFadeColor = new SKColor(0, 0, 255, 5); // Semi-transparent BLUE (alpha 5)
    private SKColor _trailFadeColor = SKColors.Blue.WithAlpha(5); // Semi-transparent BLUE (alpha 5)
    private SKColor _backgroundColor = SKColors.Black;

    // Timer for updates
    private int frameTime = 30;     // in miliseconds
    private int trailLength = 20;   // in seconds
    private DispatcherTimer _animationTimer;
    private SKCanvas canvas;

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
                if (_trailBitmap == null || _trailBitmap.Width != pixelWidth || _trailBitmap.Height != pixelHeight)
                {
                    _trailBitmap?.Dispose(); // Dispose previous if size changed
                    _trailBitmap = new SKBitmap(pixelWidth, pixelHeight, SKColorType.Rgba8888, SKAlphaType.Premul); // Use RGBA8888 for color
                    InitializeTrailBitmap();
                    // Set initial circle position to center, scaled by render scaling
                    _currentCirclePosition = new SKPoint((float)(Bounds.Width / 2), (float)(Bounds.Height / 2));
                }
            }
        };

        // Set up the DispatcherTimer
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(frameTime)
        };
        _animationTimer.Tick += AnimationTimer_Tick;
        _animationTimer.Start();

        // calculate alpha Af = 255x(1-(TargetAlpha/InitialAlpha)^(1/N)))
        byte Af = (byte)Math.Round(255 * Math.Pow(1 - (5.0/255.0), 1 / trailLength), 0, MidpointRounding.AwayFromZero);
        _trailFadeColor = SKColors.Blue.WithAlpha(Af); // Semi-transparent BLUE (alpha 5)

        // using skiacontrol
        //var canvas = this.FindControl<SKCanvasControl>("CanvasControl");
        CanvasControl.Draw += (_, e) => DrawMe(e.Canvas);
        //CanvasControl.Content = "Hello";
    }

    private void InitializeTrailBitmap()
    {
        if (_trailBitmap != null)
        {
            using (var canvas = new SKCanvas(_trailBitmap))
            {
                canvas.Clear(_backgroundColor); // Clear to black background
            }
        }
    }

    private void UpdateCirclePosition()
    {
        if (_trailBitmap == null) return;

        float moveAmount = 10f; // Max pixels to move per step (in device-independent pixels)

        _currentCirclePosition.X += (float)(_random.NextDouble() * 2 - 1) * moveAmount;
        _currentCirclePosition.Y += (float)(_random.NextDouble() * 2 - 1) * moveAmount;

        // Clamp position to stay within control bounds (device-independent pixels)
        _currentCirclePosition.X = Math.Max(_circleRadius, Math.Min((float)Bounds.Width - _circleRadius, _currentCirclePosition.X));
        _currentCirclePosition.Y = Math.Max(_circleRadius, Math.Min((float)Bounds.Height - _circleRadius, _currentCirclePosition.Y));
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        UpdateCirclePosition();
        InvalidateVisual(); // Request a redraw of this control
    }

    // Override the Render method to perform custom drawing
    public override void Render(DrawingContext context)
    {
        //Console.WriteLine("Render Called {0} canvas = {1}", DateTime.Now.ToString("mm:ss.fff"), canvas?.GetHashCode());
        base.Render(context); // Call base implementation
    }

    private void UpdateCanvas()
    {
        if (canvas is null) this.canvas = canvas;
        if (this.canvas is null) return;
        // Ensure _trailBitmap is initialized and has valid dimensions
        if (_trailBitmap == null || _trailBitmap.Width == 0 || _trailBitmap.Height == 0)
        {
            return;
        }

        // --- Core Trail Logic (Applied to _trailBitmap) ---

        // 1. Draw the semi-transparent BLUE rectangle over the entire trail bitmap
        using (var trailCanvas = new SKCanvas(_trailBitmap))
        using (var fadePaint = new SKPaint { Color = _trailFadeColor, BlendMode = SKBlendMode.SrcOver })
        {
            trailCanvas.DrawRect(0, 0, _trailBitmap.Width, _trailBitmap.Height, fadePaint);
        }

        // 2. Draw the new YELLOW circle onto the trail bitmap
        using (var trailCanvas = new SKCanvas(_trailBitmap))
        using (var circlePaint = new SKPaint { Color = _circleColor, IsAntialias = true, Style = SKPaintStyle.Fill })
        {
            // When drawing to the SKBitmap, the coordinates need to be in physical pixels.
            // We stored _currentCirclePosition in device-independent pixels, so we need to scale.
            double scaling = VisualRoot?.RenderScaling ?? 1.0;
            float scaledX = (float)(_currentCirclePosition.X * scaling);
            float scaledY = (float)(_currentCirclePosition.Y * scaling);
            float scaledRadius = (float)(_circleRadius * scaling);

            trailCanvas.DrawCircle(scaledX, scaledY, scaledRadius, circlePaint);
        }

        // --- End Core Trail Logic ---

        // 3. Draw the accumulated trail bitmap onto Avalonia's DrawingContext
        if (canvas != null)
        {
            lock(canvas)
            {
                canvas.DrawBitmap(_trailBitmap,
                        new SKRect(0, 0, _trailBitmap.Width, _trailBitmap.Height),
                        new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height),
                        null);
            }
        }
        else
        {
            // Fallback or error handling if not using Skia backend or older Avalonia version
            Debug.WriteLine("Warning: Not using Skia backend for drawing context or unsupported Avalonia version.");
        }
        Console.WriteLine("UpdateCanvas Called {0} canvas = {1}", DateTime.Now.ToString("mm:ss.fff"), canvas?.GetHashCode());
    }

    private void DrawMe(SKCanvas skCanvas)
    {
        if (canvas is null) this.canvas = skCanvas;
        if (this.canvas is null) return;
        UpdateCanvas();
        Console.WriteLine("DrawMe Called {0} canvas = {1}", DateTime.Now.ToString("mm:ss.fff"), canvas?.GetHashCode());
    }

    // Proper disposal of the bitmap when the control is no longer used
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animationTimer?.Stop(); // Stop the timer
        _trailBitmap?.Dispose();
        _trailBitmap = null;
        base.OnDetachedFromVisualTree(e);
    }
}
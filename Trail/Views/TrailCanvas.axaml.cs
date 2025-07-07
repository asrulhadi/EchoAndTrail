using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Skia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.Threading;
//using Avalonia.Controls.Skia;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO; // For Debug.WriteLine

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
    private int frameTime = 100;     // in miliseconds
    private int trailLength = 20;   // in seconds
    private DispatcherTimer _animationTimer;
    private SKCanvas canvas;

    // New: SKPicture and SKPictureRecorder for recording drawing commands
    private SKPictureRecorder _pictureRecorder = new();
    private SKPicture _currentPicture;

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
        var noOfFrames = trailLength * 1000 / frameTime;
        var pow = Math.Pow(1 - (5.0 / 255.0), 1.0 / noOfFrames);
        byte Af = (byte)Math.Round(255 * ( 1 - Math.Pow(5.0/255.0, 1.0 / noOfFrames)), 0, MidpointRounding.AwayFromZero);
        _trailFadeColor = SKColors.Blue.WithAlpha(Af); // Semi-transparent BLUE (alpha 5)

        // using skiacontrol
        CanvasControl.Draw += (_, e) => DrawMe(e.Canvas);
    }

    private void InitializeTrailBitmap()
    {
        if (_trailBitmap != null)
        {
            using var canvas = new SKCanvas(_trailBitmap);
            canvas.Clear(_backgroundColor); // Clear to black background
        }
    }

    private void UpdateCirclePosition()
    {
        if (_trailBitmap == null) return;

        float moveAmount = 50f; // Max pixels to move per step (in device-independent pixels)

        _currentCirclePosition.X += (float)(_random.NextDouble() * 2 - 1) * moveAmount;
        _currentCirclePosition.Y += (float)(_random.NextDouble() * 2 - 1) * moveAmount;

        // Clamp position to stay within control bounds (device-independent pixels)
        _currentCirclePosition.X = Math.Max(_circleRadius, Math.Min((float)Bounds.Width - _circleRadius, _currentCirclePosition.X));
        _currentCirclePosition.Y = Math.Max(_circleRadius, Math.Min((float)Bounds.Height - _circleRadius, _currentCirclePosition.Y));
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        Console.WriteLine("{0,12} Called {1} canvas = {2}", "TimerTick", DateTime.Now.ToString("mm:ss.fff"), canvas?.GetHashCode());
        UpdateCirclePosition();
        CanvasControl.InvalidateVisual();   // Request a redraw of this control
    }

    // Override the Render method to perform custom drawing
    public override void Render(DrawingContext context)
    {
        base.Render(context); // Call base implementation
        // --- Draw the SKPicture onto Avalonia's DrawingContext ---
        if (_currentPicture != null)
        {
            // 5. Convert the SKBitmap to an Avalonia.Media.Imaging.Bitmap.
            //    This typically involves saving the SKBitmap to a MemoryStream as a PNG or other format.
            using var image = SKImage.FromBitmap(_trailBitmap);
            //using (var image = SKImage.FromPicture(_currentPicture, new SKSizeI((int)_currentPicture.CullRect.Width, (int)_currentPicture.CullRect.Height)))

            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            var avaloniaBitmap = new Bitmap(stream);

            // 6. Draw the Avalonia Bitmap onto the DrawingContext.
            //    The second argument is the source rectangle within the bitmap.
            //    The third argument is the destination rectangle on the DrawingContext.
            var sourceRect = new Rect(0, 0, avaloniaBitmap.PixelSize.Width, avaloniaBitmap.PixelSize.Height);
            var destRect = new Rect(0, 0, Bounds.Width, Bounds.Height); // Draw across the whole control

            //context.DrawImage(avaloniaBitmap, sourceRect, destRect);
            Console.WriteLine("{0,12} Called {1} canvas = {2}", "DrawImage", DateTime.Now.ToString("mm:ss.fff"), canvas?.GetHashCode());
        }
        Console.WriteLine("{0,12} Called {1} canvas = {2}", "Render", DateTime.Now.ToString("mm:ss.fff"), canvas?.GetHashCode());
    }

    private void UpdateCanvas()
    {
       this.canvas ??= canvas;
        if (this.canvas is null) return;


        // Ensure _trailBitmap is initialized and has valid dimensions
        if (_trailBitmap == null || _trailBitmap.Width == 0 || _trailBitmap.Height == 0)
        {
            return;
        }

        // --- Core Trail Logic (Applied to _trailBitmap) ---

        // 1. Draw the semi-transparent BLUE rectangle over the entire trail bitmap
        using var trailCanvas = new SKCanvas(_trailBitmap);
        using (var fadePaint = new SKPaint { Color = _trailFadeColor, BlendMode = SKBlendMode.SrcOver })
        {
            trailCanvas.DrawRect(0, 0, _trailBitmap.Width, _trailBitmap.Height, fadePaint);
        }

        // 2. Draw the new YELLOW circle onto the trail bitmap
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
        canvas.DrawBitmap(_trailBitmap, 0, 0);
        Console.WriteLine("{0,12} Called {1} canvas = {2}", "UpdateCanvas", DateTime.Now.ToString("mm:ss.fff"), canvas?.GetHashCode());
    }

    private void DrawMe(SKCanvas skCanvas)
    {
        if (canvas is null) this.canvas = skCanvas;
        if (this.canvas is null) return;
        UpdateCanvas();
        //canvas.DrawPicture(_currentPicture);
        Console.WriteLine("{0,12} Called {1} canvas = {2}", "DrawMe", DateTime.Now.ToString("mm:ss.fff"), canvas?.GetHashCode());
    }

    // Proper disposal of the bitmap when the control is no longer used
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animationTimer?.Stop(); // Stop the timer
        _trailBitmap?.Dispose();
        _trailBitmap = null;
        _pictureRecorder?.Dispose(); // Dispose the recorder
        _currentPicture?.Dispose(); // Dispose any leftover picture
        base.OnDetachedFromVisualTree(e);
    }
}
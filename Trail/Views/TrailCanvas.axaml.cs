using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia.Rendering;
using Avalonia.Threading;
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
    private int frameTime = 10;
    private DispatcherTimer _animationTimer;

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
        base.Render(context); // Call base implementation

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
        // The correct way to get the ISkiaSharpApiLeaseFeature is directly from the DrawingContext if it's an ISkiaDrawingContext
        // Or use an extension method depending on Avalonia version.
        // As of Avalonia 11, the DrawingContext has a TryGetFeature method or can be cast.

        // The simplest and most robust way is to check if it's an ISkiaDrawingContext
        var skiaFeature = context.PlatformImpl?.Get	   // This extension method comes from Avalonia.Skia.Rendering.DrawingContextImplExtensions
                                                <ISkiaSharpApiLeaseFeature>();
        if (context is ISkiaDrawingContext skiaContext)
        {
            using (var lease = skiaContext.Lease())
            {
                SKCanvas avaloniaCanvas = lease.SkCanvas;
                if (avaloniaCanvas != null)
                {
                    avaloniaCanvas.DrawBitmap(_trailBitmap,
                                              new SKRect(0, 0, _trailBitmap.Width, _trailBitmap.Height),
                                              new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height),
                                              null);
                }
            }
        }
        else
        {
            // Fallback or error handling if not using Skia backend or older Avalonia version
            Debug.WriteLine("Warning: Not using Skia backend for drawing context or unsupported Avalonia version.");
        }
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
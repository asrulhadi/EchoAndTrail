using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using SkiaSharp;
using System;

namespace AvaloniaSkiaTrail;


// This class encapsulates the actual SkiaSharp drawing logic.
// It implements Avalonia's IDrawOperation interface.
internal class TrailDrawOperation : ICustomDrawOperation
{
    // Properties that the drawing operation needs
    private SKBitmap _trailBitmap;
    private SKRect _bounds; // Bounds of the drawing operation in device-independent pixels

    public TrailDrawOperation(SKRect bounds, SKBitmap trailBitmap)
    {
        _bounds = bounds;
        _trailBitmap = trailBitmap;
        // The Bounds property is required by the IDrawOperation interface.
        // It should be the area in which this operation will draw, in device-independent pixels.
        Bounds = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    public Rect Bounds { get; }
    public bool HitTest(Point p) => Bounds.Contains(p); // Basic hit testing

    // This is where Avalonia gives you the SKCanvas
    public void Dispose()
    {
        // Dispose of any temporary resources created *within* the Draw method if necessary.
        // _trailBitmap is managed by TrailCanvas, so we don't dispose it here.
    }

    // This method is called by Avalonia when it's time to perform the drawing.
    public void Render(ImmediateDrawingContext context)
    {
        //var leaseFeature = context.PlatformImpl.GetFeature<ISki() .TryGetFeature<ISkiaSharpApiLeaseFeature>();

        // The context here is IDrawingContextImpl, which for Skia is an ISkiaDrawingContextImpl.
        // We need to cast it to get the SKCanvas.
        /*if (context is ISkiaDrawingContextImpl skiaContext)
        {
            // ISkiaDrawingContextImpl has a property called SkCanvas.
            SKCanvas avaloniaCanvas = skiaContext.SkCanvas;

            if (avaloniaCanvas != null)
            {
                // Draw the _trailBitmap onto Avalonia's SKCanvas.
                // Source rect from bitmap (physical pixels)
                // Destination rect on Avalonia canvas (device-independent pixels)
                avaloniaCanvas.DrawBitmap(_trailBitmap,
                                          new SKRect(0, 0, _trailBitmap.Width, _trailBitmap.Height),
                                          new SKRect(_bounds.Left, _bounds.Top, _bounds.Width, _bounds.Height),
                                          null);
            }
        }
        else
        {
            // This branch should ideally not be hit if Avalonia is using Skia.
            // Could draw a fallback (e.g., a red X) to indicate an issue.
        }*/
    }

    public bool Equals(ICustomDrawOperation? other)
    {
        throw new NotImplementedException();
    }
}
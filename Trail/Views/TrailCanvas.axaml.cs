using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Skia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
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
    // private SKPoint _currentCirclePosition;
    private SKPath _circlePath = SKPath.ParseSvgPathData("M 40 20 A 20 20 90 0 0 0 20 A 20 20 90 0 0 40 20 Z"); // Path for the object to draw
    private SKPath _shipPath = SKPath.ParseSvgPathData("M 0 50 H 20 V 20 Q 20 10 10 0 Q 0 10 0 20 Z"); // Path for the object to draw
    private SKPath _objectPath;
    private Random _random;
    private int _circleRadius = 20;
    private Vector3D _position = new(0, 0, 0); // Initial position of the ship in 3D space
    private Vector3D shipVelocity = new(0, 0, 0); // Initial velocity of the ship in 3D space

    // Colors
    private SKColor _echoColor = new SKColor(0xFC, 0xEE, 0x03, 0xFF); // Yellow-ish color
    private SKColor _backgroundColor = SKColors.Black;
    private SKColor _trailColor = new SKColor(0x63, 0xB5, 0xB5, 0xFF); // Teal-ish color
    private SKColor _fadeToBackgroundColor = SKColors.Black;
    private float[] fadeToBackgroundMatrix = [];

    // Timer for updates
    private int frameTime = 30;     // in miliseconds
    private int trailLength = 3;   // in seconds
    private DispatcherTimer _animationTimer;

    // lock to prevent memory violation
    private Object drawLock = new();

    private Action<object?, SKCanvas> DrawTrail = (_,_) => { }; // Action to draw the trail

    private Action UpdateObjectPosition = () => { }; // Action to update the position of the circle

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
                _position = new Vector3D(Bounds.Width / 2, Bounds.Height / 2, 0); // Initial ship position
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
                if (selectedItem!.Background is ImmutableSolidColorBrush { Color: Color parsedColor })
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
                if (selectedItem!.Background is ImmutableSolidColorBrush { Color: Color parsedColor })
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
                if (selectedItem!.Background is ImmutableSolidColorBrush { Color: Color parsedColor })
                {
                    _trailColor = parsedColor.ToSKColor();
                    Console.WriteLine("Trail color set to: {0} {1}", _trailColor, parsedColor);
                    UpdateTrailLengthColor();
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
        DrawingMethodComboBox.SelectionChanged += (sender, e) =>
        {
            if (DrawingMethodComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                DrawTrail = (selectedItem.Content?.ToString()) switch
                {
                    "Fading Alpha" => DrawTrailFadeAlpha,// Use alpha fading
                    "Fading Color" => DrawTrailFadeColor,// Use color fading
                    _ => DrawTrailFadeColor,// Default to alpha fading
                };
                Console.WriteLine("Drawing method set to: {0}", selectedItem.Content);
            }
        };
        DrawTrail = DrawTrailFadeColor; // Assign the drawing action
        TrailSKCanvas.Draw += (s, e) => { lock (drawLock) { DrawTrail(s, e.Canvas); } };

        // display
        ObjectMovementComboBox.SelectionChanged += (sender, e) =>
        {
            if (ObjectMovementComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                (Action, SKPath) a = (selectedItem.Content?.ToString()) switch
                {
                    "Random Circle" => (UpdateCirclePosition, _circlePath), // Use random circle movement
                    "Ship Drift" => (UpdateShipDriftPosition, _shipPath), // Use ship drift movement
                    _ => (UpdateCirclePosition, _circlePath), // Default to random circle movement
                };
                (UpdateObjectPosition, _objectPath) = a; // Set the update action
                Console.WriteLine("Object movement set to: {0}", selectedItem.Content);
            }
        };
        _objectPath = _circlePath; // Default to circle path
        UpdateObjectPosition = UpdateCirclePosition;
    }

    private void UpdateTrailLengthColor()
    {
        // calculate alpha Af = 255x(1-(TargetAlpha/InitialAlpha)^(1/N)))
        var noOfFrames = trailLength * 1000 / frameTime;
        double fadeAmount = 1.0 - Math.Pow(1.0 / 255.0, 1.0 / noOfFrames); // Convert to a value between 0 and 1
        byte startAlpha = (byte)Math.Round(255 * fadeAmount, 0, MidpointRounding.AwayFromZero);
        _fadeToBackgroundColor = _backgroundColor.WithAlpha(startAlpha); // Semi-transparent to background (alpha 5)
        fadeToBackgroundMatrix = CreateFadeToBackgroundMatrix((float)fadeAmount);
        Console.WriteLine("Fade amount={0:N3} Starting Alpha={1}", fadeAmount, startAlpha);
    }

    private void InitializeTrailBitmap()
    {
        if (_trailBitmap is { }) new SKCanvas(_trailBitmap).Clear(_backgroundColor);
        if (_echoBitmap is { }) new SKCanvas(_echoBitmap).Clear(_backgroundColor);
    }

    private void UpdateShipDriftPosition()
    {
        static double NormalizeAngle(double angle)
        {
            while (angle > Math.PI) angle -= 2 * Math.PI;
            while (angle < -Math.PI) angle += 2 * Math.PI;
            return angle;
        }

        // Ship and environment parameters
        const double m = 50000.0;         // mass (kg)
        const double I_z = 8e6;           // moment of inertia (kg*m^2)
        const double X_u = 80000.0;       // surge damping (N/(m/s))
        const double Y_v = 100000.0;      // sway damping (N/(m/s))
        const double N_r = 5e6;           // yaw damping (Nm/(rad/s))
        const double N_delta = 3e6;       // asymmetry yaw restoring moment (Nm)

        // Current in global frame (m/s)
        const double U_x = -0.5;  //1.5;           // eastward
        const double U_y = 0.5;//0.5;           // northward

        // Initial state variables
        double u = shipVelocity.X, v = shipVelocity.Y, r = shipVelocity.Z;           // velocities in body frame
        double x = _position.X, y = _position.Y, psi = _position.Z;         // position and heading in global frame

        double dt = 1;                      // timestep (s)
                                            // int steps = 1000;

        double theta_current = Math.Atan2(U_x, U_y); // direction of current

        // Transform current to body frame
        double cosPsi = Math.Cos(psi);
        double sinPsi = Math.Sin(psi);
        double u_c = cosPsi * U_x + sinPsi * U_y;
        double v_c = -sinPsi * U_x + cosPsi * U_y;

        // Relative velocities
        double u_rel = u - u_c;
        double v_rel = v - v_c;

        // Dynamics equations (Euler integration)
        double du = (v * r - (X_u / m) * u_rel) * dt;
        double dv = (-u * r - (Y_v / m) * v_rel) * dt;
        double dr = ((-N_r * r + N_delta * Math.Sin(psi - theta_current)) / I_z) * dt;

        u += du;
        v += dv;
        r += dr;
        psi += r * dt;

        // Normalize heading angle
        psi = NormalizeAngle(psi);

        // Kinematic equations (global position update)
        double dx = (u * cosPsi - v * sinPsi) * dt;
        double dy = (u * sinPsi + v * cosPsi) * dt;

        x += dx;
        y += dy;

        // Update the current circle position based on the ship's position
        _position = new Vector3D(x, y, psi);
        shipVelocity = new Vector3D(u, v, r);
    }

    private void UpdateCirclePosition()
    {
        float radius = _circlePath.Bounds.Width / 2; // Radius of the circle in device-independent pixels
        float moveAmount = radius / 2; // Max pixels to move per step (in device-independent pixels)

        var _x = _position.X + (_random.NextDouble() * 2 - 1) * moveAmount;
        var _y = _position.Y + (_random.NextDouble() * 2 - 1) * moveAmount;

        // Clamp position to stay within control bounds (device-independent pixels)
        _x = Math.Max(radius, Math.Min((float)Bounds.Width - radius, _x));
        _y = Math.Max(radius, Math.Min((float)Bounds.Height - radius, _y));

        _position = new Vector3D(_x, _y, 0); // Update position in 3D space
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        // Console.WriteLine("{0,20} Called {1} canvas = {2} - {3}", "TimerTickA", DateTime.Now.ToString("mm:ss.fff"), _trailCanvas?.GetHashCode(), _echoCanvas?.GetHashCode());
        UpdateObjectPosition?.Invoke();
        TrailSKCanvas.InvalidateVisual();   // Request a redraw of this control
        // Console.WriteLine("{0,20} Called {1} canvas = {2} - {3}", "TimerTickB", DateTime.Now.ToString("mm:ss.fff"), _trailCanvas?.GetHashCode(), _echoCanvas?.GetHashCode());
    }

    // Override the Render method to perform custom drawing
    public override void Render(DrawingContext context)
    {
        base.Render(context); // Call base implementation
        Console.WriteLine("{0,20} Called {1} context = {2}", "Render", DateTime.Now.ToString("mm:ss.fff"), context?.GetHashCode());
    }

    private float[] CreateFadeToBackgroundMatrix(float fadeAmount)
    {
        float inverseFade = 1 - fadeAmount;  // 1 = full image, 0 = black

        // Normalize target color to 0-1 range
        float targetR = _backgroundColor.Red / 255f;
        float targetG = _backgroundColor.Green / 255f;
        float targetB = _backgroundColor.Blue / 255f;

        return [
            inverseFade, 0, 0, 0, fadeAmount * targetR,   // Red channel scaled
            0, inverseFade, 0, 0, fadeAmount * targetG,   // Green channel scaled
            0, 0, inverseFade, 0, fadeAmount * targetB,   // Blue channel scaled
            0, 0, 0, 1, 0         // Alpha unchanged
        ];
    }

    private void DrawTrailFadeColor(object? source, SKCanvas canvas)
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
            {
                using var paint = new SKPaint();
                paint.ColorFilter = SKColorFilter.CreateColorMatrix(fadeToBackgroundMatrix);
                drawingCanvas.DrawBitmap(bitmap, 0, 0, paint);
            }
            else
                drawingCanvas.Clear(SKColors.Transparent);

            // 2. Draw the new Trail circle: This creates the new "head" of the trail.
            using (var circlePaint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill })
            {
                // When drawing to the SKBitmap, the coordinates need to be in physical pixels.
                // We stored _currentCirclePosition in device-independent pixels, so we need to scale.
                double scaling = VisualRoot?.RenderScaling ?? 1.0;
                float scaledX = (float)(_position.X * scaling);
                float scaledY = (float)(_position.Y * scaling);
                float scaledRadius = (float)(_circleRadius * scaling);

                // drawingCanvas.DrawCircle(scaledX, scaledY, scaledRadius, circlePaint);
                drawingCanvas.Save();
                drawingCanvas.Translate(scaledX, scaledY); // Move to the circle position
                drawingCanvas.RotateRadians((float)_position.Z); // Rotate by the angle in radians
                drawingCanvas.DrawPath(_objectPath, circlePaint); // Draw the object path if needed
                drawingCanvas.Restore();
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

    private void DrawTrailFadeAlpha(object? source, SKCanvas canvas)
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
            {
                using (var fadePaint = new SKPaint { Color = _fadeToBackgroundColor, BlendMode = SKBlendMode.SrcOver })
                {
                    drawingCanvas.DrawRect(0, 0, bitmap.Width, bitmap.Height, fadePaint);
                }
            }
            else
                drawingCanvas.Clear(SKColors.Transparent);

            // 2. Draw the new Trail circle: This creates the new "head" of the trail.
            using (var circlePaint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill })
            {
                // When drawing to the SKBitmap, the coordinates need to be in physical pixels.
                // We stored _currentCirclePosition in device-independent pixels, so we need to scale.
                double scaling = VisualRoot?.RenderScaling ?? 1.0;
                float scaledX = (float)(_position.X * scaling);
                float scaledY = (float)(_position.Y * scaling);
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
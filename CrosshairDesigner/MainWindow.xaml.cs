using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Forms;

namespace CrosshairDesigner
{
    public partial class MainWindow : Window
    {
        // Crosshair state
        private bool _isCrosshairVisible = false;
        private double _zoomLevel = 1.0;
        private Point _panOrigin;
        private Point _panStart;
        private bool _isPanning = false;
        
        // Crosshair properties
        private Color _crosshairColor = Colors.Lime;
        private double _crosshairSize = 10;
        private double _crosshairThickness = 2;
        private double _crosshairOpacity = 1.0;
        private double _crosshairGap = 0;
        private bool _showCenterDot = false;
        private bool _showOutline = false;
        private string _currentStyle = "Cross";
        
        // Overlay window for actual crosshair
        private Window _overlayWindow;
        private HwndSource? _source;
        
        // Hotkey registration
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        private const int HOTKEY_ID = 9000;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_ALT = 0x0008;
        private const uint VK_Z = 0x5A;
        
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            
            // Initialize overlay window
            InitializeOverlayWindow();
        }
        
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Register hotkey
            var helper = new WindowInteropHelper(this);
            _source = HwndSource.FromHwnd(helper.Handle);
            _source?.AddHook(HwndHook);
            RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_SHIFT | MOD_ALT, VK_Z);
            
            // Draw initial coordinate system and crosshair
            DrawCoordinateSystem();
            DrawCrosshair();
        }
        
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // Unregister hotkey
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
            
            // Close overlay window
            _overlayWindow?.Close();
        }
        
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleCrosshair();
                handled = true;
            }
            
            return IntPtr.Zero;
        }
        
        private void InitializeOverlayWindow()
        {
            _overlayWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                Width = SystemParameters.PrimaryScreenWidth,
                Height = SystemParameters.PrimaryScreenHeight,
                Left = 0,
                Top = 0,
                WindowState = WindowState.Maximized
            };
            
            var canvas = new Canvas();
            _overlayWindow.Content = canvas;
        }
        
        private void ToggleCrosshair()
        {
            _isCrosshairVisible = !_isCrosshairVisible;
            CrosshairToggle.IsChecked = _isCrosshairVisible;
            UpdateOverlayCrosshair();
        }
        
        private void CrosshairToggle_Checked(object sender, RoutedEventArgs e)
        {
            _isCrosshairVisible = true;
            UpdateOverlayCrosshair();
        }
        
        private void CrosshairToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _isCrosshairVisible = false;
            UpdateOverlayCrosshair();
        }
        
        private void UpdateOverlayCrosshair()
        {
            if (_isCrosshairVisible)
            {
                if (!_overlayWindow.IsVisible)
                {
                    _overlayWindow.Show();
                }
                
                var canvas = _overlayWindow.Content as Canvas;
                if (canvas != null)
                {
                    canvas.Children.Clear();
                    
                    double centerX = SystemParameters.PrimaryScreenWidth / 2;
                    double centerY = SystemParameters.PrimaryScreenHeight / 2;
                    
                    DrawCrosshairOnCanvas(canvas, centerX, centerY, true);
                }
            }
            else
            {
                _overlayWindow.Hide();
            }
        }
        
        private void DrawCoordinateSystem()
        {
            CoordinateCanvas.Children.Clear();
            
            double width = PreviewGrid.ActualWidth;
            double height = PreviewGrid.ActualHeight;
            double centerX = width / 2;
            double centerY = height / 2;
            
            // Draw grid lines
            Pen gridPen = new Pen(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), 1);
            
            // Vertical lines
            for (double x = centerX % 50; x < width; x += 50 * _zoomLevel)
            {
                if (x >= 0 && x <= width)
                {
                    Line vLine = new Line
                    {
                        X1 = x, Y1 = 0,
                        X2 = x, Y2 = height,
                        Stroke = gridPen.Brush,
                        StrokeThickness = 1
                    };
                    CoordinateCanvas.Children.Add(vLine);
                }
            }
            
            // Horizontal lines
            for (double y = centerY % 50; y < height; y += 50 * _zoomLevel)
            {
                if (y >= 0 && y <= height)
                {
                    Line hLine = new Line
                    {
                        X1 = 0, Y1 = y,
                        X2 = width, Y2 = y,
                        Stroke = gridPen.Brush,
                        StrokeThickness = 1
                    };
                    CoordinateCanvas.Children.Add(hLine);
                }
            }
            
            // Draw center axes
            Pen axisPen = new Pen(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), 2);
            
            Line xAxis = new Line
            {
                X1 = 0, Y1 = centerY,
                X2 = width, Y2 = centerY,
                Stroke = axisPen.Brush,
                StrokeThickness = 2
            };
            CoordinateCanvas.Children.Add(xAxis);
            
            Line yAxis = new Line
            {
                X1 = centerX, Y1 = 0,
                X2 = centerX, Y2 = height,
                Stroke = axisPen.Brush,
                StrokeThickness = 2
            };
            CoordinateCanvas.Children.Add(yAxis);
        }
        
        private void DrawCrosshair()
        {
            CrosshairCanvas.Children.Clear();
            
            double width = PreviewGrid.ActualWidth;
            double height = PreviewGrid.ActualHeight;
            double centerX = width / 2;
            double centerY = height / 2;
            
            DrawCrosshairOnCanvas(CrosshairCanvas, centerX, centerY, false);
            
            // Update info text
            CrosshairInfoText.Text = $"X: 0 | Y: 0";
        }
        
        private void DrawCrosshairOnCanvas(Canvas canvas, double centerX, double centerY, bool isOverlay)
        {
            double size = isOverlay ? _crosshairSize : _crosshairSize * _zoomLevel;
            double thickness = isOverlay ? _crosshairThickness : _crosshairThickness * _zoomLevel;
            double gap = isOverlay ? _crosshairGap : _crosshairGap * _zoomLevel;
            
            Brush crosshairBrush = new SolidColorBrush(_crosshairColor) { Opacity = _crosshairOpacity };
            Pen crosshairPen = new Pen(crosshairBrush, thickness);
            
            if (_showOutline)
            {
                // Draw outline first (black)
                Brush outlineBrush = new SolidColorBrush(Colors.Black) { Opacity = _crosshairOpacity };
                Pen outlinePen = new Pen(outlineBrush, thickness + 2);
                
                switch (_currentStyle)
                {
                    case "Cross":
                        DrawCrossShape(canvas, centerX, centerY, size, outlinePen, gap);
                        break;
                    case "Dot":
                        DrawDotShape(canvas, centerX, centerY, size * 0.3, outlineBrush);
                        break;
                    case "Circle":
                        DrawCircleShape(canvas, centerX, centerY, size, outlinePen);
                        break;
                    case "Plus":
                        DrawPlusShape(canvas, centerX, centerY, size, outlinePen, gap);
                        break;
                }
            }
            
            // Draw actual crosshair
            switch (_currentStyle)
            {
                case "Cross":
                    DrawCrossShape(canvas, centerX, centerY, size, crosshairPen, gap);
                    break;
                case "Dot":
                    DrawDotShape(canvas, centerX, centerY, size * 0.3, crosshairBrush);
                    break;
                case "Circle":
                    DrawCircleShape(canvas, centerX, centerY, size, crosshairPen);
                    break;
                case "Plus":
                    DrawPlusShape(canvas, centerX, centerY, size, crosshairPen, gap);
                    break;
            }
            
            // Center dot if enabled
            if (_showCenterDot)
            {
                double dotSize = isOverlay ? 4 : 4 * _zoomLevel;
                DrawDotShape(canvas, centerX, centerY, dotSize, crosshairBrush);
            }
        }
        
        private void DrawCrossShape(Canvas canvas, double centerX, double centerY, double size, Pen pen, double gap)
        {
            // Top-left to center
            Line tl = new Line
            {
                X1 = centerX - size - gap, Y1 = centerY - size - gap,
                X2 = centerX - gap, Y2 = centerY - gap,
                Stroke = pen.Brush,
                StrokeThickness = ((Pen)pen).Thickness
            };
            canvas.Children.Add(tl);
            
            // Bottom-right from center
            Line br = new Line
            {
                X1 = centerX + gap, Y1 = centerY + gap,
                X2 = centerX + size + gap, Y2 = centerY + size + gap,
                Stroke = pen.Brush,
                StrokeThickness = ((Pen)pen).Thickness
            };
            canvas.Children.Add(br);
            
            // Top-right to center
            Line tr = new Line
            {
                X1 = centerX + size + gap, Y1 = centerY - size - gap,
                X2 = centerX + gap, Y2 = centerY - gap,
                Stroke = pen.Brush,
                StrokeThickness = ((Pen)pen).Thickness
            };
            canvas.Children.Add(tr);
            
            // Bottom-left from center
            Line bl = new Line
            {
                X1 = centerX - gap, Y1 = centerY + gap,
                X2 = centerX - size - gap, Y2 = centerY + size + gap,
                Stroke = pen.Brush,
                StrokeThickness = ((Pen)pen).Thickness
            };
            canvas.Children.Add(bl);
        }
        
        private void DrawDotShape(Canvas canvas, double centerX, double centerY, double size, Brush brush)
        {
            Ellipse dot = new Ellipse
            {
                Width = size * 2,
                Height = size * 2,
                Fill = brush
            };
            Canvas.SetLeft(dot, centerX - size);
            Canvas.SetTop(dot, centerY - size);
            canvas.Children.Add(dot);
        }
        
        private void DrawCircleShape(Canvas canvas, double centerX, double centerY, double size, Pen pen)
        {
            Ellipse circle = new Ellipse
            {
                Width = size * 2,
                Height = size * 2,
                Stroke = pen.Brush,
                StrokeThickness = ((Pen)pen).Thickness,
                Fill = null
            };
            Canvas.SetLeft(circle, centerX - size);
            Canvas.SetTop(circle, centerY - size);
            canvas.Children.Add(circle);
        }
        
        private void DrawPlusShape(Canvas canvas, double centerX, double centerY, double size, Pen pen, double gap)
        {
            // Horizontal line
            Line hLine = new Line
            {
                X1 = centerX - size - gap, Y1 = centerY,
                X2 = centerX + size + gap, Y2 = centerY,
                Stroke = pen.Brush,
                StrokeThickness = ((Pen)pen).Thickness
            };
            canvas.Children.Add(hLine);
            
            // Vertical line
            Line vLine = new Line
            {
                X1 = centerX, Y1 = centerY - size - gap,
                X2 = centerX, Y2 = centerY + size + gap,
                Stroke = pen.Brush,
                StrokeThickness = ((Pen)pen).Thickness
            };
            canvas.Children.Add(vLine);
        }
        
        // Zoom handling
        private void PreviewGrid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomDelta = e.Delta > 0 ? 0.1 : -0.1;
            _zoomLevel = Math.Max(0.1, Math.Min(5.0, _zoomLevel + zoomDelta));
            
            ZoomLevelText.Text = $"Zoom: {_zoomLevel * 100:F0}%";
            
            DrawCoordinateSystem();
            DrawCrosshair();
        }
        
        // Pan handling
        private void PreviewGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed || 
                (e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.Space)))
            {
                _isPanning = true;
                _panStart = e.GetPosition(PreviewGrid);
                _panOrigin = new Point(
                    Canvas.GetLeft(CoordinateCanvas),
                    Canvas.GetTop(CoordinateCanvas)
                );
                PreviewGrid.Cursor = Cursors.SizeAll;
                e.Handled = true;
            }
        }
        
        private void PreviewGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point currentPos = e.GetPosition(PreviewGrid);
                double deltaX = currentPos.X - _panStart.X;
                double deltaY = currentPos.Y - _panStart.Y;
                
                Canvas.SetLeft(CoordinateCanvas, _panOrigin.X + deltaX);
                Canvas.SetTop(CoordinateCanvas, _panOrigin.Y + deltaY);
                
                DrawCrosshair();
            }
            
            // Update coordinate display
            Point mousePos = e.GetPosition(CrosshairCanvas);
            double centerX = PreviewGrid.ActualWidth / 2;
            double centerY = PreviewGrid.ActualHeight / 2;
            int relX = (int)(mousePos.X - centerX);
            int relY = (int)(mousePos.Y - centerY);
            CrosshairInfoText.Text = $"X: {relX} | Y: {relY}";
        }
        
        private void PreviewGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            PreviewGrid.Cursor = Cursors.Arrow;
        }
        
        // Settings event handlers
        private void StyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StyleComboBox.SelectedItem is ComboBoxItem item)
            {
                _currentStyle = item.Content.ToString() ?? "Cross";
                DrawCrosshair();
                UpdateOverlayCrosshair();
            }
        }
        
        private void ChooseColorButton_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new ColorDialog
            {
                Color = System.Drawing.Color.FromArgb(_crosshairColor.R, _crosshairColor.G, _crosshairColor.B),
                FullOpen = true
            };
            
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                _crosshairColor = Color.FromRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
                ColorPreviewBorder.Background = new SolidColorBrush(_crosshairColor);
                DrawCrosshair();
                UpdateOverlayCrosshair();
            }
        }
        
        private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_crosshairSize != e.NewValue)
            {
                _crosshairSize = e.NewValue;
                SizeValueText.Text = $"Size: {_crosshairSize:F0}";
                DrawCrosshair();
                UpdateOverlayCrosshair();
            }
        }
        
        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_crosshairThickness != e.NewValue)
            {
                _crosshairThickness = e.NewValue;
                ThicknessValueText.Text = $"Thickness: {_crosshairThickness:F0}";
                DrawCrosshair();
                UpdateOverlayCrosshair();
            }
        }
        
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_crosshairOpacity != e.NewValue)
            {
                _crosshairOpacity = e.NewValue;
                OpacityValueText.Text = $"Opacity: {_crosshairOpacity * 100:F0}%";
                DrawCrosshair();
                UpdateOverlayCrosshair();
            }
        }
        
        private void GapSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_crosshairGap != e.NewValue)
            {
                _crosshairGap = e.NewValue;
                GapValueText.Text = $"Gap: {_crosshairGap:F0}";
                DrawCrosshair();
                UpdateOverlayCrosshair();
            }
        }
        
        private void CenterDotCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _showCenterDot = true;
            DrawCrosshair();
            UpdateOverlayCrosshair();
        }
        
        private void CenterDotCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _showCenterDot = false;
            DrawCrosshair();
            UpdateOverlayCrosshair();
        }
        
        private void OutlineCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _showOutline = true;
            DrawCrosshair();
            UpdateOverlayCrosshair();
        }
        
        private void OutlineCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _showOutline = false;
            DrawCrosshair();
            UpdateOverlayCrosshair();
        }
        
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _crosshairColor = Colors.Lime;
            _crosshairSize = 10;
            _crosshairThickness = 2;
            _crosshairOpacity = 1.0;
            _crosshairGap = 0;
            _showCenterDot = false;
            _showOutline = false;
            _currentStyle = "Cross";
            
            StyleComboBox.SelectedIndex = 0;
            SizeSlider.Value = 10;
            ThicknessSlider.Value = 2;
            OpacitySlider.Value = 1.0;
            GapSlider.Value = 0;
            CenterDotCheckBox.IsChecked = false;
            OutlineCheckBox.IsChecked = false;
            ColorPreviewBorder.Background = new SolidColorBrush(Colors.Lime);
            
            DrawCrosshair();
            UpdateOverlayCrosshair();
        }
        
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

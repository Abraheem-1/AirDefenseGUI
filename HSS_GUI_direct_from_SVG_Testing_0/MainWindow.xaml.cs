using System;
using Timer = System.Timers.Timer;
using Forms = System.Windows.Forms;
using WpfControls = System.Windows.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Drawing.Imaging;
using System.IO;
using System.ComponentModel; // Required for CancelEventArgs
using System.Windows.Interop;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Threading;
using System.Windows.Threading;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Linq;




namespace DefenseControlSystem
{
    public class OperationData
    {
        public string Mode { get; set; }
        public string Target { get; set; }
        public string Color { get; set; }
    }


    public partial class MainWindow : System.Windows.Window
    {
        private Timer rangeUpdateTimer;
        private Random random;

        private Dictionary<string, OperationData> operationsData = new Dictionary<string, OperationData>();

        public MainWindow()
        {
            InitializeComponent();
            StartCamera();
            InitMockRangeData();
            InitMockStatusData();
            InitMockLogs();
            InitMockRadarDirection();

            // manual rotation left and right timer setup here:
            manualRotateTimer = new DispatcherTimer();
            manualRotateTimer.Interval = TimeSpan.FromMilliseconds(50);
            manualRotateTimer.Tick += (s, e) =>
            {
                currentAngle = (currentAngle + 3 * manualRotateDirection + 360) % 360;
                UpdateGunDirection();
            };

            // manual rotation up and down timer setup here:
            elevationTimer = new DispatcherTimer();
            elevationTimer.Interval = TimeSpan.FromMilliseconds(50);
            elevationTimer.Tick += (s, e) =>
            {
                elevationAngle = Math.Clamp(elevationAngle + 1 * elevationDirection, -90, 90);
                UpdateElevationDisplay();
            };

            /*// for the mock camera
            cameraTimer = new DispatcherTimer();
            cameraTimer.Interval = TimeSpan.FromSeconds(2);
            cameraTimer.Tick += (s, e) =>
            {
                try
                {
                    string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, mockImagePaths[currentImageIndex]);
                    CameraImage.Source = new BitmapImage(new Uri(path, UriKind.Absolute));

                    currentImageIndex = (currentImageIndex + 1) % mockImagePaths.Length;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading image: " + ex.Message);
                }
            };
            cameraTimer.Start();*/

            // Start the camera

            // Call AFTER components are loaded
            this.Loaded += (s, e) => DetectModeRadio_Checked(null, null);

        }

        private void InitMockRangeData()
        {
            random = new Random();

            rangeUpdateTimer = new Timer(1000); // every 1 second
            rangeUpdateTimer.Elapsed += (s, e) =>
            {
                int range = random.Next(100, 301); // 100 to 300 meters
                Dispatcher.Invoke(() =>
                {
                    RangeTextBlock.Text = $"Menzil: {range} m";
                });
            };
            rangeUpdateTimer.Start();
        }

        private Timer statusTimer;
        private bool isSystemActive = true;
        private bool isConnected = true;

        private void InitMockStatusData()
        {
            statusTimer = new Timer(3000); // every 3 seconds
            statusTimer.Elapsed += (s, e) =>
            {
                // Toggle system and connection status
                isSystemActive = !isSystemActive;
                isConnected = !isConnected;

                Dispatcher.Invoke(() =>
                {
                    // Update System Status
                    SystemStatusText.Text = isSystemActive ? "Aktif" : "Kapalı";
                    SystemStatusText.Foreground = (System.Windows.Media.Brush)(isSystemActive
                        ? Resources["SuccessGreen"]
                        : Resources["ErrorRed"]);
                    SystemStatusEllipse.Fill = (System.Windows.Media.Brush)(isSystemActive
                        ? Resources["SuccessGreen"]
                        : Resources["ErrorRed"]);

                    // Update Connection Status
                    ConnectionStatusText.Text = isConnected ? "Bağlı" : "Bağlantı Yok";
                    ConnectionStatusText.Foreground = (System.Windows.Media.Brush)(isConnected
                        ? Resources["SuccessGreen"]
                        : Resources["ErrorRed"]);
                    ConnectionStatusEllipse.Fill = (System.Windows.Media.Brush)(isConnected
                        ? Resources["SuccessGreen"]
                        : Resources["ErrorRed"]);
                });
            };
            statusTimer.Start();
        }

        private Timer logTimer;
        private string[] sampleLogs = new string[]
        {
            "Sistem başlatıldı",
            "Kameralar bağlandı",
            "Motorlar çalışır durumda",
            "Kalibrasyon tamamlandı",
            "Hedef tespit edildi: Balon",
            "Uyarı: Hedef yasak bölgeye yaklaşıyor",
            "Hedef yok oldu",
            "Sistem duraklatıldı",
            "Bağlantı yeniden kuruldu"
        };

        private void InitMockLogs()
        {
            logTimer = new Timer(4000); // every 4 seconds
            logTimer.Elapsed += (s, e) =>
            {
                string logMessage = sampleLogs[new Random().Next(sampleLogs.Length)];
                string timestamp = DateTime.Now.ToString("HH:mm:ss");

                Dispatcher.Invoke(() =>
                {
                    TextBlock logText = new TextBlock
                    {
                        Text = $"[{timestamp}] {logMessage}",
                        Style = (Style)Resources["LogTextStyle"],
                        Margin = new Thickness(0, 0, 0, 5)
                    };

                    // Highlight if it's an alert
                    if (logMessage.StartsWith("Uyarı"))
                        logText.Foreground = (System.Windows.Media.Brush)Resources["WarningYellow"];
                    else if (logMessage.Contains("Hedef"))
                        logText.Foreground = (System.Windows.Media.Brush)Resources["SuccessGreen"];

                    LogStackPanel.Children.Add(logText);
                });
            };
            logTimer.Start();
        }

        private Timer directionTimer;
        private int currentAngle = 0;

        private void InitMockRadarDirection()
        {
            directionTimer = new Timer(1000); // every 1 second
            directionTimer.Elapsed += (s, e) =>
            {
                currentAngle = (currentAngle + 15) % 360; // rotate 15° each time

                Dispatcher.Invoke(UpdateGunDirection);
            };
            directionTimer.Start();
        }

        private void FireButton_Click(object sender, RoutedEventArgs e)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");

            TextBlock logText = new TextBlock
            {
                Text = $"[{timestamp}] Ateş komutu gönderildi",
                Style = (Style)Resources["LogTextStyle"],
                Margin = new Thickness(0, 0, 0, 5),
                Foreground = (System.Windows.Media.Brush)Resources["ErrorRed"]
            };

            LogStackPanel.Children.Add(logText);
        }

        private void LeftButton_Click(object sender, RoutedEventArgs e)
        {
            currentAngle = (currentAngle - 10 + 360) % 360;
            UpdateGunDirection();
        }

        private void RightButton_Click(object sender, RoutedEventArgs e)
        {
            currentAngle = (currentAngle + 10) % 360;
            UpdateGunDirection();
        }

        private void UpdateGunDirection()
        {
            GunDirectionRotate.Angle = currentAngle;
            GunAngleText.Text = $"{currentAngle}°";

            double length = 100;
            double radians = (currentAngle - 90) * Math.PI / 180;

            double x = length * Math.Cos(radians);
            double y = length * Math.Sin(radians);

            Canvas.SetLeft(GunAngleText, x - 10);
            Canvas.SetTop(GunAngleText, y - 10);
        }

        private DispatcherTimer manualRotateTimer;
        private int manualRotateDirection = 0; // -1 for left, +1 for right

        private void LeftButton_Down(object sender, MouseButtonEventArgs e)
        {
            manualRotateDirection = -1;
            manualRotateTimer.Start();
        }

        private void RightButton_Down(object sender, MouseButtonEventArgs e)
        {
            manualRotateDirection = 1;
            manualRotateTimer.Start();
        }

        private void StopRotation(object sender, System.Windows.Input.MouseEventArgs e)
        {
            manualRotateTimer.Stop();
            manualRotateDirection = 0;
        }

        private DispatcherTimer elevationTimer;
        private int elevationDirection = 0; // +1 = up, -1 = down
        private int elevationAngle = 30; // Initial vertical angle (°)

        private void UpButton_Down(object sender, MouseButtonEventArgs e)
        {
            elevationDirection = 1;
            elevationTimer.Start();
        }

        private void DownButton_Down(object sender, MouseButtonEventArgs e)
        {
            elevationDirection = -1;
            elevationTimer.Start();
        }

        private void StopElevation(object sender, System.Windows.Input.MouseEventArgs e)
        {
            elevationTimer.Stop();
            elevationDirection = 0;
        }

        private void UpdateElevationDisplay()
        {
            // Map -90 to +90 degrees to canvas Y positions (42 to 198)
            double minY = 42;
            double maxY = 198;

            double normalized = (elevationAngle + 90) / 180.0; // convert to [0,1]
            double y = minY + (1 - normalized) * (maxY - minY);

            // Adjust Y offset here
            double arrowOffset = -94;  // shift arrow upward (~half height of polygon)
            double textOffset = -5;   // small visual centering for font

            // Move both arrow and text to same Y
            Canvas.SetTop(ElevationArrow, y + arrowOffset);
            Canvas.SetTop(ElevationAngleText, y + textOffset);

            ElevationAngleText.Text = $"{elevationAngle}°";
        }
        /*
                private readonly string[] mockImagePaths = new[]
                {
                    "Images/mock1.jpg",
                    "Images/mock2.jpg",
                    "Images/mock3.jpg"
                };

                private int currentImageIndex = 0;
                private DispatcherTimer cameraTimer;*/


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isCameraRunning = false;
            cameraThread?.Join();
            capture?.Release();
            frame?.Dispose();
        }


        private void LogMessage(string message)
        {
            TextBlock logEntry = new TextBlock
            {
                Text = $"{DateTime.Now:HH:mm:ss} - {message}",
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(5)
            };

            Dispatcher.Invoke(() =>
            {
                LogStackPanel.Children.Add(logEntry);
                if (LogStackPanel.Children.Count > 100)
                {
                    LogStackPanel.Children.RemoveAt(0);
                }
            });
        }

        private VideoCapture capture;
        private Mat frame;
        private Thread cameraThread;
        private bool isCameraRunning = false;

        private void StartCamera()
        {
            capture = new VideoCapture(0); // 0 = default camera
            if (!capture.IsOpened())
            {
                LogMessage("🚫 Kamera açılamadı.");
                return;
            }

            frame = new Mat();
            isCameraRunning = true;

            cameraThread = new Thread(() =>
            {
                while (isCameraRunning)
                {
                    capture.Read(frame);
                    if (frame.Empty())
                        continue;

                    var image = frame.ToBitmapSource();
                    image.Freeze(); // important for cross-thread use

                    Dispatcher.Invoke(() =>
                    {
                        cameraPreview.Source = image;
                    });
                }
            });

            cameraThread.IsBackground = true;
            cameraThread.Start();

            LogMessage("✅ Kamera yayını başladı.");
        }


        private List<OperationStep> savedOperations = new();

        public class OperationStep
        {
            public string Name { get; set; }
            public bool IsActive { get; set; }
            public Action ActionToExecute { get; set; } // what this step does
        }

        private void RefreshOperationsUI()
        {
            OperationsPanel.Children.Clear();

            foreach (var op in savedOperations)
            {
                var grid = new Grid { Margin = new Thickness(5) };

                var label = new TextBlock
                {
                    Text = op.Name,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(5)
                };

                var toggle = new WpfControls.Button
                {
                    Content = op.IsActive ? "AKTİF" : "PASİF",
                    Background = op.IsActive ? Brushes.Green : Brushes.Gray,
                    Margin = new Thickness(5)
                };

                toggle.Click += (s, e) =>
                {
                    op.IsActive = !op.IsActive;
                    toggle.Content = op.IsActive ? "AKTİF" : "PASİF";
                    toggle.Background = op.IsActive ? Brushes.Green : Brushes.Gray;

                    if (op.IsActive)
                        op.ActionToExecute?.Invoke();
                };

                grid.Children.Add(label);
                grid.Children.Add(toggle);
                OperationsPanel.Children.Add(grid);
            }
        }

        enum TargetMode { RedOnly, BlueOnly, All } // example
        TargetMode currentTargetMode = TargetMode.RedOnly;
        private void AddSampleOperation()
        {
            savedOperations.Add(new OperationStep
            {
                Name = "1.Aşama: Balon Rengi Ayırılmaksızın",
                IsActive = false,
                ActionToExecute = () =>
                {
                    LogMessage("🎯 Operasyon 1 aktif: Her renk balon hedefleniyor.");
                    // e.g., switch detection mode to ANY color
                    currentTargetMode = TargetMode.All;
                }
            });

            RefreshOperationsUI();
        }

        private void BtnYeniEkle_Click(object sender, RoutedEventArgs e)
        {
            AddSampleOperation();
        }

        private void BtnSil_Click(object sender, RoutedEventArgs e)
        {
            if (savedOperations.Count > 0)
                savedOperations.RemoveAt(savedOperations.Count - 1);

            RefreshOperationsUI();
        }

        private void AddOperation(string title, bool isActive)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(isActive ? Color.FromRgb(42, 53, 88) : Color.FromRgb(42, 43, 48)),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 0, 5)
            };

            var grid = new Grid();

            if (isActive)
            {
                var leftBar = new Rectangle
                {
                    Width = 5,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    Fill = Brushes.LimeGreen
                };
                grid.Children.Add(leftBar);
            }

            var titleText = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(15, 5, 80, 5),
                Foreground = Brushes.White
            };
            grid.Children.Add(titleText);

            var button = new WpfControls.Button
            {
                Content = isActive ? "AKTİF" : "Yükle",
                Width = 60,
                Height = 20,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 10, 2),
                Background = new LinearGradientBrush(Colors.Blue, Colors.DarkBlue, 45),
                Foreground = Brushes.White,
                FontSize = 11,
                BorderThickness = new Thickness(0)
            };

            grid.Children.Add(button);
            border.Child = grid;
            OperationsPanel.Children.Add(border);
        }

        private void AddOperation_Click(object sender, RoutedEventArgs e)
        {
            // Get input values
            string operationName = OperationNameTextBox.Text.Trim();
            string mode = (ModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string target = (TargetComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string color = (ColorComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

            if (string.IsNullOrEmpty(operationName))
            {
                System.Windows.MessageBox.Show("Lütfen operasyon adı girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create the container Border
            var operationBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(42, 43, 48)),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 0, 5)
            };

            // Create the Grid and columns
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) }); // green bar
            grid.ColumnDefinitions.Add(new ColumnDefinition()); // text
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // button

            // Create green indicator (initially hidden)
            var greenIndicator = new Border
            {
                Background = Brushes.LimeGreen,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(greenIndicator, 0);
            grid.Children.Add(greenIndicator);

            // Create name text
            var nameText = new TextBlock
            {
                Text = operationName,
                Style = (Style)FindResource("NormalTextStyle"),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(15, 5, 80, 5)
            };
            Grid.SetColumn(nameText, 1);
            grid.Children.Add(nameText);

            // Create load button
            var loadButton = new WpfControls.Button
            {
                Content = "Yükle",
                Width = 60,
                Height = 20,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 10, 2),
                Background = (Brush)FindResource("ButtonGradient"),
                Foreground = Brushes.White,
                FontSize = 11,
                BorderThickness = new Thickness(0),
                Tag = new Tuple<Border, OperationData>(
                     operationBorder,
                     new OperationData
                     {
                         Mode = mode,
                         Target = target,
                         Color = color
                     })
            };
            Grid.SetColumn(loadButton, 2);
            //loadButton.Click += OperationSelect_Click;
            loadButton.Click += LoadOperation_Click;
            grid.Children.Add(loadButton);

            // Store green bar for later activation
            operationBorder.Tag = greenIndicator;

            // Finalize layout
            operationBorder.Child = grid;
            operationBorder.MouseLeftButtonDown += OperationBorder_MouseLeftButtonDown;
            OperationsPanel.Children.Add(operationBorder);

            // Reset input fields
            OperationNameTextBox.Text = "";
            ModeComboBox.SelectedIndex = 0;
            TargetComboBox.SelectedIndex = 0;
            ColorComboBox.SelectedIndex = 0;
            OperationNameHint.Visibility = Visibility.Visible;

            // Hide the input panel
            OperationInputPanel.Visibility = Visibility.Collapsed;
            OperationButtonsPanel.Visibility = Visibility.Visible;
        }


        private void ShowOperationInputPanel(object sender, RoutedEventArgs e)
        {
            OperationInputPanel.Visibility = Visibility.Visible;
            OperationButtonsPanel.Visibility = Visibility.Collapsed; // hide buttons
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(OperationNameTextBox.Text))
                OperationNameHint.Visibility = Visibility.Collapsed;
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(OperationNameTextBox.Text))
                OperationNameHint.Visibility = Visibility.Visible;
        }

        private void DeleteLastOperation_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOperation != null)
            {
                OperationsPanel.Children.Remove(selectedOperation);
                selectedOperation = null;
            }
            else
            {
                System.Windows.MessageBox.Show("Lütfen silmek istediğiniz bir operasyonu seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        private Border selectedOperation = null;

        private void OperationSelect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfControls.Button button && button.Tag is Border operation)
            {
                // Reset previous selection color if any
                if (selectedOperation != null)
                    selectedOperation.BorderBrush = null;

                // Mark current selection
                selectedOperation = operation;
                selectedOperation.BorderBrush = Brushes.Orange;
                selectedOperation.BorderThickness = new Thickness(2);
            }
        }

        private DateTime lastClickTime = DateTime.MinValue;

        private void OperationBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is Border clickedBorder)
            {
                // Clear previous selection
                if (selectedOperation != null)
                    selectedOperation.BorderBrush = null;

                // Set new selection
                selectedOperation = clickedBorder;
                selectedOperation.BorderBrush = Brushes.Orange;
                selectedOperation.BorderThickness = new Thickness(2);
            }
        }

        private Border activeOperationBorder = null;


        private void LoadOperation_Click(object sender, RoutedEventArgs e)
        {
            // 1) Reset visuals for the previously active operation
            if (activeOperationBorder != null && activeOperationBorder.Child is Grid previousGrid)
            {
                // remove old left-bar indicator
                var oldBar = previousGrid.Children
                    .OfType<Rectangle>()
                    .FirstOrDefault(r => r.Width == 5 && r.HorizontalAlignment == System.Windows.HorizontalAlignment.Left);
                if (oldBar != null)
                    previousGrid.Children.Remove(oldBar);

                // reset text & button
                var prevText = previousGrid.Children.OfType<TextBlock>().FirstOrDefault();
                var prevBtn = previousGrid.Children.OfType<WpfControls.Button>().FirstOrDefault();
                if (prevText != null)
                {
                    prevText.Background = Brushes.Transparent;
                    prevText.Foreground = Brushes.White;
                }
                if (prevBtn != null)
                {
                    prevBtn.Content = "Yükle";
                    prevBtn.Background = (Brush)FindResource("ButtonGradient");
                }

                // hide previous green bar
                if (activeOperationBorder.Tag is Border prevGreenBar)
                    prevGreenBar.Visibility = Visibility.Collapsed;
            }

            // 2) Activate the new operation
            if (sender is WpfControls.Button btn
                && btn.Parent is Grid grid
                && grid.Parent is Border newBorder)
            {
                activeOperationBorder = newBorder;

                // reset text color
                var newText = grid.Children.OfType<TextBlock>().FirstOrDefault();
                if (newText != null)
                {
                    newText.Background = Brushes.Transparent;
                    newText.Foreground = Brushes.White;
                }

                // update button
                btn.Content = "AKTİF";
                btn.Background = Brushes.DarkBlue;

                // show green bar
                if (newBorder.Tag is Border greenBar)
                    greenBar.Visibility = Visibility.Visible;

                // 3) Transfer operation parameters to the system panel
                string operationName = newText?.Text;
                if (!string.IsNullOrEmpty(operationName)
                    && operationsData.TryGetValue(operationName, out var data))
                {
                    // — Mode via RadioButtons —
                    ManualRadioButton.IsChecked = data.Mode == "Manuel";
                    SemiAutoRadioButton.IsChecked = data.Mode == "Yarı-Otomatik";
                    FullAutoRadioButton.IsChecked = data.Mode == "Tam Otomatik";

                    // — Target Type (ComboBox in top-right panel) —
                    var targetItem = TargetTypeComboBox.Items
                        .OfType<ComboBoxItem>()
                        .FirstOrDefault(i => (string)i.Content == data.Target);
                    if (targetItem != null)
                        TargetTypeComboBox.SelectedItem = targetItem;

                    // — Color (ComboBox in top-right panel) —
                    var sysColorItem = SystemColorComboBox.Items
                        .OfType<ComboBoxItem>()
                        .FirstOrDefault(i => (string)i.Content == data.Color);
                    if (sysColorItem != null)
                        SystemColorComboBox.SelectedItem = sysColorItem;
                }
            }
        }




        private void MenzilCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            RangeIndicator.Visibility = Visibility.Visible;
        }

        private void MenzilCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            RangeIndicator.Visibility = Visibility.Collapsed;
        }

        private void TargetMarkersCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            TargetMarkers.Visibility = Visibility.Visible;
        }

        private void TargetMarkersCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            TargetMarkers.Visibility = Visibility.Collapsed;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Focus(); // Ensures key presses go to the window
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.IsRepeat) return;

            if (e.Key == Key.Down)
            {
                elevationDirection = -1;
                elevationTimer.Start();
            }
            else if (e.Key == Key.Up)
            {
                elevationDirection = 1;
                elevationTimer.Start();
            }
            else if (e.Key == Key.Left)
            {
                manualRotateDirection = -1;
                manualRotateTimer.Start();
            }
            else if (e.Key == Key.Right)
            {
                manualRotateDirection = 1;
                manualRotateTimer.Start();
            }
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                elevationTimer.Stop();
            }
            else if (e.Key == Key.Left || e.Key == Key.Right)
            {
                manualRotateTimer.Stop();
            }
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            Window_KeyDown(this, e);
        }

        protected override void OnPreviewKeyUp(System.Windows.Input.KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);
            Window_KeyUp(this, e);
        }

        private void DetectModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (NormalColorPanel == null || DustEnemyColorPanel == null)
                return; // UI not ready yet

            if (NormalModeRadio.IsChecked == true)
            {
                NormalColorPanel.Visibility = Visibility.Visible;
                DustEnemyColorPanel.Visibility = Visibility.Collapsed;
            }
            else if (DustEnemyModeRadio.IsChecked == true)
            {
                NormalColorPanel.Visibility = Visibility.Collapsed;
                DustEnemyColorPanel.Visibility = Visibility.Visible;
            }
        }

    }
}

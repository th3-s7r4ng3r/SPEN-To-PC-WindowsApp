using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;


namespace SPEN_To_PC_WindowsApp
{
    public partial class MainWindow : Window
    {
        // Declaring variables for persistent connection
        private TcpListener? tcpListener;
        private CancellationTokenSource? cancellationSource;
        private TcpClient? client;
        private NetworkStream? stream;
        private bool isConnected = false;

        // Get the app's local data folder
        private const string SettingFileName = "settings.json";
        AppSettings appSettings = new();
        private bool isSingleClickCapturing = false;
        private bool isDoubleClickCapturing = false;

        // Other variables
        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private int singleClick = 0x25;  // Left arrow key
        private int doubleClick = 0x27; // Right arrow key

        // Keyinput related
        [LibraryImport("user32.dll")]
        private static partial void keybd_event(byte bVk, byte bScan, int dwFlags, IntPtr dwExtraInfo);

        public MainWindow()
        {
            InitializeComponent();
            //hiding UI panels
            SettingsPanel.Visibility = Visibility.Hidden;
            AboutPanel.Visibility = Visibility.Hidden;
            KeyMapHint.Content = "Click the capture button and then press a key\n to map that action to the selected Air Action\n         Press ESC to cancel the mapping";
            HomePageBtn.Style = (Style)FindResource("NavBtnsActive"); //set home page as opening page

            //setting up networking
            DisplayIPAddress();
            InitializeTcpListener();
            cancellationSource = new CancellationTokenSource();
            _ = StartTcpListener();

            LoadSettings(); // Load settings on app startup
            singleClick = appSettings.SingleClick;
            doubleClick = appSettings.DoubleClick;
        }

        //get ip address of the pc
        private void DisplayIPAddress()
        {
            // Get all network interfaces
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            // Find the first active (up) network interface with an IPv4 address
            NetworkInterface? activeInterface = null;

            foreach (var networkInterface in networkInterfaces)
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(address.Address))
                        {
                            activeInterface = networkInterface;
                            break;
                        }
                    }

                    if (activeInterface != null)
                        break;
                }
            }

            // Display the IP address in the IPLable
            if (activeInterface != null)
            {
                string? ipAddress = activeInterface.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();

                IPTextLable.Content = ipAddress ?? "Not available";
            }
            else
            {
                IPTextLable.Content = "Not Available";
                MessageBox.Show("No active networks found!\nPlease connect to a network", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void InitializeTcpListener()
        {
            // Initialize TcpListener on port 51515
            tcpListener = new TcpListener(IPAddress.Any, 51515);
        }

        private async Task StartTcpListener()
        {
            try
            {
                // Start listening for incoming connections
                tcpListener?.Start();

                // check for closing the connection
                while (!cancellationSource.IsCancellationRequested)
                {
                    try
                    {
                        // Accept a single connection and handle it persistently
                        client = await tcpListener.AcceptTcpClientAsync();
                        stream = client.GetStream();
                        await HandleClientPersistently();
                    }
                    catch (Exception ex)
                    {
                        // Handle exceptions (e.g., if the listener is stopped)
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting TCP listener: {ex.Message}");
            }
        }

        private async Task HandleClientPersistently()
        {
            try
            {
                // check for closing the connection
                while (!cancellationSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Read incoming data
                        byte[] buffer = new byte[1024];
                        int bytesRead = await stream.ReadAsync(buffer);

                        // Process received data
                        string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        if (receivedData == "connection closed") // Check for a special message
                        {
                            UpdateUI(false);
                            Console.WriteLine("Desktop app closed. Sending close signal.");
                            break;
                        }
                        UpdateUI(true);
                        ProcessReceivedData(receivedData);

                        // Send a heartbeat every 0.1 seconds
                        await Task.Delay(100);
                        await stream.WriteAsync(Encoding.UTF8.GetBytes("ping"));
                    }
                    catch (SocketException ex)
                    {
                        // Handle socket exceptions
                        UpdateUI(false);
                        Console.WriteLine($"Socket exception occurred: {ex.Message}");
                        break;
                    }
                    catch (IOException ex)
                    {
                        // Handle other I/O exceptions
                        UpdateUI(false);
                        Console.WriteLine($"I/O exception occurred: {ex.Message}");
                        break;
                    }
                    catch (TimeoutException)
                    {
                        // Handle heartbeat timeout
                        UpdateUI(false);
                        Console.WriteLine("Heartbeat timeout. Connection lost.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., if the client disconnects)
                UpdateUI(false);
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
            finally
            {
                // Cleanup resources
                stream?.Dispose();
                client?.Close();
            }
        }

        private void ProcessReceivedData(string data)
        {
            try
            {
                // Update UI labels (Testing only)
                CurrentAction.Content = $"Click Type: {data}";

                // Simulate arrow key presses based on click type
                if (data.Contains("Single Click"))
                {
                    SimulateKeyPress(singleClick);
                    _ = UpdateStyles("Single click");   //update the UI
                }
                else if (data.Contains("Double Click"))
                {
                    SimulateKeyPress(doubleClick);
                    _ = UpdateStyles("double click");   //update the UI
                }

                //checking the connection is alive or not
                if (data.Contains("ping"))
                {
                    _ = stream.WriteAsync(Encoding.UTF8.GetBytes("pong"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating UI: {ex.Message}");
            }
        }

        // update the UI with changes to the connection
        private void UpdateUI(bool connectionStatus)
        {
            isConnected = connectionStatus;
            if (isConnected)
            {
                ConnectionStatus.Content = "Connected";
                ConnectionStatus.Style = (Style)FindResource("ConnectionStatusConnected");  //change the style
            }
            else
            {
                ConnectionStatus.Content = "Disconnected";
                CurrentAction.Content = "None:";
                ConnectionStatus.Style = (Style)FindResource("ConnectionStatusDisconnected"); //change the style
            }
        }

        // indicate whether the key is pressed or not
        private async Task UpdateStyles(string element)
        {
            if (element == "Single click")
            {
                SingleClickAction.Style = (Style)FindResource("CurrentActionActive");
            }
            if (element == "double click")
            {
                DoubleClickAction.Style = (Style)FindResource("CurrentActionActive");
            }
            await Task.Delay(300);
            SingleClickAction.Style = (Style)FindResource("CurrentActionInactive");
            DoubleClickAction.Style = (Style)FindResource("CurrentActionInactive");
        }

        // Defininf a class to store app settings
        public class AppSettings
        {
            public int SingleClick { get; set; }
            public int DoubleClick { get; set; }
        }

        // loading data from the settings.json
        private void LoadSettings()
        {
            appSettings = ReadSettings();
            if (appSettings == null)
            {
                // First launch, create default settings
                appSettings = new AppSettings
                {
                    SingleClick = 0x27,
                    DoubleClick = 0x25
                };

                // Save the default settings
                SaveSettings(appSettings);
                LoadSettings();
            }
            else
            {
                //read from the file and assign values to variables
                singleClick = appSettings.SingleClick;
                doubleClick = appSettings.DoubleClick;
                cur_singleClick.Content = $"{KeyInterop.KeyFromVirtualKey(singleClick)}";
                cur_doubleClick.Content = $"{KeyInterop.KeyFromVirtualKey(doubleClick)}";
                SingleClickAction.Content = $"{KeyInterop.KeyFromVirtualKey(singleClick)}";
                DoubleClickAction.Content = $"{KeyInterop.KeyFromVirtualKey(doubleClick)}";
            }
        }

        private AppSettings ReadSettings()
        {
            try
            {
                // Combine the current directory with the file name
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), SettingFileName);

                // Check if the file exists before attempting to read
                if (File.Exists(filePath))
                {
                    // Read the JSON data from the file and deserialize it to AppSettings
                    string json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<AppSettings>(json);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }


        private void SaveSettings(AppSettings appSettings)
        {
            try
            {
                //Update the action lable if the settings changed
                SingleClickAction.Content = $"{KeyInterop.KeyFromVirtualKey(singleClick)}";
                DoubleClickAction.Content = $"{KeyInterop.KeyFromVirtualKey(doubleClick)}";

                // Combine the current directory with the file name
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), SettingFileName);

                // Serialize the AppSettings object to JSON
                string json = JsonSerializer.Serialize(appSettings);

                // Write the JSON data to the file
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void SimulateKeyPress(int keyCode)
        {
            keybd_event((byte)keyCode, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
            Thread.Sleep(100); // Add a small delay to ensure key press is recognized
            keybd_event((byte)keyCode, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        }


        // capturing the key event for customizing the actions
        private void Cap_singleClick_Click(object sender, RoutedEventArgs e)
        {
            StartKeyCapture(true);
        }
        private void Cap_doubleClick_Click(object sender, RoutedEventArgs e)
        {
            StartKeyCapture(false);
        }

        private void StartKeyCapture(bool isSingleClick)
        {
            // Disable the other capture button
            if (isSingleClick)
            {
                isDoubleClickCapturing = false;
                cap_singleClick.IsEnabled = false;
                cap_doubleClick.IsEnabled = false;
            }
            else
            {
                isSingleClickCapturing = false;
                cap_doubleClick.IsEnabled = false;
                cap_singleClick.IsEnabled = false;
            }

            // Set capturing state
            if (isSingleClick)
            {
                isSingleClickCapturing = true;
                cur_singleClick.Content = "Capturing...";
            }
            else
            {
                isDoubleClickCapturing = true;
                cur_doubleClick.Content = "Capturing...";
            }

            // Listen for key presses
            KeyDown += KeyCapture_KeyDown;
        }

        private void KeyCapture_KeyDown(object sender, KeyEventArgs e)
        {
            // Stop capturing on ESC key
            if (e.Key == Key.Escape)
            {
                StopKeyCapture();
            }
            if (isSingleClickCapturing)
            {
                // Update label and save key in appSettings
                cur_singleClick.Content = $"{e.Key}";
                appSettings.SingleClick = KeyInterop.VirtualKeyFromKey(e.Key);
                singleClick = appSettings.SingleClick;
                SaveSettings(appSettings);

                // Stop capturing after capturing the key
                StopKeyCapture();
            }
            else if (isDoubleClickCapturing)
            {
                // Update label and save key in appSettings
                cur_doubleClick.Content = $"{e.Key}";
                appSettings.DoubleClick = KeyInterop.VirtualKeyFromKey(e.Key);
                doubleClick = appSettings.DoubleClick;
                SaveSettings(appSettings);

                // Stop capturing after capturing the key
                StopKeyCapture();
            }
        }

        private void StopKeyCapture()
        {
            // Enable both capture buttons
            cap_singleClick.IsEnabled = true;
            cap_doubleClick.IsEnabled = true;

            // Reset capturing state and labels
            isSingleClickCapturing = false;
            isDoubleClickCapturing = false;
            cur_singleClick.Content = $"{KeyInterop.KeyFromVirtualKey(appSettings.SingleClick)}";
            cur_doubleClick.Content = $"{KeyInterop.KeyFromVirtualKey(appSettings.DoubleClick)}";

            // Stop listening for key presses
            KeyDown -= KeyCapture_KeyDown;
        }


        // action when the app is closed
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Cleanup resources on window closing
                cancellationSource?.Cancel();
                StopKeyCapture();

                if (tcpListener != null && tcpListener.Server.IsBound)
                {
                    // Stop the listener if it's still running
                    tcpListener.Stop();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during window closing: {ex.Message}");
            }
        }


        // Change the UI based on the screen
        private void HomePageBtn_click(object sender, RoutedEventArgs e)
        {
            // change the active page style
            HomePageBtn.Style = (Style)FindResource("NavBtnsActive");
            SettingsPageBtn.Style = (Style)FindResource("NavBtns");
            AboutPageBtn.Style = (Style)FindResource("NavBtns");
            // display the correct panel
            HomePanel.Visibility = Visibility.Visible;
            SettingsPanel.Visibility = Visibility.Hidden;
            AboutPanel.Visibility = Visibility.Hidden;
            // stop key capture if happening
            StopKeyCapture();
        }
        private void SettingsPageBtn_click(object sender, RoutedEventArgs e)
        {
            // change the active page style
            HomePageBtn.Style = (Style)FindResource("NavBtns");
            SettingsPageBtn.Style = (Style)FindResource("NavBtnsActive");
            AboutPageBtn.Style = (Style)FindResource("NavBtns");
            // display the correct panel
            HomePanel.Visibility = Visibility.Hidden;
            SettingsPanel.Visibility = Visibility.Visible;
            AboutPanel.Visibility = Visibility.Hidden;
            // stop key capture if happening
            StopKeyCapture();
        }
        private void AboutPageBtn_click(object sender, RoutedEventArgs e)
        {
            // change the active page style
            HomePageBtn.Style = (Style)FindResource("NavBtns");
            SettingsPageBtn.Style = (Style)FindResource("NavBtns");
            AboutPageBtn.Style = (Style)FindResource("NavBtnsActive");
            // display the correct panel
            HomePanel.Visibility = Visibility.Hidden;
            SettingsPanel.Visibility = Visibility.Hidden;
            AboutPanel.Visibility = Visibility.Visible;
            // stop key capture if happening
            StopKeyCapture();
        }


        // About section button handling
        private void GitHubLink_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://github.com/th3-s7r4ng3r/SPEN-To-PC-WindowsApp";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void ContactMeLink_Click(object sender, RoutedEventArgs e)
        {
            string url = "mailto:gvinura@gmail.com";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void DonateLink_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://www.buymeacoffee.com/th3.s7r4ng3r";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        // custom close button (Not used)
        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
    }
}



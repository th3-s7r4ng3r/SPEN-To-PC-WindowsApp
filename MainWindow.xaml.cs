using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
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
        private string currentCapturing = "none";
        private string appVersion = "1.0";

        // Other variables
        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private int singleClick = 0x25;  // Left arrow key
        private int doubleClick = 0x27; // Right arrow key
        private int swipeLeft = 0x21; // Page Up key
        private int swipeRight = 0x22; // Page Down key
        private int swipeUp = 0x26; // Up arrow key
        private int swipeDown = 0x28; // Down arrow key
        private int clockWise = 0xAF; // Volume Up key
        private int antiClockWise = 0xAE; // Volume Down key


        // Keyinput related
        [LibraryImport("user32.dll")]
        private static partial void keybd_event(byte bVk, byte bScan, int dwFlags, IntPtr dwExtraInfo);

        public MainWindow()
        {
            InitializeComponent();
            //hiding UI panels
            SettingsPanel.Visibility = Visibility.Hidden;
            AboutPanel.Visibility = Visibility.Hidden;
            versionLable.Content = "Version " + appVersion;
            KeyMapHint.Content = " Click the capture button and then press a key \n  to map that action to the selected Air Action. \n         Press ESC to cancel the mapping";
            HomePageBtn.Style = (Style)FindResource("NavBtnsActive"); //set home page as opening page

            //setting up networking
            DisplayIPAddress();
            InitializeTcpListener();
            cancellationSource = new CancellationTokenSource();
            _ = StartTcpListener();

            LoadSettings(); // Load settings on app startup
            //singleClick = appSettings.SingleClick;
            //doubleClick = appSettings.DoubleClick;
        }

        //get ip address of the pc
        private void DisplayIPAddress()
        {
            // Get all network interfaces
            NetworkInterface[] allnetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            // List for virtual adapters
            string[] virtualAdapters = ["virtual", "virtio", "pv network", "parallels ethernet", "vic ethernet", "vmware"];

            // Find the first active (up) network interface with an IPv4 address
            NetworkInterface? activeInterface = null;


            foreach (var networkInterface in allnetworkInterfaces)
            {
                //check whether the interface is up and not a loopback one
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    //check whether the interface is a virtual or not
                    bool isAVirtualAdapter = false;
                    foreach (var virtualAdapter in virtualAdapters)
                    {
                        if (networkInterface.Description.ToLowerInvariant().Contains(virtualAdapter))
                        {
                            isAVirtualAdapter = true;
                            break;
                        }
                    }

                    // go to next interface, if the current one is a virtual one
                    if (isAVirtualAdapter)
                    {
                        continue;
                    }


                    // selecting the interface if it has a IPv4 address
                    foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(address.Address))
                        {
                            activeInterface = networkInterface;
                            break;
                        }
                    }
                    // break if an active network is found
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

                // Simulate key presses based on click type
                if (data.Contains("Single Click"))
                {
                    SimulateKeyPress(singleClick);
                    _ = UpdateStyles("single");   //update the UI
                }
                else if (data.Contains("Double Click"))
                {
                    SimulateKeyPress(doubleClick);
                    _ = UpdateStyles("double");   //update the UI
                }
                else if (data.Contains("sw_left"))
                {
                    SimulateKeyPress(swipeLeft);
                    _ = UpdateStyles("left");   //update the UI
                }
                else if (data.Contains("sw_right"))
                {
                    SimulateKeyPress(swipeRight);
                    _ = UpdateStyles("right");   //update the UI
                }
                else if (data.Contains("sw_up"))
                {
                    SimulateKeyPress(swipeUp);
                    _ = UpdateStyles("up");   //update the UI
                }
                else if (data.Contains("sw_down"))
                {
                    SimulateKeyPress(swipeDown);
                    _ = UpdateStyles("down");   //update the UI
                }
                else if (data.Contains("cl_counterClockWise"))
                {
                    SimulateKeyPress(clockWise);
                    _ = UpdateStyles("counterClock");   //update the UI
                }
                else if (data.Contains("cl_clockWise"))
                {
                    SimulateKeyPress(antiClockWise);
                    _ = UpdateStyles("clockWise");   //update the UI
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
            switch (element)
            {
                case "single":
                    SingleClickAction.Style = (Style)FindResource("CurrentActionActive");
                    break;
                case "double":
                    DoubleClickAction.Style = (Style)FindResource("CurrentActionActive");
                    break;
                case "clockWise":
                    ClockWiseAction.Style = (Style)FindResource("CurrentActionActive");
                    break;
                case "counterClock":
                    AntiClockAction.Style = (Style)FindResource("CurrentActionActive");
                    break;
                case "left":
                    SwipeLeftAction.Style = (Style)FindResource("CurrentActionActive");
                    break;
                case "right":
                    SwipeRightAction.Style = (Style)FindResource("CurrentActionActive");
                    break;
                case "up":
                    SwipeUpAction.Style = (Style)FindResource("CurrentActionActive");
                    break;
                case "down":
                    SwipeDownAction.Style = (Style)FindResource("CurrentActionActive");
                    break;
            }

            await Task.Delay(400);
            SingleClickAction.Style = (Style)FindResource("CurrentActionInactive");
            DoubleClickAction.Style = (Style)FindResource("CurrentActionInactive");
            ClockWiseAction.Style = (Style)FindResource("CurrentActionInactive");
            AntiClockAction.Style = (Style)FindResource("CurrentActionInactive");
            SwipeLeftAction.Style = (Style)FindResource("CurrentActionInactive");
            SwipeUpAction.Style = (Style)FindResource("CurrentActionInactive");
            SwipeDownAction.Style = (Style)FindResource("CurrentActionInactive");
            SwipeRightAction.Style = (Style)FindResource("CurrentActionInactive");
        }

        // updating labels in home screen actions & Settings screens
        private void updateKeys()
        {
            SingleClickAction.Content = Action01KeyDisplay.Content = $"{KeyInterop.KeyFromVirtualKey(singleClick)}";
            DoubleClickAction.Content = Action02KeyDisplay.Content = $"{KeyInterop.KeyFromVirtualKey(doubleClick)}";
            ClockWiseAction.Content = Action07KeyDisplay.Content = $"{KeyInterop.KeyFromVirtualKey(clockWise)}";
            AntiClockAction.Content = Action08KeyDisplay.Content = $"{KeyInterop.KeyFromVirtualKey(antiClockWise)}";
            SwipeLeftAction.Content = Action05KeyDisplay.Content = $"{KeyInterop.KeyFromVirtualKey(swipeLeft)}";
            SwipeRightAction.Content = Action06KeyDisplay.Content = $"{KeyInterop.KeyFromVirtualKey(swipeRight)}";
            SwipeUpAction.Content = Action03KeyDisplay.Content = $"{KeyInterop.KeyFromVirtualKey(swipeUp)}";
            SwipeDownAction.Content = Action04KeyDisplay.Content = $"{KeyInterop.KeyFromVirtualKey(swipeDown)}";
        }

        // Defining a class to store app settings
        public class AppSettings
        {
            public int SingleClick { get; set; }
            public int DoubleClick { get; set; }
            public int ClockWise { get; set; }
            public int AntiClock { get; set; }
            public int SwipeLeft { get; set; }
            public int SwipeRight { get; set; }
            public int SwipeUp { get; set; }
            public int SwipeDown { get; set; }
        }

        // loading data from the settings.json
        private void LoadSettings()
        {
            appSettings = ReadSettings();
            // check whether the new setting file is in the roaming folder
            if (appSettings == null || (appSettings.SwipeLeft == 0 && appSettings.SwipeRight == 0 && appSettings.SwipeUp == 0 && appSettings.SwipeDown == 0))
            {
                // First launch, create default settings
                appSettings = new AppSettings
                {
                    SingleClick = 0x27,
                    DoubleClick = 0x25,
                    SwipeLeft = 0x21,
                    SwipeRight = 0x22,
                    SwipeUp = 0x26,
                    SwipeDown = 0x28,
                    ClockWise = 0xAF,
                    AntiClock = 0xAE
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
                swipeDown = appSettings.SwipeDown;
                swipeUp = appSettings.SwipeUp;
                swipeLeft = appSettings.SwipeLeft;
                swipeRight = appSettings.SwipeRight;
                antiClockWise = appSettings.AntiClock;
                clockWise = appSettings.ClockWise;

                // updating labels in home screen actions & Settings screens
                updateKeys();
            }
        }

        private AppSettings ReadSettings()
        {
            try
            {
                // Get the AppData Roaming directory
                string appDataRoamingPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SPENToPC");

                // Combine the AppData Roaming directory with the file name
                string filePath = Path.Combine(appDataRoamingPath, SettingFileName);

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
                // Update the action label if the settings changed
                updateKeys();

                // Get the AppData Roaming directory
                string appDataRoamingPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SPENToPC");
                // Ensure the "SPEN To PC" folder exists
                if (!Directory.Exists(appDataRoamingPath))
                {
                    Directory.CreateDirectory(appDataRoamingPath);
                }

                // Combine the AppData Roaming directory with the file name
                string filePath = Path.Combine(appDataRoamingPath, SettingFileName);

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


        // keyboard related methods
        private void SimulateKeyPress(int keyCode)
        {
            keybd_event((byte)keyCode, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
            Thread.Sleep(100); // Add a small delay to ensure key press is recognized
            keybd_event((byte)keyCode, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        }


        // capturing the key event for customizing the actions
        private void ActionCapture(object sender, RoutedEventArgs e)
        {
            var buttonText = (sender as Button).Name;
            if (buttonText.ToString().Contains("01"))
            {
                currentCapturing = "single";
                Action01KeyDisplay.Content = "Capturing...";
            }
            if (buttonText.ToString().Contains("02"))
            {
                currentCapturing = "double";
                Action02KeyDisplay.Content = "Capturing...";
            }
            if (buttonText.ToString().Contains("03"))
            {
                currentCapturing = "up";
                Action03KeyDisplay.Content = "Capturing...";
            }
            if (buttonText.ToString().Contains("04"))
            {
                currentCapturing = "down";
                Action04KeyDisplay.Content = "Capturing...";
            }
            if (buttonText.ToString().Contains("05"))
            {
                currentCapturing = "left";
                Action05KeyDisplay.Content = "Capturing...";
            }
            if (buttonText.ToString().Contains("06"))
            {
                currentCapturing = "right";
                Action06KeyDisplay.Content = "Capturing...";
            }
            if (buttonText.ToString().Contains("07"))
            {
                currentCapturing = "cw";
                Action07KeyDisplay.Content = "Capturing...";
            }
            if (buttonText.ToString().Contains("08"))
            {
                currentCapturing = "ccw";
                Action08KeyDisplay.Content = "Capturing...";
            }

            // Disable the capture buttons and dsiplay correctly
            disableCaptureKeys(true);
            // Listen for key presses
            KeyDown += KeyCapture_KeyDown;
        }

        // Handling capture keys
        private void disableCaptureKeys(bool state)
        {
            if (state)
            {
                // disables all capture keys
                Action01CaptBtn.IsEnabled = false;
                Action02CaptBtn.IsEnabled = false;
                Action03CaptBtn.IsEnabled = false;
                Action04CaptBtn.IsEnabled = false;
                Action05CaptBtn.IsEnabled = false;
                Action06CaptBtn.IsEnabled = false;
                Action07CaptBtn.IsEnabled = false;
                Action08CaptBtn.IsEnabled = false;

            }
            else
            {
                // reset back to default
                Action01CaptBtn.IsEnabled = true;
                Action02CaptBtn.IsEnabled = true;
                Action03CaptBtn.IsEnabled = true;
                Action04CaptBtn.IsEnabled = true;
                Action05CaptBtn.IsEnabled = true;
                Action06CaptBtn.IsEnabled = true;
                Action07CaptBtn.IsEnabled = true;
                Action08CaptBtn.IsEnabled = true;

            }
        }

        private void KeyCapture_KeyDown(object sender, KeyEventArgs e)
        {
            // Stop capturing on ESC key
            if (e.Key == Key.Escape)
            {
                StopKeyCapture();
            }

            switch (currentCapturing)
            {
                case "single":
                    Action01KeyDisplay.Content = $"{e.Key}";
                    singleClick = appSettings.SingleClick = KeyInterop.VirtualKeyFromKey(e.Key);
                    break;
                case "double":
                    Action02KeyDisplay.Content = $"{e.Key}";
                    doubleClick = appSettings.DoubleClick = KeyInterop.VirtualKeyFromKey(e.Key);
                    break;
                case "up":
                    Action03KeyDisplay.Content = $"{e.Key}";
                    swipeUp = appSettings.SwipeUp = KeyInterop.VirtualKeyFromKey(e.Key);
                    break;
                case "down":
                    Action04KeyDisplay.Content = $"{e.Key}";
                    swipeDown = appSettings.SwipeDown = KeyInterop.VirtualKeyFromKey(e.Key);
                    break;
                case "left":
                    Action05KeyDisplay.Content = $"{e.Key}";
                    swipeLeft = appSettings.SwipeLeft = KeyInterop.VirtualKeyFromKey(e.Key);
                    break;
                case "right":
                    Action06KeyDisplay.Content = $"{e.Key}";
                    swipeRight = appSettings.SwipeRight = KeyInterop.VirtualKeyFromKey(e.Key);
                    break;
                case "cw":
                    Action07KeyDisplay.Content = $"{e.Key}";
                    clockWise = appSettings.ClockWise = KeyInterop.VirtualKeyFromKey(e.Key);
                    break;
                case "ccw":
                    Action08KeyDisplay.Content = $"{e.Key}";
                    antiClockWise = appSettings.AntiClock = KeyInterop.VirtualKeyFromKey(e.Key);
                    break;
            }

            // Stop capturing after capturing the key
            SaveSettings(appSettings);
            StopKeyCapture();
        }

        private void StopKeyCapture()
        {
            // Enable capture buttons and update lables
            disableCaptureKeys(false);
            currentCapturing = "none";
            updateKeys();

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
            string url = "https://github.com/th3-s7r4ng3r/SPEN-To-PC-WindowsApp/releases";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void ContactMeLink_Click(object sender, RoutedEventArgs e)
        {
            string url = "mailto:th3.s7r4ng3r@gmail.com";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void DonateLink_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://www.buymeacoffee.com/th3.s7r4ng3r";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        // custom close button
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



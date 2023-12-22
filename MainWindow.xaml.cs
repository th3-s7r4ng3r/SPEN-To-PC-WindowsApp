using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;


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

        // Other variables
        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const int VK_LEFT = 0x25;  // Left arrow key
        private const int VK_RIGHT = 0x27; // Right arrow key

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, IntPtr dwExtraInfo);

        public MainWindow()
        {
            InitializeComponent();

            //setting up networking
            DisplayIPAddress();
            InitializeTcpListener();
            cancellationSource = new CancellationTokenSource();
            StartTcpListener();
        }

        //get ip address of the pc
        private void DisplayIPAddress()
        {
            // Get all network interfaces
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            // Find the first active (up) network interface with an IPv4 address
            NetworkInterface activeInterface = null;

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
            else { IPTextLable.Content = "No active network connection found!"; }
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
                while (!cancellationSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Read incoming data with a timeout
                        byte[] buffer = new byte[1024];
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length); // Timeout after 1 second

                        // Process received data
                        string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        if (receivedData == "connection closed") // Check for a special message
                        {
                            updateUI(false);
                            Console.WriteLine("Desktop app closed. Sending close signal.");
                            break;
                        }
                        updateUI(true);
                        ProcessReceivedData(receivedData);

                        // Send a heartbeat every 0.1 seconds
                        await Task.Delay(100);
                        await stream.WriteAsync(Encoding.UTF8.GetBytes("ping"));
                    }
                    catch (SocketException ex)
                    {
                        // Handle socket exceptions
                        updateUI(false);
                        Console.WriteLine($"Socket exception occurred: {ex.Message}");
                        break;
                    }
                    catch (IOException ex)
                    {
                        // Handle other I/O exceptions
                        updateUI(false);
                        Console.WriteLine($"I/O exception occurred: {ex.Message}");
                        break;
                    }
                    catch (TimeoutException)
                    {
                        // Handle heartbeat timeout
                        updateUI(false);
                        Console.WriteLine("Heartbeat timeout. Connection lost.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., if the client disconnects)
  
                updateUI(false);
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
                // Update UI labels and simulate arrow key presses based on received data
                CurrentAction.Content = $"Click Type: {data}";

                // Simulate arrow key presses based on click type
                if (data.Contains("Single Click"))
                {
                    SimulateKeyPress(VK_RIGHT);
                }
                else if (data.Contains("Double Click"))
                {
                    SimulateKeyPress(VK_LEFT);
                }
                if (data.Contains("ping"))
                {
                    stream.WriteAsync(Encoding.UTF8.GetBytes("pong"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating UI: {ex.Message}");
            }
        }

        private void SimulateKeyPress(int keyCode)
        {
            keybd_event((byte)keyCode, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
            Thread.Sleep(100); // Add a small delay to ensure key press is recognized
            keybd_event((byte)keyCode, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        }

        private void updateUI(bool connectionStatus)
        {
            isConnected = connectionStatus;
            if (isConnected)
            {
                ConnectionStatus.Content = "Connection Status: Connected";
            } else
            {
                ConnectionStatus.Content = "Connection Status: Disconnected";
                CurrentAction.Content = "None:";
            }
        }




        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Cleanup resources on window closing
                cancellationSource?.Cancel();

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
    }
}
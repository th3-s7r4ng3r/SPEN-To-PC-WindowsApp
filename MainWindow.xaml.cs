using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Threading.Tasks;

namespace SPEN_To_PC_WindowsApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //declaring all variables
        private TcpListener? tcpListener;
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
            else { IPTextLable.Content = "No active network connection found!";}
        }


        private void InitializeTcpListener()
        {
            // Initialize TcpListener on port 51515
            tcpListener = new TcpListener(IPAddress.Any, 51515);
        }

        private async void StartTcpListener()
        {
            try
            {
                // Start listening for incoming connections
                tcpListener?.Start();

                while (true)
                {
                    try
                    {
                        // Accept a pending connection
                        TcpClient client = await tcpListener.AcceptTcpClientAsync();

                        // Process the received data on a separate thread
                        Task.Run(() => HandleClient(client));
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

        private void HandleClient(TcpClient client)
        {
            try
            {
                // Get the network stream
                NetworkStream stream = client.GetStream();

                // Read the incoming data
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                // Convert the received data to a string
                string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // Process the received data and perform actions
                // Update UI and simulate arrow key presses based on received data
                Dispatcher.Invoke(() =>
                {
                    ProcessReceivedData(receivedData);
                });

                // Close the client connection
                client.Close();
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., if there's an issue with the client)
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
        }

        private void ProcessReceivedData(string data)
        {
            try
            {
                // Update UI labels and simulate arrow key presses based on received data
                ConnectionStatus.Content = "Connection Status: Connected";
                CurrentAction.Content = $"Click Type: {data}";

                // Simulate arrow key presses based on click type
                if (data == "Single Click")
                {
                    SimulateKeyPress(VK_RIGHT);
                }
                else if (data == "Double Click")
                {
                    SimulateKeyPress(VK_LEFT);
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






    }
}
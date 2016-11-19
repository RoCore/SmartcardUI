using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using static PcScTool.Extensions;

namespace PcScTool
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private PCSC.SCardContext _scardContext;
        private PCSC.SCardMonitor _monitor;
        private PCSC.IDeviceMonitor _device;
        public MainWindow()
        {
            this.Initialized += MainWindow_Initialized;
            InitializeComponent();

        }

        private void MainWindow_Initialized(object sender, EventArgs e)
        {
            Output.TextChanged += Output_TextChanged;
            Readers.SelectionChanged += Readers_SelectionChanged;
            Input.TextChanged += Input_TextChanged;

            _device = PCSC.DeviceMonitorFactory.Instance.Create(PCSC.SCardScope.User);

            _device.StatusChanged += Context_StatusChanged;
            _device.Start();
            _scardContext = new PCSC.SCardContext();
            _scardContext.Establish(PCSC.SCardScope.User);
            Output.Text += $"Available Readers:{Environment.NewLine}{string.Join(Environment.NewLine, _scardContext.GetReaders())}";

            foreach (var item in _scardContext.GetReaders())
            {
                Readers.Items.Add(item);
            }

            _monitor = new PCSC.SCardMonitor(PCSC.ContextFactory.Instance, PCSC.SCardScope.User);
            _monitor.CardInserted += SmartCard_CardInserted;
            _monitor.CardRemoved += SmartCard_CardRemoved;
        }

        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {
            Exec.IsEnabled = Readers.SelectedItem != null && Input.Text?.Length > 0;
        }

        private void Readers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Exec.IsEnabled = e.AddedItems.Count > 0 && Input.Text?.Length > 0;
        }

        private void Output_TextChanged(object sender, TextChangedEventArgs e)
        {
            Output.Focus();
            Output.Select(Output.Text.Length, 0);
        }

        ~MainWindow()
        {
            _scardContext.Dispose();
            _monitor.Dispose();
            _device.Dispose();
        }

        private void SmartCard_CardRemoved(object sender, PCSC.CardStatusEventArgs e)
        {
            Output.Text += $"{e.State}{Environment.NewLine}";
        }

        private void SmartCard_CardInserted(object sender, PCSC.CardStatusEventArgs e)
        {
            Output.Text += $"{e.State}{Environment.NewLine}";
        }

        private void Exec_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var commands = new List<string> { "clear", "reset" };
                using (var reader = new PCSC.SCardReader(_scardContext))
                {
                    reader.Connect(Readers.SelectedItem.ToString(), PCSC.SCardShareMode.Shared, PCSC.SCardProtocol.Any);
                    if (Input.Text.ToLower().IndexOf("clear") >= 0)
                    {
                        Output.Clear();
                    }

                    if (Input.Text.ToLower().IndexOf("reset") >= 0)
                    {
                        Output.Text += $"Transaction begin: {reader.BeginTransaction()}";
                    }

                    if (reader.IsConnected == false)
                    {
                        reader.Connect(Readers.SelectedItem.ToString(), PCSC.SCardShareMode.Shared, PCSC.SCardProtocol.Any);
                    }
                    foreach (var input in Input.Text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (commands.Contains(input.ToLower()))
                        {
                            continue;
                        }
                        var result = new byte[256];
                        var command = input.StringToByteArray();
                        var error = reader.Transmit(command, ref result);
                        string responseCode = "";
                        if (result.Length == 2)
                        {
                            responseCode = string.Concat(result.Select(a => a.ToString("x2") + " "));
                        }
                        else
                        {
                            responseCode = $"UTF 8 Casted:{Environment.NewLine}{UTF8Encoding.UTF8.GetString(result)}{Environment.NewLine}Binary HEX:{Environment.NewLine}{BitConverter.ToString(result).Replace('-', ' ')}";
                        }
                        Output.Text += $"{Environment.NewLine}Command: {input} {Environment.NewLine}Response: {error}{Environment.NewLine}{responseCode}";
                    }
                    if (Input.Text.ToLower().IndexOf("reset") >= 0)
                    {
                        Output.Text += $"{Environment.NewLine}Transaction end: {reader.EndTransaction(PCSC.SCardReaderDisposition.Reset)}";
                    }
                }
            }
            catch(Exception ex)
            {
                Output.Text += ex.Message;
            }
        }

        private void Context_StatusChanged(object sender, PCSC.DeviceChangeEventArgs e)
        {
            this.Output.Text += string.Concat(e.AllReaders);
        }
    }
}

// This is a very high-level and simplified example. Actual implementation would be much more involved.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.AspNetCore.Http.Features;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;


namespace phantom.WPF.AndroidFileManagement
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindowModel CurrentContext = new MainWindowModel();
        private IWebHost? _host;
        private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        public MessageSender? MessageSender;
        private async Task StartApiServer()
        {
            //string portName = "AndroidFileManagement";
            //int portNumber = 4999;

            //List<int> openPorts = await GetOpenPortsAsync(portName: portName);
            //if (openPorts.Contains(portNumber) == false)
            //{
            //    if (OpenPort(portName, portNumber))
            //    {
            //        Console.WriteLine($"Successfully opened port {portNumber}.");
            //    }
            //    else
            //    {
            //        Console.WriteLine($"Failed to open port {portNumber}.");
            //    }
            //}
            //else
            //{
            //    Console.WriteLine($"Port {portNumber} is already open.");
            //}

            _host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    // Configure Kestrel options here
                    options.Limits.MaxRequestBodySize = long.MaxValue; // Example
                    options.Limits.MaxRequestLineSize = int.MaxValue;
                    options.Limits.MaxRequestBufferSize = int.MaxValue;
                    //options.Limits.MaxRequestHeadersTotalSize = int.MaxValue;
                })
                .UseUrls("http://192.168.2.105:5001") // Specify the URL and port
                .ConfigureServices(services =>
                {
                    // Configure dependencies if needed
                    services.AddSignalR(); // Add SignalR services
                    services.AddSingleton<MessageSender>();
                    services.Configure<FormOptions>(options =>
                    {
                        options.MultipartBodyLengthLimit = long.MaxValue;
                        options.ValueLengthLimit = int.MaxValue; // Optional, for form values
                        options.KeyLengthLimit = int.MaxValue;   // Optional, for form keys
                    });
                    services.AddMvcCore()
                        //.AddJsonOptions(options =>
                        //{
                        //    // Configure JSON serialization options if needed
                        //})
                        //.AddApiExplorer() // For API discovery
                        .AddAuthorization() // For authorization features
                        .AddFormatterMappings() // For content negotiation
                                                //.AddDataAnnotations() // For data annotations
                        .AddControllersAsServices();
                })
                .Configure(app =>
                {
                    app.UseSignalR(routes =>
                    {
                        routes.MapHub<MyHub>("/myhub");
                        // You can configure other hub routes here
                    });
                    app.UseMvcWithDefaultRoute(); // Enable MVC routing BEFORE app.Run
                    app.UseMvc(routes =>
                    {
                        routes.MapRoute(
                            name: "default",
                            template: "api/{controller}/{action}/{id?}");
                    });
                    app.Run(async (context) =>
                    {
                        if (context.Request.Path == "/api/listview" && context.Request.Method == "POST")
                        {
                            string requestBody;
                            using (var reader = new StreamReader(context.Request.Body))
                            {
                                requestBody = await reader.ReadToEndAsync();
                            }
                            try
                            {
                                var jsonData = JsonSerializer.Deserialize<ListViewAPIModel>(requestBody, _jsonSerializerOptions);
                                CurrentContext.IsEnabled = true;
                                CurrentContext.CurrentPath = jsonData!.Path;
                                CurrentContext.ListViewModel = new ObservableCollection<ListViewModel>(jsonData!.Files?.OrderByDescending(x => x.Type)?.ToList() ?? new List<ListViewModel>());
                            }
                            catch (JsonException)
                            {
                                context.Response.StatusCode = 400; // Bad Request
                                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { Error = "Invalid JSON" }));
                            }
                            var data = new { Code = 0, Message = string.Empty };
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(data));
                        }
                        else
                        {
                            context.Response.StatusCode = 404;
                            await context.Response.WriteAsync("Not Found");
                        }
                    });
                })
                .Build();

            await _host.StartAsync();
            MessageSender = _host.Services.GetRequiredService<MessageSender>();
            System.Diagnostics.Debug.WriteLine("API Server started");
        }
        private async Task<string?> GetFirewallRulesAsync(string portName)
        {
            string result = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("netsh", $"advfirewall firewall show rule name=\"{portName}\"");
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                Process process = new Process();
                process.StartInfo = psi;
                process.Start();
                result = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"Netsh command failed with exit code: {process.ExitCode}");
                    return null;
                }
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting firewall rules: {ex.Message}");
                return null;
            }
        }
        private async Task<System.Collections.Generic.List<int>> GetOpenPortsAsync(string portName)
        {
            List<int> openPorts = new List<int>();
            string? rulesOutput = await GetFirewallRulesAsync(portName: portName);
            if (rulesOutput == null)
            {
                return openPorts; // Return empty list on error
            }

            // Parse the output to find lines containing "Dir=In" and "Action=Allow"
            // and extract the port number from "LocalPort="
            var lines = rulesOutput.Split(new[] { "Rule Name:" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => string.Join(' ', x.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)));
            foreach (string line in lines)
            {
                if (line.Contains("Direction: In") && line.Contains("Action: Allow"))
                {
                    // Use a regular expression to find the port number
                    Match match = Regex.Match(line, @"LocalPort: (\d+)");
                    if (match.Success)
                    {
                        if (int.TryParse(match.Groups[1].Value, out int port))
                        {
                            openPorts.Add(port);
                        }
                    }
                }
            }
            return openPorts;
        }
        private bool OpenPort(string portName, int portNumber, string protocol = "TCP")
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("netsh", $"advfirewall firewall add rule name=\"{portName}\" dir=in action=allow protocol={protocol} localport={portNumber}");
                psi.Verb = "runas"; // Run as administrator
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden; // Optional: Run in the background

                Process process = new Process();
                process.StartInfo = psi;
                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    return true; // Port opened successfully
                }
                else
                {
                    Console.WriteLine($"Failed to open port.  Netsh exit code: {process.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening port: {ex.Message}");
                return false;
            }
        }
        public void SetMainProgressBarMaxValue(double maxValue)
        {
            this.Dispatcher.Invoke(() => MainProgressBar.Maximum = maxValue);
        }

        public MainWindow()
        {
            InitializeComponent();
            Globals.MainWindow = this;
            this.DataContext = CurrentContext;
            //MainListView.ItemContainerGenerator.StatusChanged += (s, e) =>
            //{
            //    if (MainListView.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            //    {
            //        if (CurrentContext.ListViewModel?.Count > 0)
            //        {
            //            var itemToSelect = MainListView.ItemContainerGenerator.ContainerFromItem(CurrentContext.ListViewModel.First());
            //            Selector.SetIsSelected(itemToSelect, true);
            //        }
            //    }
            //};

            _ = StartApiServer();
        }

        protected override void OnClosed(EventArgs e)
        {
            _host?.StopAsync().Wait();
            base.OnClosed(e);
        }

        private void StackPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var model = (e.OriginalSource as FrameworkElement)?.DataContext as ListViewModel;
            if (e.ClickCount == 2)
            {
                // Handle other potential double-clicked elements
                e.Handled = true; // Optional: Mark as handled at the StackPanel level
                if (model?.Type == "Folder")
                {
                    CurrentContext.CurrentPath = model!.Path;
                    CurrentContext.IsEnabled = false;
                    MessageSender?.SendMessage(CurrentContext.CurrentPath!);
                }
            }
            if (model == null) return;
            MainListView.ItemsSource.Cast<ListViewModel>().Where(x => x.BorderBrush != Brushes.Transparent).ToList().ForEach(x =>
            {
                x.BorderBrush = Brushes.Transparent;
                x.BackgroundColor = Brushes.Transparent;
            });
            model.BorderBrush = Brushes.LightGray;
            model.BackgroundColor = Brushes.AliceBlue;
        }

        private void BackButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentContext.CurrentPath)) return;
            CurrentContext.CurrentPath = string.Join("/", CurrentContext.CurrentPath!.Split('/').SkipLast(1));
            CurrentContext.IsEnabled = false;
            MessageSender?.SendMessage(CurrentContext.CurrentPath!);
        }

        private void UploadButton_Clicked(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            if (openFileDialog.ShowDialog() == false) return;
            var filePath = openFileDialog.FileName;
            CurrentContext.IsEnabled = false;
            CurrentContext.ProgressBarValue = 0;
            MainProgressBar.Maximum = 0;
            CurrentContext.IsNotDownloading = Visibility.Visible;
            long fileLength = new FileInfo(filePath).Length;
            MessageSender?.SendMessage(CurrentContext.CurrentPath!, "UPLOAD", filePath, $"{fileLength}");
        }

        private void MenuItemDelete_Clicked(object sender, RoutedEventArgs e)
        {
            var model = (sender as FrameworkElement)?.DataContext as ListViewModel;
            if (model == null) return;
            MessageSender?.SendMessage(model.Path!, "DELETE");
        }

        private void MoveMenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            var model = (sender as FrameworkElement)?.DataContext as ListViewModel;
            if (model == null || model.Type != "File") return;
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = model.Name;
            var extIndex = model.Name!.LastIndexOf(".");
            var ext = model.Name.Substring(extIndex, model.Name.Length - extIndex);
            saveFileDialog.Filter = $"{ext.TrimStart('.')} Files (*{ext})|*{ext}|All Files (*.*)|*.*";
            if (saveFileDialog.ShowDialog() == false) return;
            var saveFilePath = saveFileDialog.FileName;
            CurrentContext.IsEnabled = false;
            CurrentContext.ProgressBarValue = 0;
            MainProgressBar.Maximum = 0;
            CurrentContext.IsNotDownloading = Visibility.Visible;
            MessageSender?.SendMessage(model.Path!, "MOVE", saveFilePath);
        }
    }
    public class MainWindowModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        internal void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private ObservableCollection<ListViewModel>? _listViewModel;
        public ObservableCollection<ListViewModel>? ListViewModel
        {
            get => _listViewModel;
            set
            {
                _listViewModel = value;
                OnPropertyChanged(nameof(ListViewModel));
            }
        }
        private string? _currentPath;
        public string? CurrentPath
        {
            get => _currentPath;
            set
            {
                _currentPath = value;
                OnPropertyChanged(nameof(CurrentPath));
            }
        }
        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }
        private double _progressBarValue;
        public double ProgressBarValue
        {
            get => _progressBarValue;
            set
            {
                _progressBarValue = value;
                OnPropertyChanged(nameof(ProgressBarValue));
            }
        }
        private Visibility _isNotDownloading = Visibility.Collapsed;
        public Visibility IsNotDownloading
        {
            get => _isNotDownloading;
            set
            {
                _isNotDownloading = value;
                OnPropertyChanged(nameof(IsNotDownloading));
            }
        }
    }
    public class ListViewAPIModel
    {
        public string? Path { get; set; }
        public List<ListViewModel>? Files { get; set; }
    }
    public class ListViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        internal void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public string? Name { get; set; }
        public string? Path { get; set; }
        private string? _type;
        public string? Type
        {
            get => _type; set
            {
                _type = value;
                switch (value)
                {
                    case "File":
                        _imagePath = "pack://application:,,,/phantom.WPF.AndroidFileManagement;component/Images/file-128.png";
                        break;
                    case "Folder":
                        _imagePath = "pack://application:,,,/phantom.WPF.AndroidFileManagement;component/Images/folder-128.png";
                        break;
                    default:
                        _imagePath = null;
                        break;
                }
            }
        }
        public string? Size { get; set; }
        public string? DateModified { get; set; }
        private string? _imagePath;
        public string? ImagePath
        {
            get => _imagePath;
            set
            {
                _imagePath = value;
                OnPropertyChanged(nameof(ImagePath));
            }
        }
        private int _borderThickness = 0;
        public int BorderThickness
        {
            get => _borderThickness;
            set
            {
                _borderThickness = value;
                OnPropertyChanged(nameof(BorderThickness));
            }
        }
        private Brush _backgroundColor = Brushes.Transparent;
        public Brush BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                OnPropertyChanged(nameof(BackgroundColor));
            }
        }
        private Brush _borderBrush = Brushes.Transparent;
        public Brush BorderBrush
        {
            get => _borderBrush;
            set
            {
                _borderBrush = value;
                OnPropertyChanged(nameof(BorderBrush));
            }
        }
    }
}
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
using System.Windows.Controls;


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
        private MessageSender? _messageSender;
        private async Task StartApiServer()
        {
            _host = new WebHostBuilder()
                .UseKestrel()
                .UseUrls("http://192.168.2.105:5001") // Specify the URL and port
                .ConfigureServices(services =>
                {
                    // Configure dependencies if needed
                    services.AddSignalR(); // Add SignalR services
                    services.AddSingleton<MessageSender>();
                })
                .Configure(app =>
                {
                    app.UseSignalR(routes =>
                    {
                        routes.MapHub<MyHub>("/myhub");
                        // You can configure other hub routes here
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
            _messageSender = _host.Services.GetRequiredService<MessageSender>();
            System.Diagnostics.Debug.WriteLine("API Server started");
        }

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = CurrentContext;
            _ = StartApiServer();
        }

        protected override void OnClosed(EventArgs e)
        {
            _host?.StopAsync().Wait();
            base.OnClosed(e);
        }

        private void StackPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Handle other potential double-clicked elements
                e.Handled = true; // Optional: Mark as handled at the StackPanel level
                var model = (e.OriginalSource as FrameworkElement)?.DataContext as ListViewModel;
                if (model?.Type == "Folder")
                {
                    CurrentContext.CurrentPath = model!.Path;
                    CurrentContext.IsEnabled = false;
                    _messageSender?.SendNotificationToAll(CurrentContext.CurrentPath!);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentContext.CurrentPath)) return;
            CurrentContext.CurrentPath = string.Join("/", CurrentContext.CurrentPath!.Split('/').SkipLast(1));
            CurrentContext.IsEnabled = false;
            _messageSender?.SendNotificationToAll(CurrentContext.CurrentPath!);
        }

        private void MenuItemDelete_Clicked(object sender, RoutedEventArgs e)
        {
            var model = (sender as FrameworkElement)?.DataContext as ListViewModel;
            if (model == null) return;
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
    }
}
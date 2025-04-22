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
using System.Threading.Tasks;
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
                                var jsonData = JsonSerializer.Deserialize<IEnumerable<ListViewModel>>(requestBody, _jsonSerializerOptions);
                                this.Dispatcher.Invoke(() => { this.IsEnabled = true; });
                                CurrentContext.ListViewModel = new ObservableCollection<ListViewModel>(jsonData ?? new List<ListViewModel>());
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

        private string? currentPath;
        private void StackPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Handle other potential double-clicked elements
                e.Handled = true; // Optional: Mark as handled at the StackPanel level
                var model = (e.OriginalSource as TextBlock)?.DataContext as ListViewModel;
                if (model?.Type == "Folder")
                {
                    currentPath = model!.Path;
                    this.Dispatcher.Invoke(() => { this.IsEnabled = false; });
                    _messageSender?.SendNotificationToAll(currentPath!);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentPath)) return;
            currentPath = string.Join("/", currentPath!.Split('/').SkipLast(1));
            this.Dispatcher.Invoke(() => { this.IsEnabled = false; });
            _messageSender?.SendNotificationToAll(currentPath!);
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
    }
    public class ListViewModel
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
        public string? Type { get; set; }
        public string? Size { get; set; }
        public string? DateModified { get; set; }
    }
}
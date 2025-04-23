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
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.AspNetCore.Http.Features;


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
                        else if (context.Request.Path == "/api/uploadchunk" && context.Request.Method == "POST")
                        {
                            try
                            {
                                var fileChunk = context.Request.Form.Files.FirstOrDefault();
                                var fileName = context.Request.Form["fileName"].FirstOrDefault();
                                var totalSize = long.Parse(context.Request.Form["totalSize"].FirstOrDefault() ?? "");
                                var offset = int.Parse(context.Request.Form["offset"].FirstOrDefault() ?? "");
                                var partNumber = int.Parse(context.Request.Form["partNumber"].FirstOrDefault() ?? "");
                                var totalParts = int.Parse(context.Request.Form["totalParts"].FirstOrDefault() ?? "");

                                if (fileChunk == null || fileChunk.Length == 0 || string.IsNullOrEmpty(fileName))
                                {
                                    context.Response.StatusCode = 400; // Bad Request

                                    await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { Error = "Invalid chunk data." }));
                                    return;
                                }

                                string _uploadDirectory = "UploadDatas";
                                var tempFilePath = Path.Combine(_uploadDirectory, $"{fileName}.part_{partNumber}");

                                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                                {
                                    await fileChunk.CopyToAsync(stream);
                                }

                                // Check if all parts have been uploaded
                                var allParts = Directory.GetFiles(_uploadDirectory, $"{fileName}.part_*")
                                                        .OrderBy(f => int.Parse(Path.GetFileName(f).Split('_').Last()));

                                if (allParts.Count() == totalParts)
                                {
                                    // Reassemble the file
                                    var finalFilePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", fileName);
                                    using (var finalStream = new FileStream(finalFilePath, FileMode.Create))
                                    {
                                        foreach (var partPath in allParts)
                                        {
                                            using (var partStream = new FileStream(partPath, FileMode.Open))
                                            {
                                                await partStream.CopyToAsync(finalStream);
                                            }
                                            System.IO.File.Delete(partPath); // Clean up temporary parts
                                        }
                                    }
                                    await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { Message = "File uploaded successfully", FileName = fileName, FilePath = finalFilePath }));
                                    return;
                                }

                                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { Message = $"Chunk {partNumber} received" }));
                            }
                            catch (Exception ex)
                            {
                                context.Response.StatusCode = 500;
                                await context.Response.WriteAsync($"Error processing chunk: {ex.Message}");
                            }
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
                    _messageSender?.SendMessage(CurrentContext.CurrentPath!);
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentContext.CurrentPath)) return;
            CurrentContext.CurrentPath = string.Join("/", CurrentContext.CurrentPath!.Split('/').SkipLast(1));
            CurrentContext.IsEnabled = false;
            _messageSender?.SendMessage(CurrentContext.CurrentPath!);
        }

        private void MenuItemDelete_Clicked(object sender, RoutedEventArgs e)
        {
            var model = (sender as FrameworkElement)?.DataContext as ListViewModel;
            if (model == null) return;
            _messageSender?.SendMessage(model.Path!, "DELETE");
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
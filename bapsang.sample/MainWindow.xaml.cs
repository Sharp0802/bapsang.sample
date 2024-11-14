using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows;
using System.Windows.Media.Imaging;

namespace bapsang.sample;

public partial class MainWindow
{
    private readonly string? _bearer;
    
    private readonly HttpClient _client = new();
    private string? _source;
    
    public MainWindow()
    {
        InitializeComponent();

        try
        {
            _bearer = File.ReadAllText(".bearer");
        }
        catch (Exception e)
        {
            _bearer = null;
            MessageBox.Show(e.Message, "Couldn't read file", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        var watcher = new FileSystemWatcher("C:/bapsang/", "*.png");
        watcher.NotifyFilter = NotifyFilters.Attributes
                               | NotifyFilters.CreationTime
                               | NotifyFilters.DirectoryName
                               | NotifyFilters.FileName
                               | NotifyFilters.LastAccess
                               | NotifyFilters.LastWrite
                               | NotifyFilters.Security
                               | NotifyFilters.Size;
        watcher.Created += (_, args) =>
        {
            Console.WriteLine(args.FullPath);
            _source = args.FullPath;
            Thread.Sleep(100);
            Dispatcher.Invoke(() =>
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(args.FullPath);
                bitmap.EndInit();
                ImageFrame.Source = bitmap;
            });
        };

        watcher.EnableRaisingEvents = true;

        Closed += (_, _) =>
        {
            watcher.Dispose();
            _client.Dispose();
        };
    }

    private async void Send(object sender, RoutedEventArgs e)
    {
        Dispatcher.Invoke(() => Result.Text = "loading...");
        
        string result;
        if (_source is null)
        {
            result = "Source image is null";
        }
        else
        {
            var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(await File.ReadAllBytesAsync(_source)), "image", "image.png");
        
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://bapsang.sspzoa.io/analyze-food-positions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearer);
            request.Content = content;

            var rawResponse = await _client.SendAsync(request);
            try
            {
                using var response = rawResponse.EnsureSuccessStatusCode();
                
                result = await response.Content.ReadAsStringAsync();
                var element = JsonSerializer.Deserialize<JsonElement>(result);
                result = JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(new TextEncoderSettings(UnicodeRange.Create((char)0, (char)65535)))});
            }
            catch (HttpRequestException exception)
            {
                result = $"{exception.Message}\n===== RESPONSE =====\n{await rawResponse.Content.ReadAsStringAsync()}";
            }
        }

        Dispatcher.Invoke(() => Result.Text = result);
    }
}

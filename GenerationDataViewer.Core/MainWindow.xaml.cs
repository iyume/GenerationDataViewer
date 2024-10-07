using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using Wpf.Ui.Appearance;

namespace GenerationDataViewer.Core;
// Keep same as <Window x:Class="GenerationDataViewer.Core.MainWindow">

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ApplicationThemeManager.Apply(this);
        // A1111Prompt.TextChanged += (s, e) => { };
    }

    private void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg"
        };

        if (openFileDialog.ShowDialog() == false) return;

        UploadImageInformation.Foreground = null;
        UploadImageInformation.Text = "Uploading image...";

        // "pack://application:,,,/AssemblyName;component/Resources/logo.png"
        // AssemblyShortName[;Version][;PublicKey];component/Path
        var fileBytes = File.ReadAllBytes(openFileDialog.FileName);
        var ms = new MemoryStream(fileBytes);
        BitmapImage bitmap = new();
        // no necessary Dispose on MemoryStream
        // https://learn.microsoft.com/en-us/dotnet/api/system.io.memorystream?view=net-8.0
        bitmap.BeginInit();
        bitmap.StreamSource = ms;
        bitmap.EndInit();

        UploadedImage.Height = 200;
        UploadedImage.Source = bitmap;
        LoadImage(ms);
        UploadImageInformation.Foreground = Brushes.Green;
        UploadImageInformation.Text = "Image uploaded";
    }

    private async void UploadUrlButton_Click(object sender, RoutedEventArgs e)
    {
        if (UploadImageUrl.Text.Length == 0 ||
            (!UploadImageUrl.Text.StartsWith("http://") && !UploadImageUrl.Text.StartsWith("https://")))
        {
            UploadImageInformation.Foreground = Brushes.Red;
            UploadImageInformation.Text = "Please provide a valid URL";
            return;
        }

        UploadImageInformation.Foreground = Brushes.Black;
        UploadImageInformation.Text = "Downloading image...";

        Stream fileStream;
        using (var client = new HttpClient())
        {
            var response = await client.GetAsync(UploadImageUrl.Text);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                UploadImageInformation.Foreground = Brushes.Black;
                UploadImageInformation.Text = null;
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            fileStream = await response.Content.ReadAsStreamAsync();
        }

        BitmapImage bitmap = new();
        try
        {
            bitmap.BeginInit();
            bitmap.StreamSource = fileStream;
            bitmap.EndInit();
        }
        catch (Exception ex)
        {
            UploadImageInformation.Foreground = Brushes.Black;
            UploadImageInformation.Text = null;
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        UploadedImage.Height = 300;
        UploadedImage.Source = bitmap;
        LoadImage(fileStream);
        UploadImageInformation.Foreground = Brushes.Green;
        UploadImageInformation.Text = "Image uploaded";
    }

    /// <summary>
    ///     Load image from stream, clear and update image data.
    /// </summary>
    /// <param name="ms"></param>
    private void LoadImage(Stream ms)
    {
        ClearExistImageData();
        ms.Position = 0;
        ImageInfo imageInfo;
        try
        {
            imageInfo = Image.Identify(ms);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        switch (imageInfo.Metadata.DecodedImageFormat)
        {
            case PngFormat:
            {
                var pngMetadata = imageInfo.Metadata.GetPngMetadata();
                switch (pngMetadata.TextData)
                {
                    case [{ Keyword: "parameters" }]:
                        SetUploadedImageFormat("A1111 (webui)");
                        LoadGenerationDataA1111(pngMetadata.TextData[0].Value);
                        break;
                    case [{ Keyword: "prompt" }, { Keyword: "workflow" }]:
                        SetUploadedImageFormat("ComfyUI");
                        LoadGenerationDataComfyui(pngMetadata.TextData[0].Value, pngMetadata.TextData[1].Value);
                        break;
                    default:
                        UploadedImageDescription.Visibility = Visibility.Collapsed;
                        MessageBox.Show("Unknown image format", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }

                break;
            }
            case JpegFormat:
            {
                var exif = imageInfo.Metadata.ExifProfile;
                break;
            }
        }

        return;

        void SetUploadedImageFormat(string text)
        {
            UploadedImageDescription.Visibility = Visibility.Visible;
            UploadedImageDescription.Inlines.Clear();
            UploadedImageDescription.Inlines.Add("Image format: ");
            UploadedImageDescription.Inlines.Add(new Run(text)
                { Foreground = Brushes.Green, FontWeight = FontWeights.Bold });
        }
    }

    private void ParseA1111GenerationData(string data)
    {
        // A1111 webui standard
        // Steps: 20, Sampler: Euler a, CFG scale: 7, Seed: 736439749, Size: 512x512, Model hash: c88e730a
        // dog, outside, positive\nSteps: 20, Sampler: Euler a, CFG scale: 7, Seed: 3359221883, Size: 512x512, Model hash: c88e730a
        // Negative prompt: easynegative\nSteps: 20, Sampler: Euler a, CFG scale: 7, Seed: 587383101, Size: 512x512, Model hash: c88e730a
        // girl\nNegative prompt: easynegative\nSteps: 20, Sampler: Euler a, CFG scale: 7, Seed: 1907347921, Size: 512x512, Model hash: c88e730a
        // dog,\ntwo dog\nNegative prompt: easynegative,\nbad hands\nSteps: 20, Sampler: Euler a, CFG scale: 7, Seed: 1358599088, Size: 512x512, Model hash: c88e730a
        data = data.Trim();
        var detailsIndex = data.LastIndexOf("Steps: ", StringComparison.Ordinal);
        if (detailsIndex == -1)
        {
            MessageBox.Show("Invalid data", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var details = data[detailsIndex..];
        A1111Detail.Text = details;
        data = data[..detailsIndex].TrimEnd();
        if (data.Length == 0) return;

        var negPromptIndex = data.LastIndexOf("Negative prompt: ", StringComparison.Ordinal);
        if (negPromptIndex != -1)
        {
            var negPrompt = data[(negPromptIndex + "Negative prompt: ".Length)..];
            A1111NegativePrompt.Text = negPrompt;
            data = data[..negPromptIndex].TrimEnd();
        }

        A1111Prompt.Text = data;
    }

    private void ClearExistImageData()
    {
        A1111Prompt.Text = string.Empty;
        A1111NegativePrompt.Text = string.Empty;
        A1111Detail.Text = string.Empty;
        ComfyuiPrompt.Text = string.Empty;
        ComfyuiWorkflow.Text = string.Empty;
    }

    private void LoadGenerationDataA1111(string data)
    {
        LayoutGenerationDataA1111.Visibility = Visibility.Visible;
        LayoutGenerationDataComfyui.Visibility = Visibility.Collapsed;
        ParseA1111GenerationData(data);
    }

    private void LoadGenerationDataComfyui(string prompt, string workflow)
    {
        LayoutGenerationDataA1111.Visibility = Visibility.Collapsed;
        LayoutGenerationDataComfyui.Visibility = Visibility.Visible;
        var promptJson = JsonSerializer.Deserialize<object>(prompt);
        var workflowJson = JsonSerializer.Deserialize<object>(workflow);
        promptJson ??= new { };
        workflowJson ??= new { };
        // https://docs.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializeroptions?view=net-8.0
        ComfyuiPrompt.Text = JsonSerializer.Serialize(promptJson, new JsonSerializerOptions { WriteIndented = true });
        ComfyuiWorkflow.Text =
            JsonSerializer.Serialize(workflowJson, new JsonSerializerOptions { WriteIndented = true });
    }
}
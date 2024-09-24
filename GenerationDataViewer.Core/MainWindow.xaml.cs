using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace GenerationDataViewer.Core;
// Keep same as <Window x:Class="GenerationDataViewer.Core.MainWindow">

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        ApplicationThemeManager.Apply(this);
    }

    private void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new();
        openFileDialog.Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg";

        if (openFileDialog.ShowDialog() == true)
        {
            UploadImageInformation.Foreground = null;
            UploadImageInformation.Text = "Uploading image...";
            // try?
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(openFileDialog.FileName);
            bitmap.EndInit();
            UploadedImage.Height = 200;
            UploadedImage.Source = bitmap;
            UploadImageInformation.Foreground = Brushes.Green;
            UploadImageInformation.Text = "Image uploaded";
        }
    }

    private void UploadUrlButton_Click(object sender, RoutedEventArgs e)
    {
        if (UploadImageUrl.Text.Length == 0 ||
            (!UploadImageUrl.Text.StartsWith("http://") && !UploadImageUrl.Text.StartsWith("https://")))
        {
            UploadImageInformation.Foreground = Brushes.Red;
            UploadImageInformation.Text = "Please provide a valid URL";
            return;
        }

        UploadImageInformation.Foreground = Brushes.Black;
        UploadImageInformation.Text = "Uploading image...";
        BitmapImage bitmap = new();
        bitmap.DownloadCompleted += (s, e) =>
        {
            UploadImageInformation.Foreground = Brushes.Green;
            UploadImageInformation.Text = "Image uploaded";
        };

        bitmap.BeginInit();
        try
        {
            bitmap.UriSource = new Uri(UploadImageUrl.Text, UriKind.Absolute);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        bitmap.EndInit();
        UploadedImage.Height = 200;
        UploadedImage.Source = bitmap;
    }
}
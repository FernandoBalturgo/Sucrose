using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SPS = Sucrose.Portal.Services.SteamService;
using SPSCPC = Sucrose.Portal.Views.Controls.SteamPreview;
using SPSV = Sucrose.Portal.Services.SteamItem;

namespace Sucrose.Portal.Views.Controls
{
    /// <summary>
    /// SteamStoreCard.xaml etkileşim mantığı
    /// </summary>
    public partial class SteamStoreCard : UserControl, IDisposable
    {
        private readonly SPSV Item;

        internal SteamStoreCard(SPSV Item)
        {
            this.Item = Item;

            InitializeComponent();
        }

        private void SteamStoreCard_Loaded(object sender, RoutedEventArgs e)
        {
            if (Imagine.Source != null)
            {
                return;
            }

            ToolTip = string.IsNullOrWhiteSpace(Item.Title) ? null : Item.Title;

            ThemeType.Text = GetType(Item.FileName);

            ThemeSize.Text = Item.ImageWidth > 0 && Item.ImageHeight > 0 ? $"{Item.ImageWidth}x{Item.ImageHeight}" : string.Empty;

            string Preview = Item.PreviewUrl;

            if (SPS.IsPreviewUrl(Preview))
            {
                try
                {
                    BitmapImage Image = new();

                    Image.BeginInit();
                    Image.UriSource = new Uri(Preview);
                    Image.DecodePixelWidth = 480;
                    Image.CacheOption = BitmapCacheOption.OnLoad;
                    Image.EndInit();

                    Imagine.Source = Image;
                }
                catch
                {
                }
            }
        }

        private async void SteamStoreCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SPSCPC Preview = new(Item);

            await Preview.ShowAsync();

            Preview.Dispose();
        }

        private static string GetType(string FileName)
        {
            string Extension = Path.GetExtension(FileName).TrimStart('.').ToLowerInvariant();

            return Extension switch
            {
                "gif" => "GIF",
                "mp4" or "webm" or "mov" or "mkv" or "avi" => "VIDEO",
                "html" or "htm" => "WEB",
                "png" => "PNG",
                "jpg" or "jpeg" => "JPG",
                "webp" => "WEBP",
                _ => Extension.ToUpperInvariant()
            };
        }

        public void Dispose()
        {
            GC.Collect();
            GC.SuppressFinalize(this);
        }
    }
}

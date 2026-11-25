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

            if (SPS.IsSteamUrl(Item.PreviewUrl))
            {
                try
                {
                    BitmapImage Image = new();

                    Image.BeginInit();
                    Image.UriSource = new Uri(Item.PreviewUrl);
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

        public void Dispose()
        {
            GC.Collect();
            GC.SuppressFinalize(this);
        }
    }
}

using Newtonsoft.Json;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using SHG = Skylark.Helper.Generator;
using SHV = Skylark.Helper.Versionly;
using SMMB = Sucrose.Manager.Manage.Backgroundog;
using SMMCL = Sucrose.Memory.Manage.Constant.Library;
using SMME = Sucrose.Manager.Manage.Engine;
using SMMG = Sucrose.Manager.Manage.General;
using SMMI = Sucrose.Manager.Manage.Internal;
using SMML = Sucrose.Manager.Manage.Library;
using SMMP = Sucrose.Manager.Manage.Portal;
using SMMRC = Sucrose.Memory.Manage.Readonly.Content;
using SMMRF = Sucrose.Memory.Manage.Readonly.Folder;
using SMMRG = Sucrose.Memory.Manage.Readonly.General;
using SMMRP = Sucrose.Memory.Manage.Readonly.Path;
using SMMRS = Sucrose.Memory.Manage.Readonly.Soferity;
using SMMRU = Sucrose.Memory.Manage.Readonly.Url;
using SMMVA = Sucrose.Memory.Manage.Valuable.App;
using SPEIL = Sucrose.Portal.Extension.ImageLoader;
using SPMI = Sucrose.Portal.Manage.Internal;
using SPVCTR = Sucrose.Portal.Views.Controls.ThemeReport;
using SRER = Sucrose.Resources.Extension.Resources;
using SRHR = Sucrose.Resources.Helper.Resources;
using SSCHV = Sucrose.Shared.Core.Helper.Version;
using SSDECT = Sucrose.Shared.Dependency.Enum.CommandType;
using SSDESST = Sucrose.Shared.Dependency.Enum.StoreServerType;
using SSDMI = Sucrose.Shared.Dependency.Manage.Internal;
using SSDMMP = Sucrose.Shared.Dependency.Manage.Manager.Portal;
using SSLHK = Sucrose.Shared.Live.Helper.Kill;
using SSLHR = Sucrose.Shared.Live.Helper.Run;
using SSSHC = Sucrose.Shared.Space.Helper.Copy;
using SSSHF = Sucrose.Shared.Space.Helper.Filing;
using SSSHGHD = Sucrose.Shared.Store.Helper.GitHub.Download;
using SSSHL = Sucrose.Shared.Space.Helper.Live;
using SSSHN = Sucrose.Shared.Space.Helper.Network;
using SSSHP = Sucrose.Shared.Space.Helper.Processor;
using SSSHS = Sucrose.Shared.Store.Helper.Store;
using SSSHSD = Sucrose.Shared.Store.Helper.Soferity.Download;
using SSSHU = Sucrose.Shared.Space.Helper.User;
using SSSID = Sucrose.Shared.Store.Interface.Data;
using SSSIW = Sucrose.Shared.Store.Interface.Wallpaper;
using SSSMDTD = Sucrose.Shared.Space.Model.DownloadTelemetryData;
using SSSMI = Sucrose.Shared.Space.Manage.Internal;
using SSSTMI = Sucrose.Shared.Store.Manage.Internal;
using SSTCLC = Sucrose.Shared.Theme.Converter.LocalizationConverter;
using SSTHI = Sucrose.Shared.Theme.Helper.Info;
using SSWEW = Sucrose.Shared.Watchdog.Extension.Watch;
using SXAGAB = Sucrose.XamlAnimatedGif.AnimationBehavior;

namespace Sucrose.Portal.Views.Controls
{
    /// <summary>
    /// StoreCard.xaml etkileşim mantığı
    /// </summary>
    public partial class StoreCard : UserControl, IDisposable
    {
        private readonly KeyValuePair<string, SSSIW> Wallpaper = new();
        private readonly string Theme = string.Empty;
        private readonly SPEIL Loader = new();
        private readonly string Guid;
        private SSTHI Info;

        internal StoreCard(string Theme, KeyValuePair<string, SSSIW> Wallpaper)
        {
            this.Theme = Theme;
            this.Wallpaper = Wallpaper;
            Guid = Path.Combine(Wallpaper.Value.Source, Wallpaper.Key);

            InitializeComponent();
        }

        private async Task<bool> DownloadCache()
        {
            if (SPMI.StoreDownloader.ContainsKey(Theme))
            {
                while (!SPMI.StoreDownloading.ContainsKey(Theme) || !SPMI.StoreDownloading[Theme])
                {
                    await Task.Delay(100);
                }

                Info = SSTHI.ReadJson(Path.Combine(Theme, SMMRC.SucroseInfo));

                return true;
            }
            else
            {
                SPMI.StoreDownloader[Theme] = false;

                SPMI.StoreDownloader[Theme] = SSDMMP.StoreServerType switch
                {
                    SSDESST.GitHub => SSSHGHD.Cache(Wallpaper, Theme),
                    _ => SSSHSD.Cache(Wallpaper, Theme),
                };

                if (SPMI.StoreDownloader[Theme])
                {
                    while (!SPMI.StoreDownloading.ContainsKey(Theme) || !SPMI.StoreDownloading[Theme])
                    {
                        await Task.Delay(100);
                    }

                    Info = SSTHI.ReadJson(Path.Combine(Theme, SMMRC.SucroseInfo));

                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private async void StoreCard_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                bool Result = await Task.Run(DownloadCache);

                if (Result)
                {
                    string ImagePath = Path.Combine(Theme, Info.Thumbnail);

                    if (File.Exists(ImagePath))
                    {
                        try
                        {
                            Imagine.Source = await Loader.LoadOptimalAsync(ImagePath);
                        }
                        catch (Exception Exception)
                        {
                            await SSWEW.Watch_CatchException(Exception);
                        }
                    }

                    ThemeType.Text = GetType(Wallpaper.Value.Live);

                    string ResolutionKey = $"{Wallpaper.Value.Source}/{Wallpaper.Key}";

                    (int Width, int Height) = GetImageSize(ImagePath);

                    if (Width > 0 && Height > 0)
                    {
                        ThemeSize.Text = $"{Width}x{Height}";

                        SPMI.StoreResolution[ResolutionKey] = ThemeSize.Text;
                    }
                    else
                    {
                        ThemeSize.Text = FormatSize(Wallpaper.Value.Size);
                    }

                    await Task.Delay(100);

                    Card.Visibility = Visibility.Visible;
                    Progress.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Cursor = Cursors.Arrow;
                    Warn.Visibility = Visibility.Visible;
                    Progress.Visibility = Visibility.Collapsed;
                }

                Dispose();
            }
            catch
            {
                Cursor = Cursors.Arrow;
                Warn.Visibility = Visibility.Visible;
                Progress.Visibility = Visibility.Collapsed;

                Dispose();
            }
        }

        private void StoreCard_MouseEnter(object sender, MouseEventArgs e)
        {
            if (Info != null && SMMP.StorePreview)
            {
                string GifPath = $"{SSSHS.Source(SSDMMP.StoreServerType)}/{Wallpaper.Value.Source}/{Wallpaper.Key}/{Wallpaper.Value.Live}";

                SXAGAB.SetSourceUri(Imaginer, new(GifPath));
                SXAGAB.AddLoadedHandler(Imaginer, Imaginer_MediaOpened);
            }
        }

        private void StoreCard_MouseLeave(object sender, MouseEventArgs e)
        {
            if (Info != null && SMMP.StorePreview)
            {
                Imaginer.Source = null;
                SXAGAB.SetSourceUri(Imaginer, null);

                Imagine.Visibility = Visibility.Visible;
                Imaginer.Visibility = Visibility.Hidden;

                Dispose();
            }
        }

        private void Imaginer_MediaOpened(object sender, RoutedEventArgs e)
        {
            Imaginer.Visibility = Visibility.Visible;
            Imagine.Visibility = Visibility.Hidden;

            Dispose();
        }

        private void StoreCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpenPreview();
        }

        private async void OpenPreview()
        {
            if (Info == null)
            {
                return;
            }

            StorePreview Preview = new(Theme, Wallpaper, Info, Guid);

            await Preview.ShowAsync();

            Preview.Dispose();
        }

        private void MenuUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (!SSSHP.Work(SSSMI.Update))
            {
                SSSHP.Run(SSSMI.Commandog, $"{SMMRG.StartCommand}{SSDECT.Update}{SMMRG.ValueSeparator}{SSSMI.Update}");
            }
        }

        private void MenuInstall_Click(object sender, RoutedEventArgs e)
        {
            OpenPreview();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            MenuInstall.Header = SRER.GetValue("Portal", "StoreCard", "MenuInstall");

            MenuReport.IsEnabled = Info != null;

            bool Compatible = Info != null && Info.AppVersion.CompareTo(SHV.Entry()) <= 0;

            MenuInstall.IsEnabled = Compatible;

            if (Info != null && Info.AppVersion.CompareTo(SHV.Entry()) > 0)
            {
                MenuUpdate.Visibility = Visibility.Visible;

                if (SSSHP.Work(SSSMI.Update))
                {
                    MenuUpdate.IsEnabled = false;

                    MenuUpdate.Header = SRER.GetValue("Portal", "StoreCard", "MenuUpdating");
                }
                else
                {
                    MenuUpdate.IsEnabled = true;

                    MenuUpdate.Header = SRER.GetValue("Portal", "StoreCard", "MenuUpdate");
                }

                MenuInstall.Header += $" ({SRER.GetValue("Portal", "StoreCard", "Incompatible")})";
            }
            else
            {
                MenuUpdate.Visibility = Visibility.Collapsed;
            }
        }

        private async void MenuReport_Click(object sender, RoutedEventArgs e)
        {
            SPVCTR ThemeReport = new()
            {
                Info = Info,
                Theme = Theme,
                Wallpaper = Wallpaper
            };

            await ThemeReport.ShowAsync();

            ThemeReport.Dispose();
        }

        private static (int Width, int Height) GetImageSize(string ImagePath)
        {
            try
            {
                using FileStream Stream = new(ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                byte[] Buffer = new byte[64 * 1024];

                int Read = Stream.Read(Buffer, 0, Buffer.Length);

                if (Read >= 24 && Buffer[0] == 0x89 && Buffer[1] == 0x50 && Buffer[2] == 0x4E && Buffer[3] == 0x47)
                {
                    int Width = (Buffer[16] << 24) | (Buffer[17] << 16) | (Buffer[18] << 8) | Buffer[19];
                    int Height = (Buffer[20] << 24) | (Buffer[21] << 16) | (Buffer[22] << 8) | Buffer[23];

                    return (Width, Height);
                }

                if (Read >= 10 && Buffer[0] == 0x47 && Buffer[1] == 0x49 && Buffer[2] == 0x46)
                {
                    int Width = Buffer[6] | (Buffer[7] << 8);
                    int Height = Buffer[8] | (Buffer[9] << 8);

                    return (Width, Height);
                }

                if (Read >= 4 && Buffer[0] == 0xFF && Buffer[1] == 0xD8)
                {
                    int Offset = 2;

                    while (Offset + 9 < Read)
                    {
                        if (Buffer[Offset] != 0xFF)
                        {
                            Offset++;
                            continue;
                        }

                        byte Marker = Buffer[Offset + 1];

                        if (Marker == 0xD8 || Marker == 0xD9)
                        {
                            Offset += 2;
                            continue;
                        }

                        int Length = (Buffer[Offset + 2] << 8) | Buffer[Offset + 3];

                        if (Marker >= 0xC0 && Marker <= 0xCF && Marker != 0xC4 && Marker != 0xC8 && Marker != 0xCC)
                        {
                            int Height = (Buffer[Offset + 5] << 8) | Buffer[Offset + 6];
                            int Width = (Buffer[Offset + 7] << 8) | Buffer[Offset + 8];

                            return (Width, Height);
                        }

                        Offset += 2 + Length;
                    }
                }
            }
            catch
            {
            }

            return (0, 0);
        }

        private static string GetType(string Live)
        {
            string Extension = Path.GetExtension(Live).TrimStart('.').ToLowerInvariant();

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

        private static string FormatSize(long Bytes)
        {
            if (Bytes <= 0)
            {
                return string.Empty;
            }

            string[] Units = ["B", "KB", "MB", "GB"];

            double Size = Bytes;

            int Unit = 0;

            while (Size >= 1024 && Unit < Units.Length - 1)
            {
                Size /= 1024;

                Unit++;
            }

            return $"{Size:0.#} {Units[Unit]}";
        }

        public void Dispose()
        {
            Loader.Dispose();

            GC.Collect();
            GC.SuppressFinalize(this);
        }
    }
}

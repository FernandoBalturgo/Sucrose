using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using SHG = Skylark.Helper.Generator;
using SHV = Skylark.Helper.Versionly;
using SMMCL = Sucrose.Memory.Manage.Constant.Library;
using SMMI = Sucrose.Manager.Manage.Internal;
using SMML = Sucrose.Manager.Manage.Library;
using SMMP = Sucrose.Manager.Manage.Portal;
using SMMRC = Sucrose.Memory.Manage.Readonly.Content;
using SMMRG = Sucrose.Memory.Manage.Readonly.General;
using SMMVA = Sucrose.Memory.Manage.Valuable.App;
using SPMI = Sucrose.Portal.Manage.Internal;
using SPS = Sucrose.Portal.Services.SteamService;
using SPSU = Sucrose.Portal.Services.SteamUgcFile;
using SPSV = Sucrose.Portal.Services.SteamItem;
using SRER = Sucrose.Resources.Extension.Resources;
using SSDEWT = Sucrose.Shared.Dependency.Enum.WallpaperType;
using SSSHF = Sucrose.Shared.Space.Helper.Filing;
using SSSHL = Sucrose.Shared.Space.Helper.Live;
using SSLHK = Sucrose.Shared.Live.Helper.Kill;
using SSLHR = Sucrose.Shared.Live.Helper.Run;
using SSTHI = Sucrose.Shared.Theme.Helper.Info;

namespace Sucrose.Portal.Views.Controls
{
    /// <summary>
    /// SteamPreview.xaml etkileşim mantığı
    /// </summary>
    public partial class SteamPreview : ContentDialog, IDisposable
    {
        private readonly SPSV Item;

        internal SteamPreview(SPSV Item) : base(SPMI.ContentDialogService.GetDialogHostEx())
        {
            this.Item = Item;

            InitializeComponent();
        }

        private bool Armed;

        private static string Text(string Key)
        {
            return SRER.GetValue("Portal", "SteamPage", Key);
        }

        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeTitle.Text = string.IsNullOrWhiteSpace(Item.Title) ? Text("Untitled") : Item.Title;

            Title = ThemeTitle.Text;

            ThemeDescription.Text = Item.FileDescription ?? string.Empty;

            ThemeMeta.Text = string.Join(" · ", MetaParts());

            string Preview = Item.PreviewUrl;

            if (SPS.IsPreviewUrl(Preview))
            {
                try
                {
                    BitmapImage Image = new();

                    Image.BeginInit();
                    Image.UriSource = new Uri(Preview);
                    Image.DecodePixelWidth = 1000;
                    Image.CacheOption = BitmapCacheOption.OnLoad;
                    Image.EndInit();

                    Imagine.Source = Image;
                }
                catch
                {
                }
            }
        }

        private List<string> MetaParts()
        {
            List<string> Parts = new();

            if (Item.Views > 0)
            {
                Parts.Add($"{Text("Views")}: {FormatNumber(Item.Views)}");
            }

            if (Item.Favorited > 0)
            {
                Parts.Add($"{Text("Favorites")}: {FormatNumber(Item.Favorited)}");
            }

            if (Item.Subscriptions > 0)
            {
                Parts.Add($"{Text("Subscriptions")}: {FormatNumber(Item.Subscriptions)}");
            }

            if (Item.Tags != null)
            {
                foreach (var Tag in Item.Tags)
                {
                    string Name = string.IsNullOrWhiteSpace(Tag.DisplayName) ? Tag.Tag : Tag.DisplayName;

                    if (!string.IsNullOrWhiteSpace(Name))
                    {
                        Parts.Add(Name);
                    }
                }
            }

            return Parts;
        }

        private static string FormatNumber(long Value)
        {
            if (Value >= 1_000_000_000)
            {
                return $"{Value / 1_000_000_000.0:0.#}B";
            }

            if (Value >= 1_000_000)
            {
                return $"{Value / 1_000_000.0:0.#}M";
            }

            if (Value >= 1_000)
            {
                return $"{Value / 1_000.0:0.#}K";
            }

            return Value.ToString();
        }

        private void OpenSteam_Click(object sender, RoutedEventArgs e)
        {
            if (!SPS.IsId(Item.PublishedFileId))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://steamcommunity.com/sharedfiles/filedetails/?id={Item.PublishedFileId}",
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            string Key = SMMP.SteamApiKey;

            if (string.IsNullOrWhiteSpace(Key))
            {
                return;
            }

            // Replacing a running wallpaper needs a second click to confirm.
            if (!Armed && SSSHL.Run())
            {
                Armed = true;

                ThemeMeta.Text = Text("Replace");

                return;
            }

            Armed = false;

            Download.IsEnabled = false;
            DownloadSymbol.Visibility = Visibility.Collapsed;
            DownloadRing.Visibility = Visibility.Visible;

            string Created = null;

            try
            {
                string FileUrl = Item.FileUrl;

                string UgcName = Item.FileName;

                if (string.IsNullOrWhiteSpace(FileUrl))
                {
                    SPSU Ugc = await SPS.GetUGCFileDetails(Key, Item.HContentFile);

                    FileUrl = Ugc?.FileUrl;

                    UgcName = Ugc?.FileName;
                }

                if (!SPS.IsSteamUrl(FileUrl))
                {
                    await Finish(false, Text("NoDirect"));
                    return;
                }

                string Keys;

                do
                {
                    Keys = SHG.GenerateString(SMMVA.Chars, 25, SMMRG.Randomise);
                } while (Directory.Exists(Path.Combine(SMML.Location, Keys)));

                string LibraryPath = Path.Combine(SMML.Location, Keys);

                Created = LibraryPath;

                Directory.CreateDirectory(LibraryPath);

                string FileName = string.IsNullOrWhiteSpace(UgcName) ? "wallpaper" : Path.GetFileName(UgcName);

                if (FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    FileName = "wallpaper";
                }

                string MediaPath = Path.Combine(LibraryPath, FileName);

                // Artwork/screenshots return a directory URL; the real file is <file_url>/<filename>.
                string DownloadUrl = FileUrl;

                if (DownloadUrl.EndsWith("/", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(FileName))
                {
                    DownloadUrl += Uri.EscapeDataString(FileName);
                }

                await SPS.Download(DownloadUrl, MediaPath);

                string Media = ResolveMedia(LibraryPath, MediaPath);

                if (Media == null)
                {
                    Directory.Delete(LibraryPath, true);

                    await Finish(false, Text("NoMedia"));
                    return;
                }

                SSDEWT Type = GetType(Media);

                string ThumbPath = Path.Combine(LibraryPath, "thumbnail.jpg");

                try
                {
                    if (SPS.IsSteamUrl(Item.PreviewUrl))
                    {
                        await SPS.Download(Item.PreviewUrl, ThumbPath);
                    }
                }
                catch
                {
                }

                SSTHI Info = new()
                {
                    AppVersion = SHV.Entry(),
                    Version = new Version(1, 0, 0, 0),
                    Title = string.IsNullOrWhiteSpace(Item.Title) ? Text("Untitled") : Item.Title,
                    Thumbnail = File.Exists(ThumbPath) ? "thumbnail.jpg" : string.Empty,
                    Preview = File.Exists(ThumbPath) ? "thumbnail.jpg" : Path.GetFileName(Media),
                    Description = Item.FileDescription ?? string.Empty,
                    Author = Item.Creator ?? string.Empty,
                    Type = Type,
                    Source = Path.GetFileName(Media),
                    Tags = Item.Tags?.Select(Tag => string.IsNullOrWhiteSpace(Tag.DisplayName) ? Tag.Tag : Tag.DisplayName).Where(Tag => !string.IsNullOrWhiteSpace(Tag)).ToArray() ?? Array.Empty<string>()
                };

                SSTHI.Write(Path.Combine(LibraryPath, SMMRC.SucroseInfo), Info);

                SSSHF.WriteStream(Path.Combine(LibraryPath, SMMRC.SucroseStore), string.Empty);

                SMMI.LibrarySettingManager.SetSetting(SMMCL.Selected, Path.GetFileName(Keys));

                if (SSSHL.Run())
                {
                    SSLHK.Stop();
                }

                SSLHR.Start();

                await Finish(true, null);
            }
            catch (Exception Exception)
            {
                try
                {
                    // Do not leave a half-downloaded wallpaper in the library.
                    if (Created != null && Directory.Exists(Created) && !File.Exists(Path.Combine(Created, SMMRC.SucroseInfo)))
                    {
                        Directory.Delete(Created, true);
                    }
                }
                catch
                {
                }

                await Finish(false, Exception.Message);
            }
            finally
            {
                Download.IsEnabled = true;
            }
        }

        private async Task Finish(bool Success, string Message)
        {
            DownloadRing.Visibility = Visibility.Collapsed;
            DownloadSymbol.Visibility = Visibility.Visible;

            if (Success)
            {
                DownloadSymbol.Foreground = SRER.GetResource<Brush>("SystemFillColorSuccessBrush");
                DownloadSymbol.Symbol = SymbolRegular.CloudCheckmark24;
            }
            else
            {
                DownloadSymbol.Foreground = SRER.GetResource<Brush>("PaletteRedBrush");
                DownloadSymbol.Symbol = SymbolRegular.CloudOff24;
            }

            if (!string.IsNullOrEmpty(Message))
            {
                ThemeMeta.Text = Message;
            }

            await Task.Delay(3000);

            DownloadSymbol.Foreground = SRER.GetResource<Brush>("TextFillColorPrimaryBrush");
            DownloadSymbol.Symbol = SymbolRegular.CloudArrowDown24;
        }

        private static string ResolveMedia(string Folder, string Downloaded)
        {
            string Extension = Path.GetExtension(Downloaded).ToLowerInvariant();

            if (IsMedia(Extension))
            {
                return Downloaded;
            }

            if (Extension == ".zip")
            {
                try
                {
                    using (ZipArchive Archive = ZipFile.OpenRead(Downloaded))
                    {
                        if (Archive.Entries.Sum(Entry => Entry.Length) > SPS.MaxDownloadBytes * 2)
                        {
                            return null;
                        }
                    }

                    ZipFile.ExtractToDirectory(Downloaded, Folder);

                    File.Delete(Downloaded);

                    return Directory.GetFiles(Folder).FirstOrDefault(File => IsMedia(Path.GetExtension(File).ToLowerInvariant()));
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static bool IsMedia(string Extension)
        {
            return Extension is ".mp4" or ".webm" or ".mov" or ".mkv" or ".gif" or ".png" or ".jpg" or ".jpeg";
        }

        private static bool IsImage(string Path)
        {
            string Extension = System.IO.Path.GetExtension(Path).ToLowerInvariant();

            return Extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp";
        }

        private static SSDEWT GetType(string Path)
        {
            string Extension = System.IO.Path.GetExtension(Path).ToLowerInvariant();

            return Extension switch
            {
                ".gif" or ".png" or ".jpg" or ".jpeg" => SSDEWT.Gif,
                ".html" or ".htm" => SSDEWT.Web,
                _ => SSDEWT.Video
            };
        }

        public void Dispose()
        {
            GC.Collect();
            GC.SuppressFinalize(this);
        }
    }
}
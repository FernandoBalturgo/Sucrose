using Newtonsoft.Json;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    /// StorePreview.xaml etkileşim mantığı
    /// </summary>
    public partial class StorePreview : ContentDialog, IDisposable
    {
        private readonly KeyValuePair<string, SSSIW> Wallpaper;
        private readonly PropertyChangedEventHandler InfoChangedHandler;
        private readonly object SubscribeLock = new();
        private readonly SPEIL Loader = new();
        private readonly string Theme;
        private readonly string Guid;
        private string Keys = string.Empty;
        private bool Subscribed;
        private bool State;
        private bool Error;
        internal SSTHI Info;

        internal StorePreview(string Theme, KeyValuePair<string, SSSIW> Wallpaper, SSTHI Info, string Guid) : base(SPMI.ContentDialogService.GetDialogHostEx())
        {
            this.Theme = Theme;
            this.Wallpaper = Wallpaper;
            this.Info = Info;
            this.Guid = Guid;

            InfoChangedHandler = (s, e) => StoreService_InfoChanged(Keys);

            InitializeComponent();
        }

        private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            (ThemeTitle.Text, ThemeDescription.Text) = SSTCLC.Convert(Info);

            Title = ThemeTitle.Text;

            ThemeMeta.Text = $"{Info.Version} · {Wallpaper.Value.Size} · {Info.Author}";

            ThemeTitle.FlowDirection = SRHR.IsRightToLeftText(ThemeTitle.Text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            ThemeDescription.FlowDirection = SRHR.IsRightToLeftText(ThemeDescription.Text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

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

            string GifPath = $"{SSSHS.Source(SSDMMP.StoreServerType)}/{Wallpaper.Value.Source}/{Wallpaper.Key}/{Wallpaper.Value.Live}";

            SXAGAB.SetSourceUri(Imaginer, new(GifPath));

            if (Info.AppVersion.CompareTo(SHV.Entry()) > 0)
            {
                DownloadSymbol.Visibility = Visibility.Collapsed;
                IncompatibleVersion.Visibility = Visibility.Visible;
            }

            KeyValuePair<string, SSSID> Matching = SSSTMI.StoreService.Info.FirstOrDefault(Pair => Pair.Value.Guid == Guid && Pair.Value.ProgressPercentage < 100);

            if (!Matching.Equals(default(KeyValuePair<string, SSSID>)))
            {
                Keys = Matching.Key;

                State = true;

                Subscribe();

                StoreService_InfoChanged(Keys);

                DownloadSymbol.Symbol = SymbolRegular.Empty;

                DownloadRing.Visibility = Visibility.Visible;
                DownloadSymbol.Visibility = Visibility.Collapsed;
            }
        }

        private void ContentDialog_Unloaded(object sender, RoutedEventArgs e)
        {
            Unsubscribe();
        }

        private async void Start()
        {
            if (Info != null && Info.AppVersion.CompareTo(SHV.Entry()) <= 0)
            {
                if (DownloadSymbol.Symbol == SymbolRegular.CloudArrowDown24)
                {
                    if (await SSSHN.GetHostEntryAsync())
                    {
                        State = true;
                        Error = false;

                        DownloadSymbol.Symbol = SymbolRegular.Empty;

                        DownloadRing.Visibility = Visibility.Visible;
                        DownloadSymbol.Visibility = Visibility.Collapsed;

                        await Task.Run(SendDownload);

                        await Task.Run(DownloadTheme);
                    }
                    else
                    {
                        DownloadSymbol.Foreground = SRER.GetResource<Brush>("PaletteRedBrush");
                        DownloadSymbol.Symbol = SymbolRegular.CloudDismiss24;

                        await Task.Delay(3000);

                        DownloadSymbol.Foreground = SRER.GetResource<Brush>("TextFillColorPrimaryBrush");
                        DownloadSymbol.Symbol = SymbolRegular.CloudArrowDown24;
                    }
                }
            }
            else if (Info != null)
            {
                DownloadSymbol.Visibility = Visibility.Hidden;
                IncompatibleVersion.Visibility = Visibility.Visible;

                if (!SSSHP.Work(SSSMI.Update))
                {
                    SSSHP.Run(SSSMI.Commandog, $"{SMMRG.StartCommand}{SSDECT.Update}{SMMRG.ValueSeparator}{SSSMI.Update}");
                }
            }
        }

        private void Subscribe()
        {
            lock (SubscribeLock)
            {
                if (!Subscribed)
                {
                    Subscribed = true;

                    SSSTMI.StoreService.InfoChanged += InfoChangedHandler;
                }
            }
        }

        private void Unsubscribe()
        {
            lock (SubscribeLock)
            {
                if (Subscribed)
                {
                    Subscribed = false;

                    SSSTMI.StoreService.InfoChanged -= InfoChangedHandler;
                }
            }
        }

        private async void SendDownload()
        {
            try
            {
                if (false && SMMG.TelemetryData)
                {
                    SSSMDTD DownloadData = new()
                    {
                        AppVersion = SSCHV.GetText(),
                        WallpaperTitle = Wallpaper.Key,
                        WallpaperVersion = $"{Info.Version}",
                        WallpaperAppVersion = $"{Info.AppVersion}",
                        WallpaperLocation = $"{Wallpaper.Value.Source.Split('/').LastOrDefault()}/{Wallpaper.Key}"
                    };

                    StringContent Content = new(JsonConvert.SerializeObject(DownloadData, Formatting.Indented), SMMRS.Encoding, SMMRS.ApplicationJson);

                    HttpResponseMessage Response = await SSDMI.Client.PostAsync($"{SMMRU.Soferity}/{SMMRS.Version}/{SMMRS.Telemetry}/{SMMRS.Download}/{SSSHU.GetGuid()}", Content);

                    Response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception Exception)
            {
                await SSWEW.Watch_CatchException(Exception);
            }
        }

        private async void DownloadTheme()
        {
            try
            {
                do
                {
                    Keys = SHG.GenerateString(SMMVA.Chars, 25, SMMRG.Randomise);
                } while (Directory.Exists(Path.Combine(SMML.Location, Keys)));

                Subscribe();

                string LibraryPath = Path.Combine(SMML.Location, Keys);
                string TemporaryPath = Path.Combine(SMMRP.ApplicationData, SMMRG.AppName, SMMRF.Cache, SMMRF.Store, SMMRF.Temporary, Keys);

                switch (SSDMMP.StoreServerType)
                {
                    case SSDESST.GitHub:
                        await SSSHGHD.Theme(Path.Combine(Wallpaper.Value.Source, Wallpaper.Key), TemporaryPath, Guid, Keys);
                        break;
                    default:
                        await SSSHSD.Theme(Path.Combine(Wallpaper.Value.Source, Wallpaper.Key), TemporaryPath, Guid, Keys, Wallpaper.Value.Size);
                        break;
                }

                await Task.Delay(100);

                if (Directory.Exists(TemporaryPath))
                {
                    SSSHC.Folder(TemporaryPath, LibraryPath);

                    SSSHF.WriteStream(Path.Combine(LibraryPath, SMMRC.SucroseStore), string.Empty);

                    if ((!SMMB.ClosePerformance && !SMMB.PausePerformance) || !SSSHP.Work(SSSMI.Backgroundog))
                    {
                        if (SMME.StoreStart)
                        {
                            SMMI.LibrarySettingManager.SetSetting(SMMCL.Selected, Path.GetFileName(Keys));

                            if (SSSHL.Run())
                            {
                                SSLHK.Stop();
                            }

                            SSLHR.Start();
                        }
                    }
                }
            }
            catch (Exception Exception)
            {
                Error = true;

                await SSWEW.Watch_CatchException(Exception);

                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    if (!string.IsNullOrEmpty(Keys) && SSSTMI.StoreService.Info.ContainsKey(Keys))
                    {
                        SSSTMI.StoreService.Info.Remove(Keys);
                    }

                    State = false;

                    Unsubscribe();

                    Download.ToolTip = null;

                    DownloadRing.Visibility = Visibility.Collapsed;
                    DownloadSymbol.Visibility = Visibility.Visible;

                    DownloadSymbol.Foreground = SRER.GetResource<Brush>("PaletteRedBrush");
                    DownloadSymbol.Symbol = SymbolRegular.CloudOff24;

                    await Task.Delay(3000);

                    DownloadSymbol.Foreground = SRER.GetResource<Brush>("TextFillColorPrimaryBrush");
                    DownloadSymbol.Symbol = SymbolRegular.CloudArrowDown24;
                });
            }
        }

        private async void StoreService_InfoChanged(string Keys)
        {
            if (this.Keys == Keys && SSSTMI.StoreService.Info.TryGetValue(Keys, out SSSID Key))
            {
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    DownloadRing.Progress = Key.ProgressPercentage;

                    ToolTip RingTip = new()
                    {
                        Content = Key.Percentage
                    };

                    Download.ToolTip = RingTip;

                    if (Key.ProgressPercentage >= 100)
                    {
                        if (State)
                        {
                            State = false;

                            await Task.Delay(1500);

                            if (!Error)
                            {
                                Unsubscribe();

                                Download.ToolTip = null;
                                DownloadRing.Visibility = Visibility.Collapsed;
                                DownloadSymbol.Visibility = Visibility.Visible;

                                DownloadSymbol.Foreground = SRER.GetResource<Brush>("SystemFillColorSuccessBrush");
                                DownloadSymbol.Symbol = SymbolRegular.CloudCheckmark24;

                                await Task.Delay(3000);

                                DownloadSymbol.Foreground = SRER.GetResource<Brush>("TextFillColorPrimaryBrush");
                                DownloadSymbol.Symbol = SymbolRegular.CloudArrowDown24;

                                if (SSSTMI.StoreService.Info.ContainsKey(Keys))
                                {
                                    SSSTMI.StoreService.Info.Remove(Keys);
                                }
                            }
                        }
                    }
                });
            }
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            Start();
        }

        public void Dispose()
        {
            Unsubscribe();

            Loader.Dispose();

            GC.Collect();
            GC.SuppressFinalize(this);
        }
    }
}
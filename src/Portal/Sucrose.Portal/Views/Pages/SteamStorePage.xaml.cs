using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SMMP = Sucrose.Manager.Manage.Portal;
using SPS = Sucrose.Portal.Services.SteamService;
using SPSCPC = Sucrose.Portal.Views.Controls.SteamStoreCard;
using SPSV = Sucrose.Portal.Services.SteamItem;
using SRER = Sucrose.Resources.Extension.Resources;

namespace Sucrose.Portal.Views.Pages
{
    /// <summary>
    /// SteamStorePage.xaml etkileşim mantığı
    /// </summary>
    public partial class SteamStorePage : Page, IDisposable
    {
        private const int PageSize = 20;

        // Steam stops serving results past this many items.
        private const int MaxItems = 10000;

        private static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(5);

        // Well-known Wallpaper Engine Workshop tags (type and genre).
        private static readonly string[] KnownTags =
        [
            "Video", "Scene", "Web", "Abstract", "Animal", "Anime", "Cartoon", "CGI", "Cyberpunk",
            "Fantasy", "Game", "Girls", "Guys", "Landscape", "Medieval", "Memes", "MMD", "Music", "Nature", "Pixel art",
            "Relaxing", "Retro", "Sci-Fi", "Sports", "Technology", "Television", "Vehicle"
        ];

        // Cached pages, so navigating back to this tab does not hit Steam again.
        private static readonly Dictionary<string, (DateTime Time, List<SPSV> Items, int Total)> Cache = new();

        private readonly HashSet<string> Seen = new();

        private bool Loading;

        private bool Filling;

        private bool Dirty;

        private int QueryType = SPS.QueryTrend;

        private string Search = string.Empty;

        private string RequiredTag = string.Empty;

        public SteamStorePage()
        {
            InitializeComponent();

            Filling = true;

            SortFilter.Items.Add(new ComboBoxItem { Content = Text("Trending"), Tag = SPS.QueryTrend });
            SortFilter.Items.Add(new ComboBoxItem { Content = Text("Recent"), Tag = SPS.QueryRecent });
            SortFilter.Items.Add(new ComboBoxItem { Content = Text("Updated"), Tag = SPS.QueryUpdated });
            SortFilter.Items.Add(new ComboBoxItem { Content = Text("MostSubscribed"), Tag = SPS.QuerySubscribed });
            SortFilter.SelectedIndex = 0;

            TagList.Items.Add(new ListBoxItem { Content = Text("AllTags"), Tag = string.Empty });

            foreach (string Tag in KnownTags)
            {
                TagList.Items.Add(new ListBoxItem { Content = Tag, Tag = Tag });
            }

            TagList.SelectedIndex = 0;

            ApiKey.ToolTip = Text("ApiKey");

            Filling = false;

            ApiKey.Password = SMMP.SteamApiKey;

            ThemePagination.SelectPageChanged += ThemePagination_SelectPageChanged;
        }

        private static string Text(string Key)
        {
            return SRER.GetValue("Portal", "SteamPage", Key);
        }

        private void UpdateLayout()
        {
            ThemeStore.ItemMargin = new Thickness(SMMP.AdaptiveMargin);
            ThemeStore.MaxItemsPerRow = Math.Max(1, SMMP.AdaptiveLayout);
        }

        private async void SteamStorePage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLayout();

            SMMP.LayoutChanged += LayoutChangedHandler;

            if (ThemeStore.Children.Count == 0)
            {
                await Load();
            }
        }

        private void SteamStorePage_Unloaded(object sender, RoutedEventArgs e)
        {
            SMMP.LayoutChanged -= LayoutChangedHandler;
        }

        private void LayoutChangedHandler(object sender, EventArgs e)
        {
            UpdateLayout();
        }

        private async Task Load()
        {
            if (Loading)
            {
                // Filters changed during a request: run once more when it finishes.
                Dirty = true;

                return;
            }

            string Key = SMMP.SteamApiKey;

            if (string.IsNullOrWhiteSpace(Key))
            {
                ShowStatus(Text("NeedKey"));

                return;
            }

            Loading = true;
            Dirty = false;
            Refresh.IsEnabled = false;

            int Page = ThemePagination.SelectPage;

            try
            {
                string CacheKey = $"{Key.GetHashCode()}|{QueryType}|{Page}|{Search}|{RequiredTag}";

                List<SPSV> Result;

                int Total;

                if (Cache.TryGetValue(CacheKey, out var Hit) && DateTime.UtcNow - Hit.Time < CacheTime)
                {
                    Result = Hit.Items;
                    Total = Hit.Total;
                }
                else
                {
                    (Result, Total) = await SPS.QueryFiles(Key, QueryType, Page, PageSize, Search, RequiredTag);

                    Cache[CacheKey] = (DateTime.UtcNow, Result, Total);
                }

                ThemeStore.Children.Clear();
                Seen.Clear();

                if (Result.Count == 0)
                {
                    ShowStatus(Text("NoItems"));
                }
                else
                {
                    HideStatus();
                }

                foreach (SPSV Item in Result)
                {
                    if (SPS.IsId(Item.PublishedFileId) && Seen.Add(Item.PublishedFileId))
                    {
                        ThemeStore.Children.Add(new SPSCPC(Item));
                    }
                }

                int Pages = Math.Max(1, (int)Math.Ceiling(Math.Min(Total, MaxItems) / (double)PageSize));

                ThemePagination.MaxPage = Pages;
                ThemePagination.Visibility = Pages > 1 ? Visibility.Visible : Visibility.Collapsed;

                PageScroll.ScrollToTop();
            }
            catch (Exception Exception)
            {
                ShowStatus(Exception.Message);
            }
            finally
            {
                Loading = false;
                Refresh.IsEnabled = true;
            }

            if (Dirty)
            {
                await Load();
            }
        }

        private void ShowStatus(string Message)
        {
            Status.Text = Message;
            Status.Visibility = Visibility.Visible;
        }

        private void HideStatus()
        {
            Status.Text = string.Empty;
            Status.Visibility = Visibility.Collapsed;
        }

        private async Task Restart()
        {
            // Back to page one without triggering a second load through the pagination event.
            Filling = true;

            ThemePagination.SelectPage = 1;

            Filling = false;

            await Load();
        }

        private async void ThemePagination_SelectPageChanged(object sender, EventArgs e)
        {
            if (Filling)
            {
                return;
            }

            await Load();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Cache.Clear();

            await Restart();
        }

        private async void SortFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Filling || !IsLoaded)
            {
                return;
            }

            QueryType = (SortFilter.SelectedItem as ComboBoxItem)?.Tag is int Value ? Value : SPS.QueryTrend;

            await Restart();
        }

        private async void TagList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Filling || !IsLoaded)
            {
                return;
            }

            RequiredTag = (TagList.SelectedItem as ListBoxItem)?.Tag as string ?? string.Empty;

            await Restart();
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Search = SearchBox.Text.Trim();

                await Restart();
            }
        }

        private async void ApiKey_LostFocus(object sender, RoutedEventArgs e)
        {
            string Value = ApiKey.Password.Trim();

            if (Value != SMMP.SteamApiKey)
            {
                SMMP.SetSteamApiKey(Value);

                Cache.Clear();

                await Restart();
            }
        }

        public void Dispose()
        {
            ThemeStore.Children.Clear();

            GC.Collect();
            GC.SuppressFinalize(this);
        }
    }
}

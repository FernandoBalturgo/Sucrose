using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SSDMI = Sucrose.Shared.Dependency.Manage.Internal;

namespace Sucrose.Portal.Services
{
    public class SteamTag
    {
        [JsonProperty("tag")]
        public string Tag { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }
    }

    public class SteamVoteData
    {
        [JsonProperty("votes_up")]
        public long VotesUp { get; set; }

        [JsonProperty("votes_down")]
        public long VotesDown { get; set; }

        [JsonProperty("score")]
        public double Score { get; set; }
    }

    public class SteamItem
    {
        [JsonProperty("publishedfileid")]
        public string PublishedFileId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("preview_url")]
        public string PreviewUrl { get; set; }

        [JsonProperty("file_description")]
        public string FileDescription { get; set; }

        [JsonProperty("tags")]
        public List<SteamTag> Tags { get; set; }

        [JsonProperty("views")]
        public long Views { get; set; }

        [JsonProperty("favorited")]
        public long Favorited { get; set; }

        [JsonProperty("subscriptions")]
        public long Subscriptions { get; set; }

        [JsonProperty("followers")]
        public long Followers { get; set; }

        [JsonProperty("vote_data")]
        public SteamVoteData VoteData { get; set; }

        [JsonProperty("maybe_inappropriate_sex")]
        public bool MaybeInappropriateSex { get; set; }

        [JsonProperty("creator")]
        public string Creator { get; set; }

        [JsonProperty("file_url")]
        public string FileUrl { get; set; }

        [JsonProperty("file_size")]
        public string FileSize { get; set; }

        [JsonProperty("filename")]
        public string FileName { get; set; }

        [JsonProperty("hcontent_file")]
        public string HContentFile { get; set; }
    }

    public class SteamResult
    {
        [JsonProperty("result")]
        public int Result { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("publishedfiledetails")]
        public List<SteamItem> PublishedFileDetails { get; set; }
    }

    public class SteamResponse
    {
        [JsonProperty("response")]
        public SteamResult Response { get; set; }
    }

    public class SteamUgcFile
    {
        [JsonProperty("file_url")]
        public string FileUrl { get; set; }

        [JsonProperty("file_size")]
        public long FileSize { get; set; }

        [JsonProperty("fileName")]
        public string FileName { get; set; }
    }

    public class SteamUgcResult
    {
        [JsonProperty("data")]
        public List<SteamUgcFile> Data { get; set; }
    }

    public class SteamUgcResponse
    {
        [JsonProperty("response")]
        public SteamUgcResult Response { get; set; }
    }

    public static class SteamService
    {
        private const string Api = "https://api.steampowered.com/";

        private const int AppId = 431960;

        // Values of Steam's EPublishedFileQueryType.
        public const int QueryTrend = 3;
        public const int QueryRecent = 1;
        public const int QueryUpdated = 21;
        public const int QuerySubscribed = 9;

        public const long MaxDownloadBytes = 512L * 1024 * 1024;

        public static bool IsId(string Value)
        {
            return !string.IsNullOrEmpty(Value) && Value.Length <= 20 && Value.All(char.IsAsciiDigit);
        }

        public static bool IsSteamUrl(string Value)
        {
            return Uri.TryCreate(Value, UriKind.Absolute, out Uri Result) && Result.Scheme == Uri.UriSchemeHttps &&
                (Result.Host.EndsWith(".steamusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                 Result.Host.EndsWith(".steamstatic.com", StringComparison.OrdinalIgnoreCase) ||
                 Result.Host.EndsWith(".akamaihd.net", StringComparison.OrdinalIgnoreCase) ||
                 Result.Host.EndsWith(".steampowered.com", StringComparison.OrdinalIgnoreCase) ||
                 Result.Host.EndsWith(".steamcommunity.com", StringComparison.OrdinalIgnoreCase));
        }

        // Steam documents these methods as GET with the key as a query parameter (HTTPS only).
        // Errors never include the URL, so the key does not leak into the UI.
        private static async Task<string> Get(string Method, string Key, Dictionary<string, string> Fields)
        {
            Fields["key"] = Key;

            string Query = string.Join("&", Fields.Select(Field => $"{Uri.EscapeDataString(Field.Key)}={Uri.EscapeDataString(Field.Value)}"));

            using HttpResponseMessage Response = await SSDMI.Client.GetAsync($"{Api}{Method}?{Query}");

            if (!Response.IsSuccessStatusCode)
            {
                int Code = (int)Response.StatusCode;

                throw new HttpRequestException(Code is 401 or 403 ? "Steam rejected the API key." : Code == 429 ? "Steam rate limit reached, try again later." : $"Steam returned HTTP {Code}.");
            }

            return await Response.Content.ReadAsStringAsync();
        }

        public static async Task<(List<SteamItem> Items, int Total)> QueryFiles(string Key, int QueryType, int Page, int NumPerPage, string Search, string RequiredTag)
        {
            Dictionary<string, object> Params = new()
            {
                ["creator_appid"] = AppId,
                ["appid"] = AppId,
                ["filetype"] = 0,
                ["return_tags"] = true,
                ["return_vote_data"] = true,
                ["return_data"] = true,
                ["return_previews"] = true,
                ["days"] = 7,
                ["query_type"] = string.IsNullOrWhiteSpace(Search) ? QueryType : 12,
                ["page"] = Page,
                ["numperpage"] = NumPerPage
            };

            if (!string.IsNullOrWhiteSpace(Search))
            {
                Params["search_text"] = Search;
            }

            if (!string.IsNullOrWhiteSpace(RequiredTag))
            {
                Params["requiredtags"] = new[] { RequiredTag };
            }

            string Json = await Get("IPublishedFileService/QueryFiles/v1/", Key, new() { ["input_json"] = JsonConvert.SerializeObject(Params) });

            SteamResponse Result = JsonConvert.DeserializeObject<SteamResponse>(Json);

            return (Result?.Response?.PublishedFileDetails ?? new(), Result?.Response?.Total ?? 0);
        }

        public static async Task<SteamUgcFile> GetUGCFileDetails(string Key, string UgcId)
        {
            if (!IsId(UgcId))
            {
                return null;
            }

            string Json = await Get("ISteamRemoteStorage/GetUGCFileDetails/v1/", Key, new() { ["ugcid"] = UgcId, ["appid"] = AppId.ToString() });

            SteamUgcResponse Result = JsonConvert.DeserializeObject<SteamUgcResponse>(Json);

            return Result?.Response?.Data?.FirstOrDefault();
        }

        public static async Task Download(string Url, string Path)
        {
            if (!IsSteamUrl(Url))
            {
                throw new InvalidOperationException("Refusing to download from a non-Steam host.");
            }

            using HttpResponseMessage Response = await SSDMI.Client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead);

            Response.EnsureSuccessStatusCode();

            if (Response.Content.Headers.ContentLength > MaxDownloadBytes)
            {
                throw new InvalidOperationException("File is too large.");
            }

            using Stream Stream = await Response.Content.ReadAsStreamAsync();

            using FileStream FStream = new(Path, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] Buffer = new byte[81920];

            long Total = 0;

            int Read;

            while ((Read = await Stream.ReadAsync(Buffer)) > 0)
            {
                Total += Read;

                if (Total > MaxDownloadBytes)
                {
                    throw new InvalidOperationException("File is too large.");
                }

                await FStream.WriteAsync(Buffer.AsMemory(0, Read));
            }
        }
    }
}

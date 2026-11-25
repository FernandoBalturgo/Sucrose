using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Windows.Media;
using SMMG = Sucrose.Manager.Manage.General;
using SMMO = Sucrose.Manager.Manage.Objectionable;
using SSDESHT = Sucrose.Shared.Dependency.Enum.StretchType;

namespace Sucrose.Shared.Dependency.Manage
{
    internal static class Internal
    {
        public static SSDESHT DefaultStretchType = SSDESHT.None;

        public static Stretch DefaultBackgroundStretch = Stretch.UniformToFill;

        public static readonly SocketsHttpHandler HttpHandler = new()
        {
            AllowAutoRedirect = true,
            MaxConnectionsPerServer = 100,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),

            SslOptions =
            {
                //Security hardening: default certificate validation (was: accept everything) and modern TLS only (was: SSL2/SSL3/TLS1.0 allowed).
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }
        };

        //Security hardening: outbound policy. Only HTTPS read requests (GET/HEAD) are allowed, i.e. downloading wallpapers/content.
        //Anything that sends data out (POST/PUT/...) or targets telemetry/report/upload endpoints is refused.
        private sealed class OutboundPolicyHandler(HttpMessageHandler Inner) : DelegatingHandler(Inner)
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage Request, CancellationToken Token)
            {
                Uri Target = Request.RequestUri;

                bool Read = Request.Method == HttpMethod.Get || Request.Method == HttpMethod.Head;
                bool Secure = Target != null && Target.IsAbsoluteUri && Target.Scheme == Uri.UriSchemeHttps;
                bool Tracking = Target != null && Target.IsAbsoluteUri && (Target.AbsolutePath.Contains("/telemetry/", StringComparison.OrdinalIgnoreCase) || Target.AbsolutePath.Contains("/report/", StringComparison.OrdinalIgnoreCase) || Target.AbsolutePath.Contains("/upload/", StringComparison.OrdinalIgnoreCase));

                if (!Read || !Secure || Tracking)
                {
                    throw new HttpRequestException($"Blocked by Sucrose security policy: {Request.Method} {Target?.Host}");
                }

                return base.SendAsync(Request, Token);
            }
        }

        private static readonly HttpMessageHandler PolicyHandler = new OutboundPolicyHandler(HttpHandler);

        public static readonly HttpClient Client = new(PolicyHandler, false)
        {
            Timeout = Timeout.InfiniteTimeSpan, //TimeSpan.FromSeconds(30)

            DefaultRequestHeaders =
            {
                { "User-Agent", SMMG.UserAgent }
            }
        };

        public static readonly HttpClient ClientGitHub = new(PolicyHandler, false)
        {
            Timeout = Timeout.InfiniteTimeSpan, //TimeSpan.FromSeconds(30)

            DefaultRequestHeaders =
            {
                { "User-Agent", SMMG.UserAgent },
                { "Authorization", $"Bearer {SMMO.PersonalAccessToken}" }
            }
        };
    }
}
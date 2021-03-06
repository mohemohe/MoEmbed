using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoEmbed.Models;
using MoEmbed.Providers;

namespace MoEmbed
{
    /// <summary>
    /// Handles the request object and use right metadata handler to fetch embed data.
    /// </summary>
    public class MetadataService : IDisposable
    {
        #region Retry Settings

        /// <summary>
        /// Gets or sets the default wait initial interval after a HTTP request failed.
        /// </summary>
        public static TimeSpan DefaultRequestRetryWait { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the default scaling factor that will multiply request wait interval.
        /// </summary>
        public static double DefaultRequestRetryFactor { get; set; } = 2;

        /// <summary>
        /// Gets or sets the default maximum count of retries of a HTTP request.
        /// </summary>
        public static int DefaultRequestRetryCount { get; set; } = 4;

        /// <summary>
        /// Gets or sets the default age of the cache that failed to fetch remote resource.
        /// </summary>
        public static TimeSpan DefaultErrorResponseCacheAge { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the default timeout for <see cref="HttpClient"/>.
        /// </summary>
        public static TimeSpan DefaultHttpRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets the wait initial interval after a HTTP request failed.
        /// </summary>
        public virtual TimeSpan RequestRetryWait => DefaultRequestRetryWait;

        /// <summary>
        /// Gets the scaling factor that will multiply request wait interval.
        /// </summary>
        public virtual double RequestRetryFactor => DefaultRequestRetryFactor;

        /// <summary>
        /// Gets the default maximum count of retries of a HTTP request.
        /// </summary>
        public virtual int RequestRetryCount => DefaultRequestRetryCount;

        /// <summary>
        /// Gets the age of the cache that failed to fetch remote resource.
        /// </summary>
        public virtual TimeSpan ErrorResponseCacheAge => DefaultErrorResponseCacheAge;

        /// <summary>
        /// Gets or sets the timeout for <see cref="HttpClient"/>.
        /// </summary>
        public virtual TimeSpan HttpRequestTimeout => DefaultHttpRequestTimeout;

        #endregion Retry Settings

        private readonly ILogger<MetadataService> _logger;
        private readonly IMetadataCache _Cache;

        private MetadataProviderCollection _Providers;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataService" /> class.
        /// </summary>
        /// <param name="loggerFactory">The logger factory,</param>
        /// <param name="serviceProvider">
        /// The <see cref="IServiceProvider" /> to instanciate <see cref="IMetadataProvider" /> s.
        /// </param>
        /// <param name="cache">The cache provider for the resolved metadata.</param>
        public MetadataService(ILoggerFactory loggerFactory = null, IServiceProvider serviceProvider = null, IMetadataCache cache = null)
        {
            _logger = loggerFactory?.CreateLogger<MetadataService>();
            _Cache = cache;

            if (serviceProvider != null)
            {
                foreach (var s in serviceProvider.GetServices<IMetadataProvider>())
                {
                    if (s.IsEnabled)
                    {
                        Providers.Add(s);
                    }
                    else
                    {
                        _logger.LogWarning("Ignore disabled IMetadataProvider: {0}", s);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the list of <see cref="IMetadataProvider" />.
        /// </summary>
        public MetadataProviderCollection Providers
            => _Providers ?? (_Providers = new MetadataProviderCollection());

        /// <summary>
        /// Finds the right provider and use it to fetch embed data.
        /// </summary>
        public async Task<EmbedDataResult> GetDataAsync(ConsumerRequest request)
        {
            var m = _Cache == null ? null : await _Cache.ReadAsync(this, request).ConfigureAwait(false);
            if (m == null)
            {
                foreach (var prov in Providers.GetByHost(request.Url.Host))
                {
                    m = prov.GetMetadata(request);
                    if (m != null)
                    {
                        _logger?.LogInformation("Selected Provider: {0}", prov);

                        _Cache?.WriteAsync(this, request, m).ConfigureAwait(false);
                        break;
                    }
                }
            }

            if (m != null)
            {
                var d = await m.FetchAsync(new RequestContext(this, request));

                if (d != null)
                {
                    return new EmbedDataResult()
                    {
                        Succeeded = true,
                        Data = d
                    };
                }
            }

            return new EmbedDataResult()
            {
                Succeeded = false,
                ErrorMessage = "Not Found"
            };
        }

        #region HttpClient

        private HttpClient _HttpClient;

        /// <summary>
        /// Gets a <see cref="HttpClient" /> instance that is shared over this <see
        /// cref="MetadataService" />.
        /// </summary>
        /// <remarks>The <see cref="HttpClient" /> won't follow redirect automatically.</remarks>
        public HttpClient HttpClient
        {
            get
            {
                if (_HttpClient == null)
                {
                    var c = new HttpClient(new HttpClientHandler()
                    {
                        AllowAutoRedirect = false,
                    });
                    c.Timeout = HttpRequestTimeout;

                    // Set User-Agent

                    // HACK: make User-Agent configurable
                    const string product = "MoEmbed";
                    var version = typeof(MetadataService).GetTypeInfo().Assembly.GetName().Version;
                    const string url = "https://github.com/supermomonga/MoEmbed";

                    c.DefaultRequestHeaders.UserAgent.Clear();
                    c.DefaultRequestHeaders.UserAgent.Add(
                        new ProductInfoHeaderValue("Mozilla", "5.0"));
                    c.DefaultRequestHeaders.UserAgent.Add(
                        new ProductInfoHeaderValue($"(compatible; {product}/{version}; +{url})"));

                    _HttpClient = c;
                }
                return _HttpClient;
            }
        }

        #endregion HttpClient

        #region IDisposable support

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting
        /// unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        ~MetadataService()
            => Dispose(false);

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _HttpClient?.Dispose();
            }
            _HttpClient = null;
        }

        #endregion IDisposable support
    }
}
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoEmbed.Models;
using MoEmbed.Providers;

namespace MoEmbed
{
    /// <summary>
    ///   Handles the request object and use right metadata handler to fetch embed data.
    /// </summary>
    public class MetadataService : IDisposable
    {
        private readonly ILogger<MetadataService> _logger;
        private readonly IMetadataCache _Cache;

        private List<IMetadataProvider> _Providers;

        /// <summary>
        ///   Initializes a new instance of the <see cref="MetadataService" /> class.
        /// </summary>
        public MetadataService(ILoggerFactory loggerFactory, IMetadataCache cache = null)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _logger = loggerFactory.CreateLogger<MetadataService>();
            _Cache = cache;
        }

        /// <summary>
        ///   Gets the list of <see cref="IMetadataProvider" />.
        /// </summary>
        public List<IMetadataProvider> Providers
            => _Providers ?? (_Providers = new List<IMetadataProvider>());

        /// <summary>
        ///   Finds the right provider and use it to fetch embed data.
        /// </summary>
        public async Task<EmbedDataResult> GetDataAsync(ConsumerRequest request)
        {
            var m = _Cache == null ? null : await _Cache.ReadAsync(this, request).ConfigureAwait(false);
            if (m == null)
            {
                foreach (var prov in Providers)
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
                var d = await m.FetchAsync();

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
        /// Gets a <see cref="HttpClient"/> instance that is shared over this <see cref="MetadataService"/>.
        /// </summary>
        /// <remarks>
        /// The <see cref="HttpClient"/> won't follow redirect automatically.
        /// </remarks>
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

                    // Set User-Agent

                    // HACK: make User-Agent configurable
                    const string product = "MoEmbed";
                    var version = new AssemblyName(typeof(MetadataService).AssemblyQualifiedName).Version;
                    const string url = "https://github.com/supermomonga/MoEmbed";

                    c.DefaultRequestHeaders.UserAgent.Clear();
                    c.DefaultRequestHeaders.UserAgent.Add(
                        new ProductInfoHeaderValue($"Mozilla/5.0 (compatible; {product}/{version}; +{url})"));

                    _HttpClient = c;
                }
                return _HttpClient;
            }
        }

        #endregion HttpClient

        #region IDisposable support

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
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
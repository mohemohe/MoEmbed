using System.Threading.Tasks;
using MoEmbed.Models;
using MoEmbed.Models.Metadata;

namespace MoEmbed
{
    /// <summary>
    /// Provides cache storage to <see cref="MetadataService" />.
    /// </summary>
    public interface IMetadataCache
    {
        /// <summary>
        /// Reads the cached <see cref="Metadata" /> for the URL of <paramref name="request"/>.
        /// </summary>
        /// <param name="service">The <see cref="MetadataService" /> reading cache.</param>
        /// <param name="request">The consumer request.</param>
        /// <returns>The task object representing the asynchronous operation.
        /// The <see cref="Task{TResult}.Result"/> property on the task object returns a cached <see cref="Metadata"/>.</returns>
        Task<Metadata> ReadAsync(MetadataService service, ConsumerRequest request);

        /// <summary>
        ///   Writes the <see cref="Metadata" /> to to the cache store.
        /// </summary>
        Task WriteAsync(MetadataService service, ConsumerRequest request, Metadata metadata);
    }
}

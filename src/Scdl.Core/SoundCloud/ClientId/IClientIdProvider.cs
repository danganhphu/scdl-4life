namespace Scdl.Core.SoundCloud.ClientId;

/// <summary>
/// Supplies the api-v2 <c>client_id</c>. SoundCloud never publishes it, so it
/// has to come out of the web player bundles.
/// </summary>
public interface IClientIdProvider
{
    ValueTask<string> GetAsync(CancellationToken cancellationToken);
}

namespace Scdl.Core;

/// <summary>
/// Stable anchor for <c>typeof(ICoreAssemblyMarker).Assembly</c>.
/// </summary>
/// <remarks>
/// Nothing in scdl scans assemblies at run time, so this is not a registration
/// hook the way it would be in a MediatR or MassTransit host. It exists so that
/// anything needing a handle on this assembly - the architecture tests, and any
/// future scanning - names a type that cannot be renamed by accident, rather
/// than a string literal or whichever class happened to be nearby.
/// </remarks>
public interface ICoreAssemblyMarker;

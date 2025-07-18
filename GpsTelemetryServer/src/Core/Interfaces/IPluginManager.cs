using Core.Models;

namespace Core.Interfaces;

public interface IPluginManager
{
    Task<IEnumerable<IProtocolPlugin>> LoadPluginsAsync();
    Task<IProtocolPlugin?> GetPluginForDataAsync(byte[] data);
    IProtocolPlugin? GetPlugin(ProtocolType protocolType);
    Task ShutdownAsync();
}
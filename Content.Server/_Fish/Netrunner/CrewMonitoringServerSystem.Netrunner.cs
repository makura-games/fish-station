#pragma warning disable IDE0130 // Fish-edit: Расширяем сервер мониторинга экипажа из папки Fish.
namespace Content.Server.Medical.CrewMonitoring;

public sealed partial class CrewMonitoringServerSystem
{
    /// <summary>Removes a sensor entry by its device network address so stale coordinates are no longer broadcast.</summary>
    public void RemoveSensorStatus(string address)
    {
        var servers = EntityQueryEnumerator<CrewMonitoringServerComponent>();
        while (servers.MoveNext(out var id, out var server))
        {
            if (server.SensorStatus.Remove(address) && _singletonServerSystem.IsActiveServer(id))
                BroadcastSensorStatus(id, server);
        }
    }
}

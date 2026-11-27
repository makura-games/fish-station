using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемой системе DeviceLink.
namespace Content.Shared.DeviceLinking;

public abstract partial class SharedDeviceLinkSystem
{
    /// <summary>
    /// Проверяет, передаёт ли источник высокий устойчивый сигнал в указанный вход приёмника.
    /// </summary>
    public bool IsSendingHighTo(
        Entity<DeviceLinkSourceComponent?> source,
        EntityUid sink,
        ProtoId<SinkPortPrototype> sinkPort)
    {
        if (!Resolve(source, ref source.Comp))
            return false;

        if (!source.Comp.LinkedPorts.TryGetValue(sink, out var links))
            return false;

        foreach (var (sourcePort, linkedSinkPort) in links)
        {
            if (linkedSinkPort != sinkPort)
                continue;

            if (source.Comp.LastSignals.TryGetValue(sourcePort, out var high) && high)
                return true;
        }

        return false;
    }
}

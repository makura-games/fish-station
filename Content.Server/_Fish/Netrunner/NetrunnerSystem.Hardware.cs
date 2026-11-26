using Content.Shared._Fish.Netrunner;

namespace Content.Server._Fish.Netrunner;

public sealed partial class NetrunnerSystem
{
    // Fish-edit: Ускоритель работает только в слоте глаз, нагрев оплачивается в начале активации.
    private NetrunnerVisorComponent? GetWornVisor(Entity<NetrunnerImplantComponent> ent)
    {
        if (ent.Comp.User is not { } user || !_temperatureQuery.HasComp(user)
            || !_inventory.TryGetSlotEntity(user, "eyes", out var glasses)
            || !TryComp<NetrunnerVisorComponent>(glasses, out var visor)
            || HasComp<NetrunnerDisruptionComponent>(glasses))
            return null;
        return visor;
    }

    /// <summary>Returns the effective cooldown, or zero for an administrative debug implant.</summary>
    public TimeSpan GetCooldown(Entity<NetrunnerImplantComponent> ent, NetrunnerAbilityPrototype ability)
        => ent.Comp.DebugMode ? TimeSpan.Zero
            : TimeSpan.FromSeconds(ability.Cooldown.TotalSeconds * GetCooldownMultiplier(ent));

    private TimeSpan GetGlobalCooldown(Entity<NetrunnerImplantComponent> ent)
        => ent.Comp.DebugMode ? TimeSpan.Zero
            : TimeSpan.FromSeconds(ent.Comp.GlobalCooldown.TotalSeconds * GetCooldownMultiplier(ent));

    private float GetCooldownMultiplier(Entity<NetrunnerImplantComponent> ent)
        => Math.Clamp(GetWornVisor(ent)?.CooldownMultiplier ?? 1, 0.5f, 1);

    private void SetProgramUseDelay(Entity<NetrunnerImplantComponent> ent,
        NetrunnerAbilityPrototype ability, TimeSpan cooldown)
    {
        foreach (var action in ent.Comp.GrantedActions)
        {
            if (MetaData(action).EntityPrototype?.ID == ability.Action.Id)
            {
                _actions.SetUseDelay(action, cooldown);
                break;
            }
        }
    }

    private void SetProgramCooldown(Entity<NetrunnerImplantComponent> ent,
        NetrunnerAbilityPrototype ability, TimeSpan start, TimeSpan end)
    {
        foreach (var action in ent.Comp.GrantedActions)
        {
            if (MetaData(action).EntityPrototype?.ID == ability.Action.Id)
            {
                _actions.SetCooldown(action, start, end);
                break;
            }
        }
    }

    private void ApplyActivationHeat(Entity<NetrunnerImplantComponent> ent, EntityUid user,
        NetrunnerAbilityPrototype ability)
    {
        if (ent.Comp.DebugMode || !_temperatureQuery.TryComp(user, out var body) || body.HeatCapacity <= 0)
            return;
        var heat = ability.RamCost * ent.Comp.HeatPerRam * (GetWornVisor(ent)?.HeatMultiplier ?? 1);
        _temperature.ChangeHeat((user, body), heat * body.HeatCapacity, ignoreHeatResistance: true);
    }

    private void PublishBodyTemperature(EntityUid user, NetrunnerUiState state)
    {
        if (_temperatureQuery.TryComp(user, out var body))
        {
            state.HasBodyTemperature = true;
            state.BodyTemperature = body.Temperature - 273.15f;
        }
    }
}

using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Skills;

/// <summary>
/// Клиент просит открыть EUI распределения навыков (кнопка топбара / хоткей).
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestOpenSkillsEuiEvent : EntityEventArgs;

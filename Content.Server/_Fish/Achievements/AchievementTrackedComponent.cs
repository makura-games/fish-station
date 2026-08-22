namespace Content.Server._Fish.Achievements;

/// <summary>
/// Маркер сущностей, по которым крутим gameplay-достижения.
/// Отдельный компонент нужен из‑за ограничения RT: одна directed-подписка на (comp, event).
/// </summary>
[RegisterComponent]
public sealed partial class AchievementTrackedComponent : Component;

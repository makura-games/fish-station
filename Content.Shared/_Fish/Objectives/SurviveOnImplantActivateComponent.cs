namespace Content.Shared._Fish.Objectives;

/// <summary>
/// Имплантат с этим компонентом при ручной активации засчитывает владельцу цели «выжить».
/// </summary>
/// <remarks>
/// Нужен для имплантатов, которые удаляют владельца (например, редспейс-имплант): после удаления тела разум
/// считается мёртвым, хотя персонаж не погиб, а покинул раунд. Срабатывание от смерти цель не засчитывает.
/// </remarks>
[RegisterComponent]
public sealed partial class SurviveOnImplantActivateComponent : Component;

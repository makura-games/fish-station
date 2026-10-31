namespace Content.Server._Fish.Objectives;

/// <summary>
/// Помечает разум, чей владелец покинул раунд активацией имплантата (см. <see cref="SurviveOnImplantActivateSystem"/>).
/// Для таких разумов цели «выжить» засчитываются, даже если тело удалено.
/// </summary>
[RegisterComponent]
public sealed partial class ImplantSurvivedMindComponent : Component;

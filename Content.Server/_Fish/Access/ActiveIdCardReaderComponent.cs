namespace Content.Server._Fish.Access;

/// <summary>
/// Хранит состояние отложенной обработки считывателя ID-карт.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveIdCardReaderComponent : Component
{
    /// <summary>
    /// Время следующей попытки закрыть подключённый затвор.
    /// </summary>
    public TimeSpan? NextCloseAttempt;

    /// <summary>
    /// Указывает, что состояние карты и подключённых источников нужно перечитать.
    /// </summary>
    public bool RefreshState;
}

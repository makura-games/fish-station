namespace Content.Shared._Fish.ClockworkCult;

/// <summary>
/// Константы механик Clockwork Cult (порт с tgstation late 2017/2018).
/// </summary>
public static class FishClockworkCultConstants
{
    public const float ScriptUnlockThreshold = 50000f;
    public const float ApplicationUnlockThreshold = 100000f;
    public const float MinClockcultPower = 25f;
    public const float ClockcultPowerUnit = MinClockcultPower * 100f;
    public const float PowerStandard = ClockcultPowerUnit * 0.2f;
    public const float PowerFloor = ClockcultPowerUnit * 0.1f;
    public const float PowerWallMinusFloor = ClockcultPowerUnit * 0.4f;
    public const float PowerWallTotal = PowerWallMinusFloor + PowerFloor;
    public const float PowerRod = ClockcultPowerUnit * 0.01f;
    public const float PowerMetal = ClockcultPowerUnit * 0.02f;
    public const float PowerPlasteel = ClockcultPowerUnit * 0.05f;

    public const float AbscondAbductionCost = 95f;
    public const float HeraldCostMultiplier = 0.75f;

    public const float GatewayReebeFound = 240f;
    public const float GatewayRatvarComing = 480f;
    public const float GatewayRatvarArrival = 600f;
    public const float ArkGracePeriodSeconds = 300f;
    public const float ArkScreamCooldownSeconds = 60f;

    public const int ArkBasePrepMinutes = 30;
    public const int ArkMaxPrepMinutes = 35;
    public const int ArkPlayersPerExtraMinute = 5;

    /// <summary>
    /// Длительность prep Ковчега: min(35, 30 + players/5).
    /// </summary>
    public static int CalculateArkPrepMinutes(int players) =>
        Math.Min(ArkMaxPrepMinutes, ArkBasePrepMinutes + Math.Max(0, players) / ArkPlayersPerExtraMinute);

    public const float IntegrationCogDrainWattsPerSecond = 5f;
    public const float StargazerPowerPerSecond = 5f;
    public const float VitalityReviveCost = 150f;
    public const float SubmissionConvertSeconds = 8f;
    public const float OcularWardenExclusionRange = 3f;
    public const float OcularWardenRange = 5f;
    public const float ManiaMotorRange = 7f;

    public const int ClockworkArmorCooldownSeconds = 180;
    public const int RatvarianSpearCooldownSeconds = 30;

    /// <summary>Создание 5 листов латуни из Replica Fabricator (tg 1.25 kW).</summary>
    public const float BrassSheetCreateCost = 1250f;
    public const int BrassSheetCreateCount = 5;

    /// <summary>Сервант на Transmission sigil — генерация глобальной мощности.</summary>
    public const float TransmissionSigilPowerPerSecond = 10f;
}


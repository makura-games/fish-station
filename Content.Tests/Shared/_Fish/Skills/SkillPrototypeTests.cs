using System.Collections.Generic;
using Content.Shared._Fish.Skills;
using NUnit.Framework;

namespace Content.Tests.Shared._Fish.Skills;

[TestFixture]
[TestOf(typeof(SkillMath))]
public sealed class SkillPrototypeTests
{
    [Test]
    public void CostLadderSupportsSpecialistVsUniversal()
    {
        // РњР°СЃС‚РµСЂ СЃ РЅРµРѕР±СѓС‡РµРЅРЅРѕРіРѕ: 2+3+5+8 = 18
        Assert.That(SkillMath.CostUnskilledToMaster, Is.EqualTo(18));
        Assert.That(SkillMath.GetLevelCost(2, SkillLevels.Unskilled, SkillLevels.Master), Is.EqualTo(18));

        // РЎРїРµС†РёР°Р»РёСЃС‚ РёРЅР¶РµРЅРµСЂР°: РјРёРЅ Construction 2, Р±СЋРґР¶РµС‚ 16 в†’ СЂРѕРІРЅРѕ РґРѕ РјР°СЃС‚РµСЂР°
        Assert.That(SkillMath.GetLevelCost(2, SkillLevels.Basic, SkillLevels.Master), Is.EqualTo(16));

        // РЈРЅРёРІРµСЂСЃР°Р» РїСЂРё 16: С‚СЂРё РЅР°РІС‹РєР° РґРѕ РѕР±СѓС‡РµРЅРЅРѕРіРѕ СЃ СЂР°Р·РЅС‹С… СЃС‚Р°СЂС‚РѕРІ
        // Construction 2в†’3 (3) + Electrical 2в†’3 (3) + Atmos 1в†’3 (2+3=5) + Computer 1в†’2 (2) = 13
        var universal = SkillMath.GetLevelCost(2, 2, 3)
                        + SkillMath.GetLevelCost(2, 2, 3)
                        + SkillMath.GetLevelCost(2, 1, 3)
                        + SkillMath.GetLevelCost(2, 1, 2);
        Assert.That(universal, Is.EqualTo(13));
        Assert.That(universal, Is.LessThanOrEqualTo(16));

        // Р“РёР±СЂРёРґ: Construction 2в†’4 (3+5=8) + Electrical 2в†’3 (3) + Atmos 1в†’2 (2) = 13
        var hybrid = SkillMath.GetLevelCost(2, 2, 4)
                     + SkillMath.GetLevelCost(2, 2, 3)
                     + SkillMath.GetLevelCost(2, 1, 2);
        Assert.That(hybrid, Is.EqualTo(13));

        // РќРµР»СЊР·СЏ РѕРґРЅРѕРІСЂРµРјРµРЅРЅРѕ РѕСЃРІРѕРёС‚СЊ РґРІР° РјР°СЃС‚РµСЂР° СЃ Р±СЋРґР¶РµС‚Р° 16
        var dualMaster = SkillMath.GetLevelCost(2, 2, 5) * 2;
        Assert.That(dualMaster, Is.EqualTo(32));
        Assert.That(dualMaster, Is.GreaterThan(16));
    }

    [Test]
    public void BookTrainingCapsAtExperienced()
    {
        Assert.That(SkillMath.BookTrainingMaxLevel, Is.EqualTo(SkillLevels.Experienced));
        Assert.That(SkillMath.CostUnskilledToExperienced, Is.EqualTo(10));
        Assert.That(SkillMath.GetCostForLevel(2, SkillLevels.Master), Is.EqualTo(8));
    }

    [Test]
    public void DurationAndQualityTablesWork()
    {
        var duration = new Dictionary<int, float>
        {
            [SkillLevels.Unskilled] = 1.5f,
            [SkillLevels.Trained] = 1.0f,
            [SkillLevels.Master] = 0.65f,
        };
        Assert.That(SkillMath.GetDurationMultiplier(duration, 0.3f, SkillLevels.Unskilled), Is.EqualTo(1.5f));
        Assert.That(SkillMath.GetDurationMultiplier(duration, 0.3f, SkillLevels.Master), Is.EqualTo(0.65f));

        var quality = new Dictionary<int, float>
        {
            [SkillLevels.Unskilled] = 0.75f,
            [SkillLevels.Master] = 1.35f,
        };
        Assert.That(SkillMath.GetQualityMultiplier(quality, 0.15f, SkillLevels.Unskilled), Is.EqualTo(0.75f));
        Assert.That(SkillMath.GetQualityMultiplier(quality, 0.15f, SkillLevels.Master), Is.EqualTo(1.35f));
    }

    [Test]
    public void DelayFactorFallbackWhenNoTable()
    {
        var empty = new Dictionary<int, float>();
        Assert.That(SkillMath.GetDurationMultiplier(empty, 0.3f, SkillLevels.Unskilled), Is.EqualTo(1.6f).Within(0.0001f));
        Assert.That(SkillMath.GetDurationMultiplier(empty, 0.3f, SkillLevels.Trained), Is.EqualTo(1.0f).Within(0.0001f));
    }

    [Test]
    public void TrainingKeyIsStable()
    {
        Assert.That(SkillMath.MakeTrainingKey("FishSkillMedical", 3), Is.EqualTo("FishSkillMedical:3"));
    }
}


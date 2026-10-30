using Content.Shared._Fish.Antag;
using NUnit.Framework;

namespace Content.Tests.Shared.Fish;

/// <summary>
/// Проверка констант департаментов защиты от целей антагов.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class AntagTargetingProtectionTests
{
    [Test]
    public void ProtectedDepartmentIds_AreExpected()
    {
        Assert.That(SharedAntagTargetingProtectionSystem.CommandDepartment.Id, Is.EqualTo("Command"));
        Assert.That(SharedAntagTargetingProtectionSystem.SecurityDepartment.Id, Is.EqualTo("Security"));
        Assert.That(SharedAntagTargetingProtectionSystem.CivilianDepartment.Id, Is.EqualTo("Civilian"));
        Assert.That(SharedAntagTargetingProtectionSystem.ServiceDepartment.Id, Is.EqualTo("Service"));
    }
}

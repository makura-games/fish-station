using System.Linq;
using Content.Shared.Roles;

namespace Content.Shared._Fish.Roles;

/// <summary>
/// Fish-хелперы для спонсорских проверок должностей.
/// </summary>
public static class FishJobRequirements
{
    /// <summary>
    /// Раскрывает sponsor-алиасы должности в её ID, чтобы старые вайтлисты продолжали работать
    /// после переименования или разделения ролей.
    /// </summary>
    public static string[] ApplySponsorAliases(JobPrototype job, string[] sponsorPrototypes)
    {
        if (job.SponsorAliases.Length == 0 || sponsorPrototypes.Contains(job.ID))
            return sponsorPrototypes;

        foreach (var alias in job.SponsorAliases)
        {
            if (sponsorPrototypes.Contains(alias))
                return sponsorPrototypes.Append(job.ID).ToArray();
        }

        return sponsorPrototypes;
    }
}

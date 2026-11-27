using Robust.Shared.Serialization;

namespace Content.Shared.Eye
{
    [Flags]
    [FlagsFor(typeof(VisibilityMaskLayer))]
    public enum VisibilityFlags : int
    {
        None = 0,
        Normal = 1 << 0,
        Ghost = 1 << 1, // Observers and revenants.
        Subfloor = 1 << 2, // Pipes, disposal chutes, cables etc. while hidden under tiles. Can be revealed with a t-ray.
        // Fish-start
        // Starlight-abductor. Раньше лежал на 1 << 2 вместе с Subfloor: всё, что прячут
        // абдукторы, уезжало любому игроку с тепловизором труб и наоборот.
        Abductor  = 1 << 4,
        // Fish-end
        Admin = 1 << 3, // Reserved for admins in stealth mode and admin tools.
        // Fish-start - слой служебных сущностей, скрытых от обычных клиентов
        /// <summary>
        /// Один бит, а не сочетание Admin | Ghost: PVS отдаёт сущность зрителю, только если
        /// маска зрителя содержит все биты её слоя. Администраторам и наблюдателям этот бит
        /// выдаёт FishVisibilitySystem.
        /// </summary>
        FishAdminOnly = 1 << 5,
        // Fish-end
    }
}

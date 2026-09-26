using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Explosion.Components;
using Content.Shared.Foldable;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Fish.Clothing;

/// <summary>
/// Shows protection values for the current visor state.
/// </summary>
public sealed partial class VisorExamineSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VisorComponent, GetVerbsEvent<ExamineVerb>>(OnExamineVerb);
    }

    private void OnExamineVerb(Entity<VisorComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !TryComp<ArmorComponent>(ent, out var armor))
            return;

        var message = BuildProtectionMessage(ent, armor);
        _examine.AddDetailedExamineVerb(
            args,
            ent.Comp,
            message,
            Loc.GetString("armor-examinable-verb-text"),
            "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("armor-examinable-verb-message"));
    }

    private FormattedMessage BuildProtectionMessage(Entity<VisorComponent> ent, ArmorComponent armor)
    {
        var message = new FormattedMessage();
        message.AddMarkupOrThrow(Loc.GetString("armor-examine"));
        var closed = IsClosed(ent);

        foreach (var (damageType, baseCoefficient) in armor.Modifiers.Coefficients)
        {
            var coefficient = baseCoefficient;
            if (closed && ent.Comp.ClosedDamageModifiers.Coefficients.TryGetValue(damageType, out var closedCoefficient))
                coefficient *= closedCoefficient;

            AddCoefficient(message, damageType, coefficient);
        }

        if (closed)
        {
            foreach (var (damageType, coefficient) in ent.Comp.ClosedDamageModifiers.Coefficients)
            {
                if (!ContainsDamageType(armor.Modifiers.Coefficients, damageType))
                    AddCoefficient(message, damageType, coefficient);
            }
        }

        foreach (var (damageType, baseReduction) in armor.Modifiers.FlatReduction)
        {
            var reduction = baseReduction;
            if (closed && ent.Comp.ClosedDamageModifiers.FlatReduction.TryGetValue(damageType, out var closedReduction))
                reduction += closedReduction;

            AddFlatReduction(message, damageType, reduction);
        }

        if (closed)
        {
            foreach (var (damageType, reduction) in ent.Comp.ClosedDamageModifiers.FlatReduction)
            {
                if (!ContainsDamageType(armor.Modifiers.FlatReduction, damageType))
                    AddFlatReduction(message, damageType, reduction);
            }
        }

        if (TryComp<ExplosionResistanceComponent>(ent, out var explosion))
        {
            var coefficient = explosion.DamageCoefficient;
            if (closed)
                coefficient *= ent.Comp.ClosedExplosionCoefficient;

            var value = MathF.Round((1f - coefficient) * 100f, 1);
            if (value != 0f)
            {
                message.PushNewline();
                message.AddMarkupOrThrow(Loc.GetString(explosion.Examine, ("value", value)));
            }
        }

        return message;
    }

    private void AddCoefficient(FormattedMessage message, string damageType, float coefficient)
    {
        message.PushNewline();
        var localizedType = Loc.GetString("armor-damage-type-" + damageType.ToLower());
        message.AddMarkupOrThrow(Loc.GetString(
            "armor-coefficient-value",
            ("type", localizedType),
            ("value", MathF.Round((1f - coefficient) * 100f, 1))));
    }

    private void AddFlatReduction(FormattedMessage message, string damageType, float reduction)
    {
        message.PushNewline();
        var localizedType = Loc.GetString("armor-damage-type-" + damageType.ToLower());
        message.AddMarkupOrThrow(Loc.GetString(
            "armor-reduction-value",
            ("type", localizedType),
            ("value", reduction)));
    }

    private bool IsClosed(EntityUid uid)
    {
        return TryComp<FoldableComponent>(uid, out var foldable) && !foldable.IsFolded;
    }

    private static bool ContainsDamageType(Dictionary<string, float> modifiers, string damageType)
    {
        foreach (var modifier in modifiers)
        {
            if (modifier.Key == damageType)
                return true;
        }

        return false;
    }
}

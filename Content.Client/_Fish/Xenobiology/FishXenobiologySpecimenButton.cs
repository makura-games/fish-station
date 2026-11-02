using System.Numerics;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Palette;
using Content.Shared._Fish.Xenobiology;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Fish.Xenobiology;

/// <summary>A stable colony list entry reused when measurements change.</summary>
internal sealed class FishXenobiologySpecimenButton : ContainerButton
{
    private readonly TextureRect _icon = new() { SetSize = new Vector2(40), Stretch = TextureRect.StretchMode.KeepAspectCentered };
    private readonly Label _name = new() { ClipText = true };
    private readonly Label _status = new() { ClipText = true, StyleClasses = { StyleClass.FontSmall } };
    private readonly ProgressBar _growth = new() { MinValue = 0, MaxValue = 1, MinHeight = 5 };

    public FishXenobiologySpecimenButton()
    {
        HorizontalExpand = true;
        ToggleMode = true;
        AddStyleClass(StyleClass.ButtonSquare);
        _growth.ForegroundStyleBoxOverride = FishXenobiologyPresentation.Panel(Palettes.Cyan.Element);
        var row = new BoxContainer { Margin = new Thickness(8, 6), SeparationOverride = 8 };
        var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 3 };
        row.AddChild(_icon);
        text.AddChild(_name);
        text.AddChild(_status);
        text.AddChild(_growth);
        row.AddChild(text);
        AddChild(row);
    }

    public void Update(FishXenobiologySpecimen specimen, FishXenobiologyBreedPrototype breed,
        ILocalizationManager loc, SpriteSystem sprite)
    {
        _name.Text = FishXenobiologyPresentation.Name(specimen, loc.GetString(breed.Name), loc);
        _name.ToolTip = _name.Text;
        _status.Text = loc.GetString("fish-xeno-list-status", ("status", FishXenobiologyPresentation.Status(specimen, loc)),
            ("generation", specimen.Generation));
        _status.FontColorOverride = FishXenobiologyPresentation.StatusColor(specimen);
        _growth.Visible = !specimen.Adult && !specimen.Dead;
        _growth.Value = FishXenobiologyPresentation.Unit(specimen.Growth);
        FishXenobiologyPresentation.SetIcon(_icon, sprite, breed, specimen.Adult, specimen.Dead);
    }
}

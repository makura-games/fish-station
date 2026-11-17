namespace Content.Client._Fish.Graphics.Vision;

/// <summary>Maps measured entity temperature to hue and thermal contrast.</summary>
internal static class FishThermalPalette
{
    internal static Color GetColor(float kelvin)
    {
        if (!float.IsFinite(kelvin) || kelvin < 0f)
            return Color.Transparent;

        var celsius = kelvin - 273.15f;
        Color color;
        if (celsius < 20f)
            color = Color.InterpolateBetween(new Color(0.025f, 0.15f, 1f), new Color(0.40f, 0.025f, 0.58f),
                Math.Clamp(celsius / 20f, 0f, 1f));
        else if (celsius < 28f)
            color = Color.InterpolateBetween(new Color(0.40f, 0.025f, 0.58f), new Color(1f, 0.24f, 0.025f),
                (celsius - 20f) / 8f);
        else if (celsius < 33f)
            color = Color.InterpolateBetween(new Color(1f, 0.24f, 0.025f), new Color(1f, 0.82f, 0.02f),
                (celsius - 28f) / 5f);
        else if (celsius < 37f)
            color = Color.InterpolateBetween(new Color(1f, 0.82f, 0.02f), new Color(1f, 0.98f, 0.94f),
                (celsius - 33f) / 4f);
        else
            color = Color.InterpolateBetween(new Color(1f, 0.98f, 0.94f), Color.White,
                Math.Clamp((celsius - 37f) / 23f, 0f, 1f));

        // Температура тела даёт плотный силуэт, чтобы фон не превращал его в красноватую дымку.
        color.A = Math.Clamp(MathF.Abs(celsius - 20f) / 12f, 0.08f, 1f);
        return color;
    }
}

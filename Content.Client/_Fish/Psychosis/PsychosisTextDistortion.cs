using Robust.Shared.Random;

namespace Content.Client._Fish.Psychosis;

internal static class PsychosisTextDistortion
{
    public static string DistortToNonsense(string text, IRobustRandom random)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsLetter(chars[i]))
            {
                chars[i] = random.Prob(0.5f)
                    ? (char) random.Next('а', 'я' + 1)
                    : (char) random.Next('a', 'z' + 1);
            }
            else if (char.IsDigit(chars[i]))
            {
                chars[i] = (char) random.Next('0', '9' + 1);
            }
        }

        return new string(chars);
    }
}

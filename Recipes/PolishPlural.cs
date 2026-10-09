namespace KitchenAssistant.Recipes;

// The Polish plural rule shared by the recipe labels.
internal static class PolishPlural
{
    // One for 1, few for a last digit of 2-4 (except 12-14), many for the rest (0 included).
    public static string Choose(int count, string one, string few, string many)
    {
        if (count == 1)
        {
            return one;
        }

        return count % 10 is >= 2 and <= 4 && count % 100 is not (>= 12 and <= 14) ? few : many;
    }
}

using Content.Shared.Lathe;

namespace Content.Server.Lathe;

public sealed partial class LatheSystem
{
    private static void RequeueInterruptedRecipe(LatheComponent lathe)
    {
        if (lathe.CurrentRecipe is not { } currentRecipe)
            return;

        if (lathe.Queue.First is { } node &&
            node.Value.Recipe == currentRecipe &&
            node.Value.ItemsPrinted > 0)
        {
            node.Value.ItemsPrinted--;
            return;
        }

        lathe.Queue.AddFirst(new LatheRecipeBatch(currentRecipe, itemsPrinted: 0, itemsRequested: 1));
    }
}

using System.Linq;
using Robust.Shared.Containers;

namespace Content.Shared._NC.Currency;

public static class CurrencyHelpers
{
    public static IEnumerable<EntityUid> EnumerateDeepItems(EntityUid root, IEntityManager ents)
    {
        if (!ents.TryGetComponent(root, out ContainerManagerComponent? cm)) yield break;

        var stack = new Stack<EntityUid>(cm.Containers.Values.SelectMany(c => c.ContainedEntities));

        while (stack.TryPop(out var ent))
        {
            yield return ent;

            if (ents.TryGetComponent(ent, out ContainerManagerComponent? inner))
                foreach (var sub in inner.Containers.Values.SelectMany(c => c.ContainedEntities))
                    stack.Push(sub);
        }
    }
}

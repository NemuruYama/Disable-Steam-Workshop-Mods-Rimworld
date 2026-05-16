using System;
using System.Linq;
using System.Reflection;
using DisableSteamMods.Runtime;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;

namespace DisableSteamMods.Patches;

public static class SteamWorkshopSuppressionPass
{
    private const string MainAssemblyName = "Assembly-CSharp";
    private const string WorkshopItemsTypeNameFallback = "Verse.Steam.WorkshopItems";
    private const string WorkshopItemTypeNameFallback = "Verse.Steam.WorkshopItem";

    [FreePatchAll]
    public static bool SuppressSteamWorkshopMods(ModuleDefinition module)
    {
        if (!module.Assembly.Name.Name.Equals(MainAssemblyName, StringComparison.Ordinal))
        {
            return false;
        }

        var workshopItemsType = module.GetType(typeof(Verse.Steam.WorkshopItems).FullName) ??
            module.GetType(WorkshopItemsTypeNameFallback);
        var workshopItemType = module.GetType(typeof(Verse.Steam.WorkshopItem).FullName) ??
            module.GetType(WorkshopItemTypeNameFallback);
        var allSubscribedItemsGetter = FindMethod(workshopItemsType, "get_AllSubscribedItems");
        if (workshopItemsType == null || workshopItemType == null || allSubscribedItemsGetter?.Body == null)
        {
            return false;
        }

        var filterMethod = typeof(WorkshopItemFilter).GetMethod(nameof(WorkshopItemFilter.Allows), BindingFlags.Public | BindingFlags.Static);
        if (filterMethod == null)
        {
            RewriteGetterToEmptyEnumerable(module, allSubscribedItemsGetter, workshopItemType);
            return true;
        }

        RewriteGetterToFilter(module, allSubscribedItemsGetter, workshopItemType, module.ImportReference(filterMethod));
        return true;
    }

    private static MethodDefinition? FindMethod(TypeDefinition? type, string name)
    {
        return type?.Methods.FirstOrDefault(method => method.Name == name);
    }

    private static void RewriteGetterToEmptyEnumerable(
        ModuleDefinition module,
        MethodDefinition method,
        TypeReference itemType)
    {
        var emptyMethod = typeof(Enumerable)
            .GetMethods()
            .First(candidate => candidate.Name == nameof(Enumerable.Empty) && candidate.GetParameters().Length == 0);

        var importedEmptyMethod = module.ImportReference(emptyMethod);
        var genericEmptyMethod = new GenericInstanceMethod(importedEmptyMethod);
        genericEmptyMethod.GenericArguments.Add(itemType);

        ResetBody(method, initLocals: false);
        var processor = method.Body.GetILProcessor();
        processor.Append(processor.Create(OpCodes.Call, module.ImportReference(genericEmptyMethod)));
        processor.Append(processor.Create(OpCodes.Ret));
    }

    private static void RewriteGetterToFilter(
        ModuleDefinition module,
        MethodDefinition method,
        TypeReference itemType,
        MethodReference predicate)
    {
        var whereMethod = typeof(Enumerable)
            .GetMethods()
            .First(candidate =>
                candidate.Name == nameof(Enumerable.Where) &&
                candidate.GetParameters().Length == 2 &&
                candidate.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>));

        var genericWhereMethod = new GenericInstanceMethod(module.ImportReference(whereMethod));
        genericWhereMethod.GenericArguments.Add(itemType);

        var predicateType = new GenericInstanceType(module.ImportReference(typeof(Func<,>)));
        predicateType.GenericArguments.Add(itemType);
        predicateType.GenericArguments.Add(module.TypeSystem.Boolean);

        var predicateCtor = module.ImportReference(typeof(Func<,>).GetConstructors().Single());
        predicateCtor.DeclaringType = predicateType;

        var subbedItems = method.DeclaringType.Fields.First(field => field.Name == "subbedItems");

        ResetBody(method, initLocals: false);
        var processor = method.Body.GetILProcessor();
        processor.Append(processor.Create(OpCodes.Ldsfld, module.ImportReference(subbedItems)));
        processor.Append(processor.Create(OpCodes.Ldnull));
        processor.Append(processor.Create(OpCodes.Ldftn, module.ImportReference(predicate)));
        processor.Append(processor.Create(OpCodes.Newobj, predicateCtor));
        processor.Append(processor.Create(OpCodes.Call, module.ImportReference(genericWhereMethod)));
        processor.Append(processor.Create(OpCodes.Ret));
    }

    private static void ResetBody(MethodDefinition method, bool initLocals)
    {
        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.InitLocals = initLocals;
        method.Body.Instructions.Clear();
    }
}

using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;

namespace DisableSteamMods.Patches;

public static class SteamWorkshopSuppressionPass
{
    private const ulong SelfWorkshopPublishedFileId = 3727126624UL;
    private const string MainAssemblyName = "Assembly-CSharp";
    private const string WorkshopItemsTypeNameFallback = "Verse.Steam.WorkshopItems";
    private const string WorkshopItemTypeNameFallback = "Verse.Steam.WorkshopItem";
    private const string PublishedFileIdTypeNameFallback = "Steamworks.PublishedFileId_t";
    private const string SelfWorkshopPredicateName = "DisableSteamMods_IsSelfWorkshopItem";

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

        var publishedFileIdType = ResolvePublishedFileIdType(module);
        if (publishedFileIdType == null)
        {
            RewriteGetterToEmptyEnumerable(module, allSubscribedItemsGetter, workshopItemType);
            return true;
        }

        var predicate = GetOrCreateSelfWorkshopPredicate(module, workshopItemsType, workshopItemType, publishedFileIdType);
        RewriteGetterToSelfFilter(module, allSubscribedItemsGetter, workshopItemType, predicate);
        return true;
    }

    private static MethodDefinition? FindMethod(TypeDefinition? type, string name)
    {
        return type?.Methods.FirstOrDefault(method => method.Name == name);
    }

    private static TypeDefinition? ResolvePublishedFileIdType(ModuleDefinition module)
    {
        var publishedFileIdTypeName = typeof(Steamworks.PublishedFileId_t).FullName;
        return module.GetType(publishedFileIdTypeName) ??
            module.GetType(PublishedFileIdTypeNameFallback) ??
            module.GetTypeReferences()
                .FirstOrDefault(type =>
                    type.FullName == publishedFileIdTypeName ||
                    type.FullName == PublishedFileIdTypeNameFallback)
                ?.Resolve();
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

    private static MethodDefinition GetOrCreateSelfWorkshopPredicate(
        ModuleDefinition module,
        TypeDefinition workshopItemsType,
        TypeDefinition workshopItemType,
        TypeDefinition publishedFileIdType)
    {
        var existing = workshopItemsType.Methods.FirstOrDefault(method => method.Name == SelfWorkshopPredicateName);
        if (existing != null)
        {
            return existing;
        }

        var getPublishedFileId = workshopItemType.Methods.First(method =>
            method.Name == "get_PublishedFileId" &&
            method.Parameters.Count == 0 &&
            method.ReturnType.FullName == typeof(Steamworks.PublishedFileId_t).FullName);
        var publishedFileIdValue = publishedFileIdType.Fields.First(field => field.Name == "m_PublishedFileId");

        var predicate = new MethodDefinition(
            SelfWorkshopPredicateName,
            MethodAttributes.Private | MethodAttributes.Static,
            module.TypeSystem.Boolean);

        predicate.Parameters.Add(new ParameterDefinition("item", ParameterAttributes.None, workshopItemType));
        ResetBody(predicate, initLocals: true);
        predicate.Body.Variables.Add(new VariableDefinition(module.ImportReference(publishedFileIdType)));
        workshopItemsType.Methods.Add(predicate);

        var processor = predicate.Body.GetILProcessor();
        var returnFalse = processor.Create(OpCodes.Ldc_I4_0);
        var returnInstruction = processor.Create(OpCodes.Ret);

        processor.Append(processor.Create(OpCodes.Ldarg_0));
        processor.Append(processor.Create(OpCodes.Brfalse_S, returnFalse));
        processor.Append(processor.Create(OpCodes.Ldarg_0));
        processor.Append(processor.Create(OpCodes.Callvirt, module.ImportReference(getPublishedFileId)));
        processor.Append(processor.Create(OpCodes.Stloc_0));
        processor.Append(processor.Create(OpCodes.Ldloca_S, predicate.Body.Variables[0]));
        processor.Append(processor.Create(OpCodes.Ldfld, module.ImportReference(publishedFileIdValue)));
        processor.Append(processor.Create(OpCodes.Ldc_I8, unchecked((long)SelfWorkshopPublishedFileId)));
        processor.Append(processor.Create(OpCodes.Ceq));
        processor.Append(processor.Create(OpCodes.Ret));
        processor.Append(returnFalse);
        processor.Append(returnInstruction);

        return predicate;
    }

    private static void RewriteGetterToSelfFilter(
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

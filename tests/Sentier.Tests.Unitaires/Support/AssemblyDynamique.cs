using System.Reflection;
using System.Reflection.Emit;
using Sentier.Commandes;

namespace Sentier.Tests.Unitaires.Support;

/// <summary>Génère un assembly en mémoire contenant une commande, pour tester les conflits de noms au balayage.</summary>
public static class AssemblyDynamique
{
    public static Assembly AvecCommande(string nomCommande)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Doublon" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Doublon");
        var type = module.DefineType("Doublon.Commande", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, typeof(object), [typeof(ICommandeSentier)]);
        type.SetCustomAttribute(new CustomAttributeBuilder(typeof(CommandeSentierAttribute).GetConstructor([typeof(string)])!, [nomCommande]));
        type.DefineDefaultConstructor(MethodAttributes.Public);

        var interfaceMethode = typeof(ICommandeSentier).GetMethod(nameof(ICommandeSentier.ExecuterAsync))!;
        var methode = type.DefineMethod(
            interfaceMethode.Name,
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            interfaceMethode.ReturnType,
            interfaceMethode.GetParameters().Select(p => p.ParameterType).ToArray());
        var il = methode.GetILGenerator();
        il.Emit(OpCodes.Call, typeof(Task).GetProperty(nameof(Task.CompletedTask))!.GetMethod!);
        il.Emit(OpCodes.Ret);
        type.DefineMethodOverride(methode, interfaceMethode);

        type.CreateType();
        return assembly;
    }
}

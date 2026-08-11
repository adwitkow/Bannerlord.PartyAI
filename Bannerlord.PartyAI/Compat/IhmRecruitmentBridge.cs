using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.Compat;

/// <summary>
/// Read-only integration with Improved Hire Mercenaries (IHM).
///
/// This class does not hire troops and does not alter IHM state.
/// It only asks IHM which troops are advertised for a settlement,
/// then checks whether those troops can satisfy the PAC party template.
/// </summary>
internal static class IhmRecruitmentBridge
{
    private const string IhmAssemblyName = "IHM";
    private const string TroopLookupMethodName = "GetTroopsForSettlement";

    private static bool _initialized;
    private static MethodInfo? _getTroopsForSettlement;
    private static object? _methodTarget;

    public sealed record TemplateMatch(
        CharacterObject AdvertisedTroop,
        CharacterObject DesiredTemplateTroop);

    internal static bool IsAvailable =>
        EnsureInitialized();

    internal static TemplateMatch? FindBestTemplateMatch(
        Settlement settlement,
        PartyAiEntitySettings settings)
    {
        if (settlement is null || settings?.PartyTemplate is null)
        {
            return null;
        }

        var advertisedTroops = GetTroopsForSettlement(settlement);
        if (advertisedTroops.Count == 0)
        {
            return null;
        }

        TemplateMatch? best = null;

        foreach (CharacterObject advertised in advertisedTroops)
        {
            if (advertised is null || advertised.IsHero)
            {
                continue;
            }

            // PAC already knows how to traverse:
            // Centurion -> Praetorian, Bow Maiden -> Valkyrie, etc.
            //
            // maxTierOnly:true gives us the terminal template-valid destination(s)
            // reachable from the IHM-advertised troop.
            var desiredTargets = Recruitment.UpgradeTargets(
                advertised,
                maxTierOnly: true,
                template: settings.PartyTemplate);

            foreach (CharacterObject desired in desiredTargets)
            {
                if (desired is null)
                {
                    continue;
                }

                // The troop must actually resolve into the selected PAC template.
                if (!settings.PartyTemplate.Troops.Contains(desired))
                {
                    continue;
                }

                var candidate = new TemplateMatch(advertised, desired);

                // Prefer the highest-tier desired result. This makes a T6
                // destination such as Praetorian/Valkyrie/Immortal win when
                // multiple template-valid paths are possible.
                if (best is null
                    || candidate.DesiredTemplateTroop.Tier > best.DesiredTemplateTroop.Tier
                    || (candidate.DesiredTemplateTroop.Tier == best.DesiredTemplateTroop.Tier
                        && candidate.AdvertisedTroop.Tier > best.AdvertisedTroop.Tier))
                {
                    best = candidate;
                }
            }
        }

        return best;
    }

    internal static bool HasTemplateMatch(
        Settlement settlement,
        PartyAiEntitySettings settings)
    {
        return FindBestTemplateMatch(settlement, settings) is not null;
    }

    internal static IReadOnlyList<CharacterObject> GetTroopsForSettlement(
        Settlement settlement)
    {
        if (!EnsureInitialized() || _getTroopsForSettlement is null)
        {
            return Array.Empty<CharacterObject>();
        }

        try
        {
            object?[] args = BuildArguments(_getTroopsForSettlement, settlement);
            object? raw = _getTroopsForSettlement.Invoke(_methodTarget, args);
            return ExtractCharacterObjects(raw);
        }
        catch (Exception ex)
        {
            Debug.Print(
                $"[PAC-IHM] GetTroopsForSettlement failed for " +
                $"{settlement?.Name}: {ex.GetBaseException().Message}");

            return Array.Empty<CharacterObject>();
        }
    }

    private static bool EnsureInitialized()
    {
        if (_initialized)
        {
            return _getTroopsForSettlement is not null;
        }

        _initialized = true;

        try
        {
            Assembly? ihmAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a =>
                    string.Equals(
                        a.GetName().Name,
                        IhmAssemblyName,
                        StringComparison.OrdinalIgnoreCase));

            if (ihmAssembly is null)
            {
                Debug.Print("[PAC-IHM] IHM.dll is not loaded; IHM recruitment advertisement is disabled.");
                return false;
            }

            var candidates = ihmAssembly
                .GetTypes()
                .SelectMany(t => t.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Static |
                    BindingFlags.Instance))
                .Where(m => m.Name == TroopLookupMethodName)
                .Where(m => m.GetParameters().Any(
                    p => typeof(Settlement).IsAssignableFrom(p.ParameterType)))
                .OrderByDescending(m => m.IsStatic)
                .ThenBy(m => m.GetParameters().Length)
                .ToList();

            if (candidates.Count == 0)
            {
                Debug.Print(
                    "[PAC-IHM] Could not find IHM.GetTroopsForSettlement(...) " +
                    "with a Settlement parameter.");
                return false;
            }

            foreach (MethodInfo candidate in candidates)
            {
                object? target = ResolveTarget(candidate);

                if (!candidate.IsStatic && target is null)
                {
                    continue;
                }

                _getTroopsForSettlement = candidate;
                _methodTarget = target;

                Debug.Print(
                    $"[PAC-IHM] Bound IHM advertisement provider: " +
                    $"{candidate.DeclaringType?.FullName}.{candidate.Name}(" +
                    $"{string.Join(", ", candidate.GetParameters().Select(p => p.ParameterType.Name))})");

                return true;
            }

            Debug.Print(
                "[PAC-IHM] Found GetTroopsForSettlement, but could not obtain " +
                "an instance for any matching method.");

            return false;
        }
        catch (ReflectionTypeLoadException ex)
        {
            string errors = string.Join(
                " | ",
                ex.LoaderExceptions
                    .Where(e => e is not null)
                    .Select(e => e!.Message));

            Debug.Print($"[PAC-IHM] IHM reflection load failure: {errors}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.Print(
                $"[PAC-IHM] IHM bridge initialization failed: " +
                $"{ex.GetBaseException().Message}");

            return false;
        }
    }

    private static object? ResolveTarget(MethodInfo method)
    {
        if (method.IsStatic)
        {
            return null;
        }

        Type? declaringType = method.DeclaringType;
        if (declaringType is null)
        {
            return null;
        }

        // Common mod singleton pattern: public/static Instance property.
        PropertyInfo? instanceProperty = declaringType.GetProperty(
            "Instance",
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static);

        if (instanceProperty?.GetValue(null) is object propertyInstance)
        {
            return propertyInstance;
        }

        FieldInfo? instanceField = declaringType.GetField(
            "Instance",
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static);

        if (instanceField?.GetValue(null) is object fieldInstance)
        {
            return fieldInstance;
        }

        // Last-resort support for utility/service classes with a parameterless ctor.
        ConstructorInfo? ctor = declaringType.GetConstructor(
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);

        return ctor?.Invoke(null);
    }

    private static object?[] BuildArguments(
        MethodInfo method,
        Settlement settlement)
    {
        ParameterInfo[] parameters = method.GetParameters();
        object?[] args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo p = parameters[i];
            Type t = p.ParameterType;

            if (typeof(Settlement).IsAssignableFrom(t))
            {
                args[i] = settlement;
            }
            else if (typeof(CultureObject).IsAssignableFrom(t))
            {
                args[i] = settlement.Culture;
            }
            else if (typeof(Hero).IsAssignableFrom(t))
            {
                // Read-only visibility query. MainHero is the safest identity
                // when IHM asks who is querying settlement availability.
                args[i] = Hero.MainHero;
            }
            else if (p.HasDefaultValue)
            {
                args[i] = p.DefaultValue;
            }
            else if (t == typeof(bool))
            {
                args[i] = false;
            }
            else if (t.IsEnum || t.IsValueType)
            {
                args[i] = Activator.CreateInstance(t);
            }
            else
            {
                args[i] = null;
            }
        }

        return args;
    }

    private static IReadOnlyList<CharacterObject> ExtractCharacterObjects(
        object? raw)
    {
        if (raw is null)
        {
            return Array.Empty<CharacterObject>();
        }

        if (raw is CharacterObject single)
        {
            return new[] { single };
        }

        if (raw is not IEnumerable enumerable)
        {
            return Array.Empty<CharacterObject>();
        }

        List<CharacterObject> result = new();

        foreach (object? item in enumerable)
        {
            if (item is CharacterObject troop)
            {
                result.Add(troop);
                continue;
            }

            if (item is null)
            {
                continue;
            }

            // Tolerate IHM returning wrappers, tuples, or view models.
            Type itemType = item.GetType();

            foreach (PropertyInfo property in itemType.GetProperties(
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.Instance))
            {
                if (!typeof(CharacterObject).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                if (property.GetValue(item) is CharacterObject wrapped)
                {
                    result.Add(wrapped);
                    break;
                }
            }

            foreach (FieldInfo field in itemType.GetFields(
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.Instance))
            {
                if (!typeof(CharacterObject).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                if (field.GetValue(item) is CharacterObject wrapped)
                {
                    result.Add(wrapped);
                    break;
                }
            }
        }

        return result.Distinct().ToList();
    }
}

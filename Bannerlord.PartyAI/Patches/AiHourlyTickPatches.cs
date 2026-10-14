using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PartyAI.Patches;

// Vibe coded debugging helper, not to be used in prod
public class AiHourlyTickPatches
{
    /// <summary>
    /// Call this once during mod initialization (e.g., SubModule.OnGameStart or similar).
    /// </summary>
    public static void PatchAll(Harmony harmony)
    {
        // Find every loaded type that inherits CampaignBehaviorBase
        var behaviorTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Type.EmptyTypes; }
            })
            .Where(t => !t.IsAbstract
                     && typeof(CampaignBehaviorBase).IsAssignableFrom(t)
                     && t != typeof(CampaignBehaviorBase))
            .ToList();

        var thisAssembly = typeof(AiHourlyTickPatches).Assembly;

        foreach (var type in behaviorTypes)
        {
            if (type.Assembly == thisAssembly)
            {
                continue;
            }

            TryPatchType(harmony, type);
        }
    }

    private static void TryPatchType(Harmony harmony, Type behaviorType)
    {
        // We need an instance to call RegisterEvents and intercept the AddNonSerializedListener call.
        // Instead of instantiating (which may have side effects), we SCAN the IL of RegisterEvents
        // to find which method is passed as the delegate target.

        var registerEvents = behaviorType.GetMethod(
            "RegisterEvents",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (registerEvents == null) return;

        var targetMethod = FindAiHourlyTickTarget(registerEvents, behaviorType);
        if (targetMethod == null) return;

        // Don't double-patch
        if (harmony.GetPatchedMethods().Contains(targetMethod)) return;

        var prefix = typeof(AiHourlyTickPatches)
            .GetMethod(nameof(GenericPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        var postfix = typeof(AiHourlyTickPatches)
            .GetMethod(nameof(GenericPostfix), BindingFlags.Static | BindingFlags.NonPublic);

        harmony.Patch(targetMethod,
            prefix: new HarmonyMethod(prefix),
            postfix: new HarmonyMethod(postfix));

        Console.WriteLine($"[AiHourlyTickPatcher] Patched {behaviorType.FullName}::{targetMethod.Name}");
    }

    /// <summary>
    /// Disassembles the IL of RegisterEvents looking for:
    ///   ldftn <someMethod>          ← the delegate target
    ///   newobj Action`2::.ctor
    ///   call  ...AddNonSerializedListener
    ///
    /// We look for a ldftn whose method matches Action<MobileParty, PartyThinkParams>,
    /// and whose following newobj constructs that exact Action type.
    /// </summary>
    private static MethodInfo FindAiHourlyTickTarget(MethodInfo registerEvents, Type declaringType)
    {
        var instructions = PatchProcessor.GetOriginalInstructions(registerEvents).ToList();

        var actionType = typeof(Action<MobileParty, PartyThinkParams>);
        var actionCtor = actionType.GetConstructors().FirstOrDefault();

        for (int i = 0; i < instructions.Count - 1; i++)
        {
            var instr = instructions[i];

            // ldftn or ldvirtftn
            if (instr.opcode != OpCodes.Ldftn && instr.opcode != OpCodes.Ldvirtftn)
                continue;

            if (instr.operand is not MethodInfo candidate)
                continue;

            // Verify signature: (MobileParty, PartyThinkParams) → void
            if (!MatchesAiHourlySignature(candidate))
                continue;

            // Verify the next instruction constructs Action<MobileParty, PartyThinkParams>
            var next = instructions[i + 1];
            if (next.opcode != OpCodes.Newobj)
                continue;

            if (next.operand is not ConstructorInfo ctor)
                continue;

            if (ctor.DeclaringType != actionType)
                continue;

            // Make sure the method belongs to this type (or a base that this type inherits)
            if (!declaringType.IsAssignableFrom(candidate.DeclaringType) &&
                !candidate.DeclaringType!.IsAssignableFrom(declaringType))
                continue;

            return candidate;
        }

        return null;
    }

    private static bool MatchesAiHourlySignature(MethodInfo m)
    {
        var p = m.GetParameters();
        return m.ReturnType == typeof(void)
            && p.Length == 2
            && p[0].ParameterType == typeof(MobileParty)
            && p[1].ParameterType == typeof(PartyThinkParams);
    }

    // ── Patches ──────────────────────────────────────────────────────────────

    private static (AIBehaviorData, float)[] Before = [];

    // Harmony injects __instance and the original parameters automatically.
    private static void GenericPrefix(object __instance, MobileParty __0, PartyThinkParams __1)
    {
        if (!SubModule.PartySettingsManager.IsHeroManageable(__0?.LeaderHero))
        {
            return;
        }

        Before = __1.AIBehaviorScores.ToArray();
    }

    private static void GenericPostfix(object __instance, MobileParty __0, PartyThinkParams __1)
    {
        if (!SubModule.PartySettingsManager.IsHeroManageable(__0?.LeaderHero))
        {
            return;
        }

        var rawAfter = __1.AIBehaviorScores
            .OrderByDescending(x => x.Item2)
            .ToArray();

        if (!Before.SequenceEqual(rawAfter))
        {
            var before = Before.Select(x => new AIBehaviorDisplay(x)).ToArray();
            var after = rawAfter
                .Select(x => new AIBehaviorDisplay(x))
                .ToArray();
            // Breakpoint here :)
        }
    }

    private class AIBehaviorDisplay
    {
        private AIBehaviorData _data;
        private float _weight;

        public AIBehaviorDisplay((AIBehaviorData Data, float Weight) tuple)
        {
            _data = tuple.Data;
            _weight = tuple.Weight;
        }

        public override string ToString()
        {
            return $"{_data.AiBehavior} {_data.Party.Name} - {_weight}";
        }
    }
}
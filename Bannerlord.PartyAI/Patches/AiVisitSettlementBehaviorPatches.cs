using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using HarmonyLib;
using HarmonyLib.PatchBuilder;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Patches;

internal class AiVisitSettlementBehaviorPatches
{
    private static readonly MethodInfo IsSuitableMethod = AccessTools.Method(
        typeof(AiVisitSettlementBehavior), "IsSettlementSuitableForVisitingCondition");
    private static readonly MethodInfo GetNavigationMethod = AccessTools.Method(
        typeof(AiVisitSettlementBehavior), "GetBestNavigationDataForVisitingSettlement");

    public static void Apply(Harmony harmony)
    {
        harmony.Patch<AiVisitSettlementBehavior>()
            .Method("GetApproximateVolunteersCanBeRecruitedDataFromSettlement")
                .Postfix(GetApproximateVolunteersCanBeRecruitedDataFromSettlementPostfix)
            .Method("FillSettlementsToVisitWithDistancesAsDays")
                .Postfix(FillSettlementsToVisitWithDistancesAsDaysPostfix);
    }

    private static void GetApproximateVolunteersCanBeRecruitedDataFromSettlementPostfix(
        ref (int, float) __result,
        Hero hero,
        Settlement settlement)
    {
        if (!SubModule.PartySettingsManager.IsHeroManageable(hero) ||
            hero.PartyBelongedTo == null ||
            hero.PartyBelongedTo.LeaderHero != hero)
        {
            return;
        }

        MobileParty mobileParty = hero.PartyBelongedTo;
        PartyAiEntitySettings heroSettings = SubModule.PartySettingsManager.Settings(hero);

        if (!heroSettings.AllowRecruitment
            || heroSettings.Order?.Behavior is PartyAiOrderType.RecruitFromTemplate)
        {
            __result = (0, 0f);
            return;
        }

        // if we're going to convert the troop anyway, it doesn't matter
        if (SubModule.PartySettingsManager.AllowTroopConversion && heroSettings.PartyTemplate != null)
        {
            return;
        }

        PartyComposition comp = Recruitment.GetPartyComposition(mobileParty.Party, heroSettings);

        int allowedCount = 0;
        int totalWage = 0;

        int maxSlotsPerNotable = (hero.MapFaction != settlement.MapFaction) ? 2 : 4;

        foreach (Hero notable in settlement.Notables)
        {
            if (!notable.IsAlive)
            {
                continue;
            }

            int maxIndex = Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero(
              mobileParty.IsGarrison ? mobileParty.Party.Owner : mobileParty.LeaderHero,
              notable
            );

            for (int i = 0; i <= maxIndex && i < notable.VolunteerTypes.Length; i++)
            {
                CharacterObject troop = notable.VolunteerTypes[i];
                if (troop == null)
                {
                    continue;
                }

                if (Recruitment.ShouldRecruit(comp, heroSettings, troop, mobileParty.Party))
                {
                    allowedCount++;
                    totalWage += Campaign.Current.Models.PartyWageModel.GetCharacterWage(troop);
                }
            }
        }

        if (allowedCount > 0)
        {
            float avgWage = (float)totalWage / allowedCount;
            __result = (allowedCount, avgWage);
        }
        else
        {
            __result = (0, 0f);
        }
    }

    private static void FillSettlementsToVisitWithDistancesAsDaysPostfix(MobileParty mobileParty, IList listToFill)
    {
        /// Vanilla only searches for nearby non-faction settlements when the average distance of the party's own
        /// faction settlements in range is "large". Right at that threshold the candidate list flips every hour.
        /// This postfix always adds the nearby non-faction settlements for the clan's parties.

        if (mobileParty.LeaderHero is null
            || !SubModule.PartySettingsManager.IsHeroManageable(mobileParty.LeaderHero))
        {
            return;
        }

        if (mobileParty.Army is not null && mobileParty.Army.LeaderParty != mobileParty)
        {
            return;
        }

        Type elemType = listToFill.GetType().GetGenericArguments()[0];
        FieldInfo settlementField = elemType.GetField("Settlement");

        var existing = new HashSet<Settlement>();
        foreach (object o in listToFill)
        {
            existing.Add((Settlement)settlementField.GetValue(o));
        }

        float radius = 0.5f * Campaign.Current.EstimatedAverageLordPartySpeed * (float)CampaignTime.HoursInDay;
        var search = Settlement.StartFindingLocatablesAroundPosition(mobileParty.Position.ToVec2(), radius);
        for (Settlement s = Settlement.FindNextLocatable(ref search); s != null; s = Settlement.FindNextLocatable(ref search))
        {
            if (s.IsCastle || s.MapFaction == mobileParty.MapFaction || existing.Contains(s))
            {
                continue;
            }

            if (!(bool)IsSuitableMethod.Invoke(null, [mobileParty, s]))
            {
                continue;
            }

            var args = new object[] { mobileParty, s, null, null, null, null };
            GetNavigationMethod.Invoke(null, args);
            var nav = (MobileParty.NavigationType)args[2];
            float dist = (float)args[3];
            if (nav == MobileParty.NavigationType.None || dist >= GetMaxDays(nav))
            {
                continue;
            }

            // SettlementNavigationData(float distance, int id, Settlement, NavigationType, bool isFromPort, bool isTargetingPortBetter)
            listToFill.Add(Activator.CreateInstance(elemType, dist, s.GetHashCode(), s, nav, (bool)args[4], (bool)args[5]));
            existing.Add(s);
        }
    }

    private static float GetMaxDays(MobileParty.NavigationType nav)
    {
        return Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(nav) * 4f
            / (Campaign.Current.EstimatedAverageLordPartySpeed * (float)CampaignTime.HoursInDay);
    }
}
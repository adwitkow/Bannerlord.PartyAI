using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using HarmonyLib;
using HarmonyLib.PatchBuilder;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Patches;

internal class AiVisitSettlementBehaviorPatches
{
    public static void Apply(Harmony harmony)
    {
        harmony.Patch<AiVisitSettlementBehavior>()
            .Method("GetApproximateVolunteersCanBeRecruitedDataFromSettlement")
                .Postfix(GetApproximateVolunteersCanBeRecruitedDataFromSettlementPostfix);
    }

    private static void GetApproximateVolunteersCanBeRecruitedDataFromSettlementPostfix(ref (int, float) __result, Hero hero, Settlement settlement)
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
}
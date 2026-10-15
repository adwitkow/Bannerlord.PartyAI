using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Domain.Models;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.Models;

internal class PAITroopUpgradeModel : PartyTroopUpgradeModel
{
    public override bool DoesPartyHaveRequiredItemsForUpgrade(PartyBase party, CharacterObject upgradeTarget)
    {
        if (Hero.MainHero.Equals(party.Owner))
        {
            return BaseModel.DoesPartyHaveRequiredItemsForUpgrade(party, upgradeTarget);
        }

        // let AI always upgrade regardless of items
        return true;
    }

    public override float GetUpgradeChanceForTroopUpgrade(PartyBase party, CharacterObject troop, int upgradeTargetIndex)
    {
        if (party.MobileParty == null)
        {
            return BaseModel.GetUpgradeChanceForTroopUpgrade(party, troop, upgradeTargetIndex);
        }

        if (party.MobileParty.IsGarrison
            && !SubModule.PartySettingsManager.IsGarrisonManageable(party.MobileParty.CurrentSettlement))
        {
            return BaseModel.GetUpgradeChanceForTroopUpgrade(party, troop, upgradeTargetIndex);
        }
        else if (!SubModule.PartySettingsManager.IsManageable(party.LeaderHero))
        {
            return BaseModel.GetUpgradeChanceForTroopUpgrade(party, troop, upgradeTargetIndex);
        }

        if (upgradeTargetIndex < 0 || upgradeTargetIndex >= troop.UpgradeTargets.Length || troop.UpgradeTargets.Length == 0)
        {
            return 0.00001f;
        }

        PartyAiEntitySettings heroSettings;
        if (party.MobileParty.IsGarrison)
        {
            heroSettings = SubModule.PartySettingsManager.Settings(party.MobileParty.CurrentSettlement);
        }
        else
        {
            heroSettings = SubModule.PartySettingsManager.Settings(party.LeaderHero);
        }

        PartyComposition comp = Recruitment.GetPartyComposition(party, heroSettings, troop);

        if (heroSettings.MaxTroopTier > 0 && troop.Tier >= heroSettings.MaxTroopTier)
        {
            return 0f;
        }

        var upgradeTarget = troop.UpgradeTargets[upgradeTargetIndex];
        if (Recruitment.ShouldRecruit(comp, heroSettings, upgradeTarget, party))
        {
            return 1f;
        }

        for (int i = 0; i < troop.UpgradeTargets.Length; i++)
        {
            if (i == upgradeTargetIndex)
            {
                continue;
            }

            if (Recruitment.ShouldRecruit(comp, heroSettings, troop.UpgradeTargets[i], party))
            {
                return 0f;
            }
        }

        if (heroSettings.PartyTemplate?.Troops.Contains(upgradeTarget) ?? true)
        {
            var newTargets = Recruitment.UpgradeTargets(upgradeTarget, true, heroSettings.PartyTemplate)
                .ConvertAll(c => c.DefaultFormationClass.FallbackClass())
                .Distinct();
            var currentTargets = Recruitment.UpgradeTargets(troop, true, heroSettings.PartyTemplate)
                .ConvertAll(c => c.DefaultFormationClass.FallbackClass())
                .Distinct();
            if (Enumerable.SequenceEqual(newTargets, currentTargets))
            {
                return 1f;
            }
        }

        return 0f;
    }

    public override bool IsTroopUpgradeable(PartyBase party, CharacterObject character)
    {
        bool result = BaseModel.IsTroopUpgradeable(party, character);

        if (!result || party.MobileParty == null)
        {
            return result;
        }

        if (party.MobileParty.IsGarrison
            && !SubModule.PartySettingsManager.IsGarrisonManageable(party.MobileParty.CurrentSettlement))
        {
            return result;
        }
        else if (!SubModule.PartySettingsManager.IsManageable(party.LeaderHero))
        {
            return result;
        }

        for (int i = 0; i < character.UpgradeTargets.Length; i++)
        {
            if (GetUpgradeChanceForTroopUpgrade(party, character, i) > 0f)
            {
                return true;
            }
        }

        return false;
    }

    #region Pass-through overrides
    public override bool CanPartyUpgradeTroopToTarget(PartyBase party, CharacterObject character, CharacterObject target)
    {
        return BaseModel.CanPartyUpgradeTroopToTarget(party, character, target);
    }

    public override bool DoesPartyHaveRequiredPerksForUpgrade(PartyBase party, CharacterObject character, CharacterObject upgradeTarget, out PerkObject requiredPerk)
    {
        return BaseModel.DoesPartyHaveRequiredPerksForUpgrade(party, character, upgradeTarget, out requiredPerk);
    }

    public override ExplainedNumber GetGoldCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
    {
        return BaseModel.GetGoldCostForUpgrade(party, characterObject, upgradeTarget);
    }

    public override int GetSkillXpFromUpgradingTroops(PartyBase party, CharacterObject troop, int numberOfTroops)
    {
        return BaseModel.GetSkillXpFromUpgradingTroops(party, troop, numberOfTroops);
    }

    public override int GetXpCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
    {
        return BaseModel.GetXpCostForUpgrade(party, characterObject, upgradeTarget);
    }
    #endregion
}

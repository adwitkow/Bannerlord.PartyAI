using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using Helpers;
using System.Diagnostics.CodeAnalysis;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.CampaignBehaviors.AiBehaviors;

public abstract class PartyOrderBehaviorBase : CampaignBehaviorBase
{
    protected abstract PartyAiOrderType OrderType { get; }

    public override void RegisterEvents()
    {
        // It is unnecessary to force the consumers to override.
        // They will when they need to.
    }

    public override void SyncData(IDataStore dataStore)
    {
        // It is unnecessary to force the consumers to override
        // They will when they need to.
    }

    protected bool IsPartyOrderRelevant(
        Hero? hero,
        [NotNullWhen(true)]out PartyAiEntitySettings? settings,
        [NotNullWhen(true)]out PartyAiOrder? order)
    {
        order = null;
        settings = null;

        if (!SubModule.PartySettingsManager.IsHeroManageable(hero))
        {
            return false;
        }

        settings = SubModule.PartySettingsManager.Settings(hero);
        if (!settings.HasActiveOrder)
        {
            return false;
        }

        if (settings.Order.Behavior != OrderType)
        {
            return false;
        }

        order = settings.Order;
        return true;
    }

    protected bool IsPartyOrderRelevant(
        MobileParty? party,
        [NotNullWhen(true)] out PartyAiEntitySettings? settings,
        [NotNullWhen(true)] out PartyAiOrder? order)
    {
        if (!IsPartyOrderRelevant(party?.LeaderHero, out settings, out order))
        {
            return false;
        }

        if (party?.GetNumDaysForFoodToLast() <= 3)
        {
            settings = null;
            order = null;
            return false;
        }

        return true;
    }

    protected void AddBehaviorScore(AIBehaviorData behaviorData, float score, PartyThinkParams thinkParams)
    {
        if (thinkParams.TryGetBehaviorScore(in behaviorData, out float previousScore)
            && score > previousScore)
        {
            thinkParams.SetBehaviorScore(in behaviorData, score);
            return;
        }

        thinkParams.AddBehaviorScore((behaviorData, score));
    }

    protected bool TryNavigateToSettlement(
        MobileParty party,
        Settlement targetSettlement,
        AiBehavior behavior,
        PartyThinkParams thinkParams)
    {
        AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
            party,
            targetSettlement,
            isTargetingPort: false,
            out var navigationType,
            out var bestDistance,
            out var isFromPort);

        if (navigationType == MobileParty.NavigationType.None)
        {
            return false;
        }

        var willGatherArmy = thinkParams.PossibleArmyMembersUponArmyCreation?.Count > 5;

        AIBehaviorData aibehaviorData = new AIBehaviorData(
            targetSettlement,
            behavior,
            navigationType,
            willGatherArmy,
            isFromPort,
            false);

        AddBehaviorScore(aibehaviorData, Constants.BehaviorScore, thinkParams);

        return true;
    }

    protected bool TryNavigateToParty(
        MobileParty party,
        MobileParty targetParty,
        AiBehavior behavior,
        PartyThinkParams thinkParams)
    {
        bool isFromPort = false;
        bool isTargetingPort = false;
        MobileParty.NavigationType bestNavType = MobileParty.NavigationType.None;
        if (targetParty.CurrentSettlement is not null)
        {
            isTargetingPort = targetParty.CurrentSettlement.HasPort && party.IsCurrentlyAtSea;

            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                party,
                targetParty.CurrentSettlement,
                isTargetingPort,
                out bestNavType,
                out _,
                out isFromPort);
        }
        else
        {
            AiHelper.GetBestNavigationTypeAndDistanceOfMobilePartyForMobileParty(
                party,
                targetParty,
                out bestNavType,
                out _);
        }

        if (bestNavType == MobileParty.NavigationType.None)
        {
            return false;
        }

        var willGatherArmy = thinkParams.PossibleArmyMembersUponArmyCreation?.Count > 5;

        AIBehaviorData aibehaviorData = new AIBehaviorData(
            targetParty,
            behavior,
            bestNavType,
            willGatherArmy,
            isFromPort,
            isTargetingPort);

        AddBehaviorScore(aibehaviorData, Constants.BehaviorScore, thinkParams);

        return true;
    }
}

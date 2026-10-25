using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.CampaignBehaviors.AiBehaviors;

internal class RecruitmentBehavior : PartyOrderBehaviorBase
{
    private const int RecruitmentSettlementCooldownDays = 10;

    private List<PAISettlementVisitLog> _recentlyRecruitedFromSettlements = new();

    protected override PartyAiOrderType OrderType => PartyAiOrderType.RecruitFromTemplate;

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, OnTroopRecruited);
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
        CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, OnDailyTickParty);
    }

    private void OnDailyTickParty(MobileParty party)
    {
        var hero = party?.LeaderHero;
        if (!SubModule.PartySettingsManager.IsHeroManageable(hero))
        {
            return;
        }

        var settings = SubModule.PartySettingsManager.Settings(hero);
        TrySetAutoRecruitmentOrder(settings, party);
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_recentlyRecruitedFromSettlements", ref _recentlyRecruitedFromSettlements);
    }

    private void OnTroopRecruited(Hero recruiter, Settlement settlement, Hero source, CharacterObject troop, int amount)
    {
        if (!IsPartyOrderRelevant(recruiter, out var settings, out _)
            || settlement is null)
        {
            return;
        }

        var party = recruiter.PartyBelongedTo;
        var partyComposition = Recruitment.GetPartyComposition(party.Party, settings);

        _recentlyRecruitedFromSettlements.Add(new(settlement, CampaignTime.Now, recruiter.PartyBelongedTo));
    }

    private void OnDailyTick()
    {
        _recentlyRecruitedFromSettlements.RemoveAll(l => l.Visited.ElapsedDaysUntilNow > RecruitmentSettlementCooldownDays);
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        if (!IsPartyOrderRelevant(party, out var settings, out var order))
        {
            return;
        }

        int freeSlots = party.Party.PartySizeLimit - party.Party.NumberOfAllMembers;
        float partyRatio = party.PartySizeRatio;
        if (freeSlots <= 0 || partyRatio > settings.Composition.GetTotal())
        {
            settings.ClearOrder();
            return;
        }

        var partyComposition = Recruitment.GetPartyComposition(party.Party, settings);
        var targetSettlement = order.Target as Settlement;
        if (ShouldPickNewRecruitmentTarget(settings, party, targetSettlement, partyComposition))
        {
            var newTarget = Navigation.FindNearestSettlement(
                s => IsGoodTargetForRecruiting(s, party, settings, partyComposition),
                party);

            settings.Order?.Target = newTarget;
            targetSettlement = newTarget;
        }

        if (targetSettlement is null)
        {
            Message.OrderStoppedNoValidTargets(party, order);
            StopOrder(settings);
            return;
        }

        if (!TryNavigateToSettlement(party, targetSettlement, AiBehavior.GoToSettlement, thinkParams))
        {
            Message.OrderStoppedTargetUnreachable(party, order);
            StopOrder(settings);
            return;
        }

        party.Ai.SetInitiative(0f, 1f, 2f);
    }

    private bool ShouldPickNewRecruitmentTarget(
        PartyAiEntitySettings settings,
        MobileParty party,
        [NotNullWhen(false)] Settlement? currentSettlement,
        PartyComposition partyComposition)
    {
        if (currentSettlement is null)
        {
            return true;
        }

        var settlementRecentlyVisited = _recentlyRecruitedFromSettlements.Any(l => l.Settlement == currentSettlement && l.Party == party);
        var volunteersAvailable = Recruitment.CollectEligibleVolunteers(party, currentSettlement, settings, partyComposition).Count > 0;
        var canVisitSettlement = IsSettlementSuitableForVisiting(party, currentSettlement);

        return settlementRecentlyVisited || !volunteersAvailable || !canVisitSettlement;
    }

    private bool IsGoodTargetForRecruiting(
        Settlement settlement,
        MobileParty party,
        PartyAiEntitySettings settings,
        PartyComposition partyComposition)
    {
        if (!settlement.IsVillage && !settlement.IsTown)
        {
            return false;
        }

        if (!IsSettlementSuitableForVisiting(party, settlement))
        {
            return false;
        }

        if (!settings.RecruitFromEnemySettlements
            && FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
        {
            return false;
        }

        if (_recentlyRecruitedFromSettlements.Any(l => l.Settlement == settlement && l.Party == party))
        {
            return false;
        }

        var eligibleVolunteers = Recruitment.CollectEligibleVolunteers(party, settlement, settings, partyComposition);
        if (eligibleVolunteers.Count == 0)
        {
            return false;
        }

        return true;
    }

    private static bool IsSettlementSuitableForVisiting(MobileParty mobileParty, Settlement settlement)
    {
        if (settlement.Party.MapEvent != null)
        {
            return false;
        }

        if (settlement.Party.SiegeEvent != null)
        {
            if (settlement.Party.SiegeEvent.IsBlockadeActive)
            {
                return false;
            }

            if (!mobileParty.HasNavalNavigationCapability)
            {
                return false;
            }
        }

        if (settlement.IsVillage && settlement.Village.VillageState != Village.VillageStates.Normal)
        {
            return false;
        }

        return true;
    }

    private void TrySetAutoRecruitmentOrder(PartyAiEntitySettings settings, MobileParty party)
    {
        if (ShouldAutoRecruit(settings, party)
            && CanUseRecruitOrderAutomatically(settings))
        {
            settings.SetOrder(PartyAiOrderType.RecruitFromTemplate);
        }
    }

    private bool CanUseRecruitOrderAutomatically(PartyAiEntitySettings settings)
    {
        var isFree = !settings.HasActiveOrder;
        var hasRecruitmentOrder = settings.Order?.Behavior == PartyAiOrderType.RecruitFromTemplate
            || settings.OrderQueue.Any(o => o.Behavior == PartyAiOrderType.RecruitFromTemplate);

        return isFree || !hasRecruitmentOrder;
    }

    private bool ShouldAutoRecruit(PartyAiEntitySettings settings, MobileParty party)
    {
        return settings.AutoRecruitment
            && party.PartySizeRatio < settings.AutoRecruitmentPercentage
            && party.Army == null;
    }

    private void StopOrder(PartyAiEntitySettings settings)
    {
        settings.ClearOrder();
        _recentlyRecruitedFromSettlements.RemoveAll(l => string.Equals(
            l.Party?.StringId,
            settings.Hero?.PartyBelongedTo?.StringId,
            StringComparison.InvariantCultureIgnoreCase));
    }

    public class PAISettlementVisitLog
    {
        [SaveableProperty(1)] public Settlement Settlement { get; private set; }
        [SaveableProperty(2)] public CampaignTime Visited { get; private set; }
        [SaveableProperty(3)] public MobileParty Party { get; private set; }
        public PAISettlementVisitLog(Settlement settlement, CampaignTime visited, MobileParty party)
        {
            Settlement = settlement;
            Visited = visited;
            Party = party;
        }
    }
}
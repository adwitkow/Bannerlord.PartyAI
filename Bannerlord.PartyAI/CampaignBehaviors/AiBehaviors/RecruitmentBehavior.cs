using Bannerlord.PartyAI.Compat;
using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using System.Collections.Generic;
using System.Diagnostics;
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
            // First try to find a settlement where IHM advertises a real troop
            // that belongs to the selected PAC template/upgrade path.
            var newTarget = Navigation.FindNearestSettlement(
                s => IsGoodIhmTargetForRecruiting(s, party, settings),
                party);

            if (newTarget is not null)
            {
                var match = IhmRecruitmentBridge.FindBestTemplateMatch(
                    newTarget,
                    settings);

                if (match is not null)
                {
                    var leaderName = party.LeaderHero?.Name?.ToString()
                        ?? party.Name?.ToString()
                        ?? "<unknown party>";

                    Debug.Print(
                        $"[PAC-IHM] {leaderName} is traveling to " +
                        $"{newTarget.Name} to recruit IHM troop " +
                        $"{match.AdvertisedTroop.Name} " +
                        $"(T{match.AdvertisedTroop.Tier}), targeting " +
                        $"{match.DesiredTemplateTroop.Name} " +
                        $"(T{match.DesiredTemplateTroop.Tier}).");

                    if (match.DesiredTemplateTroop.Tier >= 6)
                    {
                        Debug.Print(
                            $"[PAC-IHM][T6] {leaderName} is going to " +
                            $"{newTarget.Name} for a route to T6 " +
                            $"{match.DesiredTemplateTroop.Name}; " +
                            $"IHM currently advertises " +
                            $"{match.AdvertisedTroop.Name} " +
                            $"(T{match.AdvertisedTroop.Tier}).");
                    }
                }
            }
            else
            {
                // No physical IHM match exists right now.
                // Preserve PAC's original behavior as a fallback.
                newTarget = Navigation.FindNearestSettlement(
                    s => IsGoodTargetForRecruiting(
                        s,
                        party,
                        settings,
                        partyComposition),
                    party);

                var leaderName = party.LeaderHero?.Name?.ToString()
                    ?? party.Name?.ToString()
                    ?? "<unknown party>";

                Debug.Print(
                    $"[PAC-IHM] No IHM template match currently advertised " +
                    $"for {leaderName}; falling back to normal PAC recruitment.");
            }

            settings.Order?.Target = newTarget;
            targetSettlement = newTarget;
        }

        if (targetSettlement is null)
        {
            Message.OrderStoppedNoValidTargets(party, order);
            settings.ClearOrder();
            return;
        }

        if (!TryNavigateToSettlement(party, targetSettlement, AiBehavior.GoToSettlement, thinkParams))
        {
            Message.OrderStoppedTargetUnreachable(party, order);
            settings.ClearOrder();
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
        var canVisitSettlement = CanVisitSettlement(party, currentSettlement);

        return settlementRecentlyVisited || !volunteersAvailable || !canVisitSettlement;
    }

    private bool IsGoodIhmTargetForRecruiting(
        Settlement settlement,
        MobileParty party,
        PartyAiEntitySettings settings)
    {
        if (!settlement.IsVillage && !settlement.IsTown)
        {
            return false;
        }

        if (!CanVisitSettlement(party, settlement))
        {
            return false;
        }

        if (!settings.RecruitFromEnemySettlements
            && FactionManager.IsAtWarAgainstFaction(
                party.MapFaction,
                settlement.MapFaction))
        {
            return false;
        }

        if (_recentlyRecruitedFromSettlements.Any(
            l => l.Settlement == settlement && l.Party == party))
        {
            return false;
        }

        return IhmRecruitmentBridge.HasTemplateMatch(settlement, settings);
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

        if (!CanVisitSettlement(party, settlement))
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

        // if we're going to convert the troop anyway, it doesn't matter
        if (SubModule.PartySettingsManager.AllowTroopConversion && settings.PartyTemplate != null)
        {
            return true;
        }

        var template = settings.PartyTemplate;
        if (template is not null && !template.TroopCultures.Contains(settlement.Culture))
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

    private bool CanVisitSettlement(MobileParty mobileParty, Settlement settlement)
    {
        if (mobileParty.HasLandNavigationCapability)
        {
            return !settlement.IsUnderSiege && settlement.Party.MapEvent == null;
        }
        else
        {
            return settlement.SiegeEvent == null || !settlement.SiegeEvent.IsBlockadeActive;
        }
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
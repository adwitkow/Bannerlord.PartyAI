using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.CampaignBehaviors.AiBehaviors;

internal class PatrolClanLandsBehavior : PartyOrderBehaviorBase
{
    private const int MaxPatrolDays = 4;
    private const int Outdated = 8;

    protected override PartyAiOrderType OrderType => PartyAiOrderType.PatrolClanLands;

    private Dictionary<(string PartyId, string SettlementId), int> _daysTargetingDictionary = [];

    public override void RegisterEvents()
    {
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
        CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    private void OnDailyTick(MobileParty party)
    {
        if (!IsPartyOrderRelevant(party, out var settings, out var order))
        {
            return;
        }

        ClearOutdatedTargets();
        TickAllTargets();

        TryPickNewTarget(party, settings, order);
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        if (!IsPartyOrderRelevant(party, out var settings, out var order))
        {
            return;
        }

        if (!ShouldContinueExecutingOrder(party, settings, order))
        {
            return;
        }

        if (order.Target is not Settlement targetSettlement)
        {
            order.Target = null; // Not sure how it can happen yet, but let's be safe and reroll
            return;
        }

        if (!TryNavigateToSettlement(party, targetSettlement, AiBehavior.PatrolAroundPoint, thinkParams))
        {
            order.Target = null;
            return; // Fallback to default AI until settlement is rerolled
        }

#if LOWER_THAN_1_5
        if (party.Objective != MobileParty.PartyObjective.Defensive)
        {
            settings.CachedPartyObjective = party.Objective;
            party.SetPartyObjective(MobileParty.PartyObjective.Defensive);
        }
#endif
    }

    private bool ShouldContinueExecutingOrder(MobileParty party, PartyAiEntitySettings settings, PartyAiOrder order)
    {
        var target = order.Target as Settlement;
        return (target is not null && !FactionManager.IsAtWarAgainstFaction(party.MapFaction, target.MapFaction))
            || TryPickNewTarget(party, settings, order);
    }

    private bool TryPickNewTarget(MobileParty party, PartyAiEntitySettings settings, PartyAiOrder order)
    {
        var averageDistance = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default);
        var range = averageDistance * settings.PatrolRadius;
        var maxDistanceSq = range * 2 * (range * 2);

        var clanSettlements = party.ActualClan.Settlements
            .Where(settlement => !IsBlacklisted(settlement, party))
            .ToMBList();

        if (clanSettlements.Count == 0)
        {
            settings.ClearOrder(); // fallback to kingdom settlements?
            return false;
        }

        var settlementsByDistance = clanSettlements
            .Select(settlement => (Settlement: settlement, DistanceSquared: settlement.Position.DistanceSquared(party.Position)))
            .OrderBy(tuple => tuple.DistanceSquared)
            .ToArray();
        var closest = settlementsByDistance[0];

        Settlement target;
        // Away from clan lands
        if (closest.DistanceSquared > maxDistanceSq)
        {
            target = closest.Settlement;
        }
        else
        {
            // Variety in patrols
            var random = MBRandom.RandomFloat;
            if (random < 0.01)
            {
                // 1% to switch to a completely random clan settlement
                target = clanSettlements.GetRandomElement();
            }
            else if (random < 0.25f)
            {
                // 25% to switch to a random clan settlement within range
                var settlementsInRange = settlementsByDistance
                    .Where(tuple => tuple.DistanceSquared < maxDistanceSq)
                    .ToArray();

                target = settlementsInRange.GetRandomElement().Settlement;
            }
            else
            {
                target = closest.Settlement;
            }
        }

        order.Target = target;

        AddTargetIfMissing(target, party);

        return true;
    }

    private bool IsBlacklisted(Settlement settlement, MobileParty mobileParty)
    {
        _daysTargetingDictionary.TryGetValue((mobileParty.StringId, settlement.StringId), out int days);

        return days > MaxPatrolDays;
    }

    private void ClearOutdatedTargets()
    {
        var toClear = _daysTargetingDictionary
            .Where(pair => pair.Value > Outdated)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var key in toClear)
        {
            _daysTargetingDictionary.Remove(key);
        }
    }

    private void TickAllTargets()
    {
        var keys = _daysTargetingDictionary.Keys.ToArray();
        foreach (var key in keys)
        {
            _daysTargetingDictionary[key]++;
        }
    }

    private void AddTargetIfMissing(Settlement settlement, MobileParty mobileParty)
    {
        var key = (mobileParty.StringId, settlement.StringId);
        if (!_daysTargetingDictionary.ContainsKey(key))
        {
            _daysTargetingDictionary[key] = 0;
        }
    }
}

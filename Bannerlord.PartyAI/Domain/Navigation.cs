using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.Domain;

public static class Navigation
{
    /// <summary>
    /// Mirrors vanilla AiVisitSettlementBehavior.GetBestNavigationDataForVisitingSettlement.
    /// Computes the best navigation type and port transition flags for reaching a settlement.
    /// </summary>
    public static bool TryGetBestNavigationDataForSettlement(
      MobileParty party,
      Settlement settlement,
      out MobileParty.NavigationType navigationType,
      out bool isFromPort,
      out bool isTargetingPort)
    {
        navigationType = MobileParty.NavigationType.None;
        isFromPort = false;
        isTargetingPort = false;

        if (party == null || settlement == null)
        {
            return false;
        }

        // Normalize to canonical Settlement instance to avoid cache-key identity mismatches
        Settlement normalizedSettlement = Settlement.Find(settlement.StringId) ?? settlement;

        MobileParty.NavigationType bestNavType = MobileParty.NavigationType.None;
        float bestDistance = float.MaxValue;
        bool bestIsFromPort = false;

        bool portBlocked = normalizedSettlement.HasPort &&
                           normalizedSettlement.SiegeEvent != null &&
                           normalizedSettlement.SiegeEvent.IsBlockadeActive;

        // Try non-port targeting first (unless blockade prevents portless approach for naval-capable parties).
        if (!portBlocked || !party.HasNavalNavigationCapability)
        {
            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
              party,
              normalizedSettlement,
              false,
              out bestNavType,
              out bestDistance,
              out bestIsFromPort);
        }

        // If the party can travel by sea and the settlement has a port, also try targeting the port.
        if (party.HasNavalNavigationCapability && normalizedSettlement.HasPort)
        {
            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
              party,
              normalizedSettlement,
              true,
              out MobileParty.NavigationType portNavType,
              out float portDistance,
              out bool portIsFromPort);

            if (portDistance < bestDistance)
            {
                navigationType = portNavType;
                isFromPort = portIsFromPort;
                isTargetingPort = true;
                return navigationType != MobileParty.NavigationType.None;
            }
        }

        navigationType = bestNavType;
        isFromPort = bestIsFromPort;
        isTargetingPort = false;

        return navigationType != MobileParty.NavigationType.None;
    }

    public static MobileParty.NavigationType SanitizeNavigationType(MobileParty.NavigationType navigationType)
    {
        if (navigationType == MobileParty.NavigationType.None)
        {
            return MobileParty.NavigationType.Default;
        }

        // If the runtime value is outside the defined enum, treat it as Default.
        // This does not guarantee campaign caches support the value, so callers should still use safe wrappers.
        if (!Enum.IsDefined(typeof(MobileParty.NavigationType), navigationType))
        {
            return MobileParty.NavigationType.Default;
        }

        return navigationType;
    }

    public static float GetSafeDistanceBetweenClosestTwoTowns(MobileParty.NavigationType navigationType)
    {
        var safeNavType = SanitizeNavigationType(navigationType);

        try
        {
            return Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(safeNavType);
        }
        catch (KeyNotFoundException)
        {
            return Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default);
        }
    }

    public static Settlement? FindNearestSettlement(
        Func<Settlement, bool> condition,
        MobileParty mobileParty)
    {
        return FindNearestSettlement(mobileParty, condition, mobileParty.NavigationCapability);
    }


    public static Settlement? FindNearestSettlement(
        IMapPoint mapPoint)
    {
        return FindNearestSettlement(mapPoint, null);
    }

    private static Settlement? FindNearestSettlement(
        IMapPoint mapPoint,
        Func<Settlement, bool>? condition,
        MobileParty.NavigationType navCapability = MobileParty.NavigationType.Default)
    {
        var sanitizedNavType = SanitizeNavigationType(navCapability);

        var query = Settlement.All.AsEnumerable();

        if (condition is not null)
        {
            query = query.Where(condition);
        }

        var fewClosest = query
            .OrderBy(settlement =>
                DistanceHelper.FindClosestDistanceFromMapPointToSettlement(
                    mapPoint,
                    settlement,
                    sanitizedNavType,
                    out _))
            .Take(5)
            .ToArray();

        if (fewClosest.Length <= 1)
        {
            return fewClosest.FirstOrDefault();
        }

        return fewClosest
            .Skip(fewClosest.Length - 1)
            .FirstOrDefault();
    }
}

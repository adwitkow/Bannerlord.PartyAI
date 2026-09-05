using Bannerlord.PartyAI.Domain.Models;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;
#if LOWER_THAN_1_5
using static TaleWorlds.CampaignSystem.Party.MobileParty;
#endif

namespace Bannerlord.PartyAI.Models;

public class PartyAiEntitySettings
{
    [SaveableProperty(1)] public Hero? Hero { get; private set; }
    [SaveableProperty(2)] public bool AllowJoinArmies { get; set; } = true;
    [SaveableProperty(3)] public bool AllowDonateTroops { get; set; } = true;
    [SaveableProperty(4)] public bool AllowRaidVillages { get; set; } = true;
    [SaveableProperty(5)] public PAICustomTemplate? PartyTemplate { get; set; }
    [SaveableProperty(6)] public PartyComposition Composition { get; set; }
    [SaveableProperty(7)] public bool AllowLordPrisoners { get; set; } = true;
    [SaveableProperty(8)] public PartyAiOrder? Order { get; private set; }
#if LOWER_THAN_1_5
    [SaveableProperty(9)] public PartyObjective CachedPartyObjective { get; set; }
#endif
    [SaveableProperty(10)] public bool AllowSieging { get; set; } = true;
    [SaveableProperty(11)] public Settlement? Settlement { get; private set; }
    [SaveableProperty(12)] public bool BuyHorses { get; set; }
    [SaveableProperty(13)] public int BuyHorsesBudget { get; set; } = 500;
    [SaveableProperty(14)] public int BuyHorsesBudgetToday { get; private set; } = 500;
    [SaveableProperty(15)] public int MaxTroopTier { get; set; }
    [SaveableProperty(16)] public int TroopsConvertibleToday { get; private set; } = 5;
    [SaveableProperty(17)] public PartyAiOrder? FallbackOrder { get; private set; }
    [SaveableProperty(18)] public bool AllowRecruitment { get; set; } = true;
    [SaveableProperty(19)] public bool FilterSettlements { get; set; } = false;
    [SaveableProperty(20)] public List<Settlement> FilteredSettlements { get; set; } = new();
    [SaveableProperty(21)] public List<PartyAiOrder> OrderQueue { get; set; } = new();
    [SaveableProperty(22)] public bool AutoRecruitment { get; set; } = true;
    [SaveableProperty(23)] public float AutoRecruitmentPercentage { get; set; } = 0.5f;
    [SaveableProperty(24)] public bool DismissUnwantedTroops { get; set; } = false;
    [SaveableProperty(25)] public float DismissUnwantedTroopsPercentage { get; set; } = 0.8f;
    [SaveableProperty(26)] public bool AllowTakeTroopsFromSettlement { get; set; } = false;
    [SaveableProperty(27)] public float PatrolRadius { get; set; } = 1f;
    [SaveableProperty(28)] public bool RecruitFromEnemySettlements { get; set; } = false;

    public PartyAiEntitySettings()
    {
        Composition = new PartyComposition(0.35f, 0.30f, 0.20f, 0.15f);
    }

    public PartyAiEntitySettings(Hero hero) : this()
    {
        Hero = hero;
    }

    public PartyAiEntitySettings(Settlement settlement) : this()
    {
        Settlement = settlement;
    }

    public PartyAiEntitySettings(PartyAiEntitySettings cloneFrom)
    {
        PartyTemplate = cloneFrom.PartyTemplate;
        Composition = new PartyComposition(cloneFrom.Composition);
        CopyOptionsFrom(cloneFrom);
    }

    public PartyAiEntitySettings(
        PartyAiEntitySettings cloneFrom,
        Hero hero) : this(cloneFrom)
    {
        Hero = hero;
    }

    public PartyAiEntitySettings(
        PartyAiEntitySettings cloneFrom,
        Settlement settlement) : this(cloneFrom)
    {
        Settlement = settlement;
    }

    internal void SetOrder(PartyAiOrderType behavior, IMapPoint? target = null)
    {
        var order = new PartyAiOrder(behavior, target);

        if (Settlement != null)
        {
            return;
        }

        var party = Hero?.PartyBelongedTo;
        var army = party?.Army;
        var armyLeader = army?.LeaderParty.LeaderHero;
        if (army is not null
            && armyLeader != Hero
            && armyLeader != Hero.MainHero)
        {
            party!.Army = null;
        }

        if (HasActiveOrder)
        {
            OrderQueue.Insert(0, Order);
        }

        Order = order;
    }

    internal void SetFallbackOrder(PartyAiOrderType behavior, IMapPoint? target = null)
    {
        var order = new PartyAiOrder(behavior, target);

        if (Settlement != null)
        {
            return;
        }

        FallbackOrder = order;
    }

    [MemberNotNullWhen(true, nameof(Order))]
    internal bool HasActiveOrder => Order != null && Order.Behavior != PartyAiOrderType.None;

    internal void ClearOrder()
    {
        if (Settlement != null)
        {
            return;
        }

#if LOWER_THAN_1_5
        if (Hero.IsPartyLeader && Hero.PartyBelongedTo != null && HasActiveOrder)
        {
            Hero.PartyBelongedTo.SetPartyObjective(CachedPartyObjective);
        }
#endif

        Hero.PartyBelongedTo?.Ai.SetDoNotMakeNewDecisions(false);

        Order = null;

        if (OrderQueue.Count > 0)
        {
            Order = OrderQueue[0];
            OrderQueue.RemoveAt(0);
        }
    }

    internal void ClearAllOrders()
    {
        OrderQueue.Clear();
        ClearOrder();
    }

    internal void CopyOptionsFrom(PartyAiEntitySettings settings)
    {
        AllowJoinArmies = settings.AllowJoinArmies;
        AllowDonateTroops = settings.AllowDonateTroops;
        AllowTakeTroopsFromSettlement = settings.AllowTakeTroopsFromSettlement;
        AllowSieging = settings.AllowSieging;
        AllowRaidVillages = settings.AllowRaidVillages;
        AllowLordPrisoners = settings.AllowLordPrisoners;
        BuyHorses = settings.BuyHorses;
        Composition = new PartyComposition(settings.Composition);
        BuyHorsesBudget = settings.BuyHorsesBudget;
        MaxTroopTier = settings.MaxTroopTier;
        AllowRecruitment = settings.AllowRecruitment;
        FilterSettlements = settings.FilterSettlements;
        FilteredSettlements = settings.FilteredSettlements?.ToList() ?? new();
        OrderQueue = settings.OrderQueue?
            .Select(order => new PartyAiOrder(order))
            .ToList() ?? [];
        AutoRecruitment = settings.AutoRecruitment;
        AutoRecruitmentPercentage = settings.AutoRecruitmentPercentage;
        DismissUnwantedTroops = settings.DismissUnwantedTroops;
        DismissUnwantedTroopsPercentage = settings.DismissUnwantedTroopsPercentage;
        PatrolRadius = settings.PatrolRadius;
        RecruitFromEnemySettlements = settings.RecruitFromEnemySettlements;

        if (settings.FallbackOrder is not null)
        {
            SetFallbackOrder(
                settings.FallbackOrder.Behavior,
                settings.FallbackOrder.Target);
        }

        ResetBudgets();
    }

    internal void ResetBudgets()
    {
        BuyHorsesBudgetToday = BuyHorsesBudget;
        TroopsConvertibleToday = SubModule.PartySettingsManager.TroopsConvertedPerDay > 0 ? SubModule.PartySettingsManager.TroopsConvertedPerDay : int.MaxValue;
    }

    internal void DeductHorseBudget(int amount) => BuyHorsesBudgetToday -= amount;
    internal void DeductTroopsConvertibleToday(int amount) => TroopsConvertibleToday -= amount;

    internal void SetPartyTemplate(PAICustomTemplate? template)
    {
        PartyTemplate = template;
        Composition.ApplyTemplate(template, out _);

        // Only affect recruiting targets
        if (HasActiveOrder && Order?.Behavior == PartyAiOrderType.RecruitFromTemplate && template != null)
        {
            if (Order.Target is Settlement settlement)
            {
                var cultures = template.TroopCultures;
                bool unrestricted = cultures == null || cultures.Count == 0;
                if (!unrestricted && !cultures.Contains(settlement.Culture))
                {
                    Order.Target = null;
                }
            }
            else
            {
                // If recruit order is active but target isn't a Settlement, clear it defensively.
                Order.Target = null;
            }
        }

        // Unlock party AI so it will re-evaluate on next hourly tick
        MobileParty? ownedParty = Hero?.PartyBelongedTo;
        if (ownedParty?.Ai != null)
        {
            ownedParty.Ai.SetDoNotMakeNewDecisions(false);
            ownedParty.Ai.RethinkAtNextHourlyTick = true;
        }
    }
}

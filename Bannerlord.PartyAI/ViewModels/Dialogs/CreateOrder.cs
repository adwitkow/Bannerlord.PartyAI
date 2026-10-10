using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dialogs;

internal class CreateOrder
{
    private static Action _onCreateOrder;
    private static Hero _hero;
    private static PartyAiEntitySettings _settings;
    private static bool _fallback;

    private static readonly string _titleText = new TextObject("{=PAIUq8Q1n8k}Choose which type of order to add").ToString();
    private static readonly string _landpatrolText = new TextObject("{=PAIaOu88dqT}Patrol an area").ToString();
    private static readonly string _visitText = new TextObject("{=PAIIL6JG6Na}Visit a settlement").ToString();
    private static readonly string _escortText = new TextObject("{=PAI1Et6heEa}Escort a party").ToString();
    private static readonly string _recruitText = new TextObject("{=PAIyzzBSM4P}Recruit").ToString();
    private static readonly string _recruitHint = new TextObject("{=PAIHJFAtbk8}Order the party leader to only focus on recruiting troops. If they have an assigned troop template, they will only visit settlements that offer those troops. Keep in mind these settlements may be far away.").ToString();
    private static readonly string _stayInSettlementText = new TextObject("{=PAIOzsG1s1J}Stay in a settlement").ToString();
    private static readonly string _landpatrolHintText = new TextObject("Patrol an area around the target settlement. The party will visit villages and towns to restock its troops and supplies. Bandits and other enemies will be chased down if the party leader believes they can be caught. The party will defend villages and castles/towns within its patrol radius from raids and sieges.").ToString();
    private static readonly string _visitHintText = new TextObject("{=PAIljAEpAKF}Visit a settlement but don't stay there.").ToString();
    private static readonly string _escortHintText = new TextObject("{=PAIEI3gTLMP}Escort a party").ToString();
    private static readonly string _stayInSettlementHintText = new TextObject("{=PAIVeQlQhCC}Stay in the settlement. Will not defend the settlement if it is under siege and the party is outside the walls.").ToString();
    private static readonly string _besiegeText = new TextObject("{=PAIgXDbzpdD}Besiege a settlement").ToString();
    private static readonly string _besiegeHintText = new TextObject("{=PAIzxQXNul8}The party or army will besiege the target settlement. The order will be cleared upon capturing the city or by the attacking army being defeated.").ToString();
    private static readonly string _defendText = new TextObject("{=PAIgNGL6W5j}Defend a settlement").ToString();
    private static readonly string _defendHintText = new TextObject("{=PAITZmUFJSB}Stay in the garrison of the settlement. The party may make occassional visits to other settlements for food if there is not enough food in the settlement to buy.").ToString();

    public static void Create(PartyAiEntitySettings settings, Action callback, bool fallback = false)
    {
        _hero = settings.Hero;
        _settings = settings;
        _onCreateOrder = callback;
        _fallback = fallback;

        List<InquiryElement> list = new();

        list.Add(new InquiryElement(new PartyAiOrder(PartyAiOrderType.PatrolAroundPoint), _landpatrolText, null, true, _landpatrolHintText));
        list.Add(new InquiryElement(new PartyAiOrder(PartyAiOrderType.EscortParty), _escortText, null, true, _escortHintText));
        list.Add(new InquiryElement(new PartyAiOrder(PartyAiOrderType.StayInSettlement), _stayInSettlementText, null, true, _stayInSettlementHintText));
        list.Add(new InquiryElement(new PartyAiOrder(PartyAiOrderType.VisitSettlement), _visitText, null, true, _visitHintText));
        if (!_fallback)
        {
            list.Add(new InquiryElement(new PartyAiOrder(PartyAiOrderType.BesiegeSettlement), _besiegeText, null, true, _besiegeHintText));
        }
        list.Add(new InquiryElement(new PartyAiOrder(PartyAiOrderType.DefendSettlement), _defendText, null, true, _defendHintText));
        list.Add(new InquiryElement(new PartyAiOrder(PartyAiOrderType.RecruitFromTemplate), _recruitText, null, true, _recruitHint));
        if (_fallback)
        {
            list.Add(new InquiryElement(new PartyAiOrder(PartyAiOrderType.None), new TextObject("{=koX9okuG}None").ToString(), null, true, string.Empty));
        }

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(_titleText, string.Empty, list, isExitShown: true, 1, 1, GameTexts.FindText("str_next").ToString(), GameTexts.FindText("str_cancel").ToString(), CreateCallback, null, "", true));
    }

    private static void CreateCallback(List<InquiryElement> list)
    {
        if (list.Count == 0)
        {
            return;
        }

        PartyAiOrder order = (PartyAiOrder)list.First().Identifier;

        string title = null;
        List<InquiryElement> newList = new();
        title = new TextObject("{=PAIZScpdz8d}Select a target").ToString();

        switch (order.Behavior)
        {
            case PartyAiOrderType.None:
            case PartyAiOrderType.RecruitFromTemplate:
                ChooseTargetCallback(list);
                return;
            case PartyAiOrderType.EscortParty:
                Hero hero = _hero is not null
                    ? _hero
                    : Hero.MainHero;

                var parties = new List<MobileParty?>();
                var factionComponents = hero.MapFaction?.WarPartyComponents.Select(p => p?.MobileParty);
                if (factionComponents is not null)
                {
                    parties.AddRange(factionComponents);
                }

                if (hero.Clan?.Kingdom is not null)
                {
                    var kingdomComponents = hero.Clan.Kingdom.WarPartyComponents.Select(p => p?.MobileParty);
                    parties.AddRange(kingdomComponents);
                }

                var partiesInRange = MobileParty.All
                    .Where(m => m?.MapFaction != null
                        && m.GetPosition2D.Distance(hero.PartyBelongedTo.GetPosition2D) <= hero.PartyBelongedTo.SeeingRange * 2f
                        && !m.IsGarrison
                        && !m.IsMilitia
                        && !FactionManager.IsAtWarAgainstFaction(m.MapFaction, hero.MapFaction));
                parties.AddRange(partiesInRange);

                var ordered = parties
                    .DistinctBy(p => p.Id)
                    .OrderByDescending(s => s?.ActualClan?.Equals(hero.Clan))
                    .ThenBy(s => s?.Name?.ToString())
                    .ToList();
                foreach (MobileParty? mobileParty in ordered)
                {
                    if (mobileParty == null)
                    {
                        continue;
                    }

                    PartyAiOrder insert = new(order.Behavior, mobileParty);
                    CharacterObject character = ConversationHelper.GetConversationCharacterPartyLeader(mobileParty.Party);
                    if (character == null)
                    {
                        newList.Add(new InquiryElement(insert, mobileParty.Name?.ToString(), null));
                    }
                    else
                    {
                        newList.Add(new InquiryElement(insert, mobileParty.Name?.ToString(), new CharacterImageIdentifier(CharacterCode.CreateFrom(character))));
                    }
                }
                break;
            case PartyAiOrderType.VisitSettlement:
            case PartyAiOrderType.StayInSettlement:
            case PartyAiOrderType.DefendSettlement:
                List<Settlement> settlements = Settlement.All.Where(s => s.IsFortification || s.IsVillage).Where(s => !FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, s.MapFaction)).ToList();

                foreach (Settlement settlement in settlements.OrderByDescending(s => s.OwnerClan.Equals(_hero?.Clan)).ThenByDescending(s => s.IsTown).ThenByDescending(s => s.IsCastle).ThenBy(s => s.Name.ToString()).ToList())
                {
                    PartyAiOrder insert = new(order.Behavior, settlement);

                    newList.Add(new InquiryElement(insert, settlement.Name.ToString(), new BannerImageIdentifier(settlement.OwnerClan?.Banner, false)));
                }
                break;
            case PartyAiOrderType.PatrolAroundPoint:
                settlements = Settlement.All.Where(s => s.IsFortification || s.IsVillage).ToList();
                newList.Add(new(new PartyAiOrder(PartyAiOrderType.PatrolClanLands), new TextObject("{=PAIb2F6Hyfs}Patrol Clan Territory").ToString(), new BannerImageIdentifier(_hero?.ClanBanner, false)));
                foreach (Settlement settlement in settlements.OrderByDescending(s => s.OwnerClan.Equals(_hero?.Clan)).ThenByDescending(s => s.IsTown).ThenByDescending(s => s.IsCastle).ThenBy(s => s.Name.ToString()).ToList())
                {
                    PartyAiOrder insert = new(order.Behavior, settlement);

                    newList.Add(new InquiryElement(insert, settlement.Name.ToString(), new BannerImageIdentifier(settlement.MapFaction?.Banner, false)));
                }
                break;
            case PartyAiOrderType.BesiegeSettlement:
                foreach (Settlement settlement in Settlement.All.Where(s => FactionManager.IsAtWarAgainstFaction(s.MapFaction, Hero.MainHero.MapFaction) && s.IsFortification).OrderByDescending(s => s.IsTown).ThenBy(s => s.Name.ToString()).ToList())
                {
                    PartyAiOrder insert = new(order.Behavior, settlement);

                    newList.Add(new InquiryElement(insert, settlement.Name.ToString(), new BannerImageIdentifier(settlement.MapFaction?.Banner, false)));
                }
                break;
            default:
                break;
        }

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, null, newList, isExitShown: true, 1, 1, GameTexts.FindText("str_done").ToString(), GameTexts.FindText("str_cancel").ToString(), ChooseTargetCallback, null, null, true));
    }

    private static void ChooseTargetCallback(List<InquiryElement> list)
    {
        if (list.Count == 0)
        {
            return;
        }

        PartyAiOrder order = (PartyAiOrder)list.First().Identifier;
        if (_fallback)
        {
            _settings.SetFallbackOrder(order.Behavior, order.Target);
        }
        else
        {
            if (_settings.HasActiveOrder)
            {
                _settings.OrderQueue.Add(order);
            }
            else
            {
                _settings.SetOrder(order.Behavior, order.Target);
            }
        }

        _onCreateOrder.Invoke();
    }
}

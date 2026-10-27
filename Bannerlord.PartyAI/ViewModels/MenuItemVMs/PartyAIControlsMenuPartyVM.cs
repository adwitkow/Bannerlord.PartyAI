using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.ViewModels.Components;
using Bannerlord.PartyAI.ViewModels.Dialogs;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.View;

namespace Bannerlord.PartyAI.ViewModels.MenuItemVMs;

public class PartyAIControlsMenuPartyVM : ViewModel
{
    internal PartyBase Party;
    internal Hero Leader;
    internal Settlement Settlement;
    private readonly PartyAIControlsMenuVM _menu;
    private ImageIdentifierVM _visual;
    private PartyAICompositionDisplayVM _partyComposition;
    private ImageIdentifierVM _clanVisual;
    private bool _isInspected;
    internal Army Army => Party?.MobileParty?.Army;

    public PartyAIControlsMenuPartyVM(Hero leader, PartyAIControlsMenuVM menu)
    {
        _menu = menu;
        Leader = leader;
        if (Leader.IsPartyLeader)
        {
            Party = Leader.PartyBelongedTo?.Party;
        }

        //CharacterCode characterCode = CampaignUIHelper.GetCharacterCode(_party.LeaderHero.CharacterObject);
        CharacterCode characterCode = CharacterCode.CreateFrom(Leader.CharacterObject);
        Visual = new CharacterImageIdentifierVM(characterCode);
        ClanVisual = new BannerImageIdentifierVM(Leader.Clan.Banner, true);
        AllowEditComposition = true;
        AllowEditTemplate = true;
        EditCompositionHint = new(new TextObject("{=PAIQNUqwt4C}Edit"));
        ChangeTemplateHint = new(new TextObject("{=PAIXIv9UgAt}Change"));
        CopyPasteToggle = new(
            new TextObject(""),
            false,
            new TextObject(""),
            (bool status) =>
        {
            _menu.OnCopyPasteToggle(this, status);
        });
        RefreshValues();
    }

    internal virtual PartyAiEntitySettings Settings => SubModule.PartySettingsManager.Settings(Leader);

    [DataSourceProperty] public virtual string LeaderName => Leader.Name.ToString();
    [DataSourceProperty] public virtual bool ShowPortrait => true;
    [DataSourceProperty] public string TemplateName => Settings.PartyTemplate?.Name?.ToString() ?? new TextObject("{=PATZD6SvrZr}No Template").ToString();
    [DataSourceProperty] public bool IsInArmy => Army != null && !IsArmyLeader;
    [DataSourceProperty] public bool IsArmyLeader => Army?.LeaderParty?.LeaderHero != null && Army.LeaderParty.LeaderHero == Leader;
    [DataSourceProperty] public HintViewModel InArmyHint => new(Army?.Name ?? new TextObject(""));
    [DataSourceProperty] public HintViewModel ArmyLeaderHint => new(new TextObject("{=PAI8qha4sZa}This hero is an army leader. Orders given to their party will apply to their entire army."));
    [DataSourceProperty] public HintViewModel EditCompositionHint { get; set; }
    [DataSourceProperty] public HintViewModel ChangeTemplateHint { get; set; }
    [DataSourceProperty] public virtual bool CanShowLocationOfHero => Leader.IsActive || (Leader.IsPrisoner && Leader.CurrentSettlement != null);
    [DataSourceProperty] public virtual bool IsLordParty => true;
    [DataSourceProperty] public virtual bool IsCaravan => false;
    [DataSourceProperty] public virtual bool IsSettlement => false;
    [DataSourceProperty] public bool AllowEditComposition { get; set; }
    [DataSourceProperty] public bool AllowEditTemplate { get; set; }
    [DataSourceProperty] public HintViewModel ShowOnMapHint => new(new TextObject("{=aGJYQOef}Show hero's location on map."));
    [DataSourceProperty]
    public virtual string ActiveOrder
    {
        get
        {
            var order = SubModule.PartySettingsManager.Settings(Leader)?.Order;
            return OrderVerbalizer.GetStatusText(order).ToString();
        }
    }

    [DataSourceProperty] public PartyAIOptionToggleVM CopyPasteToggle { get; set; }

    [DataSourceProperty]
    public string PartySize => GameTexts.FindText("str_LEFT_over_RIGHT").SetTextVariable("LEFT", Party?.MobileParty.MemberRoster.TotalManCount ?? 0).SetTextVariable("RIGHT", Party?.MobileParty.Party.PartySizeLimit ?? 0).ToString();

    [DataSourceProperty]
    public HintViewModel ClanHint => new(Leader.Clan.Name);

    [DataSourceProperty]
    public string OptionsText => new TextObject("{=PAIQnwbXcqc}Options").ToString();

    [DataSourceProperty]
    public HintViewModel ChangeHint => new(new TextObject("{=PAIXIv9UgAt}Change"));

    [DataSourceProperty]
    public ImageIdentifierVM Visual
    {
        get
        {
            return _visual;
        }
        set
        {
            if (value != _visual)
            {
                _visual = value;
                OnPropertyChangedWithValue(value, "Visual");
            }
        }
    }

    [DataSourceProperty]
    public ImageIdentifierVM ClanVisual
    {
        get
        {
            return _clanVisual;
        }
        set
        {
            if (value != _clanVisual)
            {
                _clanVisual = value;
                OnPropertyChangedWithValue(value, "ClanVisual");
            }
        }
    }


    [DataSourceProperty]
    public PartyAICompositionDisplayVM PartyComposition
    {
        get
        {
            return _partyComposition;
        }
        set
        {
            if (value != _partyComposition)
            {
                _partyComposition = value;
                OnPropertyChangedWithValue(value, "PartyComposition");
            }
        }
    }

    public void EditPartyComposition() => SubModule.InformationManager.ShowPartyCompositionInquiry(Settings, EditPartyCompositionCallback);

    public virtual void EditPartyOptions() => SubModule.InformationManager.ShowPartyOptionsInquiry(Settings, RefreshValues);

    public void EditPartyTemplate()
    {
        AssignTemplateModal.Show(
            Settings,
            onConfirm: () =>
            {
                OnPropertyChangedWithValue(TemplateName, "TemplateName");
                RefreshValues();
            });
    }

    public void OpenOrderQueue()
    {
        SubModule.InformationManager.ShowOrderQueueInquiry(Settings, RefreshValues);
    }

    private void EditPartyCompositionCallback(PartyComposition composition)
    {
        Settings.Composition = composition;
        RefreshValues();
    }

    public virtual void OpenEncyclopediaLink()
    {
        if (Leader != null && Campaign.Current.EncyclopediaManager.GetPageOf(typeof(Hero)).IsValidEncyclopediaItem(Leader))
        {
            Campaign.Current.EncyclopediaManager.GoToLink(Leader.EncyclopediaLink);
        }
    }

    public virtual void ShowHeroOnMap()
    {
        if (Leader == null)
        {
            return;
        }

        // 1.3.9: Hero.GetCampaignPosition() returns CampaignVec2
        CampaignVec2 pos = Leader.GetCampaignPosition();

        Game.Current.GameStateManager.PopState();
        UISoundsHelper.PlayUISound("event:/ui/default");

        // MapScreen now expects CampaignVec2, so just pass it directly
        MapScreen.Instance.FastMoveCameraToPosition(pos);
    }

    public virtual void PartySizeBeginHint()
    {
        if (Party?.MobileParty != null)
        {
            _isInspected = Party.MobileParty.IsInspected;
            Party.MobileParty.IsInspected = true;
            InformationManager.ShowTooltip(typeof(MobileParty), new object[] { Party.MobileParty, false, true });
        }
    }

    public virtual void PartySizeEndHint()
    {
        if (Party?.MobileParty != null)
        {
            InformationManager.HideTooltip();
            Party.MobileParty.IsInspected = _isInspected;
        }
    }

    public override void RefreshValues()
    {
        base.RefreshValues();

        PartyComposition = new PartyAICompositionDisplayVM(Settings.Composition);

        OnPropertyChanged("TemplateName");
        OnPropertyChanged("ActiveOrder");

        Visual.RefreshValues();
    }
}

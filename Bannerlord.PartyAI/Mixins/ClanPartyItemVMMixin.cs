#if LOWER_THAN_1_5
using Bannerlord.PartyAI.CampaignBehaviors;
using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.ViewModels.Components;
using Bannerlord.PartyAI.ViewModels.Dialogs;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Mixins;

[ViewModelMixin(nameof(ClanPartyItemVM.UpdateProperties))]
internal class ClanPartyItemVMMixin : BaseViewModelMixin<ClanPartyItemVM>
{
    private readonly ClanPartyItemVM _vm;
    private PartyAIClanPartySettingsManager _manager => SubModule.PartySettingsManager;
    private static bool _enabled = true;
    private static Hero _cachedHero;
    private readonly int _spacing;
    private PartyAICompositionDisplayVM _partyComposition;
    internal static PartyBase SelectedParty;

    public ClanPartyItemVMMixin(ClanPartyItemVM vm) : base(vm)
    {
        _vm = vm;
        _cachedHero = _vm.Party?.LeaderHero;
        _spacing = 25;

        CreatePartyTemplateText = new TextObject("{=PAYjAC3mQzN}Create").ToString();
        CopyToText = new TextObject("{=PAISIjwQiXw}Copy To").ToString();
        SetPartyTemplateText = new TextObject("{=PAS8COe7Ysl}Set").ToString();
        DeletePartyTemplateText = new TextObject("{=PAQVXq6izAW}DEL").ToString();
        ViewPartyTemplateText = new TextObject("{=PAkCYmU0Qtl}View").ToString();
        EditPartyCompositionText = new TextObject("{=PAHiRSTCnkv}Edit Party Composition").ToString();

        OnRefresh();
    }

    [DataSourceMethod]
    public void EditPartyComposition() => SubModule.InformationManager.ShowPartyCompositionInquiry(_heroSettings, EditPartyCompositionCallback);

    [DataSourceMethod]
    public void EditPartyOptions()
    {
        if (_manager.IsLeadingCaravan(_hero))
        {
            SubModule.InformationManager.ShowCaravanOptionsInquiry(_heroSettings, OnRefresh);
        }
        else if (IsGarrison)
        {
            SubModule.InformationManager.ShowGarrisonOptionsInquiry(_heroSettings, OnRefresh);
        }
        else
        {
            SubModule.InformationManager.ShowPartyOptionsInquiry(_heroSettings, OnRefresh);
        }
    }

    private void EditPartyCompositionCallback(PartyComposition composition)
    {
        _heroSettings.Composition = composition;
        OnRefresh();
    }

    [DataSourceMethod]
    public void EditPartyTemplate()
    {
        SelectTemplate.Select(_heroSettings, OnRefresh);
    }

    internal void HandleSavePartyComposition(PartyComposition composition)
    {
        _heroSettings.Composition = composition;
        PartyComposition = new PartyAICompositionDisplayVM(_heroSettings.Composition, _spacing);
    }

    public override void OnRefresh()
    {
        var activeOrder = _manager.Settings(_hero)?.Order;
        ActiveOrderText = OrderVerbalizer.GetStatusText(activeOrder).ToString();
        PartyComposition = new PartyAICompositionDisplayVM(_heroSettings.Composition, _spacing);

        if (_heroSettings.HasActiveOrder)
        {
            _vm.IsPartyBehaviorEnabled = false;
        }

        string itemName = _heroSettings.PartyTemplate?.Name ?? new TextObject("{=PATZD6SvrZr}No Template").ToString();

        SelectedTemplateText = new TextObject("{=PAhmBRjnrwV}Troop Template: {NAME}").SetTextVariable("NAME", itemName).ToString();
        OnPropertyChangedWithValue(SelectedTemplateText, "SelectedTemplateText");
        OnPropertyChangedWithValue(ActiveOrderText, "ActiveOrderText");
    }

    private Hero _hero => _vm.Party?.LeaderHero;
    private PartyAiEntitySettings _heroSettings => IsGarrison ? _manager.Settings(GarrisonSettlement) : _manager.Settings(_hero);

    private bool IsGarrison => _vm.Party?.MobileParty?.PartyComponent is GarrisonPartyComponent;
    private Settlement GarrisonSettlement => ((GarrisonPartyComponent)_vm.Party?.MobileParty?.PartyComponent)?.Settlement;

    [DataSourceProperty]
    public bool ShowPartyAIControls => IsGarrison ? _manager.IsGarrisonManageable(GarrisonSettlement) : _manager.IsManageable(_hero);

    [DataSourceProperty]
    public bool IsEnabled => _enabled;

    [DataSourceProperty]
    public string CreatePartyTemplateText { get; }

    [DataSourceProperty]
    public string CopyToText { get; }

    [DataSourceProperty]
    public string ActiveOrderText { get; private set; }

    [DataSourceProperty]
    public bool ShowOrder => !IsGarrison && !_manager.IsLeadingCaravan(_hero);

    [DataSourceProperty]
    public string SelectedTemplateText { get; private set; }

    [DataSourceProperty]
    public string ViewPartyTemplateText { get; }

    [DataSourceProperty]
    public string SetPartyTemplateText { get; }

    [DataSourceProperty]
    public string DeletePartyTemplateText { get; }

    [DataSourceProperty]
    public string ToggleShowOptionsText => (_enabled ? new TextObject("{=PA635e8pPkz}Hide") : new TextObject("{=PA08v16flk8}Show")).ToString();

    [DataSourceProperty]
    public string EditPartyCompositionText { get; }

    [DataSourceProperty]
    public string OptionsText => new TextObject("{=PAIQnwbXcqc}Options").ToString();

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

    [DataSourceMethod]
    public void CopyTo()
    {
        if (IsGarrison)
        {
            CopyPaste.CopyGarrisonTo(_heroSettings.Settlement, ClanPartiesVMMixin.Instance._vm.RefreshValues);
            return;
        }
        CopyPaste.CopyTo(_hero, ClanPartiesVMMixin.Instance._vm.RefreshValues);
    }

    [DataSourceMethod]
    public void CreatePartyTemplate()
    {
        SelectedParty = _vm.Party;
        CreateTemplate.Create();
    }

    [DataSourceMethod]
    public void OpenOrderQueue() => SubModule.InformationManager.ShowOrderQueueInquiry(_heroSettings, OnRefresh);

    [DataSourceMethod]
    public void ViewPartyTemplate()
    {
        SelectedParty = _vm.Party;
        ViewTemplate.View();
    }

    [DataSourceMethod]
    public void DeletePartyTemplate() => ViewModels.Dialogs.DeleteTemplate.Delete(new Action(OnRefresh));

    [DataSourceMethod]
    public void ToggleShowOptions()
    {
        _enabled = !_enabled;
        OnPropertyChanged("IsEnabled");
        OnPropertyChanged("ToggleShowOptionsText");
    }
}
#endif
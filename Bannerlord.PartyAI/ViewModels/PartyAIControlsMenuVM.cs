using Bannerlord.PartyAI.ViewModels.Components;
using Bannerlord.PartyAI.ViewModels.Dialogs;
using Bannerlord.PartyAI.ViewModels.Dropdowns;
using Bannerlord.PartyAI.ViewModels.MenuItemVMs;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static Bannerlord.PartyAI.ViewModels.Dropdowns.PartyAISortDirectionDropdownVM;
using static Bannerlord.PartyAI.ViewModels.Dropdowns.PartyAISortDropdownVM;

namespace Bannerlord.PartyAI.ViewModels;

public class PartyAIControlsMenuVM : ViewModel
{
    internal static PartyAIControlsMenuVM Instance;
    private MBBindingList<PartyAIControlsMenuPartyVM> _partyList;
    private HintViewModel _createClanPartyHint;
    private bool _canCreateNewParty;
    private HintViewModel _showAllHeroesHint;
    private bool _showAllHeroes;
    private List<InquiryElement> _copySource = null;

    public PartyAIControlsMenuVM()
    {
        Instance = this;
        PartyList = new MBBindingList<PartyAIControlsMenuPartyVM>();
        CreateClanPartyHint = new HintViewModel();
        ShowAllHeroesHint = new HintViewModel(new TextObject("{=PAIqJ0819Nl}Show all heroes that can lead parties. Useful for assigning settings for any potential hero that might be a leader."));
        SortController = new PartyAISortDropdownVM(OnSortChanged);
        SortDirectionController = new PartyAISortDirectionDropdownVM(OnSortDirectionChanged);
        SortText = new TextObject("{=PAIuPlFS64X}Sort").ToString();
        SelectAllToggle = new(new TextObject(""), false, new TextObject(""), SelectAll);
        SelectAllToggle.IsDisabled = true;

        RefreshPartyList();
    }

    [DataSourceProperty]
    public bool EnablePartyList => PartyList.Count > 0;

    [DataSourceProperty]
    public bool ShowAllHeroes
    {
        get
        {
            return _showAllHeroes;
        }
        set
        {
            if (value != _showAllHeroes)
            {
                _showAllHeroes = value;
                OnPropertyChangedWithValue(value, "ShowAllHeroes");
                RefreshPartyList();
            }
        }
    }

    [DataSourceProperty] public PartyAISortDropdownVM SortController { get; private set; }
    [DataSourceProperty] public PartyAISortDirectionDropdownVM SortDirectionController { get; private set; }
    [DataSourceProperty] public PartyAIOptionToggleVM SelectAllToggle { get; set; }

    [DataSourceProperty] public string SortText { get; private set; }
    [DataSourceProperty] public bool AllowCopy { get; private set; }
    [DataSourceProperty] public bool AllowPaste { get; private set; }
    [DataSourceProperty] public bool CanCancelCopy { get; private set; }

    [DataSourceProperty]
    public bool CanCreateNewParty
    {
        get
        {
            return _canCreateNewParty;
        }
        set
        {
            if (value != _canCreateNewParty)
            {
                _canCreateNewParty = value;
                OnPropertyChangedWithValue(value, "CanCreateNewParty");
            }
        }
    }

    [DataSourceProperty] public HintViewModel CopyHint => new(new("{=PAIY2tmN6Vq}Copy settings from one party to another. Select the checkbox next to a party and press CTRL-C or this button."));
    [DataSourceProperty] public HintViewModel PasteHint => new(new("{=PAIlmndQPWI}Paste settings from one party to another. After copying, select the checkboxes next to all parties you want to paste to and press CTRL-V or this button."));
    [DataSourceProperty] public HintViewModel CancelCopyHint => new(new("{=PAImg9cMpVB}Cancel Copy/Paste operation"));
    [DataSourceProperty] public HintViewModel SelectAllHint => new(new("{=PAIQlzQNwtn}Select All"));

    [DataSourceProperty]
    public string MainHeadingText => new TextObject("{=PAIe2AmH8ga}Party AI Controls").ToString();

    [DataSourceProperty]
    public string CreateTemplateText => new TextObject("{=PAIVTBDYD5s}Create Template").ToString();

    [DataSourceProperty]
    public string DeletePartyTemplateText => new TextObject("{=PAR1D0VvXKZ}Delete Template").ToString();

    [DataSourceProperty]
    public string FineTunePartyTemplateText => new TextObject("{=PAIwK2enPSp}Fine Tune Template").ToString();

    [DataSourceProperty]
    public HintViewModel FineTunePartyTemplateHint => new(new TextObject("{=PAIzjCcuvQw}Select which troops along the upgrade paths you've chosen to be included in the party template. The topmost troop in the list will be the portrait next to the template."));

    [DataSourceProperty]
    public string CopyPasteText => new TextObject("{=PAI3Bf9LMMe}[Unused for now...]").ToString();

    [DataSourceProperty]
    public string ModOptionsText => new TextObject("{=PAIyBVEFgXu}Mod Options").ToString();

    [DataSourceProperty]
    public string CreateClanPartyText => GameTexts.FindText("str_clan_create_new_party").ToString();

    [DataSourceProperty]
    public string EditDefaultSettingsText => new TextObject("{=PAI34RDUeMT}Default Settings").ToString();

    [DataSourceProperty]
    public string DoneText => GameTexts.FindText("str_done").ToString();

    [DataSourceProperty]
    public string ShowAllHeroesText => new TextObject("{=PAIlKT8heH9}Show All Heroes").ToString();

    [DataSourceProperty]
    public MBBindingList<PartyAIControlsMenuPartyVM> PartyList
    {
        get
        {
            return _partyList;
        }
        set
        {
            if (value != _partyList)
            {
                _partyList = value;
                OnPropertyChangedWithValue(value, "PartyList");
            }
        }
    }

    [DataSourceProperty]
    public HintViewModel CreateClanPartyHint
    {
        get
        {
            return _createClanPartyHint;
        }
        set
        {
            if (value != _createClanPartyHint)
            {
                _createClanPartyHint = value;
                OnPropertyChangedWithValue(value, "CreateClanPartyHint");
            }
        }
    }

    [DataSourceProperty]
    public HintViewModel ShowAllHeroesHint
    {
        get
        {
            return _showAllHeroesHint;
        }
        set
        {
            if (value != _showAllHeroesHint)
            {
                _showAllHeroesHint = value;
                OnPropertyChangedWithValue(value, "ShowAllHeroesHint");
            }
        }
    }

    public void ExecuteDone()
    {
        GameStateManager.Current.PopState();
    }

    public void CreatePartyTemplate() => NewTemplateModal.Show();

    public void DeletePartyTemplate() => DeleteTemplateModal.Show(onConfirm: RefreshPartyList);

    public void FineTunePartyTemplate() => FineTuneTemplateModal.Show();

    public void OpenModOptions() => SubModule.InformationManager.ShowModOptionsInquiry(RefreshPartyList);

    public void EditDefaultSettings() => SubModule.InformationManager.ShowDefaultSettingsInquiry(null);

    private void OnNewPartySelectionOver()
    {
        RefreshPartyList();
    }

    public void CreateClanParty()
    {
        new ClanPartiesVM(() => { }, new Action<Hero>(CreateClanPartyCallback), new Action(OnNewPartySelectionOver), (i) => { }).ExecuteCreateNewParty();
    }

    private void CreateClanPartyCallback(Hero hero)
    {
        PartyScreenHelper.OpenScreenAsCreateClanPartyForHero(hero);
    }

    public static void GetManageableHeroes(in List<Hero> list, bool clanOnly, bool showAll)
    {
        foreach (Hero hero in Hero.AllAliveHeroes.Where(l => l != null && l.CanLeadParty() && SubModule.PartySettingsManager.IsManageable(l) && (!clanOnly || l.Clan == Clan.PlayerClan)).ToList())
        {
            if (showAll || (hero.PartyBelongedTo != null && hero.IsPartyLeader))
            {
                if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.IsCaravan)
                {
                    if (SubModule.PartySettingsManager.ManageCaravans)
                    {
                        list.Add(hero);
                    }
                    continue;
                }
                list.Add(hero);
            }
        }
    }

    private void GetManageablePartyVMs(in MBBindingList<PartyAIControlsMenuPartyVM> list, bool clanOnly)
    {
        List<Hero> heroes = new();
        GetManageableHeroes(heroes, clanOnly, ShowAllHeroes);
        foreach (Hero hero in heroes)
        {
            if (hero.PartyBelongedTo?.IsCaravan ?? false)
            {
                list.Add(new PartyAIControlsMenuCaravanVM(hero, this));
            }
            else
            {
                list.Add(new PartyAIControlsMenuPartyVM(hero, this));
            }
        }
        foreach (Settlement settlement in Settlement.All)
        {
            if (SubModule.PartySettingsManager.IsGarrisonManageable(settlement) && settlement?.Town?.GarrisonParty?.Party != null)
            {
                list.Add(new PartyAIControlsMenuSettlementVM(settlement, this));
            }
        }
    }

    public void RefreshPartyList()
    {
        PartyList.Clear();

        GetManageablePartyVMs(PartyList, false);

        OnSortChanged(SortType);

        ClanPartiesVM stockVM = new(() => { }, null, () => { }, (i) => { });
        CanCreateNewParty = stockVM.CanCreateNewParty;
        CreateClanPartyHint.HintText = stockVM.CreateNewPartyActionHint?.HintText ?? new TextObject("");

        OnPropertyChanged("EnablePartyList");
        OnPropertyChanged("PartyList");

        AllowCopy = false;
        AllowPaste = false;
        CanCancelCopy = false;
        _copySource = null;
        OnPropertyChanged("AllowCopy");
        OnPropertyChanged("CanCancelCopy");
        OnPropertyChanged("AllowPaste");
        SelectAllToggle.IsDisabled = true;
        SelectAllToggle.IsSelected = false;
        OnPropertyChanged("SelectAllToggle");

        RefreshValues();
    }

    private void OnSortDirectionChanged(PartySortDirection direction)
    {
        OnSortChanged(SortType);
    }

    private void OnSortChanged(PartySortType sortType)
    {
        List<PartyAIControlsMenuPartyVM> newParties = new();

        switch (sortType)
        {
            case PartySortType.Clan:
                newParties = PartyList.OrderByDescending(p => p.Leader.Clan.Equals(Clan.PlayerClan)).ThenByDescending(p => p.Leader.Clan.Tier).ThenByDescending(p => p.Leader.IsClanLeader).ToList();
                break;
            case PartySortType.Army:
                newParties = PartyList.OrderByDescending(p => p.Army != null).ThenBy(p => p.Army?.Name?.ToString() ?? String.Empty).ThenByDescending(p => p.IsArmyLeader).ToList();
                break;
            case PartySortType.Alphabetical:
                newParties = PartyList.OrderBy(p => (p.IsLordParty || p.IsCaravan) ? p.Leader.Name?.ToString() : (p.Settlement?.Name.ToString()) ?? string.Empty).ToList();
                break;
            case PartySortType.Troops:
                newParties = PartyList.OrderBy(p => p.Party?.NumberOfAllMembers).ToList();
                break;
            case PartySortType.Type:
                newParties = PartyList.OrderByDescending(p => p.IsLordParty).ThenByDescending(p => p.IsCaravan).ThenByDescending(p => p.IsSettlement).ToList();
                break;
            case PartySortType.Template:
                newParties = PartyList.OrderBy(p => p.Settings.PartyTemplate?.Name).ToList();
                break;
            default:
                break;
        }
        if (SortDirection == PartySortDirection.DESC)
        {
            newParties.Reverse();
        }

        PartyList.Clear();
        foreach (PartyAIControlsMenuPartyVM party in newParties)
        {
            PartyList.Add(party);
        }

        OnPropertyChanged("PartyList");
        RefreshValues();
    }

    internal void OnCopyPasteToggle(PartyAIControlsMenuPartyVM vm, bool status)
    {
        AllowCopy = false;
        if (_copySource == null)
        {
            foreach (PartyAIControlsMenuPartyVM item in PartyList)
            {
                if (item != vm)
                {
                    if (status)
                    {
                        AllowCopy = true;
                        CanCancelCopy = true;
                        item.CopyPasteToggle.IsSelected = false;
                        item.CopyPasteToggle.IsDisabled = true;
                    }
                    else
                    {
                        item.CopyPasteToggle.IsSelected = false;
                        item.CopyPasteToggle.IsDisabled = false;
                    }
                }
            }
        }
        OnPropertyChanged("AllowCopy");
    }

    internal void Copy()
    {
        PartyAIControlsMenuPartyVM vm = PartyList.FirstOrDefault(p => p.CopyPasteToggle.IsSelected);
        if (vm == null) { return; }
        if (_copySource != null) { return; }
        ;
        CopyPaste.CopyCallback(vm.Settings, (List<InquiryElement> list) =>
        {
            _copySource = list;
            vm.CopyPasteToggle.IsSelected = false;
            vm.CopyPasteToggle.IsDisabled = true;
            AllowCopy = false;
            AllowPaste = true;

            foreach (PartyAIControlsMenuPartyVM item in PartyList)
            {
                if (item != vm)
                {
                    item.CopyPasteToggle.IsSelected = false;
                    if (item.IsLordParty == vm.IsLordParty && item.IsCaravan == vm.IsCaravan && item.IsSettlement == vm.IsSettlement)
                    {
                        item.CopyPasteToggle.IsDisabled = false;
                    }
                    else
                    {
                        item.CopyPasteToggle.IsDisabled = true;
                    }
                }
            }

            OnPropertyChanged("AllowCopy");
            OnPropertyChanged("CanCancelCopy");
            OnPropertyChanged("AllowPaste");
            SelectAllToggle.IsDisabled = false;
            OnPropertyChanged("SelectAllToggle");
        });
    }

    internal void Paste()
    {
        if (_copySource == null) { return; }
        IEnumerable<PartyAIControlsMenuPartyVM> targets = PartyList.Where(p => p.CopyPasteToggle.IsSelected);
        if (targets.Count() == 0) { return; }
        ;
        foreach (PartyAIControlsMenuPartyVM target in targets)
        {
            foreach (InquiryElement source in _copySource)
            {
                CopyPaste.CopySettings(target.Settings, source);
            }
        }
        _copySource = null;
        AllowPaste = false;
        CanCancelCopy = false;
        OnPropertyChanged("AllowPaste");
        OnPropertyChanged("CanCancelCopy");
        SelectAllToggle.IsDisabled = true;
        OnPropertyChanged("SelectAllToggle");
        RefreshPartyList();
    }

    private void SelectAll(bool selected)
    {
        foreach (PartyAIControlsMenuPartyVM p in PartyList)
        {
            if (!p.CopyPasteToggle.IsDisabled)
            {
                p.CopyPasteToggle.IsSelected = selected;
            }
        }
    }

    public override void RefreshValues()
    {
        base.RefreshValues();

        foreach (PartyAIControlsMenuPartyVM item in _partyList)
        {
            item.RefreshValues();
        }
    }
}

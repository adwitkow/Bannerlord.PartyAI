using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.ViewModels.Components;
using Bannerlord.PartyAI.ViewModels.Dialogs;
using System;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels;

public class PartyAIDefaultSettingsVM : ViewModel
{
    public enum OptionsType
    {
        Party,
        Caravan,
        Garrison
    }
    public class DefaultSettingsItemVM : ViewModel
    {
        private readonly PartyAiEntitySettings _settings;
        private readonly TextObject _groupNametext;
        private PartyAICompositionDisplayVM _composition;
        private readonly OptionsType _optionsType;

        public DefaultSettingsItemVM(PartyAiEntitySettings settings, TextObject name, OptionsType optionsType)
        {
            _settings = settings;
            _groupNametext = name;
            _optionsType = optionsType;

            RefreshValues();
        }

        [DataSourceProperty]
        public PartyAICompositionDisplayVM Composition
        {
            get
            {
                return _composition;
            }
            set
            {
                if (value != _composition)
                {
                    _composition = value;
                    OnPropertyChangedWithValue(value, "Composition");
                }
            }
        }

        public void EditComposition()
        {
            SubModule.InformationManager.ShowPartyCompositionInquiry(_settings, (PartyComposition composition) =>
            {
                _settings.Composition = composition;
                RefreshValues();
            });
        }

        public void EditPartyTemplate() => AssignTemplateModal.Show(_settings, onConfirm: RefreshValues);

        public void EditPartyOptions()
        {
            switch (_optionsType)
            {
                case OptionsType.Party:
                    SubModule.InformationManager.ShowPartyOptionsInquiry(_settings, RefreshValues);
                    break;
                case OptionsType.Caravan:
                    SubModule.InformationManager.ShowCaravanOptionsInquiry(_settings, RefreshValues);
                    break;
                case OptionsType.Garrison:
                    SubModule.InformationManager.ShowGarrisonOptionsInquiry(_settings, RefreshValues);
                    break;
                default:
                    break;
            }
        }

        [DataSourceProperty] public string GroupNameText => _groupNametext.ToString();

        [DataSourceProperty] public string TemplateName => _settings.PartyTemplate?.Name?.ToString() ?? new TextObject("{=PATZD6SvrZr}No Template").ToString();

        [DataSourceProperty] public HintViewModel EditHint => new(new TextObject("{=PAIQNUqwt4C}Edit"));

        [DataSourceProperty] public HintViewModel ChangeHint => new(new TextObject("{=PAIXIv9UgAt}Change"));

        [DataSourceProperty] public string OptionsText => new TextObject("{=PAIQnwbXcqc}Options").ToString();

        public override void RefreshValues()
        {
            base.RefreshValues();
            Composition = new(_settings.Composition);
            OnPropertyChanged("TemplateName");
        }
    }

    private MBBindingList<DefaultSettingsItemVM> _itemList;
    private readonly PartyAiEntitySettings _defaultClanPartySettings;
    private readonly PartyAiEntitySettings _defaultClanCaravanSettings;
    private readonly PartyAiEntitySettings _defaultClanGarrisonSettings;
    private readonly PartyAiEntitySettings _defaultKingdomPartySettings;
    private readonly PartyAiEntitySettings _defaultKingdomGarrisonSettings;
    private readonly Action _onCloseDefaultSettings;

    public PartyAIDefaultSettingsVM(Action callback)
    {
        TitleText = new TextObject("{=PAIykz3Pc1F}Edit Default Settings").ToString();

        _defaultClanPartySettings = new PartyAiEntitySettings(SubModule.PartySettingsManager._defaultClanPartySettings);
        _defaultClanCaravanSettings = new PartyAiEntitySettings(SubModule.PartySettingsManager._defaultClanCaravanSettings);
        _defaultClanGarrisonSettings = new PartyAiEntitySettings(SubModule.PartySettingsManager._defaultClanGarrisonSettings);
        _defaultKingdomPartySettings = new PartyAiEntitySettings(SubModule.PartySettingsManager._defaultKingdomPartySettings);
        _defaultKingdomGarrisonSettings = new PartyAiEntitySettings(SubModule.PartySettingsManager._defaultKingdomGarrisonSettings);

        ItemList = new()
  {
    new DefaultSettingsItemVM(_defaultClanPartySettings, new TextObject("{=PAIOMxOAsTY}Clan Parties"), OptionsType.Party),
    new DefaultSettingsItemVM(_defaultClanCaravanSettings, new TextObject("{=PAId8ZsX3ID}Clan Caravans"), OptionsType.Caravan),
    new DefaultSettingsItemVM(_defaultClanGarrisonSettings, new TextObject("{=PAIKf5y8Z4K}Clan Garrisons"), OptionsType.Garrison),
    new DefaultSettingsItemVM(_defaultKingdomPartySettings, new TextObject("{=PAIObdiWWBa}Kingdom Parties"), OptionsType.Party),
    new DefaultSettingsItemVM(_defaultKingdomGarrisonSettings, new TextObject("{=PAIJkUlgNUw}Kingdom Garrisons"), OptionsType.Garrison),
  };

        _onCloseDefaultSettings = callback;

        RefreshValues();
    }

    [DataSourceProperty] public string AcceptText => new TextObject("{=bV75iwKa}Save").ToString();

    [DataSourceProperty] public string CancelText => GameTexts.FindText("str_cancel").ToString();

    [DataSourceProperty] public string TitleText { get; private set; }

    [DataSourceProperty]
    public MBBindingList<DefaultSettingsItemVM> ItemList
    {
        get
        {
            return _itemList;
        }
        set
        {
            if (value != _itemList)
            {
                _itemList = value;
                OnPropertyChangedWithValue(value, "ItemList");
            }
        }
    }

    public override void RefreshValues()
    {
        base.RefreshValues();

        foreach (DefaultSettingsItemVM item in ItemList)
        {
            item.RefreshValues();
        }
    }

    public void AcceptEditDefaultSettings()
    {
        SubModule.PartySettingsManager._defaultClanPartySettings = _defaultClanPartySettings;
        SubModule.PartySettingsManager._defaultClanCaravanSettings = _defaultClanCaravanSettings;
        SubModule.PartySettingsManager._defaultClanGarrisonSettings = _defaultClanGarrisonSettings;
        SubModule.PartySettingsManager._defaultKingdomPartySettings = _defaultKingdomPartySettings;
        SubModule.PartySettingsManager._defaultKingdomGarrisonSettings = _defaultKingdomGarrisonSettings;

        _onCloseDefaultSettings?.Invoke();
    }

    public void CancelEditDefaultSettings()
    {
        _onCloseDefaultSettings?.Invoke();
    }
}

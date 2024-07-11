using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Yafc.Model;

namespace Yafc.ViewModels;

internal class PreferencesViewModel(ProjectPreferences preferences, ProjectSettings settings) : ViewModelBase {
    public bool IsSeconds {
        get => preferences.time == 1;
        set {
            if (value) {
                TimeAmount = 1;
            }
        }
    }
    public bool IsMinutes {
        get => preferences.time == 60;
        set {
            if (value) {
                TimeAmount = 60;
            }
        }
    }
    public bool IsHours {
        get => preferences.time == 3600;
        set {
            if (value) {
                TimeAmount = 3600;
            }
        }
    }
    public bool IsCustom {
        get => preferences.time is not 1 and not 60 and not 3600;
        set { }
    }
    public int TimeAmount {
        get => preferences.time;
        set {
            if (value != preferences.time) {
                this.RaisePropertyChanging();
                this.RaisePropertyChanging(nameof(IsSeconds));
                this.RaisePropertyChanging(nameof(IsMinutes));
                this.RaisePropertyChanging(nameof(IsHours));
                this.RaisePropertyChanging(nameof(IsCustom));
                this.RaisePropertyChanging(nameof(SimpleItemText));
                this.RaisePropertyChanging(nameof(SimpleFluidText));
                preferences.RecordUndo(true).time = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(IsSeconds));
                this.RaisePropertyChanged(nameof(IsMinutes));
                this.RaisePropertyChanged(nameof(IsHours));
                this.RaisePropertyChanged(nameof(IsCustom));
                this.RaisePropertyChanged(nameof(SimpleItemText));
                this.RaisePropertyChanged(nameof(SimpleFluidText));
            }
        }
    }

    public string SimpleItemText => "Simple Amount" + preferences.GetPerTimeUnit().suffix;
    public bool SimpleItem {
        get => preferences.itemUnit == 0;
        set {
            if (value) {
                ItemUnit = 0;
            }
        }
    }
    public float ItemUnit {
        get => preferences.itemUnit;
        set {
            if (value != preferences.itemUnit) {
                this.RaisePropertyChanging();
                this.RaisePropertyChanging(nameof(SimpleItem));
                this.RaisePropertyChanging(nameof(SetProductionFromBelt));
                preferences.RecordUndo(true).itemUnit = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(SimpleItem));
                this.RaisePropertyChanged(nameof(SetProductionFromBelt));
            }
        }
    }
    public EntityBelt? SetProductionFromBelt {
        get => null;
        set {
            if (value != null) {
                preferences.RecordUndo(true).itemUnit = value.beltItemsPerSecond;
            }
        }
    }

    public string SimpleFluidText => "Simple Amount" + preferences.GetPerTimeUnit().suffix;
    public bool SimpleFluid {
        get => preferences.fluidUnit == 0;
        set {
            if (value) {
                FluidUnit = 0;
            }
        }
    }
    public float FluidUnit {
        get => preferences.fluidUnit;
        set {
            if (value != preferences.fluidUnit) {
                this.RaisePropertyChanging();
                this.RaisePropertyChanging(nameof(SimpleFluid));
                preferences.RecordUndo(true).fluidUnit = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(SimpleFluid));
            }
        }
    }

    public string PollutionCostModifier {
        get => DataUtils.FormatAmount(settings.PollutionCostModifier, UnitOfMeasure.Percent);
        set {
            if (DataUtils.TryParseAmount(value, out float percent, UnitOfMeasure.Percent) && settings.PollutionCostModifier != percent) {
                this.RaisePropertyChanging();
                settings.RecordUndo().PollutionCostModifier = percent;
                this.RaisePropertyChanged();
            }
        }
    }

    public string IconScale {
        get => DataUtils.FormatAmount(preferences.iconScale, UnitOfMeasure.Percent);
        set {
            if (DataUtils.TryParseAmount(value, out float percent, UnitOfMeasure.Percent) && preferences.iconScale != percent) {
                this.RaisePropertyChanging();
                preferences.RecordUndo().iconScale = percent;
                this.RaisePropertyChanged();
            }
        }
    }

    public EntityBelt? DefaultBelt {
        get => preferences.defaultBelt;
        set {
            if (preferences.defaultBelt != value) {
                this.RaisePropertyChanging();
                preferences.RecordUndo().defaultBelt = value;
                this.RaisePropertyChanged();
            }
        }
    }
    public EntityInserter? DefaultInserter {
        get => preferences.defaultInserter;
        set {
            if (preferences.defaultInserter != value) {
                this.RaisePropertyChanging();
                preferences.RecordUndo(true).defaultInserter = value;
                this.RaisePropertyChanged();
            }
        }
    }
    public int InserterCapacity {
        get => preferences.inserterCapacity;
        set {
            if (preferences.inserterCapacity != value) {
                this.RaisePropertyChanging();
                preferences.RecordUndo().inserterCapacity = value;
                this.RaisePropertyChanged();
            }
        }
    }
    public string ReactorLayout {
        get => settings.reactorSizeX + "x" + settings.reactorSizeY;
        set {
            string[] values = value.Split('x');
            if (values.Length == 1 && int.TryParse(values[0], out int size)) {
                this.RaisePropertyChanging();
                settings.RecordUndo().reactorSizeX = size;
                settings.reactorSizeY = size;
                this.RaisePropertyChanged();
            }
            else if (values.Length == 2 && int.TryParse(values[0], out int x) && int.TryParse(values[1], out int y)) {
                this.RaisePropertyChanging();
                settings.RecordUndo().reactorSizeX = x;
                settings.reactorSizeY = y;
                this.RaisePropertyChanged();
            }
        }
    }

    public Technology? TargetTechnologyForCostAnalysis {
        get => preferences.targetTechnology;
        set {
            if (preferences.targetTechnology != value) {
                this.RaisePropertyChanging();
                preferences.RecordUndo(true).targetTechnology = value;
                this.RaisePropertyChanged();
            }
        }
    }

#pragma warning disable CA1822 // Mark members as static
    public EntityBelt[] Belts => Database.allBelts;
    public EntityInserter[] Inserters => Database.allInserters;
#pragma warning restore CA1822 // Mark members as static
}

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using DynamicData;
using ReactiveUI;
using Yafc.Model;
using Yafc.UI;

namespace Yafc.ViewModels;

internal class SearchableScrollListViewModel : ViewModelBase {
    private List<FactorioObject> allObjects;
    private readonly bool includeNone;
    private SearchQuery searchQuery;

    public SearchableScrollListViewModel(IEnumerable<FactorioObject> objects, bool includeNone) {
        allObjects = objects.ToList();
        this.includeNone = includeNone;
        Query = "";
    }

    public string Query {
        get => searchQuery.query;
        [MemberNotNull(nameof(DisplayObjects))]
        set {
            if (searchQuery.query != value || DisplayObjects == null) {
                RunQuery(value);
            }
        }
    }

    [MemberNotNull(nameof(DisplayObjects))]
    private void RunQuery(string value) {
        this.RaisePropertyChanging();
        this.RaisePropertyChanging(nameof(DisplayObjects));
        this.RaisePropertyChanging(nameof(RequiredHeight));
        searchQuery = new SearchQuery(value);
        List<SearchableScrollListItem> displayObjects = allObjects.Where(o => o.Match(searchQuery)).Select(o => new SearchableScrollListItem(o, this)).ToList();
        if (includeNone) {
            displayObjects.Insert(0, new(null, this));
        }
        DisplayObjects = displayObjects.AsReadOnly();
        RequiredHeight = (int)(Math.Ceiling(DisplayObjects.Count / 11f)) * 30;
        this.RaisePropertyChanged();
        this.RaisePropertyChanged(nameof(DisplayObjects));
        this.RaisePropertyChanged(nameof(RequiredHeight));
    }

    public int RequiredHeight { get; set; }

    public IReadOnlyList<SearchableScrollListItem> DisplayObjects { get; private set; }

    public void SetObjects(IEnumerable<FactorioObject> objects) {
        allObjects = objects.ToList();
        DisplayObjects = null!; //null-forgiving: We immediately reset this.
        RunQuery(Query);
    }
}

internal class SearchableScrollListItem : ViewModelBase {
    public Image Icon { get; }
    public int TopX { get; }
    public int TopY { get; }

    public SearchableScrollListItem(FactorioObject? obj, SearchableScrollListViewModel viewModel) {
        Icon = (Image)new Parser.SdlToAvaloniaIconConverter().Convert(obj, typeof(Image), null, CultureInfo.InvariantCulture)!;
        TopX = viewModel.DisplayObjects.IndexOf(this) / 11 * 30;
        TopY = viewModel.DisplayObjects.IndexOf(this) % 11 * 30;
    }
}

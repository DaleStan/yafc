using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Yafc.Model;
using Yafc.UI;

namespace Yafc.ViewModels;

internal class SelectSingleObjectViewModel : ViewModelBase {
    private readonly IEnumerable<FactorioObject> objects;
    private readonly bool allowNone;
    private SearchQuery searchQuery;

    public SelectSingleObjectViewModel(IEnumerable<FactorioObject> objects, bool allowNone) {
        this.objects = objects;
        this.allowNone = allowNone;
        Query = "";
    }

    public string Query {
        get => searchQuery.query;
        [MemberNotNull(nameof(Objects))]
        set {
            value ??= "";
            if (searchQuery.query != value || Objects == null) {
                this.RaisePropertyChanging();
                this.RaisePropertyChanging(nameof(Objects));
                searchQuery = new SearchQuery(value);
                Objects = objects.Where(o => o.Match(searchQuery)).ToList()!;
                if (allowNone) {
                    Objects.Insert(0, null);
                }
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(Objects));
            }
        }
    }

    public List<FactorioObject?> Objects { get; set; }

}

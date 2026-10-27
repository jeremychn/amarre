using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Remotely.Server.Components.Scripts;
using Remotely.Server.Components.TreeView;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using System.Collections;

namespace Remotely.Server.Components.Pages;

[Authorize]
public partial class ScriptsPage : AuthComponentBase
{
    private readonly List<ScriptTreeNode> _treeNodes = new();
    private IEnumerable<SavedScript> _allScripts = Enumerable.Empty<SavedScript>();
    private bool _showOnlyMyScripts = true;
    private string _searchText = string.Empty;

    [Parameter]
    public string? ActiveTab { get; set; }

    public bool ShowOnlyMyScripts
    {
        get => _showOnlyMyScripts;
        set
        {
            _showOnlyMyScripts = value;
            _treeNodes.Clear();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value ?? string.Empty;
            _treeNodes.Clear();
        }
    }

    public IEnumerable<ScriptTreeNode> TreeNodes
    {
        get
        {
            if (_treeNodes.Any() == true)
            {
                return _treeNodes;
            }

            RefreshTreeNodes();

            return _treeNodes;
        }
    }

    [Inject]
    private IDataService DataService { get; init; } = null!;

    public string GetItemIconCss(ScriptTreeNode viewModel)
    {
        if (viewModel.ItemType == TreeItemType.Folder)
        {
            return "oi oi-folder text-warning";
        }
        return "oi oi-script text-success";
    }

    public async Task RefreshScripts()
    {
        EnsureUserSet();

        _treeNodes.Clear();

        _allScripts = await DataService.GetSavedScriptsWithoutContent(User.Id, User.OrganizationID);
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await RefreshScripts();
    }


    private void CreateTreeNode(SavedScript script)
    {
        var root = _treeNodes;
        ScriptTreeNode? targetParent = null;

        if (!string.IsNullOrWhiteSpace(script.FolderPath))
        {
            var paths = script.FolderPath.Split("/", StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < paths.Length; i++)
            {
                var existingParent = root.Find(x => x.Name == paths[i]);

                if (existingParent is null)
                {
                    var newItem = new ScriptTreeNode()
                    {
                        Name = paths[i],
                        ItemType = TreeItemType.Folder,
                        ParentNode = existingParent
                    };
                    root.Add(newItem);
                    root = newItem.ChildItems;
                    targetParent = newItem;
                }
                else
                {
                    root = existingParent.ChildItems;
                    targetParent = existingParent;
                }
            }
        }

        var scriptNode = new ScriptTreeNode()
        {
            Name = script.Name,
            Script = script,
            ItemType = TreeItemType.Item,
            ParentNode = targetParent
        };

        root.Add(scriptNode);
    }

    private void RefreshTreeNodes()
    {
        if (User is null)
        {
            return;
        }

        EnsureUserSet();

        _treeNodes.Clear();

        foreach (var script in _allScripts)
        {
            var showScript = ShowOnlyMyScripts ?
                script.CreatorId == User.Id :
                script.CreatorId == User.Id || script.IsPublic;

            if (!showScript)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                CreateTreeNode(script);
                continue;
            }

            // Recherche : liste à plat (dossier/nom), les dossiers repliés masqueraient les résultats.
            var fullName = string.IsNullOrWhiteSpace(script.FolderPath) ?
                script.Name :
                $"{script.FolderPath.Trim('/')}/{script.Name}";

            if (fullName.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _treeNodes.Add(new ScriptTreeNode()
                {
                    Name = fullName,
                    Script = script,
                    ItemType = TreeItemType.Item
                });
            }
        }

        _treeNodes.Sort((a, b) =>
        {
            if (a.ItemType != b.ItemType)
            {
                return Comparer.Default.Compare(a.ItemType, b.ItemType);
            }

            return Comparer.Default.Compare(a.Name, b.Name);
        });
    }
}

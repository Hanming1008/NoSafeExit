using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class ShelterTraderStation : MonoBehaviour
{
    [Header("Trader")]
    [SerializeField] private string traderDisplayName = "Trader";
    [SerializeField] private string promptText = "Open Trader";
    [Min(0.25f)] [SerializeField] private float interactionRadius = 2.5f;
    [SerializeField] private Vector3 playerCheckOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField] private List<ItemDefinition> stockItems = new List<ItemDefinition>();
    [SerializeField] private ItemDefinition currencyItem;

    private Transform player;
    private ShelterStashStation stashStation;
    private TraderShopUI shopUI;
    private bool playerNear;

    public string TraderDisplayName => string.IsNullOrWhiteSpace(traderDisplayName) ? gameObject.name : traderDisplayName;
    public string PromptText => string.IsNullOrWhiteSpace(promptText) ? "Open Trader" : promptText;
    public bool IsPlayerNear => playerNear;
    public GameObject BoundsRoot => gameObject;
    public Transform FallbackTransform => transform;
    public IReadOnlyList<ItemDefinition> StockItems => stockItems;
    public ItemDefinition CurrencyItem => currencyItem;
    public ShelterStashStation StashStation => stashStation;

    void Awake()
    {
        EnsureCatalog();
        ResolveReferences();
    }

    void Update()
    {
        ResolveReferences();
        EnsureCatalog();

        playerNear = IsPlayerWithinRange();
        if (!playerNear || shopUI == null)
            return;

        if (Input.GetKeyDown(KeyCode.F))
            shopUI.Open(this);
    }

    void OnValidate()
    {
        interactionRadius = Mathf.Max(0.25f, interactionRadius);
    }

    public void GetHighlightRoots(List<GameObject> roots)
    {
        if (roots == null)
            return;

        roots.Add(gameObject);
    }

    public void EnsureCatalog()
    {
        if (currencyItem == null)
            currencyItem = FindCurrencyItemInLoadedCatalog();

        if (stockItems != null && stockItems.Count > 0)
        {
            RemoveInvalidStockEntries();
            return;
        }

#if UNITY_EDITOR
        PopulateCatalogFromProjectAssets();
#endif
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
                player = taggedPlayer.transform;
        }

        if (player == null)
        {
            PlayerStats stats = FindFirstObjectByType<PlayerStats>(FindObjectsInactive.Exclude);
            if (stats != null)
                player = stats.transform;
        }

        if (stashStation == null)
            stashStation = FindFirstObjectByType<ShelterStashStation>(FindObjectsInactive.Include);

        if (shopUI == null)
            shopUI = FindFirstObjectByType<TraderShopUI>(FindObjectsInactive.Include);

        if (shopUI == null)
        {
            GameObject uiObject = new GameObject("Trader Shop UI");
            shopUI = uiObject.AddComponent<TraderShopUI>();
        }
    }

    private bool IsPlayerWithinRange()
    {
        if (player == null)
            return false;

        Vector3 center = player.TransformPoint(playerCheckOffset);
        float distanceSqr = (transform.position - center).sqrMagnitude;
        return distanceSqr <= interactionRadius * interactionRadius;
    }

    private ItemDefinition FindCurrencyItemInLoadedCatalog()
    {
        if (stockItems == null)
            return null;

        for (int i = 0; i < stockItems.Count; i++)
        {
            ItemDefinition item = stockItems[i];
            if (item != null && item.Type == ItemType.Currency)
                return item;
        }

        return null;
    }

    private void RemoveInvalidStockEntries()
    {
        for (int i = stockItems.Count - 1; i >= 0; i--)
        {
            ItemDefinition item = stockItems[i];
            if (item == null || item == currencyItem || item.Type == ItemType.Currency || item.Type == ItemType.Loot || IsDebugItem(item))
                stockItems.RemoveAt(i);
        }
    }

    private static bool IsDebugItem(ItemDefinition item)
    {
        if (item == null)
            return true;

        return (!string.IsNullOrWhiteSpace(item.itemId) && item.itemId.ToLowerInvariant().Contains("debug"))
            || item.name.ToLowerInvariant().Contains("debug");
    }

#if UNITY_EDITOR
    private void PopulateCatalogFromProjectAssets()
    {
        stockItems ??= new List<ItemDefinition>();
        stockItems.Clear();

        string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Data/Items" });
        List<ItemDefinition> discovered = new List<ItemDefinition>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
                continue;

            if (item.Type == ItemType.Currency)
            {
                if (currencyItem == null)
                    currencyItem = item;
                continue;
            }

            if (item.Type == ItemType.Loot)
                continue;

            if (IsDebugItem(item) || path.Contains("/Debug/"))
                continue;

            discovered.Add(item);
        }

        discovered.Sort((a, b) =>
        {
            int typeCompare = a.Type.CompareTo(b.Type);
            if (typeCompare != 0)
                return typeCompare;

            return string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase);
        });

        stockItems.AddRange(discovered);

        if (!Application.isPlaying)
            EditorUtility.SetDirty(this);
    }
#endif
}

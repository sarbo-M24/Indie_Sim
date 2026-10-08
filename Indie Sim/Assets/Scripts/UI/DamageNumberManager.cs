using UnityEngine;

/// <summary>
/// Spawner for floating damage numbers: call
/// DamageNumberManager.Instance.Spawn(worldPos, damage) from wherever damage lands.
/// Creates itself on first use (persistent, popup prefab from
/// Resources/DamageNumber), so no scene needs an object for it. A scene
/// copy with its own popupPrefab still works and takes over if it's first.
/// </summary>
public class DamageNumberManager : MonoBehaviour
{
    private const string PopupResourcePath = "DamageNumber";

    private static DamageNumberManager _instance;

    public static DamageNumberManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[DamageNumberManager]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<DamageNumberManager>();
            }
            return _instance;
        }
    }

    [SerializeField] private DamageNumberPopup popupPrefab;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (popupPrefab == null) popupPrefab = Resources.Load<DamageNumberPopup>(PopupResourcePath);
        if (popupPrefab == null) Debug.LogError($"[DamageNumberManager] No DamageNumberPopup at Resources/{PopupResourcePath}.");
    }

    public void Spawn(Vector3 worldPosition, int damage, bool isCrit = false)
    {
        if (popupPrefab == null || damage <= 0 || !SettingsService.Current.damageNumbers) return;

        DamageNumberPopup popup = Instantiate(popupPrefab, worldPosition, Quaternion.identity);
        popup.Initialize(damage, isCrit);
    }
}

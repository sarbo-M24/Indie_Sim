using UnityEngine;

/// <summary>
/// Scene-level spawner for floating damage numbers. Put one of these in the scene
/// with a DamageNumberPopup prefab assigned, then call
/// DamageNumberManager.Instance.Spawn(worldPos, damage) from wherever damage lands.
/// </summary>
public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance { get; private set; }

    [SerializeField] private DamageNumberPopup popupPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Spawn(Vector3 worldPosition, int damage, bool isCrit = false)
    {
        if (popupPrefab == null || damage <= 0) return;

        DamageNumberPopup popup = Instantiate(popupPrefab, worldPosition, Quaternion.identity);
        popup.Initialize(damage, isCrit);
    }
}

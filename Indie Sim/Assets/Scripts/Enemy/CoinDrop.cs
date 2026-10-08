using UnityEngine;

/// <summary>
/// Inspector-configurable coin drop for enemies that pay out on death
/// (ranged + Cthulhu eye always; fodder only on a low dropChance).
/// </summary>
[System.Serializable]
public class CoinDrop
{
    public GameObject coinPrefab;
    [Tooltip("Chance (0-1) that this death drops any coins at all.")]
    [Range(0f, 1f)] public float dropChance = 1f;
    public int minCoins = 1;
    public int maxCoins = 3;
    [Tooltip("How much force to apply to coins (makes them bounce).")]
    public float coinDropForce = 3f;
    [Tooltip("How spread out the coins spawn.")]
    public float coinSpreadRadius = 0.5f;

    /// <summary>Spawns a random number of coins around position with a little pop.</summary>
    public void Drop(Vector3 position)
    {
        if (coinPrefab == null)
        {
            Debug.LogWarning("[CoinDrop] No coin prefab assigned. Skipping coin drop.");
            return;
        }

        if (Random.value >= dropChance) return;

        int coinCount = Random.Range(minCoins, maxCoins + 1); // +1 because max is exclusive

        for (int i = 0; i < coinCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * coinSpreadRadius;
            GameObject coin = Object.Instantiate(coinPrefab, position + (Vector3)randomOffset, Quaternion.identity);

            Rigidbody2D coinRb = coin.GetComponent<Rigidbody2D>();
            if (coinRb != null)
            {
                Vector2 randomForce = new Vector2(
                    Random.Range(-coinDropForce, coinDropForce),
                    Random.Range(coinDropForce * 0.5f, coinDropForce));
                coinRb.AddForce(randomForce, ForceMode2D.Impulse);
                coinRb.AddTorque(Random.Range(-5f, 5f), ForceMode2D.Impulse);
            }
        }
    }
}

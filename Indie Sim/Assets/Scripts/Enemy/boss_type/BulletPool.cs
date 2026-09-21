using System.Collections.Generic;
using UnityEngine;

public class BulletPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int initialPoolSize = 100;
    [SerializeField] private int maxPoolSize = 300;

    private Queue<GameObject> availableBullets = new Queue<GameObject>();
    private HashSet<GameObject> activeBullets = new HashSet<GameObject>();
    private Transform poolParent;

    private void Awake()
    {
        poolParent = new GameObject("ActiveBullets").transform;
        poolParent.SetParent(transform);

        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateBullet();
        }

        Debug.Log($"Bullet pool initialized with {initialPoolSize} bullets");
    }

    private GameObject CreateBullet()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError("Bullet prefab not assigned to BulletPool!");
            return null;
        }

        GameObject bullet = Instantiate(bulletPrefab, poolParent);
        bullet.SetActive(false);
        availableBullets.Enqueue(bullet);
        return bullet;
    }

    public GameObject SpawnBullet(Vector3 position, Vector2 velocity, int damage, float lifetime,
        LayerMask? damageableLayersOverride = null, LayerMask? destructionLayersOverride = null)
    {
        GameObject bullet;

        if (availableBullets.Count > 0)
        {
            bullet = availableBullets.Dequeue();
        }
        else if (activeBullets.Count < maxPoolSize)
        {
            bullet = CreateBullet();
            if (bullet == null) return null;
        }
        else
        {
            Debug.LogWarning("Bullet pool exhausted!");
            return null;
        }

        bullet.transform.SetParent(null); 
        bullet.transform.position = position;
        bullet.transform.rotation = Quaternion.identity;
        bullet.SetActive(true);

        // Don't use Rigidbody2D velocity - bullets move themselves in Update()
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic; // Make sure it's kinematic
            rb.linearVelocity = Vector2.zero; // Clear any physics velocity
        }

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.Initialize(damage, lifetime, this, velocity, damageableLayersOverride, destructionLayersOverride);
        }

        activeBullets.Add(bullet);
        return bullet;
    }

    public void ReturnBullet(GameObject bullet)
    {
        if (bullet == null) return;

        if (!activeBullets.Contains(bullet))
        {
            return;
        }

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        bullet.transform.SetParent(poolParent);
        bullet.SetActive(false);

        activeBullets.Remove(bullet);
        availableBullets.Enqueue(bullet);
    }

    public int GetActiveCount() => activeBullets.Count;
    public int GetAvailableCount() => availableBullets.Count;
}
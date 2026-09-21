using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// A single floating damage number. Attach to a prefab with a TextMeshPro (3D, not UGUI)
/// component so it can be instantiated directly in world space without a Canvas.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class DamageNumberPopup : MonoBehaviour
{
    [Header("Motion")]
    [SerializeField] private float riseDistance = 1f;
    [SerializeField] private float lifetime = 0.7f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.Linear(0, 1, 1, 0);

    [Header("Random Spread")]
    [Tooltip("Random horizontal offset so overlapping hits don't stack exactly on top of each other.")]
    [SerializeField] private float horizontalJitter = 0.3f;

    [Header("Crit")]
    [SerializeField] private Color critColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private float critScale = 1.4f;

    [Header("Sorting")]
    [Tooltip("TextMeshPro renders through a MeshRenderer, which isn't exposed on the TMP component itself — set it here so the number draws above floor/ground sprites instead of using whatever the prefab defaulted to.")]
    [SerializeField] private string sortingLayerName = "UI";
    [SerializeField] private int sortingOrder = 100;

    private TextMeshPro text;
    private Vector3 startPos;

    private void Awake()
    {
        text = GetComponent<TextMeshPro>();

        Renderer meshRenderer = GetComponent<Renderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;
        }
    }

    public void Initialize(int damage, bool isCrit = false)
    {
        text.text = isCrit ? $"{damage}!" : damage.ToString();
        if (isCrit)
        {
            text.color = critColor;
            transform.localScale *= critScale;
        }

        startPos = transform.position + new Vector3(Random.Range(-horizontalJitter, horizontalJitter), 0f, 0f);
        transform.position = startPos;

        StartCoroutine(AnimateAndDestroy());
    }

    private IEnumerator AnimateAndDestroy()
    {
        float elapsed = 0f;
        Color startColor = text.color;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            transform.position = startPos + Vector3.up * riseDistance * moveCurve.Evaluate(t);

            Color c = startColor;
            c.a = alphaCurve.Evaluate(t);
            text.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }
}

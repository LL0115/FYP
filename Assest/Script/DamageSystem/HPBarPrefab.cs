using UnityEngine;

[CreateAssetMenu(fileName = "HealthBarPrefab", menuName = "UI/Health Bar Prefab")]
public class HealthBarPrefab : ScriptableObject
{
    public GameObject healthBarPrefab;
    public Vector3 offset = new Vector3(0, 2, 0);
    public Vector3 scale = new Vector3(1, 1, 1);

    public GameObject CreateHealthBar(Transform parent, DamagableItem target)
    {
        GameObject healthBar = Instantiate(healthBarPrefab, parent.position + offset, Quaternion.identity);
        healthBar.transform.SetParent(parent);
        healthBar.transform.localPosition = offset;
        healthBar.transform.localScale = scale;

        HPBar hpBar = healthBar.GetComponent<HPBar>();
        if (hpBar != null)
        {
            hpBar.Initialize(target);
        }

        return healthBar;
    }
}
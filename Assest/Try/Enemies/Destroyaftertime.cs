using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    [SerializeField] private float lifetime = 1.0f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
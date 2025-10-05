using Unity.VisualScripting;
using UnityEngine;

public class stats : MonoBehaviour
{
    public float health = 100f;
    public float damage = 20f;

    public void takeDamage(float damage)
    {
        health -= damage;

        if(gameObject.CompareTag("Breakable") && health <= 0) gameObject.SetActive(false);
    }
}
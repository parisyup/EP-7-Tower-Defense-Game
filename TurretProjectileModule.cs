using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;


/// <summary>
/// Mount this on your turret root. Set: 
/// - muzzleSystem: ParticleSystem at the barrel tip (for spawn point/forward)
/// - modelRoot:    Optional � the turret mesh to rotate toward target
/// - aimAt:        The Transform to shoot at
/// Call Fire() to shoot one projectile.
/// </summary>
public class TurretProjectileModule : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject muzzleSystem;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Transform aimAt;

    [Header("Bullet Configs")]
    public float BulletSpawnForce = 2000f;   // Force passed into Bullet.Spawn()
    public float BulletWeight = 0.02f;       // Mass applied to bullet rigidbody
    public LayerMask HitMask = ~0;           // Used only for penetration backcasts
    public Bullet BulletPrefab;              // Prefab with Rigidbody + Bullet script
    public bool EnableTrails = true;
    public Gradient Color;
    public Material Material;
    public AnimationCurve WidthCurve = AnimationCurve.Linear(0, 0.05f, 1, 0.0f);
    public float Duration = 0.15f;
    public float MinVertexDistance = 0.1f;
    public int MaxObjectsToPenetrate = 0;
    public float MaxPenetrationDepth = 0.2f;
    public Vector3 AccuracyLoss = new Vector3(0.01f, 0.01f, 0.01f);

    [Header("Explosion Configs")]
    public bool Explosive = false;
    public float ExplosionRange = 10f;
    public float ExplosionForce = 100f;
    public float ExplosionUpForce = 20f;
    public float ExplosionDamage = 20f;
    public GameObject Explosion;

    private ObjectPool<Bullet> bulletPool;
    private ObjectPool<TrailRenderer> trailPool;

    stats myStats;

    void Awake()
    {
        bulletPool = new ObjectPool<Bullet>(CreateBullet, null, null, b => Destroy(b.gameObject), true);
        trailPool = new ObjectPool<TrailRenderer>(CreateTrail, null, null, t => Destroy(t.gameObject), true);
        myStats = GetComponent<stats>();
    }

    /// <summary>Fires a single projectile from the turret toward aimAt.</summary>
    public void Fire()
    {
        if (aimAt == null || muzzleSystem == null || BulletPrefab == null) return;

        Vector3 toTarget = (aimAt.position - muzzleSystem.transform.position);
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Vector3 dir = toTarget.normalized;

        // Spawn projectile
        Bullet bullet = bulletPool.Get();
        bullet.transform.position = muzzleSystem.transform.position;
        bullet.transform.forward = dir;
        bullet.gameObject.SetActive(true);
        bullet.OnCollision += HandleBulletCollision;
        bullet.Spawn(dir * BulletSpawnForce);

        // Optional trail
        if (EnableTrails)
        {
            TrailRenderer trail = trailPool.Get();
            trail.transform.SetParent(bullet.transform, false);
            trail.transform.localPosition = Vector3.zero;
            trail.emitting = true;
            trail.gameObject.SetActive(true);
        }
    }

    private void HandleBulletCollision(Bullet bullet, Collision collision, int objectsPenetrated)
    {
        float damageFalloff = 1;
        if(MaxObjectsToPenetrate > 0) damageFalloff = (float) (MaxObjectsToPenetrate - objectsPenetrated) / MaxObjectsToPenetrate;
        
        if(collision != null && !collision.gameObject.CompareTag("Turret")) collision?.gameObject.GetComponent<stats>()?.takeDamage(myStats.damage);

        // Simple penetration pass
        if (collision != null && MaxObjectsToPenetrate > objectsPenetrated)
        {
            Vector3 direction = (bullet.transform.position - bullet.SpawnLocation).normalized;
            ContactPoint contact = collision.GetContact(0);
            Vector3 backCastOrigin = contact.point + direction * MaxPenetrationDepth;

            if (Physics.Raycast(backCastOrigin, -direction, out RaycastHit hit, MaxPenetrationDepth, HitMask))
            {
                direction += new Vector3(
                    Random.Range(-AccuracyLoss.x, AccuracyLoss.x),
                    Random.Range(-AccuracyLoss.y, AccuracyLoss.y),
                    Random.Range(-AccuracyLoss.z, AccuracyLoss.z)
                );

                bullet.transform.position = hit.point + direction * 0.01f;
                bullet.Rigidbody.linearVelocity = bullet.SpawnVelocity - direction;
                return; // keep flying
            }
        }

        if (Explosive)
        {
            Collider[] hitColliders = Physics.OverlapSphere(bullet.transform.position, ExplosionRange);

            List<int> enemiesHit = new();
            int currentEnemyid = -1;

            foreach (Collider collider in hitColliders)
            {
                EnemyController enemy = collider.gameObject.GetComponentInParent<EnemyController>();
                if (enemy != null) currentEnemyid = enemy.GetInstanceID();

                if(!collider.CompareTag("Turret") && enemy != null && !enemiesHit.Contains(currentEnemyid))
                {
                    enemiesHit.Add(currentEnemyid);
                    collision?.gameObject.GetComponentInParent<stats>()?.takeDamage(myStats.damage);

                    if (enemy.myStats.health <= 0) enemy.Death();
                    enemy.explosionForce(ExplosionForce, bullet.transform.position, ExplosionRange, ExplosionUpForce);
                }
            }

            GameObject explosion = Instantiate(Explosion);
            explosion.transform.position = bullet.transform.position;
            Destroy(explosion, 2f);
        }





        TrailRenderer trail = bullet.GetComponentInChildren<TrailRenderer>();
        DisableTrailAndBullet(trail, bullet);
    }

    private void DisableTrailAndBullet(TrailRenderer trail, Bullet bullet)
    {
        if (trail != null)
        {
            trail.transform.SetParent(null, true);
            StartCoroutine(DelayedDisableTrail(trail));
        }

        bullet.gameObject.SetActive(false);
        bullet.OnCollision -= HandleBulletCollision;
        bulletPool.Release(bullet);
    }

    private IEnumerator DelayedDisableTrail(TrailRenderer trail)
    {
        yield return new WaitForSeconds(Duration);
        yield return null;
        trail.emitting = false;
        trail.gameObject.SetActive(false);
        trailPool.Release(trail);
    }

    // --- Pools ---

    private Bullet CreateBullet()
    {
        Bullet b = Instantiate(BulletPrefab);
        var rb = b.GetComponent<Rigidbody>();
        if (rb) rb.mass = BulletWeight;
        return b;
    }

    private TrailRenderer CreateTrail()
    {
        GameObject go = new GameObject("Bullet Trail");
        var tr = go.AddComponent<TrailRenderer>();
        tr.colorGradient = Color;
        tr.material = Material;
        tr.widthCurve = WidthCurve;
        tr.time = Duration;
        tr.minVertexDistance = MinVertexDistance;
        tr.emitting = false;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.SetActive(false);
        return tr;
    }
}

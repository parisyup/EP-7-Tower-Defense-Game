using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class turretController : MonoBehaviour
{
    cameraStats camStats;

    public float smoothTime = 0.06f;
    Vector3 _vel;

    public GameObject aimAt;
    public GameObject currentTarget;
    public float range = 10f;
    public float leaveRange = 15f;
    public float aimAtSpeed = 100f;
    public float fireRate = 1f;
    public bool Zoom = false;
    public float ZoomMultiplier = 4f;
    public float ZoomSpeed;

    CinemachineCamera cam;
    float originalFov;

    EnemyController currentEnemyScript;

    TurretProjectileModule turretProjectileModule;
    stats myStats;

    public LayerMask ignoreLayer;

    float countDownForNextBullet = 0f;
    private void Start()
    {
        camStats = GetComponent<cameraStats>();
        turretProjectileModule = GetComponent<TurretProjectileModule>();
        myStats = GetComponent<stats>();

        if (Zoom)
        {
            cam = camStats.camera.GetComponent<CinemachineCamera>();
            originalFov = cam.Lens.FieldOfView;
        }
    }

    void Update()
    {
        findClosestEnemy();
        rechamberBullet();

        if (currentTarget != null && !camStats.controlledByPlayer && currentEnemyScript.isAlive)
        {
            aimAtEnemy();
            checkIfTargetIsVaild();
            fire();
        }

        if (myStats.health <= 0) death();
    }

    void death()
    {
        cameraController mainCameraController = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<cameraController>();
        if (mainCameraController.selected == camStats) mainCameraController.returnToMainCamera();

        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (camStats.controlledByPlayer)
        {
            playerControl();
        }
    }

    void aimAtEnemy()
    {
        aimAt.transform.position = Vector3.Lerp(aimAt.transform.position, new Vector3(currentTarget.transform.position.x, currentTarget.transform.position.y + 1, currentTarget.transform.position.z), aimAtSpeed * Time.deltaTime);
    }

    void findClosestEnemy()
    {
        if (currentTarget != null && currentEnemyScript.isAlive) return;
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float minDistance = Mathf.Infinity;

        foreach (GameObject enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            EnemyController enemyController = enemy.GetComponent<EnemyController>();
            if (distance < minDistance && distance <= range && enemyController != null && enemyController.isAlive)
            {
                minDistance = distance;
                currentTarget = enemy;
            }
        }

        if (currentTarget != null) currentEnemyScript = currentTarget.GetComponent<EnemyController>();
    }

    void checkIfTargetIsVaild()
    {
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);

        if (distanceToTarget > leaveRange) currentTarget = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, leaveRange);
    }

    void rechamberBullet()
    {
        if (countDownForNextBullet > 0) countDownForNextBullet -= Time.deltaTime;
    }

    void fire()
    {
        if (countDownForNextBullet <= 0)
        {
            turretProjectileModule.Fire();
            countDownForNextBullet = 1 / fireRate;
        }
    }

    void playerControl()
    {

        playerControlledCamera();

        if (Mouse.current.leftButton.isPressed) fire();

        handleZoom();
    }

    void handleZoom()
    {
        if (!Zoom) return;
        if (Mouse.current.rightButton.isPressed) cam.Lens.FieldOfView = Mathf.Lerp(cam.Lens.FieldOfView, originalFov / ZoomMultiplier, ZoomSpeed * Time.deltaTime);
        else cam.Lens.FieldOfView = Mathf.Lerp(cam.Lens.FieldOfView, originalFov, ZoomSpeed * Time.deltaTime);
    }

    void playerControlledCamera()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        Vector3 lookPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, ~ignoreLayer))
        {
            lookPoint = hit.point;
        }
        else
        {
            lookPoint = Camera.main.transform.position + Camera.main.transform.forward * 100f;
        }

        aimAt.transform.position = Vector3.SmoothDamp(aimAt.transform.position, lookPoint, ref _vel, smoothTime);
    }
}
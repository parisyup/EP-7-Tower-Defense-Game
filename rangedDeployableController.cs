using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;

public class rangedDeployableController : MonoBehaviour
{
    public cameraStats camStats;
    public stats myStats;
    public GameObject target;
    public bool isAlive;

    public List<Rigidbody> ragRBs = new();
    public List<Collider> ragCols = new();
    public Dictionary<Rigidbody, bool> rbWasKinematic = new();
    public Animator animator;
    public NavMeshAgent agent;

    [Header("Tuning")]
    public float turnSpeed = 10f;
    public float inputAccel = 18f;
    public float inputDecel = 24f;
    public float inputDead = .08f;
    public float rangeBuffer = 7.5f;

    Vector3 pointToTravelTo;
    float checkTimer;

    bool _prevIsPlayer;
    Vector2 _smoothedInput;
    Vector2 _targetInput;

    void Start()
    {
        camStats = GetComponent<cameraStats>();
        myStats = GetComponent<stats>();
        isAlive = true;
    }

    private void OnEnable()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();

        if (animator)
        {
            animator.gameObject.GetComponentsInChildren(true, ragRBs);
            animator.gameObject.GetComponentsInChildren(true, ragCols);

            ragRBs.RemoveAll(rb => rb.gameObject == this.gameObject);

            foreach (var rb in ragRBs)
            {
                rbWasKinematic[rb] = rb.isKinematic;
                rb.isKinematic = true;
            }
        }
    }

    private void Update()
    {
        if(!isAlive) return;
        if(myStats.health <= 0) { Death(); return; }

        control();
    }

    private void LateUpdate()
    {
        if (camStats && camStats.controlledByPlayer) FaceCameraYaw();
    }

    void FaceCameraYaw()
    {
        if (!Camera.main) return;
        float yaw = Camera.main.transform.rotation.eulerAngles.y;
        Quaternion targetRot = Quaternion.Euler(0, yaw, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
    }

    private void control()
    {
        bool isPlayer = camStats && camStats.controlledByPlayer;
        if(animator) animator.SetBool("isPlayer", isPlayer);

        if (isPlayer != _prevIsPlayer)
        {
            if (agent)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
                agent.isStopped = false;
                agent.updateRotation = !isPlayer;
            }

            animator.SetFloat("Speed", 0);
            animator.SetFloat("InputX", 0);
            animator.SetFloat("InputY", 0);

            pointToTravelTo = transform.position;
            checkTimer = 0f;

            _smoothedInput = Vector2.zero;
            _targetInput = Vector2.zero;

            _prevIsPlayer = isPlayer;
        }

        if (camStats.controlledByPlayer) controlByPlayer();
        else controlByAi();
    }

    void controlByPlayer()
    {
        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Vertical");

        _targetInput.Set(x, y);

        float accel = (_targetInput.magnitude > _smoothedInput.magnitude) ? inputAccel : inputDecel;
        _smoothedInput = Vector2.MoveTowards(_smoothedInput, _targetInput, accel * Time.deltaTime);

        animator.SetFloat("InputX", _smoothedInput.x, 0.08f, Time.deltaTime);
        animator.SetFloat("InputY", _smoothedInput.y, 0.08f, Time.deltaTime);

        Vector3 camF = Camera.main ? Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized : transform.forward;
        Vector3 camR = Camera.main ? Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized : transform.right;

        Vector3 desired = (camF * _smoothedInput.y + camR * _smoothedInput.x);
        if (desired.sqrMagnitude > 1f) desired.Normalize();

        agent.Move(desired * agent.speed * Time.deltaTime);
    }

    void controlByAi()
    {
        if (target == null || !target.activeInHierarchy) return;

        float distanceToTarget = Vector3.Distance(pointToTravelTo, transform.position);
        updatePointToTravelTo(distanceToTarget);
        pathingLogic();

        animator.SetFloat("Speed", agent.velocity.magnitude);

        if (distanceToTarget <= agent.stoppingDistance)
        {
            FaceTarget();
        }
    }

    public void updatePointToTravelTo(float distanceToTarget)
    {
        float distanceCheck = Vector3.Distance(target.transform.position, transform.position);
        if (!(distanceToTarget >= distanceCheck - rangeBuffer && distanceToTarget <= distanceCheck + rangeBuffer)) pointToTravelTo = target.transform.position;

        if (checkTimer > 0 || distanceToTarget < agent.stoppingDistance)
        {
            checkTimer -= Time.deltaTime;
            return;
        }

        RaycastHit hit;
        if (Physics.Raycast(transform.position, (target.transform.position - transform.position).normalized, out hit, Mathf.Infinity))
        {
            if (target.GetEntityId() == hit.collider.gameObject.GetEntityId())
            {
                pointToTravelTo = target.gameObject.GetComponent<Collider>().ClosestPoint(transform.position);
                checkTimer = 1;
                return;
            }
        }

        pointToTravelTo = target.transform.position;
    }

    public void pathingLogic()
    {
        if (pointToTravelTo == agent.destination) return;

        Vector3 newPosition = pointToTravelTo;

        if (NavMesh.SamplePosition(pointToTravelTo, out NavMeshHit hit, Mathf.Max(target.transform.localScale.y / 1.8f, 2f), agent.areaMask))
            newPosition = hit.position;

        NavMeshPath path = new NavMeshPath();
        bool check = NavMesh.CalculatePath(agent.transform.position, newPosition, agent.areaMask, path);
        if (check) agent.SetPath(path);
    }

    void FaceTarget()
    {
        Vector3 direction = (pointToTravelTo - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Lerp(transform.rotation, lookRotation, Time.deltaTime * turnSpeed);
    }

    public void Death()
    {
        if (isAlive)
        {
            cameraController mainCameraController = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<cameraController>();
            if (mainCameraController.selected == camStats) mainCameraController.returnToMainCamera();

            isAlive = false;

            Destroy(gameObject, 120);

            gameObject.GetComponent<Collider>().enabled = false;

            agent.enabled = false;

            if (animator) animator.enabled = false;

            foreach (var rb in ragRBs)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.WakeUp();
            }
        }
    }


}

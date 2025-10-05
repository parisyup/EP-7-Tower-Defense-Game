using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
public class EnemyLocomotion : MonoBehaviour
{
    NavMeshAgent agent;
    GameObject target;
    public float turnSpeed = 7.5f;
    public float rangeBuffer = 7.5f;
    Animator animator;
    EnemyController enemyController;

    Vector3 pointToTravelTo;
    float checkTimer;

    bool _won = false;
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        enemyController = GetComponent<EnemyController>();
    }

    void Update()
    {
        if (_won || target == null || !enemyController.isAlive) return;
        if (!target.activeInHierarchy) return;

        float distanceToTarget = Vector3.Distance(pointToTravelTo, transform.position);

        updatePointToTravelTo(distanceToTarget);

        pathingLogic();

        animator.SetFloat("Speed", agent.velocity.magnitude);

        if (distanceToTarget <= agent.stoppingDistance)
        {
            FaceTarget();

            enemyController.withinAttackRange = true;
        }
        else enemyController.withinAttackRange = false;
    }

    void FaceTarget()
    {
        Vector3 direction = (pointToTravelTo - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Lerp(transform.rotation, lookRotation, Time.deltaTime * turnSpeed);
    }

    public void attackAnimation()
    {
        animator.ResetTrigger("Slash");
        animator.SetTrigger("Slash");
    }


    public void won()
    {
        if (!enemyController.isAlive) return;
        agent.destination = transform.position;
        animator.SetTrigger("Celebrate");
        _won = true;
    }

    public void updatePointToTravelTo(float distanceToTarget)
    {
        float distanceCheck = Vector3.Distance(target.transform.position, transform.position);
        if(!(distanceToTarget >= distanceCheck - rangeBuffer && distanceToTarget <= distanceCheck + rangeBuffer)) pointToTravelTo = target.transform.position;

        if(checkTimer > 0 || distanceToTarget < agent.stoppingDistance)
        {
            checkTimer -= Time.deltaTime;
            return;
        }

        RaycastHit hit;
        if(Physics.Raycast(transform.position, (target.transform.position - transform.position).normalized, out hit, Mathf.Infinity))
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

        if(NavMesh.SamplePosition(pointToTravelTo, out NavMeshHit hit, Mathf.Max(target.transform.localScale.y / 1.8f, 2f), agent.areaMask))
            newPosition = hit.position;

        NavMeshPath path = new NavMeshPath();
        bool check = NavMesh.CalculatePath(agent.transform.position, newPosition, agent.areaMask, path);
        if (check) agent.SetPath(path);
    }


    public void setTarget(GameObject t) { target = t; }
}
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    public bool withinAttackRange;
    public float timeBetweenAttacks = 2f;
    public float attackCoolDown = 0f;
    public stats myStats;

    public List<GameObject> targets;
    public GameObject target;

    public event Action<EnemyController> Died;
    public bool isAlive;

    public List<Rigidbody> ragRBs = new();
    public List<Collider> ragCols = new();
    public Dictionary<Rigidbody, bool> rbWasKinematic = new();
    public Transform hips;
    public Animator animator;
    public NavMeshAgent agent;



    EnemyLocomotion locomotion;
    void Start()
    {
        locomotion = GetComponent<EnemyLocomotion>();
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

            if (animator && animator.isHuman) hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (!hips) hips = animator.transform;
        }
    }

    void Update()
    {
        if (myStats.health <= 0) Death();

        attackCheck();
        checkIfTargetIsVaild();

        if (target.activeInHierarchy) locomotion.setTarget(target);
    }

    void attackCheck()
    {
        if (attackCoolDown <= 0 && withinAttackRange && target != null)
        {
            locomotion.attackAnimation();
            attackCoolDown = timeBetweenAttacks;

            stats targetStats = target.GetComponent<stats>();
            if (targetStats != null)
            {
                targetStats.takeDamage(myStats.damage);
            }


        }
        else if (attackCoolDown > 0) attackCoolDown -= Time.deltaTime;
    }

    void checkIfTargetIsVaild()
    {
        if(targets.Count > 0 && target == null) target = targets[0];
        if (!target.activeInHierarchy)
        {
            targets.Remove(target);

            if (targets.Count > 0)
            {
                target = targets[0];
                return;
            }

            locomotion.won();
        }
    }

    public void Death()
    {
        if (isAlive)
        {
            isAlive = false;
            Died?.Invoke(this);

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

    public void explosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius, float explosionUpForce)
    {
        foreach (var rb in ragRBs)
        {
            float hipboost = (hips && rb.transform == hips) ? 1.3f : 1f;
            rb.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, explosionUpForce);
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    /// 获得敌人头顶的警示
    private GameObject alarmSign;

    /// 用于控制动画状态机
    public Animator anim;
    
    /// 控制动画的状态 Animator中的Parameter
    public int animState;
    
    /// 敌人的当前的状态 (巡逻或者攻击)
    private EnemyBaseState _currentState;
    
    /// 获取巡逻状态的对象
    public PatrolState patrolState = new PatrolState();
    
    /// 获取攻击状态的对象
    public AttackState attackState = new AttackState();
    
    [Header("Movement")] public float speed; // 移动速度
    public Transform ponitA, pointB;
    public Transform targetPonit; // 目标点
    
    [Header("Attack Settings")] public float attackRate; // 普攻击CD 技能CD
    public float skillRate; //  技能CD
    public float attackRange; // 攻击距离
    public float skillRange; // 技能打击距离
    private float nextAttack = 0;
    
    [Header("Base State")] public float enemyHP;
    public bool isDead;
    
    /// 是否有炸弹
    public bool hasBomb;
    
    /// 是否是Boss
    public bool isBoss;


    /// 攻击列表，敌人的可攻击范围检测到物体就添加进这个列表
    public List<Transform> attackList = new List<Transform>();
    
    // 初始化 方便子类进行修改
    public virtual void Init()
    {
        anim = GetComponent<Animator>();
        alarmSign = transform.GetChild(0).gameObject;
    }
    
    // 确保游戏一开始变量有值，优先State执行
    private void Awake()
    {
        Init();
    }
    
    void Start()
    {
        GameManager.instance.isEnemy(this);
        // 开始游戏直接进入巡逻状态
        TransitionToState(patrolState);
        // Slider绑定boss血量
        if (isBoss)
            UIManager.instance.SetBossHealth(enemyHP);
    }
    
    public virtual void Update()
    {
        // 实时更新血量  1
        if (isBoss)
            UIManager.instance.UpdateBossHealth(enemyHP);
        // 更新血量之后播放
        anim.SetBool("dead", isDead);
        // 死亡终止活动
        if (isDead)
        {
            GameManager.instance.EnemyDead(this);
            return;
        }
        
        // 当前敌人执行当前状态
        _currentState.OnUpdate(this);
        // 持续更新动画，保证动画与Parameter保持一致
        anim.SetInteger("state", animState);
    }
    
    /// <summary>
    /// 切换状态的方法
    /// </summary>
    /// <param name="state">传入一个状态机对象切换敌人的状态，切换之后进入该状态</param>
    public void TransitionToState(EnemyBaseState state)
    {
        // 切换状态 当前状态 = 传入的状态
        _currentState = state;
        // 切换之后敌人进入当前状态
        _currentState.EnterState(this);
    }
    
    /// 移动的方法
    public void MoveToTarget()
    {
        // 朝着目标进行移动（当前坐标，目标坐标，距离的Delta值,确保在不同的机型得到同样的效果）
        transform.position = Vector2.MoveTowards(transform.position, targetPonit.position,
            speed * Time.deltaTime);
        FilpDirection();
    }
    
    /// 攻击玩家
    public void AttackAction()
    {
        // 先判断攻击距离
        if (Vector2.Distance(transform.position, targetPonit.position) < attackRange)
        {
            // 释放之前先判断CD冷却
            if (Time.time > nextAttack)
            {
                // 播放攻击动画
                anim.SetTrigger("attack");
                // 计算下一次可攻击时间
                nextAttack = Time.time + attackRate;
            }
        }
    }
    
    /// 对炸弹进行技能攻击 虚方法，让子类可以重写
    public virtual void SkillAction()
    {
        // 先判断攻击距离
        if (Vector2.Distance(transform.position, targetPonit.position) < skillRange)
        {
            // 释放之前先判断CD冷却
            if (Time.time > nextAttack)
            {
                // 播放攻击动画
                anim.SetTrigger("skill");
                nextAttack = Time.time + skillRate;
            }
        }
    }
    
    /// 巡逻过程中需要左右翻转 他一定是在移动的过程中进行调用的
    public void FilpDirection()
    {
        // 在目标点的左侧，向右追不需要翻转
        if (transform.position.x < targetPonit.position.x)
            transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        else
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);
    }


    /// 判断当前坐标与PointA和B的位置，将移动的目标点设置为距离远的那一个
    public void SwitchPoint()
    {
        // A点于当前怪物的距离>B点到当前怪物的距离说明A点远
        if (Mathf.Abs(ponitA.position.x - transform.position.x)
            > Mathf.Abs(pointB.position.x - transform.position.x))
            targetPonit = ponitA;
        else
            targetPonit = pointB;
    }
    
    /// 物体进入检测范围就添加进攻击列表里
    private void OnTriggerStay2D(Collider2D other)
    {
        // 如果攻击列表不包含，再添加进列表里 添加的是transform
        // （人物没有炸弹才进行判断，有炸弹就不添加进列表里）如果死亡不添加进攻击列表 
        // 如果玩家死亡 不继续判断
        if (!attackList.Contains(other.transform) && !hasBomb && !isDead && !GameManager.instance.gameOver)
            attackList.Add(other.transform);
    }
    
    /// 物体走出检测范围从攻击列表里移除
    private void OnTriggerExit2D(Collider2D other)
    {
        attackList.Remove(other.transform);
    }
    
    /// 物体刚一进入检测范围播放动画 
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 不是死亡状态下开始一个协程  处理玩家死亡后敌人巡逻依旧会有警示标志的bug
        if (!isDead && !GameManager.instance.gameOver)
            StartCoroutine(OnAlarm());
    }
    
    /// 启动播放等待动画播完关闭 协程方式
    IEnumerator OnAlarm()
    {
        alarmSign.SetActive(true);
        // 等待几秒后继续执行，这里的时间获取动画片段的时间 层级默认是0，第一个动画片段[0]的长度
        yield return new WaitForSeconds(
            alarmSign.GetComponent<Animator>().GetCurrentAnimatorClipInfo(0)[0].clip.length);
        alarmSign.SetActive(false);
    
        // yield return null; // 当上一步执行完，没有返回等待下一帧继续执行后面的代码
    }
}
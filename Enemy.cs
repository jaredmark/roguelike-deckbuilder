using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : BattleEntity
{
    public enum State { UsedForBattle, UsedForDevSelection, UsedForDisplay}

    public EnemyData enemyData;

    public int AttackModifier { get; private set; }
    public float AttackMultiplier { get; private set; }
    public float AttackReduction { get; private set; }
    public float IncomingDamageMultiplier { get; private set; }
    public float IncomingDamageReduction { get; private set; }
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public int Defense { get; private set; }
    public AnimatorOverrideController AnimatorOverrideController { get; private set; }
    public ActionAttack previousAttackAction;
    public EnemySpell spellToCast = null;
    public EnemySpell specialSpellToCast;

    bool CanPerformActions
    {
        get
        {
            foreach (IEffectPreventsActions actionPreventer in actionPreventers) if (actionPreventer.WillPreventAction) return false;
            return true;
        }
    }

    [SerializeField] Vector3 usedForBattleSize;
    [SerializeField] int usedForBattleSortingLayer;
    [SerializeField] Vector3 usedForDevSelectionSize;
    [SerializeField] int usedForDevSelectionSortingLayer;

    Vector3 defaultPos = new Vector3(0, 0, 0);
    Vector3 defaultDevSelectPos = new Vector3(0, 0, -1f);

    //For change tile position;
    bool willChangeTilePos;
    float tileChangeSpeed;
    const float DEFAULT_TILE_CHANGE_SPEED = 5f;

    //For cast effect:
    float castEffectTimeElapsed;
    float castEffectBlinkDuration;
    float castEffectBlinkInterval;
    float castEffectDuration;
    bool isCastEffectActive;
    Coroutine corCastEffect;
    Material castEffectMaterial;

    public readonly float attackDamageTriggerTime = 0.75f;

    public readonly int defaultAttackModifier = 0;
    public readonly float defaultAttackMultiplier = 1.0f;
    public readonly float defaultAttackReduction = 0;
    public readonly float defaultIncomingDamageMultiplier = 1.0f;
    public readonly float defaultIncomingDamageReduction = 0;

    //dont edit;
    [HideInInspector] public bool hasAttacked;
    [HideInInspector] public bool isQuick;
    [HideInInspector] public bool isPrioritizedForAttack;
    [HideInInspector] public bool isDisarmed;
    bool hasFinishedTurn, hasSpecialSpell;
    public bool HasUltimate { get; private set; }
    Dictionary<int, StatusIcon> StatusEffectIcons { get; set; }
    public bool HasFinishedAttacking { get; private set; }
    bool hasPreparedBattleAction;
    bool hasInitializedReferences = false;
    public bool willDespawn { get; private set; }
    State state;

    public readonly List<IEffectModifiesAttack> attackModifiers = new List<IEffectModifiesAttack>();
    public readonly List<IEffectModifiesEnemyStats> statModifiers = new List<IEffectModifiesEnemyStats>();
    readonly List<IEffectPreventsDeath> deathPreventers = new List<IEffectPreventsDeath>();
    readonly List<IEffectPreventsActions> actionPreventers = new List<IEffectPreventsActions>();
    readonly List<IEffectRandomizesTarget> targetRandomizers = new List<IEffectRandomizesTarget>();

    public bool CanTargetRandomly
    {
        get
        {
            foreach (IEffectRandomizesTarget targetRandomizer in targetRandomizers)
            {
                if (targetRandomizer.WillRandomizeTarget) return true;
            }

            return false;
        }
    }
    public IEffectRandomizesTarget TargetRandomizer
    {
        get
        {
            foreach (IEffectRandomizesTarget targetRandomizer in targetRandomizers) if (targetRandomizer.WillRandomizeTarget) return targetRandomizer;
            return null;
        }
    }

    HealthBar healthBar;
    Transform enemySkillIcons;
    Transform statsDisplay;
    new BoxCollider2D collider;
    DevSetting devSetting;

    public delegate void OnFinishedStrikeEvent();
    public delegate int OnTakeBonusDamageEvent();
    public delegate void OnAfterTakingDamageEvent();
    public delegate void OnSlainEvent();
    delegate void OnBeforePlayingTurnHandler();
    public delegate void OnGainStatusEffectEvent(StatusEffect statusEffect);
    public delegate void OnAfterTakingDamageGetAttackerEvent(BattleEntity attacker);
    public delegate void OnAfterTakingDamageGetDamageEvent(int damage);

    OnBeforePlayingTurnHandler OnBeforePlayingTurn;
    public OnFinishedStrikeEvent OnFinishedStrike;
    public OnTakeBonusDamageEvent OnTakeBonusDamage;
    public OnGainStatusEffectEvent OnGainStatusEffect;
    public OnAfterTakingDamageGetAttackerEvent OnAfterTakingDamageGetAttacker;
    public OnAfterTakingDamageGetDamageEvent OnAfterTakingDamageGetDamage;
    public GameManager.VoidEvent OnGetSpellToCast;
    public GameManager.VoidEvent OnStartTurn;
    public GameManager.VoidEvent OnSlayMinion;
    GameManager.VoidEvent OnAfterAddingStatusEffect;
    public GameManager.VoidEvent OnAfterGainingEffect;
    public GameManager.VoidEvent OnModifyEffectStacks;

    protected override void Awake()
    {
        if (!hasCalledAwake)
        {
            base.Awake();
            gameManager = GameManager.gm;
            statsDisplay = transform.Find("Canvas").Find("StatsDisplay");
            healthBar = statsDisplay.Find("HealthBar").GetComponent<HealthBar>();
            enemySkillIcons = statsDisplay.Find("EnemySkillIcons");
            collider = GetComponent<BoxCollider2D>();
            statusIconsPanel = healthBar.transform.Find("StatusIcons");
            StatusEffectIcons = new Dictionary<int, StatusIcon>();
            statusIconsPanel = healthBar.transform.Find("StatusIcons");

            defaultMaterial = Instantiate(MaterialManager.MaterialEntity);
            onDamagedMaterial = Instantiate(MaterialManager.MaterialBrightFlash);
            castEffectMaterial = Instantiate(MaterialManager.MaterialBrightFlash);
            castEffectMaterial.name += StringManager.GetRandomString(6);
            ChangeSpriteRendererMaterial(defaultMaterial);

            hasCalledAwake = true;
        }
    }

    void Update()
    {
        if (willChangeTilePos)
        {
            float currentXPos = transform.localPosition.x;
            Vector2 newPos = new Vector2(Mathf.Lerp(currentXPos, 0, Time.deltaTime * tileChangeSpeed), transform.localPosition.y);
            transform.localPosition = newPos;
            if (transform.localPosition.x.Equals(0f))
            {
                willChangeTilePos = false;
            }
        }
    }

    public void Initialize(State state, EnemyData enemyData)
    {
        Awake();
        this.state = state;

        InitializeReferences();
        ClearCurrentSpellToCast();
        ClearCurrentSpecialSpellToCast();
        ClearAllStatusEffects();

        this.enemyData = enemyData;
        this.enemyData.Enemy = this;
        
        animator.Play(StringManager.ANIM_STATE_NAME_ENEMY_IDLE, 0);
        SetHealth(enemyData.Health, enemyData.Health);

        AttackModifier = defaultAttackModifier;
        AttackMultiplier = defaultAttackMultiplier;
        AttackReduction = defaultAttackReduction;
        IncomingDamageMultiplier = defaultIncomingDamageMultiplier;
        IncomingDamageReduction = defaultIncomingDamageReduction;
        RecentDamageTaken = 0;
        HasFinishedAttacking = false;
        hasPreparedBattleAction = false;
        willChangeTilePos = false;

        IsSlain = false;

        collider.enabled = true;

        ApplyEnemyDataBuffs();
        InitializeSkills();

        if (state == State.UsedForBattle)
        {           
            transform.localScale = usedForBattleSize;
            spriteRendererMain.sortingOrder = usedForBattleSortingLayer;
            transform.localPosition = defaultPos;
        }
        else if (state == State.UsedForDevSelection)
        {
            transform.localScale = usedForDevSelectionSize;
            spriteRendererMain.sortingOrder = usedForDevSelectionSortingLayer;
            transform.localPosition = defaultDevSelectPos;
        }

        this.enemyData.OnInitializeEnemy?.Invoke(this);
        //Clear events
    }

    //public override void TakeAbsoluteDamage(int amount, BattleEntity attacker, bool isLastHit)
    //{
    //    Attacker = null;
    //    ReduceHealth(amount, amount.ToString(), attacker, isLastHit, false);
    //}

    public override void TakeBattleDamage(DamageData damageData)
    {
        int amount = damageData.amount; //Damage calculation START

        OnBeforeTakingDmgGetAttacker?.Invoke(damageData.source);

        int incomingDamageReduction = 0;
        float incomingDamageMultiplier = defaultIncomingDamageMultiplier;
        float incomingDamageReductionMultiplier = defaultIncomingDamageReduction;

        OnBeforeTakingDmgGetDmgRefInc?.Invoke(ref amount); //Damage increasing events
        OnBeforeTakingDmgGetDmgRefTypeInc?.Invoke(ref amount, damageData.type);

        gameManager.OnEnemyBeforeTakingDamageGetDmgRefInc?.Invoke(ref amount);
        gameManager.OnEnemyBeforeTakingDamageGetDmgEnemTyp?.Invoke(ref amount, this, damageData.type);

        foreach (IEffectModifiesDefense defenseModifier in defenseModifiers)
        {
            incomingDamageReduction += defenseModifier.IncomingDamageReduction;
            incomingDamageMultiplier += defenseModifier.IncomingDamageMultiplier;
            incomingDamageReductionMultiplier += defenseModifier.IncomingDamageReductionMultiplier;
        }

        amount = Mathf.RoundToInt((amount) * (incomingDamageMultiplier + 0.001f)); //apply damage multiplier
        OnBeforeTakingDmgGetDmgRefMult?.Invoke(ref amount);
        gameManager.OnEnemyBeforeTakingDmgGetDmgRefMult?.Invoke(ref amount);

        if (!damageData.isAbsolute) 
        {
            amount -= incomingDamageReduction; //Apply flat damage reduction
            OnBeforeTakingDmgGetDmgRefReduc?.Invoke(ref amount);

            amount = Mathf.RoundToInt(amount - (amount * (incomingDamageReductionMultiplier + 0.001f))); //apply percentage damage reduction
        }

        //damage calculation END

        if (amount < 0) amount = 0;

        BattleEntity attacker = damageData.source;
        Attacker = attacker;

        if (damageNegators.Count > 0 && damageData.type == DamageData.TYPE_NORMAL) //for attack blocking effects
        {
            foreach (IEffectNegatesDamage damageNegator in damageNegators)
            {
                if (damageNegator.WillBlockAttack)
                {
                    amount = 0;
                    damageNegator.OnNegateDamage?.Invoke();
                }
            }
        }

        CurrentHealth -= amount;
        RecentDamageSource = attacker;
        RecentDamageTaken = amount;

        if (Attacker != null && (Attacker is PlayerCharacter || Attacker is Minion)) gameManager.IncreaseDamageDealtThisTurnAtksSpls(amount);

        OnAfterTakingDamageGetDamage?.Invoke(RecentDamageTaken);

        GameManager.UiHandler.SpawnDamageText(transform.position, RecentDamageTaken.ToString(), DamageText.LaunchDirection.Right, new Vector2(60f, 100f));

        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
        }

        healthBar.UpdateValues(CurrentHealth, MaxHealth);

        bool canKill = !damageData.cannotKill;
        if (deathPreventers.Count > 0)
        {
            foreach (IEffectPreventsDeath deathPreventer in deathPreventers)
            {
                if (deathPreventer.WillPreventDeath)
                {
                    canKill = false;
                    break;
                }
            }
        }

        //Slay() was here

        //if hp is not equal or less than 0
        //Update HP Bar(will play anim)
        if (damageData.type == DamageData.TYPE_NORMAL)
        {
            OnAfterTakingDamageByAttack?.Invoke();
            OnAfterTakingDamageByAttackGetAttacker?.Invoke(Attacker);
        }
        OnAfterTakingDamage?.Invoke();
        OnAfterTakingDamageGetAttacker?.Invoke(Attacker);

        if (CurrentHealth <= 0 && canKill && damageData.isLastHit)
        {
            Slay();
        }

        //temp
        PlayFlashEffect(DEFAULT_DAMAGED_EFFECT_DURATION);
    }

    public void TakeDamage(int amount) //Pure damage and events will not play
    {
        CurrentHealth -= amount;

        GameManager.UiHandler.SpawnDamageText(transform.position, amount.ToString(), DamageText.LaunchDirection.Right, new Vector2(60f, 100f));

        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
        }

        healthBar.UpdateValues(CurrentHealth, MaxHealth);

        bool canKill = true;
        if (deathPreventers.Count > 0)
        {
            foreach (IEffectPreventsDeath deathPreventer in deathPreventers)
            {
                if (deathPreventer.WillPreventDeath)
                {
                    canKill = false;
                    break;
                }
            }
        }

        if (CurrentHealth <= 0 && canKill)
        {
            Slay();
        }

        //temp
        PlayFlashEffect(DEFAULT_DAMAGED_EFFECT_DURATION);
    }

    public void Heal(int amount)
    {
        if (amount > 0)
        {
            Particle.PlayParticleWithoutEffect(ResourcesManager.ParticleHealTemp, this);
            CurrentHealth += amount;
            if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
            
            healthBar.UpdateValues(CurrentHealth, MaxHealth);
        }
    }

    public override void Slay()
    {
        IsSlain = true;

        //freeze hurt anim
        gameManager.OnEnemySlainGetEnemy?.Invoke(this);
        gameManager.OnEnemySlain?.Invoke();
        OnSlain?.Invoke();
        GetComponent<BoxCollider2D>().enabled = false;
        RemoveFromTile();

        //disable collider
        //hide battle texts
        // after fadeout anim remove from play

        StartCoroutine(RemoveObject(GameManager.GetAnimationLength(animator, "EnemyFadeOut")));
    }

    public void Flee()
    {
        willDespawn = true;
        PlayDeathEffect(0.5f);
        StartCoroutine(RemoveObject(0.5f));
    }

    public void Despawn()
    {
        willDespawn = true;
        StartCoroutine(RemoveObject(0f));
    }

    IEnumerator RemoveObject(float timeToRemove)
    {
        void RemoveSpellPassive(EnemySpell enemySpell)
        {
            if (enemySpell != null && enemySpell is IEnemySpellHasPassive passiveSpell) passiveSpell.RemoveEnemySpellPassive();
        }

        RemoveSpellPassive(specialSpellToCast);
        RemoveSpellPassive(spellToCast);

        ClearAllStatusEffects();
        ClearAllTraits();
        DestroyStatsDisplay();

        //If there are no enemies left. end battle
        if (gameManager.GetEnemiesOnField().Count == 0)
        {
            gameManager.EndVictoriousBattle();
        }

        yield return new WaitForSeconds(timeToRemove);

        Destroy(gameObject);
    }

    void DestroyStatsDisplay()
    {
        Destroy(statsDisplay.gameObject);
    }

    public bool CanPlayFirst()
    {
        if (isQuick)
        {
            return true;
        }

        return false;
    }

    public bool CanPlayTurn()
    {
        if (!hasFinishedTurn && !IsSlain && CanPerformActions && ((spellToCast != null && spellToCast.CanCast()) || (specialSpellToCast != null && specialSpellToCast.CanCast())))
        {
            return true;
        }

        //TEMP - remove spell passives if cant plkay turn
        void RemoveEnemySpellPassive(EnemySpell enemySpell)
        {
            IEnemySpellHasPassive passiveEnemySpell = enemySpell as IEnemySpellHasPassive;
            passiveEnemySpell.RemoveEnemySpellPassive();
        }
        if (specialSpellToCast is IEnemySpellHasPassive) RemoveEnemySpellPassive(specialSpellToCast);
        if (spellToCast is IEnemySpellHasPassive) RemoveEnemySpellPassive(spellToCast);
        // // // // // TEMP // // // // //

        return false;
    }

    public void EndTurn()
    {
        hasFinishedTurn = true;
        animator.Play(StringManager.ANIM_STATE_NAME_ENEMY_IDLE, 0);
    }

    public void PlayBattleAction()
    {
        if (!hasPreparedBattleAction)
        {
            hasPreparedBattleAction = true;

            StartCoroutine(CastSpell(PlayCastEffect(0.066f, 0.033f)));
        }
    }

    float PlayCastEffect(float blinkDuration, float blinkInterval)
    {
        IEnumerator PlayCastEffect()
        {
            while (true)
            {
                castEffectTimeElapsed += Time.deltaTime;
                if (castEffectTimeElapsed >= castEffectDuration) break;

                if (((castEffectTimeElapsed >= castEffectBlinkInterval && castEffectTimeElapsed <= castEffectBlinkInterval + castEffectBlinkDuration)) ||
                    ((castEffectTimeElapsed >= (castEffectBlinkInterval * 2) + castEffectBlinkDuration && castEffectTimeElapsed <= (castEffectBlinkDuration * 2) + (castEffectBlinkInterval * 2))))
                {
                    if (!isCastEffectActive)
                    {
                        SetMaterial(castEffectMaterial);
                        isCastEffectActive = true;
                    }
                }
                else
                {
                    if (isCastEffectActive)
                    {
                        UnsetMaterial(castEffectMaterial);
                        isCastEffectActive = false;
                    }
                }

                yield return null;
            }

            if (isCastEffectActive)
            {
                UnsetMaterial(castEffectMaterial);
                isCastEffectActive = false;
            }

            corCastEffect = null;
        }

        castEffectDuration = (blinkDuration * 2) + (blinkInterval * 3);
        castEffectBlinkDuration = blinkDuration;
        castEffectBlinkInterval = blinkInterval;
        castEffectTimeElapsed = 0f;

        if (corCastEffect == null) corCastEffect = StartCoroutine(PlayCastEffect());

        return castEffectDuration;
    }

    IEnumerator CastSpell(float timeToCast)
    {
        yield return new WaitForSeconds(timeToCast);

        OnBeforePlayingTurn?.Invoke();
        OnBeforePlayingTurn = null;

        if (specialSpellToCast != null)
        {
            if (specialSpellToCast.CanCast()) CastSpell(specialSpellToCast);
            ClearCurrentSpecialSpellToCast();
        }

        if (spellToCast != null)
        {
            if (spellToCast.CanCast()) CastSpell(spellToCast);
            ClearCurrentSpellToCast();
        }
    }

    public void CastSpell(EnemySpell enemySpell)
    {
        if (enemySpell is IEffectTargeting)
        {
            IEffectTargeting effectTargeting = enemySpell as IEffectTargeting;

            if (gameManager.CanSelectTarget(effectTargeting))
            {
                if (!effectTargeting.HasSelectedTarget && !(enemySpell is IEffectDamaging))
                {
                    gameManager.GetEffectTarget(enemySpell as IEffectTargeting);
                }
            }
        }
        enemySpell.Cast();
        enemySpell.OnCast?.Invoke();
    }

    public void SetNextSpecialSpellToCast(EnemySpell enemySpecialSpell)
    {
        if (enemySpecialSpell != null)
        {
            if (enemySpecialSpell.Enemy == null) enemySpecialSpell.Enemy = this;

            ClearCurrentSpecialSpellToCast();
            specialSpellToCast = enemySpecialSpell;
            ShowEnemySkillIcon(specialSpellToCast);

            if (specialSpellToCast is IEnemySpellHasPassive)
            {
                IEnemySpellHasPassive enemySpellHasPassive = specialSpellToCast as IEnemySpellHasPassive;
                enemySpellHasPassive.ApplyEnemySpellPassive();
            }

            if (specialSpellToCast is IEffectTargeting)
            {
                IEffectTargeting targetingEffect = specialSpellToCast as IEffectTargeting;
                targetingEffect.TargetList.Clear();
                targetingEffect.HasSelectedTarget = false;
            }

            specialSpellToCast.OnSelect?.Invoke();
            gameManager.OnEnemySetSpellToCast?.Invoke();
        }
    }

    public void SetNextSpellToCast(EnemySpell enemySpell)
    {
        if (enemySpell != null)
        {
            if (enemySpell.Enemy == null) enemySpell.Enemy = this;

            ClearCurrentSpellToCast();
            spellToCast = enemySpell;
            //show and play animation of enemy intent
            ShowEnemySkillIcon(spellToCast);

            if (spellToCast is IEnemySpellHasPassive)
            {
                IEnemySpellHasPassive enemySpellHasPassive = spellToCast as IEnemySpellHasPassive;
                enemySpellHasPassive.ApplyEnemySpellPassive();
            }

            if (enemySpell is IEffectTargeting)
            {
                IEffectTargeting targetingEffect = enemySpell as IEffectTargeting;
                targetingEffect.TargetList.Clear();
                targetingEffect.HasSelectedTarget = false;
            }

            spellToCast.OnSelect?.Invoke();
            gameManager.OnEnemySetSpellToCast?.Invoke();
        }
    }

    void RemoveSkillIcon(EnemySkillIcon icon)
    {
        if (icon != null)
        {
            Destroy(icon.gameObject);
        }
    }

    void RemoveEnemySpellPassive(EnemySpell enemySpell)
    {
        if (enemySpell is IEnemySpellHasPassive passiveSpell)
        {
            passiveSpell.RemoveEnemySpellPassive();
        }
    }

    void ClearCurrentSpellToCast()
    {
        if (spellToCast != null)
        {
            RemoveSkillIcon(spellToCast.EnemySkillIcon);
            RemoveEnemySpellPassive(spellToCast);
            spellToCast = null;
        }
    }

    void ClearCurrentSpecialSpellToCast()
    {
        if (specialSpellToCast != null)
        {
            RemoveSkillIcon(specialSpellToCast.EnemySkillIcon);
            RemoveEnemySpellPassive(specialSpellToCast);
            specialSpellToCast = null;
        }
    }

    public void GetSpellToCast()
    {
        hasFinishedTurn = false;
        hasPreparedBattleAction = false;
        hasAttacked = false;

        ClearCurrentSpecialSpellToCast();
        ClearCurrentSpellToCast();

        if (enemyData.SpecialSpellList.Values.Count > 0)
        {
            SetNextSpecialSpellToCast(enemyData.SpecialSpellList.GetSpellToCast());
        }

        SetNextSpellToCast(enemyData.SpellList.GetSpellToCast());

        OnGetSpellToCast?.Invoke();
    }

    public bool WillAttack
    {
        get { return (spellToCast != null && spellToCast is EnemySpellDamaging) || (specialSpellToCast != null && specialSpellToCast is EnemySpellDamaging); }
    }

    void ShowEnemySkillIcon(EnemySpell enemySpell)
    {
        enemySpell.EnemySkillIcon = Instantiate(ResourcesManager.PrefabEnemySkillIcon, enemySkillIcons).GetComponent<EnemySkillIcon>();
        enemySpell.EnemySkillIcon.Initialize(enemySpell);
    }

    public void UpdateEnemySkillIcon()
    {
        spellToCast?.EnemySkillIcon?.Initialize(spellToCast);
        specialSpellToCast?.EnemySkillIcon?.Initialize(specialSpellToCast);
    }

    void InitializeSkills()
    {
        List<EnemySpell> enemySpells = new List<EnemySpell>();
        enemySpells.AddRange(enemyData.SpellList.Values);
        enemySpells.AddRange(enemyData.SpecialSpellList.Values);

        foreach (EnemySpell enemySpell in enemySpells)
        {
            enemySpell.Enemy = this;
            if (enemySpell.attackHitParticles.Count == 0)
            {
                enemySpell.attackHitParticles.Add(ResourcesManager.ParticleHitBasic1);
            }
        }

        foreach (Trait trait in Traits.Values)
        {
            if (trait is IEffectHasEnemySpells)
            {
                IEffectHasEnemySpells effectHasEnemySpells = trait as IEffectHasEnemySpells;
                foreach (EnemySpell enemySpell in effectHasEnemySpells.EnemySpells)
                {
                    enemySpell.Enemy = this;
                    if (enemySpell.attackHitParticles.Count == 0)
                    {
                        enemySpell.attackHitParticles.Add(ResourcesManager.ParticleHitBasic1);
                    }
                }
            }
        }
    }

    public override bool AddStatusEffect(BattleEntity source, StatusEffect statusEffectToAdd)
    {
        bool addedNew = false;

        if (state == State.UsedForBattle)
        {
            if (statusEffectToAdd is StatusEffect009 && gameManager.OnActivateWeakenSpells != null) //for activating weaken spells
            {
                bool canActivateWeakenSpells = true;
                StatusEffectUpgradeable weak = GetStatusEffect(9) as StatusEffectUpgradeable;

                if (weak != null && weak.Level >= weak.MaxLevel) canActivateWeakenSpells = false;

                void PlayWeakenSpells()
                {
                    weak = GetStatusEffect(9) as StatusEffectUpgradeable;
                    if (weak?.Level >= weak?.MaxLevel) gameManager.OnActivateWeakenSpells?.Invoke();
                    OnAfterAddingStatusEffect -= PlayWeakenSpells;
                    return;
                }

                if (canActivateWeakenSpells) OnAfterAddingStatusEffect += PlayWeakenSpells;
            }

            addedNew = base.AddStatusEffect(source, statusEffectToAdd);

            if (addedNew)
            {
                OnAfterAddingStatusEffect?.Invoke();
                OnAfterGainingEffect?.Invoke();
            }
        }
        else
        {
            StatusIcon i = Instantiate(ResourcesManager.PrefabStatusIcon, statusIconsPanel).GetComponent<StatusIcon>();
            i.InitializeStatusEffectIcon(statusEffectToAdd);
        }

        return addedNew;
    }

    public override void AddTrait(Trait trait)
    {
        if (state == State.UsedForBattle)
        {
            base.AddTrait(trait);

            OnAfterGainingEffect?.Invoke();

        }
        else
        {
            StatusIcon i = Instantiate(ResourcesManager.PrefabStatusIcon, statusIconsPanel).GetComponent<StatusIcon>();
            i.InitializeTraitIcon(trait);
        }
    }

    void ApplyEnemyDataBuffs()
    {
        foreach (Trait trait in enemyData.Traits)
        {
            AddTrait(trait);
        }
        foreach (StatusEffect statusEffect in enemyData.StatusEffects)
        {
            AddStatusEffect(null, statusEffect);
        }
    }

    public List<BattleEntity> GetAttackTarget(Spell.TargetSelectionType targetSelectType)
    {
        List<BattleEntity> attackTarget = new List<BattleEntity>();

        if (CanTargetRandomly)
        {
            attackTarget.AddRange(gameManager.GetRandomEffectTarget(TargetRandomizer, targetSelectType, true));
            if (attackTarget.Count > 0) return attackTarget;
        }

        if (targetSelectType == Spell.TARGET_SELECTION_TYPE_ALL_MINION_SELECT)
        {
            attackTarget.AddRange(gameManager.GetTargetableMinions());
            if (attackTarget.Count > 0) return attackTarget;
        }

        BattleEntity target = gameManager.GetPrioritizedMinionAttackTarget();

        if (target != null)
        {
            attackTarget.Add(target);
            return attackTarget;
        }

        List<Minion> minionsOnField = gameManager.GetMinionsOnField();
        for (int i = 0; i < minionsOnField.Count; i++)
        {
            if (minionsOnField[i].IsTargetable())
            {
                target = minionsOnField[i];
                break;
            }
        }

        if (target == null) if (PlayerCharacter.pc.IsTargetable()) target = PlayerCharacter.pc;
        if (target != null) attackTarget.Add(target);

        return attackTarget;
    }

    public void ModifyDefense(int amount)
    {
        Defense += amount;
    }

    public void ModifyAttack(int amount)
    {
        AttackModifier += amount;
        //Update enemy skill icon
    }

    public void ModifyAttackMultiplier(float amount)
    {
        AttackMultiplier += amount;
        //update damage text
    }

    public void ModifyIncomingDamageReduction(float amount)
    {
        IncomingDamageReduction += amount;
    }

    void InitializeReferences()
    {
        if (!hasInitializedReferences)
        {
            devSetting = gameManager.DevSetting;

            hasInitializedReferences = true;
        }
    }

    void RemoveFromTile()
    {
        transform.SetParent(gameManager.battleStage);
    }

    public void AddAttackModifier(IEffectModifiesAttack attackModifier)
    {
        attackModifiers.Add(attackModifier);
        spellToCast?.EnemySkillIcon?.UpdateText();
        specialSpellToCast?.EnemySkillIcon?.UpdateText();
    }

    public void RemoveAttackModifier(IEffectModifiesAttack attackModifier)
    {
        attackModifiers.Remove(attackModifier);
        spellToCast?.EnemySkillIcon?.UpdateText();
        specialSpellToCast?.EnemySkillIcon?.UpdateText();
    }

    public void AddStatModifier(IEffectModifiesEnemyStats statModifier)
    {
        if (!statModifiers.Contains(statModifier))
        {
            statModifiers.Add(statModifier);

            float totalHealthMultiplier = 0f;
            foreach (IEffectModifiesEnemyStats enemyStatModifier in statModifiers)
            {
                totalHealthMultiplier += enemyStatModifier.HealthMultiplierModifier;
            }

            float currentHealthPercentage = CurrentHealth / MaxHealth;
            int maxHealth = Mathf.RoundToInt(enemyData.Health + (enemyData.Health * totalHealthMultiplier));
            int currentHealth = Mathf.RoundToInt(currentHealthPercentage * maxHealth);

            SetHealth(currentHealth, maxHealth);
        }
    }

    void SetHealth(int currentHealth, int maxHealth)
    {
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        healthBar.Initialize(enemyData.Name, CurrentHealth, MaxHealth);
    }

    public void SetSelectableTarget(bool willDo) //called when selecting enemy for target
    {
        //rework this soon:
        //currently gameManager is using colliders and raycast 2d to get targets
        collider.enabled = willDo;
    }

    public void ChangeTilePosition(int tileIndex, float changeSpeed)
    {
        if (tileIndex < 0) tileIndex = 0;
        else if (tileIndex >= gameManager.enemyTiles.childCount) tileIndex = gameManager.enemyTiles.childCount - 1;

        int currentTileIndex = transform.parent.GetSiblingIndex();
        int tileChangeDir = tileIndex - currentTileIndex;

        if (currentTileIndex != tileIndex)
        {
            Dictionary<int, Enemy> enemiesToChangePos = new Dictionary<int, Enemy>();
            //get enemies to change pos
            for (int i = currentTileIndex + tileChangeDir; (tileChangeDir < 0 && i >= tileIndex) || (tileChangeDir > 0 && i <= tileIndex); i += tileChangeDir)
            {
                Enemy enemy = gameManager.GetEnemy(i);
                if (enemy != null && enemy != this)
                {
                    enemiesToChangePos.Add(i, enemy);
                }
            }

            MoveToTile(tileIndex, changeSpeed);
            foreach (int enemyTileIndex in enemiesToChangePos.Keys)
            {
                enemiesToChangePos[enemyTileIndex].MoveToTile(enemyTileIndex + (tileChangeDir * -1), changeSpeed);
            }
        }



        //if (tileChangeDir < 0) //Going front
        //{
        //    for (int i = currentTileIndex - 1; i >= tileIndex; i--)
        //    {
        //        Enemy enemy = gameManager.GetEnemy(i);
        //        if (enemy != null)
        //        {
        //            enemy.ChangeTilePosition(i + 1, DEFAULT_TILE_CHANGE_SPEED);
        //        }
        //    }
        //}
        //else if (tileChangeDir > 0)
        //{
        //    for (int i = currentTileIndex + 1; i <= tileIndex; i++)
        //    {
        //        Enemy enemy = gameManager.GetEnemy(i);
        //        if (enemy != null)
        //        {
        //            enemy.ChangeTilePosition(i - 1, DEFAULT_TILE_CHANGE_SPEED);
        //        }
        //    }
        //}

        
    }

    void MoveToTile(int index, float moveSpeed)
    {
        transform.SetParent(gameManager.enemyTiles.GetChild(index));
        willChangeTilePos = true;
        tileChangeSpeed = moveSpeed;
    }

    public void AddDeathPreventer(IEffectPreventsDeath deathPreventer)
    {
        if (!deathPreventers.Contains(deathPreventer)) deathPreventers.Add(deathPreventer);
    }

    public void RemoveDeathPreventer(IEffectPreventsDeath deathPreventer)
    {
        if (deathPreventers.Contains(deathPreventer)) deathPreventers.Remove(deathPreventer);
    }

    public void AddActionPreventer(IEffectPreventsActions actionPreventer)
    {
        if (!actionPreventers.Contains(actionPreventer)) actionPreventers.Add(actionPreventer);
    }

    public void RemoveActionPreventer(IEffectPreventsActions actionPreventer)
    {
        if (actionPreventers.Contains(actionPreventer)) actionPreventers.Remove(actionPreventer);
    }

    public void AddTargetRandomizer(IEffectRandomizesTarget targetRandomizer)
    {
        if (!targetRandomizers.Contains(targetRandomizer))
        {
            targetRandomizer.Source = this;
            targetRandomizers.Add(targetRandomizer);
        }
    }

    public void RemoveTargetRandomizer(IEffectRandomizesTarget targetRandomizer)
    {
        if (targetRandomizers.Contains(targetRandomizer))
        {
            targetRandomizers.Remove(targetRandomizer);
        }
    }
}

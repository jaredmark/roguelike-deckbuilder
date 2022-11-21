using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public abstract class BattleEntity : MonoBehaviour
{
    class MaterialColorModifier
    {
        public readonly Color color;
        public readonly float intensity;
        public float currentIntensity;

        public MaterialColorModifier(Color color, float intensity)
        {
            this.color = color;
            this.intensity = intensity;
        }
    }

    public int RecentDamageTaken { get; protected set; }

    public bool IsSlain { get; set; }
    [HideInInspector] public bool untargetable;
    protected bool hasCalledAwake;

    public BattleEntity RecentDamageSource { get; protected set; }
    public BattleEntity Attacker { get; protected set; }

    protected Transform statusIconsPanel;
    protected GameManager gameManager;
    [HideInInspector] public Animator animator;

    public Dictionary<int, StatusEffect> StatusEffects { get; protected set; }
    public Dictionary<int, Trait> Traits { get; protected set; }
    protected Dictionary<int, StatusIcon> TraitIcons { get; set; }

    readonly protected List<IEffectModifiesDefense> defenseModifiers = new List<IEffectModifiesDefense>();
    readonly protected List<IEffectNegatesDamage> damageNegators = new List<IEffectNegatesDamage>();
    readonly protected List<IEffectPrevsNegativEff> negativEffPrevs = new List<IEffectPrevsNegativEff>();

    public bool IsImmune
    {
        get
        {
            foreach (IEffectPrevsNegativEff f in negativEffPrevs) if (f.WillPrevNegativEff) return true;
            return false;
        }
    }

    protected List<int> tempIntList = new List<int>();

    public delegate void OnTakeDamageEvent(ref int damageTaken, BattleEntity attacker);
    public delegate void OnGetDamageRefTypeEvent(ref int damage, DamageData.Type dmgType);
    public delegate void OnAfterAttackGetTargetEvent(List<BattleEntity> targetList);

    public GameManager.VoidEvent OnAfterTakingDamageByAttack;
    public OnTakeDamageEvent OnBeforeTakingCalculatedDamage;
    public GameManager.VoidEvent OnAfterTakingDamage;
    public GameManager.VoidEvent OnBeforeTakingDamageByAttack;
    public OnAfterAttackGetTargetEvent OnAfterAttackGetTarget;
    public GameManager.VoidEvent OnAfterStrike;
    public GameManager.VoidEvent OnAfterTakingSpellDamage;
    public GameManager.OnGetIntEvent OnBeforeTakingDmgGetDmgRefInc;
    public GameManager.OnGetIntEvent OnBeforeTakingDmgGetDmgRefMult;
    public GameManager.OnGetIntEvent OnBeforeTakingDmgGetDmgRefReduc;
    public OnGetDamageRefTypeEvent OnBeforeTakingDmgGetDmgRefTypeInc;
    public GameManager.OnGetEntEvent OnBeforeTakingDmgGetAttacker;
    public GameManager.OnGetEntEvent OnAfterTakingDamageByAttackGetAttacker;
    public GameManager.VoidEvent OnSlain;

    public abstract void TakeBattleDamage(DamageData damageData);
    public abstract void Slay();

    public SpriteRenderer spriteRendererMain { get; private set; }
    public readonly List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
    protected Material defaultMaterial;
    readonly protected List<Material> activeMaterials = new List<Material>();

    float maxColorModifierIntensity;
    readonly List<MaterialColorModifier> materialColorModifiers = new List<MaterialColorModifier>();

    //For fade in/out effect:
    float fadeElapsedTime;
    float fadeDuration;
    Coroutine fadeCor;

    //For damaged effect
    protected const float DEFAULT_DAMAGED_EFFECT_DURATION = 0.15f;
    Coroutine corFlashEffect;
    protected Material onDamagedMaterial;
    bool willPlayDamagedEffect;
    float damagedEffectElapsedTime;
    float damagedEffectDuration;

    protected virtual void Awake()
    {
        if (!hasCalledAwake)
        {
            gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
            animator = GetComponent<Animator>();
            spriteRendererMain = transform.Find("Sprite").GetComponent<SpriteRenderer>();
            spriteRenderers.Add(spriteRendererMain);
            spriteRenderers.Add(transform.Find("Shadow").GetComponent<SpriteRenderer>());

            Traits = new Dictionary<int, Trait>();
            StatusEffects = new Dictionary<int, StatusEffect>();
            TraitIcons = new Dictionary<int, StatusIcon>();
        }
    }

    void LateUpdate()
    {
        ApplyMaterialColorModifier();
    }

    public bool IsTargetable()
    {
        if (!IsSlain && !untargetable)
        {
            return true;
        }

        return false;
    }

    public virtual bool AddStatusEffect(BattleEntity source, StatusEffect statusEffectToAdd) //returns true if added a new effect.
    {
        bool addedNew = false;

        if (IsImmune) { if (statusEffectToAdd._Type == StatusEffect.TYPE_NEGATIVE) return addedNew; }

        bool isAlreadyApplied = false;

        if (StatusEffects.ContainsKey(statusEffectToAdd.ID))
        {
            isAlreadyApplied = true;
        }

        StatusEffect statusEffect = statusEffectToAdd.Duplicate();

        if (!isAlreadyApplied)
        {
            StatusEffects.Add(statusEffect.ID, statusEffect);
            statusEffect.Apply(this);
            StatusIcon statusEffectIcon = Instantiate(ResourcesManager.PrefabStatusIcon, statusIconsPanel).GetComponent<StatusIcon>();
            statusEffectIcon.InitializeStatusEffectIcon(statusEffect);

            addedNew = true;
        }
        else
        {
            if (StatusEffects[statusEffectToAdd.ID] is StatusEffectUpgradeable effectU && effectU.Level < effectU.MaxLevel) addedNew = true;
            else if (statusEffect is StatusEffectStackable) addedNew = true;

            StatusEffects[statusEffectToAdd.ID].Reapply(statusEffect);

            if (this is Enemy) //Update enemy skill damage text
            {
                if (statusEffectToAdd is IEffectModifiesAttack)
                {
                    Enemy enemy = this as Enemy;
                    enemy.spellToCast?.EnemySkillIcon?.UpdateText();
                    enemy.specialSpellToCast?.EnemySkillIcon?.UpdateText();
                }
            }
        }

        if (addedNew)
        {
            if (this is Minion)
            {
                Minion minion = this as Minion;
                gameManager.OnMinionGainStatusEffectGetMinion?.Invoke(minion);
            }
            if (this is Enemy)
            {
                Enemy enemy = this as Enemy;
                gameManager.OnEnemyGainStatusEffect?.Invoke(source, statusEffect);
                gameManager.OnEnemyGainStatusEffectGetEnemy?.Invoke(enemy);
                gameManager.OnEnemyGainStatusEffectGetEnemySource?.Invoke(enemy, source);
                enemy.OnGainStatusEffect?.Invoke(statusEffect);
            }
        }

        return addedNew;
    }

    public void RemoveStatusEffect(StatusEffect statusEffect)
    {
        if (StatusEffects.ContainsKey(statusEffect.ID))
        {
            StatusEffects[statusEffect.ID].Remove();
            Destroy(StatusEffects[statusEffect.ID].StatusEffectIcon.gameObject);
            StatusEffects.Remove(statusEffect.ID);
        }
    }

    public StatusEffect GetStatusEffect(int id)
    {
        if (StatusEffects.ContainsKey(id))
        {
            return StatusEffects[id];
        }

        return null;
    }

    public void ClearAllStatusEffects()
    {
        tempIntList.Clear();
        foreach (int ID in StatusEffects.Keys)
        {
            tempIntList.Add(ID);
        }

        for (int i = tempIntList.Count - 1; i >= 0; i--)
        {
            RemoveStatusEffect(StatusEffects[tempIntList[i]]);
        }
    }

    public virtual void AddTrait(Trait trait)
    {
        if (!Traits.ContainsKey(trait.ID))
        {
            Trait traitToApply = trait.Duplicate();

            traitToApply.Apply(this);
            Traits.Add(traitToApply.ID, traitToApply);
            StatusIcon traitIcon = Instantiate(ResourcesManager.PrefabStatusIcon, statusIconsPanel).GetComponent<StatusIcon>();
            traitIcon.InitializeTraitIcon(traitToApply);
            TraitIcons.Add(traitToApply.ID, traitIcon);
        }
    }

    public void RemoveTrait(Trait trait)
    {
        if (Traits.ContainsKey(trait.ID))
        {
            trait.Remove();
            Traits.Remove(trait.ID);
            Destroy(TraitIcons[trait.ID].gameObject);
            TraitIcons.Remove(trait.ID);
        }
    }

    public Trait GetTrait(int id)
    {
        if (Traits.ContainsKey(id))
        {
            return Traits[id];
        }

        return null;
    }

    public void ClearAllTraits()
    {
        tempIntList.Clear();

        foreach (int ID in Traits.Keys)
        {
            tempIntList.Add(ID);
        }

        for (int i = tempIntList.Count - 1; i >= 0; i--)
        {
            RemoveTrait(Traits[tempIntList[i]]);
        }
    }

    public void AddDefenseModifier(IEffectModifiesDefense defenseModifier)
    {
        if (!defenseModifiers.Contains(defenseModifier)) defenseModifiers.Add(defenseModifier);
    }

    public void RemoveDefenseModifier(IEffectModifiesDefense defenseModifier)
    {
        defenseModifiers.Remove(defenseModifier);
    }
    
    public void AddDamageNegator(IEffectNegatesDamage damageNegator)
    {
        if (!damageNegators.Contains(damageNegator)) damageNegators.Add(damageNegator);
    }

    public void RemoveDamageNegator(IEffectNegatesDamage damageNegator)
    {
        damageNegators.Remove(damageNegator);
    }

    public void AddNegativEffPrev(IEffectPrevsNegativEff f)
    {
        if (!negativEffPrevs.Contains(f)) negativEffPrevs.Add(f);
    }

    public void RemoveNegativEffPrev(IEffectPrevsNegativEff f)
    {
        if (negativEffPrevs.Contains(f)) negativEffPrevs.Remove(f);
    }

    protected void SetMaterial(Material material)
    {
        ChangeSpriteRendererMaterial(material);
        foreach (Material mat in activeMaterials)
        {
            if (mat.name == material.name)
            {
                return;
            }
        }

        activeMaterials.Add(material);
    }

    protected void UnsetMaterial(Material material)
    {
        for (int i = activeMaterials.Count - 1; i >= 0; i--)
        {
            if (activeMaterials[i].name == material.name)
            {
                activeMaterials.RemoveAt(i);

                if (activeMaterials.Count > 0) ChangeSpriteRendererMaterial(activeMaterials[activeMaterials.Count - 1]);
                else ChangeSpriteRendererMaterial(defaultMaterial);

                return;
            }
        }

        Debug.LogWarning("Material not removed. Material name: " + material.name);
    }

    protected void ChangeSpriteRendererMaterial(Material material) //Use this to change the spriteRenderer/s material.
    {
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer.gameObject.name == "Shadow") continue;
            spriteRenderer.material = material;
        }
    }

    protected void ChangeSpriteRendererColor(Color color)
    {
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            spriteRenderer.color = color;
        }
    }

    public void PlayBlinkEffect(Color color, float intensity, float duration, float fadeInDuration, float fadeOutStart)
    {
        IEnumerator PlayBlinkEffect()
        {
            MaterialColorModifier matColorModifier = new MaterialColorModifier(color, intensity);
            materialColorModifiers.Add(matColorModifier);

            float timeElapsed = 0f;
            defaultMaterial.SetInt("_WillModifyColor", 1);

            while (true)
            {
                timeElapsed += Time.deltaTime;
                if (timeElapsed >= duration) break;

                if (timeElapsed < fadeInDuration)
                {
                    matColorModifier.currentIntensity = Mathf.Clamp((float)System.Math.Round(Mathf.Sin((((timeElapsed / fadeInDuration) * 0.5f) * Mathf.PI)), 2), 0f, 1f) * intensity;
                }
                else if (timeElapsed >= fadeOutStart)
                {
                    matColorModifier.currentIntensity = Mathf.Clamp((float)System.Math.Round(Mathf.Sin((((((timeElapsed - fadeOutStart) / (duration - fadeOutStart)) * 0.5f) + 0.5f) * Mathf.PI)), 2), 0f, 1f) * intensity;
                }
                else
                {
                    matColorModifier.currentIntensity = matColorModifier.intensity;
                }

                yield return null;
            }

            materialColorModifiers.Remove(matColorModifier);
            if (materialColorModifiers.Count == 0) defaultMaterial.SetInt("_WillModifyColor", 0);
        }

        StartCoroutine(PlayBlinkEffect());
    }

    public void PlayBlinkEffect(Color color, float intensity, float fadeInDuration)
    {
        IEnumerator PlayBlinkEffect()
        {
            MaterialColorModifier matColorModifier = new MaterialColorModifier(color, intensity);
            materialColorModifiers.Add(matColorModifier);

            float timeElapsed = 0f;
            defaultMaterial.SetInt("_WillModifyColor", 1);

            while (materialColorModifiers.Contains(matColorModifier))
            {
                timeElapsed += Time.deltaTime;

                if (timeElapsed < fadeInDuration)
                {
                    matColorModifier.currentIntensity = Mathf.Clamp((float)System.Math.Round(Mathf.Sin((((timeElapsed / fadeInDuration) * 0.5f) * Mathf.PI)), 2), 0f, 1f) * intensity;
                }
                else
                {
                    matColorModifier.currentIntensity = matColorModifier.intensity;
                }

                yield return null;
            }

            if (materialColorModifiers.Count == 0) defaultMaterial.SetInt("_WillModifyColor", 0);
        }

        StartCoroutine(PlayBlinkEffect());
    }

    void SetMaxColorModifierIntensity()
    {
        maxColorModifierIntensity = 0f;

        foreach (MaterialColorModifier matColorModifier in materialColorModifiers)
        {
            if (matColorModifier.currentIntensity > maxColorModifierIntensity) maxColorModifierIntensity = matColorModifier.currentIntensity;
        }
    }

    void ApplyMaterialColorModifier()
    {
        if (materialColorModifiers.Count > 0)
        {
            Color currentColor = materialColorModifiers[materialColorModifiers.Count - 1].color;
            defaultMaterial.SetColor("_Color", currentColor);

            float currentMatColorModifierIntensity = 0f;

            foreach (MaterialColorModifier matColorModifier in materialColorModifiers)
            {
                currentMatColorModifierIntensity += matColorModifier.currentIntensity;
            }

            SetMaxColorModifierIntensity();
            defaultMaterial.SetFloat("_ColorIntensity", Mathf.Clamp(currentMatColorModifierIntensity, 0f, maxColorModifierIntensity));
        }
    }

    public void PlayFadeInEffect(float duration)
    {
        IEnumerator PlayFadeInEffect()
        {
            Dictionary<SpriteRenderer, float> currAlphas = new Dictionary<SpriteRenderer, float>();
            foreach (SpriteRenderer spriteRenderer in spriteRenderers)
            {
                currAlphas.Add(spriteRenderer, spriteRenderer.color.a);
            }

            while (true)
            {
                fadeElapsedTime += Time.deltaTime;
                if (fadeElapsedTime >= duration) break;
                
                foreach (SpriteRenderer spriteRenderer in spriteRenderers)
                {
                    float currentSpriteAlpha = (Mathf.Sin((((fadeElapsedTime / fadeDuration) * 0.5f) * Mathf.PI)) * (1f - currAlphas[spriteRenderer])) + currAlphas[spriteRenderer];
                    spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, currentSpriteAlpha);
                }

                yield return null;
            }

            foreach (SpriteRenderer spriteRenderer in spriteRenderers)
            {
                spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1f);
            }
            fadeCor = null;
        }

        fadeElapsedTime = 0f;
        fadeDuration = duration;

        if (fadeCor != null) StopCoroutine(fadeCor);
        fadeCor = StartCoroutine(PlayFadeInEffect());
    }

    public void PlayFadeOutEffect(float duration)
    {
        IEnumerator PlayFadeOutEffect()
        {
            Dictionary<SpriteRenderer, float> currAlphas = new Dictionary<SpriteRenderer, float>();
            foreach (SpriteRenderer spriteRenderer in spriteRenderers)
            {
                currAlphas.Add(spriteRenderer, spriteRenderer.color.a);
            }

            while (true)
            {
                fadeElapsedTime += Time.deltaTime;
                if (fadeElapsedTime >= duration) break;

                foreach (SpriteRenderer spriteRenderer in spriteRenderers)
                {
                    float currentSpriteAlpha = currAlphas[spriteRenderer] - (Mathf.Sin((((fadeElapsedTime / fadeDuration) * 0.5f) * Mathf.PI)) * currAlphas[spriteRenderer]);
                    spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, currentSpriteAlpha);
                }

                yield return null;
            }

            foreach (SpriteRenderer spriteRenderer in spriteRenderers)
            {
                spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 0f);
            }
            fadeCor = null;
        }

        fadeElapsedTime = 0f;
        fadeDuration = duration;

        if (fadeCor != null) StopCoroutine(fadeCor);
        fadeCor = StartCoroutine(PlayFadeOutEffect());
    }

    protected void PlayFlashEffect(float duration)
    {
        IEnumerator PlayFlashEffect()
        {
            while (willPlayDamagedEffect)
            {
                if (damagedEffectElapsedTime >= damagedEffectDuration)
                {
                    UnsetMaterial(onDamagedMaterial);
                    corFlashEffect = null;
                    willPlayDamagedEffect = false;
                }
                else damagedEffectElapsedTime += Time.deltaTime;

                yield return null;
            }
        }

        SetMaterial(onDamagedMaterial);
        willPlayDamagedEffect = true;
        damagedEffectElapsedTime = 0f;
        damagedEffectDuration = duration;

        if (corFlashEffect == null) corFlashEffect = StartCoroutine(PlayFlashEffect());
    }

    protected void PlayDeathEffect(float duration)
    {
        IEnumerator PlayEffect(float _duration)
        {
            float dur = _duration;
            float timeElapsed = 0f;

            defaultMaterial.SetInt(StringManager.MATERIAL_STRING_MINION_WILL_DISSOLVE, 1);
            while (true)
            {
                timeElapsed += Time.deltaTime;
                if (timeElapsed > dur) break;
                defaultMaterial.SetFloat(StringManager.MATERIAL_STRING_MINION_DISSOLVE_AMOUNT, timeElapsed / dur);
                yield return null;
            }
            //defaultMaterial.SetInt(StringManager.MATERIAL_STRING_MINION_WILL_DISSOLVE, 0);
        }

        Particle.PlayParticleWithoutEffect(ResourcesManager.PrefabDictionaryParticle[7], this);
        PlayFadeOutEffect(duration);
        animator.speed = 0f;
        SetMaterial(defaultMaterial);
        StartCoroutine(PlayEffect(duration));
    }

    public bool OverridesAnimation(string overridedAnimName)
    {
        AnimatorOverrideController AOC = animator.runtimeAnimatorController as AnimatorOverrideController;
        if (AOC != null)
        {
            List<KeyValuePair<AnimationClip, AnimationClip>> animOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            AOC.GetOverrides(animOverrides);

            foreach (KeyValuePair<AnimationClip, AnimationClip> animOverride in animOverrides)
            {
                if (animOverride.Key.name == overridedAnimName)
                {
                    if (animOverride.Value != null) return true;
                    else return false;
                }
            }
        }

        return false;
    }
}

public class Minion : BattleEntity
{
    public const float DURATION_ON_SUMMON_BLNK_DEFAULT = 0.4f;

    public float attackTriggerTime;
    public float spellCastTriggerTime;
    [SerializeField] float slainFadeOutDuration = 0.3f;
    [SerializeField] float exitFadeOutDuration = 0.3f;

    public enum State { None, UsedInBattle, UsedForSelection }
    [HideInInspector] public State state = State.None;
    
    public CardData OriginalCardData { get; private set; }
    public CardData CurrentCardData { get; private set; }
    public MinionCardData minionCardData { get; private set; }

    public CardData.Type Type { get; private set; }
    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public int Strikes { get; private set; }
    int prevAttack, prevDefense;

    AnimationClip AnimIdle { get; set; }
    AnimationClip AnimAttack { get; set; }
    AnimationClip AnimDefend { get; set; }
    AnimationClip AnimSlain { get; set; }
    AnimationClip AnimCastSpell { get; set; }

    readonly float colliderMargin = 0.5f;
    public readonly int defaultAttackModifier = 0;
    public readonly float defaultAttackMultiplier = 1.0f;
    public readonly float defaultAttackReduction = 0;
    public readonly float defaultIncomingDamageMultiplier = 1.0f;
    public readonly float defaultIncomingDamageReduction = 0;

    Spell selectedSpell;
    [HideInInspector] public bool isSelected;
    [HideInInspector] public bool attacksEnemyBehind;
    [HideInInspector] public bool isPrioritizedForAttack;
    [HideInInspector] public bool isDisarmed;
    [HideInInspector] public bool willAttackAllEnemies;
    [HideInInspector] public bool isTemporary;
    [HideInInspector] public bool willNotDespawnThisTurn;
    bool willVanishIfSlain;
    [HideInInspector] public bool isSlow;
    public bool HasDespawned { get; private set; }
    public bool HasPreparedBattleAction { get; private set; }
    public bool canAttack { get; private set; }
    bool isDefending;
    bool canChooseActions;
    bool hasFinishedTurn;
    bool isInteractable;
    bool canDefend = true;
    bool hasInitializedSlainSpell;
    bool hasCastedSpell;
    bool hasPlayedSlainSpells;
    bool willDespawn;
    bool isVanished;
    bool hasLeftBattle;

    bool WillReturnWhenSlain {
        get {
            foreach (IEffectReturnsMinionWhenSlain effect in effectListReturnMinionWhenSlain)
            {
                if (effect.WillReturnMinionWhenSlain) return true;
            }

            return false;
        }
    }
    //
    public readonly List<IEffectModifiesCriticalStrike> criticalStrikeModifiers = new List<IEffectModifiesCriticalStrike>();
    readonly List<IEffectModifiesAttack> attackModifiers = new List<IEffectModifiesAttack>();
    readonly List<IEffectModifiesDefGained> defenseGainedModifiers = new List<IEffectModifiesDefGained>();
    public readonly List<IEffectHasOnHitEffects> onHitEffects = new List<IEffectHasOnHitEffects>();
    public readonly List<IEffectReturnsMinionWhenSlain> effectListReturnMinionWhenSlain = new List<IEffectReturnsMinionWhenSlain>();
    readonly List<IEffectPreventsDeath> deathPreventers = new List<IEffectPreventsDeath>();
    readonly List<IEffectPreventsActions> actionPreventers = new List<IEffectPreventsActions>();
    readonly List<IEffectPreventsDespawn> despawnPreventers = new List<IEffectPreventsDespawn>();
    readonly List<IEffectPreventsIncreasingDef> defIncPrevs = new List<IEffectPreventsIncreasingDef>();
    readonly List<IEffectPreventsDirectDamage> dirDamagePrevs = new List<IEffectPreventsDirectDamage>();
    readonly List<IEffectVanishesCard> cardVanishers = new List<IEffectVanishesCard>();
    readonly List<IEffectPreventsAtkReduc> atkReducPrevs = new List<IEffectPreventsAtkReduc>();

    bool WillPreventAtkReduc
    {
        get
        {
            foreach (IEffectPreventsAtkReduc f in atkReducPrevs) if (f.WillPreventAtkReduc) return true;
            return false;
        }
    }

    public bool IsVanishing
    {
        get
        {
            if (isTemporary) return true;
            foreach (IEffectVanishesCard f in cardVanishers) if (f.WillVanishCard) return true;
            return false;
        }
    }

    bool WillPrevDirDmg
    {
        get
        {
            foreach (IEffectPreventsDirectDamage eff in dirDamagePrevs) if (eff.WillPreventDirectDamage) return true;
            return false;
        }
    }

    public bool CanIncreaseDef
    {
        get
        {
            foreach (IEffectPreventsIncreasingDef prev in defIncPrevs) if (prev.WillPreventIncreasingDef) return false;
            return true;
        }
    }

    bool CanPlayNextTurn
    {
        get
        {
            if (willNotDespawnThisTurn) return true;
            foreach (IEffectPreventsDespawn despawnPreventer in despawnPreventers) if (despawnPreventer.WillPreventDespawn) return true;
            return false;
        }
    }

    bool CannotBeSlainImmediately
    {
        get
        {
            foreach (IEffectPreventsDeath dp in deathPreventers) if (dp.DeathPrevType == Spell.DeathPrevType.Immediately && dp.WillPreventDeath) return true;
            return false;
        }
    }

    bool CannotBeSlainSlayFirst
    {
        get
        {
            foreach (IEffectPreventsDeath dp in deathPreventers) if (dp.DeathPrevType == Spell.DeathPrevType.SlayFirst && dp.WillPreventDeath) return true;
            return false;
        }
    }

    bool CanPerformActions
    {
        get
        {
            foreach (IEffectPreventsActions actionPreventer in actionPreventers) if (actionPreventer.WillPreventAction) return false;
            return true;
        }
    }

    readonly List<Spell> slainSpells = new List<Spell>();

    public delegate void OnBeforeDealingAttackDamageEvent(ref int damage, BattleEntity attackTarget);
    public delegate void OnBeforeStrikeGetTargetEvent(BattleEntity attackTarget);
    public delegate void OnAfterTakingDamageEvent(BattleEntity attacker);
    public delegate void OnAfterCalculatingOutgoingAttackDamageEvent(ref int attack);
    public delegate void OnGetAttackedTargetEvent(BattleEntity attackTarget);

    GameManager.VoidEvent OnBeforePlayingTurn;
    GameManager.VoidEvent OnMinionSlain;
    public GameManager.VoidEvent OnFinishedStrike;
    public GameManager.VoidEvent OnCheckCanPlayTurn;
    public GameManager.VoidEvent OnGainDefense;
    public OnBeforeDealingAttackDamageEvent OnBeforeDealingAttackDamage;
    public OnBeforeStrikeGetTargetEvent OnBeforeStrikeGetTarget;
    public GameManager.VoidEvent OnBeforeAttack;
    public OnAfterTakingDamageEvent OnAfterTakingDamageGetAttacker;
    public OnAfterCalculatingOutgoingAttackDamageEvent OnAfterCalculatingOutgoingAttackDamage;
    public OnGetAttackedTargetEvent OnGetAttackedTarget;
    public GameManager.OnBeforeTriggeringMarkEvent OnBeforeTriggeringMark;
    public GameManager.VoidEvent OnSpendTurn;
    public GameManager.VoidEvent OnModifyAtk;

    List<BattleEntity> prioTargets;

    UIHandler uiHandler;
    public new BoxCollider2D collider { get; private set; }
    public SummoningTile SummoningTile { get; private set; }

    TextMeshProUGUI textAttack, textDefense;
    PlayerCharacter playerCharacter;
    Transform enemyTiles, attackIcon, defenseIcon, battleStage;
    Transform statsDisplay;

    protected override void Awake()
    {
        if (!hasCalledAwake)
        {
            base.Awake();
            uiHandler = GameManager.UiHandler;
            collider = GetComponent<BoxCollider2D>();
            statsDisplay = transform.Find("Canvas").Find("StatsDisplay");
            attackIcon = statsDisplay.Find("AttackIcon").transform;
            defenseIcon = statsDisplay.Find("DefenseIcon").transform;
            textAttack = attackIcon.Find("Text").GetComponent<TextMeshProUGUI>();
            textDefense = defenseIcon.Find("Text").GetComponent<TextMeshProUGUI>();
            playerCharacter = PlayerCharacter.pc;
            enemyTiles = gameManager.enemyTiles;
            battleStage = gameManager.battleStage;
            statusIconsPanel = statsDisplay.Find("StatusIcons").transform;

            defaultMaterial = Instantiate(MaterialManager.MaterialEntity);
            onDamagedMaterial = Instantiate(MaterialManager.MaterialBrightFlash);
            ChangeSpriteRendererMaterial(defaultMaterial);

            hasCalledAwake = true;
        }
    }

    public void Initialize(CardData OriginalCardData, CardData CurrentCardData)
    {
        Awake();
        ClearAllEvents();

        RecentDamageTaken = 0;

        IsSlain = false;
        hasFinishedTurn = false;
        hasInitializedSlainSpell = false;
        HasPreparedBattleAction = false;
        hasCastedSpell = false;
        HasDespawned = false;
        hasPlayedSlainSpells = false;
        Attacker = null;

        this.OriginalCardData = OriginalCardData;
        this.CurrentCardData = CurrentCardData;
        minionCardData = CurrentCardData as MinionCardData;

        //
        if (minionCardData.minionType == MinionCardData.MINIONTYPE_SUPPORT)
        {
            canAttack = false;
            Attack = 0;
            Defense = minionCardData.Defense;
            Strikes = 0;
            prevAttack = Attack;
            prevDefense = Defense;

            textDefense.SetText(Defense.ToString());
        }
        else
        {
            canAttack = true;
            Attack = minionCardData.Attack;
            Defense = minionCardData.Defense;
            Strikes = 1;
            prevAttack = Attack;
            prevDefense = Defense;

            textAttack.SetText(Attack.ToString());
            textDefense.SetText(Defense.ToString());
        }
        //

        SetRendererLayerOrder();

        InitializeSpells();
        InitializeAttackHitParticles();

        ResizeCollider();
        Reposition();
        ShowStatsDisplay();
        SetInteractable(true);
    }

    public void SetState(State state)
    {
        if (this.state != state)
        {
            if (state == State.UsedInBattle)
            {
                SetGlowActive(false);
                SetHighlightActive(false);
                SetFaintHighlightActive(false);
            }
            else if (state == State.UsedForSelection)
            {
                SetGlowActive(true);
            }

            this.state = state;
        }
    }

    public void PlaySummonEffect(float duration)
    {
        PlayBlinkEffect(new Color(0.3f, 0.9f, 0.9f, 1f), 0.8f, duration, duration * 0.375f, duration / 2f);
        PlayFadeInEffect(duration * 0.66f);
    }

    public void PlayEntryAnimation()
    {
        if (OverridesAnimation(StringManager.OVERRIDED_ANIM_NAME_MINION_ENTRY)) animator.Play(StringManager.ANIM_STATE_NAME_MINION_ENTRY, 0);
    }

    public void PlayExitEffect()
    {
        Particle.PlayParticleWithoutEffect(ResourcesManager.PrefabDictionaryParticle[9], this);
        PlayBlinkEffect(new Color(0.68f, 0.94f, 1f, 1f), 0.4f, exitFadeOutDuration / 2f);
        PlayFadeOutEffect(exitFadeOutDuration);
        //IEnumerator FadeOut()
        //{
        //    yield return new WaitForSeconds(exitFadeOutDuration * 0.33f);
        //    PlayFadeOutEffect(exitFadeOutDuration * 0.66f);
        //}

        //StartCoroutine(FadeOut());
    }

    void SetGlowActive(bool willDo)
    {
        if (willDo)
        {
            defaultMaterial.SetInt(StringManager.MATERIAL_STRING_MINION_IS_GLOWING, 1);
        }
        else
        {
            defaultMaterial.SetInt(StringManager.MATERIAL_STRING_MINION_IS_GLOWING, 0);
        }
    }

    public void SetHighlightActive(bool willDo) //the highlight effect when the minion is hovered
    {
        if (willDo)
        {
            defaultMaterial.SetInt(StringManager.MATERIAL_STRING_MINION_IS_HOVERED, 1);
        }
        else
        {
            defaultMaterial.SetInt(StringManager.MATERIAL_STRING_MINION_IS_HOVERED, 0);
        }
    }

    public void SetFaintHighlightActive(bool willDo) //faint hightlight when the minion is pressed, the lighter highlight wont work if this is active
    {
        if (willDo)
        {
            defaultMaterial.SetInt(StringManager.MATERIAL_STRING_MINION_IS_PRESSED, 1);
        }
        else
        {
            defaultMaterial.SetInt(StringManager.MATERIAL_STRING_MINION_IS_PRESSED, 0);
        }
    }

    public void SetInteractable(bool willDo)
    {
        isInteractable = willDo;
    }

    public override void TakeBattleDamage(DamageData damageData)
    {
        OnBeforeTakingDmgGetAttacker?.Invoke(damageData.source);

        int amount = GetCalculatedIncomingDamage(damageData);

        BattleEntity attacker = damageData.source;

        OnBeforeTakingCalculatedDamage?.Invoke(ref amount, attacker);

        Defense -= amount;
        RecentDamageSource = attacker;
        Attacker = attacker;
        RecentDamageTaken = amount;

        if (!WillPrevDirDmg)
        {
            if (Defense < 0)
            {
                int takenDamageByPlayer = Mathf.Abs(Defense);
                damageData.ModifyAmount(takenDamageByPlayer);
                playerCharacter.TakeBattleDamage(damageData);
                playerCharacter.RecentAttackDamageTaken = takenDamageByPlayer;
                Defense = 0;
            }
            else
            {
                playerCharacter.RecentAttackDamageTaken = 0;
            }
        }
        else if (Defense < 0)
        {
            Defense = 0;
        }

        UpdateStatValues();

        if (damageData.type == DamageData.TYPE_NORMAL)
        {
            OnAfterTakingDamageByAttack?.Invoke();
            OnAfterTakingDamageByAttackGetAttacker?.Invoke(attacker);
        }
        OnAfterTakingDamageGetAttacker?.Invoke(attacker);
        OnAfterTakingDamage?.Invoke();
        gameManager.OnMinionAfterTakingDamageGetAttacker?.Invoke(attacker);

        PlayFlashEffect(DEFAULT_DAMAGED_EFFECT_DURATION);

        bool cannotBeSlain = CannotBeSlainImmediately || damageData.cannotKill;

        if (Defense <= 0 && amount != 0 && !cannotBeSlain && damageData.isLastHit) //When slain
        {
            if (damageData.willBanishTargetIfSlain) willVanishIfSlain = true;
            Slay();
        }
        else if (damageData.canExecute)
        {
            Slay();
        }
    }

    public override void Slay()
    {
        //if ((isVanishing || willVanishIfSlain) && !WillReturnWhenSlain)
        //{
        //    Vanish();
        //}
        if (!willDespawn)
        {
            //play slain spells if has any:
            //if (slainSpells.Count > 0 && !hasPlayedSlainSpells)
            //{
                
            //    gameManager.OnMinionSlain?.Invoke();
            //    gameManager.OnMinionSlainGetMinion?.Invoke(this);

            //    GameManager.PlayActionAtFront(new ActionSlay(this));

            //    hasPlayedSlainSpells = true;
            //    return;
            //}

            if (!hasPlayedSlainSpells)
            {
                bool hasActionsToPlay = false;
                bool cannotBeSlainSlayFirst = CannotBeSlainSlayFirst;

                if (cannotBeSlainSlayFirst)
                {
                    List<IEffectPreventsDeath> dpl = new List<IEffectPreventsDeath>(deathPreventers);
                    dpl.Reverse();
                    for (int i = dpl.Count - 1; i >= 0; i--) if (dpl[i].WillPreventDeath && dpl[i].DeathPrevType == Spell.DeathPrevType.SlayFirst) dpl[i].OnPreventDeath?.Invoke(Attacker);
                }

                if (slainSpells.Count > 0)
                {
                    foreach (Spell spell in slainSpells)
                    {
                        PlaySlainSpell(spell);
                    }
                }

                if (cannotBeSlainSlayFirst) //if cannot be slain, dont slay
                {
                    return;
                }
                else
                {
                    IsSlain = true;

                    if (Attacker is Enemy e)
                    {
                        e.OnSlayMinion?.Invoke();
                    }

                    OnSlain?.Invoke();
                    gameManager.OnMinionSlain?.Invoke();
                    gameManager.OnMinionSlainGetMinion?.Invoke(this);
                    gameManager.OnMinionSlainGetMinionSource?.Invoke(this, Attacker);

                    //Check if this unit has actions in action queue & doesnt have its slay action:
                    if (GameManager.GetBattleEntityActionCount(this) > 0)
                    {
                        GameManager.PlayActionBehindLastAction(new ActionSlay(this, this), this);
                        hasActionsToPlay = true;
                    }

                    hasPlayedSlainSpells = true;
                    if (hasActionsToPlay) return;
                }
            }

            willDespawn = true;

            if (WillReturnWhenSlain)
            {
                ReturnToHand();
                return;
            }
            else if (IsVanishing || willVanishIfSlain)
            {
                Vanish();
            }
            else //When slain normally
            {
                gameManager.SendCardDataToDiscardPile(OriginalCardData);
                PlayActionRemoveObject();
            } 
            //freeze hurt anim
            //disable collider
            //hide battle texts
            // after fadeout anim remove from play
        }
    }

    public void LeaveBattle()
    {
        if (IsVanishing)
        {
            Vanish();
        }
        else
        {
            //   !
            willDespawn = true;
            IsSlain = true;
            hasLeftBattle = true;
            //   !
            // after fadeout anim remove from play

            gameManager.SendCardDataToDiscardPile(OriginalCardData);
            PlayActionRemoveObject();
        }
    }

    public void SendOnTopOfDeck()
    {
        willDespawn = true;
        IsSlain = true;
        hasLeftBattle = true;

        gameManager.SendCardDataOnTopOfDeck(OriginalCardData);
        PlayActionRemoveObject();
    }

    public void Vanish()
    {
        willDespawn = true;
        IsSlain = true;
        hasLeftBattle = true;

        if (PlayerCharacter.pc.WillBlockVanishing)
        {
            gameManager.SendCardDataToDiscardPile(OriginalCardData);
            PlayerCharacter.pc.OnBlockVanishing();
        }
        else
        {
            isVanished = true;
            gameManager.SendCardDataToVanishedPile(OriginalCardData);
        }

        PlayActionRemoveObject();
    }

    public void ReturnToHand()
    {
        IsSlain = true;
        gameManager.SpawnCardInBattle(OriginalCardData);
        PlayActionRemoveObject();
    }

    public void ReturnToHandImmediately()
    {
        IsSlain = true;
        gameManager.SpawnCardInBattle(OriginalCardData);
        RemoveObject();
    }

    void PlayActionRemoveObject()
    {
        if (gameManager.IsInBattle)
        {
            Action actRemove = new ActionRemoveObject(this, this);
            if (!GameManager.PlayActionBehindLastAction(actRemove, this))
            {
                GameManager.PlayHighPriorityActionAtFront(actRemove);
            }          
        }
    }

    public void RemoveObject()
    {
        float removeObjTime = 0f;
        if (hasLeftBattle)
        {
            PlayExitEffect();
            removeObjTime = exitFadeOutDuration;
        }
        else if (IsSlain)
        {
            //play effect
            PlayDeathEffect(slainFadeOutDuration);
            removeObjTime = slainFadeOutDuration;
        }
        else removeObjTime = GameManager.GetAnimationLength(animator, "MinionFadeOut");

        //Clear values
        HasDespawned = true;

        gameManager.OnMinionLeftGetMinion?.Invoke(this);
        gameManager.OnMinionLeft?.Invoke();

        ClearAllTraits();
        ClearAllAttackOnHitEffects();
        ClearAllStatusEffects();
        RemoveFromSummoningTile();
        RemoveSpellPassive();

        collider.enabled = false;
        DestroyStatsDisplay();

        IEnumerator RemoveObject(float timeToRemove)
        {
            yield return new WaitForSeconds(timeToRemove);
            Destroy(gameObject);
        }

        StartCoroutine(RemoveObject(removeObjTime));
    }

    public void Sacrifice()
    {
        gameManager.IncreaseSacrificeCount();
        gameManager.UpdateCardsText();
        Attacker = null;
        Slay();
        GameManager.PlayActionBehindLastAction(new ActionApplyStatusEffect(this, ResourcesManager.ParticleGainDefenseDefault, PlayerCharacter.pc, DataLibrary.soul, 0f), this);
    }

    int GetCalculatedIncomingDamage(DamageData damageData)
    {
        int amount = damageData.amount;

        int incomingDamageReduction = 0;
        float incomingDamageMultiplier = defaultIncomingDamageMultiplier;
        float incomingDamageReductionMultiplier = defaultIncomingDamageReduction;

        foreach (IEffectModifiesDefense defenseModifier in defenseModifiers)
        {
            incomingDamageReduction += defenseModifier.IncomingDamageReduction;
            incomingDamageMultiplier += defenseModifier.IncomingDamageMultiplier;
            incomingDamageReductionMultiplier += defenseModifier.IncomingDamageReductionMultiplier;
        }

        OnBeforeTakingDmgGetDmgRefInc?.Invoke(ref amount); //Flat Damage increases

        amount = Mathf.RoundToInt((amount) * (incomingDamageMultiplier + 0.001f)); //Damage multipliers
        OnBeforeTakingDmgGetDmgRefMult?.Invoke(ref amount);

        if (!damageData.isAbsolute)
        {
            amount -= incomingDamageReduction; //flat reduction
            OnBeforeTakingDmgGetDmgRefReduc?.Invoke(ref amount);

            amount = Mathf.RoundToInt(amount - (amount * (incomingDamageReductionMultiplier + 0.001f))); //percentage reduction
        }
  
        if (amount < 0) amount = 0;
        return amount;
    }

    public int GetCalculatedOutgoingAttackDamage()
    {
        //check status effects
        int baseAttack = Attack + defaultAttackModifier;
        float attackMultiplier = defaultAttackMultiplier;
        float attackReduction = defaultAttackReduction;

        foreach (IEffectModifiesAttack attackModifier in attackModifiers)
        {
            if (WillPreventAtkReduc && attackModifier.AttackModifier < 0) { /*do nothing*/}
            else baseAttack += attackModifier.AttackModifier;

            if (WillPreventAtkReduc && attackModifier.AttackMultiplierModifier < 0f) { /*do nothing*/}
            else attackMultiplier += attackModifier.AttackMultiplierModifier;

            if (WillPreventAtkReduc && attackModifier.AttackReductionModifier > 0f) { /*do nothing*/}
            else attackReduction += attackModifier.AttackReductionModifier;
        }
           // + AttackModifier;
        float calculatedDamageOutput = baseAttack * (attackMultiplier + 0.001f);
        int calculatedDamageOutputInt = Mathf.RoundToInt(calculatedDamageOutput - (calculatedDamageOutput * (attackReduction + 0.001f)));

        return calculatedDamageOutputInt;
    }

    public void UpdateStatValues()
    {
        int currAttack = GetCalculatedOutgoingAttackDamage();

        if (prevAttack != currAttack)
        {
            textAttack.SetText(currAttack.ToString());
            OnModifyAtk?.Invoke();
        }

        if (prevDefense != Defense)
        {
            textDefense.SetText(Defense.ToString());
        }

        prevAttack = currAttack;
        prevDefense = Defense;
    }

    public bool CanAttack()
    {
        if (canAttack && GetCalculatedOutgoingAttackDamage() > 0 && !isDisarmed && CanPerformActions) //dont put isSlain
        {
            return true;
        }

        return false;
    }

    public List<BattleEntity> GetAttackTarget()
    {
        List<BattleEntity> targets = new List<BattleEntity>();

        if (CanAttackAllEnemies())
        {
            foreach (Enemy enemy in gameManager.GetEnemiesOnField())
            {
                if (enemy.IsTargetable()) targets.Add(enemy);
            }
            return targets;
        }

        //if has prioritized targets
        BattleEntity target = gameManager.GetPrioritizedEnemyAttackTarget();
        if (target != null)
        {
            targets.Add(target);
            return targets;
        }

        if (prioTargets != null && prioTargets.Count > 0)
        {
            targets.AddRange(prioTargets);
            return targets;
        }

        if (attacksEnemyBehind)
        {
            Enemy enemy = gameManager.GetEnemyBehind();
            if (enemy.IsTargetable()) targets.Add(enemy);
            else
            {
                List<Enemy> enemiesOnField = gameManager.GetEnemiesOnField();
                for (int i = enemiesOnField.Count - 1; i >= 0; i--)
                {
                    if (enemiesOnField[i].IsTargetable())
                    {
                        targets.Add(enemiesOnField[i]);
                        break;
                    }
                }
            }
            return targets;
        }

        List<Enemy> _enemiesOnField = gameManager.GetEnemiesOnField();
        for (int i = 0; i < _enemiesOnField.Count; i++)
        {
            if (_enemiesOnField[i].IsTargetable())
            {
                targets.Add(_enemiesOnField[i]);
                return targets;
            }
        }

        return targets;
    }

    public int GetStrikesCount()
    {
        int strikes = Strikes;

        foreach (IEffectModifiesAttack attackModifier in attackModifiers)
        {
            strikes += attackModifier.StrikeModifier;
        }

        return strikes;
    }

    public bool CanDefend()
    {
        if (!isDefending && canDefend)
        {
            return true;
        }
        return false;
    }

    public void Defend()
    {
        //Show the defense icon and play its anim
        //Highlight this minions DEF and lower the brightness of ATK

        isDefending = true;
        canAttack = false;
    }

  

    public bool CanActivateSpell()
    {
        if (CurrentCardData.Spells.Count > 1)
        {
            int numOfActivatableSpell = 0;
            foreach (Spell spell in CurrentCardData.Spells)
            {
                if (spell.IsActivatable())
                {
                    numOfActivatableSpell++;
                }
            }
            if (numOfActivatableSpell != 0)
            {
                return true;
            }
        }
        else if (CurrentCardData.Spells.Count == 1)
        {
            if (CurrentCardData.Spells[0].IsActivatable())
            {
                return true;
            }
        }

        return false;
    }

    //Check if can cast atleast one of the spells
    public bool CanCastSpell()
    {
        if (!hasCastedSpell)
        {
            if (CurrentCardData.Spells.Count > 1)
            {
                int numOfCanCastSpell = 0;
                foreach (Spell spell in CurrentCardData.Spells)
                {
                    numOfCanCastSpell++;
                }
                if (numOfCanCastSpell != 0)
                {
                    return true;
                }
            }
            else if (CurrentCardData.Spells.Count == 1)
            {
                if (CurrentCardData.Spells[0].CanCast())
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void CastSpell()
    {
        if (CanActivateSpell() && CanCastSpell())
        {
            int numOfActivatableSpell = 0;
            foreach (Spell spell in CurrentCardData.Spells)
            {
                if (spell.IsActivatable())
                {
                    numOfActivatableSpell++;
                }
            }

            if (selectedSpell == null)
            {
                if (numOfActivatableSpell > 1 && selectedSpell == null)
                {
                    if (selectedSpell != null)
                    {

                    }
                    //Go to choose spell to cast screen and pass this card as argument
                }
                else if (numOfActivatableSpell == 1)
                {
                    //SELECT THE ACTIVATABLE SPELL
                    selectedSpell = CurrentCardData.Spells[0];
                }
            }
            if(selectedSpell != null)
            {
                CastSpell(selectedSpell);
            }
        }
    }

    public void CastSpell(Spell spell)
    {
        if (spell is IEffectDiscarding)
        {
            IEffectDiscarding discardingEffect = spell as IEffectDiscarding;
            if (!discardingEffect.HasDiscarded)
            {
                GameManager.PlayHighPriorityAction(new ActionDiscard(this, discardingEffect, this));
            }
        }

        if (spell is IEffectMinionReturning)
        {
            IEffectMinionReturning minionReturningEffect = spell as IEffectMinionReturning;
            if (!minionReturningEffect.HasSelectedMinions)
            {
                GameManager.PlayHighPriorityAction(new ActionReturnMinion(this, minionReturningEffect));
                return;
            }
        }

        if (spell is IEffectTargeting)
        {
            IEffectTargeting targetingEffect = spell as IEffectTargeting;
            if (!targetingEffect.HasSelectedTarget)
            {
                GameManager.PlayHighPriorityAction(new ActionGetEffectTarget(this, targetingEffect));
                return;
            }
        }

        if (spell is IEffectMinionReturning)
        {
            IEffectMinionReturning minionReturningEffect = spell as IEffectMinionReturning;
            if (minionReturningEffect.ReturnType == Spell.MINION_RETURN_TYPE_TO_HAND) gameManager.ReturnMinionsToHand(minionReturningEffect.SelectedMinions);
            else if (minionReturningEffect.ReturnType == Spell.MINION_RETURN_TYPE_ON_TOP_OF_DECK) gameManager.SendMinionsOnTopOfDeck(minionReturningEffect.SelectedMinions);
        }

        if (spell._Type != Spell.TYPE_ON_SUMMON || spell._Type != Spell.TYPE_PASSIVE)
        {
            //For AfterCastSpell: compare 1st qActs to 2ndqActs to get first new battleAction of this unit. Then link it to the AfterCastSpell  action.
            List<Action> qActs1 = GameManager.gm.GetActionsInQueues();

            spell.Cast();

            List<Action> qActs2 = GameManager.gm.GetActionsInQueues();
            Action firstAct = null;

            if (qActs1.Count > 0)
            {
                foreach (Action act in qActs2)
                {
                    if (!qActs1.Contains(act))
                    {
                        firstAct = act;
                        break;
                    }
                }
            }

            if (firstAct != null)
            {
                ActionAfterSpellCast actionAf = new ActionAfterSpellCast(this, () => { AfterCastSpell(spell); });
                actionAf.behindAction = firstAct;
                GameManager.PlayHighPriorityActionAtFront(actionAf);
            }
            else
            {
                AfterCastSpell(spell);
            }
        }
        else
        {
            spell.Cast();
        }
    }

    void AfterCastSpell(Spell spell)
    {
        gameManager.TriggerSpellcastSpells(spell);
        gameManager.OnAfterCastSpellGetCardData?.Invoke(CurrentCardData, spell);

        if (spell._Type != Spell.Type.Passive)
        {
            hasCastedSpell = true;
        }

        if (spell is IEffectDiscarding)
        {
            IEffectDiscarding discardingEffect = spell as IEffectDiscarding;
            discardingEffect.HasDiscarded = false;
        }
        if (spell is IEffectMinionReturning)
        {
            IEffectMinionReturning minionReturningEffect = spell as IEffectMinionReturning;
            minionReturningEffect.HasSelectedMinions = false;
        }
        if (spell is IEffectTargeting)
        {
            IEffectTargeting targetingEffect = spell as IEffectTargeting;
            targetingEffect.HasSelectedTarget = false;
        }

        selectedSpell = null;

        //send this guy's OriginalCardData to discard pile
        //then set this card's parent to HandZoneVeryFront
        //play animation

        //then after a few delay despawn this object
        if (gameManager.IsInNormalTurn) gameManager.EnableCards(true);

        gameManager.OnAfterCastingGetSpell?.Invoke(spell);
        gameManager.OnAfterCastingSpell?.Invoke();
    }

    public bool Combat()
    {
        bool hasPlayedAction = false;

        foreach (Spell spell in CurrentCardData.Spells)
        {
            if (spell._Type == Spell.TYPE_APPEAR)
            {
                PlayAppearSpell(spell);
                hasPlayedAction = true;
            }
            else if (spell._Type == Spell.TYPE_APPEAR || spell._Type == Spell.TYPE_WEAKEN || spell._Type == Spell.TYPE_SPELLCAST || spell._Type == Spell.TYPE_SLAIN)
            {
                CastSpell(spell);
                hasPlayedAction = true;
            }
        }

        if (!hasPlayedAction && CanAttack())
        {
            AttackEnemy();
            hasPlayedAction = true;
        }

        return hasPlayedAction;
    }

    void ResizeCollider()
    {
        collider.enabled = true;
        Vector2 newColliderSize = new Vector2(spriteRendererMain.bounds.size.x + colliderMargin, spriteRendererMain.bounds.size.y + colliderMargin);
        collider.size = newColliderSize;
    }

    void Reposition()
    {
        transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z - 0.5f);
    }

    void ShowStatsDisplay()
    {
        //play fade anim
        if(canAttack)
        {
            attackIcon.gameObject.SetActive(true);
        }
        else
        {
            attackIcon.gameObject.SetActive(false);
        }

        defenseIcon.gameObject.SetActive(true);
        statusIconsPanel.gameObject.SetActive(true);
    }

    void HideStatsDisplay()
    {
        attackIcon.gameObject.SetActive(false);
        defenseIcon.gameObject.SetActive(false);
        statusIconsPanel.gameObject.SetActive(false);
    }

    void DestroyStatsDisplay()
    {
        Destroy(statsDisplay.gameObject);
    }

    public List<Spell> GetSpells()
    {
        List<Spell> spells = new List<Spell>();
        foreach (Spell spell in CurrentCardData.Spells)
        {
            spells.Add(spell);
        }

        return spells;
    }

    void SetRendererLayerOrder()
    {
        int sortingOrder = 3 - transform.parent.GetSiblingIndex();

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            spriteRenderer.sortingOrder = sortingOrder;
        }
    }

    void ClearAllEvents()
    {
        OnBeforePlayingTurn = null;
        OnMinionSlain = null;
    }

    void RemoveFromSummoningTile()
    {
        transform.SetParent(battleStage);
    }

    public void PlayAppearSpell(Spell spell)
    {
        if (spell._Type == Spell.TYPE_APPEAR && spell.CanCast())
        {
            gameManager.OnCastAppearSpell?.Invoke(spell);

            CastSpell(spell);
        }
    }

    public void PlayOnSummonSpell(Spell spell)
    {
        if (spell._Type == Spell.TYPE_ON_SUMMON && spell.CanCast())
        {
            CastSpell(spell);
        }
    }

    public void PlaySlainSpell(Spell spell)
    {
        if (spell._Type == Spell.TYPE_SLAIN && spell.CanCast())
        {
            CastSpell(spell);
        }
    }

    public void PlayBattleAction()
    {
        if (!HasPreparedBattleAction)
        {
            OnBeforePlayingTurn?.Invoke();
            OnBeforePlayingTurn = null;
            if (CanAttack())
            {
                AttackEnemy();
            }

            HasPreparedBattleAction = true;
        }
    }

    public bool CanPlayTurn()
    {
        if (!hasFinishedTurn && !IsSlain && CanAttack() && CanPerformActions && PlayerCharacter.pc.CanAttackInBattle)
        {
            return true;
        }

        return false;
    }

    public void EndTurn()
    {
        if (!gameManager.IsInNormalTurn)
        {
            hasFinishedTurn = true;
            animator.Play(StringManager.ANIM_STATE_NAME_MINION_IDLE, 0);
        }
    }

    public void SpendTurn()
    {
        if (minionCardData.minionType == MinionCardData.MINIONTYPE_SUPPORT)
        {
            SetDefense(minionCardData.Defense);
            hasFinishedTurn = false;
        }
        else
        {
            if (CanPlayNextTurn)
            {
                willNotDespawnThisTurn = false;
                hasFinishedTurn = false;
                HasPreparedBattleAction = false;

                OnSpendTurn?.Invoke();
            }
            else
            {
                LeaveBattle();
            }
        }

        //add apply bloodless effect
    }

    public bool CanGainAttack
    {
        get
        {
            if (minionCardData.minionType == MinionCardData.MINIONTYPE_SUPPORT) return false;
            return true;
        }
    }

    public void IncreaseAttack(int amount)
    {
        if (amount > 0)
        {
            void ApplyEffect()
            {
                Attack += amount;
                UpdateStatValues();
            }

            Particle.PlayParticleWithEffect(ResourcesManager.ParticleGainDefenseDefault, ApplyEffect, this);
        }
    }

    /// <summary>
    /// Do not directly call. Use GameManager.IncreaseMinionDefense instead.
    /// </summary>
    /// <param name="amount"></param>
    public void IncreaseDefense(int amount)
    {
        if (PlayerCharacter.pc.CanIncreaseMinionDef && CanIncreaseDef)
        {
            foreach (IEffectModifiesDefGained defGainedModifier in defenseGainedModifiers)
            {
                amount += defGainedModifier.DefGainModifier;
            }

            if (amount > 0)
            {
                Defense += amount;
                UpdateStatValues();

                Particle.PlayParticleWithoutEffect(ResourcesManager.ParticleGainDefenseDefault, this);

                OnGainDefense?.Invoke();
                gameManager.OnMinionGainDefense?.Invoke();
                gameManager.OnMinionModifyDefense?.Invoke();
            }
        }
    }

    public void ReduceDefense(int amount)
    {
        Defense -= amount;
        if (Defense < 0) Defense = 0;
        UpdateStatValues();
        gameManager.OnMinionModifyDefense?.Invoke();
    }

    public void SetDefense(int newValue)
    {
        Defense = newValue;
        UpdateStatValues();
        gameManager.OnMinionModifyDefense?.Invoke();
    }

    bool CanAttackAllEnemies()
    {
        if (willAttackAllEnemies) return true;

        return false;
    }

    public void AddDefGainedIncreaser(IEffectModifiesDefGained defenseGainedModifier)
    {
        defenseGainedModifiers.Add(defenseGainedModifier);
    }

    public void RemoveDefGainedIncreaser(IEffectModifiesDefGained defenseGainedModifier)
    {
        defenseGainedModifiers.Remove(defenseGainedModifier);
    }

    void InitializeSpells()
    {
        slainSpells.Clear();

        foreach (Spell spell in CurrentCardData.Spells)
        {
            if (spell is PlayerSpell)
            {
                PlayerSpell playerSpell = spell as PlayerSpell;
                playerSpell.Minion = this;
                playerSpell.Card = null;
                playerSpell.CurrentCardData = CurrentCardData;
                playerSpell.OriginalCardData = OriginalCardData;
            }

            if (spell._Type == Spell.TYPE_SLAIN)
            {
                slainSpells.Add(spell);
            }

            if (spell._Type == Spell.TYPE_DISCARD)
            {
                void CastThis()
                {
                    CastSpell(spell);
                }
                gameManager.OnDiscardByEffect += CastThis;
            }

            if (spell._Type == Spell.TYPE_ON_SUMMON)
            {
                PlayOnSummonSpell(spell);
            }

            if (spell._Type == Spell.TYPE_APPEAR)
            {
                PlayAppearSpell(spell);
            }

            if (spell is PassiveSpell)
            {
                PassiveSpell passiveSpell = spell as PassiveSpell;
                passiveSpell.ApplyPassive();
            }

            if (spell._Type == Spell.TYPE_SPELLCAST)
            {
                gameManager.AddSpellcastSpellInList(spell);
            }

            if (spell is ISpellHasBloodlessEffect blf && !gameManager.HasDealtDamageLastTurnAtksSpls && GameManager.BattleTurnCount > 1) blf.OnTriggerBloodless?.Invoke();

            if (spell._Type == Spell.TYPE_SELF_BUFF)
            {
                if (spell is IEffectHasStatusEffects)
                {
                    IEffectHasStatusEffects effectHasStatusEffects = spell as IEffectHasStatusEffects;
                    foreach (StatusEffect statusEffect in effectHasStatusEffects.StatusEffects)
                    {
                        AddStatusEffect(null, statusEffect);
                    }
                }
                if (spell is IEffectHasTraits)
                {
                    IEffectHasTraits effectHasTraits = spell as IEffectHasTraits;
                    foreach (Trait trait in effectHasTraits.Traits)
                    {
                        if(trait is CardTrait)
                        {
                            CardTrait cardTrait = trait as CardTrait;
                            if (cardTrait.CanBeUsedByEntities) AddTrait(trait);
                        }
                        else
                        {
                            AddTrait(trait);
                        }
                    }
                }
            }

            if (spell._Type == Spell.TYPE_WEAKEN) gameManager.OnActivateWeakenSpells += spell.Cast;
        }
    }

    void InitializeAttackHitParticles()
    {
        if (minionCardData.attackHitParticles.Count == 0) minionCardData.attackHitParticles.Add(ResourcesManager.ParticleHitBasic1);
    }

    public GameObject GetAttackHitParticle()
    {
        return minionCardData.attackHitParticles[0];
    }

    void RemoveSpellPassive()
    {
        foreach (Spell spell in CurrentCardData.Spells)
        {
            if (spell._Type == Spell.TYPE_DISCARD) gameManager.OnDiscardByEffect -= spell.Cast;
            if (spell is PassiveSpell)
            {
                PassiveSpell spellPassive = spell as PassiveSpell;
                spellPassive.RemovePassive();
            }
            if (spell._Type == Spell.TYPE_SPELLCAST) gameManager.RemoveSpellcastSpellInList(spell);
            if (spell._Type == Spell.TYPE_WEAKEN) gameManager.OnActivateWeakenSpells -= spell.Cast;
        }
    }

    public void SetAttack(int newValue)
    {
        if (WillPreventAtkReduc && newValue < Attack) { /*do nothing*/}
        else Attack = newValue;
        UpdateStatValues();
    }

    public void AddAttackModifier(IEffectModifiesAttack attackModifier)
    {
        if (!attackModifiers.Contains(attackModifier)) attackModifiers.Add(attackModifier);
        UpdateStatValues();
    }

    public void RemoveAttackModifier(IEffectModifiesAttack attackModifier)
    {
        if (attackModifiers.Contains(attackModifier)) attackModifiers.Remove(attackModifier);
        UpdateStatValues();
    }

    public void AttackEnemy()
    {
        OnBeforeAttack?.Invoke();
        GameManager.PlayAction(GetActionAttack());
    }

    public void AttackEnemy(List<BattleEntity> target)
    {
        AttackEnemy(target, null);
    }

    public void AttackEnemy(List<BattleEntity> target, GameManager.VoidEvent OnAfterAttack)
    {
        prioTargets = target;
        OnBeforeAttack?.Invoke();
        ActionAttack action = GetActionAttack() as ActionAttack;
        if (OnAfterAttack != null) action.OnAfterAttack += OnAfterAttack;
        action.AddSpecifiedTargets(new List<BattleEntity>(prioTargets));
        GameManager.PlayAction(action);
        prioTargets = null;
    }

    public Action GetActionAttack()
    {
        DamageData damageData = new DamageData(this, null, GetCalculatedOutgoingAttackDamage, GetStrikesCount, DamageData.TYPE_NORMAL, GetAttackHitParticle());
        return new ActionAttack(damageData, GetAttackTarget());
    }

    public Action GetActionAttack(List<BattleEntity> target)
    {
        DamageData damageData = new DamageData(this, null, GetCalculatedOutgoingAttackDamage, GetStrikesCount, DamageData.TYPE_NORMAL, GetAttackHitParticle());
        return new ActionAttack(damageData, target);
    }

    public void AddAttackOnHitEffect(IEffectHasOnHitEffects effect)
    {
        if (!onHitEffects.Contains(effect)) onHitEffects.Add(effect);
    }

    //public void IncreaseOnHitEffectStack(on hit effect, stackAmount)

    public void RemoveAttackOnHitEffect(IEffectHasOnHitEffects effect)
    {
        if (onHitEffects.Contains(effect)) onHitEffects.Remove(effect);
    }

    void ClearAllAttackOnHitEffects()
    {
        onHitEffects.Clear();
    }

    public void AddCriticalStrikeModifier(IEffectModifiesCriticalStrike criticalStrikeModifier)
    {
        //sort modifier from highest critical damage(index 0) to lowest (last index)
        bool hasFoundHigherCriticalDamage = false;
        int higherCriticalDamageIndex = 0;

        for (int i = 0; i < criticalStrikeModifiers.Count; i++)
        {
            float currentCriticalDamage = (float)System.Math.Round(criticalStrikeModifiers[i].CriticalDamage, 2);

            if (!hasFoundHigherCriticalDamage && currentCriticalDamage > criticalStrikeModifier.CriticalDamage)
            {
                hasFoundHigherCriticalDamage = true;
                higherCriticalDamageIndex = i;
            }
            else if (hasFoundHigherCriticalDamage && currentCriticalDamage > criticalStrikeModifier.CriticalDamage)
            {
                if (currentCriticalDamage < criticalStrikeModifiers[higherCriticalDamageIndex].CriticalDamage)
                {
                    higherCriticalDamageIndex = i;
                }
            }
        }

        if (hasFoundHigherCriticalDamage)
        {
            criticalStrikeModifiers.Insert(higherCriticalDamageIndex + 1, criticalStrikeModifier);
        }
        else
        {
            criticalStrikeModifiers.Insert(0, criticalStrikeModifier);
        }
    }

    public void RemoveCriticalStrikeModifier(IEffectModifiesCriticalStrike criticalStrikeModifier)
    {
        criticalStrikeModifiers.Remove(criticalStrikeModifier);
    }

    public void SetSummoningTile(SummoningTile summoningTile)
    {
        SummoningTile = summoningTile;
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

    public void AddDespawnPreventer(IEffectPreventsDespawn despawnPreventer)
    {
        if (!despawnPreventers.Contains(despawnPreventer)) despawnPreventers.Add(despawnPreventer);
    }

    public void RemoveDespawnPreventer(IEffectPreventsDespawn despawnPreventer)
    {
        if (despawnPreventers.Contains(despawnPreventer)) despawnPreventers.Remove(despawnPreventer);
    }

    public void AddDefIncPrev(IEffectPreventsIncreasingDef defIncPrev)
    {
        if (!defIncPrevs.Contains(defIncPrev)) defIncPrevs.Add(defIncPrev);
    }

    public void RemoveDefIncPrev(IEffectPreventsIncreasingDef defIncPrev)
    {
        if (defIncPrevs.Contains(defIncPrev)) defIncPrevs.Remove(defIncPrev);
    }

    public void AddDirDmgPrev(IEffectPreventsDirectDamage f)
    {
        if (!dirDamagePrevs.Contains(f)) dirDamagePrevs.Add(f);
    }

    public void RemoveDirDmgPrev(IEffectPreventsDirectDamage f)
    {
        if (dirDamagePrevs.Contains(f)) dirDamagePrevs.Remove(f);
    }

    public void AddCardVanisher(IEffectVanishesCard f)
    {
        if (!cardVanishers.Contains(f)) cardVanishers.Add(f);
    }

    public void RemoveCardVanisher(IEffectVanishesCard f)
    {
        if (cardVanishers.Contains(f)) cardVanishers.Remove(f);
    }

    public void AddAtkReducPrev(IEffectPreventsAtkReduc f)
    {
        if (!atkReducPrevs.Contains(f)) atkReducPrevs.Add(f);
    }

    public void RemoveAtkReducPrev(IEffectPreventsAtkReduc f)
    {
        if (atkReducPrevs.Contains(f)) atkReducPrevs.Remove(f);
    }
}

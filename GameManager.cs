using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GameManager : MonoBehaviour
{
    class CustomCardDrawInfo
    {
        public int count;
        public string textToSearch;

        public CustomCardDrawInfo(int count, string textToSearch)
        {
            this.count = count;
            this.textToSearch = textToSearch;
        }
    }

    //for testing background
    [SerializeField] GameObject bg1;
    [SerializeField] GameObject bg2;

    public enum BattleState
    {
        None,
        NormalTurn,
        LockedNormalTurn,
        CardSelected,
        MinionSelected,
        CardDragging,
        Summoning,
        EndingPlayerTurn,
        Battle,
        EndingBattle,
        BattlePaused,
        EndStageBattle,
        SelectingCardsInHand,
        SelectingMinions,
        SummoningInBattle,
        SelectingEffectTarget
    }
    public BattleState battleState;

    //Editable:
    const float TIME_DELAY_BEGIN_BATTLE = 1.5f;
    const float TIME_DELAY_END_PLAYER_TURN = 0.2f;
    const float TIME_DELAY_START_TURN_BATTLE = 0.55f;
    const float TIME_DELAY_END_TURN_BATTLE = 0.5f;
    const float TIME_INTERVAL_DISCARD = 0.1f;
    const float TIME_INTERVAL_STATUS_EFFECT_END_TURN_EFFECTS = 0.2f;
    readonly bool alternateTurns = false;
    readonly static int defaultUltPointGained = 1;

    //Times and Delays:
    readonly float cardDrawTimeInterval = 0.1f;
    readonly float discardHandTimeInterval = 0.05f;
    readonly float numberTextChangeTime = 10f;
    readonly float playUnitTurnDelay = 0.25f;

    //For locking player's turn:
    bool IsInLockedNormalTurn { get; set; }
    Action turnUnlockingAction;

    //Technical stuff, don't edit:
    bool isDiscardingHand;
    bool willDiscardAfterDrawingAtEndTurn;
    bool hasClickedMouseButtonDown;
    bool hasStartedTransition;
    bool canChooseTarget = false;
    bool isPlayingEndTurnEffects;
    bool isPlayingEndPlanEffects;
    static bool playerAttacksFirst;
    public bool IsInBattle { get; private set; }
    public bool IsInEvent { get; set; }
    public bool IsInNormalTurn { get; private set; }
    public bool HasEndedTurn { get; private set; }
    public bool AreCardsDisabled { get; private set; }
    Minion minionTemp;
    Enemy enemyTemp;
    IEffectDiscarding discardingEffect;

    //battle carddata lists:
    public readonly List<CardData> inBattleDeck = new List<CardData>();
    public readonly List<CardData> discardPile = new List<CardData>();
    public readonly List<CardData> removedFromPlayCards = new List<CardData>();

    //summoning related stuff:
    SummonInfo currSummonInfo;
    //Card summoningCard;
    //CardData summoningMinionCurrentCardData, summoningMinionOriginalCardData;
    readonly List<SummonInfo> summonInfoList = new List<SummonInfo>();
    public readonly List<Minion> summonedMinions = new List<Minion>();
    public bool IsSummoning { get; set; }

    //drawing cards related stuff:
    const float TIME_CARD_DRAW_INTERVAL = 0.2f;
    public bool IsDrawingCards { get; private set; }
    readonly List<CardData> cardsToDrawFromDeck = new List<CardData>();
    readonly Queue<CustomCardDrawInfo> customCardDrawInfos = new Queue<CustomCardDrawInfo>();

    int CardsToDrawCount
    {
        get
        {
            int customDrawCount = 0;
            foreach (CustomCardDrawInfo customDraw in customCardDrawInfos) customDrawCount += customDraw.count;
            return randomCardsToDrawCount + minionCardsToDrawCount + spellCardsToDrawCount + customDrawCount + safeDrawCount + randomCardsToSeekCount;
        }
    }

    int CardsToSeekCount
    {
        get
        {
            return randomCardsToSeekCount;
        }
    }

    int randomCardsToDrawCount;
    int randomCardsToSeekCount;
    int minionCardsToDrawCount;
    int spellCardsToDrawCount;
    int safeDrawCount; //if there are no minion cards in your hand, draw a minion card; if there is one in your deck
    bool isCardDrawingPaused;

    readonly static CardData backupMinionCardData = DataLibrary.GetCardData(90);

    //for card discarding:
    public bool IsDiscarding { get; private set; }
    int minCardsToDiscardCount, maxCardsToDiscardCount;
    bool hasDiscardLimit;
    public readonly List<Card> recentlyDiscardedCards = new List<Card>();
    readonly List<Card> cardsToDiscard = new List<Card>();

    //for selecting minions on field:
    public bool IsSelectingMinionsOnField { get; private set; }
    IEffectMinionSelecting selectingEffect;
    bool hasSkipSelectingMinion;

    //for selecting minions to summon from outside of hand:
    public bool IsSelectingCardsToSummon { get; set; }
    IEffectSummoning effectSummoningFromOutsideOfHand;

    //for selection screen:
    public bool isSelectingInSelectionScreen, isSelectingCardsInHand;
    public int minSelectionCount, maxSelectionCount;
    public readonly List<Card> selectedCards = new List<Card>();
    public readonly List<Item> selectedItems = new List<Item>();

    //for selecting effect target:
    public bool IsSelectingEffectTarget { get; private set; }
    IEffectTargeting targetingEffect;

    //for shuffling cards to a card list (deck or discard pile)
    public bool HasFinishedShufflingCardsToCardList { get; private set; }

    //Battle stuff:
    public int CurrentUltPoints { get; private set; }
    public static int BattleTurnCount { get; private set; }
    public int SacrificeCount { get; private set; }
    public bool HasDealtDamageLastTurnAtksSpls { get; private set; }
    int dmgDealtInCurrTurnByPlayerAtksSpls;

    public readonly List<Reward> currentBattleRewards = new List<Reward>();
    List<int> slainSpellPlayCounts = new List<int>();
    List<Spell> spellcastSpells = new List<Spell>();
    readonly List<System.Type> oncePerTurnSpells = new List<System.Type>();

    BattleEntity currentBattleActionPlayer;
    readonly Queue<Action> actionQueue = new Queue<Action>();
    readonly Queue<Action> highPriorityActionQueue = new Queue<Action>();
    readonly Queue<Action> lowPriorityActionQueue = new Queue<Action>();
    readonly List<Queue<Action>> actionQueueList = new List<Queue<Action>>();
    readonly Queue<Action> activeActionQueue = new Queue<Action>(); //this action queue is played regardless of the current battle state.
    public Action currentAction, currentActiveAction;

    //For limited turn battles:
    bool isBattleTurnLimited;
    int currentBattleTurnLimit;

    //For sorting enemies' positions:
    public enum EnemySortPivot { Left, Right, Center }

    public Transform enemyTiles;
    const float ENEMY_TILES_MIN_X = 1f; //Both are world positions
    const float ENEMY_TILES_MAX_X = 10f;

    //Events:
    public delegate void VoidEvent();
    public delegate void OnSummonMinionEvent<T>(T summonedMinion);
    public delegate void OnEnemySlainEvent<T>(T slainEnemy);
    public delegate void OnCastSpellEvent(Spell castedSpell);
    public delegate void OnMinionLeftGetMinionEvent(Minion minion);
    public delegate void OnEnemyGainStatusEffectEvent(BattleEntity source, StatusEffect statusEffect);
    public delegate void OnAfterApplyingStatusEffectEvent(BattleEntity source, List<StatusEffect> statusEffects);
    public delegate void OnGetEntEvent(BattleEntity entity);
    public delegate void OnMinionGainStatusEffectGetMinionEvent(Minion minion);
    public delegate void OnGetIntEvent(ref int amount);
    public delegate void OnGetDamageEnemyEvent(ref int damage, Enemy enemy);
    public delegate void OnAfterPlayingCardGetCardEvent(Card card);
    public delegate void OnAfterAcquiringItemGetItemEvent(Item item);
    public delegate void OnAfterDrawingCardGetCardEvent(Card card);
    public delegate void OnBeforeTriggeringMarkEvent(StatusEffect mark);
    public delegate void OnAfterSelectingCardsEvent(List<Card> selectedCards);
    public delegate void OnAfterSelectingItemsEvent(List<Item> selectedItems);
    public delegate void OnAfterSummoningMinionGetMinionEvent(List<Minion> summonedMinions);
    public delegate void OnInitializeCardDataEvent(CardData originalCardData, CardData currentCardData);
    public delegate void OnGetEnemyEvent(Enemy enemy);
    public delegate void OnGetMinionEvent(Minion minion);
    public delegate void OnGetCardDataEvent(CardData cardData);
    public delegate void OnGetEnemySourceEvent(Enemy enemy, BattleEntity source);
    public delegate void OnBeforeDisplayingRewardEvent(List<Reward> rewardList);
    public delegate void OnAfterIncreasingDefGetMinionsEvent(List<Minion> minions);
    public delegate void OnEnemyBeforeTakingDamageGetDmgEnemTypEvent(ref int damage, Enemy enemy, DamageData.Type damageType);
    public delegate bool OnCheckCardDataEvent(CardData cardData);
    public delegate void OnGetCardDataSpellEvent(CardData cardData, Spell spell);
    public delegate void OnGetEntIntEvent(BattleEntity source, ref int amount);
    public delegate void OnGetMinionEntityEvent(Minion m, BattleEntity e);
    public delegate void OnGetEntIntSplEvent(BattleEntity source, ref int amount, Spell spl);

    public OnGetEntIntSplEvent OnBeforeIncMinDefGetValSpl;
    public OnGetMinionEntityEvent OnMinionSlainGetMinionSource;
    public OnGetDamageEnemyEvent OnEnemyBeforeTakingDamageGetDamageEnemy;
    public OnGetMinionEvent OnAfterSummoningMinionGetMinion;
    public VoidEvent OnAfterDealingSpellDmg;
    public OnGetEntIntEvent OnBeforeDealingSpellDmgGetSrcDmg;
    public OnGetCardDataSpellEvent OnAfterCastSpellGetCardData;
    public VoidEvent OnUnitEndedTurn;
    public OnSummonMinionEvent<CardData> OnPlayerSummonMinionCardData;
    public OnSummonMinionEvent<Minion> OnPlayerSummonGetMinion;
    public VoidEvent OnSummonMinion;
    public VoidEvent OnPlayerSummonMinion;
    public VoidEvent OnModifyDiscardPile;
    public VoidEvent OnModifyVanishedPile;
    public VoidEvent OnMinionSlain;
    public OnMinionLeftGetMinionEvent OnMinionSlainGetMinion;
    public VoidEvent OnAfterMinionStrike;
    public VoidEvent OnEnemySlain;
    public VoidEvent OnSummonEnemy; //called in ActionSummonEnemy
    public VoidEvent OnDiscardByEffect;
    public VoidEvent OnFinishedReturningMinions;
    public VoidEvent OnCancelledReturningMinions;
    public OnCastSpellEvent OnCastAppearSpell;
    public OnCastSpellEvent OnAfterCastingGetSpell;
    public VoidEvent OnAfterCastingSpell;
    public VoidEvent OnAfterCastingSpellFromHand;
    public OnMinionLeftGetMinionEvent OnMinionLeftGetMinion;
    public VoidEvent OnMinionLeft;
    public OnEnemyGainStatusEffectEvent OnEnemyGainStatusEffect;
    public OnAfterApplyingStatusEffectEvent OnAfterApplyingStatusEffectToEnemy;
    public OnGetEnemyEvent OnEnemyGainStatusEffectGetEnemy;
    public OnGetEnemySourceEvent OnEnemyGainStatusEffectGetEnemySource;
    public OnGetEnemyEvent OnEnemySlainGetEnemy;
    public VoidEvent OnStartTurn;
    public VoidEvent OnEndOfSummoningPhase;
    public OnMinionGainStatusEffectGetMinionEvent OnMinionGainStatusEffectGetMinion;
    public OnGetEntEvent OnMinionAfterTakingDamageGetAttacker;
    public OnGetIntEvent OnEnemyBeforeTakingDamageGetDmgRefInc;
    public VoidEvent OnStartBattle;
    public OnGetIntEvent OnMinionBeforeDealingAttackDamageGetDamage;
    public OnAfterPlayingCardGetCardEvent OnAfterPlayingCardGetCard;
    public VoidEvent OnEndTurn;
    public VoidEvent OnEndBattle;
    public OnAfterDrawingCardGetCardEvent OnAfterDrawingCardGetCard;
    public OnAfterDrawingCardGetCardEvent OnAfterDrawingCardGetCardReduce; //when reducing something in the card such as cost, atk, def, etc.
    public OnBeforeTriggeringMarkEvent OnBeforeTriggeringMark;
    public VoidEvent OnTriggerMark;
    public OnAfterSummoningMinionGetMinionEvent OnAfterSummoningMinionsGetMinions;
    public VoidEvent OnAfterSummoningMinions;
    public VoidEvent OnEndVictoriousBattle;
    public OnInitializeCardDataEvent OnInitializeCardDataInBattle;
    //public OnGetIntEvent OnPlayerBeforeTakingDamageGetDamage;
    public VoidEvent OnMinionGainDefense;
    public VoidEvent OnMinionModifyDefense;
    public VoidEvent OnRemoveCard;
    public VoidEvent OnAfterPlayingSpellCard;
    public VoidEvent OnActivateWeakenSpells;
    public OnAfterIncreasingDefGetMinionsEvent OnAfterIncreasingDefGetMinions;
    public OnEnemyBeforeTakingDamageGetDmgEnemTypEvent OnEnemyBeforeTakingDamageGetDmgEnemTyp;
    public VoidEvent OnAfterPlayerSpellAttack;
    public VoidEvent OnDiscardPileToDeck;
    public VoidEvent OnEnemySetSpellToCast;
    public OnGetIntEvent OnEnemyBeforeTakingDmgGetDmgRefMult;
    public OnGetMinionEvent OnSummonMinionOutOfHand;
    public OnGetMinionEvent OnMinionTriggerMark;

    public Queue<ActionDrawCard.AfterDrawEventInfo> OnAfterDrawingCardGetCardEvents = new Queue<ActionDrawCard.AfterDrawEventInfo>();
    //event in front is only played. key is number of times the event is invoked until its removed

    //Non-battle related events:
    public VoidEvent OnAfterUpgrade;
    public VoidEvent OnAfterRemovingItem;
    public VoidEvent OnAfterAcquiringItem;
    public OnAfterAcquiringItemGetItemEvent OnAfterAcquiringItemGetItem;
    public OnAfterSelectingCardsEvent OnAfterSelectingCards;
    public OnAfterSelectingItemsEvent OnAfterSelectingItems;
    public VoidEvent OnUseConsumableItem;
    public OnBeforeDisplayingRewardEvent OnBeforeDisplayingReward;
    public VoidEvent OnGainPassiveEffect;
    public VoidEvent OnRemovePassiveEffect;

    Transform cardSpawnPoint, itemDisplay;
    public Transform handZone, summoningTilesT, handZoneFront, battleStage, canvasUI, canvasBattle;
    public static Transform passiveEffectBuffDisplay, passiveEffectDebuffDisplay, passiveEffectKeyItemsDisplay;
    //public static PlayerData PlayerData { get; private set; }
    public static ObjectPooler ObjectPooler { get; private set; }
    public CardActionsPanel cardActionsPanel;
    public static UIHandler UiHandler { get; private set; }
    public DevSetting DevSetting { get; private set; }
    public ResourcesManager ResourcesManager { get; private set; }
    public static StageMap StageMap { get; private set; }
    public static DialogueBox DialogueBox { get; private set; }
    public static ShopWindow ShopWindow { get; private set; }
    public static CameraManager CameraManager { get; private set; }
    public static LightManager LightManager { get; private set; }
    public static RectTransform RectCanvBattle { get; private set; }
    public static List<SummoningTile> summoningTiles = new List<SummoningTile>();
    public static GameManager gm;
    public static GraphicRaycaster CanvasBattleGR;
    public static EventSystem EventSystem;
    public static PointerEventData PointerEventData;
    public static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    Coroutine cardDrawingCoroutine;

    void Awake()
    {
        if (gm == null) gm = this;

        handZone = GameObject.Find("HandZone").transform;
        handZoneFront = GameObject.Find("HandZoneFront").transform;
        cardActionsPanel = GameObject.Find("CardActionsPanel").GetComponent<CardActionsPanel>();
        ObjectPooler = GameObject.Find("ObjectPooler").GetComponent<ObjectPooler>();
        //PlayerData = GameObject.Find("PlayerData").GetComponent<PlayerData>();
        UiHandler = GameObject.Find("Canvas_UI").GetComponent<UIHandler>();
        cardSpawnPoint = GameObject.Find("CardSpawnPoint").transform;
        battleStage = GameObject.Find("Stage_Battle").transform;
        enemyTiles = battleStage.Find("EnemyTiles");
        DevSetting = GameObject.Find("Canvas_Dev").transform.Find("DevSettings").GetComponent<DevSetting>();
        ResourcesManager = GameObject.Find("ResourcesManager").GetComponent<ResourcesManager>();
        canvasUI = GameObject.Find("Canvas_UI").transform;
        StageMap = canvasUI.Find("Screens").Find("StageMap").GetComponent<StageMap>();
        itemDisplay = UiHandler.transform.Find("ItemDisplay");
        DialogueBox = UiHandler.transform.Find("DialogueBox").GetComponent<DialogueBox>();
        ShopWindow = UiHandler.transform.Find("ShopWindow").GetComponent<ShopWindow>();
        passiveEffectBuffDisplay = UiHandler.transform.Find("PassiveEffectDisplay").Find("Enhancements");
        passiveEffectDebuffDisplay = UiHandler.transform.Find("PassiveEffectDisplay").Find("Curses");
        passiveEffectKeyItemsDisplay = UiHandler.transform.Find("PassiveEffectDisplay").Find("KeyItems");
        canvasBattle = GameObject.Find("Canvas_Battle").transform;
        RectCanvBattle = canvasBattle.GetComponent<RectTransform>();
        CanvasBattleGR = canvasBattle.GetComponent<GraphicRaycaster>();
        EventSystem = GameObject.Find("EventSystem").GetComponent<EventSystem>();
        PointerEventData = new PointerEventData(EventSystem);
        CameraManager = Camera.main.GetComponent<CameraManager>();
        LightManager = GameObject.Find("EntityGlobalLight").GetComponent<LightManager>();

        summoningTilesT = battleStage.Find("SummoningTiles");
        foreach (Transform tile in summoningTilesT) summoningTiles.Add(tile.GetComponent<SummoningTile>());

        actionQueueList.Add(highPriorityActionQueue);
        actionQueueList.Add(actionQueue);
        actionQueueList.Add(lowPriorityActionQueue);
    }

    void Start()
    {

        //Test below:
        QualitySettings.vSyncCount = 1;
        SetGameFPS(60);

        //PlayerCharacter.pc.SetPos(PlayerCharacter.pc.posPlayerEntrance);
        //PlayerCharacter.pc.Walk(PlayerCharacter.pc.posPlayerInBattle);

        WorldData wd = new WorldData(GetRandomInteger(), WorldData.DEFAULT_STG_LT);
        StageMap.Initialize(wd);
        StageMap.EnableTravelling();
        PlayerCharacter.pc.Initialize();

        UiHandler.ShowStandbyButtons();
        SetItemSlotsCount(PlayerData.data.ItemSlotsCount);

        AddCardToDeck(DataLibrary.GetStarterDeck(PlayerData.data.Class));

        AcquireItem(DataLibrary.GetItemData(34));
        AcquireItem(DataLibrary.GetItemData(34));
        AcquireItem(DataLibrary.GetItemData(38));
        AcquireItem(DataLibrary.GetItemData(38));
    }

    void Update()
    {
        if (IsInBattle)
        {
            if (handZone.childCount + handZoneFront.childCount != 0)
            {
                SetCardsIndex();
            }

            //For ending player's turn when the END TURN button is pressed
            if (battleState == BattleState.EndingPlayerTurn && !IsPlayerBusy() && !hasStartedTransition && AreActionQueuesEmpty() && currentAction == null)
            {
                if (willDiscardAfterDrawingAtEndTurn)
                {
                    HandToDiscardPile();
                    willDiscardAfterDrawingAtEndTurn = false;
                }
                else
                {
                    hasStartedTransition = true;
                    EndPlayerTurn();
                }
            }

            PlayActiveActionQueue();
            PlayActionQueue();

            if (IsSummoning && battleState == BattleState.Summoning) SelectTileToSummonMinion();
            SelectEffectTarget();
            if (IsSelectingMinionsOnField && battleState == BattleState.SelectingMinions) SelectMinionsOnField();
        }
    }

    void SelectTileToSummonMinion()
    {
        if (Input.GetMouseButtonDown(0))
        {
            hasClickedMouseButtonDown = true;
        }

        if (Input.GetMouseButtonUp(0) && hasClickedMouseButtonDown)
        {
            if (GetClickHitTag() == StringManager.TAG_SUMMONING_TILE)
            {
                SummoningTile clickedTile = GetRaycastHit2D().collider.transform.GetComponent<SummoningTile>();

                if (clickedTile.CanBeSummonedOn())
                {
                    currSummonInfo.targetSummTile = clickedTile;
                    SummonMinion(currSummonInfo);
                }
            }

            hasClickedMouseButtonDown = false;
        }
        else if (Input.GetMouseButtonUp(1) && UiHandler.btnCancel.gameObject.activeSelf)
        {
            OnClickCancelSummoning();
        }
    }

    void SelectEffectTarget()
    {
        if (IsSelectingEffectTarget)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                canChooseTarget = true;
            }

            if (canChooseTarget)
            {
                if (Input.GetMouseButtonUp(1))
                {
                    OnClickCancelGetSpellTarget();
                }

                if (Input.GetMouseButtonUp(0))
                {
                    bool canCastTargetingEffect = false;

                    if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_MINION_SELECT
                        || targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_OTHER_MINION
                        || targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_SINGLE_DIGIT_ATK_MINION)
                    {
                        if (GetClickHitTag() == StringManager.TAG_MINION)
                        {
                            targetingEffect.TargetList.Add(GetRaycastHit2D().collider.transform.GetComponent<Minion>());
                            targetingEffect.HasSelectedTarget = true;
                            canCastTargetingEffect = true;
                        }
                    }
                    else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ENEMY_SELECT
                        || targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ELITE_ENEMY_SELECT
                        || targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_NON_BOSS_ENEMY_SELECT)
                    {
                        if (GetClickHitTag() == StringManager.TAG_ENEMY)
                        {
                            Enemy enemy = GetRaycastHit2D().collider.transform.GetComponent<Enemy>();
                            if (enemy.IsTargetable())
                            {
                                targetingEffect.TargetList.Add(enemy);
                                targetingEffect.HasSelectedTarget = true;
                                canCastTargetingEffect = true;
                            }
                        }
                    }

                    if (canCastTargetingEffect)
                    {
                        CastTargetingEffect(targetingEffect);
                        IsSelectingEffectTarget = false;
                        targetingEffect = null;
                    }

                    canChooseTarget = false;
                }
            }
        }
    }

    void SelectMinionsOnField()
    {
        if (Input.GetMouseButtonDown(0))
        {
            canChooseTarget = true;
        }

        if (canChooseTarget)
        {
            if (Input.GetMouseButtonUp(0))
            {
                if (GetClickHitTag() == StringManager.TAG_MINION)
                {
                    Minion minion = GetRaycastHit2D().collider.transform.GetComponent<Minion>();

                    if (!selectingEffect.SelectedMinions.Contains(minion))
                    {
                        selectingEffect.SelectedMinions.Add(minion);
                    }
                    else
                    {
                        selectingEffect.SelectedMinions.Remove(minion);
                    }

                    if (selectingEffect.MinionSelectType == Spell.MINION_SELECT_TYPE_ANY_MINION_SELECT)
                    {
                        if (selectingEffect.SelectedMinions.Count > 0) UiHandler.btnOkay.interactable = true;
                        else UiHandler.btnOkay.interactable = false;
                    }
                    else
                    {
                        selectingEffect.HasSelectedMinions = true;
                        PlayMinionSelectingEffect(selectingEffect);
                        IsSelectingMinionsOnField = false;
                        selectingEffect = null;
                    }
                }

                canChooseTarget = false;
            }
        }
    }

    void PlayActionQueue()
    {
        if (!AreActionQueuesEmpty() || currentAction != null)
        {
            if (currentAction == null)
            {
                if (highPriorityActionQueue.Count > 0) currentAction = highPriorityActionQueue.Dequeue();
                else if (actionQueue.Count > 0) currentAction = actionQueue.Dequeue();
                else if (lowPriorityActionQueue.Count > 0) currentAction = lowPriorityActionQueue.Dequeue();

                if (currentAction.behindAction != null && IsActionInQueues(currentAction.behindAction, out Queue<Action> q))
                {
                    if (currentAction.behindAction.behindAction == currentAction) currentAction.behindAction.behindAction = null;
                    if (!IsActionBehindOf(currentAction.behindAction, currentAction))
                    {
                        PutActionInFrontOf(currentAction, currentAction.behindAction);
                        currentAction = null;

                        PlayActionQueue();
                        return;
                    }
                }

                if (currentAction.isLastAction && HasNonLastActions(currentAction.source, out List<Action> lastActions))
                {
                    //arrange last actions:
                    foreach (Action lastAction in lastActions)
                    {
                        PutActionBehindLastAction(lastAction, lastAction.source);
                    }
                    currentAction = null;
                    PlayActionQueue();
                    return;
                }

                if (currentAction == turnUnlockingAction)
                {
                    IsInLockedNormalTurn = false;
                    turnUnlockingAction = null;
                }

                currentAction.Start();
            }

            if (currentAction != null)
            {
                if (currentAction.IsFinished())
                {
                    if (currentAction is BattleEntityAction)
                    {
                        BattleEntityAction battleEntityAction = currentAction as BattleEntityAction;
                        battleEntityAction.Finish();
                    }

                    currentAction = null;
                    if (!AreActionQueuesEmpty())
                    {
                        PlayActionQueue();
                        return;
                    }
                    else
                    {
                        if (battleState == BattleState.EndingBattle)
                        {
                            if (isPlayingEndTurnEffects)
                            {
                                isPlayingEndTurnEffects = false;
                                ReduceStatusDurations();
                            }

                            EndTurnBattle();
                        }
                        else if (!IsInNormalTurn)
                        {
                            EndCurrentBattleActionPlayersTurn();
                            GetNextUnitToPlay();
                        }
                    }
                }
            }
        }
    }

    void PlayActiveActionQueue()
    {
        if (activeActionQueue.Count > 0 || currentActiveAction != null)
        {
            if (currentActiveAction == null)
            {
                currentActiveAction = activeActionQueue.Dequeue();
                currentActiveAction.Start();
            }

            if (currentActiveAction != null)
            {
                if (currentActiveAction.IsFinished())
                {
                    currentActiveAction = null;
                    if (activeActionQueue.Count > 0)
                    {
                        PlayActiveActionQueue();
                    }
                }
            }
        }
    }

    bool AreActionQueuesEmpty()
    {
        return highPriorityActionQueue.Count == 0 && actionQueue.Count == 0 && lowPriorityActionQueue.Count == 0;
    }

    public List<Action> GetActionsInQueues()
    {
        List<Action> actions = new List<Action>();
        if (currentAction != null) actions.Add(currentAction);
        actions.AddRange(highPriorityActionQueue);
        actions.AddRange(actionQueue);
        actions.AddRange(lowPriorityActionQueue);

        return actions;
    }

    public static void PlayHighPriorityAction(Action action)
    {
        GameManager.gm.highPriorityActionQueue.Enqueue(action);
    }

    public static void PlayHighPriorityActionAtFront(Action action)
    {
        Queue<Action> tempQueue = new Queue<Action>(GameManager.gm.highPriorityActionQueue);
        GameManager.gm.highPriorityActionQueue.Clear();
        GameManager.gm.highPriorityActionQueue.Enqueue(action);

        while (tempQueue.Count != 0)
        {
            GameManager.gm.highPriorityActionQueue.Enqueue(tempQueue.Dequeue());
        }
    }

    public static void PlayAction(Action action)
    {
        GameManager.gm.actionQueue.Enqueue(action);
    }

    public static void PlayActionAtFront(Action action)
    {
        Queue<Action> tempQueue = new Queue<Action>(GameManager.gm.actionQueue);
        GameManager.gm.actionQueue.Clear();
        GameManager.gm.actionQueue.Enqueue(action);

        while (tempQueue.Count != 0)
        {
            GameManager.gm.actionQueue.Enqueue(tempQueue.Dequeue());
        }
    }

    public static void PlayLowPriorityAction(Action action)
    {
        GameManager.gm.lowPriorityActionQueue.Enqueue(action);
    }

    static bool IsActionInQueues(Action action, out Queue<Action> actionQ)
    {
        actionQ = null;

        if (action != null)
        {
            if (action == GameManager.gm.currentAction) return true;
            foreach (Queue<Action> q in GameManager.gm.actionQueueList)
            {
                if (q.Contains(action))
                {
                    actionQ = q;
                    return true;
                }
            }
        }

        return false;
    }

    public static bool PlayActionInFrontOfFirstAction(Action action, BattleEntity source, bool dontPutIfSameActionType)
    {
        bool hasPut = false;

        foreach (Queue<Action> q in GameManager.gm.actionQueueList)
        {
            List<Action> tempList = new List<Action>(q);
            for (int i = 0; i < tempList.Count; i++)
            {
                if (tempList[i].source == source)
                {
                    if (dontPutIfSameActionType && action.GetType() == tempList[i].GetType()) return false;
                    else
                    {
                        tempList.Insert(i, action);
                        hasPut = true;
                        break;
                    }
                }
            }

            if (hasPut) break;
        }

        return hasPut;
    }

    static bool IsActionBehindOf(Action behindAction, Action action)
    {
        List<Action> actionQ = GameManager.gm.GetActionsInQueues();

        if (actionQ.Count > 1)
        {
            if (GameManager.gm.currentAction == action) if (actionQ[1] == behindAction) return true;
            for (int i = actionQ.Count - 1; i > 0; i--)
            {
                if (actionQ[i] == behindAction)
                {
                    return actionQ[i - 1] == action;
                }
            }
        }

        return true;
    }

    public static void PutActionsOfBehindOf(BattleEntity entity1, BattleEntity entity2)
    {
        List<Action> ent1Actions = new List<Action>();
        List<Action> tempList = new List<Action>();
        Queue<Action> ent2LastActQ = null;
        bool hasFoundEnt2LastAct = false;

        for (int i = gm.actionQueueList.Count - 1; i >= 0; i--)
        {
            tempList.Clear();
            tempList.AddRange(gm.actionQueueList[i]);
            gm.actionQueueList[i].Clear();

            for (int j = tempList.Count - 1; j >= 0; j--)
            {
                if (tempList[j].source == entity1)
                {
                    ent1Actions.Insert(0, tempList[j]);
                    tempList.RemoveAt(j);
                }
                else if (!hasFoundEnt2LastAct && tempList[j].source == entity2)
                {
                    ent2LastActQ = gm.actionQueueList[i];
                    hasFoundEnt2LastAct = true;
                }
            }

            foreach (Action a in tempList) gm.actionQueueList[i].Enqueue(a);
        }

        //if (ent1Actions.Count == 0 && gm.currentAction != null && gm.currentAction.source == entity1) ent1Actions.Add(gm.currentAction);
        if (!hasFoundEnt2LastAct && gm.currentAction != null && gm.currentAction.source == entity2) hasFoundEnt2LastAct = true;

        if (hasFoundEnt2LastAct && ent1Actions.Count > 0)
        {
            if (ent2LastActQ != null)
            {
                List<Action> l = new List<Action>(ent2LastActQ);
                ent2LastActQ.Clear();

                for (int i = l.Count - 1; i >= 0; i--)
                {
                    if (l[i].source == entity2)
                    {
                        ent1Actions.Reverse();
                        foreach (Action a in ent1Actions) l.Insert(i + 1, a);
                        break;
                    }
                }

                foreach (Action a in l)
                {
                    ent2LastActQ.Enqueue(a);
                }
            }
            else
            {
                List<Action> l = new List<Action>(gm.actionQueueList[0]);
                gm.actionQueueList[0].Clear();
                ent1Actions.Reverse();
                foreach (Action a in ent1Actions) l.Insert(0, a);
                foreach (Action a in l) gm.actionQueueList[0].Enqueue(a);
            }
        }
    }

    public static void PutActionBehindOf(Action action, Action targetAction)
    {
        bool hasPut = false;

        if (GameManager.gm.currentAction == targetAction) PlayHighPriorityActionAtFront(action);
        foreach (Queue<Action> q in GameManager.gm.actionQueueList)
        {
            if (q.Contains(targetAction))
            {
                List<Action> l = new List<Action>(q);
                for (int i = 0; i < l.Count; i++)
                {
                    if (l[i] == targetAction)
                    {
                        l.Insert(i + 1, action);
                        break;
                    }
                }
                q.Clear();
                foreach (Action a in l) q.Enqueue(a);
                hasPut = true;
            }

            if (hasPut) break;
        }
    }

    static void PutActionInFrontOf(Action action, Action targetAction)
    {
        bool hasPut = false;

        foreach (Queue<Action> q in GameManager.gm.actionQueueList)
        {
            if (q.Contains(targetAction))
            {
                List<Action> l = new List<Action>(q);
                for (int i = 0; i < l.Count; i++)
                {
                    if (l[i] == targetAction)
                    {
                        l.Insert(i, action);
                        break;
                    }
                }
                q.Clear();
                foreach (Action a in l) q.Enqueue(a);
                hasPut = true;
            }

            if (hasPut) break;
        }
    }

    static bool PutActionBehindLastAction(Action action, BattleEntity source)
    {
        bool hasPutActBehindLastAct = false;
        bool isActionInQueue = false;
        Queue<Action> qOfAct = null;
        int indexInQ = 0;

        if (IsActionInQueues(action, out Queue<Action> actionQ)) //check if action is in q
        {
            isActionInQueue = true;
            if (actionQ != null)
            {
                qOfAct = actionQ;
                List<Action> l = new List<Action>(actionQ);
                for (int i = l.Count - 1; i >= 0; i--)
                {
                    if (l[i] == action)
                    {
                        indexInQ = i;
                        break;
                    }
                }
            }
        }

        for (int i = GameManager.gm.actionQueueList.Count - 1; i >= 0; i--)
        {
            List<Action> tempList = new List<Action>(GameManager.gm.actionQueueList[i]);
            GameManager.gm.actionQueueList[i].Clear();

            for (int j = tempList.Count - 1; j >= 0; j--)
            {
                if (tempList[j].source == source && tempList[j] != action) //if its the last action
                {
                    if (isActionInQueue)
                    {
                        if (qOfAct == GameManager.gm.actionQueueList[i]) //if will remove act in same queue
                        {
                            if (indexInQ < j)
                            {
                                tempList.RemoveAt(indexInQ);
                                tempList.Insert(j, action);
                            }
                            else
                            {
                                tempList.RemoveAt(indexInQ);
                                tempList.Insert(j + 1, action);
                            }
                        }
                        else if (qOfAct != null) //if will remove from other q
                        {
                            tempList.Insert(j + 1, action);
                            List<Action> l = new List<Action>(qOfAct);
                            qOfAct.Clear();
                            l.RemoveAt(indexInQ);
                            foreach (Action a in l) qOfAct.Enqueue(a);
                        }
                        else //if action is currentAction
                        {
                            tempList.Insert(j + 1, action);
                            GameManager.gm.currentAction = null;
                        }
                    }
                    else tempList.Insert(j + 1, action);

                    hasPutActBehindLastAct = true;
                    break;
                }
            }

            for (int j = 0; j < tempList.Count; j++)
            {
                GameManager.gm.actionQueueList[i].Enqueue(tempList[j]);
            }

            if (hasPutActBehindLastAct) break;
        }

        if (!hasPutActBehindLastAct)
        {
            if (GameManager.gm.currentAction != null && GameManager.gm.currentAction.source == source && GameManager.gm.currentAction != action)
            {
                if (isActionInQueue)
                {
                    List<Action> l = new List<Action>(qOfAct);
                    l.RemoveAt(indexInQ);
                    qOfAct.Clear();
                    foreach (Action a in l) qOfAct.Enqueue(a);
                }

                foreach (Queue<Action> q in GameManager.gm.actionQueueList)
                {
                    if (q.Count > 0)
                    {
                        List<Action> l = new List<Action>(q);
                        q.Clear();
                        q.Enqueue(action);
                        foreach (Action a in l) q.Enqueue(a);
                        hasPutActBehindLastAct = true;
                    }
                }

                if (!hasPutActBehindLastAct)
                {
                    GameManager.gm.actionQueueList[0].Enqueue(action);
                    hasPutActBehindLastAct = true;
                }
            }
        }

        return hasPutActBehindLastAct;
    }

    public static bool PlayActionBehindLastAction(Action action, BattleEntity source)
    {
        return PutActionBehindLastAction(action, source);
    }

    static bool HasActionBeforeThisAction(Action action)
    {
        foreach (Queue<Action> q in GameManager.gm.actionQueueList)
        {
            List<Action> tempList = new List<Action>(q);
            if (tempList.Count > 1)
            {
                for (int i = 1; i < tempList.Count; i++)
                {
                    if (tempList[i] == action) return true;
                }
            }
        }

        return false;
    }

    static Action GetActionInFront()
    {
        Action action = null;

        foreach (Queue<Action> q in GameManager.gm.actionQueueList)
        {
            if (q.Count > 0)
            {
                List<Action> l = new List<Action>(q);
                return l[0];
            }
        }

        return action;
    }

    bool HasNonLastActions(BattleEntity source, out List<Action> lastActions)
    {
        int act = 0;
        int lastAct = 0;
        lastActions = new List<Action>();

        if (currentAction != null && currentAction.source == source)
        {
            act++;
            if (currentAction.isLastAction)
            {
                lastAct++;
                lastActions.Add(currentAction);
            }
        }
        for (int i = 0; i < actionQueueList.Count; i++)
        {
            foreach (Action action in actionQueueList[i])
            {
                if (action.source == source)
                {
                    act++;
                    if (action.isLastAction)
                    {
                        lastAct++;
                        lastActions.Add(action);
                    }
                }
            }
        }

        return act > lastAct;
    }

    public static int GetBattleEntityActionCount(BattleEntity source)
    {
        int actCount = 0;

        if (source != null)
        {
            void GetActCount(Queue<Action> actionQueue)
            {
                foreach (Action action in actionQueue)
                {
                    if (action != null && action.source == source) actCount++;
                }
            }

            if (GameManager.gm.currentAction != null && GameManager.gm.currentAction.source == source) actCount++;
            GetActCount(GameManager.gm.highPriorityActionQueue);
            GetActCount(GameManager.gm.actionQueue);
            GetActCount(GameManager.gm.lowPriorityActionQueue);
        }

        return actCount;
    }

    static int GetBattleEntityLastActionCount(BattleEntity source)
    {
        int actCount = 0;

        void GetActCount(Queue<Action> actionQueue)
        {
            foreach (Action action in actionQueue)
            {
                if (action != null && action.source == source && action.isLastAction) actCount++;
            }
        }

        if (GameManager.gm.currentAction != null && GameManager.gm.currentAction.source == source && GameManager.gm.currentAction.isLastAction) actCount++;
        GetActCount(GameManager.gm.highPriorityActionQueue);
        GetActCount(GameManager.gm.actionQueue);
        GetActCount(GameManager.gm.lowPriorityActionQueue);

        return actCount;
    }

    static List<Action> GetBattleEntityLastActions(BattleEntity source)
    {
        List<Action> actions = new List<Action>();

        if (GameManager.gm.currentAction != null && GameManager.gm.currentAction.isLastAction) actions.Add(GameManager.gm.currentAction);
        foreach (Queue<Action> q in GameManager.gm.actionQueueList)
        {
            List<Action> tempList = new List<Action>(q);
            foreach (Action act in tempList)
            {
                if (act != null && act.isLastAction) actions.Add(act);
            }
        }

        return actions;
    }

    void EndCurrentBattleActionPlayersTurn()
    {
        if (currentBattleActionPlayer != null)
        {
            if (currentBattleActionPlayer is Minion)
            {
                minionTemp = currentBattleActionPlayer as Minion;
                minionTemp.EndTurn();
            }
            else if (currentBattleActionPlayer is Enemy)
            {
                enemyTemp = currentBattleActionPlayer as Enemy;
                enemyTemp.EndTurn();
            }

            currentBattleActionPlayer = null;
        }
        else
        {
            Debug.Log(StringManager.ERR_CURRENT_BATTLE_ACTION_PLAYER_NULL);
        }
    }

    public void ChangeBattleState(BattleState battleState)
    {
        if (this.battleState != battleState)
        {
            if (battleState == BattleState.NormalTurn)
            {
                EnableCards(true);
                RemoveAllCardsColliders(false);
                //CheckCardsSelectability();
                RemoveAllMinionColliders(false);
                SetCardsInHandState(Card.State.UsedForBattle);

                SetPlayableSummonTilesState(SummoningTile.BattleState.Idle);

                foreach (Minion minion in GetMinionsOnField())
                {
                    if (minion.state != Minion.State.UsedInBattle) minion.SetState(Minion.State.UsedInBattle);
                }

                //foreach (Card card in GetPlayableCardsInHand())
                //{
                //    card.SetState(Card.State.UsedForBattle);
                //}
                CheckCardsSelectability();

                UiHandler.HideButton("Cancel");
                SetEndTurnButtonActive(true);
            }
            else if (battleState == BattleState.LockedNormalTurn)
            {
                SetCardsInHandState(Card.State.UsedForBattle);
                RemoveAllMinionColliders(true);
                SetEndTurnButtonActive(false);
                EnableCards(false);
            }
            else if (battleState == BattleState.CardSelected)
            {
                //show the UI panel
                //disable all other colliders so the player can only click the panel
                foreach (Transform cardTransform in handZone)
                {
                    Card card = cardTransform.GetComponent<Card>();
                    card.SetRaycastable(true);
                }
                RemoveAllMinionColliders(false);
            }
            else if (battleState == BattleState.MinionSelected)
            {
                RemoveAllMinionColliders(true);
            }
            else if (battleState == BattleState.CardDragging)
            {
                RemoveAllMinionColliders(true);
                DeselectCards();
                RemoveAllCardsColliders(true);

                Card draggedCard = GetDraggedCardInHand();

                if (draggedCard.CurrentCardData is MinionCardData)
                {
                    if (CanSummonMinion(draggedCard.CurrentCardData))
                    {
                        SetPlayableSummonTilesState(SummoningTile.BattleState.Active);
                    }
                }
            }
            else if (battleState == BattleState.Summoning)
            {
                //handZoneFront.GetChild(0).GetComponent<BoxCollider2D>().enabled = true;
                //disable card, minion, enemy colliders. Tiles only
                //get the card in handzonefront, if it can be summoned:
                //if the tile can be summoned on, make it selectable. If not, then not selectable
                SetPlayableSummonTilesState(SummoningTile.BattleState.Active);
                RemoveAllMinionColliders(true);
                SetEndTurnButtonActive(false);
                EnableCards(false);
            }
            else if (battleState == BattleState.EndingPlayerTurn)
            {
                RemoveAllCardsColliders(true);
                RemoveAllMinionColliders(true);

                EnableCards(false);
                CheckCardsSelectability();

                UiHandler.DisableButton("EndTurn");
            }
            else if (battleState == BattleState.Battle)
            {

            }
            else if (battleState == BattleState.SummoningInBattle)
            {
                //isSummoningInBattle = true;
            }
            else if (battleState == BattleState.SelectingEffectTarget)
            {
                if (targetingEffect != null)
                {
                    if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_MINION_SELECT)
                    {
                        foreach (Minion minion in GetMinionsOnField())
                        {
                            minion.SetState(Minion.State.UsedForSelection);
                        }
                    }
                }

                //disable cards
                EnableCards(false);
                SetEndTurnButtonActive(false);
                //test:    
            }
            else if (battleState == BattleState.SelectingCardsInHand)
            {
                RemoveAllMinionColliders(true);
            }
            else if (battleState == BattleState.SelectingMinions)
            {
                foreach (Minion minion in GetMinionsOnField())
                {
                    minion.SetState(Minion.State.UsedForSelection);
                }

                EnableCards(false);
                cardActionsPanel.Hide(true);
                SetEndTurnButtonActive(false);
            }

            this.battleState = battleState;
        }
    }

    //////////////////////////////// GAMEPLAY STUFF //////////////////////////////////////////

    IEnumerator StartBattle()
    {
        yield return new WaitForSeconds(TIME_DELAY_BEGIN_BATTLE);
        StartPlayerTurn();
    }

    public void PrepareBattle(List<EnemyData> enemyDataList, List<Reward> battleRewards)
    {
        DespawnEnemies();
        List<Enemy> enemies = SpawnEnemies(enemyDataList);
        SortEnemiesPosition(EnemySortPivot.Right);

        currentBattleRewards.Clear();
        if (battleRewards != null) currentBattleRewards.AddRange(battleRewards);

        SetSummonTilesState(SummoningTile.BattleState.Disabled);
    }

    public void PrepareBattle(List<EnemyData> enemyDataList, List<Reward> battleRewards, int battleTurnLimit)
    {
        PrepareBattle(enemyDataList, battleRewards);
        isBattleTurnLimited = true;
        currentBattleTurnLimit = battleTurnLimit;
    }

    public void AddBattleReward(Reward battleReward)
    {
        if (!currentBattleRewards.Contains(battleReward)) currentBattleRewards.Add(battleReward);
    }

    public void BeginBattle() //called at start of battle encounter
    {
        if (GetEnemiesOnField().Count != 0)
        {
            IsInBattle = true;
            IsInNormalTurn = true;

            ShowSummoningTiles();
            ClearBattleCardData();
            PrepareInBattleDeck();

            //SpawnEnemies(enemyRoomData.GetEnemyData());

            PlayerCharacter.pc.Initialize();
            UiHandler.UpdateInBattleTextValues();

            PlayerCharacter.pc.SetPos(PlayerCharacter.pc.posPlayerEntrance);
            PlayerCharacter.pc.Walk(PlayerCharacter.pc.posPlayerInBattle, true);

            UiHandler.ShowInBattleUI();
            ClearHand();
            ShowCardsInHand();
            ResetBattleTurnCount();
            ClearBattleValues();
            ResetUltPointCount();

            OnStartBattle?.Invoke();

            PreparePlayerTurn();
            StartCoroutine(StartBattle());
        }
    }

    public void EndVictoriousBattle()
    {
        OnEndVictoriousBattle?.Invoke();

        if (IsInEvent)
        {
            EndEventBattle();
        }
        else if (IsInBattle)
        {
            EndBattle();

            UiHandler.ShowVictoryText();
            StageMap.EnableTravelling();
        }
    }

    void EndEventBattle()
    {
        if (IsInEvent)
        {
            EndBattle();
        }
    }

    void EndBattle()
    {
        if (IsInBattle)
        {
            IsInBattle = false;
            isBattleTurnLimited = false;

            OnEndBattle?.Invoke();
            HideCardsInHand();
            UiHandler.HideInBattleUI();
            ClearBattleCardData();
            ClearSpellcastSpellList();

            PlayerCharacter.pc.ClearAllStatusEffects();
            PlayerCharacter.pc.ClearAllTraits();
            PlayerCharacter.pc.ClearEnergy();

            //Despawn minions
            foreach (Minion minion in GetMinionsOnFieldEvenIfSlain())
            {
                minion.RemoveObject();
            }

            if (IsSelectingEffectTarget) OnClickCancelGetSpellTarget();
            if (IsSummoning) OnClickCancelSummoning();
            //cancel card dragging
            ClearBattleValues();
        }
    }

    void EndTurnLimitedBattle()
    {
        //Tell the player time's up. THen coroutine below:

        if (IsInEvent)
        {
            EndEventBattle();
        }
    }

    void ClearBattleValues()
    {
        SacrificeCount = 0;
        randomCardsToDrawCount = 0;
        randomCardsToSeekCount = 0;
        minionCardsToDrawCount = 0;
        spellCardsToDrawCount = 0;
        safeDrawCount = 0;
        dmgDealtInCurrTurnByPlayerAtksSpls = 0;

        highPriorityActionQueue.Clear();
        actionQueue.Clear();
        lowPriorityActionQueue.Clear();
        oncePerTurnSpells.Clear();
        OnAfterDrawingCardGetCardEvents.Clear();

        cardsToDrawFromDeck.Clear();

        currentAction = null;
        currentActiveAction = null;

        HasDealtDamageLastTurnAtksSpls = false;
        IsSelectingEffectTarget = false;
        canChooseTarget = false;
        //AreCardsDisabled = false;
        isCardDrawingPaused = false;
        isPlayingEndTurnEffects = false;
        isPlayingEndPlanEffects = false;
        HasEndedTurn = false;

        ClearSummoningRelatedValues();
    } //Called every start of turn

    void ShowCardsInHand()
    {
        if (!handZone.gameObject.activeSelf && !handZoneFront.gameObject.activeSelf)
        {
            handZone.gameObject.SetActive(true);
            handZoneFront.gameObject.SetActive(true);
        }
    }

    void HideCardsInHand() //Change func name; called every end of battle
    {
        foreach (Card card in GetCardsInHand()) card.Despawn(0f);
        handZone.gameObject.SetActive(false);
        handZoneFront.gameObject.SetActive(false);
    }

    void ClearBattleCardData()
    {
        ClearHand();
        inBattleDeck.Clear();
        cardsToDrawFromDeck.Clear();
        discardPile.Clear();
        ClearVanishedPile();
    }

    void PreparePlayerTurn()
    {
        ClearBattleValues();
        PlayerCharacter.pc.StartTurnGainEnergy();
    }

    void StartPlayerTurn()
    {
        if (IsInBattle)
        {
            IncreaseBattleTurnCount();

            HasDealtDamageLastTurnAtksSpls = dmgDealtInCurrTurnByPlayerAtksSpls > 0 ? true : false;
            dmgDealtInCurrTurnByPlayerAtksSpls = 0;

            //UnlockPlayerActions();
            ChangeBattleState(BattleState.NormalTurn);

            OnStartTurn?.Invoke();
            foreach (Enemy enemy in GetEnemiesOnField()) enemy.OnStartTurn?.Invoke();

            if (BattleTurnCount != 1) PreparePlayerTurn();
            CheckCardsSelectability();

            //Playstartturneffects
            PrepareEnemiesForNextTurn();
            SpendMinionsTurn();

            HasEndedTurn = false;
            IsInNormalTurn = true;
            hasStartedTransition = false;

            PlayStartTurnEffects();
            StartTurnDraw();
        }
    }

    void EndPlayerTurn()
    {
        IEnumerator EndPlayerTurn()
        {
            yield return new WaitForSeconds(TIME_DELAY_END_PLAYER_TURN);
            StartTurnBattle();
        }

        IsInNormalTurn = false;

        StartCoroutine(EndPlayerTurn());
    }

    void StartTurnBattle()
    {
        IEnumerator StartTurnBattle()
        {
            yield return new WaitForSeconds(TIME_DELAY_START_TURN_BATTLE);
            ChangeBattleState(BattleState.Battle);
            GetNextUnitToPlay();
        }

        isPlayingEndPlanEffects = false;
        StartCoroutine(StartTurnBattle());
    }

    void EndTurnBattle()
    {
        IEnumerator EndBattle()
        {
            yield return new WaitForSeconds(TIME_DELAY_END_TURN_BATTLE);
            //PreparePlayerTurn();
            GainUltPoint();
            StartPlayerTurn();
        }

        StartCoroutine(EndBattle());
    }

    bool IsPlayerBusy()
    {
        //if player is still drawing or still attacking or still casting or still showing effects return true, if not, return false;

        if (IsDrawingCards || isDiscardingHand)
        {
            return true;
        }

        return false;
    }

    //public void IncreaseMinionDefense(BattleEntity source, List<Minion> targetMinions, int amount)
    //{
    //    OnBeforeIncMinDefGetVal?.Invoke(source, ref amount);
    //    new ActionIncreaseDefense(source, targetMinions, amount).Start();
    //}

    //public void IncreaseMinionDefense(BattleEntity source, List<Minion> targetMinions, List<int> specifiedAmounts)
    //{
    //    int offset = 0;
    //    OnBeforeIncMinDefGetVal?.Invoke(source, ref offset);
    //    for (int i = 0; i < specifiedAmounts.Count; i++) specifiedAmounts[i] += offset;
    //    new ActionIncreaseDefense(source, targetMinions, specifiedAmounts).Start();
    //}

    public void PrepareInBattleDeck()
    {
        inBattleDeck.Clear();

        foreach (CardData playerCardData in PlayerData.data.PlayerDeck)
        {
            inBattleDeck.Add(playerCardData.Duplicate());
        }
        ShuffleItemsInList(inBattleDeck);
    }

    //Start of player's turn draw, effects will not be triggered
    public void StartTurnDraw()
    {
        if (BattleTurnCount == 1)
        {
            //for innate cards
            for (int i = inBattleDeck.Count - 1; i >= 0; i--) if (inBattleDeck[i].InHandInFirstTurn) PreDrawCardFromDeck(inBattleDeck[i]);
        }
        int drawCount = PlayerCharacter.pc.GetStartingDrawCount();
        if (drawCount > 0)
        {
            drawCount -= 1;
            safeDrawCount = 1;
            if (drawCount > 0) DrawCardFromDeck(drawCount);
            else SafeDrawCardFromDeck(safeDrawCount);
        }

    }

    public void DrawCardFromDeck(int amount) //draw a number of cards from deck
    {
        randomCardsToDrawCount += amount;
        StartDrawingCards();
    }

    public void DrawCardFromDeck(int amount, string textSearch)
    {
        if (amount > 0 && textSearch != null)
        {
            customCardDrawInfos.Enqueue(new CustomCardDrawInfo(amount, textSearch));
            StartDrawingCards();
        }
    }

    public void SeekCardFromDeck(int amount)
    {
        if (inBattleDeck.Count != 0)
        {
            randomCardsToSeekCount += amount;
            StartDrawingCards();
        }
    }

    void SafeDrawCardFromDeck(int amount)
    {
        safeDrawCount += amount;
        StartDrawingCards();
    }

    public void DrawMinionCardFromDeck(int amount)
    {
        minionCardsToDrawCount += amount;
        StartDrawingCards();
    }

    public void DrawSpellCardFromDeck(int amount)
    {
        spellCardsToDrawCount += amount;
        StartDrawingCards();
    }

    void StartDrawingCards()
    {
        if (!IsDrawingCards)
        {
            IsDrawingCards = true;
            cardDrawingCoroutine = StartCoroutine(DrawCardFromDeck());
        }
    }

    void PreStartingDrawCardsFromDeck()
    {
        bool HasMinionCardInHand()
        {
            foreach (CardData cardData in cardsToDrawFromDeck)
            {
                if (cardData is MinionCardData) return true;
            }

            foreach (Card card in GetPlayableCardsInHand())
            {
                if (card.CurrentCardData is MinionCardData) return true;
            }

            return false;
        }

        if (inBattleDeck.Count == 0) return;

        while (CardsToDrawCount > 0)
        {
            if (inBattleDeck.Count != 0)
            {
                if (randomCardsToDrawCount > 0)
                {
                    PreDrawCardFromDeck(inBattleDeck[inBattleDeck.Count - 1]);
                    randomCardsToDrawCount--;
                }
                else if (customCardDrawInfos.Count > 0)
                {
                    CustomCardDrawInfo drawInfo = customCardDrawInfos.Peek();
                    CardData cardToPreDraw = SearchRandomCardInBattleDeck(drawInfo.textToSearch);

                    if (cardToPreDraw == null) customCardDrawInfos.Dequeue();
                    else
                    {
                        PreDrawCardFromDeck(cardToPreDraw);
                        drawInfo.count--;
                        if (drawInfo.count <= 0) customCardDrawInfos.Dequeue();
                    }
                }
                else if (safeDrawCount > 0)
                {
                    if (!HasMinionCardInHand()) //if has no minion card
                    {
                        bool hasAddedMinionCard = false;

                        for (int i = inBattleDeck.Count - 1; i >= 0; i--)
                        {
                            if (inBattleDeck[i] is MinionCardData)
                            {
                                PreDrawCardFromDeck(inBattleDeck[i]);

                                minionCardsToDrawCount--;
                                hasAddedMinionCard = true;
                                break;
                            }
                        }

                        if (!hasAddedMinionCard)
                        {
                            randomCardsToDrawCount += 1;
                            safeDrawCount -= 1;
                        }
                    }
                    else //if has one
                    {
                        randomCardsToDrawCount += safeDrawCount;
                        safeDrawCount = 0;
                    }
                }
                else if (minionCardsToDrawCount > 0)
                {
                    bool hasAddedMinionCard = false;

                    for (int i = inBattleDeck.Count - 1; i >= 0; i--)
                    {
                        if (inBattleDeck[i] is MinionCardData)
                        {
                            PreDrawCardFromDeck(inBattleDeck[i]);

                            minionCardsToDrawCount--;
                            hasAddedMinionCard = true;
                            break;
                        }
                    }

                    if (!hasAddedMinionCard)
                    {
                        minionCardsToDrawCount = 0;
                    }
                }
                else if (spellCardsToDrawCount > 0)
                {
                    bool hasAddedSpellCard = false;

                    for (int i = inBattleDeck.Count - 1; i >= 0; i--)
                    {
                        if (inBattleDeck[i] is SpellCardData)
                        {
                            PreDrawCardFromDeck(inBattleDeck[i]);

                            spellCardsToDrawCount--;
                            hasAddedSpellCard = true;
                            break;
                        }
                    }

                    if (!hasAddedSpellCard)
                    {
                        spellCardsToDrawCount = 0;
                    }
                }
                else if (randomCardsToSeekCount > 0)
                {
                    PreDrawCardFromDeck(inBattleDeck[inBattleDeck.Count - 1]);
                    randomCardsToSeekCount--;

                    if (inBattleDeck.Count == 0 && randomCardsToSeekCount > 0) randomCardsToSeekCount = 0;
                }

                if (inBattleDeck.Count == 0) break;
            }
            else
            {
                randomCardsToDrawCount = 0;
                customCardDrawInfos.Clear();
                OnAfterDrawingCardGetCardEvents.Clear();
                safeDrawCount = 0;
                minionCardsToDrawCount = 0;
                spellCardsToDrawCount = 0;
                randomCardsToSeekCount = 0;
            }

            if (CardsToDrawCount == 0 && !HasMinionCardInHand()) //if has no minion card
            {
                cardsToDrawFromDeck.Add(backupMinionCardData.Duplicate());
            }
        }
    }

    public List<CardData> GetRandomUnderlingCardData(int count)
    {
        if (count < 0) count = 0;
        List<CardData> carDatList = new List<CardData>();

        for (int i = 0; i < count; i++)
        {
            carDatList.Add(GetItemFromListByChance(DataLibrary.underlCarDatList, DataLibrary.underlCarDatDrawChanceList).Duplicate());
        }
        return carDatList;
    }

    public List<CardData> GetCardDataListFromBattleDeckByCond(OnCheckCardDataEvent onCheckCardData)
    {
        List<CardData> cardDataList = new List<CardData>();

        if (onCheckCardData != null)
        {
            foreach (CardData cardData in inBattleDeck)
            {
                if (onCheckCardData.Invoke(cardData)) cardDataList.Add(cardData);
            }
        }

        return cardDataList;
    }

    public int GetCardDataCountFromBattleCountByCond(OnCheckCardDataEvent onCheckCardData)
    {
        int count = 0;
        if (onCheckCardData != null)
        {
            foreach (CardData cardData in inBattleDeck) if (onCheckCardData.Invoke(cardData)) count++;
        }

        return count;
    }

    CardData SearchRandomCardInBattleDeck(string textToFind)
    {
        CardData cardData = null;

        List<CardData> cardsFound = new List<CardData>();
        foreach (CardData card in inBattleDeck)
        {
            if (card.Name.Contains(textToFind))
            {
                cardsFound.Add(card);
                continue;
            }

            foreach (Spell spell in card.Spells)
            {
                if (spell.GetDescription().Contains(textToFind))
                {
                    cardsFound.Add(card);
                    break;
                }
            }
        }

        if (cardsFound.Count > 0)
        {
            cardData = cardsFound[Random.Range(0, cardsFound.Count)];
        }

        return cardData;
    }

    void PreDrawCardFromDeck(CardData cardData)
    {
        if (inBattleDeck.Contains(cardData))
        {
            cardsToDrawFromDeck.Add(cardData);
            inBattleDeck.Remove(cardData);
        }
    }

    public void DrawCardFromDeck(List<CardData> origCardDataList)
    {
        foreach (CardData cardData in origCardDataList) PreDrawCardFromDeck(cardData);
        StartDrawingCards();
    }

    Card DrawCardFromDeck(CardData originalCardData) //draw specified card data from deck
    {
        List<CardData> listToDrawCardDataFrom = null;
        if (inBattleDeck.Contains(originalCardData)) listToDrawCardDataFrom = inBattleDeck;
        else if (cardsToDrawFromDeck.Contains(originalCardData)) listToDrawCardDataFrom = cardsToDrawFromDeck;

        Card cardDrawnFromDeck = null;

        if (listToDrawCardDataFrom != null)
        {
            listToDrawCardDataFrom.Remove(originalCardData);
            cardDrawnFromDeck = SpawnCardInBattle(originalCardData);
            OnAfterDrawingCardGetCard?.Invoke(cardDrawnFromDeck);
            if (OnAfterDrawingCardGetCardEvents.Count > 0)
            {
                ActionDrawCard.AfterDrawEventInfo OnDrawEvent = OnAfterDrawingCardGetCardEvents.Peek();
                OnDrawEvent.afterDraw?.Invoke(cardDrawnFromDeck);
                OnDrawEvent.dur--;
                if (OnDrawEvent.dur <= 0) OnAfterDrawingCardGetCardEvents.Dequeue();
            }
            OnAfterDrawingCardGetCardReduce?.Invoke(cardDrawnFromDeck);
            UiHandler.UpdateDeckButtonTextValue();
        }

        return cardDrawnFromDeck;
    }

    public Card SpawnCardInBattle(CardData cardData)
    {
        Card card = ObjectPooler.SpawnCardFromPool(cardData, handZone);
        card.transform.position = cardSpawnPoint.position;

        CardData tempCardData = cardData.Duplicate();
        OnInitializeCardDataInBattle?.Invoke(cardData, tempCardData);
        card.Initialize(cardData, tempCardData, Card.State.UsedForBattle);
        //card.CheckSelectability();
        CheckCardsSelectability();

        return card;
    }

    public void AddCardsToHand(List<CardData> cardDataList)
    {
        foreach (CardData cardData in cardDataList)
        {
            CardData newCardData = cardData.Duplicate();
            SpawnCardInBattle(newCardData);
        }
    }

    public int GetInBattleDeckCount()
    {
        return inBattleDeck.Count + cardsToDrawFromDeck.Count;
    }

    IEnumerator DrawCardFromDeck()
    {
        PreStartingDrawCardsFromDeck();
        CheckCardsSelectability();
        foreach (Card card in GetPlayableCardsInHand())
        {
            card.DimCard(false);
        }

        bool willReturnCardsFromDiscardPileToDeck = false;

        while (cardsToDrawFromDeck.Count > 0 || CardsToDrawCount != 0) //cardsToDraw. count > 0 && cardsToDrawCount != 0
        {
            if (GetInBattleDeckCount() == 0 && discardPile.Count == 0)
            {
                minionCardsToDrawCount = 0;
                spellCardsToDrawCount = 0;
                randomCardsToDrawCount = 0;
                randomCardsToSeekCount = 0;
                safeDrawCount = 0;
                customCardDrawInfos.Clear();
                OnAfterDrawingCardGetCardEvents.Clear();
                break;
            }
            else if (GetInBattleDeckCount() == 0 && discardPile.Count != 0)
            {
                ReturnCardsFromDiscardPileToDeck();
                willReturnCardsFromDiscardPileToDeck = true;
                break;
            }
            else if (cardsToDrawFromDeck.Count == 0 && CardsToDrawCount != 0)
            {
                PreStartingDrawCardsFromDeck();
            }

            if (cardsToDrawFromDeck.Count > 0)
            {
                DrawCardFromDeck(cardsToDrawFromDeck[0]);
                yield return new WaitForSeconds(TIME_CARD_DRAW_INTERVAL);
            }
        }

        if (!willReturnCardsFromDiscardPileToDeck) IsDrawingCards = false;
        CheckCardsSelectability();
    }

    public void PauseDrawingCards()
    {
        if (!isCardDrawingPaused)
        {
            StopCoroutine(cardDrawingCoroutine);
            isCardDrawingPaused = true;
        }
    }

    public void ContinueDrawingCards()
    {
        if (isCardDrawingPaused)
        {
            isCardDrawingPaused = false;

            cardDrawingCoroutine = StartCoroutine(DrawCardFromDeck());
        }
    }

    void ReturnCardsFromDiscardPileToDeck()
    {
        SendCardDataToDrawPile(discardPile);
        discardPile.Clear();
        ShuffleItemsInList(inBattleDeck);
        UiHandler.UpdateDiscardPileButtonTextValue();
        OnModifyDiscardPile?.Invoke();
        OnDiscardPileToDeck?.Invoke();

        if (!isCardDrawingPaused) cardDrawingCoroutine = StartCoroutine(DrawCardFromDeck());
    }

    public bool CanSacrificeMinions(IEffectMinionSacrificing eff)
    {
        if (eff is MinionCardDataSacrificeToSummon mSac && mSac.canSkipSacrificing) return true;
        return GetMinionCount() > 0;
    }

    public bool CanSelectMinions(IEffectMinionSelecting eff)
    {
        return GetMinionCount() > 0;
    }

    public void StartSelectingMinionsOnField(IEffectMinionSelecting selectingEffect)
    {
        if (selectingEffect is IEffectMinionReturning returningEffect && CanReturnMinions(returningEffect) ||
            selectingEffect is IEffectMinionSacrificing sacrificingEffect && CanSacrificeMinions(sacrificingEffect) ||
            selectingEffect is PlayerSpell && CanSelectMinions(selectingEffect))
        {
            selectingEffect.SelectedMinions.Clear();
            hasSkipSelectingMinion = false;

            //ClearSummoningRelatedValues();
            if (selectingEffect.MinionSelectType == Spell.MINION_SELECT_TYPE_ALL_MINION_SELECT)
            {
                selectingEffect.SelectedMinions.AddRange(GetMinionsOnField());
                selectingEffect.HasSelectedMinions = true;
            }

            if (!selectingEffect.HasSelectedMinions)
            {
                IsSelectingMinionsOnField = true;
                this.selectingEffect = selectingEffect;

                if (selectingEffect.MinionSelectType == Spell.MINION_SELECT_TYPE_ANY_MINION_SELECT)
                {
                    void OnClickOkay()
                    {
                        selectingEffect.HasSelectedMinions = true;
                        PlayMinionSelectingEffect(selectingEffect);
                        selectingEffect = null;
                        IsSelectingMinionsOnField = false;
                    }

                    UiHandler.btnOkay.gameObject.SetActive(true);
                    UiHandler.btnOkay.onClick.RemoveAllListeners();
                    UiHandler.btnOkay.onClick.AddListener(OnClickOkay);
                    UiHandler.btnOkay.interactable = false;
                }

                if (selectingEffect is MinionCardDataSacrificeToSummon mSac && mSac.canSkipSacrificing)
                {
                    void OnClickSkip()
                    {
                        selectingEffect.HasSelectedMinions = true;
                        hasSkipSelectingMinion = true;
                        PlayMinionSelectingEffect(selectingEffect);
                        selectingEffect = null;
                        IsSelectingMinionsOnField = false;
                    }

                    UiHandler.btnSkip.gameObject.SetActive(true);
                    UiHandler.btnSkip.onClick.RemoveAllListeners();
                    UiHandler.btnSkip.onClick.AddListener(OnClickSkip);
                    UiHandler.btnSkip.interactable = true;
                }

                cardActionsPanel.Hide(true);
                ChangeBattleState(BattleState.SelectingMinions);             
            }
        }
    }

    public bool CanReturnMinions(IEffectMinionReturning returningEffect)
    {
        if (GetMinionCount() > 0)
        {
            return true;
        }

        return false;
    }

    public void ReturnMinionsToHand(List<Minion> minions)
    {
        foreach (Minion minion in minions)
        {
            minion.ReturnToHand();
        }

        OnFinishedReturningMinions?.Invoke();
    }

    public void ReturnMinionsToHandImmediately(List<Minion> minions)
    {
        foreach (Minion minion in minions)
        {
            minion.ReturnToHandImmediately();
        }

        OnFinishedReturningMinions?.Invoke();
    }

    public void SendMinionsOnTopOfDeck(List<Minion> minions)
    {
        minions.Reverse();
        foreach (Minion minion in minions) minion.SendOnTopOfDeck();
    }

    public void CancelSelectingMinions()
    {
        if (IsSelectingMinionsOnField)
        {
            IsSelectingMinionsOnField = false;
            selectingEffect = null;
            UnlockPlayerActions();
        }
    }

    public void PlayMinionSelectingEffect(IEffectMinionSelecting selectingEffect)
    {
        if (UiHandler.btnOkay.gameObject.activeSelf) UiHandler.btnOkay.gameObject.SetActive(false);
        if (UiHandler.btnCancel.gameObject.activeSelf) UiHandler.btnCancel.gameObject.SetActive(false);
        if (UiHandler.btnSkip.gameObject.activeSelf) UiHandler.btnSkip.gameObject.SetActive(false);

        if (selectingEffect is PlayerSpell)
        {
            PlayerSpell playerSpell = selectingEffect as PlayerSpell;
            if (playerSpell.Card != null) playerSpell.Card.CastSpell(playerSpell);
            else if (playerSpell.Minion != null) playerSpell.Minion.CastSpell(playerSpell);
        }
        else if (IsSummoning && currSummonInfo != null)
        {
            if (selectingEffect is MinionCardDataReturnToSummon minCardDataRet && currSummonInfo.currCardData == minCardDataRet)
            {
                ReturnMinionsToHandImmediately(minCardDataRet.SelectedMinions);
                minCardDataRet.OnReturnMinion?.Invoke();
                ChooseSummoningTile(currSummonInfo);
            }
            else if (IsSummoning && selectingEffect is MinionCardDataSacrificeToSummon minCardDataSac)
            {
                if (minCardDataSac.canSkipSacrificing && !hasSkipSelectingMinion) currSummonInfo.canBeCancelled = false;

                foreach (Minion min in minCardDataSac.SelectedMinions)
                {
                    min.Sacrifice();
                }
                minCardDataSac.OnSacrificeMinions?.Invoke();

                if (highPriorityActionQueue.Count > 0)
                {
                    PauseSummoning();
                }
                else ChooseSummoningTile(currSummonInfo);
                //if has actions in front of curr summoning action, pause the action and let the actions in front play then resume summoning              
            }
        }
    }

    void PauseSummoning()
    {
        Action unlockAct = currentAction;
        if (currentAction == null)
        {
            unlockAct = new ActionSummonMinion(PlayerCharacter.pc, summonInfoList);
            ActionMinionSummoning actS = unlockAct as ActionMinionSummoning;
            foreach (SummonInfo si in actS.summonInfoList) si.canBeCancelled = false;
            PlayActionAtFront(unlockAct);
            LockPlayerActions(unlockAct);
        }
        else
        {
            unlockAct = currentAction;
            LockPlayerActions(unlockAct);
            PlayActionAtFront(unlockAct);
            currentAction = null;
        }

        summonInfoList.Clear();
        currSummonInfo = null;
    }

    public void StartDiscardingCards(ActionDiscard actionDiscard)
    {
        if (CanDiscard(actionDiscard.sourceEffect, actionDiscard.sourceCard))
        {
            cardsToDiscard.Clear();
            discardingEffect = null;

            IsDiscarding = true;

            discardingEffect = actionDiscard.sourceEffect;
            hasDiscardLimit = actionDiscard.hasLimit;
            minCardsToDiscardCount = actionDiscard.minCount;
            maxCardsToDiscardCount = actionDiscard.maxCount;

            List<Card> discardableCards = GetPlayableCardsInHand();
            if (actionDiscard.sourceCard != null) discardableCards.Remove(actionDiscard.sourceCard);

            if (actionDiscard.type == ActionDiscard.Type.Random)
            {
                int discardCount = Random.Range(actionDiscard.minCount, actionDiscard.maxCount + 1);
                List<Card> cardsToDiscard = new List<Card>();

                for (int i = 0; i < discardCount; i++)
                {
                    if (discardableCards.Count > 0)
                    {
                        int randomIndex = Random.Range(0, discardableCards.Count);
                        cardsToDiscard.Add(discardableCards[randomIndex]);
                        discardableCards.RemoveAt(randomIndex);
                    }
                    else break;
                }

                DiscardCardsByEffect(cardsToDiscard);
            }
            else
            {
                for (int i = discardableCards.Count - 1; i >= 0; i--)
                {
                    if (actionDiscard.type == ActionDiscard.Type.MinionCardSelect)
                    {
                        if (!(discardableCards[i].CurrentCardData is MinionCardData))
                        {
                            discardableCards.RemoveAt(i);
                        }
                    }
                }

                //StartSelectingCardsInHand(discardableCards, minCardsToDiscardCount, maxCardsToDiscardCount, DiscardCardsByEffect);
            }
        }
    }

    public bool CanDiscard(IEffectDiscarding discardingEffect, Card sourceCard)
    {
        int discardableCardCount = 0;

        List<Card> cardsInHand = GetPlayableCardsInHand();
        if (sourceCard != null) cardsInHand.Remove(sourceCard);

        foreach (Card card in cardsInHand)
        {
            if (discardingEffect.DiscardType == ActionDiscard.TYPE_MINION_CARD_SELECT)
            {
                if (card.CurrentCardData is MinionCardData)
                {
                    discardableCardCount++;
                }
            }
            else
            {
                discardableCardCount++;
            }
        }

        if (discardingEffect.HasLimit)
        {
            if (discardableCardCount >= discardingEffect.MinDiscardCount)
            {
                return true;
            }
        }
        else
        {
            if (discardableCardCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    public void DiscardCardsByEffect(List<Card> cards)
    {
        IEnumerator Discard()
        {
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                cards[i].DiscardByEffect();
                OnDiscardByEffect?.Invoke();
                yield return new WaitForSeconds(TIME_INTERVAL_DISCARD);
            }

            UnlockPlayerActions();
            //if (IsInNormalTurn && battleState != BattleState.NormalTurn)
            //{
            //    ChangeBattleState(BattleState.NormalTurn);
            //}
            //else if (!IsInNormalTurn && battleState != BattleState.Battle)
            //{
            //    ChangeBattleState(BattleState.Battle);
            //}
        }

        recentlyDiscardedCards.Clear();
        recentlyDiscardedCards.AddRange(cards);
        if (discardingEffect != null) discardingEffect.HasDiscarded = true;
        IsDiscarding = false;

        StartCoroutine(Discard());
    }

    public void OnClickDiscardingCard(Card card)
    {
        if (IsDiscarding)
        {
            void UpdateOkayButton()
            {
                if (UiHandler.btnOkay.gameObject.activeSelf)
                {
                    if (UiHandler.btnOkay.IsInteractable())
                    {
                        if (cardsToDiscard.Count == 0)
                        {
                            UiHandler.btnOkay.interactable = false;
                        }
                    }
                    else
                    {
                        if (cardsToDiscard.Count > 0)
                        {
                            UiHandler.btnOkay.interactable = true;
                        }
                    }
                }
            }

            if (!cardsToDiscard.Contains(card))
            {
                if ((hasDiscardLimit && cardsToDiscard.Count != maxCardsToDiscardCount) || !hasDiscardLimit)
                {
                    cardsToDiscard.Add(card);

                    //if only has to discard 1 card, discard it:
                    if (maxCardsToDiscardCount == 1)
                    {
                        DiscardCardsByEffect(cardsToDiscard);
                        return;
                    }

                    UiHandler.AddCardToSelectedCardsDisplay(card);
                    UpdateOkayButton();
                }
            }
            else
            {
                cardsToDiscard.Remove(card);
                UiHandler.RemoveCardFromSelectedCardsDisplay(card);
                UpdateOkayButton();
            }
        }
    }

    void SetCardsInHandState(Card.State state)
    {
        foreach (Card card in GetPlayableCardsInHand()) card.SetState(state);
    }

    public void DuplicateCardInHand(List<Card> cards)
    {
        List<Card> hc = GetPlayableCardsInHand();

        for (int i = 0; i < cards.Count; i++)
        {
            if (hc.Contains(cards[i]))
            {
                Card card = SpawnCardInBattle(cards[i].OriginalCardData.Duplicate());
                bool foundInd = false;
                int ind = 0;
                for (int j = 0; j < hc.Count; j++)
                {
                    if (!foundInd && hc[j].OriginalCardData == cards[i].OriginalCardData)
                    {
                        card.handZoneIndex = j + 1;
                        foundInd = true;
                    }
                    else if (foundInd)
                    {
                        hc[j].handZoneIndex = j + 1;
                    }
                }

                hc.Insert(ind + 1, card);
                cards[i].Deselect();
                cards[i].DontHoverNextFrame();
            }
        }

        foreach (Card card in hc) card.PutCardToFront(false);
    }

    public bool CanSelectCardInHand(Spell.CardInHandSelType selType, int min, out List<Card> cards)
    {
        cards = new List<Card>();

        if (selType == Spell.CARD_IN_HAND_SEL_TYPE_MINION)
        {
            foreach (Card card in GetPlayableCardsInHand()) if (card.CurrentCardData is MinionCardData) cards.Add(card); 
        }
        else if (selType == Spell.CARD_IN_HAND_SEL_TYPE_MINION_ZERO_DEF)
        {
            foreach (Card card in GetPlayableCardsInHand()) if (card.CurrentCardData is MinionCardData mcd && mcd.Defense == 0) cards.Add(card);
        }
        else if (selType == Spell.CARD_IN_HAND_SEL_TYPE_SPELL)
        {
            foreach (Card card in GetPlayableCardsInHand()) if (card.CurrentCardData is SpellCardData) cards.Add(card);
        }

        if (cards.Count >= min) return true;
        return false;
    }

    public bool CanSelectCardInHand(Spell.CardInHandSelType selType, int min)
    {
        int count = 0;

        if (selType == Spell.CARD_IN_HAND_SEL_TYPE_MINION)
        {
            count = GetMinionCardCountInHand();
        }
        else if (selType == Spell.CARD_IN_HAND_SEL_TYPE_MINION_ZERO_DEF)
        {
            foreach (Card card in GetPlayableCardsInHand()) if (card.CurrentCardData is MinionCardData mcd && mcd.Defense == 0) count++;
        }
        else if (selType == Spell.CARD_IN_HAND_SEL_TYPE_SPELL)
        {
            foreach (Card card in GetPlayableCardsInHand()) if (card.CurrentCardData is SpellCardData) count++;
        }

        return count >= min;
    }

    public void StartSelectingCardsInHand(List<CardData> selectableCards, int min, int max, OnAfterSelectingCardsEvent OnAfterSelectingCards)
    {
        if (selectableCards.Count >= min)
        {
            ChangeBattleState(BattleState.SelectingCardsInHand);

            isSelectingCardsInHand = true;
            minSelectionCount = min;
            maxSelectionCount = max;
            selectedCards.Clear();
            //Change battle state

            void PlayEvent(List<Card> cards)
            {
                this.OnAfterSelectingCards -= PlayEvent;
                OnAfterSelectingCards(cards);
                isSelectingCardsInHand = false;
                UiHandler.btnOkay.onClick.RemoveAllListeners();
                UiHandler.btnOkay.gameObject.SetActive(false);
                if (!IsSummoning) UnlockPlayerActions();
            }
            this.OnAfterSelectingCards += PlayEvent;

            if (!(minSelectionCount == 1 && maxSelectionCount == 1))
            {
                void OnClickOkayButton()
                {
                    OnAfterSelectingCards?.Invoke(selectedCards);
                }

                UiHandler.btnOkay.gameObject.SetActive(true);
                UiHandler.btnOkay.onClick.RemoveAllListeners();
                UiHandler.btnOkay.onClick.AddListener(OnClickOkayButton);
                UiHandler.btnOkay.interactable = false;
            }

            List<Card> cardsInHand = GetPlayableCardsInHand();
            int c = 0;
            foreach (Card card in cardsInHand)
            {
                //if (card.isSelected) card.Deselect();
                card.SetState(Card.State.UsedForSelection);

                if (selectableCards.Contains(card.CurrentCardData) || selectableCards.Contains(card.OriginalCardData))
                {
                    card.SetCardSelectable(true);
                    card.SetGlowActive(true);
                    card.DimCard(false);
                    c++;
                }
                else
                {
                    card.SetCardSelectable(false);
                    card.SetGlowActive(false);
                    card.DimCard(true);
                }
            }
            SetEndTurnButtonActive(false);
        }
    }

    public void SelectCardInHand(Card card)
    {
        if (isSelectingCardsInHand)
        {
            void UpdateOkayButton()
            {
                if (UiHandler.btnOkay.gameObject.activeSelf)
                {
                    if (UiHandler.btnOkay.IsInteractable())
                    {
                        if (cardsToDiscard.Count == 0)
                        {
                            UiHandler.btnOkay.interactable = false;
                        }
                    }
                    else
                    {
                        if (cardsToDiscard.Count > 0)
                        {
                            UiHandler.btnOkay.interactable = true;
                        }
                    }
                }
            }

            if (!selectedCards.Contains(card))
            {
                selectedCards.Add(card);

                if (minSelectionCount == 1 && maxSelectionCount == 1)
                {
                    OnAfterSelectingCards?.Invoke(selectedCards);
                    return;
                }

                UiHandler.AddCardToSelectedCardsDisplay(card);
                UpdateOkayButton();
            }
            else
            {
                selectedCards.Remove(card);
                UiHandler.RemoveCardFromSelectedCardsDisplay(card);
                UpdateOkayButton();
            }
        }
    }

    public bool CanSummonMinionByEffect(IEffectSummoning eff, out List<CardData> cardChoices)
    {
        int availableSummonTileCount = GetAvailableSummonTileCount();
        cardChoices = new List<CardData>();

        if (availableSummonTileCount >= eff.MinSummonCount)
        {
            int summonableMinionCount = 0;

            if (eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.Deck) cardChoices.AddRange(inBattleDeck);
            if (eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.DiscardPile || eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.DiscardVanishPile) cardChoices.AddRange(discardPile);
            if (eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.VanishPile || eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.DiscardVanishPile) cardChoices.AddRange(removedFromPlayCards);
            if (eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.Hand)
            {
                foreach (Card card in GetPlayableCardsInHand()) if (card.CurrentCardData is MinionCardData) cardChoices.Add(card.CurrentCardData);
            }
            else if (eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.HandZeroDef)
            {
                foreach (Card card in GetPlayableCardsInHand()) if (card.CurrentCardData is MinionCardData mcd && mcd.Defense == 0) cardChoices.Add(card.CurrentCardData);
            }

            foreach (CardData cardData in cardChoices)
            {
                if (cardData is MinionCardData)
                {
                    MinionCardData minionCardData = cardData as MinionCardData;
                    if (CanSummonMinion(minionCardData))
                    {
                        summonableMinionCount++;
                        if (summonableMinionCount >= eff.MinSummonCount)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public bool CanTransferCards(IEffectTransfersCard eff)
    {
        if (eff.CardTransferType == ActionTransfersCard.Type.VanishedToDiscard || eff.CardTransferType == ActionTransfersCard.Type.VanishedToHand)
        {
            return removedFromPlayCards.Count >= eff.MinCardToTransfCount;
        }
        return false;
    }

    public void StartTransferingCards(ActionTransfersCard.Type type, int minCount, int maxCount, List<CardData> excluded)
    {
        List<CardData> cardsToShow = null;

        if (type == ActionTransfersCard.Type.VanishedToDiscard || type == ActionTransfersCard.Type.VanishedToHand) cardsToShow = new List<CardData>(removedFromPlayCards);

        foreach (CardData cd in excluded) if (cardsToShow.Contains(cd)) cardsToShow.Remove(cd);

        if (cardsToShow.Count > 0)
        {
            void SendCards(List<Card> cards)
            {
                List<CardData> l = new List<CardData>();
                foreach (Card c in cards) l.Add(c.OriginalCardData);

                if (type == ActionTransfersCard.Type.VanishedToDiscard) SendCardDataFromVanishedToDiscard(l);
                else if (type == ActionTransfersCard.Type.VanishedToHand) SendCardDataFromVanishedToHand(l);
            }

            UiHandler.ShowCardSelectScreen(cardsToShow, minCount, maxCount, SendCards);
        }
    }

    public void StartSummoningMinionByEffect(IEffectSummoning eff)
    {
        if (eff is ActionMinionSummoning actSumm && actSumm.hasSelectedMinionsToSummon) //if has selected, start summoning
        {
            StartSummoningMinion(actSumm.summonInfoList);
        }
        else if (CanSummonMinionByEffect(eff, out List<CardData> cardsToShow)) //if hasnt selected cards, show grid and select
        {
            int availableSummonTileCount = GetAvailableSummonTileCount();

            IsSelectingCardsToSummon = true;

            selectedCards.Clear();

            for (int i = cardsToShow.Count - 1; i >= 0; i--)
            {
                if (cardsToShow[i]._Type == CardData.TYPE_SPELL)
                {
                    cardsToShow.RemoveAt(i);
                }
                else if (!CanSummonMinion(cardsToShow[i]))
                {
                    cardsToShow.RemoveAt(i);
                }
            }

            void SummonSelectedCards(List<Card> cards)
            {
                UiHandler.HideSelectScreen();

                //remove cards from deck:
                List<CardData> carDatList = new List<CardData>();
                foreach (Card selectedCard in cards) carDatList.Add(selectedCard.OriginalCardData);

                if (eff is ActionMinionSummoning _actSumm && !_actSumm.hasSelectedMinionsToSummon)
                {
                    foreach (Card card in cards)
                    {
                        SummonInfo summInfo = _actSumm.AddCardDataToSummon(card.OriginalCardData);
                        summInfo.srcCard = card;
                        if (eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.Deck) summInfo.type = SummonInfo.TYPE_FROM_DECK;
                        else if (eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.DiscardPile) summInfo.type = SummonInfo.TYPE_FROM_DISCARD_PILE;
                        else if (eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.DiscardVanishPile) summInfo.type = SummonInfo.TYPE_FROM_DISCARD_VANISH_PILE;
                        else if (eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.VanishPile) summInfo.type = SummonInfo.TYPE_FROM_VANISH_PILE;
                        else if (eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.Hand || eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.HandZeroDef)
                        {
                            summInfo.type = SummonInfo.TYPE_FROM_HAND;
                        }
                    }
                    StartSummoningMinion(_actSumm.summonInfoList);
                }
            }
            //availableSummonTileCount < eff.MaxSummonCount ? availableSummonTileCount : eff.MaxSummonCount
            //show grid
            if (eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.Hand ||
                eff.CardSelectionLocation == SummonInfo.CardSelectionLocation.HandZeroDef)
            {
                StartSelectingCardsInHand(cardsToShow, eff.MinSummonCount, eff.MaxSummonCount, SummonSelectedCards);
            }
            else UiHandler.ShowCardSelectScreen(cardsToShow, eff.MinSummonCount, eff.MaxSummonCount, SummonSelectedCards);
        }
        else
        {
            //Show error message to player.
        }
    }

    public void OnAfterSelectingCardsInSelectionScreen()
    {
        UiHandler.HideSelectScreen();
        OnAfterSelectingCards?.Invoke(selectedCards);
        OnAfterSelectingCards = null;
    }

    public void OnClickCardInCardSelectionScreen(Card card)
    {
        void SelectCard(Card _card)
        {
            selectedCards.Add(_card);
            //show selected FX.
        }

        void DeselectCard(Card _card)
        {
            selectedCards.Remove(_card);
            //play deselected FX.
        }

        if (!selectedCards.Contains(card))
        {
            if (minSelectionCount == 1 && maxSelectionCount == 1)
            {
                SelectCard(card);
                OnAfterSelectingCardsInSelectionScreen();
                return;
            }
            else
            {
                if (selectedCards.Count < maxSelectionCount)
                {
                    SelectCard(card);

                    if (selectedCards.Count >= minSelectionCount) UiHandler.btnGridOkay.interactable = true;
                }
            }

            //if has reached max selection count:
            //disable non selected cards
        }
        else
        {
            DeselectCard(card);

            if (selectedCards.Count < minSelectionCount) UiHandler.btnGridOkay.interactable = false;
            //UiHandler.RemoveCardFromSelectedCardsDisplay(card);
            //if there are disabled non selected cards:
            //make them selectable again.
        }
    }

    public void OnClickItemInItemSelectionScreen(Item item)
    {
        void SelectItem(Item _item)
        {
            selectedItems.Add(_item);
        }

        void DeselectItem(Item _item)
        {
            selectedItems.Remove(_item);
        }

        if (!selectedItems.Contains(item))
        {
            SelectItem(item);

            if (minSelectionCount == 1 && maxSelectionCount == 1)
            {
                UiHandler.HideSelectScreen();
                OnAfterSelectingItems?.Invoke(selectedItems);
                OnAfterSelectingItems = null;
                return;
            }
        }
        else
        {
            DeselectItem(item);
        }
    }

    void SetEndTurnButtonActive(bool willDo)
    {
        UiHandler.btnEndTurn.interactable = willDo;
    }

    void HandToDiscardPile()
    {
        //Disable the card's colliders
        foreach (Transform cardTransfrom in handZone)
        {
            Card card = cardTransfrom.GetComponent<Card>();
            if (card != null)
            {
                card.IsCardMoving(false);
            }
        }
        DeselectCards();
        isDiscardingHand = true;

        StartCoroutine(DiscardHand());
    }

    public void ClearHand()
    {
        List<Card> cards = GetPlayableCardsInHand();

        for (int i = cards.Count - 1; i >= 0; i--)
        {
            ObjectPooler.DespawnUIObject(cards[i].gameObject);
        }
    }

    public void SendCardDataOnTopOfDeck(CardData origCardData)
    {
        inBattleDeck.Insert(inBattleDeck.Count, origCardData);
        UiHandler.UpdateDeckButtonTextValue();
    }

    public void ShuffleCardsToDrawPile(List<CardData> cardsToAdd)
    {
        HasFinishedShufflingCardsToCardList = false;
        DisplayShuffledCardsOnScreen(cardsToAdd);

        List<CardData> duplicatedCardDataList = new List<CardData>();
        foreach (CardData cardData in cardsToAdd)
        {
            duplicatedCardDataList.Add(cardData.Duplicate());
        }
        SendCardDataToDrawPile(cardsToAdd);
        ShuffleItemsInList(inBattleDeck);
    }

    public void ShuffleCardsToDiscardPile(List<CardData> cardsToAdd)
    {
        HasFinishedShufflingCardsToCardList = false;
        DisplayShuffledCardsOnScreen(cardsToAdd);

        //duplicate cardData then send to discard pile:
        List<CardData> duplicatedCardDataList = new List<CardData>();
        foreach (CardData cardData in cardsToAdd)
        {
            duplicatedCardDataList.Add(cardData.Duplicate());
        }
        SendCardDataToDiscardPile(duplicatedCardDataList);
    }

    public void ShuffleCardsToDrawAndDiscardPile(List<CardData> cardsToAddToDeck, List<CardData> cardsToAddToDiscardPile)
    {
        List<CardData> allCards = new List<CardData>();
        allCards.AddRange(cardsToAddToDeck);
        allCards.AddRange(cardsToAddToDiscardPile);

        HasFinishedShufflingCardsToCardList = false;
        DisplayShuffledCardsOnScreen(allCards);

        List<CardData> duplicatedCardDataToDeck = new List<CardData>();
        List<CardData> duplicatedCardDataToDiscardPile = new List<CardData>();
        foreach (CardData cardData in cardsToAddToDeck)
        {
            duplicatedCardDataToDeck.Add(cardData.Duplicate());
        }

        foreach (CardData cardData in cardsToAddToDiscardPile)
        {
            duplicatedCardDataToDiscardPile.Add(cardData.Duplicate());
        }

        SendCardDataToDrawPile(duplicatedCardDataToDeck);
        SendCardDataToDiscardPile(duplicatedCardDataToDiscardPile);

        ShuffleItemsInList(inBattleDeck);
    }

    void DisplayShuffledCardsOnScreen(List<CardData> cardDataList)
    {
        //display to screen
        //play anim
        //get anim time
        //after the anim time:
        HasFinishedShufflingCardsToCardList = true;
    }

    void SendCardDataToDrawPile(List<CardData> cardDataList)
    {
        inBattleDeck.AddRange(cardDataList);
        UiHandler.UpdateDeckButtonTextValue();
    }

    public void SendCardDataFromVanishedToDiscard(List<CardData> l)
    {
        foreach (CardData cd in l)
        {
            if (removedFromPlayCards.Contains(cd))
            {
                removedFromPlayCards.Remove(cd);
                OnModifyVanishedPile?.Invoke();
                SendCardDataToDiscardPile(cd);
                UiHandler.UpdateRemovedFromPlayCardsTextValue();
            }
        }
    }

    void SendCardDataFromVanishedToHand(List<CardData> l)
    {
        foreach (CardData cd in l)
        {
            if (removedFromPlayCards.Contains(cd))
            {
                removedFromPlayCards.Remove(cd);
                OnModifyVanishedPile?.Invoke();
                SpawnCardInBattle(cd);
                UiHandler.UpdateRemovedFromPlayCardsTextValue();
            }
        }
    }

    public void SendCardDataToDiscardPile(CardData cardData)
    {
        discardPile.Add(cardData);
        UiHandler.UpdateDiscardPileButtonTextValue();
        OnModifyDiscardPile?.Invoke();
    }

    void SendCardDataToDiscardPile(List<CardData> cardDataList)
    {
        discardPile.AddRange(cardDataList);
        UiHandler.UpdateDiscardPileButtonTextValue();
        OnModifyDiscardPile?.Invoke();
    }

    public void SendCardDataToVanishedPile(CardData cardData)
    {
        removedFromPlayCards.Add(cardData);
        UiHandler.UpdateRemovedFromPlayCardsTextValue();
        OnModifyVanishedPile?.Invoke();
    }

    void ClearVanishedPile()
    {
        removedFromPlayCards.Clear();
        OnModifyVanishedPile?.Invoke();
        if (UiHandler.btnVanishedPile.gameObject.activeSelf) UiHandler.btnVanishedPile.gameObject.SetActive(false);
    }

    public int VanishedPileMinionCardCount
    {
        get
        {
            int c = 0;
            foreach (CardData cd in removedFromPlayCards) if (cd is MinionCardData) c++;
            return c;
        }
    }

    void DeselectCards()
    {
        foreach (Transform cardTransfrom in handZoneFront)
        {
            Card card = cardTransfrom.GetComponent<Card>();
            if (card != null)
            {
                card.Deselect();
            }
        }
    }

    IEnumerator DiscardHand()
    {
        int totalChildCount = handZone.childCount + handZoneFront.childCount;

        List<Card> cardsInHand = GetPlayableCardsInHand();
        List<Card> retainedCards = new List<Card>();
        cardsInHand.Reverse();

        while (cardsInHand.Count > 0)
        {
            for (int i = cardsInHand.Count - 1; i >= 0; i--)
            {
                if (cardsInHand[i].willKeepInHand)
                {
                    retainedCards.Add(cardsInHand[i]);
                    cardsInHand.RemoveAt(i);
                    continue;
                }
                else
                {
                    Card card = cardsInHand[i];
                    cardsInHand.RemoveAt(i);

                    if (card.isTemporary) card.Vanish();
                    else card.Discard();
                    break;
                }
            }

            yield return new WaitForSeconds(discardHandTimeInterval);
        }

        if (retainedCards.Count > 0) foreach (Card card in retainedCards) card.IsCardMoving(true);
        isDiscardingHand = false;
    }

    void ShuffleItemsInList<T>(List<T> itemList)
    {
        List<T> tempList = new List<T>(itemList);
        itemList.Clear();

        int times = tempList.Count;

        for (int i = 0; i < times; i++)
        {
            int randomIndex = Random.Range(0, tempList.Count);
            itemList.Add(tempList[randomIndex]);
            tempList.RemoveAt(randomIndex);
        }
    }

    public void SetCardsIndex()
    {
        //Then rearranges their index if there's a missing index
        bool missingIndexFound;
        int indexDifference = 0;
        int currentIndex = 0;

        if (handZone.childCount + handZoneFront.childCount != 0)
        {
            for (int i = 0; i < handZone.childCount + handZoneFront.childCount; i++)
            {
                missingIndexFound = true;

                if (handZone.childCount != 0)
                {
                    foreach (RectTransform cardRect in handZone.transform)
                    {
                        Card card = cardRect.GetComponent<Card>();

                        if (card.handZoneIndex == currentIndex + indexDifference)
                        {
                            missingIndexFound = false;
                        }
                    }
                }

                if (handZoneFront.childCount != 0)
                {
                    foreach (RectTransform cardRect in handZoneFront.transform)
                    {
                        Card card = cardRect.GetComponent<Card>();

                        if (card.handZoneIndex == currentIndex + indexDifference)
                        {
                            missingIndexFound = false;
                        }
                    }
                }

                if (missingIndexFound)
                {
                    indexDifference++;
                }

                if (handZone.childCount != 0)
                {
                    foreach (RectTransform cardRect in handZone.transform)
                    {
                        Card card = cardRect.GetComponent<Card>();

                        if (card.handZoneIndex == currentIndex + indexDifference)
                        {
                            card.handZoneIndex -= indexDifference;
                        }
                    }
                }

                if (handZoneFront.childCount != 0)
                {
                    foreach (RectTransform cardRect in handZoneFront.transform)
                    {
                        Card card = cardRect.GetComponent<Card>();

                        if (card.handZoneIndex == currentIndex + indexDifference)
                        {
                            card.handZoneIndex -= indexDifference;
                        }
                    }
                }

                currentIndex++;
            }
        }

        //Assigns index to card once. Indeices are only assigned in the handzone
        foreach (RectTransform cardRect in handZone.transform)
        {
            Card card = cardRect.GetComponent<Card>();
            int largest = 0;

            if (!card.handZoneIndexSet)
            {
                if (handZone.childCount + handZoneFront.childCount == 0)
                {
                    card.handZoneIndex = card.transform.GetSiblingIndex();
                }
                else if (handZone.childCount + handZoneFront.childCount > 0)
                {
                    if (handZone.childCount != 0)
                    {
                        foreach (RectTransform cardRect_ in handZone.transform)
                        {
                            Card card_ = cardRect_.GetComponent<Card>();
                            if (card_.handZoneIndex > largest)
                            {
                                largest = card_.handZoneIndex;
                            }
                        }
                    }
                    if (handZoneFront.childCount != 0)
                    {
                        foreach (RectTransform cardRect_ in handZoneFront.transform)
                        {
                            Card card_ = cardRect_.GetComponent<Card>();
                            if (card_.handZoneIndex > largest)
                            {
                                largest = card_.handZoneIndex;
                            }
                        }
                    }
                    card.handZoneIndex = largest + 1;
                }

                card.handZoneIndexSet = true;
            }
        }
    }

    //public int GetCardTypeCountFromList(List<CardData> cardDataList, CardData.Type cardType)
    //{
    //    int count = 0;
    //    foreach (CardData cardData in cardDataList)
    //    {
    //        if (cardData.Type == cardType)
    //        {
    //            count++;
    //        }
    //    }

    //    return count;
    //}

    //public int GetSpellTypeCountFromList(List<CardData> cardDataList, Spell.SpellType spellType)
    //{
    //    int count = 0;
    //    foreach (CardData cardData in cardDataList)
    //    {
    //        foreach (Spell spell in cardData.Spell)
    //        {
    //            if (spell.Type == spellType)
    //            {
    //                count++;
    //                break;
    //            }
    //        }
    //    }

    //    return count;
    //}

    //public bool CanPayCost(int cost)
    //{
    //    if (PlayerData.CurrentEnergy >= cost)
    //    {
    //        return true;
    //    }

    //    return false;
    //}

    //public void PayEnergyCost(int amount)
    //{
    //    if (IsPayingCostToSummon)
    //    {
    //        PlayerData.ReduceCurrentEnergy(amount);
    //        UiHandler.UpdateEnergyIconTextValue();
    //    }
    //}

    //public void RefreshPlayerEnergy()
    //{
    //    if (PlayerData.CurrentEnergy < PlayerData.MaxEnergy)
    //    {
    //        IncreasePlayerEnergy(PlayerData.MaxEnergy - PlayerData.CurrentEnergy);
    //    }
    //}

    //public void IncreasePlayerEnergy(int amount)
    //{
    //    if (amount > 0)
    //    {
    //        PlayerData.IncreaseCurrentEnergy(amount);
    //        UiHandler.UpdateEnergyIconTextValue();
    //    }
    //}

    public bool AreAllTilesOccupied()
    {
        foreach (SummoningTile tile in summoningTiles) if (tile.CanBeSummonedOn()) return false;
        return true;
    }

    public void IncreaseDamageDealtThisTurnAtksSpls(int num)
    {
        dmgDealtInCurrTurnByPlayerAtksSpls += num;
    }    

    public int GetMinionCount()
    {
        int minionCount = 0;
        foreach (SummoningTile tile in summoningTiles)
        {
            if (tile.Minion != null && !tile.Minion.IsSlain) minionCount++;
        }

        return minionCount;
    }

    public int GetSummonableCardCountInBattleDeck()
    {
        int count = 0;

        foreach (CardData cardData in inBattleDeck)
        {
            if (CanSummonMinion(cardData)) count++;
        }

        return count;
    }

    public void RemoveAllCardsColliders(bool willDo)
    {
        if (handZone.childCount != 0)
        {
            foreach (Transform cardTransform in handZone)
            {
                Card card = cardTransform.GetComponent<Card>();
                card.SetRaycastable(!willDo);
            }
        }
        if (handZoneFront.childCount != 0)
        {
            foreach (Transform cardTransform in handZoneFront)
            {
                Card card = cardTransform.GetComponent<Card>();
                card.SetRaycastable(!willDo);
            }
        }
    }

    public void RemoveAllMinionColliders(bool willDo)
    {
        foreach (SummoningTile tile in summoningTiles)
        {
            if (tile.Minion != null)
            {
                BoxCollider2D collider = tile.Minion.GetComponent<BoxCollider2D>();
                collider.enabled = !willDo;
            }
        }
    }

    void InitializeMinionsForTargeting(List<Minion> minions)
    {
        foreach(Minion minion in GetMinionsOnField())
        {
            if (minions.Contains(minion))
            {
                minion.collider.enabled = true;
                minion.SetState(Minion.State.UsedForSelection);
            }
            else
            {
                minion.collider.enabled = false;
            }
        }
    }

    void ClearSummoningRelatedValues()
    {
        currSummonInfo = null;
        summonInfoList.Clear();
        IsSummoning = false;
    }

    bool CanSummonMinion(SummonInfo summInfo)
    {
        if (summInfo != null)
        {
            if (CanSummonMinion(summInfo.currCardData))
            {
                if (summInfo.type == SummonInfo.TYPE_FROM_HAND || summInfo.type == SummonInfo.TYPE_BY_CARD)
                {
                    foreach (Card card in GetPlayableCardsInHand()) if (card.OriginalCardData == summInfo.origCardData) return true;
                }
                else if (summInfo.type == SummonInfo.TYPE_FROM_DECK)
                {
                    foreach (CardData cd in inBattleDeck) if (cd == summInfo.origCardData) return true;
                }
                else if (summInfo.type == SummonInfo.TYPE_FROM_DISCARD_PILE)
                {
                    foreach (CardData cd in discardPile) if (cd == summInfo.origCardData) return true;
                }
                else if (summInfo.type == SummonInfo.TYPE_FROM_VANISH_PILE)
                {
                    foreach (CardData cd in removedFromPlayCards) if (cd == summInfo.origCardData) return true;
                }
                else if (summInfo.type == SummonInfo.TYPE_FROM_DISCARD_VANISH_PILE)
                {
                    foreach (CardData cd in discardPile) if (cd == summInfo.origCardData) return true;
                    foreach (CardData cd in removedFromPlayCards) if (cd == summInfo.origCardData) return true;
                }
                else if (summInfo.type == SummonInfo.TYPE_OUT_OF_DECK)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool CanSummonMinion(Card c)
    {
        bool canPayCost = ((c.CurrentCardData.WillNotPayCost || PlayerCharacter.pc.CanPayEnergy(c.Cost)) || PlayerCharacter.pc.WillSkipCostOnInsuffMana);
        return canPayCost && CanSummonMinion(c.CurrentCardData);
    }

    public bool CanSummonMinion(CardData CurrentCardData)
    {
        if (CurrentCardData is SpellCardData)
        {
            return false;
        }

        if (CurrentCardData is MinionCardData)
        {
            if (CurrentCardData is MinionCardDataReturnToSummon minCardDataRet)
            {
                return CanReturnMinions(minCardDataRet) || minCardDataRet.HasSelectedMinions;
            }
            else if (CurrentCardData is MinionCardDataSacrificeToSummon minCardDataSac)
            {
                return (CanSacrificeMinions(minCardDataSac) && (GetAvailableSummonTileCount() + (GetMinionCount() - GetMinionCountSummonsOnOwnTileOnLeave()) > 0)) || minCardDataSac.HasSelectedMinions && GetAvailableEnemyTileCount() > 0;
            }
            else if (GetAvailableSummonTileCount() > 0)
            {
                return true;
            }
        }

        return false;
    }

    void SummonMinion(SummonInfo summonInfo) //fix for cards with minion selecting effect OR maybe remove this 
    {
        if (summonInfo != null)
        {
            Minion summonedMinion = null;

            if (summonInfo.targetSummTile != null && summonInfo.targetSummTile.CanBeSummonedOn())
            {
                if (summonInfo.srcCard != null) summonInfo.srcCard.willRemove = true;
                summonedMinion = summonInfo.targetSummTile.SummonMinion(summonInfo);
                OnAfterSummoningMinionGetMinion?.Invoke(summonedMinion);

                if (currentAction != null && currentAction is ActionMinionSummoning actM) actM.OnAfterSummoningMinionGetMinion?.Invoke(summonedMinion);
                summonedMinions.Add(summonedMinion);

                if (summonInfo.type == SummonInfo.TYPE_FROM_DECK && inBattleDeck.Contains(summonInfo.origCardData))
                {
                    inBattleDeck.Remove(summonInfo.origCardData);
                    UiHandler.UpdateDeckButtonTextValue();
                }
                else if ((summonInfo.type == SummonInfo.TYPE_FROM_DISCARD_PILE || summonInfo.type == SummonInfo.TYPE_FROM_DISCARD_VANISH_PILE) && discardPile.Contains(summonInfo.origCardData))
                {
                    discardPile.Remove(summonInfo.origCardData);
                    UiHandler.UpdateDiscardPileButtonTextValue();
                }
                else if ((summonInfo.type == SummonInfo.TYPE_FROM_VANISH_PILE || summonInfo.type == SummonInfo.TYPE_FROM_DISCARD_VANISH_PILE) && removedFromPlayCards.Contains(summonInfo.origCardData))
                {
                    removedFromPlayCards.Remove(summonInfo.origCardData);
                    UiHandler.UpdateRemovedFromPlayCardsTextValue();
                }
                else if (summonInfo.type == SummonInfo.TYPE_FROM_HAND) summonInfo.srcCard.AfterSummon();
            }

            RemoveSummonInfo(summonInfo);

            if (summonInfo.type == SummonInfo.TYPE_BY_CARD)
            {
                AfterSummonByCard(summonInfo.srcCard, summonedMinion);
            }
            else
            {
                if (summonInfoList.Count != 0)
                {
                    summonInfo.srcCard?.AfterSummon();

                    if (highPriorityActionQueue.Count > 0) PauseSummoning();
                    else ChooseSummoningTile(summonInfoList[0]);
                }
                else
                {
                    summonInfo.srcCard?.AfterSummon();
                    AfterSummoning();
                }
            }
        }
    }

    public void StartSummoningMinionByCard(Card card, bool canBeCancelled)
    {
        if (card.CurrentCardData is MinionCardData)
        {
            //go select summonign tile
            SummonInfo summonInfo = new SummonInfo();
            summonInfo.srcCard = card;
            summonInfo.currCardData = card.CurrentCardData;
            summonInfo.origCardData = card.OriginalCardData;
            summonInfo.type = SummonInfo.TYPE_BY_CARD;
            summonInfo.canBeCancelled = canBeCancelled;

            ChooseSummoningTile(new List<SummonInfo>() { summonInfo });
        }
    }

    public void StartSummoningMinion(List<Card> cardsToSummon)
    {
        List<SummonInfo> summonInfoList = new List<SummonInfo>();

        for (int i = 0; i < cardsToSummon.Count; i++)
        {
            SummonInfo summonInfo = new SummonInfo();
            summonInfo.srcCard = cardsToSummon[i];
            summonInfo.currCardData = cardsToSummon[i].CurrentCardData;
            summonInfo.origCardData = cardsToSummon[i].OriginalCardData;
            summonInfoList.Add(summonInfo);
        }

        ChooseSummoningTile(summonInfoList);
    }

    public void StartSummoningMinion(List<SummonInfo> summonInfoList)
    {
        ChooseSummoningTile(summonInfoList);
    }

    public void StartSummoningMinion(SummonInfo summonInfo)
    {
        ChooseSummoningTile(summonInfo);
    }

    public void StartSummoningMinion(List<CardData> cardDataListToSummon)
    {
        List<SummonInfo> summonInfoList = new List<SummonInfo>();

        for (int i = 0; i < cardDataListToSummon.Count; i++)
        {
            SummonInfo summonInfo = new SummonInfo();
            summonInfo.currCardData = cardDataListToSummon[i];
            summonInfo.origCardData = cardDataListToSummon[i];
            summonInfoList.Add(summonInfo);
        }

        ChooseSummoningTile(summonInfoList);
    }

    //void StartSummoningMinion(List<Card> cardsToSummon, List<StatusEffect> statusEffectsToApply)
    //{
    //    this.cardsToSummon.Clear();
    //    this.cardsToSummon.AddRange(cardsToSummon);
    //    summoningCard = this.cardsToSummon[0];
    //    this.cardsToSummon.RemoveAt(0);

    //    willApplyStatusEffectToSummonedMinion = true;
    //    statusEffectsToApplyToSummonedMinion.Clear();
    //    statusEffectsToApplyToSummonedMinion.AddRange(statusEffectsToApply);

    //    ChooseSummoningTile(summoningCard.CurrentCardData, summoningCard.OriginalCardData);
    //}

    public void AfterSummonByCard(Card card, Minion summonedMinion)
    {
        bool willPayCost = true;
        if (card.CurrentCardData.WillNotPayCost) willPayCost = false;

        if (willPayCost && PlayerCharacter.pc.CanPayEnergy(card.Cost) && card.CurrentCardData is MinionCardData)
        {
            PlayerCharacter.pc.ReduceCurrentEnergy(card.Cost);
        }
        else if (willPayCost && !PlayerCharacter.pc.CanPayEnergy(card.Cost) && PlayerCharacter.pc.WillSkipCostOnInsuffMana && summonedMinion != null) PlayerCharacter.pc.OnSkipCostOnInsuffManaGetMinion(summonedMinion);

        //ObjectPooler.DespawnUIObject(card.gameObject);
        card.AfterSummon();
        AfterSummoning();
    }

    public void AfterSummoning()
    {
        if (UiHandler.btnCancel.gameObject.activeSelf) UiHandler.btnCancel.gameObject.SetActive(false);

        ActionMinionSummoning actM = null;
        actM = currentAction as ActionMinionSummoning;

        OnAfterSummoningMinionsGetMinions?.Invoke(summonedMinions);
        actM?.OnAfterSummoningMinionsGetMinions?.Invoke(summonedMinions);
        OnAfterSummoningMinions?.Invoke();
        actM?.OnAfterSummoningMinions?.Invoke();
        summonedMinions.Clear();

        if (IsInNormalTurn)
        {
            EnableCards(true);
            SetEndTurnButtonActive(true);
        }
        if (IsSelectingCardsToSummon)
        {
            IsSelectingCardsToSummon= false;
        }

        UnlockPlayerActions();
        ClearSummoningRelatedValues();

        //if (IsInNormalTurn && battleState != BattleState.NormalTurn) ChangeBattleState(BattleState.NormalTurn);
        //else if (!IsInNormalTurn && battleState != BattleState.Battle) ChangeBattleState(BattleState.Battle);
    }

    void ChooseSummoningTile(List<SummonInfo> summonInfoList)
    {
        for (int i = 0; i < summonInfoList.Count; i++)
        {
            ChooseSummoningTile(summonInfoList[i]);
        }
    }

    void RemoveSummonInfo(SummonInfo summonInfo)
    {
        if (summonInfo != null)
        {
            summonInfoList.Remove(summonInfo);
            if (currentAction != null && currentAction is ActionMinionSummoning actSumm) actSumm.summonInfoList.Remove(summonInfo);
            if (currSummonInfo == summonInfo) currSummonInfo = null;
        }
    }

    void QueueSummonInfo(SummonInfo summonInfo)
    {
        if (!summonInfoList.Contains(summonInfo)) summonInfoList.Add(summonInfo);
        else
        {
            summonInfoList.Remove(summonInfo);
            currSummonInfo = null;
        }
        if (currSummonInfo == null) currSummonInfo = summonInfo;
    }

    void ChooseSummoningTile(SummonInfo summonInfo)
    {      
        if (!IsSummoning)
        {
            IsSummoning = true;
            EnableCards(false);
            cardActionsPanel.Hide(true);
        }

        QueueSummonInfo(summonInfo);

        if (currSummonInfo == summonInfo)
        {
            //display text: choose tile to summon X
            //check if can summon:
            if (!CanSummonMinion(currSummonInfo))
            {
                Debug.Log("CANNOT SUMMON: " + currSummonInfo.origCardData.Name);
                RemoveSummonInfo(currSummonInfo);
                if (summonInfoList.Count > 0)
                {
                    QueueSummonInfo(summonInfoList[0]);
                    ChooseSummoningTile(currSummonInfo);
                    return;
                }
                else
                {
                    AfterSummoning();
                    return;
                }
            }

            //for minion cards that has to select minions to summon:
            if (currSummonInfo.currCardData is IEffectMinionSelecting minSel)
            {
                if (!minSel.HasSelectedMinions)
                {
                    void CancelSelecting()
                    {
                        CancelSelectingMinions();
                        OnClickCancelSummoning();
                        currSummonInfo.srcCard?.Deselect();
                    }

                    if (!(minSel is MinionCardDataSacrificeToSummon msac && msac.canSkipSacrificing) && currSummonInfo.canBeCancelled)
                    {
                        UiHandler.btnCancel.gameObject.SetActive(true);
                        UiHandler.btnCancel.onClick.RemoveAllListeners();
                        UiHandler.btnCancel.onClick.AddListener(CancelSelecting);
                    }

                    if (!(minSel is MinionCardDataSacrificeToSummon mSac && mSac.canSkipSacrificing && GetMinionCount() == 0))
                    {
                        StartSelectingMinionsOnField(minSel);
                        return;
                    }
                }
            }

            //if is forced or has set summ tile, dont choose tile
            if (summonInfo.isForced)
            {
                if (summonInfo.targetSummTile == null) summonInfo.targetSummTile = GetAvailableSummoningTileFront();
                SummonMinion(summonInfo);
            }
            else if (summonInfo.targetSummTile != null)
            {
                SummonMinion(summonInfo);

            }//if has set tile
            else //Select summ tile
            {
                if (currSummonInfo.canBeCancelled && !(currSummonInfo.currCardData is MinionCardDataReturnToSummon))
                {
                    UiHandler.btnCancel.gameObject.SetActive(true);
                    UiHandler.btnCancel.onClick.RemoveAllListeners();
                    UiHandler.btnCancel.onClick.AddListener(OnClickCancelSummoning);
                }

                ChangeBattleState(BattleState.Summoning);
            }
        }
    }

    //ChooseSummoningTile(bool canChooseTile) if can;t, choose randomly or according to the carddata
    public void OnClickCancelSummoning()
    {
        if (IsSummoning)
        {
            currSummonInfo?.srcCard?.Deselect();

            ClearSummoningRelatedValues();

            EnableCards(true);
            UiHandler.btnCancel.gameObject.SetActive(false);
            UnlockPlayerActions();
        }
    }

    void LockPlayerActions(Action unlockingAction)
    {
        if (IsActionInQueues(unlockingAction, out Queue<Action> q))
        {
            turnUnlockingAction = unlockingAction;
            IsInLockedNormalTurn = true;
            ChangeBattleState(BattleState.LockedNormalTurn);
        }
        //add  battlestate locked
        //when other functions try to switch to normal turn while locked, it should switch to battlestate locked instead
        //if summoning paused, cards involved in hand should be not interactable by other effects such as being selectable
        //when summoning is resume, put involved card to hand.
    }

    void UnlockPlayerActions() //NOTE: Might add switch battleState to BattleState.Battle if not in summ phase
    {
        if (IsInLockedNormalTurn) ChangeBattleState(BattleState.LockedNormalTurn);
        else
        {
            if (!IsInNormalTurn)
            {
                if (isPlayingEndTurnEffects) ChangeBattleState(BattleState.EndingBattle);
                else ChangeBattleState(BattleState.Battle);
            }
            else
            {
                if (isPlayingEndPlanEffects) ChangeBattleState(BattleState.EndingPlayerTurn);
                else ChangeBattleState(BattleState.NormalTurn);
            }
        }
    }

    public int GetAvailableSummonTileCount()
    {
        int count = 0;
        foreach (SummoningTile tile in summoningTiles)
        {
            if (tile.CanBeSummonedOn())
            {
                count++;
            }
        }

        return count;
    }

    public int GetMinionCountSummonsOnOwnTileOnLeave()
    {
        int count = 0;
        foreach(Minion minion in GetMinionsOnField())
        {
            if (minion.CurrentCardData is MinionCardData minCarDat && minCarDat.SummonsOnTileOnWhenSlain()) count++;
        }

        return count;
    }

    List<SummoningTile> GetAvailableSummoningTiles()
    {
        List<SummoningTile> availableTiles = new List<SummoningTile>();

        foreach (SummoningTile tile in summoningTiles)
        {
            if (tile.CanBeSummonedOn()) availableTiles.Add(tile);
        }

        return availableTiles;
    }

    SummoningTile GetAvailableSummoningTileFront()
    {
        foreach (SummoningTile tile in summoningTiles)
        {
            if (tile.CanBeSummonedOn()) return tile;
        }

        return null;
    }

    List<SummoningTile> GetActiveSummoningTiles()
    {
        List<SummoningTile> availableTiles = new List<SummoningTile>();

        foreach (SummoningTile tile in summoningTiles)
        {
            if (!tile.IsDisabled) availableTiles.Add(tile);
        }

        return availableTiles;
    }
    public void OnClickEndTurn()
    {
        if (IsInNormalTurn)
        {
            HasEndedTurn = true;
            ChangeBattleState(BattleState.EndingPlayerTurn);

            OnEndOfSummoningPhase?.Invoke();
            PlayerCharacter.pc.ClearEnergy();
            PlayEndPlanEffects();

            if (!IsDrawingCards) //sent this here from top
            {
                HandToDiscardPile();
            }
            else willDiscardAfterDrawingAtEndTurn = true;
        }
    }

    bool CanRandomlySelectTarget(IEffectTargeting targetingEffect)
    {
        if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ALL_UNIT_SELECT ||
            targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_PLAYER_SELECT)
        {
            return false;
        }

        bool canRandomizeTarget = false;

        if (targetingEffect is EnemySpell spell)
        {
            canRandomizeTarget = spell.Enemy.CanTargetRandomly;
        }

        if (canRandomizeTarget)
        {
            int targetCount = GetMinionCount() + GetEnemyCount();
            if (targetingEffect is EnemySpell && targetingEffect is IEffectDamaging) targetCount++;
            Debug.Log(targetCount > 0);
            return targetCount > 0;
        }

        
        return false;
    }

    public bool CanSelectTarget(IEffectTargeting targetingEffect)
    {
        if (CanRandomlySelectTarget(targetingEffect)) return true;

        if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ENEMY_SELECT ||
            targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_FRONT_ENEMY ||
            targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_RANDOM_ENEMY)
        {
            if (targetingEffect is EnemySpell && GetEnemyCount() > 0) return true;
            else if (GetTargetableEnemyCount() > 0) return true;
            return false;
        }
        else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ALL_ENEMY_SELECT)
        {
            if (targetingEffect is PlayerSpell)
            {
                if (GetTargetableEnemyCount() > 0) return true;
                else return false;
            }
            else
            {
                return true;
            }
        }
        if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_MINION_SELECT)
        {
            if (GetMinionCount() > 0) return true;
            else return false;
        }
        else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ALL_MINION_SELECT)
        {
            if (targetingEffect is PlayerSpell)
            {
                if (GetMinionCount() > 0) return true;
                else return false;
            }
            else return true;
        }
        else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_OTHER_MINION)
        {
            if (targetingEffect is PlayerSpell)
            {
                PlayerSpell playerSpell = targetingEffect as PlayerSpell;
                if (GetMinionCount() > 1) return true;
                else return false;
            }
            else
            {
                Debug.LogError("Target selection type: other minion select should only be used by player spells.");
                return false;
            }
        }
        else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ELITE_ENEMY_SELECT)
        {
            foreach (Enemy enemy in GetEnemiesOnField()) if (enemy.enemyData._Type == EnemyData.TYPE_ELITE) return true;
            return false;
        }
        else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_NON_BOSS_ENEMY_SELECT)
        {
            foreach (Enemy enemy in GetEnemiesOnField()) if (enemy.enemyData._Type != EnemyData.TYPE_BOSS) return true;
            return false;
        }
        else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_NONE)
        {
            return false;
        }
        else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_BEHIND_THIS)
        {
            if (targetingEffect is PlayerSpell spell && spell.Minion != null && GetMinionExactlyBehindOf(spell.Minion) != null) return true;
            return false;
        }
        else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_FRONT_OF_THIS)
        {
            if (targetingEffect is PlayerSpell s && s.Minion != null && GetMinionExactlyInFrontOf(s.Minion) != null) return true;
            return false;
        }
        else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_SINGLE_DIGIT_ATK_MINION)
        {
            foreach (Minion minion in GetMinionsOnField()) if (minion.Attack <= 9) return true;
            return false;
        }
        else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_BEHIND_MINIONS_ONLY)
        {
            return GetMinionCount() > 1;
        }
        else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ALL_ZERO_DEF_MINION)
        {
            return GetMinionsOnFieldWithZeroDef().Count > 0;
        }
        else
        {
            return true;
        }
    }

    public List<BattleEntity> GetRandomEffectTarget(IEffectRandomizesTarget targetRandomizer, Spell.TargetSelectionType defTargetSelectType, bool isAnAttack)
    {
        if (defTargetSelectType == Spell.TARGET_SELECTION_TYPE_MINION_SELECT ||
            defTargetSelectType == Spell.TARGET_SELECTION_TYPE_ENEMY_SELECT ||
            defTargetSelectType == Spell.TARGET_SELECTION_TYPE_SELF_TARGET ||
            defTargetSelectType == Spell.TARGET_SELECTION_TYPE_FRONT_ENEMY ||
            defTargetSelectType == Spell.TARGET_SELECTION_TYPE_BEHIND_MINION ||
            defTargetSelectType == Spell.TARGET_SELECTION_TYPE_FRONT_MINION ||
            defTargetSelectType == Spell.TARGET_SELECTION_TYPE_RANDOM_ENEMY)
        {
            List<BattleEntity> targets = new List<BattleEntity>();
            List<BattleEntity> potentialTargets = new List<BattleEntity>();
            potentialTargets.AddRange(GetTargetableEnemies());
            potentialTargets.AddRange(GetTargetableMinions());

            if (targetRandomizer.Source is Enemy && GetMinionCount() == 0 && isAnAttack) potentialTargets.Add(PlayerCharacter.pc);
            targets.Add(potentialTargets[Random.Range(0, potentialTargets.Count)]);
            Debug.Log("Potential random targets count: " + potentialTargets.Count);
            return targets;
        }
        else if (defTargetSelectType == Spell.TARGET_SELECTION_TYPE_ALL_MINION_SELECT ||
            defTargetSelectType == Spell.TARGET_SELECTION_TYPE_ALL_ENEMY_SELECT)
        {
            List<List<BattleEntity>> potentialTargets = new List<List<BattleEntity>>();
            if (GetEnemyCount() > 0) potentialTargets.Add(new List<BattleEntity>(GetEnemiesOnField()));
            if (GetMinionCount() > 0) potentialTargets.Add(new List<BattleEntity>(GetMinionsOnField()));
            else if (isAnAttack) potentialTargets.Add(new List<BattleEntity>() { PlayerCharacter.pc });

            return potentialTargets[Random.Range(0, potentialTargets.Count)];
        } 
        //Add one for other minion

        return null;
    }

    public void GetEffectTarget(IEffectTargeting targetingEffect)
    {
        if (CanRandomlySelectTarget(targetingEffect))
        {
            targetingEffect.TargetList.Clear();

            bool isAnAttack = false;
            IEffectRandomizesTarget randomizingEffect = null;
            if (targetingEffect is EnemySpell enemySpell)
            {
                randomizingEffect = enemySpell.Enemy.TargetRandomizer;
                if (enemySpell is IEffectDamaging) isAnAttack = true;
            }

            targetingEffect.TargetList.AddRange(GetRandomEffectTarget(randomizingEffect, targetingEffect._TargetSelectionType, isAnAttack));
            if (targetingEffect.TargetList.Count != 0)
            {
                targetingEffect.HasSelectedTarget = true;
                return;
            }
        }

        if (CanSelectTarget(targetingEffect))
        {
            targetingEffect.TargetList.Clear();

            if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ALL_ENEMY_SELECT)
            {
                if (targetingEffect is EnemySpell)
                {
                    targetingEffect.TargetList.AddRange(GetEnemiesOnField());
                }
                else
                {
                    targetingEffect.TargetList.AddRange(GetTargetableEnemies());
                }

                targetingEffect.HasSelectedTarget = true;
            }
            else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_SELF_TARGET)
            {
                if (targetingEffect is PlayerSpell)
                {
                    PlayerSpell playerSpell = targetingEffect as PlayerSpell;
                    targetingEffect.TargetList.Add(playerSpell.Minion);
                }
                else if (targetingEffect is EnemySpell)
                {
                    EnemySpell enemySpell = targetingEffect as EnemySpell;
                    targetingEffect.TargetList.Add(enemySpell.Enemy);
                }
                targetingEffect.HasSelectedTarget = true;
            }
            else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ALL_MINION_SELECT)
            {
                if (targetingEffect is EnemySpell)
                {
                    if (GetTargetableMinionCount() == 0)
                    {
                        if (PlayerCharacter.pc.IsTargetable())
                        {
                            targetingEffect.TargetList.Add(PlayerCharacter.pc);
                            Debug.LogWarning("Targeted player");
                        }
                        else Debug.LogWarning("Player not targeted :(");
                    }
                    else targetingEffect.TargetList.AddRange(GetTargetableMinions());
                }
                else
                {
                    targetingEffect.TargetList.AddRange(GetMinionsOnField());
                }
                targetingEffect.HasSelectedTarget = true;
            }
            else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_FRONT_ENEMY)
            {
                if (targetingEffect is EnemySpell)
                {
                    targetingEffect.TargetList.Add(GetEnemyInFront());
                }
                else
                {
                    foreach (Enemy enemy in GetEnemiesOnField())
                    {
                        if (enemy.IsTargetable())
                        {
                            targetingEffect.TargetList.Add(enemy);
                            break;
                        }
                    }
                }
                
                targetingEffect.HasSelectedTarget = true;
            }
            else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_PLAYER_SELECT)
            {
                if (targetingEffect is EnemySpell)
                {
                    targetingEffect.TargetList.Add(PlayerCharacter.pc);
                }
                else
                {
                    if (PlayerCharacter.pc.IsTargetable()) targetingEffect.TargetList.Add(PlayerCharacter.pc);
                }
                
                targetingEffect.HasSelectedTarget = true;
            }
            else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_FRONT_MINION)
            {
                if (targetingEffect is EnemySpell)
                {
                    if (GetTargetableMinionCount() == 0)
                    {
                        if (PlayerCharacter.pc.IsTargetable())
                        {
                            targetingEffect.TargetList.Add(PlayerCharacter.pc);
                            Debug.LogWarning("Targeted player");
                        }
                        else Debug.LogWarning("Player not targeted :(");
                    }
                    else
                    {
                        foreach (Minion minion in GetMinionsOnField())
                        {
                            if (minion.IsTargetable())
                            {
                                targetingEffect.TargetList.Add(minion);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    targetingEffect.TargetList.Add(GetMinionInFront());
                }

                targetingEffect.HasSelectedTarget = true;
            }
            else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_BEHIND_MINION)
            {
                if (targetingEffect is EnemySpell)
                {
                    if (GetMinionCount() > 0) targetingEffect.TargetList.Add(GetMinionBehind());
                    else targetingEffect.TargetList.Add(PlayerCharacter.pc);
                }
                else
                {
                    targetingEffect.TargetList.Add(GetMinionBehind());
                }

                targetingEffect.HasSelectedTarget = true;
            }
            else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_RANDOM_ENEMY)
            {
                if (targetingEffect is EnemySpell) targetingEffect.TargetList.Add(GetRandomEnemy());
                else targetingEffect.TargetList.Add(GetRandomTargettableEnemy());
                targetingEffect.HasSelectedTarget = true;
            }
            else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_BEHIND_THIS)
            {
                if (targetingEffect is PlayerSpell playerSpell && playerSpell.Minion != null)
                {
                    targetingEffect.TargetList.Add(GetMinionExactlyBehindOf(playerSpell.Minion));
                }
                targetingEffect.HasSelectedTarget = true;
            }
            else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_FRONT_OF_THIS)
            {
                if (targetingEffect is PlayerSpell s && s.Minion != null) targetingEffect.TargetList.Add(GetMinionExactlyInFrontOf(s.Minion));
                targetingEffect.HasSelectedTarget = true;
            }
            else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ALL_UNIT_SELECT)
            {
                //for enemoes:
                if (GetMinionCount() > 0) targetingEffect.TargetList.AddRange(GetMinionsOnField());
                else targetingEffect.TargetList.Add(PlayerCharacter.pc);
                targetingEffect.TargetList.AddRange(GetEnemiesOnField());

                targetingEffect.HasSelectedTarget = true;
            }
            else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_BEHIND_MINIONS_ONLY)
            {
                targetingEffect.TargetList.AddRange(GetMinionsOnField());
                targetingEffect.TargetList.Remove(GetMinionInFront());
                targetingEffect.HasSelectedTarget = true;
            }
            else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ALL_ZERO_DEF_MINION)
            {
                targetingEffect.TargetList.AddRange(GetMinionsOnFieldWithZeroDef());
                targetingEffect.HasSelectedTarget = true;
            }

            if (!targetingEffect.HasSelectedTarget)
            {
                if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_MINION_SELECT)
                {
                    RemoveAllMinionColliders(false);
                }
                else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_OTHER_MINION)
                {
                    RemoveAllMinionColliders(false);
                    PlayerSpell playerSpell = targetingEffect as PlayerSpell;
                    playerSpell.Minion.collider.enabled = false;
                }
                else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ENEMY_SELECT)
                {
                    RemoveAllMinionColliders(true);
                }
                else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_ELITE_ENEMY_SELECT)
                {
                    foreach (Enemy enemy in GetEnemiesOnField()) enemy.SetSelectableTarget(enemy.enemyData._Type == EnemyData.TYPE_ELITE);
                }
                else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_NON_BOSS_ENEMY_SELECT)
                {
                    foreach (Enemy enemy in GetEnemiesOnField()) enemy.SetSelectableTarget(enemy.enemyData._Type != EnemyData.TYPE_BOSS);
                }
                else if (targetingEffect._TargetSelectionType == Spell.TARGET_SELECTION_TYPE_SINGLE_DIGIT_ATK_MINION)
                {
                    List<Minion> minions = new List<Minion>();
                    foreach (Minion minion in GetMinionsOnField()) if (minion.Attack <= 9) minions.Add(minion);
                    InitializeMinionsForTargeting(minions);
                }

                this.targetingEffect = targetingEffect;
                IsSelectingEffectTarget = true;
                cardActionsPanel.Hide(true);
                ChangeBattleState(BattleState.SelectingEffectTarget);
            }
        }
        else
        {
            targetingEffect.HasSelectedTarget = true;
        }
    }

    public void CastTargetingEffect(IEffectTargeting targetingEffect)
    {
        if (targetingEffect is IEffectHasEndPlanAction hepa)
        {
            hepa.PlayEndPlanAction();
        }
        else if (targetingEffect is IEffectHasEndTurnAction heta)
        {
            heta.PlayEndTurnAction();
        }
        else if (targetingEffect is PlayerSpell)
        {
            PlayerSpell playerSpell = targetingEffect as PlayerSpell;
            if (playerSpell.Card != null) playerSpell.Card.CastSpell(playerSpell);
            else if (playerSpell.Minion != null) playerSpell.Minion.CastSpell(playerSpell);
        }
        else if (targetingEffect is ItemData)
        {
            ItemData itemData = targetingEffect as ItemData;
            itemData.Item.Use();
        }
        else if (targetingEffect is EnemySpell)
        {
            EnemySpell enemySpell = targetingEffect as EnemySpell;
            enemySpell.Enemy.CastSpell(enemySpell);
        }

        if (battleState != BattleState.CardDragging) UnlockPlayerActions();
    }

    void OnClickCancelGetSpellTarget()
    {
        if (battleState == BattleState.SelectingEffectTarget)
        {

            UiHandler.HideButton("Cancel");
            //ChangeBattleState(BattleState.NormalTurn);
            EnableCards(true);
            if (targetingEffect is PlayerSpell)
            {
                PlayerSpell playerSpell = targetingEffect as PlayerSpell;
                playerSpell.Card?.Deselect();
            }

            IsSelectingEffectTarget = false;
            targetingEffect = null;
            canChooseTarget = false;

            UnlockPlayerActions();
        }
    }

    void GetEnemiesSkillToCast()
    {
        foreach (Enemy enemy in GetEnemiesOnField())
        {
            enemy.GetSpellToCast();
        }
    }
    
    public void AddSpellcastSpellInList(Spell spell)
    {
        if (spell._Type == Spell.TYPE_SPELLCAST)
        {
            spellcastSpells.Add(spell);
        }
    }

    public void RemoveSpellcastSpellInList(Spell spell)
    {
        spellcastSpells.Remove(spell);
    }

    void ClearSpellcastSpellList()
    {
        spellcastSpells.Clear();
    }

    public void TriggerSpellcastSpells(Spell triggeringSpell)
    {
        foreach (Spell spell in spellcastSpells)
        {
            if (spell._Type == Spell.TYPE_SPELLCAST)
            {
                if (triggeringSpell != spell)
                {
                    if (spell is IEffectTargeting)
                    {
                        PlayActionAtFront(new ActionGetEffectTarget(null, spell as IEffectTargeting));
                    }
                    else
                    {
                        spell.Cast();
                    }
                }
            }
        }
    }

    public void DisableOncePerTurnSpell(PlayerSpell spell)
    {
        if (spell.oncePerTurnEffect) oncePerTurnSpells.Add(spell.GetType());
    }

    public bool CanCastOncePerTurnSpell(PlayerSpell spell)
    {
        if (spell.oncePerTurnEffect)
        {
            foreach (System.Type _spell in oncePerTurnSpells) if (_spell == spell.GetType()) return false;
        }

        return true;
    }

    public bool IsActionQueueActive()
    {
        if (currentAction != null || !AreActionQueuesEmpty())
        {
            return true;
        }
        
        return false;
    }

    public void GetNextUnitToPlay()
    {
        if (!IsInNormalTurn)
        {
            enemyTemp = null;
            minionTemp = null;

            //Get enemy who will play first
            enemyTemp = GetNextEnemyToPlayFirst();
            if (enemyTemp != null)
            {
                currentBattleActionPlayer = enemyTemp;
                StartCoroutine(PlayNextUnitsTurn(playUnitTurnDelay, enemyTemp));
                return;
            }

            minionTemp = GetNextMinionToPlay();
            if (minionTemp != null)
            {
                currentBattleActionPlayer = minionTemp;
                StartCoroutine(PlayNextUnitsTurn(playUnitTurnDelay, minionTemp));
                return;
            }

            enemyTemp = GetNextEnemyToPlay();
            if (enemyTemp != null)
            {
                currentBattleActionPlayer = enemyTemp;
                StartCoroutine(PlayNextUnitsTurn(playUnitTurnDelay, enemyTemp));
                return;
            }

            //then get the slow units to play next:
            minionTemp = GetNextMinionToPlayLast();
            if (minionTemp != null)
            {
                currentBattleActionPlayer = minionTemp;
                StartCoroutine(PlayNextUnitsTurn(playUnitTurnDelay, minionTemp));
                return;
            }

            //end battle:
            ChangeBattleState(BattleState.EndingBattle);
            OnEndTurn?.Invoke();
            PlayEndTurnEffects();
        } 
    }

    IEnumerator PlayNextUnitsTurn(float delay, BattleEntity unitToPlay)
    {
        Minion minion = unitToPlay as Minion;
        Enemy enemy = unitToPlay as Enemy;

        yield return new WaitForSeconds(delay);

        minion?.PlayBattleAction();
        enemy?.PlayBattleAction();
    }

    Enemy GetNextEnemyToPlay()
    {
        foreach (Enemy enemy in GetEnemiesOnField())
        {
            if (enemy.CanPlayTurn())
            {
                return enemy;
            }
        }
        return null;
    }

    Enemy GetNextEnemyToPlayFirst()
    {
        foreach (Enemy enemy in GetEnemiesOnField())
        {
            if (enemy.CanPlayFirst() && enemy.CanPlayTurn())
            {
                return enemy;
            }
        }

        return null;
    }

    Minion GetNextMinionToPlay()
    {
        for (int i = summoningTiles.Count - 1; i >= 0; i--)
        {
            Minion minion = summoningTiles[i].Minion;
            if (minion != null)
            {
                if (minion.CanPlayTurn() && !minion.isSlow)
                {
                    return summoningTiles[i].Minion;
                }
            }
        }

        return null;
    }

    Minion GetNextMinionToPlayLast()
    {
        for (int i = summoningTiles.Count - 1; i >= 0; i--)
        {
            Minion minion = summoningTiles[i].Minion;

            if (minion != null)
            {
                if (minion.CanPlayTurn() && minion.isSlow)
                {
                    return minion;
                }
            }
        }

        return null;
    }

    public bool HasPrioritizedEnemyAttackTarget()
    {
        foreach (Enemy enemy in GetEnemiesOnField())
        {
            if (enemy.isPrioritizedForAttack && enemy.IsTargetable()) return true;
        }

        return false;
    }

    public bool HasPrioritizedMinionAttackTarget()
    {
        foreach (Minion minion in GetMinionsOnField())
        {
            if (minion.isPrioritizedForAttack && minion.IsTargetable()) return true;
        }

        return false;
    }

    public Enemy GetPrioritizedEnemyAttackTarget()
    {
        List<Enemy> prioritizedEnemies = new List<Enemy>();

        foreach (Enemy enemy in GetEnemiesOnField())
        {
            if (enemy.isPrioritizedForAttack && enemy.IsTargetable())
            {
                prioritizedEnemies.Add(enemy);
            }
        }

        if (prioritizedEnemies.Count == 1)
        {
            return prioritizedEnemies[0];
        }
        else if (prioritizedEnemies.Count > 1)
        {
            return prioritizedEnemies[Random.Range(0, prioritizedEnemies.Count)];
        }
        else
        {
            return null;
        }
    }

    public BattleEntity GetPrioritizedMinionAttackTarget()
    {
        List<BattleEntity> prioritizedMinions = new List<BattleEntity>();

        foreach (Minion minion in GetMinionsOnField())
        {
            if (minion.isPrioritizedForAttack && minion.IsTargetable())
            {
                prioritizedMinions.Add(minion);
            }
        }

        //if (PlayerCharacter.pc.isPrioritizedForAttack && PlayerCharacter.pc.IsTargetable())
        //{
        //    prioritizedMinions.Add(PlayerCharacter.pc);
        //}

        if (prioritizedMinions.Count == 1)
        {
            return prioritizedMinions[0];
        }
        else if (prioritizedMinions.Count > 1)
        {
            return prioritizedMinions[Random.Range(0, prioritizedMinions.Count)];
        }
        else
        {
            return null;
        }
    }

    public List<BattleEntity> GetPrioritizedMinionAttackTargets()
    {
        List<BattleEntity> prioritizedMinions = new List<BattleEntity>();

        foreach (Minion minion in GetMinionsOnField())
        {
            if (minion.isPrioritizedForAttack && minion.IsTargetable())
            {
                prioritizedMinions.Add(minion);
            }
        }

        return prioritizedMinions;
    }

    void PlayStartTurnEffects()
    {
        foreach (IEffectHasStartTurnAction startTurnEffect in GetStartTurnEffects())
        {
            startTurnEffect.PlayStartTurnAction();
        }

        foreach (Minion minion in GetMinionsOnField())
        {
            foreach (Spell spell in minion.CurrentCardData.Spells)
            {
                if (spell is IEffectHasStartTurnAction startTurnAction && startTurnAction.CanPlayStartTurnAction)
                {
                    startTurnAction.PlayStartTurnAction();
                }
            }
        }
    }

    List<IEffectHasStartTurnAction> GetStartTurnEffects()
    {
        List<IEffectHasStartTurnAction> startTurnEffects = new List<IEffectHasStartTurnAction>();

        foreach (Minion minion in GetMinionsOnField())
        {
            foreach (StatusEffect statusEffect in minion.StatusEffects.Values)
            {
                if (statusEffect is IEffectHasStartTurnAction)
                {
                    startTurnEffects.Add(statusEffect as IEffectHasStartTurnAction);
                }
            }
        }

        foreach (Enemy enemy in GetEnemiesOnField())
        {
            foreach (StatusEffect statusEffect in enemy.StatusEffects.Values)
            {
                if (statusEffect is IEffectHasStartTurnAction)
                {
                    startTurnEffects.Add(statusEffect as IEffectHasStartTurnAction);
                }
            }
        }

        return startTurnEffects;
    }

    public void PlayEndTurnEffects()
    {
        //check for units if it has status effects with actions && has not triggered its effects
        //if that unit has finished playing status effect actions
        List<IEffectHasEndTurnAction> endTurnEffects = GetEndTurnEffects();

        if (endTurnEffects.Count > 0)
        {
            foreach (IEffectHasEndTurnAction endTurnEffect in endTurnEffects)
            {
                if (endTurnEffect.CanPlayEndTurnAction)
                {
                    if (endTurnEffect is IEffectTargeting t)
                    {
                        BattleEntity source = null;
                        if (t is PlayerSpell p) source = p.Minion != null ? p.Minion as BattleEntity : PlayerCharacter.pc as BattleEntity;
                        PlayLowPriorityAction(new ActionGetEffectTarget(source, t));
                    }
                    else endTurnEffect.PlayEndTurnAction();
                }
            }

            if (!AreActionQueuesEmpty())
            {
                isPlayingEndTurnEffects = true;
                return;
            }
        }

        ReduceStatusDurations();
        EndTurnBattle();
    }

    void PlayEndPlanEffects()
    {
        foreach (Item item in GetPlayerItems())
        {
            if (item.ItemData is IEffectHasEndPlanAction)
            {
                IEffectHasEndPlanAction effectHasEndPlanAction = item.ItemData as IEffectHasEndPlanAction;
                if (effectHasEndPlanAction.CanPlayEndPlanAction)
                {
                    if (effectHasEndPlanAction is IEffectTargeting t)
                    {
                        BattleEntity source = null;
                        if (t is PlayerSpell p) source = p.Minion != null ? p.Minion as BattleEntity : PlayerCharacter.pc as BattleEntity;
                        PlayLowPriorityAction(new ActionGetEffectTarget(source, t));
                    }
                    else effectHasEndPlanAction.PlayEndPlanAction();
                    isPlayingEndPlanEffects = true;
                }
            }
        }
    }

    void ReduceStatusDurations()
    {
        void ReduceStatusEffectDuration(List<StatusEffect> statusEffects)
        {
            for (int i = statusEffects.Count - 1; i >= 0; i--)
            {
                if (statusEffects[i].HasSetDuration) statusEffects[i].ReduceDuration();
            }
        }

        void ReduceTraitDuration(List<Trait> traits)
        {
            for (int i = traits.Count - 1; i >= 0; i--)
            {
                if (traits[i].HasSetDuration) traits[i].ReduceDuration();
            }
        }

        List<Trait> traitList = new List<Trait>();
        List<StatusEffect> statusEffectList = new List<StatusEffect>();

        foreach (Minion minion in GetMinionsOnField())
        {
            traitList.Clear();
            traitList.AddRange(minion.Traits.Values);
            ReduceTraitDuration(traitList);

            statusEffectList.Clear();
            statusEffectList.AddRange(minion.StatusEffects.Values);
            ReduceStatusEffectDuration(statusEffectList);
        }

        foreach (Enemy enemy in GetEnemiesOnField())
        {
            traitList.Clear();
            traitList.AddRange(enemy.Traits.Values);
            ReduceTraitDuration(traitList);

            statusEffectList.Clear();
            statusEffectList.AddRange(enemy.StatusEffects.Values);
            ReduceStatusEffectDuration(statusEffectList);
        }

        traitList.Clear();
        traitList.AddRange(PlayerCharacter.pc.Traits.Values);
        statusEffectList.Clear();
        statusEffectList.AddRange(PlayerCharacter.pc.StatusEffects.Values);
        ReduceStatusEffectDuration(statusEffectList);
        ReduceTraitDuration(traitList);
    }

    List<IEffectHasEndTurnAction> GetEndTurnEffects()
    {
        List<IEffectHasEndTurnAction> endTurnEffects = new List<IEffectHasEndTurnAction>();

        foreach (Item item in GetPlayerItems())
        {
            if (item.ItemData is IEffectHasEndTurnAction)
            {
                endTurnEffects.Add(item.ItemData as IEffectHasEndTurnAction);
            }
        }

        foreach (Minion minion in GetMinionsOnField())
        {
            foreach (Trait trait in minion.Traits.Values)
            {
                if (trait is IEffectHasEndTurnAction) endTurnEffects.Add(trait as IEffectHasEndTurnAction);
            }

            foreach (StatusEffect statusEffect in minion.StatusEffects.Values)
            {
                if (statusEffect is IEffectHasEndTurnAction)
                {
                    endTurnEffects.Add(statusEffect as IEffectHasEndTurnAction);
                }
            }

            foreach (Spell spell in minion.CurrentCardData.Spells)
            {
                if (spell is IEffectHasEndTurnAction endTurnEffect && endTurnEffect.CanPlayEndTurnAction) endTurnEffects.Add(endTurnEffect);
            }
        }

        foreach (Enemy enemy in GetEnemiesOnField())
        {
            foreach (Trait trait in enemy.Traits.Values)
            {
                if (trait is IEffectHasEndTurnAction) endTurnEffects.Add(trait as IEffectHasEndTurnAction);
            }

            foreach (StatusEffect statusEffect in enemy.StatusEffects.Values)
            {
                if (statusEffect is IEffectHasEndTurnAction)
                {
                    endTurnEffects.Add(statusEffect as IEffectHasEndTurnAction);
                }
            }
        }

        //add for player

        return endTurnEffects;
    }

    public void SpendMinionsTurn()
    {
        foreach (Minion minion in GetMinionsOnField())
        {
            if (!minion.IsSlain) minion.SpendTurn();
        }
    }

    public int GetAvailableEnemyTileCount()
    {
        int availableTileCount = enemyTiles.childCount;

        for (int tileIndex = enemyTiles.childCount - 1; tileIndex >= 0; tileIndex--)
        {
            Transform enemyTile = enemyTiles.GetChild(tileIndex);

            foreach (Transform enemyT in enemyTile)
            {
                if (enemyT.gameObject.activeSelf && enemyT.tag == StringManager.TAG_ENEMY)
                {
                    availableTileCount--;
                    break;
                }
            }
        }

        return availableTileCount;
    }

    public void SpawnEnemy(EnemyData enemyData, Enemy.State state)
    {
        EnemyData enemyDataCopy = enemyData.Duplicate();

        for (int tileIndex = enemyTiles.childCount - 1; tileIndex >= 0; tileIndex--)
        {
            Transform enemyTile = enemyTiles.GetChild(tileIndex);

            bool canSpawnOnTile = true;

            foreach (Transform enemyT in enemyTile)
            {
                if (enemyT.gameObject.activeSelf && enemyT.tag == StringManager.TAG_ENEMY)
                {
                    canSpawnOnTile = false;
                    break;
                }
            }

            if (canSpawnOnTile)
            {
                Enemy enemy = Instantiate(enemyDataCopy.Prefab, enemyTile).GetComponent<Enemy>();
                enemy.Initialize(state, enemyDataCopy);
                SortEnemiesPosition(EnemySortPivot.Right);
                return;
            }
        }

        Debug.LogError("All enemy tiles are occupied. Cannot summon " + enemyDataCopy.Name);
    }

    public Enemy SpawnEnemy(EnemyData enemyData, Enemy.State state, Transform tile)
    {
        EnemyData enemyDataCopy = enemyData.Duplicate();
        Enemy enemy = Instantiate(enemyDataCopy.Prefab, tile).GetComponent<Enemy>();
        enemy.Initialize(state, enemyDataCopy);
        SortEnemiesPosition(EnemySortPivot.Right);
        return enemy;
    }

    List<Enemy> SpawnEnemies(List<EnemyData> enemyList)
    {
        List<Enemy> enemies = new List<Enemy>();
        int tileIndex = enemyTiles.childCount - 1;

        for (int i = enemyList.Count - 1; i >= 0; i--)
        {
            enemies.Add(SpawnEnemy(enemyList[i], Enemy.State.UsedForBattle, enemyTiles.GetChild(tileIndex)));
            tileIndex--;
        }

        return enemies;
    }

    public void DespawnEnemies()
    {
        foreach (Transform enemyTileT in enemyTiles)
        {
            foreach (Transform enemyT in enemyTileT)
            {
                if (enemyT.tag == StringManager.TAG_ENEMY)
                {
                    Destroy(enemyT.gameObject);
                    break;
                }
            }
        }
    }

    public void SortEnemiesPosition(EnemySortPivot pivot)
    {
        float enemySpritesTotalWidth = 0f;
        List<Enemy> enemies = GetEnemiesOnField();

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i].willDespawn)
            {
                enemies.RemoveAt(i);
                continue;
            }
                
            else
            {
                enemySpritesTotalWidth += enemies[i].spriteRendererMain.bounds.size.x;
            }
        }

        //If total width exceeded enemy tiles' width
        if (enemySpritesTotalWidth > ENEMY_TILES_MAX_X - ENEMY_TILES_MIN_X)
        {
            List<float> xPosList = new List<float>();
            float currentMinX = ENEMY_TILES_MAX_X;
            float currEnemyTilesMin = 0f;

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                float spriteBoundsCenterX = enemies[i].spriteRendererMain.bounds.center.x;
                float spriteBoundsWidth = enemies[i].spriteRendererMain.bounds.size.x;
                float spriteBoundsMaxX = enemies[i].spriteRendererMain.bounds.max.x;

                float spritePivotPosX = currentMinX - (spriteBoundsMaxX - spriteBoundsCenterX);
                currentMinX -= spriteBoundsWidth;

                xPosList.Insert(0, spritePivotPosX);

                if (i == 0) currEnemyTilesMin = currentMinX - (spriteBoundsCenterX - enemies[i].spriteRendererMain.bounds.min.x);
            }

            float enemyTilesWidth = ENEMY_TILES_MAX_X - ENEMY_TILES_MIN_X;
            float currTilesWidth = ENEMY_TILES_MAX_X - currEnemyTilesMin;
            float ratio = (enemyTilesWidth / currTilesWidth);

            for (int i = 0; i < xPosList.Count; i++)
            {
                xPosList[i] = ENEMY_TILES_MIN_X + (ratio * (xPosList[i] - currEnemyTilesMin));
            }

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Vector3 currTilePos = enemies[i].transform.parent.position;
                enemies[i].transform.parent.position = new Vector3(xPosList[i], currTilePos.y, currTilePos.z);
            }
        }
        else
        {
            if (pivot == EnemySortPivot.Right)
            {
                float currentMinX = ENEMY_TILES_MAX_X;

                for (int i = enemies.Count - 1; i >= 0; i--)
                {
                    Vector3 currTilePos = enemies[i].transform.parent.position;
                    enemies[i].transform.parent.position = new Vector3(currentMinX, currTilePos.y, currTilePos.z);

                    float spriteBoundsCenterX = enemies[i].spriteRendererMain.bounds.center.x;
                    float spriteBoundsWidth = enemies[i].spriteRendererMain.bounds.size.x;
                    float spriteBoundsMaxX = enemies[i].spriteRendererMain.bounds.max.x;

                    float spritePivotPosX = currentMinX - (spriteBoundsMaxX - spriteBoundsCenterX);
                    currentMinX -= spriteBoundsWidth;

                    currTilePos = enemies[i].transform.parent.position;
                    enemies[i].transform.parent.position = new Vector3(spritePivotPosX, currTilePos.y, currTilePos.z);
                }
            }
        }
    }

    void PrepareEnemiesForNextTurn()
    {
        GetEnemiesSkillToCast();
    }

    void SetItemSlotsCount(int count)
    {
        foreach (Transform transform in itemDisplay)
        {
            Destroy(transform.gameObject);
            transform.gameObject.SetActive(false);
        }

        for (int i = 0; i < count; i++)
        {
            Instantiate(ResourcesManager.PrefabItemSlot, itemDisplay);
        }
    }

    public int GetEmptyItemSlotCount()
    {
        int emptyItemSlotCount = 0;

        foreach (Transform transform in itemDisplay)
        {
            if (transform.gameObject.activeSelf && transform.childCount == 0) emptyItemSlotCount++;
        }

        return emptyItemSlotCount;
    }

    public void AddItemSlot(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            Instantiate(ResourcesManager.PrefabItemSlot, itemDisplay);
        }

        PlayerData.data.ItemSlotsCount += amount;
    }

    public void RemoveItemSlot(int amount)
    {
        if (GetEmptyItemSlotCount() >= amount)
        {
            for (int i = itemDisplay.childCount - 1; i >= 0; i--)
            {
                Transform itemSlot = itemDisplay.GetChild(i);
                if (itemSlot.childCount == 0) Destroy(itemSlot.gameObject);
            }
            PlayerData.data.ItemSlotsCount -= amount;
        }
        else
        {
            Debug.Log("Empty item slot count is lower than the amount of item slot to remove. Current empty item slot/s count: " + GetEmptyItemSlotCount() + " Amount of item slots to remove: " + amount);
        }
    }

    public bool CanAcquireItem(ItemData itemData)
    {
        if (HasAvailableItemSlot())
        {
            return true;
        }
        else
        {
            if (itemData is ItemDataConsumable itemConsumable) //for items that can be used even with full inventory
            {
                if (itemConsumable.IsUsedImmediately && itemConsumable.CanBeUsed()) return true;
            }

            return false;
        }
    }

    public void StartApplyingCardEffect(CardData.Effect cardEffect, List<CardData> applicableCards) //use CanApplyCardEffect for the card data list
    {
        if (applicableCards.Count > 0)
        {
            UiHandler.ShowCardSelectScreen(applicableCards, 1, 1, (List<Card> cards) => {
                cards[0].OriginalCardData.ApplyEffect(cardEffect);
            });
        }
    }

    public bool CanApplyCardEffect(CardData.Effect cardEffect, out List<CardData> applicableCards)
    {
        applicableCards = cardEffect.GetApplicableCards();
        return applicableCards.Count > 0;
    }

    public void AddCardToDeck(CardData cardData)
    {
        PlayerData.data.PlayerDeck.Add(cardData);
        foreach (CardDataTrait trait in cardData.traits) trait.Apply(cardData);
    }

    public void AddCardToDeck(List<CardData> cdl)
    {
        foreach (CardData cd in cdl)
        {
            PlayerData.data.PlayerDeck.Add(cd);
            foreach (CardDataTrait trait in cd.traits) trait.Apply(cd);
        }
    }

    public void AcquireItem(ItemData itemData)
    {
        if (CanAcquireItem(itemData))
        {
            if (itemData is ItemDataConsumable consumableItem)
            {
                if (consumableItem.IsUsedImmediately && consumableItem.CanBeUsed())
                {
                    consumableItem.Use();
                    return;
                }
            }

            Transform itemSlot = GetAvailableItemSlot();

            if (itemSlot != null)
            {
                Item item = Instantiate(ResourcesManager.PrefabItem, itemSlot).GetComponent<Item>();
                item.Initialize(itemData, Item.State.Active);
                PlayerData.data.PlayerItems.Add(itemData);
                OnAfterAcquiringItem?.Invoke();
                OnAfterAcquiringItemGetItem?.Invoke(item);
            }
        }
        else
        {
            //show message that item inventory is full
            Debug.Log("no slot available");
        }
    }

    public void RemoveItem(Item item)
    {
        if (item.CanBeRemoved)
        {
            item.Remove();
            PlayerData.data.PlayerItems.Remove(item.ItemData);
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
            RearrangeItems();
            OnAfterRemovingItem?.Invoke();
        }
    }

    public void RemoveItem(ItemData itemData)
    {
        foreach (Item item in GetPlayerItems())
        {
            if (item.ItemData == itemData)
            {
                RemoveItem(item);
                return;
            }
        }

        Debug.LogError("Couldn't find item.");
    }

    public void RandomlyRemoveItem()
    {
        List<Item> items = GetPlayerItems();

        if (items.Count > 0)
        {
            int randomIndex = Random.Range(0, items.Count);
            RemoveItem(items[randomIndex]);
        }
    }

    public List<Item> GetPlayerItems()
    {
        List<Item> items = new List<Item>();

        foreach (Transform itemSlot in itemDisplay)
        {
            if (itemSlot.gameObject.activeSelf)
            {
                foreach (Transform itemT in itemSlot)
                {
                    if (itemT.gameObject.activeSelf)
                    {
                        Item item = itemT.GetComponent<Item>();
                        if (item != null)
                        {
                            items.Add(item);
                            break;
                        }
                    }
                }
            }         
        }

        return items;
    }

    public List<int> GetPlayerItemsID()
    {
        List<int> itemsIDList = new List<int>();

        foreach (Item item in GetPlayerItems())
        {
            itemsIDList.Add(item.ItemData.ID);
        }

        return itemsIDList;
    }

    public bool HasAvailableItemSlot()
    {
        foreach (Transform itemSlot in itemDisplay)
        {
            if (itemSlot.gameObject.activeSelf && itemSlot.childCount == 0)
            {
                return true;
            }
        }

        return false;
    }

    public int GetAvailableItemSlotCount()
    {
        int count = 0;

        foreach (Transform itemSlot in itemDisplay)
        {
            if (itemSlot.gameObject.activeSelf && itemSlot.childCount == 0)
            {
                count++;
            }
        }

        return count;
    }

    Transform GetAvailableItemSlot()
    {
        Transform availableSlot = null;

        foreach (Transform itemSlot in itemDisplay)
        {
            if (itemSlot.gameObject.activeSelf && itemSlot.childCount == 0)
            {
                availableSlot = itemSlot;
                break;
            }
        }

        return availableSlot;
    }

    public void RearrangeItems()
    {
        for (int i = 0; i < itemDisplay.childCount; i++)
        {
            foreach (Transform itemT in itemDisplay.GetChild(i))
            {
                if (itemT.tag == StringManager.TAG_ITEM && itemT.gameObject.activeSelf)
                {
                    break;
                }
                else
                {
                    itemDisplay.GetChild(i).SetAsLastSibling();
                }
            }
        }
    }

    public void TransformCard(CardData cardData)
    {
        if (PlayerData.data.PlayerDeck.Contains(cardData))
        {
            CardData newCardData = DataLibrary.CardCollection.GetItemByBaseRarity(1, new List<DataLibrary.Class> { DataLibrary.class001, PlayerData.data.Class }, cardData.Rarity)[0];
            for (int i = PlayerData.data.PlayerDeck.Count - 1; i >= 0; i--)
            {
                if (PlayerData.data.PlayerDeck[i] == cardData)
                {
                    PlayerData.data.PlayerDeck[i] = newCardData;
                    break;
                }
            }

            Debug.Log("Success");
        }
    }

    public void StartTransformingCard()
    {
        UiHandler.ShowCardSelectScreen(PlayerData.data.PlayerDeck, 1, 1, (List<Card> cards) => { TransformCard(cards[0].OriginalCardData); });
    }

    public void UpgradeCard(CardData cardData)
    {
        if (cardData.IsUpgradeable())
        {
            cardData.UpgradeInfo.SetLevel(cardData.UpgradeInfo.Level + 1, 1);
        }
    }

    public bool CanUpgradeCard()
    {
        foreach (CardData cardData in PlayerData.data.PlayerDeck)
        {
            if (cardData.IsUpgradeable()) return true;
        }

        return false;
    }

    public bool CanUpgradeItem()
    {
        foreach (Item item in GetPlayerItems())
        {
            if (item.ItemData.CanUpgrade()) return true;
        }

        return false;
    }

    public bool CanRemoveMinionCardFromDeck()
    {
        foreach (CardData cardData in PlayerData.data.PlayerDeck)
        {
            if (cardData is MinionCardData && cardData.CanBeRemovedFromDeck) return true;
        }

        return false;
    }

    public bool CanRemoveSpellCardFromDeck()
    {
        foreach (CardData cardData in PlayerData.data.PlayerDeck)
        {
            if (cardData is SpellCardData && cardData.CanBeRemovedFromDeck) return true;
        }

        return false;
    }

    public bool CanRemoveCardFromDeck()
    {
        if (PlayerCharacter.pc.CanRemoveCard)
        {
            foreach (CardData cardData in PlayerData.data.PlayerDeck) if (cardData.CanBeRemovedFromDeck) return true;
        }

        return false;
    }

    public bool CanRemoveItem()
    {
        if (GetPlayerItems().Count > 0)
        {
            return true;
        }

        return false;
    }

    public void RandomlyRemoveMinionCardFromDeck()
    {
        if (CanRemoveMinionCardFromDeck())
        {
            List<CardData> minionCardData = new List<CardData>();
            foreach (CardData cardData in PlayerData.data.PlayerDeck)
            {
                if (cardData is MinionCardData && cardData.CanBeRemovedFromDeck) minionCardData.Add(cardData);
            }

            int randomIndex = Random.Range(0, minionCardData.Count);
            RemoveCardFromDeck(minionCardData[randomIndex]);
        }
    }

    public void RandomlyRemoveSpellCardFromDeck()
    {
        if (CanRemoveSpellCardFromDeck())
        {
            List<CardData> spellCardData = new List<CardData>();
            foreach (CardData cardData in PlayerData.data.PlayerDeck)
            {
                if (cardData is SpellCardData && cardData.CanBeRemovedFromDeck) spellCardData.Add(cardData);
            }

            int randomIndex = Random.Range(0, spellCardData.Count);
            RemoveCardFromDeck(spellCardData[randomIndex]);
        }
    }

    public void RandomlyRemoveCardFromDeck()
    {
        if (CanRemoveCardFromDeck())
        {
            List<CardData> removableCards = new List<CardData>();

            foreach (CardData cardData in PlayerData.data.PlayerDeck)
            {
                if (cardData.CanBeRemovedFromDeck) removableCards.Add(cardData);
            }

            int randomIndex = Random.Range(0, removableCards.Count);

            RemoveCardFromDeck(removableCards[randomIndex]);
        }
    }

    public void RemoveCardFromDeck(CardData cardData)
    {
        PlayerData.data.PlayerDeck.Remove(cardData);
    }

    public List<CardData> GetRemovableCardDataList()
    {
        List<CardData> removableCards = new List<CardData>();
        foreach (CardData cardData in PlayerData.data.PlayerDeck) if (cardData.CanBeRemovedFromDeck) removableCards.Add(cardData);
        return removableCards;
    }

    public List<ItemData> GetRemovableItemDataList()
    {
        List<ItemData> removableItemDataList = new List<ItemData>();

        foreach (Item item in GetPlayerItems())
        {
            removableItemDataList.Add(item.ItemData);
        }

        return removableItemDataList;
    }

    public int GetRemovableCardInDeckCount()
    {
        return PlayerData.data.PlayerDeck.Count;
    }

    public int GetRemovableItemCount()
    {
        return GetPlayerItems().Count;
    }

    public void DuplicateCard(List<Card> cards)
    {
        foreach (Card card in cards)
        {
            AddCardToDeck(card.OriginalCardData.Duplicate());
        }
    }

    public List<ItemData> GetMerchantItemDataList()
    {
        List<ItemData> itemDataList = new List<ItemData>();

        List<DataLibrary.Class> classes = new List<DataLibrary.Class>();
        classes.Add(DataLibrary.class001);
        classes.Add(PlayerData.data.Class);

        //add consumables:
        itemDataList.AddRange(DataLibrary.ConsumableItemCollection.GetItemByBaseRarity(DataLibrary.MERCHANT_CONSUMABLE_ITEM_COUNT, classes, DataLibrary.rarity001));

        //add passive items:
        List<int> currentPassiveItemsID = new List<int>();
        foreach (Item item in GetPlayerItems())
        {
            if (item.ItemData is ItemDataPassive) currentPassiveItemsID.Add(item.ItemData.ID);
        }
        itemDataList.AddRange(DataLibrary.ItemCollection.GetUniqueItemByBaseRarity(DataLibrary.MERCHANT_PASSIVE_ITEM_COUNT, classes, DataLibrary.rarity001, currentPassiveItemsID));

        //add heal items:
        itemDataList.Add(DataLibrary.healItem);

        //add upgrade item:
        itemDataList.Add(DataLibrary.upgradeItem);

        //add remove items:
        itemDataList.Add(DataLibrary.removeItem);

        //Duplicate itemData:
        for (int i = 0; i < itemDataList.Count; i++)
        {
            int itemID = itemDataList[i].ID;
            itemDataList[i] = DataLibrary.GetItemData(itemID);
        }

        return itemDataList;
    }

    public List<CardData> GetMerchantCardDataList()
    {
        List<CardData> cardDataList = new List<CardData>();
        List<DataLibrary.Class> cardClasses = new List<DataLibrary.Class>();

        //add 2 common class cards:
        cardDataList.AddRange(DataLibrary.CardCollection.GetItemByBaseRarity(2, DataLibrary.class001, DataLibrary.rarity001));

        //add 6 current class cards:
        //cardDataList.AddRange(DataLibrary.CardCollection.GetItemByBaseRarity(6, PlayerData.data.Class, DataLibrary.rarity002));
        cardClasses.Add(PlayerData.data.Class);
        int q = StageMap.CurrentStage - 1;
        if (q > 2) q = 2;
        Dictionary<DataLibrary.Rarity, float> rarityChances = DataLibrary.merchantCardRewardRarityChancesList[q];
        cardDataList.AddRange(DataLibrary.CardCollection.GetItemByCustomChances(6, rarityChances, cardClasses));

        //Duplicate cardData:
        for (int i = 0; i < cardDataList.Count; i++)
        {
            int cardDataID = cardDataList[i].ID;
            cardDataList[i] = DataLibrary.GetCardData(cardDataID);
        }

        return cardDataList;
    }

    void ResetBattleTurnCount()
    {
        BattleTurnCount = 0;
    }

    void IncreaseBattleTurnCount()
    {
        BattleTurnCount++;

        if (isBattleTurnLimited)
        {
            if (BattleTurnCount > currentBattleTurnLimit)
            {
                EndTurnLimitedBattle();
                return;
            }
        }

        if (alternateTurns)
        {
            if (BattleTurnCount % 2 == 1)
            {
                playerAttacksFirst = true;
            }
            else
            {
                playerAttacksFirst = false;
            }
        }
        else
        {
            playerAttacksFirst = true;
        }
    }

    public static bool IsPlayersInitiativeTurn()
    {
        if (playerAttacksFirst)
        {
            return true;
        }

        return false;
    }

    //////////////////////////////////////////////////////////////////////////////////////

    void SetGameFPS(int amount)
    {
        Application.targetFrameRate = amount;
    }

    public bool DoesGraphicRaycasterHit(string objTag)
    {
        PointerEventData.position = Input.mousePosition;
        raycastResults.Clear();
        CanvasBattleGR.Raycast(PointerEventData, raycastResults);

        foreach (RaycastResult r in raycastResults) if (r.gameObject.transform.tag == objTag) return true;
        return false;
    }

    public string GetClickHitTag()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        if (hit.collider != null)
        {
            return hit.collider.transform.tag;
        }

        return "";
    }

    public RaycastHit2D GetRaycastHit2D()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        return hit;
    }

    public void ChangeNumberText(int newAmount, TextMeshProUGUI textToChange)
    {
        float currentValue = System.Convert.ToInt32(textToChange.text);
        int roundedCurrentValue;

        if (Mathf.Abs(currentValue - newAmount) > 3)
        {
            IEnumerator ChangeText()
            {
                while (true)
                {
                    currentValue = Mathf.Lerp(currentValue, newAmount, numberTextChangeTime * Time.deltaTime);
                    roundedCurrentValue = Mathf.RoundToInt(currentValue);
                    textToChange.SetText(roundedCurrentValue.ToString());
                    if (roundedCurrentValue == newAmount)
                    {
                        break;
                    }

                    yield return new WaitForEndOfFrame();
                }
            }

            StartCoroutine(ChangeText());
        }
        else
        {
            textToChange.SetText(newAmount.ToString());
        }
    }

    public List<Minion> GetMinionsOnField()
    {
        List<Minion> minions = new List<Minion>();

        for (int i = summoningTiles.Count - 1; i >= 0; i--)
        {
            Minion minion = summoningTiles[i].Minion;

            if (minion != null)
            {
                if (!minion.IsSlain)
                {
                    minions.Add(minion);
                }
            }
        }

        return minions;
    }

    public List<Minion> GetMinionsOnFieldWithZeroDef()
    {
        List<Minion> minions = GetMinionsOnField();

        for (int i = minions.Count - 1; i >= 0; i--)
        {
            if (minions[i].Defense != 0) minions.RemoveAt(i);
        }

        return minions;
    }

    public List<Minion> GetMinionsOnFieldEvenIfSlain()
    {
        List<Minion> minions = new List<Minion>();

        for (int i = summoningTiles.Count - 1; i >= 0; i--)
        {
            foreach (Transform minionT in summoningTiles[i].transform)
            {
                Minion minion = minionT.GetComponent<Minion>();
                if (minion != null) minions.Add(minion);
            }
        }

        return minions;
    }

    public Minion GetMinionBehindOf(Minion selectedMinion)
    {
        Minion minionBehind = null;

        int currentIndex = selectedMinion.transform.parent.GetSiblingIndex();
        if (currentIndex != 0)
        {
            currentIndex -= 1;
            while (currentIndex >= 0)
            {
                SummoningTile behindTile = summoningTiles[currentIndex];

                Minion minion = behindTile.Minion;
                if (minion != null)
                {
                    if (!minion.IsSlain)
                    {
                        minionBehind = minion;
                        break;
                    }
                }

                currentIndex--;
            }
        }

        return minionBehind;
    }

    public Minion GetMinionExactlyBehindOf(Minion selectedMinion)
    {
        Minion minionBehind = null;

        int currentIndex = selectedMinion.transform.parent.GetSiblingIndex();
        if (currentIndex != 0)
        {
            currentIndex -= 1;

            SummoningTile behindTile = summoningTiles[currentIndex];

            Minion minion = behindTile.Minion;
            if (minion != null)
            {
                if (!minion.IsSlain)
                {
                    minionBehind = minion;
                }
            }
        }

        return minionBehind;
    }

    public Minion GetMinionExactlyInFrontOf(Minion selectedMinion)
    {
        Minion front = null;

        int currentIndex = selectedMinion.transform.parent.GetSiblingIndex();
        if (currentIndex != summoningTiles.Count - 1)
        {
            currentIndex += 1;

            SummoningTile frontTl = summoningTiles[currentIndex];

            Minion minion = frontTl.Minion;
            if (minion != null && !minion.IsSlain)
            {
                front = minion;
            }
        }

        return front;
    }

    public Enemy GetEnemyBehindOf(Enemy enemySelected)
    {
        Enemy enemyBehind = null;

        int currentIndex = enemySelected.transform.parent.GetSiblingIndex();
        int maxIndex = enemyTiles.childCount - 1;
        if (currentIndex != maxIndex)
        {
            currentIndex++;

            while (currentIndex <= maxIndex)
            {
                Transform behindTile = enemyTiles.GetChild(currentIndex);

                foreach (Transform tileChild in behindTile)
                {
                    Enemy enemy = tileChild.GetComponent<Enemy>();
                    if (enemy != null)
                    {
                        if (!enemy.IsSlain)
                        {
                            enemyBehind = enemy;
                            break;
                        }
                    }
                }

                if (enemyBehind != null)
                {
                    break;
                }

                currentIndex++;
            }
        }

        return enemyBehind;
    }

    public List<Enemy> GetEnemiesOnField()
    {
        List<Enemy> enemies = new List<Enemy>();
        foreach (Transform tile in enemyTiles)
        {
            foreach (Transform enemyTransform in tile)
            {
                Enemy enemy = enemyTransform.GetComponent<Enemy>();
                if (enemy != null)
                {
                    if (!enemy.IsSlain) enemies.Add(enemy);
                }
            }
        }

        return enemies;
    }

    public List<Enemy> GetEnemiesOnField(int id)
    {
        List<Enemy> enemies = new List<Enemy>();
        foreach (Transform tile in enemyTiles)
        {
            foreach (Transform enemyTransform in tile)
            {
                Enemy enemy = enemyTransform.GetComponent<Enemy>();
                if (enemy != null)
                {
                    if (!enemy.IsSlain && enemy.enemyData.ID == id) enemies.Add(enemy);
                }
            }
        }

        return enemies;
    }

    public Enemy GetEnemy(int tileIndex)
    {
        foreach (Transform enemyT in enemyTiles.GetChild(tileIndex))
        {
            if (enemyT.tag == StringManager.TAG_ENEMY)
            {
                return enemyT.GetComponent<Enemy>();
            }
        }

        return null;
    }

    public Enemy GetRandomTargettableEnemy()
    {
        List<Enemy> enemies = GetTargetableEnemies();
        return enemies[Random.Range(0, enemies.Count)];
    }

    public Enemy GetRandomEnemy()
    {
        List<Enemy> enemies = GetEnemiesOnField();
        return enemies[Random.Range(0, enemies.Count)];
    }

    public int GetEnemyCount()
    {
        return GetEnemiesOnField().Count;
    }

    public int GetEnemyCount(int id)
    {
        int count = 0;

        foreach (Enemy enemy in GetEnemiesOnField())
        {
            if (enemy.enemyData.ID == id) count++;
        }

        return count;
    }

    public int GetTargetableEnemyCount()
    {
        int targetableEnemyCount = 0;

        foreach (Enemy enemy in GetEnemiesOnField())
        {
            if (enemy.IsTargetable()) targetableEnemyCount++;
        }

        return targetableEnemyCount;
    }

    public Minion GetMinionInFront()
    {
        for (int i = summoningTiles.Count - 1; i >= 0; i--)
        {
            SummoningTile tile = summoningTiles[i];

            Minion minion = tile.Minion;
            if (minion != null)
            {
                if (!minion.IsSlain) return minion;
            }
        }

        return null;
    }

    public Minion GetMinionBehind()
    {
        for (int i = 0; i < summoningTiles.Count; i++)
        {
            SummoningTile tile = summoningTiles[i];

            Minion minion = tile.Minion;
            if (minion != null)
            {
                if (!minion.IsSlain) return minion;
            }
        }

        return null;
    }

    public Enemy GetEnemyInFront()
    {
        for (int i = 0; i < enemyTiles.childCount; i++)
        {
            foreach (Transform enemyTransform in enemyTiles.GetChild(i))
            {
                Enemy enemy = enemyTransform.GetComponent<Enemy>();
                if (enemy != null)
                {
                    if (!enemy.IsSlain)
                    {
                        return enemy;
                    }
                }
            }
        }

        return null;
    }

    public Enemy GetEnemyInBack()
    {
        for (int i = enemyTiles.childCount - 1; i >= 0; i--)
        {
            foreach (Transform enemyT in enemyTiles.GetChild(i))
            {
                if (enemyT.tag == StringManager.TAG_ENEMY)
                {
                    return enemyT.GetComponent<Enemy>();
                }
            }
        }

        return null;
    }

    public Enemy GetEnemyBehind()
    {
        for (int i = enemyTiles.childCount - 1; i >= 0; i--)
        {
            foreach (Transform enemyTransform in enemyTiles.GetChild(i))
            {
                Enemy enemy = enemyTransform.GetComponent<Enemy>();
                if (enemy != null)
                {
                    if (!enemy.IsSlain)
                    {
                        return enemy;
                    }
                }
            }
        }

        return null;
    }

    public List<BattleEntity> GetAllUnitsOnField()
    {
        List<BattleEntity> units = new List<BattleEntity>();
        units.AddRange(GetMinionsOnField());
        units.AddRange(GetEnemiesOnField());

        return units;
    }

    public List<Card> GetPlayableCardsInHand()
    {
        List<Card> cardList = new List<Card>();

        foreach (Transform cardTransform in handZone)
        {
            Card card = cardTransform.GetComponent<Card>();
            if (card != null && !card.willRemove)
            {
                cardList.Add(card);
            }
        }

        foreach (Transform cardTransform in handZoneFront)
        {
            Card card = cardTransform.GetComponent<Card>();
            if (card != null && !card.willRemove)
            {
                cardList.Add(card);
            }
        }

        //arrange
        bool swapped = true;
        while (swapped)
        {
            swapped = false;
            for (int i = 0; i < cardList.Count - 1; i++)
            {
                if (cardList[i].handZoneIndex > cardList[i + 1].handZoneIndex)
                {
                    Card temp = cardList[i];
                    cardList[i] = cardList[i + 1];
                    cardList[i + 1] = temp;
                    swapped = true;
                }
            }
        }

        return cardList;
    }

    public List<Card> GetCardsInHand()
    {
        List<Card> cardList = new List<Card>();

        foreach (Transform cardTransform in handZone)
        {
            Card card = cardTransform.GetComponent<Card>();
            if (card != null)
            {
                cardList.Add(card);
            }
        }

        foreach (Transform cardTransform in handZoneFront)
        {
            Card card = cardTransform.GetComponent<Card>();
            if (card != null)
            {
                cardList.Add(card);
            }
        }

        //arrange
        bool swapped = true;
        while (swapped)
        {
            swapped = false;
            for (int i = 0; i < cardList.Count - 1; i++)
            {
                if (cardList[i].handZoneIndex > cardList[i + 1].handZoneIndex)
                {
                    Card temp = cardList[i];
                    cardList[i] = cardList[i + 1];
                    cardList[i + 1] = temp;
                    swapped = true;
                }
            }
        }

        return cardList;
    }

    public int GetPlayableCardsInHandCount()
    {
        int count = 0;

        foreach (Card card in GetPlayableCardsInHand())
        {
            if (!card.willRemove) count++;
        }

        return count;
    }

    Card GetDraggedCardInHand()
    {
        foreach (Card card in GetPlayableCardsInHand())
        {
            if (card.draggable.isBeingDragged) return card;
        }

        return null;
    }

    public int GetMinionCardCountInHand()
    {
        int minionCardCount = 0;

        foreach (Card card in GetPlayableCardsInHand())
        {
            if (card.CurrentCardData is MinionCardData) minionCardCount++;
        }

        return minionCardCount;
    }

    public int GetMinionCardCountInPlayerDeck()
    {
        int minionCardCount = 0;

        foreach (CardData cardData in PlayerData.data.PlayerDeck)
        {
            if (cardData is MinionCardData) minionCardCount++;
        }

        return minionCardCount;
    }

    public List<CardData> GetMinionCardDataListFromPlayerDeck()
    {
        List<CardData> minionCardDataList = new List<CardData>();

        foreach (CardData cardData in PlayerData.data.PlayerDeck)
        {
            if (cardData is MinionCardData) minionCardDataList.Add(cardData);
        }

        return minionCardDataList;
    }

    public List<Enemy> GetTargetableEnemies()
    {
        List<Enemy> targetableEnemies = new List<Enemy>();

        foreach (Enemy enemy in GetEnemiesOnField())
        {
            if (enemy.IsTargetable()) targetableEnemies.Add(enemy);
        }

        return targetableEnemies;
    }

    public List<Minion> GetTargetableMinions()
    {
        List<Minion> targetableMinions = new List<Minion>();

        foreach (Minion minion in GetMinionsOnField())
        {
            if (minion.IsTargetable()) targetableMinions.Add(minion);
        }

        return targetableMinions;
    }

    public int GetTargetableMinionCount()
    {
        int targetableMinionCount = 0;

        foreach (Minion minion in GetMinionsOnField())
        {
            if (minion.IsTargetable()) targetableMinionCount++;
        }

        return targetableMinionCount;
    }

    public void CheckCardsSelectability()
    {
        foreach (Card card in GetPlayableCardsInHand())
        {
            card.CheckSelectability();
        }
    }

    public void EnableCards(bool willDo)
    {
        AreCardsDisabled = !willDo;
        CheckCardsSelectability();  
    }

    public void IncreaseSacrificeCount()
    {
        SacrificeCount += 1;
    }

    public void SetSlainSpellPlayCount(int count)
    {
        if (count > 1)
        {
            slainSpellPlayCounts.Add(count);
        }
    }

    public void RemoveSlainSpellPlayCount(int count)
    {
        for (int i = 0; i < slainSpellPlayCounts.Count; i++)
        {
            if (slainSpellPlayCounts[i] == count)
            {
                slainSpellPlayCounts.RemoveAt(i);
                return;
            }
        }
    }

    public int GetSlainSpellPlayCount()
    {
        int playCount = 1;

        foreach (int count in slainSpellPlayCounts)
        {
            if (count > playCount)
            {
                playCount = count;
            }
        }

        return playCount;
    }

    public void UpdateCardsText()
    {
        foreach (Card card in GetPlayableCardsInHand())
        {
            card.UpdateCardText(card.CurrentCardData);
        }
    }

    void GainUltPoint()
    {
        CurrentUltPoints++;
        if (CurrentUltPoints > PlayerData.data.MaxUltPoints)
        {
            CurrentUltPoints = PlayerData.data.MaxUltPoints;
        }

        UiHandler.UpdateUltPointIconValues(CurrentUltPoints, PlayerData.data.MaxUltPoints);
    }

    void ResetUltPointCount()
    {
        CurrentUltPoints = 0;
        UiHandler.UpdateUltPointIconValues(CurrentUltPoints, PlayerData.data.MaxUltPoints);
    }

    public void ReduceUltPoint(int amount)
    {
        CurrentUltPoints -= amount;
        if (CurrentUltPoints < 0)
        {
            CurrentUltPoints = 0;
        }

        UiHandler.UpdateUltPointIconValues(CurrentUltPoints, PlayerData.data.MaxUltPoints);
    }

    void SetSummonTilesState(SummoningTile.BattleState state)
    {
        foreach (SummoningTile tile in summoningTiles) tile.SetState(state);
    }

    void SetPlayableSummonTilesState(SummoningTile.BattleState state)
    {
        foreach (SummoningTile tile in summoningTiles) if (tile.CanBeSummonedOn()) tile.SetState(state);
    }

    void ShowSummoningTiles() //called at start of battle
    {
        int playableTileCount = Mathf.Clamp(PlayerCharacter.pc.SummoningTilesCount, 1, summoningTiles.Count);

        for (int i = 0; i < summoningTiles.Count; i++)
        {
            if (i < playableTileCount) summoningTiles[i].SetState(SummoningTile.BattleState.Idle); //and play animation
        }
    }

    void HideSummoningTiles() //called at end of  battle
    {
        foreach (SummoningTile tile in summoningTiles)
        {
            tile.SetState(SummoningTile.BattleState.Disabled);
        }
    }

    //public void OpenMap()
    //{
    //    if (!StageMap.gameObject.activeSelf)
    //    {
    //        StageMap.gameObject.SetActive(true);
    //        if (StageMap.CanTravel)
    //        {
    //            UiHandler.HideStandbyButtons();
    //        }
    //    }
    //}

    //public void CloseMap()
    //{
    //    if (StageMap.gameObject.activeSelf)
    //    {
    //        StageMap.gameObject.SetActive(false);
    //        if (StageMap.CanTravel)
    //        {
    //            UiHandler.ShowStandbyButtons();
    //        }
    //    }
    //}

    public static int GetRandomInteger()
    {
        return Random.Range(int.MinValue, int.MaxValue);
    }

    public static bool GetTrueBoolByChance(float chance)
    {
        if (chance > 100f)
        {
            chance = 100f;
        }

        float randomNumber = Random.Range(0, 100f);

        if (randomNumber < chance)
        {
            return true;
        }

        return false;
    }

    public static T GetItemFromListByChance<T>(List<T> list, List<float> percentChances)
    {
        if (list.Count == percentChances.Count)
        {
            float totalPercentage = 0;

            foreach (float percentChance in percentChances)
            {
                totalPercentage += percentChance;
            }

            float percentRatio = 100f / totalPercentage;
            float[] newPercentChances = new float[percentChances.Count];

            for (int i = 0; i < newPercentChances.Length; i++)
            {
                if (newPercentChances.Length == 1)
                {
                    newPercentChances[i] = 100f;
                }
                else if (i == 0)
                {
                    newPercentChances[i] = (float)System.Math.Round(percentChances[i] * percentRatio, 2);
                }
                else if (i > 0 && i < newPercentChances.Length - 1)
                {
                    newPercentChances[i] = (float)System.Math.Round((percentChances[i] * percentRatio) + newPercentChances[i - 1], 2);
                }
                else if (i == newPercentChances.Length - 1)
                {
                    newPercentChances[i] = 100f;
                }
            }

            float randomNumber = (float)System.Math.Round(Random.Range(0, 100f), 2);
            int selectedIndex = -1;

            for (int i = 0; i < newPercentChances.Length; i++)
            {
                if (i == 0)
                {
                    if (randomNumber >= 0 && randomNumber <= newPercentChances[i])
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                else if (i > 0 && i < newPercentChances.Length - 1)
                {
                    if (randomNumber > newPercentChances[i - 1] && randomNumber <= newPercentChances[i])
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                else if (i == newPercentChances.Length - 1)
                {
                    if (randomNumber > newPercentChances[i - 1] && randomNumber <= 100f)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            return list[selectedIndex];
        }
        return default;
    }

    public static T GetItemFromListByChance<T>(List<T> list, float[] percentChances)
    {
        if (list.Count == percentChances.Length)
        {
            float totalPercentage = 0;

            foreach (float percentChance in percentChances)
            {
                totalPercentage += percentChance;
            }

            float percentRatio = 100f / totalPercentage;
            float[] newPercentChances = new float[percentChances.Length];

            for (int i = 0; i < newPercentChances.Length; i++)
            {
                if (newPercentChances.Length == 1)
                {
                    newPercentChances[i] = 100f;
                }
                else if (i == 0)
                {
                    newPercentChances[i] = (float)System.Math.Round(percentChances[i] * percentRatio, 2);
                }
                else if (i > 0 && i < newPercentChances.Length - 1)
                {
                    newPercentChances[i] = (float)System.Math.Round((percentChances[i] * percentRatio) + newPercentChances[i - 1], 2);
                }
                else if (i == newPercentChances.Length - 1)
                {
                    newPercentChances[i] = 100f;
                }
            }

            float randomNumber = (float)System.Math.Round(Random.Range(0, 100f), 2);
            int selectedIndex = -1;

            for (int i = 0; i < newPercentChances.Length; i++)
            {
                if (i == 0)
                {
                    if (randomNumber >= 0 && randomNumber <= newPercentChances[i])
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                else if (i > 0 && i < newPercentChances.Length - 1)
                {
                    if (randomNumber > newPercentChances[i - 1] && randomNumber <= newPercentChances[i])
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                else if (i == newPercentChances.Length - 1)
                {
                    if (randomNumber > newPercentChances[i - 1] && randomNumber <= 100f)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            return list[selectedIndex];
        }
        return default;
    }

    public static T[] RemoveArrayElement<T>(T[] array, int index)
    {
        T[] newArray = new T[array.Length - 1];
        int currIndex = 0;

        for (int i = 0; i < newArray.Length; i++)
        {
            if (index == currIndex)
            {
                currIndex++;
            }

            newArray[i] = array[currIndex];
            currIndex++;
        }

        return newArray;
    }

    public static void ChangeAnimation(AnimationClip animationClip, string overridedAnimName, Animator animator)
    {
        
        if (animator.runtimeAnimatorController is AnimatorOverrideController)
        {
            AnimatorOverrideController AOC = null;

            if (overridedAnimName.Contains(StringManager.SUFFIX_CLONE)) AOC = animator.runtimeAnimatorController as AnimatorOverrideController;
            else
            {
                AOC = Instantiate(animator.runtimeAnimatorController as AnimatorOverrideController);
                animator.runtimeAnimatorController = AOC;
            }

            List<KeyValuePair<AnimationClip, AnimationClip>> animOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            AOC.GetOverrides(animOverrides);

            for (int i = 0; i < animOverrides.Count; i++)
            {
                if (animOverrides[i].Key.name == overridedAnimName)
                {
                    KeyValuePair<AnimationClip, AnimationClip> animOverride = new KeyValuePair<AnimationClip, AnimationClip>(animOverrides[i].Key, animationClip);
                    animOverrides[i] = animOverride;
                    AOC.ApplyOverrides(animOverrides);
                    
                    return;
                }
            }
        }
    }

    public static float GetAnimationLength(Animator animator, string animName) //name of the animation clip or overrided anim clip
    {
        AnimatorOverrideController AOC = animator.runtimeAnimatorController as AnimatorOverrideController;
        if (AOC != null)
        {
            List<KeyValuePair<AnimationClip, AnimationClip>> animOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            AOC.GetOverrides(animOverrides);

            foreach (KeyValuePair<AnimationClip, AnimationClip> animOverride in animOverrides)
            {
                if (animOverride.Key.name == animName)
                {
                    if (animOverride.Value != null)
                    {
                        return animOverride.Value.length;
                    }
                    else
                    {
                        return animOverride.Key.length;
                    }
                }
            }
        }
        else
        {
            foreach (AnimationClip animationClip in animator.runtimeAnimatorController.animationClips)
            {
                if (animationClip.name == animName) return animationClip.length;
            }
        }

        return 0.5f;
    }

    public static float[] GetDistributedXPositionsInRect(RectTransform rectTransform, int childCount, float preferredColumnWidth)
    {
        float[] xPositions = new float[childCount];

        float totalColWidth = childCount * preferredColumnWidth;

        if (childCount % 2 == 1 && totalColWidth < rectTransform.rect.width)
        {
            float startPoint = -(((childCount - 1) / 2) * preferredColumnWidth);
            for (int i = 0; i < xPositions.Length; i++)
            {
                xPositions[i] = startPoint + (i * preferredColumnWidth);
            }
        }
        else if (childCount % 2 == 0 && totalColWidth < rectTransform.rect.width)
        {
            float startPoint = -(preferredColumnWidth / 2) + -(((childCount / 2) - 1) * preferredColumnWidth);
            for (int i = 0; i < xPositions.Length; i++)
            {
                xPositions[i] = startPoint + (i * preferredColumnWidth);
            }
        }
        else
        {
            float newCardTileWidth = rectTransform.rect.width / childCount;
            float startPoint = -(rectTransform.rect.width / 2) + (newCardTileWidth / 2);            
            for (int i = 0; i < xPositions.Length; i++)
            {
                xPositions[i] = startPoint + (i * newCardTileWidth);
            }
        }

        return xPositions;
    }
}

public class SummonInfo
{
    public enum CardSelectionLocation { None, Deck, DiscardPile, DiscardVanishPile, VanishPile, Hand, HandZeroDef };
    public enum Type { OutOfDeck, FromHand, ByCard, FromDiscardPile, FromDeck, FromDiscardVanishPile, FromVanishPile };

    public const Type TYPE_OUT_OF_DECK = Type.OutOfDeck;
    public const Type TYPE_FROM_HAND = Type.FromHand;
    public const Type TYPE_BY_CARD = Type.ByCard; //by playing a card and paying cost
    public const Type TYPE_FROM_DISCARD_PILE = Type.FromDiscardPile;
    public const Type TYPE_FROM_DECK = Type.FromDeck;
    public const Type TYPE_FROM_DISCARD_VANISH_PILE = Type.FromDiscardVanishPile;
    public const Type TYPE_FROM_VANISH_PILE = Type.FromVanishPile;

    public BattleEntity src;
    public Card srcCard;
    public CardData currCardData;
    public CardData origCardData;
    public Type type;
    public bool canBeCancelled;
    public bool isForced;
    public SummoningTile targetSummTile;
    public bool willNotDespawnAtCurrTurn;
}
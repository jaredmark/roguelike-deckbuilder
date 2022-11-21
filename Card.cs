using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Card : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler
{
    const float DURATION_USE_EFFECT = 0.3f;

    public enum State { None, UsedForBattle, UsedForSelection, UsedForDisplay, DeckEditorDeckCard, DeckEditorCardCollCard, UsedForRewardScreen, UsedForShopScreen, Upgrading }
    public CardData CurrentCardData, OriginalCardData;

    float posX, posY, posZ, rotZ;

    //For tweaking card positions in hand:
    float cardTileWidth = 190f;
    float moveSpeed = 8f;
    float leftMostRotation = 15f;
    float rotationSpeed = 10f;
    float posYHandZoneFront = 200f;
    float distanceAwayFromToppedCard = 250f;
    public static float enlargedSizeMultiplier = 1.3f;
    float scaleChangeTime = 18f;
    float cardSelectedMoveSpeed = 12f;
    Color currentColor, targetColor;
    [SerializeField]
    float colorChangeTime = 10f;
    [SerializeField]
    Vector3 cardForSelectionSize;

    public int Cost
    {
        get
        {
            int _cost = cost;
            foreach (IEffectModifiesCardCost f in costModifs)
            {
                if (f.IsCostZero) return 0;
                _cost += f.CostModifier;
            }
            foreach (IEffectModifiesCardStats f in statModifs) _cost += f.CostModifier;
            if (_cost < 0) _cost = 0;
            return _cost;
        }
        private set
        {
            cost = value;
        }
    }
    int cost;

    //Don't edit these:
    State state;
    bool isPointed;
    static bool hasSetStaticValues;
    bool hasCalledStart = false;
    public int handZoneIndex = -1;
    public bool handZoneIndexSet = false;
    int prevParentChildCount = -1;
    public float distanceFromOldPosX;
    bool isHovered = false;
    public bool hasClickedMouseButtonUp = false;
    bool hasClickedMouseDown;
    bool willEnlarge = false;
    public static float defaultWidth, defaultHeight;
    public static Vector3 enlargedSize, defaultSize;
    public bool isSelected = false;
    public bool canChooseCardActions = false;
    bool hasChangedBattleState = false;
    bool isPosFrozen = false;
    bool isScaleFrozen;
    public bool isSelectedInBattle = false;
    Spell selectedSpell;
    public bool IsSelectable { get; private set; }
    bool willDimCard = false;
    bool willUpdateImageColors = false;
    bool isSilenced = false;
    bool canBeHovered = false;
    public bool willRemove;
    bool hasCalledAwake;
    bool cantPointerEnter;

    //for card traits:
    public bool isTemporary;
    public bool willKeepInHand;
    readonly Dictionary<int, CardTrait> cardTraits = new Dictionary<int, CardTrait>();

    Image artworkImage;
    Image frameImage;
    Image descBackgroundImage;

    TextMeshProUGUI textAttack;
    TextMeshProUGUI textDefense;
    TextMeshProUGUI textName;
    TextMeshProUGUI textCost;
    TextMeshProUGUI textDescription;

    Transform cardGlow;
    Transform attackIcon;
    Transform defenseIcon;

    public RectTransform handZone, handZoneFront, rectTransform;
    GameManager gameManager;
    ObjectPooler objectPooler;
    public Draggable draggable { get; private set; }
    CardActionsPanel cardActionsPanel;
    Transform summoningTiles;
    UIHandler uiHandler;
    DevSetting devSetting;
    ToolTipViewer tooltipViewer;
    RawImage rawImage;
    CanvasGroup canvasGroup;

    readonly List<Image> imageComponents = new List<Image>();
    readonly List<TextMeshProUGUI> textComponents = new List<TextMeshProUGUI>();
    readonly Dictionary<Image, Material> defaultImageMaterials = new Dictionary<Image, Material>();
    readonly Dictionary<TextMeshProUGUI, Material> defaultTextMaterials = new Dictionary<TextMeshProUGUI, Material>();
    Material glowMaterial;

    //for tool tip viewer:
    readonly List<ToolTipInfo> tooltipInfos = new List<ToolTipInfo>();
    readonly Dictionary<int, StatusEffect> statusEffectDictionary = new Dictionary<int, StatusEffect>();
    readonly Dictionary<int, Trait> traitDictionary = new Dictionary<int, Trait>();

    public GameManager.VoidEvent OnDiscardByEffect;
    public GameManager.VoidEvent OnPurchase;
    public GameManager.VoidEvent OnUpdateCost;
    public GameManager.VoidEvent OnAfterCastSpell;

    Coroutine corGlowEffect;
    Coroutine corFadeEffect;

    readonly List<IEffectModifiesCardCost> costModifs = new List<IEffectModifiesCardCost>();
    readonly List<IEffectVanishesCard> cardVanishers = new List<IEffectVanishesCard>();
    readonly List<IEffectModifiesCardStats> statModifs = new List<IEffectModifiesCardStats>();

    public bool IsVanishing
    {
        get
        {
            if (isTemporary) return true;
            foreach (IEffectVanishesCard f in cardVanishers) if (f.WillVanishCard) return true;
            return false;
        }
    }

    void Awake()
    {
        if (!hasCalledAwake)
        {
            draggable = GetComponent<Draggable>();
            rectTransform = GetComponent<RectTransform>();
            tooltipViewer = GetComponent<ToolTipViewer>();
            rawImage = GetComponent<RawImage>();
            canvasGroup = GetComponent<CanvasGroup>();

            cardGlow = transform.Find("Glow").transform;

            textName = transform.Find("Name").GetComponent<TextMeshProUGUI>();
            textDescription = transform.Find("Description").GetComponent<TextMeshProUGUI>();
            textCost = transform.Find("CostIcon").Find("Cost").GetComponent<TextMeshProUGUI>();

            if (transform.name.Substring(0, StringManager.OBJECT_POOL_TAG_MINION_CARD.Length) == StringManager.OBJECT_POOL_TAG_MINION_CARD)
            {
                attackIcon = transform.Find("AttackIcon");
                defenseIcon = transform.Find("DefenseIcon");
                textAttack = attackIcon.Find("Text").GetComponent<TextMeshProUGUI>();
                textDefense = defenseIcon.Find("Text").GetComponent<TextMeshProUGUI>();
            }

            if (!hasSetStaticValues)
            {
                defaultSize = transform.localScale;
                defaultWidth = rectTransform.sizeDelta.x;
                defaultHeight = rectTransform.sizeDelta.y;
                enlargedSize = new Vector3(transform.localScale.x * enlargedSizeMultiplier, transform.localScale.y * enlargedSizeMultiplier, transform.localScale.z);

                hasSetStaticValues = true;
            }

            void GetChildComponents(Transform transform)
            {
                foreach (Transform childT in transform)
                {
                    if (childT.TryGetComponent(out Image image)) if (image.transform.tag != StringManager.TAG_MASK) imageComponents.Add(image);
                    if (childT.TryGetComponent(out TextMeshProUGUI text)) textComponents.Add(text);

                    if (childT.childCount > 0) GetChildComponents(childT);
                }
            }

            GetChildComponents(transform);

            foreach (Image image in imageComponents)
            {
                if (image.gameObject.name == StringManager.CARD_ARTWORK_NAME)
                {
                    artworkImage = image;
                }
                else if (image.gameObject.name == StringManager.CARD_FRAME_NAME)
                {
                    frameImage = image;
                }

                if (transform.name.Substring(0, StringManager.OBJECT_POOL_TAG_SPELL_CARD.Length) == StringManager.OBJECT_POOL_TAG_SPELL_CARD)
                {
                    descBackgroundImage = image;
                }
            }

            hasCalledAwake = true;
        }
    }

    void Start()
    {
        if (!hasCalledStart)
        {
            hasCalledStart = true;

            gameManager = GameManager.gm;
            handZone = gameManager.handZone as RectTransform;
            handZoneFront = gameManager.handZoneFront as RectTransform;
            objectPooler = GameManager.ObjectPooler;
            devSetting = gameManager.DevSetting;
            cardActionsPanel = gameManager.cardActionsPanel;
            summoningTiles = gameManager.summoningTilesT;
            uiHandler = GameManager.UiHandler;

            glowMaterial = Instantiate(MaterialManager.MaterialCard);
            foreach (Image image in imageComponents) defaultImageMaterials.Add(image, image.material);
            foreach (TextMeshProUGUI text in textComponents) defaultTextMaterials.Add(text, text.material);
        }   
    }

    void Update()
    {
        if (hasClickedMouseDown && Input.GetMouseButtonUp(0))
        {
            if (!willRemove)
            {
                if (gameManager.IsInNormalTurn)
                {
                    gameManager.ChangeBattleState(GameManager.BattleState.NormalTurn);
                }

                PutCardToFront(false);
                hasChangedBattleState = false;
                isHovered = false;
                SetRaycastable(true);
            }

            draggable.OnPointerUp(null);
            hasClickedMouseDown = false;
            Debug.LogError("GAME IS BEING SUSSY AGANE!!!!");
        }

        if (!willRemove)
        {
            if (state == State.UsedForBattle)
            {
                if (draggable.isBeingDragged)
                {
                    if (gameManager.battleState == GameManager.BattleState.CardSelected)
                    {
                        cardActionsPanel.Close();
                        Deselect();
                    }

                    if (!hasChangedBattleState)
                    {
                        gameManager.ChangeBattleState(GameManager.BattleState.CardDragging);
                        PutCardToFront(true);
                        hasChangedBattleState = true;
                    }

                    if (draggable.isAnchoring && willEnlarge) willEnlarge = false;
                    else if (!draggable.isAnchoring && !willEnlarge) willEnlarge = true;
                }

                if (gameManager.battleState == GameManager.BattleState.CardSelected || gameManager.battleState == GameManager.BattleState.MinionSelected)
                {
                    if (isSelected)
                    {
                        if (gameManager.battleState == GameManager.BattleState.CardSelected && isSelected && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
                        {
                            canChooseCardActions = true;
                        }

                        if (canChooseCardActions)
                        {
                            if (Input.GetMouseButtonUp(0))
                            {
                                GameManager.PointerEventData.position = Input.mousePosition;
                                GameManager.raycastResults.Clear();
                                GameManager.CanvasBattleGR.Raycast(GameManager.PointerEventData, GameManager.raycastResults);

                                bool hasHitCardActionsPanel = false;

                                foreach (RaycastResult raycastResult in GameManager.raycastResults)
                                {
                                    if (raycastResult.gameObject.transform.tag == StringManager.TAG_CARD)
                                    {
                                        Card card = raycastResult.gameObject.GetComponent<Card>();

                                        if (card != this)
                                        {
                                            Deselect();
                                            ResetHandZoneCardValues();
                                            card.Select();
                                            return;
                                        }
                                    }

                                    if (raycastResult.gameObject.transform == cardActionsPanel.transform)
                                    {
                                        hasHitCardActionsPanel = true;
                                    }
                                }

                                if (!hasHitCardActionsPanel)
                                {
                                    Deselect();
                                    ResetHandZoneCardValues();
                                }
                            }
                            else if (Input.GetMouseButtonUp(1))
                            {
                                Deselect();
                                ResetHandZoneCardValues();
                            }
                        }
                    }
                }
            }

            if (IsInHand())
            {
                if (!draggable.isBeingDragged)
                {
                    if (handZoneIndexSet && !isPosFrozen)
                    {
                        SetCardXPosition();
                        SetCardYPosition();
                        SetCardZPosition();
                        SetCardZRotation();

                        if (Vector2.Distance(transform.localPosition, new Vector2(posX, posY)) > 0.3f && !isHovered)
                        {
                            transform.localPosition = Vector2.Lerp(transform.localPosition, new Vector2(posX, posY), moveSpeed * Time.deltaTime);
                        }
                        else if (Vector2.Distance(transform.localPosition, new Vector2(posX, posY)) > 0.3f && isHovered)
                        {
                            transform.localPosition = Vector2.Lerp(transform.localPosition, new Vector2(posX, posY), cardSelectedMoveSpeed * Time.deltaTime);
                        }

                        if (Mathf.Abs(posZ - transform.localPosition.z) > 0.3f)
                        {
                            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, posZ);
                        }
                    }
                }

                if (Mathf.Abs(transform.localEulerAngles.z - rotZ) > 0.3f)
                {
                    float newZRot = Mathf.LerpAngle(transform.localEulerAngles.z, rotZ, rotationSpeed * Time.deltaTime);
                    Vector3 newRot = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, newZRot);
                    transform.localEulerAngles = newRot;
                }

                UpdateImageColors();
                EnlargeSize(willEnlarge);
            }
        }

        if (isPointed)
        {
            if (Input.GetMouseButtonUp(1))
            {
                if (state == State.DeckEditorDeckCard)
                {
                    devSetting.OnRightClickDeckEditorDeckCard(this);
                    return;
                }
            }
        }
    }

    IEnumerator NextFrameUpdate()
    {
        yield return new WaitForEndOfFrame();
        if (IsSelectable && gameManager.DoesGraphicRaycasterHit(StringManager.TAG_CARD))
        {
            cantPointerEnter = true;
        }
    }

    public void DontHoverNextFrame()
    {
        StartCoroutine(NextFrameUpdate());
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (Input.GetMouseButtonDown(0))
        {
            hasClickedMouseDown = true;

            if (state == State.UsedForBattle)
            {
                if (IsSelectable)
                {
                    if ((gameManager.battleState == GameManager.BattleState.NormalTurn || gameManager.battleState == GameManager.BattleState.SummoningInBattle))
                    {
                        //gameManager.RemoveAllCardsColliders(true);
                    }
                }
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (state == State.UsedForBattle)
        {
            if (IsInHand())
            {
                bool willStartCoroutine = true;
                gameManager.RemoveAllCardsColliders(false);

                if (gameManager.battleState == GameManager.BattleState.NormalTurn || gameManager.battleState == GameManager.BattleState.MinionSelected ||
                    gameManager.battleState == GameManager.BattleState.SummoningInBattle)
                {
                    if (!draggable.isBeingDragged && IsSelectable && hasClickedMouseDown)
                    {
                        Select();
                    }
                }
                else if (gameManager.battleState == GameManager.BattleState.CardDragging)
                {
                    SetRaycastable(false);
                    bool hasPerformedAction = false;

                    if (gameManager.GetClickHitTag() == "SummoningTile")
                    {
                        SummoningTile summoningTile = gameManager.GetRaycastHit2D().collider.transform.GetComponent<SummoningTile>();
                        if (CanDragSummon() && summoningTile.CanBeSummonedOn())
                        {
                            DragSummon(summoningTile);
                            hasPerformedAction = true;
                            willStartCoroutine = false;
                        }
                        else if (gameManager.CanSummonMinion(this))
                        {
                            OnClickSummon();
                        }
                    }

                    if (!hasPerformedAction)
                    {
                        if (gameManager.IsInNormalTurn)
                        {
                            gameManager.ChangeBattleState(GameManager.BattleState.NormalTurn);
                        }

                        PutCardToFront(false);
                        hasChangedBattleState = false;
                        isHovered = false;
                    }

                    //If has not despawned or used
                    if (!willRemove) SetRaycastable(true);
                }

                if (willStartCoroutine)
                {
                    StartCoroutine(NextFrameUpdate());
                }
            }
        }
        else if (state == State.UsedForSelection && hasClickedMouseDown)
        {
            if (IsSelectable)
            {
                if (gameManager.isSelectingCardsInHand)
                {
                    gameManager.SelectCardInHand(this);
                }
                else if (gameManager.isSelectingInSelectionScreen)
                {
                    gameManager.OnClickCardInCardSelectionScreen(this);
                }
            }
        }
        else if (state == State.DeckEditorCardCollCard && hasClickedMouseDown)
        {
            devSetting.OnClickDeckEditorCardCollCard(this);
        }
        else if (state == State.DeckEditorDeckCard && hasClickedMouseDown)
        {
            devSetting.OnClickDeckEditorDeckCard(this);
        }
        else if (state == State.Upgrading && hasClickedMouseDown)
        {
            if (IsSelectable)
            {
                GameManager.UiHandler.ShowUpgradeConfirmationScreen(this);
            }
        }
        else if (state == State.UsedForRewardScreen && hasClickedMouseDown)
        {
            uiHandler.OnClickCardReward(this);
        }
        else if (state == State.UsedForShopScreen && hasClickedMouseDown)
        {
            OnPurchase?.Invoke();
        }

        hasClickedMouseDown = false;
        hasClickedMouseButtonUp = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {       
        isPointed = true;

        if (IsInHand())
        {
            if (/*!willDimCard &&*/ !draggable.isBeingDragged && !isHovered && !cantPointerEnter && !isSelected && gameManager.battleState !=
                GameManager.BattleState.CardSelected && canBeHovered)
            {
                PutCardToFront(true);
                isHovered = true;
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointed = false;
        cantPointerEnter = false;

        if (IsInHand() && !willRemove)
        {
            if (/*!Input.GetMouseButton(0) &&*/ gameManager.battleState != GameManager.BattleState.CardSelected && state == State.UsedForBattle || (state == State.UsedForSelection && gameManager.isSelectingCardsInHand))
            {
                if (!draggable.isBeingDragged && isHovered && !isSelected && rawImage.raycastTarget)
                {
                    PutCardToFront(false);
                    isHovered = false;
                }
            }
            //else if (!IsSelectable && Input.GetMouseButton(0))
            //{
            //    PutCardToFront(false);
            //    isHovered = false;
            //}

            ResetHandZoneCardValues();
        }

        //hasClickedMouseDown = false;
    }

    void OnDisable()
    {
        isPointed = false;
    }

    public void Initialize(CardData cardData, State state)
    {
        Initialize(cardData, cardData.Duplicate(), state);
    }

    public void Initialize(CardData originalCardData, CardData currentCardData, State state)
    {
        Awake();
        Start();
        this.state = State.None;

        isHovered = false;
        willEnlarge = false;
        isSelected = false;
        isPosFrozen = false;
        isScaleFrozen = false;
        isTemporary = false;
        hasChangedBattleState = false;
        canChooseCardActions = false;
        hasClickedMouseButtonUp = false;
        hasClickedMouseDown = false;
        prevParentChildCount = -1;
        isSelectedInBattle = false;
        willUpdateImageColors = false;
        willDimCard = false;
        draggable.isBeingDragged = false;
        IsSelectable = false;
        isSilenced = false;
        canBeHovered = true;
        willRemove = false;
        cantPointerEnter = false;

        OriginalCardData = originalCardData;
        CurrentCardData = currentCardData;

        SetCardIndex();
        transform.localEulerAngles = new Vector3(0, 0, 0);
        transform.localScale = defaultSize;
        rectTransform.sizeDelta = new Vector2(defaultWidth, defaultHeight);

        SetGlowActive(false);
        SetMaterialToDefault();

        InitializeCardData();
        Cost = CurrentCardData.Cost;

        if (state == State.UsedForBattle)
        {
            InitializeSpells();
        }

        SetState(state);

        SetRaycastable(true);
        InitializeCardArtwork();
        InitializeCardFrame();

        InitializeColor();
        UpdateCardText(CurrentCardData);

        ResetShaderValues();

        tooltipViewer.Initialize(() => {
            tooltipInfos.Clear();
            statusEffectDictionary.Clear();
            traitDictionary.Clear();

            foreach (Spell spell in CurrentCardData.Spells)
            {
                if (spell is IEffectHasTraits)
                {
                    IEffectHasTraits effectHasTraits = spell as IEffectHasTraits;
                    foreach (Trait trait in effectHasTraits.Traits)
                    {
                        if (!traitDictionary.ContainsKey(trait.ID)) traitDictionary.Add(trait.ID, trait);
                    }
                }
                if (spell is IEffectHasStatusEffects)
                {
                    IEffectHasStatusEffects effectHasStatusEffects = spell as IEffectHasStatusEffects;
                    foreach (StatusEffect statusEffect in effectHasStatusEffects.StatusEffects)
                    {
                        if (!statusEffectDictionary.ContainsKey(statusEffect.ID)) statusEffectDictionary.Add(statusEffect.ID, statusEffect);
                    }
                }
            }

            foreach (Trait trait in traitDictionary.Values)
            {
                tooltipInfos.Add(new ToolTipInfo(null, trait.Name, trait.GetDescription()));
            }
            foreach (StatusEffect statusEffect in statusEffectDictionary.Values)
            {
                tooltipInfos.Add(new ToolTipInfo(null, statusEffect.Name, statusEffect.GetDescription()));
            }

            return tooltipInfos;
        });
    }

    public void SetCardIndex()
    {
        handZoneIndex = -1;
        handZoneIndexSet = false;
        gameManager.SetCardsIndex();
    }

    public void SetState(State state)
    {
        if (this.state != state) //remove if buggy
        {
            this.state = state;

            if (state == State.UsedForBattle)
            {
                if (!willDimCard) currentColor = ResourcesManager.CardNormalColor;

                transform.localScale = defaultSize;
                draggable.canBeDragged = true;

                //SetCardIndex();
                //CheckSelectability();
            }
            else if (state == State.UsedForDisplay)
            {
                transform.localScale = defaultSize;
                draggable.canBeDragged = false;
                transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
                targetColor = ResourcesManager.CardNormalColor;
            }
            else if (state == State.UsedForSelection)
            {
                transform.localScale = defaultSize;
                draggable.canBeDragged = false;
                transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
                //temp;
                SetCardSelectable(true);
            }
            else if (state == State.Upgrading)
            {
                transform.localScale = defaultSize;
                draggable.canBeDragged = false;
                transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);

                InitializeUpgrading();
            }
            else if (state == State.UsedForRewardScreen)
            {
                targetColor = ResourcesManager.CardNormalColor;
                transform.localScale = defaultSize;
                draggable.canBeDragged = false;
                transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
            }
            else
            {
                targetColor = ResourcesManager.CardNormalColor;
                transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
                draggable.canBeDragged = false;
            }
        }
    }

    void ResetShaderValues()
    {
        canvasGroup.alpha = 1f;

        foreach (Image image in imageComponents)
        {
            image.material.SetInt(StringManager.MATERIAL_STRING_CARD_IS_GLOWING, 0);
            image.material.SetFloat(StringManager.MATERIAL_STRING_CARD_GLOW_INTESITY, 0f);
        }

        foreach (TextMeshProUGUI text in textComponents)
        {
            text.material.SetInt(StringManager.MATERIAL_STRING_CARD_IS_GLOWING, 0);
            text.material.SetFloat(StringManager.MATERIAL_STRING_CARD_GLOW_INTESITY, 0f);
        }
    }

    void SetMaterial(Material material)
    {
        foreach (Image image in imageComponents) image.material = material;
        foreach (TextMeshProUGUI text in textComponents) text.material = material;
    }

    void SetMaterialToDefault()
    {
        foreach (Image image in imageComponents) image.material = defaultImageMaterials[image];
        foreach (TextMeshProUGUI text in textComponents) text.material = defaultTextMaterials[text];
    }

    public void PlayGlowEffect(float fadeInDuration, float duration)
    {
        if (gameObject.activeSelf)
        {
            SetMaterial(glowMaterial);

            IEnumerator PlayGlowEffect()
            {
                foreach (Image image in imageComponents) image.material.SetInt(StringManager.MATERIAL_STRING_CARD_IS_GLOWING, 1);
                foreach (TextMeshProUGUI text in textComponents) text.material.SetInt(StringManager.MATERIAL_STRING_CARD_IS_GLOWING, 1);

                float timeElapsed = 0f;
                float currIntensity = 0f;
                float prevIntensity = 0f;

                while (true)
                {
                    timeElapsed += Time.deltaTime;
                    if (timeElapsed > duration) break;

                    prevIntensity = currIntensity;
                    if (timeElapsed < fadeInDuration)
                    {
                        currIntensity = Mathf.Clamp((float)System.Math.Round(Mathf.Sin((((timeElapsed / fadeInDuration) * 0.5f) * Mathf.PI)), 2), 0f, 1f);

                        foreach (Image image in imageComponents) image.material.SetFloat(StringManager.MATERIAL_STRING_CARD_GLOW_INTESITY, currIntensity);
                        foreach (TextMeshProUGUI text in textComponents) text.material.SetFloat(StringManager.MATERIAL_STRING_CARD_GLOW_INTESITY, currIntensity);
                    }
                    else currIntensity = 1f;

                    if (!prevIntensity.Equals(currIntensity))
                    {
                        foreach (Image image in imageComponents) image.material.SetFloat(StringManager.MATERIAL_STRING_CARD_GLOW_INTESITY, currIntensity);
                        foreach (TextMeshProUGUI text in textComponents) text.material.SetFloat(StringManager.MATERIAL_STRING_CARD_GLOW_INTESITY, currIntensity);
                    }

                    yield return null;
                }

                foreach (Image image in imageComponents) image.material.SetInt(StringManager.MATERIAL_STRING_CARD_IS_GLOWING, 0);
                foreach (TextMeshProUGUI text in textComponents) text.material.SetInt(StringManager.MATERIAL_STRING_CARD_IS_GLOWING, 0);
            }

            if (corGlowEffect != null) StopCoroutine(corGlowEffect);
            corGlowEffect = StartCoroutine(PlayGlowEffect());
        }
    }

    void SetAlpha(float value)
    {
        canvasGroup.alpha = value;
    }

    void PlayFadeOutEffect(float duration)
    {
        if (gameObject.activeSelf)
        {
            IEnumerator PlayFadeOutEffect()
            {
                float timeElapsed = 0f;
                float currAlpha = 1f;

                while (true)
                {
                    timeElapsed += Time.deltaTime;
                    if (timeElapsed > duration) break;

                    currAlpha = 1f - (float)System.Math.Round(Mathf.Sin((((timeElapsed / duration) * 0.5f) * Mathf.PI)), 2);
                    canvasGroup.alpha = currAlpha;

                    yield return null;
                }

                canvasGroup.alpha = 0f;
            }

            if (corFadeEffect != null) StopCoroutine(corFadeEffect);
            corFadeEffect = StartCoroutine(PlayFadeOutEffect());
        }
    }

    void PlayUseEffect(float duration)
    {
        if (gameObject.activeSelf)
        {
            PlayGlowEffect(duration / 2f, duration / 2f);
            IEnumerator FadeOut()
            {
                yield return new WaitForSeconds(duration * 0.33f);
                PlayFadeOutEffect(duration * 0.66f);
            }

            StartCoroutine(FadeOut());
        }
    }

    public void PutCardToFront(bool willDo)
    {
        if (willDo)
        {
            transform.SetParent(handZoneFront);
        }
        else
        {
            transform.SetParent(handZone);
            transform.SetSiblingIndex(handZoneIndex);
        }
        SetCardXPosition();
        SetCardYPosition();
        SetCardZPosition();
        SetCardZRotation();

        willEnlarge = willDo;
    }

    bool IsInHand()
    {
        if (transform.parent == handZone || transform.parent == handZoneFront)
        {
            return true;
        }

        return false;
    }

    void Select()
    {
        if (state == State.UsedForBattle)
        {
            if (IsSelectable)
            {
                if (gameManager.battleState == GameManager.BattleState.SummoningInBattle)
                {
                    isSelectedInBattle = true;
                }
                PutCardToFront(true);
                gameManager.ChangeBattleState(GameManager.BattleState.CardSelected);
                cardActionsPanel.Show(this);
                isSelected = true;
                isHovered = true;
                //draggable.canBeDragged = false;

                //Then make all other cards look dark
            }
        }
    }

    public void Deselect()
    {
        if (state == State.UsedForBattle)
        {
            PutCardToFront(false);
            if (gameManager.battleState != GameManager.BattleState.MinionSelected && !isSelectedInBattle && !gameManager.HasEndedTurn)
            {
                gameManager.ChangeBattleState(GameManager.BattleState.NormalTurn);
            }
            else if (isSelectedInBattle)
            {
                gameManager.ChangeBattleState(GameManager.BattleState.SummoningInBattle);
            }
            cardActionsPanel.Close();
            isSelected = false;
            isHovered = false;
            draggable.canBeDragged = true;
            canChooseCardActions = false;

            //foreach (Transform cardTransform in handZone)
            //{
            //    Card card = cardTransform.GetComponent<Card>();
            //    card.StartCoroutine(card.NextFrameUpdate());
            //}
            //hasClickedMouseDown = false;
            //hasClickedMouseButtonUp = false;
        }
    }

    public void Vanish()
    {
        if (!PlayerCharacter.pc.WillBlockVanishing)
        {
            //play vanish anim
            gameManager.SendCardDataToVanishedPile(OriginalCardData);

            PlayUseEffect(DURATION_USE_EFFECT);
            Despawn(DURATION_USE_EFFECT);
        }
        else
        {
            Discard();

            if (PlayerCharacter.pc.WillBlockVanishing) PlayerCharacter.pc.OnBlockVanishing();
        }
    }

    public void Discard()
    {
        foreach (Spell spell in CurrentCardData.Spells) if (spell._Type == Spell.TYPE_END_TURN_DISCARD) spell.Cast();
        gameManager.SendCardDataToDiscardPile(OriginalCardData);

        PlayUseEffect(DURATION_USE_EFFECT);
        Despawn(DURATION_USE_EFFECT);
    }

    public void DiscardByEffect()
    {
        gameManager.SendCardDataToDiscardPile(OriginalCardData);
        OnDiscardByEffect?.Invoke();

        PlayUseEffect(DURATION_USE_EFFECT);
        Despawn(DURATION_USE_EFFECT);
    }

    public void Despawn(float delay)
    {
        SetRaycastable(false);
        isScaleFrozen = true;
        isPosFrozen = true;
        willRemove = true;
        gameManager.OnRemoveCard?.Invoke();
        OnDiscardByEffect = null;
        OnUpdateCost = null;

        //remove card traits
        if (state == State.UsedForBattle)
        {
            if (cardTraits.Count != 0)
            {
                foreach (CardTrait cardTrait in cardTraits.Values)
                {
                    cardTrait.RemoveFromCard();
                }
                cardTraits.Clear();
            }

            foreach (Spell spell in CurrentCardData.Spells)
            {
                if (spell is CardPassiveSpell)
                {
                    CardPassiveSpell cardPassiveSpell = spell as CardPassiveSpell;
                    cardPassiveSpell.RemoveCardPassive();
                }
            }

            cardVanishers.Clear();
        }

        /////////////
        
        IEnumerator Despawn()
        {
            yield return new WaitForSeconds(delay);
            objectPooler.DespawnUIObject(gameObject);
        }

        if (!gameObject.activeSelf || delay.Equals(0f)) objectPooler.DespawnUIObject(gameObject);
        else StartCoroutine(Despawn());
    }

    bool CanPerformActions()
    {
        if (PlayerCharacter.pc.CanPayEnergy(Cost) && (gameManager.CanSummonMinion(this) || (CanActivateSpell() && CanCastSpell())) /*can cast ultimate*/)
        {
            return true;
        }
        return false;
    }

    bool CanDragSummon()
    {
        if (CurrentCardData is IEffectMinionSelecting)
        {
            return false;
        }

        if (gameManager.CanSummonMinion(CurrentCardData) && PlayerCharacter.pc.CanPayEnergy(Cost))
        {
            return true;
        }

        return false;
    }

    void DragSummon(SummoningTile summoningTile)
    {
        if (summoningTile != null && summoningTile.CanBeSummonedOn())
        {
            SummonInfo summonInfo = new SummonInfo();
            summonInfo.srcCard = this;
            summonInfo.currCardData = CurrentCardData;
            summonInfo.origCardData = OriginalCardData;
            summonInfo.type = SummonInfo.TYPE_BY_CARD;

            Minion summonedMinion = summoningTile.SummonMinion(summonInfo);
            gameManager.summonedMinions.Add(summonedMinion);
            gameManager.AfterSummonByCard(this, summonedMinion);
        }
    }

    public void OnClickSummon()
    {
        gameManager.StartSummoningMinionByCard(this, true);
    }

    //for rightclicking during card selected. DONT EDIT
    public void OnClickCancel()
    {
        if (gameManager.battleState == GameManager.BattleState.CardSelected)
        {
            Deselect();
            PutCardToFront(false);
            isHovered = false;
            StartCoroutine(NextFrameUpdate());
        }
    }

    public bool CanUltimateUpgrade()
    {
        if (CurrentCardData.IsUltimateUpgradeable())
        {
            if (gameManager.CurrentUltPoints >= CurrentCardData.UpgradeInfo.UltimateUpgradeCost)
            {
                return true;
            }
        }

        return false;
    }

    public void UltimateUpgrade()
    {
        if (CanUltimateUpgrade())
        {
            CurrentCardData.UpgradeInfo.UltimateUpgrade(CurrentCardData.UpgradeInfo.Level, CurrentCardData.UpgradeInfo.UpgradeOption);
            UpdateCardText(CurrentCardData);
            gameManager.ReduceUltPoint(CurrentCardData.UpgradeInfo.UltimateUpgradeCost);

            if (gameManager.battleState != GameManager.BattleState.NormalTurn)
            {
                gameManager.ChangeBattleState(GameManager.BattleState.NormalTurn);
                Deselect();
            }
        }
    }

    public bool CanActivateSpell()
    {
        //reminder: check if spells are castable
        if (CurrentCardData.Spells.Count > 1)
        {
            foreach (Spell spell in CurrentCardData.Spells)
            {
                if (spell.IsActivatable()) return true && !isSilenced;
            }
        }
        else if (CurrentCardData.Spells.Count == 1)
        {
            if (CurrentCardData.Spells[0].IsActivatable())
            {
                return true && !isSilenced;
            }
        }

        return false;
    }

    //Check if can cast atleast one of the spells
    public bool CanCastSpell()
    {
        bool CanPaySpellCost(int cost)
        {
            if (CurrentCardData.WillNotPayCost) return true;
            if (PlayerCharacter.pc.willNotPayEnergyForNextSpellCast) return true;
            if (PlayerCharacter.pc.CanPayEnergy(cost)) return true;
            if (PlayerCharacter.pc.WillSkipCostOnInsuffMana) return true;

            return false;
        }

        if (!CurrentCardData.WillNotPaySpellCost && !CanPaySpellCost(Cost)) return false;

        if (CurrentCardData.Spells.Count > 1)
        {
            foreach (Spell spell in CurrentCardData.Spells)
            {
                if (spell.CanCast()) return true;
            }
        }
        else if (CurrentCardData.Spells.Count == 1)
        {
            if (CurrentCardData.Spells[0].CanCast()) return true;
        }

        return false;
    }

    public void CastSpell()
    {
        if (CanActivateSpell() && CanCastSpell())
        {
            foreach(Spell spell in CurrentCardData.Spells) if (spell.IsActivatable()) CastSpell(spell);
        }
    }

    public void CastSpell(Spell spell)
    {
        if (spell is IEffectDiscarding)
        {
            IEffectDiscarding discardingEffect = spell as IEffectDiscarding;
            if (!discardingEffect.HasDiscarded)
            {
                GameManager.PlayHighPriorityAction(new ActionDiscard(PlayerCharacter.pc, discardingEffect, this));
                return;
            }
        }

        if (spell is IEffectMinionReturning)
        {
            IEffectMinionReturning minionReturningEffect = spell as IEffectMinionReturning;
            if (!minionReturningEffect.HasSelectedMinions)
            {
                GameManager.PlayHighPriorityAction(new ActionReturnMinion(PlayerCharacter.pc, minionReturningEffect));
                return;
            }
        }

        if (spell is IEffectTargeting)
        {
            IEffectTargeting targetingEffect = spell as IEffectTargeting;
            if (!targetingEffect.HasSelectedTarget)
            {
                GameManager.PlayHighPriorityAction(new ActionGetEffectTarget(PlayerCharacter.pc, targetingEffect));
                return;
            }
        }

        if (spell is IEffectMinionReturning)
        {
            IEffectMinionReturning minionReturningEffect = spell as IEffectMinionReturning;
            if (minionReturningEffect.ReturnType == Spell.MINION_RETURN_TYPE_TO_HAND) gameManager.ReturnMinionsToHand(minionReturningEffect.SelectedMinions);
            else if (minionReturningEffect.ReturnType == Spell.MINION_RETURN_TYPE_ON_TOP_OF_DECK) gameManager.SendMinionsOnTopOfDeck(minionReturningEffect.SelectedMinions);
        }

        if (spell is IEffectMinionSelecting ems)
        {
            if (!ems.HasSelectedMinions)
            {
                GameManager.PlayHighPriorityAction(new ActionSelectMinion(PlayerCharacter.pc, ems));
                return;
            }
        }

        spell.Cast();
        AfterCastSpell(spell);
    }

    void AfterCastSpell(Spell spell)
    {
        gameManager.OnAfterPlayingSpellCard?.Invoke();
        gameManager.TriggerSpellcastSpells(spell);
        gameManager.OnAfterCastSpellGetCardData?.Invoke(CurrentCardData, spell);

        //pay energy cost
        bool willPayCost = true;

        if (CurrentCardData.WillNotPaySpellCost || PlayerCharacter.pc.willNotPayEnergyForNextSpellCast)
        {
            willPayCost = false;
        }

        if (willPayCost && PlayerCharacter.pc.WillSkipCostOnInsuffMana) PlayerCharacter.pc.OnSkipCostOnInsuffManaGetCard(this);
        else PlayerCharacter.pc.ReduceCurrentEnergy(Cost);

        if (spell is PlayerSpell playerSpell && playerSpell.oncePerTurnEffect) gameManager.DisableOncePerTurnSpell(playerSpell);

        cardActionsPanel.Close();
        selectedSpell = null;

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

        //send this guy's OriginalCardData to discard pile
        //then set this card's parent to HandZoneVeryFront
        //play animation

        OnAfterCastSpell?.Invoke();
        gameManager.OnAfterCastingSpellFromHand?.Invoke();
        gameManager.OnAfterCastingGetSpell?.Invoke(spell);
        gameManager.OnAfterCastingSpell?.Invoke();
        gameManager.OnAfterPlayingCardGetCard?.Invoke(this);

        //then after a few delay despawn this object
        if (gameManager.battleState != GameManager.BattleState.NormalTurn) gameManager.ChangeBattleState(GameManager.BattleState.NormalTurn);
        gameManager.EnableCards(true);

        if (IsVanishing) Vanish();
        else Discard();
    }

    public void AfterSummon()
    {
        gameManager.OnAfterPlayingCardGetCard?.Invoke(this);

        PlayUseEffect(DURATION_USE_EFFECT);
        Despawn(DURATION_USE_EFFECT);
    }

    public void SetCardXPosition()
    {
        int totalChildCount = handZone.transform.childCount + handZoneFront.transform.childCount;
        float totalCardTileWidth = handZone.childCount * cardTileWidth;

        if (totalChildCount % 2 == 1 && totalCardTileWidth < handZone.rect.width)
        {
            float startPoint = -(((totalChildCount - 1) / 2) * cardTileWidth);
            posX = startPoint + (handZoneIndex * cardTileWidth);
        }
        else if (totalChildCount % 2 == 0 && totalCardTileWidth < handZone.rect.width)
        {
            float startPoint = -(cardTileWidth / 2) + -(((totalChildCount / 2) - 1) * cardTileWidth);
            posX = startPoint + (handZoneIndex * cardTileWidth);
        }
        else
        {
            float newCardTileWidth = handZone.rect.width / totalChildCount;
            float startPoint = -(handZone.rect.width / 2) + (newCardTileWidth / 2);
            posX = startPoint + (handZoneIndex * newCardTileWidth);
        }

        if (handZoneFront.childCount != 0)
        {
            Card toppedCard = handZoneFront.GetChild(0).GetComponent<Card>();
            int toppedCardIndex = toppedCard.handZoneIndex;

            if (toppedCardIndex != handZoneIndex)
            {
                float distanceAway;
                float cardTileWidthUsed;

                if ((handZone.childCount + handZoneFront.childCount) * cardTileWidth < handZone.rect.width)
                {
                    cardTileWidthUsed = cardTileWidth;
                    distanceAway = distanceAwayFromToppedCard - cardTileWidthUsed;
                }
                else
                {
                    cardTileWidthUsed = handZone.rect.width / (handZone.childCount + handZoneFront.childCount);
                    distanceAway = distanceAwayFromToppedCard - cardTileWidthUsed;
                }

                for (int i = 0; i < Mathf.Abs(toppedCardIndex - handZoneIndex) - 1; i++)
                {
                    distanceAway /= 2;
                }

                if(handZoneIndex < toppedCardIndex)
                {
                    posX = toppedCard.posX - (Mathf.Abs(toppedCardIndex - handZoneIndex) * cardTileWidthUsed) - distanceAway;
                }
                else if (handZoneIndex > toppedCardIndex)
                {
                    posX = ((Mathf.Abs(toppedCardIndex - handZoneIndex) * cardTileWidthUsed) + toppedCard.posX) + distanceAway;
                }
            }
        }
    }

    public void SetCardYPosition()
    {
        if (transform.parent == handZoneFront)
        {
            posY = posYHandZoneFront;
        }
        else
        {
            float height = handZone.rect.height / 2;
            posY = -0.000185f * (Mathf.Pow(posX - 0, 2)) + height;
        }
    }

    void SetCardZPosition()
    {
        if (transform.parent == handZoneFront.transform)
        {
            posZ = -25;
        }
        else
        {
            int totalChildCount = handZone.transform.childCount + handZoneFront.transform.childCount;
            posZ = (((totalChildCount - 0) - handZoneIndex) * 5f) + 1f;
        }
    }

    void SetCardZRotation()
    {
        if(transform.parent == handZoneFront)
        {
            rotZ = 0f;
        }
        else
        {
            rotZ = -((Mathf.Abs(posX - (-(handZone.rect.width / 2))) / handZone.rect.width) * (leftMostRotation * 2) - leftMostRotation);
        }
    }

    void EnlargeSize(bool willDo)
    {
        if (!isScaleFrozen)
        {
            if (willDo)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, enlargedSize, scaleChangeTime * Time.deltaTime);
            }
            else
            {
                transform.localScale = Vector3.Lerp(transform.localScale, defaultSize, scaleChangeTime * Time.deltaTime);
            }
        }
    }

    public void SetRaycastable(bool willDo)
    {
        rawImage.raycastTarget = willDo;
    }

    public void ResetHandZoneCardValues()
    {
        IEnumerator Func()
        {
            yield return new WaitForEndOfFrame();

            foreach (Transform cardTransform in handZone)
            {
                Card card = cardTransform.GetComponent<Card>();
                card.hasClickedMouseButtonUp = false;
            }
        }

        StartCoroutine(Func());
    }

    public void IsCardMoving(bool willDo)
    {
        isPosFrozen = !willDo;
    }

    public void SetGlowActive(bool willDo)
    {
        cardGlow.gameObject.SetActive(willDo);
    }

    public void CheckSelectability()
    {
        if (gameManager.AreCardsDisabled || gameManager.IsDrawingCards)
        {
            SetGlowActive(false);
            SetCardSelectable(false);
        }
        else
        {
            SetGlowActive(true);
            SetCardSelectable(true);
        }
    }

    public void DimCard(bool willDo)
    {
        if (willDo)
        {
            willDimCard = true;
            targetColor = ResourcesManager.CardDimmedColor;
        }
        else
        {
            willDimCard = false;
            targetColor = ResourcesManager.CardNormalColor;
        }

        willUpdateImageColors = true;
    }

    public void SetCardSelectable(bool isSelectable)
    {
        if (isSelectable)
        {
            if (isSilenced)
            {
                if (CanPerformActions()) DimCard(false);
            }
            else DimCard(false);

            canBeHovered = true;

            if (CanPerformActions() || state == State.UsedForSelection)
            {
                IsSelectable = true;
                ////SetGlowActive(true);
                if (state == State.UsedForBattle) draggable.canBeDragged = true;
            }
            else
            {
                IsSelectable = false;
                SetGlowActive(false);
                draggable.canBeDragged = false;
            }
        }
        else
        {
            canBeHovered = false;
            IsSelectable = false;
            if (gameManager.AreCardsDisabled) DimCard(true);
            SetGlowActive(false);
            draggable.canBeDragged = false;
        }
    }

    public void UpdateImageColors()
    {
        if (willUpdateImageColors)
        {
            if (transform.parent.name == "HandZone" || transform.parent.name == "HandZoneFront")
            {
                int colorChangingDone = 0;
                currentColor.r = Mathf.Lerp(currentColor.r, targetColor.r, Time.deltaTime * colorChangeTime);
                currentColor.g = Mathf.Lerp(currentColor.g, targetColor.g, Time.deltaTime * colorChangeTime);
                currentColor.b = Mathf.Lerp(currentColor.b, targetColor.b, Time.deltaTime * colorChangeTime);
                currentColor.a = Mathf.Lerp(currentColor.a, targetColor.a, Time.deltaTime * colorChangeTime);

                foreach (Image image in imageComponents)
                {
                    if (image.gameObject.activeSelf)
                    {
                        if (image.color.Equals(targetColor))
                        {
                            colorChangingDone++;
                        }
                        else
                        {
                            image.color = currentColor;
                        }
                    }
                }

                int textColorChangingDone = 0;

                foreach (TextMeshProUGUI text in textComponents)
                {
                    if (text.gameObject.activeSelf)
                    {
                        if (text.color.a.Equals(targetColor.r))
                        {
                            textColorChangingDone++;
                        }
                        else
                        {
                            Color newColor = new Color(text.color.r, text.color.g, text.color.b, currentColor.r);
                            text.color = newColor;
                        }
                    }
                }

                if (colorChangingDone == imageComponents.Count && textColorChangingDone == textComponents.Count)
                {
                    willUpdateImageColors = false;
                }
            }
        }
    }

    void InitializeColor()
    {
        foreach (Image image in imageComponents)
        {
            image.color = ResourcesManager.CardNormalColor;
        }

        foreach (TextMeshProUGUI text in textComponents)
        {
            text.color = new Color(text.color.r, text.color.g, text.color.b, 1f);
        }
    }

    void InitializeSpells()
    {
        foreach (Spell spell in CurrentCardData.Spells)
        {
            if (spell._Type == Spell.TYPE_SENT_TO_DISCARD_PILE)
            {
                OnDiscardByEffect += spell.Cast;
            }
            if (spell._Type == Spell.TYPE_SELF_BUFF)
            {
                if (spell is IEffectHasTraits)
                {
                    IEffectHasTraits effectHasTraits = spell as IEffectHasTraits;
                    foreach (Trait trait in effectHasTraits.Traits)
                    {
                        if (trait is CardTrait)
                        {
                            CardTrait cardTrait = trait as CardTrait;
                            AddCardTrait(cardTrait);
                        }
                    }
                }
            }
            if (spell._Type == Spell.TYPE_CARD_PASSIVE)
            {
                CardPassiveSpell cardPassiveSpell = spell as CardPassiveSpell;
                cardPassiveSpell.ApplyCardPassive();
            }
            if (spell is ISpellHasBloodlessEffect blf && !gameManager.HasDealtDamageLastTurnAtksSpls && GameManager.BattleTurnCount > 1) blf.OnTriggerBloodless?.Invoke();
        }
    }

    public void AddCardTrait(CardTrait cardTrait)
    {
        if (!cardTraits.ContainsKey(cardTrait.ID))
        {
            cardTrait.ApplyToCard(this);
            cardTraits.Add(cardTrait.ID, cardTrait);
        }
    }

    public CardTrait GetCardTrait(int id)
    {
        if (cardTraits.ContainsKey(id)) return cardTraits[id];
        return null;
    }

    public void RemoveCardTrait(CardTrait cardTrait)
    {
        if (cardTrait != null && cardTraits.ContainsKey(cardTrait.ID))
        {
            cardTraits[cardTrait.ID].RemoveFromCard();
            cardTraits.Remove(cardTrait.ID);
        }
    }

    void InitializeCardData()
    {
        CurrentCardData.Card = this;

        foreach(Spell spell in CurrentCardData.Spells)
        {
            if (spell is PlayerSpell)
            {
                PlayerSpell playerSpell = spell as PlayerSpell;
                playerSpell.Card = this;
                playerSpell.OriginalCardData = OriginalCardData;
                playerSpell.CurrentCardData = CurrentCardData;
            }
        }
    }

    public void UpdateCardText(CardData cardData)
    {
        string cardDescription = "";

        for (int i = 0; i < cardData.Spells.Count; i++)
        {
            if (cardDescription != "") cardDescription += "\n";
            cardDescription += cardData.Spells[i].GetDescription();
        }

        if (cardData is MinionCardDataReturnToSummon minCardRet)
        {
            if (cardDescription != "") cardDescription += "\n";
            cardDescription += minCardRet.GetReturnEffectDesc();
        }

        if (cardData is MinionCardDataSacrificeToSummon minCardSac)
        {
            if (cardDescription != "") cardDescription += "\n";
            cardDescription += minCardSac.GetSacrificeEffectDesc();
        }

        textName.SetText("#" + cardData.ID.ToString() + " " + cardData.Name);
        if (cardDescription == "") cardDescription = " ";
        textDescription.SetText(cardDescription);

        if (cardData is MinionCardData)
        {
            MinionCardData minionCardData = cardData as MinionCardData;

            if (minionCardData.minionType == MinionCardData.MINIONTYPE_SUPPORT)
            {
                attackIcon.gameObject.SetActive(false);
            }
            else
            {
                attackIcon.gameObject.SetActive(true);
                textAttack?.SetText(minionCardData.Attack.ToString());
            }

            textDefense?.SetText(minionCardData.Defense.ToString());
        }

        textCost.SetText(Cost.ToString());
    }

    public void ModifyAttack(int amount)
    {
        if (CurrentCardData is MinionCardData)
        {
            MinionCardData minionCardData = CurrentCardData as MinionCardData;
            minionCardData.Attack += amount;

            textAttack.SetText(minionCardData.Attack.ToString());
        }
    }

    public void ModifyDefense(int amount)
    {
        if (CurrentCardData is MinionCardData)
        {
            MinionCardData minionCardData = CurrentCardData as MinionCardData;
            minionCardData.Defense += amount;

            textDefense.SetText(minionCardData.Defense.ToString());
        }
    }

    public void ModifyCost(int amount)
    {
        Cost += amount;
        UpdateCost();
    }

    public void UpdateCost()
    {
        textCost.SetText(Cost.ToString());
        OnUpdateCost?.Invoke();
        CheckSelectability();
    }

    public void SetCost(int amount)
    {
        Cost = amount;
        UpdateCost();
    }

    void InitializeCardArtwork()
    {
        if (CurrentCardData.Artwork != null)
        {
            artworkImage.sprite = CurrentCardData.Artwork;
        }
        else
        {
            artworkImage.sprite = ResourcesManager.CardCanvasTemp;
        }
        //canvasImage.SetNativeSize();
    }

    void InitializeCardFrame()
    {
        if (CurrentCardData is SpellCardData)
        {
            if (CurrentCardData.Rarity == DataLibrary.rarity003) frameImage.sprite = ResourcesManager.SpriteSpellCardFrameVeryRare;
            else if (CurrentCardData.Rarity == DataLibrary.rarity002) frameImage.sprite = ResourcesManager.SpriteSpellCardFrameRare;
            else frameImage.sprite = ResourcesManager.SpriteSpellCardFrameCommon;
        }
        else
        {
            if (CurrentCardData.Rarity == DataLibrary.rarity003) frameImage.sprite = ResourcesManager.SpriteMinionCardFrameVeryRare;
            else if (CurrentCardData.Rarity == DataLibrary.rarity002) frameImage.sprite = ResourcesManager.SpriteMinionCardFrameRare;
            else frameImage.sprite = ResourcesManager.SpriteMinionCardFrameCommon;
        }
    }

    void InitializeUpgrading()
    {
        if (OriginalCardData.IsUpgradeable())
        {
            targetColor = ResourcesManager.CardNormalColor;
            IsSelectable = true;
        }
        else
        {
            targetColor = ResourcesManager.CardDimmedColor;
            IsSelectable = false;
        }
    }

    public void SetColor(Color color)
    {
        foreach (Image image in imageComponents)
        {
            image.color = color;
        }
    }

    public void Upgrade()
    {
        if (OriginalCardData.IsUpgradeable())
        {
            OriginalCardData.UpgradeInfo.SetLevel(OriginalCardData.UpgradeInfo.Level + 1, 1);
            UpdateCardText(OriginalCardData);
        }
    }

    public void UpgradeInBattle()
    {
        if (OriginalCardData.IsUpgradeable())
        {
            OriginalCardData.UpgradeInfo?.SetLevel(OriginalCardData.UpgradeInfo.Level + 1, 1);
            CurrentCardData.UpgradeInfo?.SetLevel(CurrentCardData.UpgradeInfo.Level + 1, 1);
            UpdateCardText(CurrentCardData);
        }
    }

    public void Silence()
    {
        if (!isSilenced)
        {
            isSilenced = true;

            if (!CanPerformActions())
            {
                DimCard(true);
            }
        }
    }

    public void Desilence()
    {
        if (isSilenced)
        {
            isSilenced = false;

            DimCard(false);
        }
    }

    public void AddCardVanisher(IEffectVanishesCard f)
    {
        if (!cardVanishers.Contains(f)) cardVanishers.Add(f);
    }

    public void RemoveCardVanisher(IEffectVanishesCard f)
    {
        if (cardVanishers.Contains(f)) cardVanishers.Remove(f);
    }

    public void AddCostModif(IEffectModifiesCardCost f)
    {
        if (!costModifs.Contains(f))
        {
            costModifs.Add(f);
            UpdateCost();
        }
    }

    public void RemoveCostModif(IEffectModifiesCardCost f)
    {
        if (costModifs.Contains(f))
        {
            costModifs.Remove(f);
            UpdateCost();
        }
    }

    public void AddStatModif(IEffectModifiesCardStats f)
    {
        if (!statModifs.Contains(f))
        {
            statModifs.Add(f);
            UpdateCost();
        }
    }

    public void RemoveStatModif(IEffectModifiesCardStats f)
    {
        if (statModifs.Contains(f))
        {
            statModifs.Remove(f);
            UpdateCost();
        }
    }
}

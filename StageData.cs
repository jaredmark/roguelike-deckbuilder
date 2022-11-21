using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class RoomData
{
    public Sprite Icon { get; protected set; }
    public string Name { get; protected set; }
    public RoomIcon RoomIconObj { get; protected set; }
    public List<RoomData> WayPoints { get; protected set; }
    public List<RoomData> PrevWayPoints { get; protected set; }
    public int levelIndex; //ascending order 0 = bottom
    public int stageIndex;

    public delegate void OnEnterEvent();
    public OnEnterEvent OnEnter;

    //stage prefab
    public RoomData()
    {
        WayPoints = new List<RoomData>();
        PrevWayPoints = new List<RoomData>();
    }

    public virtual void Enter()
    {
        OnEnter?.Invoke();
    }

    public virtual void Leave()
    {

    }

    public void GetOtherRoomDataValues(RoomData otherRoomData)
    {
        WayPoints.AddRange(otherRoomData.WayPoints);
        PrevWayPoints.AddRange(otherRoomData.PrevWayPoints);
        RoomIconObj = otherRoomData.RoomIconObj;
        levelIndex = otherRoomData.levelIndex;
        stageIndex = otherRoomData.stageIndex;

        foreach (RoomData rd in otherRoomData.WayPoints)
        {
            rd.PrevWayPoints.Add(this);
            rd.PrevWayPoints.Remove(otherRoomData);
        }
        foreach (RoomData rd in otherRoomData.PrevWayPoints)
        {
            rd.WayPoints.Add(this);
            rd.WayPoints.Remove(otherRoomData);
        }
    }

    public void SetWayPoint(RoomData roomData)
    {
        if (roomData != null)
        {
            if (!WayPoints.Contains(roomData) && !roomData.PrevWayPoints.Contains(this))
            {
                WayPoints.Add(roomData);
                roomData.PrevWayPoints.Add(this);
            }
        }
    }

    public void SetGameObject(RoomIcon roomIconObj)
    {
        RoomIconObj = roomIconObj;
    }
}

//public abstract class RoomDataOld
//{
//    public Sprite Icon { get; protected set; }
//    public Vector2Int Pos { get; protected set; }
//    public string Name { get; protected set; }
//    public RoomIcon RoomIconObj { get; protected set; }
//    public List<Vector2Int> WayPoints { get; protected set; }
//    public bool HasWaypoint { get; protected set; }
//    public bool CanBeEntered { get; protected set; }
//    public bool CanBePassedThrough { get; protected set; }
//    public RoomData prevRoom;
//    public bool isConnectedToStart;

//    public int levelChoicesCount;

//    public delegate void OnEnterEvent();
//    public OnEnterEvent OnEnter;

//    //stage prefab

//    public RoomData(Vector2Int position)
//    {
//        Pos = position;
//        WayPoints = new List<Vector2Int>();
//    }

//    public virtual void Enter()
//    {
//        CanBeEntered = false;
//        OnEnter?.Invoke();
//    }

//    public virtual void Leave()
//    {
        
//    }

//    public void GetOtherRoomDataValues(RoomData otherRoomData)
//    {
//        SetPos(otherRoomData.Pos.x, otherRoomData.Pos.y);
//        WayPoints = otherRoomData.WayPoints;
//        HasWaypoint = otherRoomData.HasWaypoint;
//        //CanBeEntered = otherRoomData.CanBeEntered;
//        RoomIconObj = otherRoomData.RoomIconObj;
//    }

//    public void SetWayPoint(RoomData roomData)
//    {
//        if (roomData != null)
//        {
//            if (!WayPoints.Contains(roomData.Pos))
//            {
//                WayPoints.Add(roomData.Pos);
//                roomData.SetWayPoint(this);
//                HasWaypoint = true;
//            }
//        }
//    }

//    public void SetGameObject(RoomIcon roomIconObj)
//    {
//        RoomIconObj = roomIconObj;
//    }

//    public void SetPos(int x, int y)
//    {
//        Pos = new Vector2Int(x, y);
//    }

//    public void SetPos(Vector2Int vector2)
//    {
//        Pos = new Vector2Int(vector2.x, vector2.y);
//    }
//}

public abstract class BattleRoomData : RoomData
{
    public readonly List<Reward> rewards = new List<Reward>();
    public readonly List<EnemyData> enemyDataList = new List<EnemyData>();

    protected void SetArcaniteReward(int minAmount, int maxAmount)
    {
        rewards.Add(new RewardArcanite(Random.Range(minAmount, maxAmount + 1)));
    }

    protected void SetUpgradeReward()
    {
        rewards.Add(new RewardItem(DataLibrary.GetItemData(30)));
    }

    protected void SetRemoveReward()
    {
        rewards.Add(new RewardItem(DataLibrary.GetItemData(31)));
    }
}

public class StartingRoomData : RoomData
{
    public override void Enter()
    {
        PlayerCharacter.pc.SetPos(PlayerCharacter.pc.posPlayerInBattle);
        GameManager.StageMap.EnableTravelling();
    }
}

public class EndingRoomData : RoomData
{

}

public class EmptyRoomData : RoomData
{

}

public class KeyRoomData : BattleRoomData
{
    public override void Enter()
    {
        base.Enter();

        EnemyWave selectedBossEnemyWave = DataLibrary.enemyBossList.Get(GameManager.StageMap.CurrentStage);
        rewards.Add(new RewardPassiveEffect(DataLibrary.keyItems[GameManager.StageMap.CurrentStage - 1]));
        rewards.Add(new RewardArcanite(Random.Range(DataLibrary.MIN_BOSS_REWARD_GOLD, DataLibrary.MAX_BOSS_REWARD_GOLD + 1)));
        //spawn boss
        GameManager.gm.PrepareBattle(selectedBossEnemyWave.EnemyDataList, rewards);
        GameManager.gm.BeginBattle();
    }
}

public class TreasureRoomData : RoomData
{
    abstract class Type
    {
        public GameObject treasureContainerPrefab;
        public readonly List<Reward> rewards = new List<Reward>();
        //stage

        protected void GetConsumableOrPassiveItemRewards(List<Reward> rewardsListToFill, int passiveItemCount, int consumItemCount, DataLibrary.Rarity baseRarity)
        {
            List<DataLibrary.Class> itemClasses = new List<DataLibrary.Class>();
            itemClasses.Add(DataLibrary.class001);
            itemClasses.Add(PlayerData.data.Class);

            List<int> currentItemsID = new List<int>();
            foreach (Item item in GameManager.gm.GetPlayerItems())
            {
                currentItemsID.Add(item.ItemData.ID);
            }

            if (passiveItemCount > 0)
            {
                List<ItemData> passiveItemDataList = DataLibrary.ItemCollection.GetUniqueItemByBaseRarity(passiveItemCount, itemClasses, baseRarity, currentItemsID);
                foreach (ItemData itemData in passiveItemDataList)
                {
                    rewardsListToFill.Add(new RewardItem(DataLibrary.GetItemData(itemData.ID)));
                }
            }

            //then get consumables:

            if (consumItemCount > 0)
            {
                List<ItemData> consumItemDataList = DataLibrary.ConsumableItemCollection.GetItemByBaseRarity(1, itemClasses, baseRarity);
                foreach (ItemData itemData in consumItemDataList)
                {
                    rewardsListToFill.Add(new RewardItem(DataLibrary.GetItemData(itemData.ID)));
                }
            }
        }

        protected void GetPassiveItemRewards(List<Reward> rewardsListToFill, int amount, DataLibrary.Rarity baseRarity)
        {
            List<DataLibrary.Class> itemClasses = new List<DataLibrary.Class>();
            itemClasses.Add(DataLibrary.class001);
            itemClasses.Add(PlayerData.data.Class);

            List<int> currentItemsID = new List<int>();
            foreach (Item item in GameManager.gm.GetPlayerItems())
            {
                currentItemsID.Add(item.ItemData.ID);
            }

            foreach (ItemData itemData in DataLibrary.ItemCollection.GetUniqueItemByBaseRarity(amount, itemClasses, baseRarity, currentItemsID))
            {
                rewardsListToFill.Add(new RewardItem(DataLibrary.GetItemData(itemData.ID)));
            }
        }
    }

    class TypeCommon : Type
    {
        const int MIN_ITEM_COUNT = 0;
        const int MAX_ITEM_COUNT = 2;
        const DataLibrary.Rarity BASE_ITEM_RARITY = DataLibrary.Rarity.Common;

        public TypeCommon()
        {
            treasureContainerPrefab = ResourcesManager.PrefabTreasureContainer;
            //get random consumables
            GetConsumableOrPassiveItemRewards(rewards, 1, Random.Range(MIN_ITEM_COUNT, MAX_ITEM_COUNT + 1), BASE_ITEM_RARITY);
        }
    }

    class TypeRare : Type
    {
        const int MAX_PASSIVE_ITEM_COUNT = 1;
        const int MIN_ITEM_COUNT = 0;
        const int MAX_ITEM_COUNT = 1;
        const DataLibrary.Rarity BASE_ITEM_RARITY = DataLibrary.Rarity.Rare;

        public TypeRare()
        {
            treasureContainerPrefab = ResourcesManager.PrefabTreasureContainer;
            GetConsumableOrPassiveItemRewards(rewards, MAX_PASSIVE_ITEM_COUNT, Random.Range(MIN_ITEM_COUNT, MAX_ITEM_COUNT + 1), BASE_ITEM_RARITY);
        }
    }

    class TypeVeryRare : Type
    {
        const int MAX_ITEM_COUNT = 1;
        const DataLibrary.Rarity BASE_ITEM_RARITY = DataLibrary.Rarity.Epic;

        public TypeVeryRare()
        {
            treasureContainerPrefab = ResourcesManager.PrefabTreasureContainer;
            //get item
            GetPassiveItemRewards(rewards, MAX_ITEM_COUNT, BASE_ITEM_RARITY);
        }
    }

    readonly List<Reward> rewards = new List<Reward>();
    readonly GameObject treasureContainerPrefab;
    TreasureContainer treasureContainer;

    public TreasureRoomData()
    {
        //Get treasure room type by chance:
        List<DataLibrary.Rarity> rarityList = new List<DataLibrary.Rarity>(DataLibrary.TreasureRoomTypeRarityDictionary.Keys);
        List<float> rarityChancesList = new List<float>(DataLibrary.TreasureRoomTypeRarityDictionary.Values);

        DataLibrary.Rarity roomRarity = GameManager.GetItemFromListByChance(rarityList, rarityChancesList);

        Type roomType;

        switch (roomRarity)
        {
            case DataLibrary.Rarity.Common:
                roomType = new TypeCommon();
                break;
            case DataLibrary.Rarity.Rare:
                roomType = new TypeRare();
                break;
            case DataLibrary.Rarity.Epic:
                roomType = new TypeVeryRare();
                break;
            default:
                Debug.Log("Treasure Room Type not set.");
                roomType = null;
                break;
        }

        rewards.AddRange(roomType.rewards);
        treasureContainerPrefab = roomType.treasureContainerPrefab;
    }

    public override void Enter()
    {
        base.Enter();

        //spawn treasure container and initialize it
        Debug.Log("Spawned chest");
        treasureContainer =  Object.Instantiate(treasureContainerPrefab, GameManager.gm.battleStage).GetComponent<TreasureContainer>();
        treasureContainer.Initialize(rewards);
    }

    public override void Leave()
    {
        base.Leave();

        Object.Destroy(treasureContainer.gameObject);
    }
}

public class EnemyRoomData : BattleRoomData
{
    public abstract class Modifier
    {
        public string Name { get; protected set; }
        public abstract string Description { get; }
        public int ID { get; protected set; }

        public abstract void Apply();
        public abstract void Remove();
    }

    public class Modifier000 : Modifier, IEffectModifiesMaxEnergy, IEffectModifiesStartTurnDrawCount
    {
        public int MaxEnergyModifier { get; set; }
        public int StartTurnDrawCountModifier { get; set; }

        public Modifier000()
        {
            Name = "Mindbreaking";
            ID = 0;

            MaxEnergyModifier = -1;
            StartTurnDrawCountModifier = -1;
        }

        public override string Description
        {
            get
            {
                return MaxEnergyModifier + " Max Energy\n" + StartTurnDrawCountModifier + " Start of turn draw count";
            }
        }

        public override void Apply()
        {
            PlayerCharacter.pc.AddMaxEnergyModifier(this);
            PlayerCharacter.pc.AddStartTurnDrawCountModifier(this);
        }

        public override void Remove()
        {
            PlayerCharacter.pc.RemoveMaxEnergyModifier(this);
            PlayerCharacter.pc.RemoveStartTurnDrawCountModifier(this);
        }
    }

    public class Modifier001 : Modifier, IEffectModifiesMaxEnergy
    {
        public int MaxEnergyModifier { get; set; }

        public Modifier001()
        {
            Name = "Aggravating";
            ID = 1;

            MaxEnergyModifier = -1;
        }

        public override string Description => MaxEnergyModifier + " Max Energy";

        public override void Apply()
        {
            PlayerCharacter.pc.AddMaxEnergyModifier(this);
        }

        public override void Remove()
        {
            PlayerCharacter.pc.RemoveMaxEnergyModifier(this);
        }
    }

    public class Modifier002 : Modifier, IEffectModifiesStartTurnDrawCount
    {
        public int StartTurnDrawCountModifier { get; set; }

        public override string Description => StartTurnDrawCountModifier + " Start of turn draw count";

        public Modifier002()
        {
            Name = "Uninspiring";
            ID = 2;

            StartTurnDrawCountModifier = -1;
        }

        public override void Apply()
        {
            PlayerCharacter.pc.AddStartTurnDrawCountModifier(this);
        }

        public override void Remove()
        {
            PlayerCharacter.pc.RemoveStartTurnDrawCountModifier(this);
        }
    }

    public List<Modifier> modifs;

    public EnemyRoomData()
    {
        rewards.Add(new RewardCardPack(DataLibrary.rarity001));
        rewards.Add(new RewardArcanite(Random.Range(DataLibrary.minNormalEnemyArcaniteReward, DataLibrary.maxNormalEnemyArcaniteReward + 1)));
        if (GameManager.GetTrueBoolByChance(DataLibrary.nonBossBttleCnsmblRewardChance))
        {
            List<DataLibrary.Class> itemClasses = new List<DataLibrary.Class>();
            itemClasses.Add(DataLibrary.class001);
            itemClasses.Add(PlayerData.data.Class);
            rewards.Add(new RewardItem(DataLibrary.GetItemData(DataLibrary.ConsumableItemCollection.GetItem(1, itemClasses)[0].ID)));
        }
    }

    public override void Enter()
    {
        base.Enter();

        EnemyWave.Type enemyWaveType = GameManager.StageMap.currentLevel <= GameManager.StageMap.easyTime ? EnemyWave.TYPE_WEAK : EnemyWave.TYPE_NORMAL;

        foreach (EnemyData enemyData in DataLibrary.EnemyWaveList.GetEnemyWave(GameManager.StageMap.CurrentStage, enemyWaveType).GetEnemyData())
        {
            enemyDataList.Add(DataLibrary.DuplicateEnemyData(enemyData));
        }

        GameManager.gm.PrepareBattle(enemyDataList, rewards);
        GameManager.gm.BeginBattle();
    }
}

public class StrongEnemyRoomData : BattleRoomData
{
    static readonly DataLibrary.Rarity itemRewardBaseRarity = DataLibrary.rarity002;

    public StrongEnemyRoomData()
    {
        rewards.Add(new RewardCardPack(DataLibrary.rarity002));
        rewards.Add(new RewardArcanite(Random.Range(DataLibrary.minStrongEnemyArcaniteReward, DataLibrary.maxStrongEnemyArcaniteReward + 1)));
    }

    public override void Enter()
    {
        base.Enter();

        foreach (EnemyData enemyData in DataLibrary.EnemyWaveList.GetEnemyWave(GameManager.StageMap.CurrentStage, EnemyWave.TYPE_STRONG).GetEnemyData())
        {
            enemyDataList.Add(DataLibrary.DuplicateEnemyData(enemyData));
        }

        List<DataLibrary.Class> itemClasses = new List<DataLibrary.Class>();
        itemClasses.Add(DataLibrary.class001);
        itemClasses.Add(PlayerData.data.Class);
        List<int> currenItemIDList = GameManager.gm.GetPlayerItemsID();
        rewards.Add(new RewardItem(DataLibrary.GetItemData(DataLibrary.ItemCollection.GetUniqueItemByBaseRarity(1, itemClasses, itemRewardBaseRarity, currenItemIDList)[0].ID)));

        if (GameManager.GetTrueBoolByChance(DataLibrary.nonBossBttleCnsmblRewardChance))
        {
            rewards.Add(new RewardItem(DataLibrary.GetItemData(DataLibrary.ConsumableItemCollection.GetItem(1, itemClasses)[0].ID)));
        }

        //if (GameManager.StageMap.nonBossBattleEncounterCount % DataLibrary.upgrdRewardNonBossEnmyEncntrCount == 0) SetUpgradeReward();
        //if (GameManager.StageMap.nonBossBattleEncounterCount % DataLibrary.removeRewardNonBossEnmyEncntrCount == 0) SetRemoveReward();
        GameManager.gm.PrepareBattle(enemyDataList, rewards);
        GameManager.gm.BeginBattle();
    }
}

public class FinalBossRoomData : BattleRoomData
{

}

public class RestRoomData : RoomData
{
    public class RestRoomDialogueData
    {
        public float healthPercentage;
        public CardData.Effect cardEffect;

        public RestRoomDialogueData(float healthPercentage, CardData.Effect cardEffect)
        {
            this.healthPercentage = healthPercentage;
            this.cardEffect = cardEffect;
        }
    }

    readonly RestRoomDialogueData data;

    public RestRoomData()
    {
        data = new RestRoomDialogueData(0.35f, GameManager.GetItemFromListByChance(DataLibrary.cardEffects, DataLibrary.cardEffectChances));
    }

    public override void Enter()
    {
        base.Enter();
        GameManager.UiHandler.ShowRestRoomDialogue(data);
    }
}

public class EventRoomData : RoomData
{
    EventData eventData;

    public EventRoomData(EventData eventData)
    {
        this.eventData = eventData;
    }

    public override void Enter()
    {
        base.Enter();

        GameManager.gm.IsInEvent = true;
        //eventData = new EventData009();
        eventData.Start();
        //play event object
    }

    public override void Leave()
    {
        base.Leave();
        eventData.OnLeave();
    }
}

public class ShopRoomData : RoomData
{
    Merchant merchant;

    public override void Enter()
    {
        base.Enter();
        //spawn storekeeper
        merchant = Object.Instantiate(ResourcesManager.PrefabMerchant, GameManager.gm.battleStage).GetComponent<Merchant>();
        merchant.Initialize(GameManager.gm.GetMerchantItemDataList(), GameManager.gm.GetMerchantCardDataList(), PlayerCharacter.pc.ShopPriceModifier);
        GameManager.StageMap.EnableTravelling();

        GameManager.StageMap.merchantAppearanceCount++;
        //change background
    }

    public override void Leave()
    {
        //GameManager.StageMap.EmptyRoom(GameManager.StageMap.CurrentRoom);
        GameManager.ShopWindow.Close();
        merchant.gameObject.SetActive(false);
        Object.Destroy(merchant.gameObject);
        base.Leave();      
    }
}

//public class WorldData
//{
//    public enum GameStage { First, Second, Last };
//    public const GameStage GS_FIRST = GameStage.First;
//    public const GameStage GS_SECOND = GameStage.Second;
//    public const GameStage GS_LAST = GameStage.Last;

//    const int MIN_MAP_WIDTH = 9;
//    const int MIN_MAP_HEIGHT = 7;

//    const int MAP_OUTER_SIZE = 3; //should be greater than 0
//    const float MAP_OUTER_FIRST_FILL_PERCENTAGE = 0.1f;
//    const float MAP_COVERAGE_MIN_PERCENTAGE = 0.8f;
//    const float MAP_COVERAGE_MAX_PERECNTAGE = 0.9f;
//    const int ROOM_CONNECTION_MAX_RADIUS = 3;

//    const int ROOM_DISTANCE_FROM_START_KEY = 9;
//    const int ROOM_DISTANCE_FROM_EACH_OTHER_KEY = 7;

//    const float ROOM_PERCENTAGE_STRONG_ENEMY = 0.16f;
//    const int ROOM_DISTANCE_FROM_START_STRONG_ENEMY = 3;
//    const int ROOM_DISTANCE_FROM_EACH_OTHER_STRONG_ENEMY = 4;

//    const int GAP_REST_ROOM = 5;
//    const int GAP_FROM_START_REST_ROOM = 4;
//    const float PERCENTAGE_REST_ROOM = 0.1f;

//    const float ROOM_PERCENTAGE_EVENT = 0.16f;
//    const int ROOM_DISTANCE_FROM_EACH_OTHER_EVENT = 2;

//    const float ROOM_PERCENTAGE_TREASURE = 0.08f;
//    const int ROOM_DISTANCE_FROM_START_TREASURE = 4;
//    const int ROOM_DISTANCE_FROM_EACH_OTHER_TREASURE = 5;

//    const int FAR_KEY_ROOM_FOG_DIST = 6;
//    const float DIAG_PATH_CHANCE = 5f;
//    const float ROOM_PRCTG_BONUS_PATH = 0.2f;

//    [SerializeField] bool SPAWN_START_ROOM_IN_LEFT_RIGHT_SIDE = false;
//    [SerializeField] bool START_ROOM_HAS_DIAGONAL_PATHWAY = false;

//    public readonly int seed;

//    public readonly Dictionary<Vector2Int, RoomData> RoomDataDictionary = new Dictionary<Vector2Int, RoomData>();
//    public readonly List<RoomData> revealedRooms = new List<RoomData>();

//    public WorldData(int seed, int width, int height)
//    {
//        this.seed = seed;
//        Random.InitState(seed);

//        if (width < MIN_MAP_WIDTH) width = MIN_MAP_WIDTH;
//        if (height < MIN_MAP_HEIGHT) height = MIN_MAP_HEIGHT;

//        int mapArea = width * height;
//        int roomCount = 0;
//        int minX, maxX, minY, maxY;
//        List<Vector2Int> vector2IntList = new List<Vector2Int>();
//        Vector2Int vector2Int = Vector2Int.zero;

//        //set min and max x and y values:
//        if (height % 2 == 0)
//        {
//            List<int> minMaxModifiers = new List<int>();
//            minMaxModifiers.Add(height / 2);
//            minMaxModifiers.Add((height / 2) - 1);

//            int selectedIndex = Random.Range(0, minMaxModifiers.Count);
//            minY = -minMaxModifiers[selectedIndex];
//            minMaxModifiers.RemoveAt(selectedIndex);
//            maxY = minMaxModifiers[0];
//        }
//        else
//        {
//            minY = -((height - 1) / 2);
//            maxY = (height - 1) / 2;
//        }

//        if (width % 2 == 0)
//        {
//            List<int> minMaxModifiers = new List<int>();
//            minMaxModifiers.Add(width / 2);
//            minMaxModifiers.Add((width / 2) - 1);

//            int selectedIndex = Random.Range(0, minMaxModifiers.Count);
//            minX = -minMaxModifiers[selectedIndex];
//            minMaxModifiers.RemoveAt(selectedIndex);
//            maxX = minMaxModifiers[0];
//        }
//        else
//        {
//            minX = -((width - 1) / 2);
//            maxX = (width - 1) / 2;
//        }

//        //fill RoomDataDictionary with null values
//        for (int x = minX; x <= maxX; x++)
//        {
//            for (int y = minY; y <= maxY; y++)
//            {
//                RoomDataDictionary.Add(new Vector2Int(x, y), null);
//            }
//        }

//        //put the final boss room data first at the middle)
//        RoomDataDictionary[Vector2Int.zero] = new EndingRoomData(Vector2Int.zero);

//        //fill positions in the outer part of the map with rooms:
//        for (int x = minX; x <= maxX; x++)
//        {
//            for (int y = minY; y <= maxY; y++)
//            {
//                if ((x < minX + MAP_OUTER_SIZE || x > maxX - MAP_OUTER_SIZE) && (y < minY + MAP_OUTER_SIZE || y > maxY - MAP_OUTER_SIZE))
//                {
//                    vector2IntList.Add(new Vector2Int(x, y));
//                }
//            }
//        }

//        for (int i = Mathf.RoundToInt(mapArea * MAP_OUTER_FIRST_FILL_PERCENTAGE); i > 0; i--)
//        {
//            int randomIndex = Random.Range(0, vector2IntList.Count);
//            RoomDataDictionary[vector2IntList[randomIndex]] = new EmptyRoomData(vector2IntList[randomIndex]);
//            vector2IntList.RemoveAt(randomIndex);
//            roomCount++;
//        }

//        //fill the rest of the map with rooms in any position:
//        vector2IntList.Clear();
//        foreach (Vector2Int pos in RoomDataDictionary.Keys)
//        {
//            if (RoomDataDictionary[pos] == null) vector2IntList.Add(pos);
//        }

//        int targetRoomCount = Mathf.RoundToInt(Random.Range(MAP_COVERAGE_MIN_PERCENTAGE, MAP_COVERAGE_MAX_PERECNTAGE) * mapArea);
//        for (int i = roomCount - 1; i < targetRoomCount; i++)
//        {
//            int randomIndex = Random.Range(0, vector2IntList.Count);
//            RoomDataDictionary[vector2IntList[randomIndex]] = new EmptyRoomData(vector2IntList[randomIndex]);
//            vector2IntList.RemoveAt(randomIndex);
//            roomCount++;
//        }

//        //remove null rooms
//        vector2IntList.Clear();
//        vector2IntList.AddRange(RoomDataDictionary.Keys);

//        for (int i = vector2IntList.Count - 1; i >= 0; i--)
//        {
//            if (RoomDataDictionary[vector2IntList[i]] == null)
//            {
//                RoomDataDictionary.Remove(vector2IntList[i]);
//            }
//        }

//        //SET STARTING ROOM'S LOCATION
//        vector2IntList.Clear();

//        //get the rooms at the side of the map
//        bool hasFoundFarthestRoom = false;
//        int minSideRoomX = 0;
//        int maxSideRoomX = 0;
//        int minSideRoomY = 0;
//        int maxSideRoomY = 0;
//        Vector2Int roomDataPos;

//        if (SPAWN_START_ROOM_IN_LEFT_RIGHT_SIDE)
//        {
//            //get farthest room to the left
//            for (int x = minX; x <= maxX; x++)
//            {
//                for (int y = minY; y <= maxY; y++)
//                {
//                    roomDataPos = new Vector2Int(x, y);
//                    if (RoomDataDictionary.ContainsKey(roomDataPos))
//                    {
//                        hasFoundFarthestRoom = true;
//                        if (!vector2IntList.Contains(roomDataPos))
//                        {
//                            vector2IntList.Add(roomDataPos);
//                        }
//                    }
//                }
//                if (hasFoundFarthestRoom)
//                {
//                    minSideRoomX = x;
//                    break;
//                }
//            }

//            //get farthest room to the right
//            for (int x = maxX; x >= minY; x--)
//            {
//                for (int y = minY; y <= maxY; y++)
//                {
//                    roomDataPos = new Vector2Int(x, y);
//                    if (RoomDataDictionary.ContainsKey(roomDataPos))
//                    {
//                        hasFoundFarthestRoom = true;
//                        if (!vector2IntList.Contains(roomDataPos))
//                        {
//                            vector2IntList.Add(roomDataPos);
//                        }
//                    }
//                }
//                if (hasFoundFarthestRoom)
//                {
//                    maxSideRoomX = x;
//                    break;
//                }
//            }
//        }

//        //get farthest room from the top
//        for (int y = maxY; y >= minY; y--)
//        {
//            for (int x = minX; x <= maxX; x++)
//            {
//                roomDataPos = new Vector2Int(x, y);
//                if (RoomDataDictionary.ContainsKey(roomDataPos))
//                {
//                    hasFoundFarthestRoom = true;
//                    if (!vector2IntList.Contains(roomDataPos))
//                    {
//                        vector2IntList.Add(roomDataPos);
//                    }
//                }
//            }
//            if (hasFoundFarthestRoom)
//            {
//                maxSideRoomY = y;
//                break;
//            }
//        }

//        //get farthest room from the bottom
//        for (int y = minY; y <= maxY; y++)
//        {
//            for (int x = minX; x <= maxX; x++)
//            {
//                roomDataPos = new Vector2Int(x, y);
//                if (RoomDataDictionary.ContainsKey(roomDataPos))
//                {
//                    hasFoundFarthestRoom = true;
//                    if (!vector2IntList.Contains(roomDataPos))
//                    {
//                        vector2IntList.Add(roomDataPos);
//                    }
//                }
//            }
//            if (hasFoundFarthestRoom)
//            {
//                minSideRoomY = y;
//                break;
//            }
//        }
//        //
//        //get random pos
//        Vector2Int randomSideRoomPos = vector2IntList[Random.Range(0, vector2IntList.Count)];

//        //change that selected side room to enemy room so it is the first room to be explored:
//        EnemyRoomData firstEnemyRoomData = new EnemyRoomData(randomSideRoomPos);
//        //set first encounter rewards:
//        List<DataLibrary.Class> itemClasses = new List<DataLibrary.Class> { DataLibrary.class001, PlayerData.data.Class};
//        firstEnemyRoomData.rewards.Add(new RewardItem(DataLibrary.GetItemData(DataLibrary.ItemCollection.GetItemByRarity(1, DataLibrary.rarity001, itemClasses)[0].ID)));
//        firstEnemyRoomData.rewards.Add(new RewardItem(DataLibrary.GetItemData(DataLibrary.ConsumableItemCollection.GetItemByRarity(1, DataLibrary.rarity001, itemClasses)[0].ID)));

//        firstEnemyRoomData.WayPoints.AddRange(RoomDataDictionary[randomSideRoomPos].WayPoints);
//        RoomDataDictionary[randomSideRoomPos] = firstEnemyRoomData;
//        firstEnemyRoomData.isConnectedToStart = true;

//        //get random offset from the random side room position
//        vector2IntList.Clear();
//        for (int y = -1; y <= 1; y++)
//        {
//            for (int x = -1; x <= 1; x++)
//            {
//                roomDataPos = new Vector2Int(x, y);
//                if (roomDataPos == Vector2Int.zero) continue;
//                else if (!START_ROOM_HAS_DIAGONAL_PATHWAY && Mathf.Abs(x) == Mathf.Abs(y)) continue;

//                if (!RoomDataDictionary.ContainsKey(roomDataPos + randomSideRoomPos))
//                {
//                    if (randomSideRoomPos.x == minSideRoomX)
//                    {
//                        if (roomDataPos.x < 0) vector2IntList.Add(roomDataPos);
//                    }
//                    if (randomSideRoomPos.x == maxSideRoomX)
//                    {
//                        if (roomDataPos.x > 0) vector2IntList.Add(roomDataPos);
//                    }
//                    if (randomSideRoomPos.y == minSideRoomY)
//                    {
//                        if (roomDataPos.y < 0) vector2IntList.Add(roomDataPos);
//                    }
//                    if (randomSideRoomPos.y == maxSideRoomY)
//                    {
//                        if (roomDataPos.y > 0) vector2IntList.Add(roomDataPos);
//                    }
//                }
//            }
//        }

//        //Get random offset for the starting room
//        Vector2Int startingRoomOffset = vector2IntList[Random.Range(0, vector2IntList.Count)];

//        //Insert starting room
//        Vector2Int startingRoomPos = randomSideRoomPos + startingRoomOffset;
//        RoomDataDictionary.Add(startingRoomPos, new StartingRoomData(startingRoomPos));
//        RoomDataDictionary[randomSideRoomPos].SetWayPoint(RoomDataDictionary[startingRoomPos]);
//        RoomDataDictionary[startingRoomPos].isConnectedToStart = true;

//        //Create room connections

//        bool OnSegment(Vector2Int p, Vector2Int q, Vector2Int r)
//        {
//            if (q.x <= Mathf.Max(p.x, r.x) && q.x >= Mathf.Min(p.x, r.x) &&
//                q.y <= Mathf.Max(p.y, r.y) && q.y >= Mathf.Min(p.y, r.y))
//                return true;

//            return false;
//        }

//        int GetOrientation(Vector2Int p, Vector2Int q, Vector2Int r)
//        {
//            int val = (q.y - p.y) * (r.x - q.x) -
//                    (q.x - p.x) * (r.y - q.y);

//            if (val == 0) return 0; // colinear

//            return (val > 0) ? 1 : 2; // clock or counterclock wise
//        }

//        bool DoLineSegmentsIntersect(Vector2Int p1, Vector2Int q1, Vector2Int p2, Vector2Int q2)
//        {
//            int o1 = GetOrientation(p1, q1, p2);
//            int o2 = GetOrientation(p1, q1, q2);
//            int o3 = GetOrientation(p2, q2, p1);
//            int o4 = GetOrientation(p2, q2, q1);

//            // General case
//            if (o1 != o2 && o3 != o4)
//                return true;

//            // Special Cases
//            // p1, q1 and p2 are colinear and p2 lies on segment p1q1
//            if (o1 == 0 && OnSegment(p1, p2, q1)) return true;

//            // p1, q1 and q2 are colinear and q2 lies on segment p1q1
//            if (o2 == 0 && OnSegment(p1, q2, q1)) return true;

//            // p2, q2 and p1 are colinear and p1 lies on segment p2q2
//            if (o3 == 0 && OnSegment(p2, p1, q2)) return true;

//            // p2, q2 and q1 are colinear and q1 lies on segment p2q2
//            if (o4 == 0 && OnSegment(p2, q1, q2)) return true;

//            return false; // Doesn't fall in any of the above cases
//        }

//        List<Vector2Int> GetPossibleWaypoints(Vector2Int origin)
//        {
//            List<Vector2Int> passablePosList = new List<Vector2Int>();
//            List<Vector2Int> impassablePosList = new List<Vector2Int>();
//            List<Vector2Int> possibleWayPoints = new List<Vector2Int>();
//            List<Vector2Int> wayPoints = new List<Vector2Int>();
//            Vector2Int currSidePos = Vector2Int.zero;

//            int currRad = 0;
//            int maxRad = ROOM_CONNECTION_MAX_RADIUS;

//            do
//            {
//                currRad++;
//                passablePosList.Clear();

//                //Get passable outer rooms around the origin room
//                for (int x = Mathf.Clamp(origin.x - currRad, minX, maxX); x <= Mathf.Clamp(origin.x + currRad, minX, maxX); x++)
//                {
//                    for (int y = Mathf.Clamp(origin.y - currRad, minY, maxY); y <= Mathf.Clamp(origin.y + currRad, minY, maxY); y++)
//                    {
//                        if (x == origin.x - currRad || x == origin.x + currRad || y == origin.y - currRad || y == origin.y + currRad)
//                        {
//                            currSidePos.Set(x, y);

//                            if (!impassablePosList.Contains(currSidePos) && !passablePosList.Contains(currSidePos))
//                            {
//                                passablePosList.Add(currSidePos);
//                            }
//                        }
//                    }
//                }

//                //Check if there's a room for each pos. If there's one, add it to possible waypoints list then add the positions behind that room to impassable pos list
//                foreach (Vector2Int pos in passablePosList)
//                {
//                    if (RoomDataDictionary.ContainsKey(pos))
//                    {
//                        if (!RoomDataDictionary[origin].WayPoints.Contains(pos))
//                        {
//                            possibleWayPoints.Add(pos);
//                        }

//                        wayPoints.Add(pos);
                        
//                        //add positions behind it to impassable pos list
//                        //if possible waypoint is diagonal
//                        if (Mathf.Abs((pos - origin).x) == Mathf.Abs((pos - origin).y))
//                        {
//                            int incrementX = -((pos.x - origin.x) / Mathf.Abs(pos.x - origin.x));
//                            int incrementY = -((pos.y - origin.y) / Mathf.Abs(pos.y - origin.y));
//                            int startPointX = Mathf.Clamp(origin.x + (maxRad * ((pos.x - origin.x) / Mathf.Abs(pos.x - origin.x))), minX, maxX);
//                            int startPointY = Mathf.Clamp(origin.y + (maxRad * ((pos.y - origin.y) / Mathf.Abs(pos.y - origin.y))), minY, maxY);

//                            for (int x = startPointX; x != pos.x + incrementX; x += incrementX)
//                            {
//                                for (int y = startPointY; y != pos.y + incrementY; y += incrementY)
//                                {
//                                    vector2Int.Set(x, y);
//                                    if (!impassablePosList.Contains(vector2Int))
//                                    {
//                                        impassablePosList.Add(vector2Int);
//                                    }
//                                }
//                            }
//                        }
//                        //if possible waypoint is horizontal or vertical
//                        else if (pos.x == origin.x || pos.y == origin.y)
//                        {
//                            if (pos.y == origin.y) //if horizontal
//                            {
//                                int targetX = Mathf.Clamp(origin.x + (maxRad * ((pos.x - origin.x) / Mathf.Abs(pos.x - origin.x))), minX, maxX);
//                                int increment = (pos.x - origin.x) / Mathf.Abs(pos.x - origin.x);
//                                int _minY = pos.y;
//                                int _maxY = pos.y;

//                                for (int x = pos.x; x != targetX + increment; x += increment)
//                                {
//                                    for (int y = _minY; y <= _maxY; y++)
//                                    {
//                                        vector2Int.Set(x, y);
//                                        if (!impassablePosList.Contains(vector2Int))
//                                        {
//                                            impassablePosList.Add(vector2Int);
//                                        }
//                                    }

//                                    _minY = Mathf.Clamp(_minY - 1, minY, maxY);
//                                    _maxY = Mathf.Clamp(_maxY + 1, minY, maxY);
//                                }
//                            }
//                            else if (pos.x == origin.x) //if vertical
//                            {
//                                int targetY = Mathf.Clamp(origin.y + (maxRad * ((pos.y - origin.y) / Mathf.Abs(pos.y - origin.y))), minY, maxY);
//                                int increment = (pos.y - origin.y) / Mathf.Abs(pos.y - origin.y);
//                                int _minX = pos.x;
//                                int _maxX = pos.x;

//                                for (int y = pos.y; y != targetY + increment; y += increment)
//                                {
//                                    for (int x = _minX; x <= _maxX; x++)
//                                    {
//                                        vector2Int.Set(x, y);
//                                        if (!impassablePosList.Contains(vector2Int))
//                                        {
//                                            impassablePosList.Add(vector2Int);
//                                        }
//                                    }

//                                    _minX = Mathf.Clamp(_minX - 1, minX, maxX);
//                                    _maxX = Mathf.Clamp(_maxX + 1, minX, maxX);
//                                }
//                            }
//                        }
//                        else
//                        {
//                            if (Mathf.Abs(pos.x - origin.x) > Mathf.Abs(pos.y - origin.y))
//                            {
//                                int targetX = Mathf.Clamp(origin.x + (maxRad * ((pos.x - origin.x) / Mathf.Abs(pos.x - origin.x))), minX, maxX);
//                                int incrementX = (pos.x - origin.x) / Mathf.Abs(pos.x - origin.x);
//                                int incrementY = (pos.y - origin.y) / Mathf.Abs(pos.y - origin.y);
//                                int targetY = pos.y;

//                                for (int x = pos.x; x != targetX + incrementX; x += incrementX)
//                                {
//                                    for (int y = pos.y; y != targetY + incrementY; y += incrementY)
//                                    {
//                                        vector2Int.Set(x, y);
//                                        if (!impassablePosList.Contains(vector2Int))
//                                        {
//                                            impassablePosList.Add(vector2Int);
//                                        }
//                                    }

//                                    targetY = Mathf.Clamp(targetY + incrementY, minY, maxY);
//                                }
//                            }
//                            else
//                            {
//                                int targetY = Mathf.Clamp(origin.y + (maxRad * ((pos.y - origin.y) / Mathf.Abs(pos.y - origin.y))), minY, maxY);
//                                int incrementY = (pos.y - origin.y) / Mathf.Abs(pos.y - origin.y);
//                                int incrementX = (pos.x - origin.x) / Mathf.Abs(pos.x - origin.x);
//                                int targetX = pos.x;

//                                for (int y = pos.y; y != targetY + incrementY; y += incrementY)
//                                {
//                                    for (int x = pos.x; x != targetX + incrementX; x += incrementX)
//                                    {
//                                        vector2Int.Set(x, y);
//                                        if (!impassablePosList.Contains(vector2Int))
//                                        {
//                                            impassablePosList.Add(vector2Int);
//                                        }
//                                    }

//                                    targetX = Mathf.Clamp(targetX + incrementX, minX, maxX);
//                                }
//                            }
//                        }
//                    }
//                }

//                if (passablePosList.Count == 0)
//                {
//                    break;
//                }
//                else if (currRad == maxRad && wayPoints.Count == 0)
//                {
//                    maxRad++;
//                }

//            } while (currRad < maxRad);

//            //verify if the positions in possible waypoints intersects with other waypoinrts
//            for (int i = possibleWayPoints.Count - 1; i >= 0; i--)
//            {
//                //verify if the selected waypoint intersects with other waypoints
//                int targetX = 0;
//                int incrementX = 0;
//                int xMin = 0;

//                if (origin.x == possibleWayPoints[i].x)
//                {
//                    xMin = origin.x;
//                    if (origin.x < 0)
//                    {
//                        incrementX = -1;
//                        targetX = minX;
//                    }
//                    else
//                    {
//                        incrementX = 1;
//                        targetX = maxX;
//                    }
//                }
//                else if (Mathf.Abs(possibleWayPoints[i].x) < Mathf.Abs(origin.x))
//                {
//                    xMin = possibleWayPoints[i].x;
//                    if (possibleWayPoints[i].x < origin.x)
//                    {
//                        incrementX = 1;
//                        targetX = maxX;
//                    }
//                    else
//                    {
//                        incrementX = -1;
//                        targetX = minX;
//                    }
//                }
//                else
//                {
//                    xMin = origin.x;
//                    if (origin.x < possibleWayPoints[i].x)
//                    {
//                        incrementX = 1;
//                        targetX = maxX;
//                    }
//                    else
//                    {
//                        incrementX = -1;
//                        targetX = minX;
//                    }
//                }

//                bool intersectsToOtherWaypoint = false;
//                for (int x = xMin + incrementX; x != targetX + incrementX; x += incrementX)
//                {
//                    for (int y = minY; y <= maxY; y++)
//                    {
//                        vector2Int.Set(x, y);
//                        if (RoomDataDictionary.ContainsKey(vector2Int))
//                        {
//                            foreach (Vector2Int wayPoint in RoomDataDictionary[vector2Int].WayPoints)
//                            {
//                                if (origin != vector2Int && origin != wayPoint && possibleWayPoints[i] != vector2Int && possibleWayPoints[i] != wayPoint)
//                                {
//                                    if (DoLineSegmentsIntersect(origin, possibleWayPoints[i], vector2Int, wayPoint))
//                                    {
//                                        intersectsToOtherWaypoint = true;
//                                        break;
//                                    }
//                                }
//                            }

//                            if (intersectsToOtherWaypoint) break;
//                        }
//                    }

//                    if (intersectsToOtherWaypoint) break;
//                }
//                if (intersectsToOtherWaypoint) possibleWayPoints.RemoveAt(i);
//            }

//            if (possibleWayPoints.Count > 1)
//            {
//                List<Vector2Int> diagWP = new List<Vector2Int>();
//                List<Vector2Int> nonDiagWP = new List<Vector2Int>();

//                foreach(Vector2Int pos in possibleWayPoints)
//                {
//                    if (pos.x != origin.x && pos.y != origin.y) diagWP.Add(pos);
//                    else nonDiagWP.Add(pos);
//                }

//                if (diagWP.Count > 0 && nonDiagWP.Count > 0 && GameManager.GetTrueBoolByChance(DIAG_PATH_CHANCE)) possibleWayPoints = diagWP;
//            }

//            return possibleWayPoints;
//        }

//        //vector2IntList.Clear();
//        //vector2IntList.AddRange(RoomDataDictionary.Keys);
//        //vector2IntList.Remove(startingRoomPos);

//        ////Create room connections for every room for first time
//        //while (vector2IntList.Count > 0)
//        //{
//        //    int randomIndex = Random.Range(0, vector2IntList.Count);
//        //    List<Vector2Int> possibleWaypoints = GetPossibleWaypoints(vector2IntList[randomIndex]);
//        //    if (possibleWaypoints.Count > 0)
//        //    {
//        //        int randomIndex2 = Random.Range(0, possibleWaypoints.Count);
//        //        RoomDataDictionary[vector2IntList[randomIndex]].SetWayPoint(RoomDataDictionary[possibleWaypoints[randomIndex2]]);
//        //    }

//        //    vector2IntList.RemoveAt(randomIndex);
//        //}

//        bool connectedRoomsToStart = false;
//        bool willConnectRooms = true;
//        while (!connectedRoomsToStart)
//        {
//            //count number of rooms connected to starting room
//            List<RoomData> currRD = new List<RoomData>() { firstEnemyRoomData };
//            List<RoomData> lRD = new List<RoomData>(RoomDataDictionary.Values);
//            lRD.Remove(RoomDataDictionary[startingRoomPos]);
//            List<RoomData> nextRD = new List<RoomData>();
//            while (currRD.Count > 0)
//            {
//                foreach(RoomData rd in currRD)
//                {
//                    foreach (Vector2Int wp in rd.WayPoints) RoomDataDictionary[wp].isConnectedToStart = true;
//                    lRD.Remove(rd);
//                }
//                foreach (RoomData rd in currRD)
//                {
//                    List<Vector2Int> wpl = GetPossibleWaypoints(rd.Pos);
//                    for (int i = wpl.Count - 1; i >= 0; i--)
//                    {
//                        if (RoomDataDictionary[wpl[i]].isConnectedToStart) wpl.RemoveAt(i);
//                    }
//                    if (willConnectRooms)
//                    {
//                        int branchCount = Random.Range(2, 4);
//                        for (int i = 0; i < branchCount; i++)
//                        {
//                            if (wpl.Count > 0)
//                            {
//                                int randomIndex = Random.Range(0, wpl.Count);
//                                RoomData targetRD = RoomDataDictionary[wpl[randomIndex]];
//                                rd.SetWayPoint(targetRD);
//                                targetRD.isConnectedToStart = true;
//                                wpl.RemoveAt(randomIndex);
//                            }
//                        }
//                        willConnectRooms = false;
//                    }
//                    if (wpl.Count > 0)
//                    {
//                        RoomData targetRD = RoomDataDictionary[wpl[Random.Range(0, wpl.Count)]];
//                        rd.SetWayPoint(targetRD);
//                        targetRD.isConnectedToStart = true;
//                    }                   
//                }
//                foreach (RoomData rd in currRD)
//                {
//                    foreach (Vector2Int wp in rd.WayPoints)
//                    {
//                        RoomData _rd = RoomDataDictionary[wp];
//                        if (lRD.Contains(_rd) && !nextRD.Contains(_rd))
//                        {
//                            nextRD.Add(_rd);
//                        }
//                    }
//                }

//                if (nextRD.Count == 0)
//                {
//                    List<RoomData> unconnected = new List<RoomData>();
//                    foreach (RoomData rd in RoomDataDictionary.Values) if (!rd.isConnectedToStart) unconnected.Add(rd);

//                    bool hasConnected = false;
//                    bool connectToAny = false;
//                    while (unconnected.Count > 0)
//                    {
//                        hasConnected = false;
//                        for (int i = unconnected.Count - 1; i >= 0; i--)
//                        {
//                            List<Vector2Int> wpl = GetPossibleWaypoints(unconnected[i].Pos);
//                            for (int j = wpl.Count - 1; j >= 0; j--)
//                            {
//                                if (!connectToAny && !RoomDataDictionary[wpl[j]].isConnectedToStart) wpl.RemoveAt(j);
//                            }
//                            if (wpl.Count > 0)
//                            {
//                                unconnected[i].SetWayPoint(RoomDataDictionary[wpl[Random.Range(0, wpl.Count)]]);
//                                unconnected[i].isConnectedToStart = true;
//                                hasConnected = true;
//                                unconnected.RemoveAt(i);
//                            }
//                        }

//                        if (!hasConnected) connectToAny = true;
//                    }

//                    connectedRoomsToStart = true;
//                    break;
//                }

//                currRD.Clear();
//                currRD.AddRange(nextRD);
//                nextRD.Clear();
//            }    
//        }

//        //Make connections again from rooms with many connections
//        List<RoomData> rdl = new List<RoomData>();
//        int pathCount = Mathf.RoundToInt(RoomDataDictionary.Count * ROOM_PRCTG_BONUS_PATH);
//        rdl.AddRange(RoomDataDictionary.Values);
//        rdl.Remove(RoomDataDictionary[startingRoomPos]);
//        rdl.Remove(firstEnemyRoomData);

//        for (int i = 0; i < pathCount; i++)
//        {
//            int randomIndex = Random.Range(0, rdl.Count);
//            List<Vector2Int> wpl = GetPossibleWaypoints(rdl[randomIndex].Pos);
//            if (wpl.Count > 0) rdl[randomIndex].SetWayPoint(RoomDataDictionary[wpl[Random.Range(0, wpl.Count)]]);
//            rdl.RemoveAt(randomIndex);
//        }

//        ////Create another room connections making sure that all rooms are connected to the starting room
//        //List<Vector2Int> posConnectedToStartList = new List<Vector2Int>();
//        //posConnectedToStartList.Add(randomSideRoomPos);

//        //List<Vector2Int> posNotConnectedToStartList = new List<Vector2Int>();
//        //posNotConnectedToStartList.AddRange(RoomDataDictionary.Keys);
//        //posNotConnectedToStartList.Remove(randomSideRoomPos);

//        //List<Vector2Int> vector2IntList2 = new List<Vector2Int>();
//        //do
//        //{
//        //    //vector2IntList2 - temporarily stores pos not connected to start

//        //    void UpdatePositionsConnectedToStartList()
//        //    {
//        //        while (true)
//        //        {
//        //            int prevPosConnectedToStartCount = posConnectedToStartList.Count;

//        //            for (int i = posConnectedToStartList.Count - 1; i >= 0; i--)
//        //            {
//        //                foreach (Vector2Int wayPoint in RoomDataDictionary[posConnectedToStartList[i]].WayPoints)
//        //                {
//        //                    if (!posConnectedToStartList.Contains(wayPoint))
//        //                    {
//        //                        posConnectedToStartList.Add(wayPoint);
//        //                        posNotConnectedToStartList.Remove(wayPoint);
//        //                    }
//        //                }
//        //            }

//        //            if (prevPosConnectedToStartCount == posConnectedToStartList.Count)
//        //            {
//        //                break;
//        //            }
//        //        }
//        //    }

//        //    //update room connected to start list:
//        //    UpdatePositionsConnectedToStartList();

//        //    //connect rooms not connected to start room
//        //    int prevNotConnectedRoomCount = posNotConnectedToStartList.Count;
//        //    int noWaypointCount = 0;
//        //    int hadWaypointCount = 0;
//        //    vector2IntList2.Clear();
//        //    vector2IntList2.AddRange(posNotConnectedToStartList);
//        //    for (int i = vector2IntList2.Count - 1; i >= 0; i--)
//        //    {
//        //        int randomIndex = Random.Range(0, vector2IntList2.Count);
//        //        Vector2Int notConnectedPos = vector2IntList2[randomIndex];
//        //        List<Vector2Int> possibleWaypoints = GetPossibleWaypoints(notConnectedPos);

//        //        if (possibleWaypoints.Count > 0) hadWaypointCount++;

//        //        //remove pos from possiblewaypoints that are not connected to start
//        //        for (int j = possibleWaypoints.Count - 1; j >= 0; j--)
//        //        {
//        //            if (!posConnectedToStartList.Contains(possibleWaypoints[j]))
//        //            {
//        //                possibleWaypoints.RemoveAt(j);
//        //            }
//        //        }

//        //        //Connect room to a random waypoint
//        //        if (possibleWaypoints.Count > 0)
//        //        {
//        //            int randomIndex2 = Random.Range(0, possibleWaypoints.Count);
//        //            RoomDataDictionary[notConnectedPos].SetWayPoint(RoomDataDictionary[possibleWaypoints[randomIndex2]]);
//        //            posConnectedToStartList.Add(notConnectedPos);
//        //            posNotConnectedToStartList.Remove(notConnectedPos);

//        //            UpdatePositionsConnectedToStartList();
//        //        }
//        //        else
//        //        {
//        //            noWaypointCount++;
//        //        }

//        //        vector2IntList2.RemoveAt(randomIndex);
//        //    }

//        //    if (prevNotConnectedRoomCount == posNotConnectedToStartList.Count)
//        //    {
//        //        Debug.Log("Not connected rooms count: " + posNotConnectedToStartList.Count + "\nNo waypoint count: " + noWaypointCount + "\nHad waypoint count: " + hadWaypointCount);
//        //        break;
//        //    }

//        //} while (posNotConnectedToStartList.Count > 0);

//        List<Vector2Int> GetEmptyRoomPosList()
//        {
//            List<Vector2Int> emptyRoomPosList = new List<Vector2Int>();

//            foreach (Vector2Int pos in RoomDataDictionary.Keys)
//            {
//                if (RoomDataDictionary[pos] is EmptyRoomData)
//                {
//                    emptyRoomPosList.Add(pos);
//                }
//            }

//            return emptyRoomPosList;
//        }

//        //Calculate travel distances of rooms from the starting point
//        Dictionary<Vector2Int, int> roomDistanceDict = new Dictionary<Vector2Int, int>();
//        roomDistanceDict.Add(startingRoomPos, 0);

//        List<Vector2Int> posList = new List<Vector2Int>(RoomDataDictionary.Keys);

//        List<Vector2Int> currPosList = new List<Vector2Int>() { startingRoomPos };
//        List<Vector2Int> nextPosList = new List<Vector2Int>();

//        while (posList.Count > 0)
//        {
//            foreach (Vector2Int pos in currPosList) posList.Remove(pos);
//            foreach (Vector2Int pos in currPosList)
//            {
//                foreach (Vector2Int nextPos in RoomDataDictionary[pos].WayPoints)
//                {
//                    if (!roomDistanceDict.ContainsKey(nextPos))
//                    {
//                        int? lowestPrevDist = null;
//                        foreach (Vector2Int prevPos in RoomDataDictionary[nextPos].WayPoints)
//                        {
//                            if (roomDistanceDict.ContainsKey(prevPos))
//                            {
//                                if (lowestPrevDist == null) lowestPrevDist = roomDistanceDict[prevPos];
//                                else if (roomDistanceDict[prevPos] < lowestPrevDist) lowestPrevDist = roomDistanceDict[prevPos];
//                            }
//                        }

//                        nextPosList.Add(nextPos);
//                        roomDistanceDict.Add(nextPos, (int)++lowestPrevDist);
//                    }
//                }
//            }

//            currPosList.Clear();
//            currPosList.AddRange(nextPosList);
//            nextPosList.Clear();
//        }

//        Debug.Log("Final boss room distance from start: " + roomDistanceDict[Vector2Int.zero]);

//        //ADD KEY ROOMS
//        int keyRoomDistanceFromStart = ROOM_DISTANCE_FROM_START_KEY;
//        int keyRoomDistanceFromEachOther = ROOM_DISTANCE_FROM_EACH_OTHER_KEY;

//        List<Vector2Int> availablePositions = new List<Vector2Int>();
//        List<Vector2Int> keyRoomPositions = new List<Vector2Int>();
//        List<RoomData> keyRooms = new List<RoomData>();

//        for (int i = 0; i < 3; i++)
//        {
//            //get available positions (empty rooms) if has none
//            if (availablePositions.Count == 0)
//            {
//                while (true)
//                {
//                    availablePositions.AddRange(GetEmptyRoomPosList());

//                    //remove rooms near start
//                    for (int j = availablePositions.Count - 1; j >= 0; j--)
//                    {
//                        if (GetDistanceBetweenTwoPoints(startingRoomPos, availablePositions[j]) <= keyRoomDistanceFromStart)
//                        {
//                            availablePositions.RemoveAt(j);
//                        }
//                    }

//                    //remove rooms nearby key rooms
//                    foreach (Vector2Int keyRoomPosition in keyRoomPositions)
//                    {
//                        for (int j = availablePositions.Count - 1; j >= 0; j--)
//                        {
//                            if (GetDistanceBetweenTwoPoints(keyRoomPosition, availablePositions[j]) <= keyRoomDistanceFromEachOther)
//                            {
//                                availablePositions.RemoveAt(j);
//                            }
//                        }
//                    }

//                    //if there's no available pos left, reduce distances and try again
//                    if (keyRoomDistanceFromStart == 0 && keyRoomDistanceFromEachOther == 0 && availablePositions.Count == 0)
//                    {
//                        Debug.LogError("No available positions found for key room data.");
//                        break;
//                    }
//                    else if (availablePositions.Count == 0)
//                    {
//                        if (keyRoomDistanceFromEachOther >= keyRoomDistanceFromStart) keyRoomDistanceFromEachOther--;
//                        else keyRoomDistanceFromStart--;
//                    }
//                    else
//                    {
//                        break;
//                    }
//                }
//            }

//            if (availablePositions.Count == 0) break;

//            //select pos from available positions and assign key room data
//            int randomIndex = Random.Range(0, availablePositions.Count);
//            Vector2Int keyRoomPos = availablePositions[randomIndex];
//            keyRoomPositions.Add(keyRoomPos);
//            availablePositions.RemoveAt(randomIndex);
//            KeyRoomData keyRoomData = new KeyRoomData(keyRoomPos);
//            keyRoomData.WayPoints.AddRange(RoomDataDictionary[keyRoomPos].WayPoints);
//            RoomDataDictionary[keyRoomPos] = keyRoomData;
//            keyRooms.Add(keyRoomData);

//            for (int j = availablePositions.Count - 1; j >= 0; j--)
//            {
//                if (GetDistanceBetweenTwoPoints(keyRoomPos, availablePositions[j]) <= keyRoomDistanceFromEachOther) availablePositions.RemoveAt(j);
//            }
//        }

//        //add treasure rooms
//        int treasureRoomDistanceFromEachOther = ROOM_DISTANCE_FROM_EACH_OTHER_TREASURE;
//        int treasureRoomDistanceFromStart = ROOM_DISTANCE_FROM_START_TREASURE;
//        int treasureRoomCount = Mathf.RoundToInt(ROOM_PERCENTAGE_TREASURE * RoomDataDictionary.Count);
//        availablePositions.Clear();
//        List<Vector2Int> treasureRoomPositions = new List<Vector2Int>();

//        do
//        {
//            //GET AVAILABLE POSITIONS:
//            if (availablePositions.Count == 0)
//            {
//                while (true)
//                {
//                    availablePositions.AddRange(GetEmptyRoomPosList());

//                    //remove empty rooms near start
//                    for (int i = availablePositions.Count - 1; i >= 0; i--)
//                    {
//                        if (GetDistanceBetweenTwoPoints(startingRoomPos, availablePositions[i]) <= treasureRoomDistanceFromStart)
//                        {
//                            availablePositions.RemoveAt(i);
//                        }
//                    }

//                    //remove empty rooms nearby existing treasure rooms
//                    foreach (Vector2Int treasureRoomPosition in treasureRoomPositions)
//                    {
//                        for (int i = availablePositions.Count - 1; i >= 0; i--)
//                        {
//                            if (GetDistanceBetweenTwoPoints(treasureRoomPosition, availablePositions[i]) <= treasureRoomDistanceFromEachOther)
//                            {
//                                availablePositions.RemoveAt(i);
//                            }
//                        }
//                    }

//                    //if there's no available pos left, reduce distances and try again
//                    if (treasureRoomDistanceFromStart == 0 && treasureRoomDistanceFromEachOther == 0 && availablePositions.Count == 0)
//                    {
//                        Debug.LogError("No available positions found for treasure room data.");
//                        break;
//                    }
//                    else if (availablePositions.Count == 0)
//                    {
//                        if (treasureRoomDistanceFromEachOther >= treasureRoomDistanceFromStart) treasureRoomDistanceFromEachOther--;
//                        else treasureRoomDistanceFromStart--;
//                    }
//                    else
//                    {
//                        break;
//                    }
//                }
//            }

//            if (availablePositions.Count == 0) break;

//            int selectedIndex = Random.Range(0, availablePositions.Count);
//            Vector2Int treasureRoomPos = availablePositions[selectedIndex];
//            treasureRoomPositions.Add(treasureRoomPos);
//            TreasureRoomData treasureRoomData = new TreasureRoomData(treasureRoomPos);
//            treasureRoomData.WayPoints.AddRange(RoomDataDictionary[treasureRoomPos].WayPoints);
//            RoomDataDictionary[treasureRoomPos] = treasureRoomData;
//            availablePositions.RemoveAt(selectedIndex);

//            for (int i = availablePositions.Count - 1; i >= 0; i--)
//            {
//                if (GetDistanceBetweenTwoPoints(treasureRoomPos, availablePositions[i]) <= treasureRoomDistanceFromEachOther) availablePositions.RemoveAt(i);
//            }

//        } while (treasureRoomPositions.Count < treasureRoomCount);

//        //add strong enemy rooms
//        int strongEnemyRoomDistanceFromEachOther = ROOM_DISTANCE_FROM_EACH_OTHER_STRONG_ENEMY;
//        int strongEnemyRoomDistanceFromStart = ROOM_DISTANCE_FROM_START_STRONG_ENEMY;
//        int strongEnemyRoomCount = Mathf.RoundToInt(ROOM_PERCENTAGE_STRONG_ENEMY * RoomDataDictionary.Count);
//        availablePositions.Clear();
//        List<Vector2Int> strongEnemyRoomPositions = new List<Vector2Int>();

//        while (strongEnemyRoomPositions.Count < strongEnemyRoomCount)
//        {
//            if (availablePositions.Count == 0)
//            {
//                while (true)
//                {
//                    availablePositions.AddRange(GetEmptyRoomPosList());

//                    for (int i = availablePositions.Count - 1; i >= 0; i--)
//                    {
//                        if (GetDistanceBetweenTwoPoints(startingRoomPos, availablePositions[i]) <= strongEnemyRoomDistanceFromStart)
//                        {
//                            availablePositions.RemoveAt(i);
//                        }
//                    }

//                    foreach (Vector2Int strongEnemyRoomPosition in strongEnemyRoomPositions)
//                    {
//                        for (int i = availablePositions.Count - 1; i >= 0; i--)
//                        {
//                            if (GetDistanceBetweenTwoPoints(strongEnemyRoomPosition, availablePositions[i]) <= strongEnemyRoomDistanceFromEachOther)
//                            {
//                                availablePositions.RemoveAt(i);
//                            }
//                        }
//                    }

//                    //if there's no available pos left, reduce distances and try again
//                    if (strongEnemyRoomDistanceFromStart == 0 && strongEnemyRoomDistanceFromEachOther == 0 && availablePositions.Count == 0)
//                    {
//                        Debug.LogError("No available positions found for strong enemy room data.");
//                        break;
//                    }
//                    else if (availablePositions.Count == 0)
//                    {
//                        if (strongEnemyRoomDistanceFromEachOther >= strongEnemyRoomDistanceFromStart) strongEnemyRoomDistanceFromEachOther--;
//                        else strongEnemyRoomDistanceFromStart--;
//                    }
//                    else
//                    {
//                        break;
//                    }
//                }
//            }

//            if (availablePositions.Count == 0) break;

//            int selectedIndex = Random.Range(0, availablePositions.Count);
//            Vector2Int strongEnemyRoomPos = availablePositions[selectedIndex];
//            strongEnemyRoomPositions.Add(strongEnemyRoomPos);
//            StrongEnemyRoomData strongEnemyRoomData = new StrongEnemyRoomData(strongEnemyRoomPos);
//            strongEnemyRoomData.WayPoints.AddRange(RoomDataDictionary[strongEnemyRoomPos].WayPoints);
//            RoomDataDictionary[strongEnemyRoomPos] = strongEnemyRoomData;
//            availablePositions.RemoveAt(selectedIndex);

//            for (int i = availablePositions.Count - 1; i >= 0; i--)
//            {
//                if (GetDistanceBetweenTwoPoints(strongEnemyRoomPos, availablePositions[i]) <= strongEnemyRoomDistanceFromEachOther) availablePositions.RemoveAt(i);
//            }
//        }

//        //Add rest rooms
//        int restRoomGap = GAP_REST_ROOM;
//        int restRoomGapFromStart = GAP_FROM_START_REST_ROOM;
//        int restRoomCount = Mathf.RoundToInt(PERCENTAGE_REST_ROOM * RoomDataDictionary.Count);
//        availablePositions.Clear();
//        List<Vector2Int> restRoomPositions = new List<Vector2Int>();

//        while (restRoomPositions.Count < restRoomCount)
//        {
//            if (availablePositions.Count == 0)
//            {
//                while (true)
//                {
//                    availablePositions.AddRange(GetEmptyRoomPosList());

//                    for (int i = availablePositions.Count - 1; i >= 0; i--)
//                    {
//                        if (GetDistanceBetweenTwoPoints(startingRoomPos, availablePositions[i]) <= restRoomGapFromStart)
//                        {
//                            availablePositions.RemoveAt(i);
//                        }
//                    }

//                    foreach (Vector2Int strongEnemyRoomPosition in restRoomPositions)
//                    {
//                        for (int i = availablePositions.Count - 1; i >= 0; i--)
//                        {
//                            if (GetDistanceBetweenTwoPoints(strongEnemyRoomPosition, availablePositions[i]) <= restRoomGap)
//                            {
//                                availablePositions.RemoveAt(i);
//                            }
//                        }
//                    }

//                    //if there's no available pos left, reduce distances and try again
//                    if (restRoomGapFromStart == 0 && restRoomGap == 0 && availablePositions.Count == 0)
//                    {
//                        Debug.LogError("No available positions found for strong enemy room data.");
//                        break;
//                    }
//                    else if (availablePositions.Count == 0)
//                    {
//                        if (restRoomGap >= restRoomGapFromStart) restRoomGap--;
//                        else restRoomGapFromStart--;
//                    }
//                    else
//                    {
//                        break;
//                    }
//                }
//            }

//            if (availablePositions.Count == 0) break;

//            int selectedIndex = Random.Range(0, availablePositions.Count);
//            Vector2Int restRoomPos = availablePositions[selectedIndex];
//            restRoomPositions.Add(restRoomPos);
//            RestRoomData restRoomData = new RestRoomData(restRoomPos);
//            restRoomData.WayPoints.AddRange(RoomDataDictionary[restRoomPos].WayPoints);
//            RoomDataDictionary[restRoomPos] = restRoomData;
//            availablePositions.RemoveAt(selectedIndex);

//            for (int i = availablePositions.Count - 1; i >= 0; i--)
//            {
//                if (GetDistanceBetweenTwoPoints(restRoomPos, availablePositions[i]) <= restRoomGap) availablePositions.RemoveAt(i);
//            }
//        }

//        //add event rooms
//        int eventRoomDistanceFromEachOther = ROOM_DISTANCE_FROM_EACH_OTHER_EVENT;
//        int eventRoomCount = Mathf.RoundToInt(ROOM_PERCENTAGE_EVENT * RoomDataDictionary.Count);
//        List<Vector2Int> eventRoomPositions = new List<Vector2Int>();
//        availablePositions.Clear();

//        while (eventRoomPositions.Count < eventRoomCount)
//        {
//            if (availablePositions.Count == 0)
//            {
//                while (true)
//                {
//                    availablePositions.AddRange(GetEmptyRoomPosList());

//                    foreach (Vector2Int eventRoomPosition in eventRoomPositions)
//                    {
//                        for (int i = availablePositions.Count - 1; i >= 0; i--)
//                        {
//                            if (GetDistanceBetweenTwoPoints(eventRoomPosition, availablePositions[i]) <= eventRoomDistanceFromEachOther)
//                            {
//                                availablePositions.RemoveAt(i);
//                            }
//                        }
//                    }

//                    //if there's no available pos left, reduce distances and try again
//                    if (eventRoomDistanceFromEachOther == 0 && availablePositions.Count == 0)
//                    {
//                        Debug.LogError("No available positions found for strong event room data.");
//                        break;
//                    }
//                    else if (availablePositions.Count == 0)
//                    {
//                        eventRoomDistanceFromEachOther--;
//                    }
//                    else
//                    {
//                        break;
//                    }
//                }
//            }

//            if (availablePositions.Count == 0) break;

//            int selectedIndex = Random.Range(0, availablePositions.Count);
//            Vector2Int eventRoomPos = availablePositions[selectedIndex];
//            eventRoomPositions.Add(eventRoomPos);
//            availablePositions.RemoveAt(selectedIndex);

//            for (int i = availablePositions.Count - 1; i >= 0; i--)
//            {
//                if (GetDistanceBetweenTwoPoints(eventRoomPos, availablePositions[i]) <= eventRoomDistanceFromEachOther) availablePositions.RemoveAt(i);
//            }
//        }

//        //sort eventRoomPosList by distance from start from shortest to longest
//        bool swapped = true;
//        while (swapped)
//        {
//            swapped = false;

//            for (int i = 0; i < eventRoomPositions.Count - 1; i++)
//            {
//                if (roomDistanceDict[eventRoomPositions[i + 1]] < roomDistanceDict[eventRoomPositions[i]])
//                {
//                    Vector2Int posTemp = eventRoomPositions[i + 1];
//                    eventRoomPositions[i + 1] = eventRoomPositions[i];
//                    eventRoomPositions[i] = posTemp;
//                    swapped = true;
//                }
//            }
//        }

//        eventRoomCount = eventRoomPositions.Count;
//        int s1EventCount = Mathf.RoundToInt(eventRoomCount / 3.0f);
//        int s2EventCount = Mathf.RoundToInt(eventRoomCount * 0.66f) - s1EventCount;
//        int s3EventCount = eventRoomCount - (s1EventCount + s2EventCount);

//        List<EventData> s1Events = new List<EventData>();
//        List<EventData> s2Events = new List<EventData>();
//        List<EventData> s3Events = new List<EventData>();

//        EventData eventDataTemp = null;
//        int index = 0;
//        while (true)
//        {
//            eventDataTemp = DataLibrary.GetEventData(index);
//            if (eventDataTemp != null)
//            {
//                if (eventDataTemp.encounterStages.Contains(GS_FIRST)) s1Events.Add(eventDataTemp);
//                if (eventDataTemp.encounterStages.Contains(GS_SECOND)) s2Events.Add(eventDataTemp);
//                if (eventDataTemp.encounterStages.Contains(GS_LAST)) s3Events.Add(eventDataTemp);
//            }
//            else break;
//            index++;
//        }

//        for (int i = 0; i < eventRoomPositions.Count; i++)
//        {
//            if (i > s1EventCount) eventDataTemp = s1Events[Random.Range(0, s1Events.Count)];
//            else if (i > s1EventCount + s2EventCount) eventDataTemp = s2Events[Random.Range(0, s2Events.Count)];
//            else eventDataTemp = s3Events[Random.Range(0, s3Events.Count)];

//            Vector2Int eventRoomPos = eventRoomPositions[i];
//            EventRoomData eventRoomData = new EventRoomData(eventRoomPos, eventDataTemp);
//            eventRoomData.WayPoints.AddRange(RoomDataDictionary[eventRoomPos].WayPoints);
//            RoomDataDictionary[eventRoomPos] = eventRoomData;

//            if (eventDataTemp.OneTimeEncounter)
//            {
//                if (s1Events.Contains(eventDataTemp)) s1Events.Remove(eventDataTemp);
//                if (s2Events.Contains(eventDataTemp)) s2Events.Remove(eventDataTemp);
//                if (s3Events.Contains(eventDataTemp)) s3Events.Remove(eventDataTemp);
//            }
//        }

//        //fill all empty rooms with enemy room data
//        foreach (Vector2Int pos in GetEmptyRoomPosList())
//        {
//            EnemyRoomData enemyRoomData = new EnemyRoomData(pos);
//            enemyRoomData.WayPoints.AddRange(RoomDataDictionary[pos].WayPoints);
//            RoomDataDictionary[pos] = enemyRoomData;
//        }

//        Dictionary<int, List<RoomData>> rooms = new Dictionary<int, List<RoomData>>();
//        List<RoomData> l = new List<RoomData>(RoomDataDictionary.Values);
//        List<RoomData> currL = new List<RoomData>() { RoomDataDictionary[startingRoomPos] };
//        int dist = 1;

//        List<RoomData> nearestBossRooms = new List<RoomData>();
//        int nearBossDist = 0;

//        while (l.Count != 0)
//        {
//            List<RoomData> nextR = new List<RoomData>();
//            foreach (RoomData r in currL) l.Remove(r);
//            foreach (RoomData r in currL)
//            {
//                foreach(Vector2Int rdPos in r.WayPoints)
//                {
//                    RoomData rd = RoomDataDictionary[rdPos];
//                    if (l.Contains(rd) && !nextR.Contains(rd))
//                    {
//                        if (rd.prevRoom == null) rd.prevRoom = r;
//                        nextR.Add(rd);
//                        if (rd is KeyRoomData)
//                        {
//                            if (nearestBossRooms.Count == 0 || dist == nearBossDist)
//                            {
//                                nearestBossRooms.Add(rd);
//                                nearBossDist = dist;
//                            }
//                        }
//                    }
//                }
//            }
//            if (!rooms.ContainsKey(dist)) rooms.Add(dist, nextR);

//            currL.Clear();
//            currL.AddRange(nextR);
//            Debug.Log(dist);
//            dist++;
//        }

//        //Reveal pathways towards first boss
//        RoomData nearBossRoom = null;
//        if (nearestBossRooms.Count > 1) nearBossRoom = nearestBossRooms[Random.Range(0, nearestBossRooms.Count)];
//        else nearBossRoom = nearestBossRooms[0];
//        int distBossFirstEn = GetDistanceBetweenTwoPoints(nearBossRoom.Pos, firstEnemyRoomData.Pos);

//        List<RoomData> pathToBoss = new List<RoomData>();
//        RoomData prevRD = nearBossRoom;

//        while (prevRD != null)
//        {
//            pathToBoss.Add(prevRD.prevRoom);
//            if (prevRD.prevRoom == null) break;
//            else prevRD = prevRD.prevRoom;
//        }

//        List<RoomData> farKeyRooms = new List<RoomData>(keyRooms);
//        farKeyRooms.Remove(nearBossRoom);

//        for (int i = 1; i < nearBossDist; i++)
//        {
//            for (int j = rooms[i].Count - 1; j >= 0; j--)
//            {
//                if (!pathToBoss.Contains(rooms[i][j]))
//                {
//                    //if (GetDistanceBetweenTwoPoints(rooms[i][j].Pos, nearBossRoom.Pos) > distBossFirstEn) rooms[i].RemoveAt(j);
//                    //else
//                    //{
//                        foreach (RoomData rd in farKeyRooms) if (GetDistanceBetweenTwoPoints(rooms[i][j].Pos, rd.Pos) <= FAR_KEY_ROOM_FOG_DIST)
//                        {
//                            rooms[i].RemoveAt(j);
//                            break;
//                        }
//                    //}
//                } 
//            }

//            revealedRooms.AddRange(rooms[i]);
//        }

//        l.Clear();
//        l.AddRange(revealedRooms);
//        foreach (RoomData rd in pathToBoss) if (!revealedRooms.Contains(rd)) revealedRooms.Add(rd);
//        foreach (RoomData rd in l)
//        {
//            int revdWayPointsCount = 0;
//            foreach (Vector2Int pos in rd.WayPoints)
//            {
//                if (revealedRooms.Contains(RoomDataDictionary[pos])) revdWayPointsCount++;
//            }
//            if (!(rd is KeyRoomData) && revdWayPointsCount < 1 && !pathToBoss.Contains(rd)) revealedRooms.Remove(rd);
//        }

//        revealedRooms.Add(nearBossRoom);
//    }

//    public static int GetDistanceBetweenTwoPoints(Vector2Int pointA, Vector2Int pointB)
//    {
//        return Mathf.Abs(pointA.x - pointB.x) + Mathf.Abs(pointA.y - pointB.y);
//    }
//}

public class WorldData
{
    public enum GameStage { None, First, Second, Last };
    public const GameStage GS_FIRST = GameStage.First;
    public const GameStage GS_SECOND = GameStage.Second;
    public const GameStage GS_LAST = GameStage.Last;

    public class Stage
    {
        public readonly RoomData[][] levels; //1st index is its order, 2nd is its choices from top to bottom
        public int roomCount;

        public Stage(int length)
        {
            levels = new RoomData[length][];
        }
    }

    public const int DEFAULT_STG_LT = 14;
    bool WILL_SPAWN_ENEMY_EVENTS = true; //for testing purposes

    const int STAGES_COUNT = 3;
    const int MAX_CHOICES_PER_LEVEL = 3;
    const int SINGLE_CHOICE_STR_PTH_LIMIT = 2;
    const float STRAIGHT_PATH_CHANCE = 35f;
    const int STRT_PATH_LIMIT = 4;

    const int ROOMS_PER_ELITE = 11;
    const int ELITE_GAP = 1;
    const int ELITE_GAP_FROM_ST = 2;
    const int ELITE_GAP_FROM_FN = 1;

    const int ROOMS_PER_CHEST = 10;
    const int CHEST_GAP = 1;
    const int CHEST_GAP_FROM_ST = 3;
    const int CHEST_GAP_FROM_FN = 0;
    const float BONUS_CHEST_CHANCE = 10f;

    const int ROOMS_PER_REST = 11;
    const int REST_GAP = 3;
    const int REST_GAP_FROM_ST = 5;

    const int ROOMS_PER_SHOP = 11;
    const int SHOP_GAP = 4;
    const int SHOP_GAP_FROM_ST = 3;

    const float EVENTS_RATIO = 0.35f; //to enemy rooms

    public readonly Stage[] stages;
    readonly static List<float> choiceCountChances = new List<float>() { 15f, 45f, 40f }; //1st element is chance of having 1 choice in a level, 2nd element for 2 choices and so on...
    readonly static List<float> straightPathCountChances = new List<float>() { 35f, 35f, 20f, 10f };
    public readonly int stgL;

    public WorldData(int seed, int stageLength)
    {
        stgL = stageLength;

        //create nodes of empty rooms
        List<int> ints = new List<int>();
        for (int i = 1; i <= choiceCountChances.Count; i++) ints.Add(i);


        List<int> strgtPathLts = new List<int>();
        for (int i = 1; i <= straightPathCountChances.Count; i++) strgtPathLts.Add(i);
        List<RoomData> curr = new List<RoomData>();
        List<RoomData> nxt = new List<RoomData>();

        bool OnSegment(Vector2Int p, Vector2Int q, Vector2Int r)
        {
            if (q.x <= Mathf.Max(p.x, r.x) && q.x >= Mathf.Min(p.x, r.x) &&
                q.y <= Mathf.Max(p.y, r.y) && q.y >= Mathf.Min(p.y, r.y))
                return true;

            return false;
        }

        int GetOrientation(Vector2Int p, Vector2Int q, Vector2Int r)
        {
            int val = (q.y - p.y) * (r.x - q.x) -
                    (q.x - p.x) * (r.y - q.y);

            if (val == 0) return 0; // colinear

            return (val > 0) ? 1 : 2; // clock or counterclock wise
        }

        bool DoLineSegmentsIntersect(Vector2Int p1, Vector2Int q1, Vector2Int p2, Vector2Int q2)
        {
            int o1 = GetOrientation(p1, q1, p2);
            int o2 = GetOrientation(p1, q1, q2);
            int o3 = GetOrientation(p2, q2, p1);
            int o4 = GetOrientation(p2, q2, q1);

            // General case
            if (o1 != o2 && o3 != o4)
                return true;

            // Special Cases
            // p1, q1 and p2 are colinear and p2 lies on segment p1q1
            if (o1 == 0 && OnSegment(p1, p2, q1)) return true;

            // p1, q1 and q2 are colinear and q2 lies on segment p1q1
            if (o2 == 0 && OnSegment(p1, q2, q1)) return true;

            // p2, q2 and p1 are colinear and p1 lies on segment p2q2
            if (o3 == 0 && OnSegment(p2, p1, q2)) return true;

            // p2, q2 and q1 are colinear and q1 lies on segment p2q2
            if (o4 == 0 && OnSegment(p2, q1, q2)) return true;

            return false; // Doesn't fall in any of the above cases
        }

        void ConnectNodes(List<RoomData> a, List<RoomData> b, bool areParallel)
        {         
            if (areParallel)
            {
                if (a.Count != b.Count)
                {
                    Debug.LogError("vvv");
                    return;
                }
                for (int i = 0; i < a.Count; i++) a[i].SetWayPoint(b[i]);
            }
            else
            {
                if (a.Count == 1)
                {
                    foreach (RoomData rd in b) a[0].SetWayPoint(rd);
                }
                else if (b.Count == 1)
                {
                    foreach (RoomData rd in a) rd.SetWayPoint(b[0]);
                }
                else
                {
                    List<KeyValuePair<Vector2Int, Vector2Int>> lsl = new List<KeyValuePair<Vector2Int, Vector2Int>>();
                    List<RoomData> wp = new List<RoomData>();
                    int aos = Mathf.RoundToInt(((a.Count / -2f) + 0.5f) * 100);
                    int bos = Mathf.RoundToInt(((a.Count / -2f) + 0.5f) * 100);

                    foreach (RoomData rd in a)
                    {
                        if (rd.levelIndex == 0) a[0].SetWayPoint(b[0]);
                        else if (rd.levelIndex == a.Count - 1) a[a.Count - 1].SetWayPoint(b[b.Count - 1]);
                        else
                        {
                            //get possible waypoints
                            wp.Clear();
                            wp.AddRange(b);
                            Vector2Int pos = new Vector2Int(100, (rd.levelIndex * 100) + aos);
                            foreach (RoomData brd in b)
                            {
                                Vector2Int bpos = new Vector2Int(200, (brd.levelIndex * 100) + bos);

                                if (Mathf.Abs(bpos.y) - Mathf.Abs(pos.y) > 100)
                                {
                                    wp.Remove(brd);
                                }
                                else
                                {
                                    foreach (KeyValuePair<Vector2Int, Vector2Int> ls in lsl)
                                    {
                                        if (DoLineSegmentsIntersect(pos, bpos, ls.Key, ls.Value))
                                        {
                                            wp.Remove(brd);
                                            break;
                                        }
                                    }
                                }
                            }

                            if (wp.Count > 0)
                            {
                                int r = Random.Range(0, wp.Count);
                                RoomData _rd = wp[r];
                                rd.SetWayPoint(_rd);
                                lsl.Add(new KeyValuePair<Vector2Int, Vector2Int>(pos, new Vector2Int(200, (_rd.levelIndex * 100) + bos)));
                            }
                        }
                    }

                    List<RoomData> uc = new List<RoomData>();
                    foreach (RoomData rd in b) if (rd.PrevWayPoints.Count == 0) uc.Add(rd);

                    if (uc.Count > 0)
                    {
                        foreach (RoomData rd in uc)
                        {
                            wp.Clear();
                            wp.AddRange(a);
                            Vector2Int pos = new Vector2Int(200, (rd.levelIndex * 100) + bos);
                            foreach (RoomData ard in a)
                            {
                                Vector2Int apos = new Vector2Int(100, (ard.levelIndex * 100) + aos);
                                if (Mathf.Abs(apos.y) - Mathf.Abs(pos.y) > 100)
                                {
                                    wp.Remove(ard);
                                }
                                else
                                {
                                    foreach (KeyValuePair<Vector2Int, Vector2Int> ls in lsl)
                                    {
                                        if (DoLineSegmentsIntersect(pos, apos, ls.Key, ls.Value))
                                        {
                                            wp.Remove(ard);
                                            break;
                                        }
                                    }
                                }
                            }

                            if (wp.Count > 0)
                            {
                                int r = Random.Range(0, wp.Count);
                                RoomData _rd = wp[r];
                                _rd.SetWayPoint(rd);
                                lsl.Add(new KeyValuePair<Vector2Int, Vector2Int>(pos, new Vector2Int(100, (_rd.levelIndex * 100) + aos)));
                            }
                        }
                    }
                }
            }
        }

        stages = new Stage[STAGES_COUNT];
        int prevNC = 0;
        int currNClt = 0;
        for (int x = 0; x < stages.Length; x++)
        {
            stages[x] = new Stage(stageLength);

            for (int i = 0; i < stageLength; i++)
            {
                if (i == 0)
                {
                    StartingRoomData rd = new StartingRoomData();
                    stages[x].levels[i] = new RoomData[] { rd };
                    rd.levelIndex = i;
                    rd.stageIndex = i;
                }

                Debug.Log("x: " + x + " i: " + i );
                curr.Clear();
                foreach (RoomData rd in stages[x].levels[i]) curr.Add(rd);
                
                int nchoice = GameManager.GetItemFromListByChance(ints, choiceCountChances);
                int n = GameManager.GetItemFromListByChance(strgtPathLts, straightPathCountChances);
                if (nchoice == 1 && n > SINGLE_CHOICE_STR_PTH_LIMIT) n = SINGLE_CHOICE_STR_PTH_LIMIT;

                if ((prevNC == nchoice && currNClt >= STRT_PATH_LIMIT) || (nchoice == 1 && currNClt >= SINGLE_CHOICE_STR_PTH_LIMIT))
                {
                    List<int> il = new List<int>(ints);
                    List<float> cl = new List<float>(choiceCountChances);
                    il.RemoveAt(prevNC - 1);
                    cl.RemoveAt(prevNC - 1);
                    nchoice = GameManager.GetItemFromListByChance(il, cl);
                    currNClt = 0;
                }

                for (int j = 0; j < n; j++)
                {
                    if (i < stageLength - 2)
                    {
                        stages[x].levels[i + 1] = new RoomData[nchoice];
                        for (int y = 0; y < nchoice; y++)
                        {
                            RoomData rd = new EmptyRoomData() { levelIndex = y };
                            rd.stageIndex = i + 1;
                            stages[x].levels[i + 1][y] = rd;
                            nxt.Add(rd);
                            stages[x].roomCount++;
                        }

                        if (i > 0 && curr.Count == nxt.Count && GameManager.GetTrueBoolByChance(STRAIGHT_PATH_CHANCE))
                        {  
                            ConnectNodes(curr, nxt, true);
                        }
                        else
                        {
                            ConnectNodes(curr, nxt, false);
                        }
                        
                        if (prevNC != nchoice)
                        {
                            prevNC = nchoice;
                            currNClt = 1;
                            if (j != n - 1) i++;
                        }
                        else
                        {
                            currNClt++;
                            if (currNClt == STRT_PATH_LIMIT)
                            {
                                curr.Clear();
                                curr.AddRange(nxt);
                                nxt.Clear();
                                break;
                            }
                            else if (j != n - 1) i++;
                        }
                    }
                    else
                    {
                        RoomData rd = new KeyRoomData() { levelIndex = 0 };
                        rd.stageIndex = i + 1;
                        stages[x].levels[i + 1] = new RoomData[] { rd };
                        nxt.Add(rd);
                        ConnectNodes(curr, nxt, false);
                        i+=2;
                        nxt.Clear();
                        break;
                    }
                    curr.Clear();
                    curr.AddRange(nxt);
                    nxt.Clear();
                }
            }
        }

        //assign rooms
        //rooms are chosen base on percentage and number of nodes in that level
        //get empty rooms and put it in a list (by prev waypoints check if has similar room)
        //assign rooms
        //modify selection if necessary

        List<RoomData> empty = new List<RoomData>();

        //Add elite
        for (int i = 0; i < stages.Length; i++)
        {
            int elitCt = stages[i].roomCount / ROOMS_PER_ELITE;
            elitCt = Random.Range(elitCt, elitCt + 2);
            empty.Clear();
            
            for (int j = 1; j < stages[i].levels.Length - 2; j++)
            {
                if (j > ELITE_GAP_FROM_ST && j < stgL - ELITE_GAP_FROM_FN - 1)
                {
                    for (int x = 0; x < stages[i].levels[j].Length; x++)
                    {
                        RoomData rd = stages[i].levels[j][x];
                        if (rd is EmptyRoomData) empty.Add(rd);
                    }
                }
            }

            for (int j = 0; j < elitCt; j++)
            {
                if (empty.Count == 0) break;

                int r = Random.Range(0, empty.Count);
                RoomData rrd = empty[r];
                StrongEnemyRoomData rd = new StrongEnemyRoomData();
                stages[i].levels[rrd.stageIndex][rrd.levelIndex] = rd;
                rd.GetOtherRoomDataValues(rrd);
                empty.RemoveAt(r);

                curr.Clear();
                nxt.Clear();
                curr.Add(rd);
                int eliteGap = Random.Range(ELITE_GAP - 1, ELITE_GAP + 2);
                if (eliteGap <= 0) eliteGap = 1;

                for (int x = 0; x < eliteGap; x++)
                {
                    foreach (RoomData _rd in curr)
                    {
                        foreach (RoomData pwp in _rd.PrevWayPoints)
                        {
                            if (empty.Contains(pwp) && !nxt.Contains(pwp))
                            {
                                empty.Remove(pwp);
                                nxt.Add(pwp);
                            }
                        }
                    }

                    if (nxt.Count == 0) break;
                    else
                    {
                        curr.Clear();
                        curr.AddRange(nxt);
                        nxt.Clear();
                    }
                }

                curr.Clear();
                nxt.Clear();
                curr.Add(rd);
                for (int x = 0; x < eliteGap; x++)
                {
                    foreach (RoomData _rd in curr)
                    {
                        foreach (RoomData wp in _rd.WayPoints)
                        {
                            if (empty.Contains(wp) && !nxt.Contains(wp))
                            {
                                empty.Remove(wp);
                                nxt.Add(wp);
                            }
                        }
                    }

                    if (nxt.Count == 0) break;
                    else
                    {
                        curr.Clear();
                        curr.AddRange(nxt);
                        nxt.Clear();
                    }
                }
            }
        }

        //Add treasure rooms
        for (int i = 0; i < stages.Length; i++)
        {
            int chestCt = stages[i].roomCount / ROOMS_PER_CHEST;
            if (chestCt == 0) chestCt = 1;
            else if (chestCt > 1) chestCt = Random.Range(1, chestCt + 1);
            if (GameManager.GetTrueBoolByChance(BONUS_CHEST_CHANCE)) chestCt++;
            empty.Clear();

            for (int j = 1; j < stages[i].levels.Length - 1; j++)
            {
                if (j > CHEST_GAP_FROM_ST && j < stgL - CHEST_GAP_FROM_FN - 1)
                {
                    for (int x = 0; x < stages[i].levels[j].Length; x++)
                    {
                        RoomData rd = stages[i].levels[j][x];
                        if (rd is EmptyRoomData) empty.Add(rd);
                    }
                }
            }

            for (int j = 0; j < chestCt; j++)
            {
                if (empty.Count == 0) break;

                int r = Random.Range(0, empty.Count);
                RoomData rrd = empty[r];
                TreasureRoomData rd = new TreasureRoomData();
                stages[i].levels[rrd.stageIndex][rrd.levelIndex] = rd;
                rd.GetOtherRoomDataValues(rrd);
                empty.RemoveAt(r);

                curr.Clear();
                nxt.Clear();
                curr.Add(rd);
                int chestGap = Random.Range(CHEST_GAP - 1, CHEST_GAP + 2);
                if (chestGap <= 0) chestGap = 1;

                for (int x = 0; x < chestGap; x++)
                {
                    foreach (RoomData _rd in curr)
                    {
                        foreach (RoomData pwp in _rd.PrevWayPoints)
                        {
                            if (!nxt.Contains(pwp)) nxt.Add(pwp);
                            if (empty.Contains(pwp)) empty.Remove(pwp);
                        }
                    }

                    if (nxt.Count == 0) break;
                    else
                    {
                        curr.Clear();
                        curr.AddRange(nxt);
                        nxt.Clear();
                    }
                }

                curr.Clear();
                nxt.Clear();
                curr.Add(rd);
                for (int x = 0; x < chestGap; x++)
                {
                    foreach (RoomData _rd in curr)
                    {
                        foreach (RoomData wp in _rd.WayPoints)
                        {
                            if (!nxt.Contains(wp)) nxt.Add(wp);
                            if (empty.Contains(wp)) empty.Remove(wp);
                        }
                    }

                    if (nxt.Count == 0) break;
                    else
                    {
                        curr.Clear();
                        curr.AddRange(nxt);
                        nxt.Clear();
                    }
                }
            }
        }

        //Assign Rest areas
        for (int i = 0; i < stages.Length; i++)
        {
            empty.Clear();
            int restCt = Mathf.RoundToInt(stages[i].roomCount / (float)ROOMS_PER_REST);

            for (int j = 1; j < stages[i].levels.Length - 1; j++)
            {
                if (j > REST_GAP_FROM_ST)
                {
                    for (int x = 0; x < stages[i].levels[j].Length; x++)
                    {
                        RoomData rd = stages[i].levels[j][x];
                        if (rd is EmptyRoomData) empty.Add(rd);
                    }
                }
            }

            bool willSelectAnyEmptRoom = false;
            for (int j = 0; j < restCt; j++)
            {
                if (empty.Count == 0)
                {
                    willSelectAnyEmptRoom = true;
                    for (int k = 1; k < stages[i].levels.Length - 1; k++)
                    {
                        if (k > REST_GAP_FROM_ST)
                        {
                            for (int x = 0; x < stages[i].levels[k].Length; x++)
                            {
                                RoomData erd = stages[i].levels[k][x];
                                if (erd is EmptyRoomData) empty.Add(erd);
                            }
                        }
                    }
                    if (empty.Count == 0) break;
                }

                int r = Random.Range(0, empty.Count);
                RoomData rrd = empty[r];
                RestRoomData rd = new RestRoomData();
                stages[i].levels[rrd.stageIndex][rrd.levelIndex] = rd;
                rd.GetOtherRoomDataValues(rrd);
                empty.RemoveAt(r);    

                curr.Clear();
                nxt.Clear();
                curr.Add(rd);
                int restGap = Random.Range(REST_GAP - 1, REST_GAP + 2);
                if (restGap <= 0) restGap = 1;

                if (!willSelectAnyEmptRoom)
                {
                    foreach (RoomData erd in stages[i].levels[rrd.stageIndex]) if (empty.Contains(erd)) empty.Remove(erd);
                    for (int x = 0; x < restGap; x++)
                    {
                        foreach (RoomData _rd in curr)
                        {
                            foreach (RoomData pwp in _rd.PrevWayPoints)
                            {
                                if (!nxt.Contains(pwp)) nxt.Add(pwp);
                                if (empty.Contains(pwp)) empty.Remove(pwp);
                            }
                        }

                        if (nxt.Count == 0) break;
                        else
                        {
                            curr.Clear();
                            curr.AddRange(nxt);
                            nxt.Clear();
                        }
                    }

                    curr.Clear();
                    nxt.Clear();
                    curr.Add(rd);
                    for (int x = 0; x < restGap; x++)
                    {
                        foreach (RoomData _rd in curr)
                        {
                            foreach (RoomData wp in _rd.WayPoints)
                            {
                                if (!nxt.Contains(wp)) nxt.Add(wp);
                                if (empty.Contains(wp)) empty.Remove(wp);
                            }
                        }

                        if (nxt.Count == 0) break;
                        else
                        {
                            curr.Clear();
                            curr.AddRange(nxt);
                            nxt.Clear();
                        }
                    }
                }
            }
        }

        //Assign sshops
        for (int i = 0; i < stages.Length; i++)
        {
            empty.Clear();
            int shopCt = Mathf.RoundToInt(stages[i].roomCount / (float)ROOMS_PER_REST);

            for (int j = 1; j < stages[i].levels.Length - 1; j++)
            {
                if (j > SHOP_GAP_FROM_ST)
                {
                    for (int x = 0; x < stages[i].levels[j].Length; x++)
                    {
                        RoomData rd = stages[i].levels[j][x];
                        if (rd is EmptyRoomData) empty.Add(rd);
                    }
                }
            }


            for (int j = 0; j < shopCt; j++)
            {
                if (empty.Count == 0) break;

                int r = Random.Range(0, empty.Count);
                RoomData rrd = empty[r];
                ShopRoomData rd = new ShopRoomData();
                stages[i].levels[rrd.stageIndex][rrd.levelIndex] = rd;
                rd.GetOtherRoomDataValues(rrd);
                empty.RemoveAt(r);

                curr.Clear();
                nxt.Clear();
                curr.Add(rd);
                int shopGap = Random.Range(SHOP_GAP - 1, SHOP_GAP + 2);
                if (shopGap <= 0) shopGap = 1;

                for (int x = 0; x < shopGap; x++)
                {
                    foreach (RoomData _rd in curr)
                    {
                        foreach (RoomData pwp in _rd.PrevWayPoints)
                        {
                            if (!nxt.Contains(pwp)) nxt.Add(pwp);
                            if (empty.Contains(pwp)) empty.Remove(pwp);
                        }
                    }

                    if (nxt.Count == 0) break;
                    else
                    {
                        curr.Clear();
                        curr.AddRange(nxt);
                        nxt.Clear();
                    }
                }

                curr.Clear();
                nxt.Clear();
                curr.Add(rd);
                for (int x = 0; x < shopGap; x++)
                {
                    foreach (RoomData _rd in curr)
                    {
                        foreach (RoomData wp in _rd.WayPoints)
                        {
                            if (!nxt.Contains(wp)) nxt.Add(wp);
                            if (empty.Contains(wp)) empty.Remove(wp);
                        }
                    }

                    if (nxt.Count == 0) break;
                    else
                    {
                        curr.Clear();
                        curr.AddRange(nxt);
                        nxt.Clear();
                    }
                }
            }
        }

        List<EventData> s1Events = new List<EventData>();
        List<EventData> s2Events = new List<EventData>();
        List<EventData> s3Events = new List<EventData>();
        List<List<EventData>> events = new List<List<EventData>>() { s1Events, s2Events, s3Events };
        EventData eventDataTemp = null;

        int index = 0;
        while (true)
        {
            eventDataTemp = DataLibrary.GetEventData(index);
            if (eventDataTemp != null)
            {
                if (eventDataTemp.encounterStages.Contains(GS_FIRST)) s1Events.Add(eventDataTemp);
                if (eventDataTemp.encounterStages.Contains(GS_SECOND)) s2Events.Add(eventDataTemp);
                if (eventDataTemp.encounterStages.Contains(GS_LAST)) s3Events.Add(eventDataTemp);
            }
            else break;
            index++;
        }

        //Assign event rooms
        if (WILL_SPAWN_ENEMY_EVENTS)
        {
            for (int i = 0; i < stages.Length; i++)
            {
                empty.Clear();

                for (int j = 1; j < stages[i].levels.Length - 1; j++)
                {
                    for (int x = 0; x < stages[i].levels[j].Length; x++)
                    {
                        RoomData rd = stages[i].levels[j][x];
                        if (rd is EmptyRoomData) empty.Add(rd);
                    }
                }

                float eventsRatio = Random.Range(EVENTS_RATIO - 0.1f, EVENTS_RATIO + 0.05f);
                int eventCt = Mathf.RoundToInt(empty.Count * eventsRatio);

                for (int j = 0; j < eventCt; j++)
                {
                    int r = Random.Range(0, empty.Count);
                    RoomData rrd = empty[r];

                    int r2 = Random.Range(0, events[i].Count);
                    EventData e = events[i][r2];
                    if (e.OneTimeEncounter) events[i].RemoveAt(r2);

                    EventRoomData rd = new EventRoomData(e);
                    stages[i].levels[rrd.stageIndex][rrd.levelIndex] = rd;
                    rd.GetOtherRoomDataValues(rrd);
                    empty.RemoveAt(r);
                }

                for (int j = empty.Count - 1; j >= 0; j--)
                {
                    int r = Random.Range(0, empty.Count);
                    RoomData rrd = empty[r];
                    EnemyRoomData rd = new EnemyRoomData();
                    stages[i].levels[rrd.stageIndex][rrd.levelIndex] = rd;
                    rd.GetOtherRoomDataValues(rrd);
                    empty.RemoveAt(r);
                }
            }
        }
    }
}
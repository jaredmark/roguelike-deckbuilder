using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    Transform canvas;

    class ObjectPool
    {
        public string tag;
        public int objectCount;
        public GameObject prefab;
        //If the object is a UI element, it will be parented to a canvas object.
        public bool isUIObject;

        public ObjectPool(string tag, int objectCount, GameObject prefab, bool isUIObject)
        {
            this.tag = tag;
            this.objectCount = objectCount;
            this.prefab = prefab;
            this.isUIObject = isUIObject;
        }
    }

    Dictionary<string, Queue<GameObject>> objectPool = new Dictionary<string, Queue<GameObject>>();
    List<ObjectPool> objectsToPool = new List<ObjectPool>();

    void Awake()
    {
        objectsToPool.Add(new ObjectPool(StringManager.OBJECT_POOL_TAG_MINION_CARD, 45, Resources.Load("Prefabs/MinionCard") as GameObject, true));
        objectsToPool.Add(new ObjectPool(StringManager.OBJECT_POOL_TAG_SPELL_CARD, 30, Resources.Load("Prefabs/SpellCard") as GameObject, true));
        objectsToPool.Add(new ObjectPool(StringManager.TAG_TOOLTIP, 4, Resources.Load<GameObject>("Prefabs/ToolTip"), true));

        canvas = transform.Find("Canvas");

        foreach (ObjectPool objectToPool in objectsToPool)
        {
            Queue<GameObject> objectQueue = new Queue<GameObject>();
            Transform parent;

            if (objectToPool.isUIObject)
            {
                parent = canvas;
            }
            else
            {
                parent = transform;
            }

            for (int i = 0; i < objectToPool.objectCount; i ++)
            {
                GameObject obj = Instantiate(objectToPool.prefab, parent);
                obj.SetActive(false);
                objectQueue.Enqueue(obj);
            }

            objectPool.Add(objectToPool.tag, objectQueue);
        }
    }

    public Card SpawnCardFromPool(CardData cardData, Transform parent)
    {
        if (cardData is SpellCardData)
        {
            return SpawnFromObjectPool(StringManager.OBJECT_POOL_TAG_SPELL_CARD, parent).GetComponent<Card>();
        }
        else
        {
            return SpawnFromObjectPool(StringManager.OBJECT_POOL_TAG_MINION_CARD, parent).GetComponent<Card>();
        }
    }

    public GameObject SpawnFromObjectPool(string tag, Transform parent)
    {
        GameObject obj = null;
        
        int maxCount = objectPool[tag].Count;
        int i = 0;
        
        while (obj == null)
        {
            obj = objectPool[tag].Dequeue();
            if (obj.activeSelf)
            {
                objectPool[tag].Enqueue(obj);
                obj = null;
            }
            
            i++;
            if (i == maxCount)
            {
                Debug.Log("Pooled object ran out");
                foreach (ObjectPool objectPool in objectsToPool)
                {
                    if (objectPool.tag == tag)
                    {
                        Transform newObjParent;
                        if (objectPool.isUIObject)
                        {
                            newObjParent = canvas;
                        }
                        else
                        {
                            newObjParent = transform;
                        }
                        GameObject newObj = Instantiate(objectPool.prefab, newObjParent);
                        obj = newObj;
                        break;
                    }
                }
                break;
            }
        }
        obj.SetActive(true);
        obj.transform.SetParent(parent);
        objectPool[tag].Enqueue(obj);

        return obj;
    }

    public void DespawnObject(GameObject obj)
    {
        obj.transform.SetParent(transform);
        obj.transform.gameObject.SetActive(false);
    }

    public void DespawnUIObject(GameObject obj)
    {
        obj.transform.SetParent(canvas);
        obj.transform.gameObject.SetActive(false);
    }
}

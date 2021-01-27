using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;
using SaveSystem;
using UnityEngine.EventSystems;

public class GameManagerScript : MonoBehaviour
{
    public int level;
    public bool disableInput;
    public bool isMenu;
    public bool weaponButtonsAppeared;
    public bool unlockingDoor;
    public GameObject player;
    public List<GameObject> zombieList;
    public List<GameObject> humanList;
    public List<GameObject> karmaPersonagesList;
    [HideInInspector]
    public float maxHeadProtection = .5f;
    [HideInInspector]
    public float maxBodyProtection = 1.5f;
    [HideInInspector]
    public float minShoeNoise = .6f;
    [HideInInspector]
    public float maxShoeNoise = 1.5f;
    [HideInInspector]
    public float minHumanSpeed = .5f;
    [HideInInspector]
    public float maxHumanSpeed = 1.1f;
    [HideInInspector]
    public float maxZombieAttack = 2f;
    [HideInInspector]
    public float maxZombieSpeed = 2f;

    public NavMeshSurface navMeshSurface;
    public LayerMask obstacleVisualMask;
    public LayerMask wallsMask;
    public LayerMask roofMask;
    public LayerMask zombieMask;
    private Cryptography cryptography = new Cryptography("*********************");

    public static GameManagerScript instance = null;
    public AssetsHolder AH;
    public GameMenuHandler GMH;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else if (instance != this) Destroy(gameObject);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (weaponButtonsAppeared) StartCoroutine(GMH.WeaponButtonsHideCoroutnie());
            if (GMH.infoMenuActive) GMH.ResetInfoMenu();
            else if (!isMenu && AH.mainMenuButton.GetComponent<Button>().interactable) GMH.InfoRayCast();
        }

    }

    private void OnSceneLoaded(Scene aScene, LoadSceneMode aMode)
    {
        player = GameObject.Find("Characters/PlayerJason");
        if (player == null) player = GameObject.Find("Characters/PlayerEmily");
        if (player == null) player = GameObject.Find("Characters/PlayerBill");
        AH = GameObject.Find("AssetsHolder").GetComponent<AssetsHolder>();
        GMH = GameObject.Find("Canvas/GameMenuHandler").GetComponent<GameMenuHandler>();
        navMeshSurface = GameObject.Find("EnviromentStatic/NavMeshSurface").GetComponent<NavMeshSurface>();
        navMeshSurface.BuildNavMesh();

        if (((float)Screen.height / Screen.width) < 1.7f)
        {
            Camera.main.GetComponent<CameraConstantWidth>().WidthOrHeight = 1f;
            GameObject.Find("Canvas").GetComponent<CanvasScaler>().matchWidthOrHeight = .6f;
        }

        zombieList.Clear();
        foreach (Transform t in GameObject.Find("Characters").transform)
            if (t.tag == "zombie") zombieList.Add(t.gameObject);
        humanList.Clear();
        karmaPersonagesList.Clear();
        foreach (Transform t in GameObject.Find("Characters").transform)
            if (t.tag == "human")
            {
                if (t.gameObject != player && t.GetComponent<NPCScript>().karmaPersonage) karmaPersonagesList.Add(t.gameObject);
                humanList.Add(t.gameObject);
            }

        SetPlayerPrefs();
        SaveClass save = SaveGameFile();
        if (save != null)
        {
            level = save.level;
            SetPersonageEquipment(save);
        }
        else level = 1;

        if (player.GetComponent<PlayerScript>().cola > 0)
        {
            AH.throwColaButtonObj.transform.Find("ColaValue").GetComponent<Text>().text = player.GetComponent<PlayerScript>().cola.ToString();
            AH.throwColaButtonObj.SetActive(true);
        }
        else AH.throwColaButtonObj.SetActive(false);
        GameObject.Find("Canvas/LevelText").transform.Find("Text").GetComponent<Text>().text = level.ToString();
        disableInput = false;

        StartCoroutine(GMH.SceneFadeOut(false, 2));
    }

    public string Txt(string text)
    {
        string translatedText = Lean.Localization.LeanLocalization.GetTranslationText(text);
        return translatedText;
    }

    public GameObject ClosestObject(GameObject mainObj, List<GameObject> list)
    {
        GameObject nearestObj = null;
        float distance = Mathf.Infinity;
        foreach (GameObject obj in list)
        {
            Vector3 mainObjPos = new Vector3(mainObj.transform.position.x, 0, mainObj.transform.position.z);
            Vector3 objPos = new Vector3(obj.transform.position.x, 0, obj.transform.position.z);
            float d = Vector3.Distance(mainObjPos, objPos);
            if (d < distance)
            {
                distance = d;
                nearestObj = obj;
            }
        }
        return nearestObj;
    }

    public Vector3 ScreenPos(GameObject obj)
    {
        float multiplier = 1 + obj.transform.position.z / 13.5f;
        Vector3 pos = Camera.main.WorldToScreenPoint(obj.transform.position + new Vector3(0, 0, 1.1f * multiplier));
        var rectT = GameObject.Find("Canvas").GetComponent<RectTransform>();
        pos.x *= rectT.rect.width / Screen.width;
        pos.y *= rectT.rect.height / Screen.height;
        return pos;
    }

    public void AddNoiseObjectToList(GameObject obj, float noisiness) //Add noise object to listOfHearedObjects of all zobmies in range
    {
        foreach (GameObject zombie in zombieList)
        {
            var ZScript = zombie.GetComponent<ZombieScript>();
            var ZHeadPos = ZScript.viewMeshFilter.transform.position;
            if (ZScript.knockdown || ZScript.listOfHearedObjects.Contains(obj)) continue;
            if (Vector3.Distance(obj.transform.position, ZHeadPos) <= ZScript.viewRadius * noisiness)
            {
                Vector3 dirToTarget = (ZHeadPos - obj.transform.position).normalized;
                float dstToTarget = Vector3.Distance(obj.transform.position, ZHeadPos);
                if (!Physics.Raycast(obj.transform.position, dirToTarget, dstToTarget, ZScript.obstacleSoundMask))
                    ZScript.listOfHearedObjects.Add(obj);
            }
        }
    }

    public void AddGunshotNoiseToList(GameObject obj, float noisiness)
    {
        foreach (GameObject zombie in zombieList)
        {
            var ZScript = zombie.GetComponent<ZombieScript>();
            if (ZScript.knockdown || ZScript.listOfHearedObjects.Contains(obj)) continue;
            var ZHeadPos = ZScript.viewMeshFilter.transform.position;
            Vector3 objPos = new Vector3(obj.transform.position.x, 1, obj.transform.position.z);
            float localNoiseness = noisiness;
            List<GameObject> obstaclesList = new List<GameObject>();
            while (!ZScript.listOfHearedObjects.Contains(obj))
            {
                if (Vector3.Distance(objPos, ZHeadPos) <= ZScript.viewRadius * localNoiseness)
                {
                    Vector3 dirToTarget = (ZHeadPos - objPos).normalized;
                    float dstToTarget = Vector3.Distance(objPos, ZHeadPos);
                    RaycastHit hit;
                    if (!Physics.Raycast(objPos, dirToTarget, out hit, dstToTarget, ZScript.obstacleSoundMask))
                    {
                        ZScript.listOfHearedObjects.Add(obj);
                    }
                    else if (localNoiseness > .5f)
                    {
                        localNoiseness /= 2;
                        hit.collider.gameObject.GetComponent<BoxCollider>().enabled = false;
                        obstaclesList.Add(hit.collider.gameObject);
                    }
                    else break;
                }
                else break;
            }
            if (obstaclesList.Count > 0)
            {
                foreach (GameObject obst in obstaclesList) obst.GetComponent<BoxCollider>().enabled = true;
            }
        }
    }

    public Vector3 RandomNavSphere(Vector3 origin, float dist, int layermask)
    {
        NavMeshHit navHit;
        Vector3 viewPos = new Vector3(origin.x, 1.1f, origin.z);
        int counter = 0;
        do
        {
            Vector3 randDirection = Random.onUnitSphere * Random.Range(2, 5f);
            randDirection += origin;
            NavMesh.SamplePosition(randDirection, out navHit, dist, layermask);
            counter++;
            if (counter == 20) return Vector3.zero;
            print("SamplePosition " + navHit.position);
        }
        while (!Physics.Raycast(viewPos, navHit.position.normalized, dist, obstacleVisualMask));
        return navHit.position;
    }

    public void DoorBreak(GameObject door)
    {
        print("DoorBreak " + door);
        SoundManager.instance.RandomizeSfx(1f, SoundManager.instance.doorSounds[4]);
        var particle = Instantiate(AH.doorBreakRubble, door.transform.position, door.transform.rotation);
        particle.GetComponent<ParticleSystem>().collision.SetPlane(0, GameObject.Find("EnviromentStatic/Ground").transform);
        Destroy(door);
        Destroy(particle, 4);
    }

    public void FurnitureBreak(GameObject furtinure)
    {
        print("FurnitureBreak " + furtinure);
        if (furtinure.name.Contains("CabinetSmall") || furtinure.name.Contains("Kitchen"))
            SoundManager.instance.RandomizeSfx(1, SoundManager.instance.furnitureSounds[11]);
        else if (furtinure.name.Contains("Table"))
                SoundManager.instance.RandomizeSfx(1, SoundManager.instance.furnitureSounds[10]);
        else if (furtinure.name.Contains("Chair"))
            SoundManager.instance.RandomizeSfx(.35f, SoundManager.instance.furnitureSounds[12], SoundManager.instance.furnitureSounds[13]);
        else if (furtinure.name.Contains("HospitalBed") || furtinure.name.Contains("Stove"))
            SoundManager.instance.RandomizeSfx(.6f, SoundManager.instance.furnitureSounds[14]);
        else if (furtinure.name.Contains("MedicDevice"))
            SoundManager.instance.RandomizeSfx(.7f, SoundManager.instance.furnitureSounds[15]);
        var particle = Instantiate(AH.doorBreakRubble, furtinure.transform.position, furtinure.transform.rotation);
        particle.GetComponent<ParticleSystem>().collision.SetPlane(0, GameObject.Find("EnviromentStatic/Ground").transform);
        Destroy(furtinure);
        Destroy(particle, 4);
    }

    public void TextPopup(int number, GameObject obj, string text, Color color)
    {
        GameObject popup = Instantiate(AH.speechPrefabs[number], GameObject.Find("Canvas").transform);
        popup.GetComponent<RectTransform>().anchoredPosition = ScreenPos(obj);
        popup.SetActive(true);
        popup.GetComponent<TextPopupScript>().StartTextPopup(text, color);
    }

    public void CheckRebake()
    {
        if (!navMeshSurface.GetComponent<RebakeScript>().isBusy)
        {
            navMeshSurface.GetComponent<RebakeScript>().isBusy = true;
            StartCoroutine(navMeshSurface.GetComponent<RebakeScript>().RebakeNavMeshCoroutine());
        }
        else navMeshSurface.GetComponent<RebakeScript>().again = true;
    }

    private void SetPlayerPrefs()
    {
        AH.chooseHandToggle.isOn = PlayerPrefs.GetInt("isLeftHanded", 0) == 1;
        if (AH.chooseHandToggle.isOn) GMH.ChooseHand();
    }

    public IEnumerator NextLevelCoroutine()
    {
        if (!unlockingDoor)
        {
            unlockingDoor = true;
            print("Start unlocking ExitDoor");
            disableInput = true;
            GMH.SetButtonsActive(false);
            var PScript = player.GetComponent<PlayerScript>();
            PScript.anim.SetBool("isWalking", false);
            PScript.anim.SetBool("isRunning", false);
            GameObject handle = null;
            foreach (Transform t in GameObject.Find("Enviroment").transform)
                if (t.name.Contains("ExitDoor") && t.gameObject.activeSelf) 
                    handle = t.gameObject.transform.Find("Handle").gameObject;
            Vector3 rotateDirection = (handle.transform.position - player.transform.position).normalized;
            rotateDirection.y = 0;
            while (Quaternion.Angle(player.transform.rotation, Quaternion.LookRotation(rotateDirection)) > 1)
            {
                Vector3 newDirection = Vector3.RotateTowards(player.transform.forward, rotateDirection, 5 * Time.deltaTime, 0.0f);
                player.transform.rotation = Quaternion.LookRotation(newDirection);
                yield return new WaitForEndOfFrame();
            }
            PScript.anim.SetTrigger("interact B");
            yield return new WaitForSeconds(1f);
            if (!PScript.attacked)
            {
                SoundManager.instance.RandomizeSfx(1f, SoundManager.instance.doorSounds[7]);
                yield return new WaitForSeconds(2.33f);
                if (!PScript.attacked)
                {
                    Time.timeScale = 0;
                    SoundManager.instance.RandomizeSfx(.4f, SoundManager.instance.musicSounds[1]);
                    PScript.haveKey = false;
                    unlockingDoor = false;
                    CountKarma();
                    AH.winMenuObj.SetActive(true);
                }
            }
        }
    }

    public SaveClass SaveGameFile()
    {
        FileSave fileSave = new FileSave(FileFormat.Xml);
        string encrypted = fileSave.ReadFromFile<string>(Application.persistentDataPath + "/save.xml");
        if (encrypted == null) return null;
        else return cryptography.Decrypt<SaveClass>(encrypted);
    }

    public void SaveGame(string personage)
    {
        print("SaveGame " + personage);
        SaveClass save = SaveGameFile();
        string[] JasonWear = null;
        string[] JasonWeapons = null;
        int[] JasonBullets = null;
        string[] EmilyWear = null;
        string[] EmilyWeapons = null;
        int[] EmilyBullets = null;
        string[] BillWear = null;
        string[] BillWeapons = null;
        int[] BillBullets = null;
        string[] currentWeapon = new string[] { "", "", "" };
        int[] karma = new int[3];
        int[] cola = new int[3];

        if (save != null)
        {
            if (save.JasonWear != null) JasonWear = save.JasonWear;
            if (save.JasonWeapons != null) JasonWeapons = save.JasonWeapons;
            if (save.JasonBullets != null) JasonBullets = save.JasonBullets;
            if (save.EmilyWear != null) EmilyWear = save.EmilyWear;
            if (save.EmilyWeapons != null) EmilyWeapons = save.EmilyWeapons;
            if (save.EmilyBullets != null) EmilyBullets = save.EmilyBullets;
            if (save.BillWear != null) BillWear = save.BillWear;
            if (save.BillWeapons != null) BillWeapons = save.BillWeapons;
            if (save.BillBullets != null) BillBullets = save.BillBullets;
            currentWeapon = save.currentWeapon;
            karma = save.karma;
            cola = save.cola;
        }

        var PScript = player.GetComponent<PlayerScript>();
        if (personage == PlayerScript.Personage.Jason.ToString())
        {
            JasonWear = new string[] { PScript.head.ToString(), PScript.clothes.ToString(), PScript.shoes.ToString() };
            JasonWeapons = PScript.weapons;
            JasonBullets = PScript.bullets;
            currentWeapon[0] = PScript.weapon.ToString();
            karma[0] = PScript.karma;
            cola[0] = PScript.cola;
        }
        else if (personage == PlayerScript.Personage.Emily.ToString())
        {
            EmilyWear = new string[] { PScript.head.ToString(), PScript.clothes.ToString(), PScript.shoes.ToString() };
            EmilyWeapons = PScript.weapons;
            EmilyBullets = PScript.bullets;
            currentWeapon[1] = PScript.weapon.ToString();
            karma[1] = PScript.karma;
            cola[1] = PScript.cola;
        }
        else if (personage == PlayerScript.Personage.Bill.ToString())
        {
            BillWear = new string[] { PScript.head.ToString(), PScript.clothes.ToString(), PScript.shoes.ToString() };
            BillWeapons = PScript.weapons;
            BillBullets = PScript.bullets;
            currentWeapon[2] = PScript.weapon.ToString();
            karma[2] = PScript.karma;
            cola[2] = PScript.cola;
        }

        FileSave fileSave = new FileSave(FileFormat.Xml);
        fileSave.WriteToFile(Application.persistentDataPath + "/save.xml", cryptography.Encrypt(new SaveClass(level,
            JasonWear, JasonWeapons, JasonBullets, EmilyWear, EmilyWeapons, EmilyBullets,
            BillWear, BillWeapons, BillBullets, currentWeapon, karma, cola)));
    }

    public void DeleteSave()
    {
        print("Savegame deleted");
        string[] filePaths = Directory.GetFiles(Application.persistentDataPath);
        if (filePaths.Length > 0) File.Delete(filePaths[0]);
    }

    private void SetPersonageEquipment(SaveClass save)
    {
        var PScript = player.GetComponent<PlayerScript>();
        print("Loadgame " + PScript.personage);
        if (PScript.personage == PlayerScript.Personage.Jason)
        {
            PScript.head = (HumanScript.Head)System.Enum.Parse(typeof(HumanScript.Head), save.JasonWear[0]);
            PScript.clothes = (HumanScript.Clothes)System.Enum.Parse(typeof(HumanScript.Clothes), save.JasonWear[1]);
            PScript.shoes = (HumanScript.Shoes)System.Enum.Parse(typeof(HumanScript.Shoes), save.JasonWear[2]);
            PScript.weapons = save.JasonWeapons;
            PScript.weapon = (HumanScript.Weapon)System.Enum.Parse(typeof(HumanScript.Weapon), save.currentWeapon[0]);
            PScript.bullets = save.JasonBullets;
            PScript.karma = save.karma[0];
            PScript.cola = save.cola[0];
        }
        else if (PScript.personage == PlayerScript.Personage.Emily)
        {
            PScript.head = (HumanScript.Head)System.Enum.Parse(typeof(HumanScript.Head), save.EmilyWear[0]);
            PScript.clothes = (HumanScript.Clothes)System.Enum.Parse(typeof(HumanScript.Clothes), save.EmilyWear[1]);
            PScript.shoes = (HumanScript.Shoes)System.Enum.Parse(typeof(HumanScript.Shoes), save.EmilyWear[2]);
            PScript.weapons = save.EmilyWeapons;
            PScript.weapon = (HumanScript.Weapon)System.Enum.Parse(typeof(HumanScript.Weapon), save.currentWeapon[1]);
            PScript.bullets = save.EmilyBullets;
            PScript.karma = save.karma[1];
            PScript.cola = save.cola[1];
        }
        else if (PScript.personage == PlayerScript.Personage.Bill)
        {
            PScript.head = (HumanScript.Head)System.Enum.Parse(typeof(HumanScript.Head), save.BillWear[0]);
            PScript.clothes = (HumanScript.Clothes)System.Enum.Parse(typeof(HumanScript.Clothes), save.BillWear[1]);
            PScript.shoes = (HumanScript.Shoes)System.Enum.Parse(typeof(HumanScript.Shoes), save.BillWear[2]);
            PScript.weapons = save.BillWeapons;
            PScript.weapon = (HumanScript.Weapon)System.Enum.Parse(typeof(HumanScript.Weapon), save.currentWeapon[2]);
            PScript.bullets = save.BillBullets;
            PScript.karma = save.karma[2];
            PScript.cola = save.cola[2];
        }

        PScript.LoadHeadModel();
        PScript.LoadBodyModel();
        PScript.SetStats();
        PScript.WeaponButtonSetup();
    }

    public void RandomPlace(List<GameObject> _waypoints, List<GameObject> objects)
    {
        int count = _waypoints.Count;
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, _waypoints.Count);
            objects[i].transform.position =
                new Vector3(_waypoints[index].transform.position.x, objects[i].transform.position.y, _waypoints[index].transform.position.z);
            _waypoints.RemoveAt(index);
            objects[i].SetActive(true);
        }
    }

    public void CountKarma()
    {
        if (karmaPersonagesList.Count > 0)
        {
            int karmaChange = 0;
            foreach (GameObject personage in karmaPersonagesList)
            {
                if (personage.tag == "human" && !personage.GetComponent<HumanScript>().isDead)
                {
                    karmaChange++;
                    player.GetComponent<PlayerScript>().karma++;
                }
                else
                {
                    karmaChange--;
                    player.GetComponent<PlayerScript>().karma--;
                }
            }
            var karmaText = AH.winMenuObj.transform.Find("KarmaText").GetComponent<Text>();
            string plus = "";
            if (karmaChange > 0)
            {
                karmaText.color = new Color(.024f, .688f, 0);
                plus = "+";
            }
            if (karmaChange < 0) karmaText.color = new Color(.537f, .13f, .13f);
            karmaText.text = Txt("UI/Karma") + " " + plus + karmaChange;
        }
    }
}
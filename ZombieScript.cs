using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class ZombieScript : MonoBehaviour
{
	public enum State
	{
		idle,
		walkingToEat,
		patrol,
		rotate,
		chase,
		checkPos,
		prowl,
		attack,
		eat,
		obstacleBreak
	}
	[Header("Zombie params")]
	public string zombieName;
	public State state;
	public GameObject[] waypoints = new GameObject[] { };
	public int health;
	public float speed;
	public float hunger;
	public float attackStrength;
	public bool haveRightBoot;
	public bool haveLeftBoot;

	public bool agentIsStopped;
	public bool knockdown;
	public bool isDead;
	public bool isCheckPos;
	public bool stayIdle;
	[Header("Common params")]
	private int waypointNum;
	public Vector3 targetWP;
	[HideInInspector]
	public CapsuleCollider mainCollider;
	public GameObject torso;
	public GameObject headObj;
	private GameObject leftThigh;
	private GameObject rightThigh;
	public string animHit = "animDone";
	private bool isPause;
	private bool isEatPause;
	private int obstacleTimer;
	public GameObject obstacle;
	private Vector3 zombiePos;
	private Vector3 obstaclePos;
	private int prowlTimer;
	private bool checkPosBreaker;
	private bool doCollisionOnce;

	[HideInInspector]
	public NavMeshAgent agent;
	[HideInInspector]
	public NavMeshPath path;
	[HideInInspector]
	public float defaultNMSpeed;
	private Vector3 pathDirection;
	private Vector3 rotateDirection;

	public GameObject targetToChase;
	private int memoryOftargetToChase;
	public GameObject attackTarget;
	public GameObject bodyToEat;
	public List<GameObject> listOfHearedObjects = new List<GameObject>();
	public Vector3 lastKnownTargetPos;
	private Vector3 prowlPos;
	private GameObject hearSign;
	public AudioSource eatAudioSource;

	protected GameManagerScript GMS;
	protected AssetsHolder AH;
	public Animator anim;

	//private List<Vector3> _path = new List<Vector3>();


	[Header("FOW params")]
	public float viewRadius = 5f;
	private float defaultViewRadius;
	[Range(0, 360)]
	public float viewAngle = 90;

	public LayerMask targetMask;
	public LayerMask obstacleSoundMask;
	public LayerMask destroyableObstacleMask;

	[HideInInspector]
	private List<GameObject> visibleTargets = new List<GameObject>();

	public float meshResolution;
	public int edgeResolveIterations;
	public float edgeDstThreshold;
	public MeshFilter viewMeshFilter;
	Mesh viewMesh;

	private void Awake()
	{
		GMS = GameObject.Find("GameManager").GetComponent<GameManagerScript>();
		AH = GameObject.Find("AssetsHolder").GetComponent<AssetsHolder>();
		anim = GetComponent<Animator>();
		agent = GetComponent<NavMeshAgent>();
		agent.updateRotation = false;
		defaultNMSpeed = agent.speed;
		path = new NavMeshPath();
		eatAudioSource = GetComponent<AudioSource>();
		health = health + Random.Range(0, health / 2);
		defaultViewRadius = viewRadius;
		mainCollider = transform.Find("MainCollider").GetComponent<CapsuleCollider>();
		leftThigh = transform.Find("Bip001/Bip001 Pelvis/Bip001 L Thigh").gameObject;
		rightThigh = transform.Find("Bip001/Bip001 Pelvis/Bip001 R Thigh").gameObject;
	}

	void Start()
	{
		if (waypoints.Length > 0)
		{
			targetWP = waypoints[0].transform.position;
			transform.LookAt(targetWP);
			StartCoroutine(PatrolCoroutine());
		}
		else if (!stayIdle) StartCoroutine(ProwlCoroutine());

		viewMesh = new Mesh();
		viewMesh.name = "View Mesh";
		viewMeshFilter.mesh = viewMesh;
		StartCoroutine(FindTargetsWithDelay());
	}

	void Update()
	{
/*		if (Input.GetMouseButtonDown(1))
		{
			RaycastHit hit;
			if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit))
			{
				print(gameObject + " move to " + hit.point);
				lastKnownTargetPos = hit.point;
				isPause = true;
				StopAgent(true);
				anim.SetBool("isRunning", false);
				anim.SetBool("isWalking", true);
				state = State.chase;
				StartCoroutine(ChaseCoroutine());
			}
		}*/

		if (!knockdown)
		{
			switch (state)
			{
				default:
				case State.idle:

					break;

				case State.patrol:
					if (AngleToTarget() > 1)
					{
						RotateToDir(pathDirection);
					}
					if (Vector3.Distance(transform.position, targetWP) < .2f)
					{
						print("targetWP reached by " + gameObject);
						StopAgent(true);
						anim.SetBool("isDumbWalking", false);
						waypointNum++;
						if (waypointNum > waypoints.Length - 1) waypointNum = 0;
						targetWP = waypoints[waypointNum].transform.position;
						state = State.idle;
						StartCoroutine(PatrolCoroutine());
					}
					break;

				case State.rotate:
					if (Quaternion.Angle(transform.rotation, Quaternion.LookRotation(rotateDirection)) < 1)
					{
						if (isCheckPos)
						{
							print("Rotate done, State.checkPos " + gameObject);
							StopAgent(false);
							agent.SetDestination(lastKnownTargetPos);
							//PathList();
							state = State.checkPos;
						}
						else if (waypoints.Length > 0)
						{
							print("Rotate done, return to State.patrol " + gameObject);
							StopAgent(false);
							agent.SetDestination(targetWP);
							state = State.patrol;
						}
						else
						{
							print("Rotate done, State.prowl " + gameObject);
							StopAgent(false);
							agent.SetDestination(prowlPos);
							state = State.prowl;
						}
					}
					else RotateToDir(rotateDirection);
					break;

				case State.chase:
					if (isPause) break;
					if (AngleToTarget() > 1) RotateToDir(pathDirection);

					if (memoryOftargetToChase == 4 && Vector3.Distance(transform.position, targetToChase.transform.position) < 1.1f)
					{
						print(gameObject + " attacking " + targetToChase);
						StopAgent(true);
						anim.SetBool("isRunning", false);
						anim.SetTrigger("hold");
						targetToChase.GetComponent<HumanScript>().anim.SetBool("isWalking", false);
						targetToChase.GetComponent<HumanScript>().anim.SetBool("isRunning", false);
						targetToChase.GetComponent<HumanScript>().attacked = true;
						targetToChase.GetComponent<HumanScript>().attacker = gameObject;
						StartCoroutine(targetToChase.GetComponent<HumanScript>().FightingCoroutine());
						attackTarget = targetToChase;
						targetToChase = null;
						isCheckPos = false;
						state = State.attack;
						break;
					}

					if (Vector3.Distance(transform.position, lastKnownTargetPos) < .2f || 
						(targetToChase != null && targetToChase.GetComponent<HumanScript>().attacked))
					{
						anim.SetBool("isRunning", false);
						targetToChase = null;
						StopAgent(true);
						StartCoroutine(LookAroundCoroutine());
						eatAudioSource.clip = SoundManager.instance.disappointmentSounds[Random.Range(0, SoundManager.instance.disappointmentSounds.Length)];
						eatAudioSource.volume = 1f * SoundManager.instance.masterSoundVolume;
						eatAudioSource.Play();
						state = State.idle;
					}
					break;

				case State.checkPos:
					if (AngleToTarget() > 1) RotateToDir(pathDirection);
					if (lastKnownTargetPos == Vector3.zero)
					{
						StopCheckPosZombieOnTheWay(null);
						break;
					}
					if (Vector3.Distance(transform.position, lastKnownTargetPos) < 1f && !checkPosBreaker)
					{
						checkPosBreaker = true;
						StartCoroutine(CheckPosBreakerCoroutine());
					}
					if (Vector3.Distance(transform.position, lastKnownTargetPos) < .2f)
					{
						StopAgent(true);
						anim.SetBool("isWalking", false);
						lastKnownTargetPos = Vector3.zero;
						StartCoroutine(LookAroundCoroutine());
						state = State.idle;
					}
					break;

				case State.prowl:
					if (AngleToTarget() > 1) RotateToDir(pathDirection);
					if (Vector3.Distance(transform.position, prowlPos) < .2f)
					{
						StopAgent(true);
						anim.SetBool("isDumbWalking", false);
						StartCoroutine(ProwlCoroutine());
						state = State.idle;
					}
					break;

				case State.attack:
					var targetDir = GetDirectionZeroY(attackTarget.transform.position); 
					if (Quaternion.Angle(transform.rotation, Quaternion.LookRotation(targetDir)) > 1) 
						RotateToDir(targetDir);

					break;

				case State.walkingToEat:
					if (isPause) break;
					if (AngleToTarget() > 1) RotateToDir(pathDirection);
					if (bodyToEat.tag == "zombie")
					{
						print(gameObject + " too late to eat " + bodyToEat);
						bodyToEat = null;
						state = State.idle;
						PatrolOrProwl();
						break;
					}

					if (Vector3.Distance(transform.position, bodyToEat.GetComponent<HumanScript>().torso.transform.position) < .95f)
					{
						var HScript = bodyToEat.GetComponent<HumanScript>();
						StopAgent(true);
						anim.SetBool("isWalking", false);
						if ((HScript.headObj.activeSelf && HScript.lifeFillImage.fillAmount > .8f)
							|| HScript.isEaten == true)
						{
							print(gameObject + " too late to eat " + bodyToEat);
							bodyToEat = null;
							state = State.idle;
							PatrolOrProwl();
						}
						else
						{
							print(gameObject + " start eating " + bodyToEat);
							anim.SetBool("isEating", true);
							bodyToEat.GetComponent<HumanScript>().isEaten = true;
							StartCoroutine(PauseBeforeEating());
							isEatPause = true;
							state = State.eat;
						}
					}

					break;

				case State.eat:
					if (isEatPause) break;
					if (!eatAudioSource.isPlaying)
					{
						eatAudioSource.volume = .2f * SoundManager.instance.masterSoundVolume;
						eatAudioSource.clip = SoundManager.instance.zombieEatingSounds[Random.Range(0, SoundManager.instance.zombieEatingSounds.Length)];
						eatAudioSource.Play();
					}
					hunger -= Time.deltaTime / 15;
					if (hunger < 0)
					{
						hunger = 0;
						StartCoroutine(CheckIfEatCoroutine());
						isEatPause = true;
					}
					break;


				case State.obstacleBreak:
					if (obstacle == null)
					{
						StartCoroutine(PauseAfterObstacleBreak());
						state = State.idle;
						break;
					}
					if (obstacle.tag == "door" && obstacle.GetComponent<DoorScript>().lockStrength > 0) RotateToDir(GetDirectionZeroY(obstacle.transform.position));
					if (animHit == "animDone")
					{
						print(obstacle.transform.position + " " + obstaclePos + " Distance " + Vector3.Distance(obstacle.transform.position, obstaclePos));
						var obstaclePosYzeroed = obstacle.transform.position;
						obstaclePosYzeroed.y = 0;
						if (Vector3.Distance(obstacle.transform.position, obstaclePos) > .18f || Vector3.Distance(transform.position, obstaclePosYzeroed) > 1.7f)
						{
							print("Stop breaking because of Distance " + obstacle + " <> zombie " + Vector3.Distance(transform.position, obstacle.transform.position));
							StartCoroutine(PauseAfterObstacleBreak());
							state = State.idle;
							break;
						}
						animHit = "";
						if (Random.Range(0, 2) == 0) anim.SetTrigger("fight A");
						else anim.SetTrigger("fight B");
					}
					else if (animHit != "")
					{
						animHit = "";
						if (obstacle.tag == "door")
						{
							var DScript = obstacle.GetComponent<DoorScript>();
							if (DScript.lockStrength > 0)
							{
								DScript.lockStrength -= attackStrength + Random.Range(0, attackStrength);
								if (DScript.lockStrength <= 0)
								{
									print("Lock break " + obstacle);
									DScript.LockBreak();
									var hit = GetDirectionZeroY(obstacle.transform.position);
									hit += hit * 2;
									obstacle.GetComponent<Rigidbody>().velocity = hit;
									StartCoroutine(PauseAfterObstacleBreak());
									state = State.idle;
								}
								else DScript.DoorHit();
							}
							else
							{
								DScript.doorStrength -= attackStrength + Random.Range(0, attackStrength);
								if (DScript.doorStrength <= 0)
								{
									GMS.DoorBreak(obstacle);
									StartCoroutine(PauseAfterObstacleBreak());
									state = State.idle;
								}
								else
								{
									var hit = GetDirectionZeroY(obstacle.transform.position);
									hit += hit * 2;
									obstacle.GetComponent<Rigidbody>().velocity = hit;
									DScript.DoorHit();
								}
							}
						}
						else
						{
							var FScript = obstacle.GetComponent<FurnitureScript>();
							if (FScript.furStrength > 0)
							{
								FScript.furStrength -= attackStrength + Random.Range(0, attackStrength);
								if (FScript.furStrength <= 0)
								{
									var hit = GetDirectionZeroY(obstacle.transform.position);
									obstacle.GetComponent<Rigidbody>().velocity = hit * 12 / obstacle.GetComponent<Rigidbody>().mass;
									GMS.FurnitureBreak(obstacle);
									StartCoroutine(PauseAfterObstacleBreak());
									state = State.idle;
								}
								else
								{
									var hit = GetDirectionZeroY(obstacle.transform.position);
									obstacle.GetComponent<Rigidbody>().velocity = hit * 12 / obstacle.GetComponent<Rigidbody>().mass;
									FScript.FurnitureHit();
								}
							}
						}
					}
					break; 
			}

		}
	}

/*	private void OnDrawGizmos()
	{
		if (_path.Count > 0 && agent)
		{
			Gizmos.color = Color.blue;
			for (var i = 0; i < _path.Count - 1; i++)
			{
				Gizmos.DrawLine(_path[i], _path[i + 1]);
			}
			Gizmos.color = Color.green;
			Gizmos.DrawSphere(path.corners[1], 0.2f);
		}
		Gizmos.color = Color.red;
		Gizmos.DrawLine(transform.position, transform.position + transform.forward);
	}*/

	void LateUpdate()
	{
		if (!knockdown) DrawFieldOfView();
	}

	private void OnCollisionEnter(Collision other) 
	{
		if ((other.collider.tag == "human" || other.collider.tag == "cola") && state != State.attack && state != State.chase && !knockdown && !doCollisionOnce)
		{
			print(other.gameObject + " touched " + gameObject);
			doCollisionOnce = true;
			StartCoroutine(ReleaseDoCollisionOnce());
			StopAgent(true);
			StartCoroutine(CheckIfEatCoroutine());
			lastKnownTargetPos = LastKnownTargetPosNavMesh(other.transform.position);
			anim.SetBool("isDumbWalking", false);
			anim.SetBool("isWalking", true);
			rotateDirection = GetDirectionZeroY(other.gameObject.transform.position);
			state = State.rotate;
		}
		else if (other.collider.tag == "zombie" && state == State.checkPos && !doCollisionOnce)
		{
			print(other.gameObject + " touched " + gameObject);
			doCollisionOnce = true;
			StartCoroutine(ReleaseDoCollisionOnce());
			Vector3 objPos = new Vector3(transform.position.x, 1, transform.position.z);
			Vector3 dirToTarget = (lastKnownTargetPos - transform.position).normalized;
			float dstToTarget = Vector3.Distance(objPos, lastKnownTargetPos);
			if (Physics.Raycast(objPos, dirToTarget, dstToTarget, GMS.zombieMask)
				|| Vector3.Distance(transform.position, lastKnownTargetPos) < .7f) StopCheckPosZombieOnTheWay(other.gameObject);
			else
			{
				var resolverS = gameObject.AddComponent<ZombieCollisionResolver>();
				resolverS.GetComponent<ZombieCollisionResolver>().col = other.collider;
			}
		}
		else if (state == State.prowl)
		{
			StopProwl();
		}
	}

	private IEnumerator ReleaseDoCollisionOnce()
	{
		yield return new WaitForSeconds(.2f);
		doCollisionOnce = false;
	}

	private float AngleToTarget() 
	{
		pathDirection = GetDirectionZeroY(agent.steeringTarget);
		if (pathDirection == Vector3.zero && path.corners.Length > 0) pathDirection = GetDirectionZeroY(path.corners[1]);
		float angle = Quaternion.Angle(transform.rotation, Quaternion.LookRotation(pathDirection));
		return angle;
	}

	private void RotateToDir(Vector3 dirTo)
	{
		Vector3 newDirection = Vector3.RotateTowards(transform.forward, dirTo, agent.speed * 2 * Time.deltaTime, 0.0f);
		transform.rotation = Quaternion.LookRotation(newDirection);
	}

	private Vector3 GetDirectionZeroY(Vector3 target)
	{
		Vector3 dir = (target - transform.position).normalized;
		dir.y = 0;
		return dir;
	}

	private IEnumerator PatrolCoroutine()
	{
		print("PatrolCoroutine() " + gameObject);
		yield return new WaitForSeconds(Random.Range(2, 3.5f));
		if (!knockdown && state == State.idle)
		{
			agent.CalculatePath(targetWP, path);
			yield return null;
			//PathList();
			if (path.corners.Length > 0) rotateDirection = GetDirectionZeroY(path.corners[1]);
			else rotateDirection = GetDirectionZeroY(targetWP);
			agent.speed = SetNMSpeed(speed / 2.5f);
			anim.SetBool("isDumbWalking", true);
			state = State.rotate;
		}
	}

	private IEnumerator LookAroundCoroutine()
	{
		print("LookAroundCoroutine()");
		state = State.idle;
		anim.SetBool("isLookAround", true);
		yield return new WaitForSeconds(4.7f);
		anim.SetBool("isLookAround", false);
		if (!knockdown && state == State.idle)
		{
			isCheckPos = false;
			PatrolOrProwl();
		}
	}

	private IEnumerator ChaseCoroutine()
	{
		print("ChaseCoroutine() lastKnownTargetPos " + lastKnownTargetPos);
		yield return new WaitForSeconds(.2f);
		agent.CalculatePath(lastKnownTargetPos, path);
		yield return null;
		//PathList();
		if (path.corners.Length > 0) pathDirection = GetDirectionZeroY(path.corners[1]);
		agent.speed = SetNMSpeed(speed * 2);
		anim.SetBool("isRunning", true);
		StopAgent(false);
		agent.SetPath(path);
		isPause = false;
	}

	private void CheckPos()
	{
		print(gameObject + " CheckPos() lastKnownTargetPos " + lastKnownTargetPos);
		targetToChase = null;
		rotateDirection = GetDirectionZeroY(lastKnownTargetPos);
		agent.speed = SetNMSpeed(speed);
		anim.SetBool("isLookAround", false);
		anim.SetBool("isDumbWalking", false);
		anim.SetBool("isRunning", false);
		anim.SetBool("isWalking", true);
		state = State.rotate;
	}

	private IEnumerator ProwlCoroutine()
	{
		yield return new WaitUntil(() => GMS.navMeshSurface != null);
		prowlPos = GMS.RandomNavSphere(transform.position, 5, 1);
		prowlPos.y = 0;
		print(gameObject + " ProwlingCoroutine() prowlPos " + prowlPos);
		if (prowlPos == Vector3.zero)
		{
			PatrolOrProwl();
			yield break;
		}
		yield return new WaitForSeconds(Random.Range(2f, 4f));
		if (!knockdown && state == State.idle)
		{
			agent.CalculatePath(prowlPos, path);
			yield return null;
			//PathList();
			if (path.corners.Length > 0) rotateDirection = GetDirectionZeroY(path.corners[1]);
			else
			{
				PatrolOrProwl();
				yield break;
			}
			agent.speed = SetNMSpeed(speed / 2.5f);
			anim.SetBool("isDumbWalking", true);
			prowlTimer = 0;
			state = State.rotate;
		}
	}

	private IEnumerator WalkingToEatCoroutine()
	{
		Vector3 pos = bodyToEat.GetComponent<HumanScript>().torso.transform.position;
		pos.y = 0;
		print("WalkingToEatCoroutine() bodyToEat position " + pos);
		agent.SetDestination(pos);
		yield return null;
		//PathList();
		if (path.corners.Length > 0) pathDirection = GetDirectionZeroY(path.corners[1]);
		else pathDirection = GetDirectionZeroY(pos);
		agent.speed = SetNMSpeed(speed);
		anim.SetBool("isDumbWalking", false);
		anim.SetBool("isWalking", true);
		StopAgent(false);
		isPause = false;
	}

	private IEnumerator PauseBeforeEating()
	{
		float timer = 0;
		var bodyToEatPos = GetDirectionZeroY(bodyToEat.GetComponent<HumanScript>().torso.transform.position);
		while (timer < .8f)
		{
			if (Quaternion.Angle(transform.rotation, Quaternion.LookRotation(bodyToEatPos)) > 1) RotateToDir(bodyToEatPos);
			timer += Time.deltaTime;
			defaultViewRadius = Mathf.Lerp(5, 2.5f, timer / .8f);
			viewRadius = defaultViewRadius * HungerMultiplier();
			yield return new WaitForEndOfFrame();
		}
		if (state == State.eat)
		{
			isEatPause = false;
		}
	}

	private IEnumerator PauseAfterObstacleBreak()
	{
		obstacle = null;
		yield return new WaitUntil(() => animHit == "animDone");
		if (!knockdown && state == State.idle)
		{
			if (isCheckPos)
			{
				lastKnownTargetPos = LastKnownTargetPosNavMesh(transform.position + transform.forward * 3f);
				CheckPos();
			}
			else PatrolOrProwl();
		}
	}

	public IEnumerator ReanimationCoroutine()
	{
		yield return new WaitForSeconds(.2f);
		anim.SetTrigger("wakeUp");
		torso.GetComponent<BoxCollider>().isTrigger = false;
		if (anim.GetCurrentAnimatorStateInfo(0).IsName("Death D")) transform.forward = transform.forward * -1;
		yield return new WaitForSeconds(3.8f);
		mainCollider.enabled = true;
		torso.GetComponent<NavMeshModifierVolume>().enabled = false;
		GMS.CheckRebake();
		agent.enabled = true;
		viewMeshFilter.gameObject.SetActive(true);
		knockdown = false;
		StartCoroutine(LookAroundCoroutine());

		yield return new WaitForEndOfFrame();
	}

	private void CheckHearSounds()
	{
		if (listOfHearedObjects.Count > 0)
		{
			GameObject nearestTarget = GMS.ClosestObject(gameObject, listOfHearedObjects);
			if (!isCheckPos || (isCheckPos && hearSign == null))
			{
				if (state == State.chase && targetToChase != null)
				{
					print("chase && hear targetToChase");
					if (hearSign == null) StartCoroutine(HearSomethingSign());
					agent.SetDestination(nearestTarget.transform.position);
				}
				else
				{
					isCheckPos = true;
					print(gameObject + " hears " + nearestTarget);
					StopAgent(true);
					StartCoroutine(HearSomethingSign());
					if (nearestTarget.tag == "door") lastKnownTargetPos = LastKnownTargetPosNavMesh(nearestTarget.GetComponent<DoorScript>().realPos);
					else lastKnownTargetPos = LastKnownTargetPosNavMesh(nearestTarget.transform.position);
					StartCoroutine(CheckIfEatCoroutine());
					CheckPos();
				}
			}
		}
	}

	private IEnumerator HearSomethingSign()
	{
		if (hearSign == null)
		{
			SoundManager.instance.RandomizeSfx(.2f, SoundManager.instance.zombieHearSounds);
			hearSign = Instantiate(AH.hearSignPrefab, transform.position + new Vector3(0f, 2.2f, .4f), Quaternion.identity);
			hearSign.transform.LookAt(Camera.main.transform.position);
			hearSign.transform.eulerAngles = new Vector3(hearSign.transform.eulerAngles.x, -182, 0);
			yield return new WaitForSeconds(.5f);
			Color originalColor = hearSign.GetComponentInChildren<SpriteRenderer>().color;
			for (float t = 0.01f; t < .8f; t += Time.deltaTime)
			{
				hearSign.GetComponentInChildren<SpriteRenderer>().color = Color.Lerp(originalColor, Color.clear, Mathf.Min(1, t / .8f));
				yield return null;
			}
			Destroy(hearSign);
		}
	}

	public IEnumerator KnockedDownCoroutine(string hand)
	{
		knockdown = true;
		StopAgent(true);
		state = State.idle;
		StopAllMovingAnims();
		lastKnownTargetPos = Vector3.zero;
		isCheckPos = false;
		GMS.TextPopup(1, gameObject, GMS.Txt("Speech/Knockdown"), new Color(1, .236f, .222f));
		yield return new WaitForSeconds(.2f);
		SoundManager.instance.RandomizeSfx(.7f, SoundManager.instance.zombieHurtSounds);
		viewMeshFilter.gameObject.SetActive(false);
		attackTarget = null;
		RandomDeath(hand);
		mainCollider.enabled = false;
		yield return new WaitForSeconds(3.5f);
		SoundManager.instance.RandomizeSfx(.2f, SoundManager.instance.zombieHearSounds);
		anim.SetTrigger("wakeUp");
		torso.GetComponent<BoxCollider>().isTrigger = false;
		if (anim.GetCurrentAnimatorStateInfo(0).IsName("Death D")) transform.forward = transform.forward * -1;
		yield return new WaitForSeconds(2.7f);
		mainCollider.enabled = true;
		torso.GetComponent<NavMeshModifierVolume>().enabled = false;
		GMS.CheckRebake();
		viewMeshFilter.gameObject.SetActive(true);
		knockdown = false;
		StartCoroutine(LookAroundCoroutine());
	}

	public IEnumerator HeadShotCoroutine(string hand)
	{
		knockdown = true;
		isDead = true;
		agent.enabled = false;
		GMS.TextPopup(2, gameObject, GMS.Txt("Speech/Headshot"), new Color(1, .236f, .222f));
		headObj.SetActive(false);
		var particle = Instantiate(AH.headShotParticle, transform.position + new Vector3(0, .95f, 0), transform.rotation);
		Destroy(particle, 3);
		yield return new WaitForSeconds(.2f);
		RandomDeath(hand);
		mainCollider.enabled = false;
		StopAllCoroutines();
		yield return new WaitForSeconds(3);
		this.enabled = false;
	}

	public IEnumerator ZombieBiteCoroutine(GameObject target)
	{
		var targetScript = target.GetComponent<HumanScript>();
		targetScript.mainCollider.enabled = false;
		targetScript.torso.GetComponent<BoxCollider>().enabled = true;
		anim.SetTrigger("bite");
		targetScript.StopAllAttackAnims();
		SoundManager.instance.RandomizeSfx(.5f, SoundManager.instance.zombieBiteSounds);
		yield return new WaitForSeconds(1.2f);
		if (targetScript.bittenState == 0) targetScript.headModel.GetComponent<MeshRenderer>().material = AH.deadCitizensMat[0];
		else if (targetScript.bittenState == 1) targetScript.headModel.GetComponent<MeshRenderer>().material = AH.deadCitizensMat[1];
		else targetScript.headModel.GetComponent<MeshRenderer>().material = AH.deadCitizensMat[2];
		state = State.idle;
		attackTarget = null;
		lastKnownTargetPos = Vector3.zero;
		targetScript.emojiImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
		targetScript.emojiImage.sprite = AH.emojiSprites[3];
		targetScript.RandomDeath();
		StartCoroutine(targetScript.HealthBarMoveCoroutine());
		yield return new WaitForSeconds(2f); 
		PatrolOrProwl();
	}

	private IEnumerator CheckIfEatCoroutine()
	{
		if (state == State.eat && bodyToEat != null)
		{
			bodyToEat.GetComponent<HumanScript>().isEaten = false;
			bodyToEat = null;
			anim.SetBool("isEating", false);
			eatAudioSource.Stop();
			float timer = 0;
			while (timer < .5f)
			{
				timer += Time.deltaTime;
				defaultViewRadius = Mathf.Lerp(2.5f, 5f, timer / .5f);
				viewRadius = defaultViewRadius * HungerMultiplier();
				yield return new WaitForEndOfFrame();
			}
			yield return new WaitForSeconds(1.2f);
			if (state == State.eat)
			{
				if (zombieName.Contains("Fat") || zombieName.Contains("Big")) eatAudioSource.clip = SoundManager.instance.zombieBurpSounds[3];
				else eatAudioSource.clip = SoundManager.instance.zombieBurpSounds[Random.Range(0, 3)];
				eatAudioSource.volume = .7f * SoundManager.instance.masterSoundVolume;
				eatAudioSource.Play();
				state = State.idle;
				PatrolOrProwl();
			}
		}
	}

	public void CheckPosWalkTo(Vector3 pos)
	{
		print(gameObject + " checkPos walk to " + pos);
		lastKnownTargetPos = LastKnownTargetPosNavMesh(pos);
		isCheckPos = true;
		CheckPos();
	}

	public void ChaseRunTo(Vector3 pos)
	{
		isPause = true;
		isCheckPos = true;
		state = State.chase;
		lastKnownTargetPos = LastKnownTargetPosNavMesh(pos);
		StartCoroutine(ChaseCoroutine());
	}


	// ---------------------------------
	private IEnumerator FindTargetsWithDelay()
	{
		yield return new WaitForSeconds(Random.value);
		Vector3 prowlPos = Vector3.zero;
		while (true)
		{
			yield return new WaitForSeconds(.1f);
			if ((state == State.chase || state == State.checkPos || state == State.patrol || state == State.walkingToEat)
				&& AngleToTarget() < 3 && !knockdown) CheckObstaclesFOV();
			yield return new WaitForSeconds(.1f);
			if (!knockdown)
			{
				if (state != State.attack) CheckFOV(); 
				if (state != State.attack && memoryOftargetToChase < 5) CheckHearSounds();
			}
			viewRadius = defaultViewRadius * HungerMultiplier();
			listOfHearedObjects.Clear();
			if (memoryOftargetToChase == 0) targetToChase = null;
			memoryOftargetToChase--;

			if (state != State.eat) hunger += .0006666f;
			if (hunger > 1) hunger = 1;

			if (state == State.prowl && !knockdown)
			{
				if (prowlTimer == 0) prowlPos = transform.position;
				prowlTimer++;
				if (prowlTimer > 3)
				{
					prowlTimer = 0;
					if (Vector3.Distance(prowlPos, transform.position) < .2f) StopProwl();
				}
			}
		}
	}

	//Проверка, в поле ли зрения игрок или нет
	private void CheckFOV()
	{
		visibleTargets.Clear();
		var targetsToEat = new List<GameObject>();
		Transform target;
		Collider[] targetsInViewRadius = Physics.OverlapSphere(viewMeshFilter.transform.position, viewRadius, targetMask);
		for (int i = 0; i < targetsInViewRadius.Length; i++)
		{
			if (targetsInViewRadius[i].tag == "human") target = targetsInViewRadius[i].transform.parent;
			else target = targetsInViewRadius[i].transform.parent.parent.parent.parent;
			Vector3 targetPos = target.position;
			if (target.tag == "human")
			{
				if (target.GetComponent<HumanScript>().attacked) continue;
				targetPos.y = 1f;
			}
			else if (target.gameObject == bodyToEat || !target.GetComponent<HumanScript>().readyToEat
				|| state == State.eat || state == State.chase || state == State.walkingToEat || state == State.obstacleBreak || isCheckPos
				|| hunger < .5f || target.GetComponent<HumanScript>().isEaten
				|| (target.GetComponent<HumanScript>().headObj.activeSelf &&
				target.GetComponent<HumanScript>().lifeFillImage.fillAmount > .7f)
				|| Vector3.Distance(transform.position, target.transform.position) < 1.1f)
				continue;
			Vector3 dirToTarget = (targetPos - viewMeshFilter.transform.position).normalized;
			Vector2 V2from = new Vector2(viewMeshFilter.transform.forward.x, viewMeshFilter.transform.forward.z);
			Vector2 V2to = new Vector2(dirToTarget.x, dirToTarget.z);
			if (Vector2.Angle(V2from, V2to) < viewAngle / 2)
			{
				float dstToTarget = Vector3.Distance(viewMeshFilter.transform.position, targetPos);
				if (!Physics.Raycast(viewMeshFilter.transform.position, dirToTarget, dstToTarget, GMS.obstacleVisualMask))
				{
					if (target.tag == "human") visibleTargets.Add(target.gameObject);
					else targetsToEat.Add(target.GetComponent<HumanScript>().torso);  
				}
			}
		}

		if (visibleTargets.Count > 0)
		{
			GameObject oldTargetToChase = targetToChase;
			targetToChase = GMS.ClosestObject(gameObject, visibleTargets);
			lastKnownTargetPos = LastKnownTargetPosNavMesh(targetToChase.transform.position);
			memoryOftargetToChase = 5;

			if (state != State.chase && targetToChase != oldTargetToChase)
			{
				isPause = true;
				StopAgent(true);
				print("Got'cha!!! " + "targets count " + visibleTargets.Count);
				eatAudioSource.Stop();
				if (hearSign == null) SoundManager.instance.RandomizeSfx(.5f, SoundManager.instance.zombieChaseSounds);
				StartCoroutine(CheckIfEatCoroutine());
				anim.SetBool("isLookAround", false);
				StopAllMovingAnims();
				isCheckPos = true;
				state = State.chase;
				StartCoroutine(ChaseCoroutine());
			}
			else agent.SetDestination(lastKnownTargetPos);
		}
		else if (targetsToEat.Count > 0)
		{
			bodyToEat = GMS.ClosestObject(gameObject, targetsToEat).transform.parent.parent.parent.parent.gameObject;
			print("Braaiins!!! targets count " + targetsToEat.Count);
			anim.SetBool("isLookAround", false);
			isPause = true;
			StopAgent(true);
			state = State.walkingToEat;
			StartCoroutine(WalkingToEatCoroutine());
		}
	}

	private void CheckObstaclesFOV()
	{
		List<GameObject> visibleDestroyableObstacles = new List<GameObject>();
		Vector3 viewPos = new Vector3(transform.position.x, .3f, transform.position.z);
		Vector3 viewPosL = new Vector3(leftThigh.transform.position.x, .3f, leftThigh.transform.position.z);
		Vector3 viewPosR = new Vector3(rightThigh.transform.position.x, .3f, rightThigh.transform.position.z);
		Collider[] obstaclesClose = Physics.OverlapSphere(viewPos, 1.5f, destroyableObstacleMask);
		for (int i = 0; i < obstaclesClose.Length; i++)
		{
			GameObject obstacle = obstaclesClose[i].gameObject;
			if (obstacle.tag == "door") obstacle = obstacle.transform.parent.gameObject;
			Vector3 obstaclePos = new Vector3(obstacle.transform.position.x, .3f, obstacle.transform.position.z);
			Vector3 dirToTarget = transform.forward;
			dirToTarget = new Vector3(dirToTarget.x, .3f, dirToTarget.z);
//			Vector2 V2from = new Vector2(transform.forward.x, transform.forward.z);
//			Vector2 V2to = new Vector2(dirToTarget.x, dirToTarget.z);
//			if (Vector2.Angle(V2from, V2to) < 75)
//			{
			float dstToTarget = Vector3.Distance(viewPos, obstaclePos);
			if (Physics.Raycast(viewPos, dirToTarget, dstToTarget, destroyableObstacleMask) ||
			Physics.Raycast(viewPosL, dirToTarget, dstToTarget, destroyableObstacleMask) ||
			Physics.Raycast(viewPosR, dirToTarget, dstToTarget, destroyableObstacleMask))
			{
				visibleDestroyableObstacles.Add(obstacle);
			}

//			}
		}

		if (visibleDestroyableObstacles.Count > 0)
		{
			print("visibleDestroyableObstacles.Count " + visibleDestroyableObstacles.Count);
			GameObject nearest = GMS.ClosestObject(gameObject, visibleDestroyableObstacles);
			Vector3 nearestPos = nearest.transform.position;
			nearestPos.y = 0;

			if (nearest.tag == "door" && (state == State.chase || state == State.checkPos) && memoryOftargetToChase < 4 &&
				nearest.GetComponent<DoorScript>().lockStrength > 0	&& Vector3.Distance(transform.position, nearestPos) < 1.1f)
			{
				print("StartObstacleBreak() because state == chase || checkPos");
				obstacle = nearest;
				StartObstacleBreak();
			}
			else
			{
				if (obstacle == nearest)
				{
					if (Vector3.Distance(zombiePos, transform.position) > .25f)
					{
						print("zombiePos Distance too long " + Vector3.Distance(zombiePos, transform.position) + " obstacleTimer = 0");
						obstacleTimer = 0;
						zombiePos = transform.position;
					}
					obstacleTimer++;
					if (obstacleTimer >= 5)
					{
						print("StartObstacleBreak() because zombiePos Distance too short " + Vector3.Distance(zombiePos, transform.position));
						obstacleTimer = 0;
						StartObstacleBreak();
					}
				}
				else obstacleTimer = 0;

				if (obstacleTimer == 0)
				{
					obstacle = nearest;
					zombiePos = transform.position;
				}
			}
		}
	}

	public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
	{
		if (!angleIsGlobal)
		{
			angleInDegrees += transform.eulerAngles.y;
		}
		return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
	}

	void DrawFieldOfView()
	{
		int stepCount = Mathf.RoundToInt(viewAngle * meshResolution);
		float stepAngleSize = viewAngle / stepCount;
		List<Vector3> viewPoints = new List<Vector3>();
		ViewCastInfo oldViewCast = new ViewCastInfo();
		for (int i = 0; i <= stepCount; i++)
		{
			float angle = viewMeshFilter.transform.eulerAngles.y - viewAngle / 2 + stepAngleSize * i;
			ViewCastInfo newViewCast = ViewCast(angle);

			if (i > 0)
			{
				bool edgeDstThresholdExceeded = Mathf.Abs(oldViewCast.dst - newViewCast.dst) > edgeDstThreshold;
				if (oldViewCast.hit != newViewCast.hit || (oldViewCast.hit && newViewCast.hit && edgeDstThresholdExceeded))
				{
					EdgeInfo edge = FindEdge(oldViewCast, newViewCast);
					if (edge.pointA != Vector3.zero)
					{
						viewPoints.Add(edge.pointA);
					}
					if (edge.pointB != Vector3.zero)
					{
						viewPoints.Add(edge.pointB);
					}
				}

			}


			viewPoints.Add(newViewCast.point);
			oldViewCast = newViewCast;
		}

		int vertexCount = viewPoints.Count + 1;
		Vector3[] vertices = new Vector3[vertexCount];
		int[] triangles = new int[(vertexCount - 2) * 3];

		vertices[0] = Vector3.zero;
		for (int i = 0; i < vertexCount - 1; i++)
		{
			vertices[i + 1] = viewMeshFilter.transform.InverseTransformPoint(viewPoints[i]);

			if (i < vertexCount - 2)
			{
				triangles[i * 3] = 0;
				triangles[i * 3 + 1] = i + 1;
				triangles[i * 3 + 2] = i + 2;
			}
		}

		viewMesh.Clear();

		viewMesh.vertices = vertices;
		viewMesh.triangles = triangles;
		viewMesh.RecalculateNormals();
	}

	EdgeInfo FindEdge(ViewCastInfo minViewCast, ViewCastInfo maxViewCast)
	{
		float minAngle = minViewCast.angle;
		float maxAngle = maxViewCast.angle;
		Vector3 minPoint = Vector3.zero;
		Vector3 maxPoint = Vector3.zero;

		for (int i = 0; i < edgeResolveIterations; i++)
		{
			float angle = (minAngle + maxAngle) / 2;
			ViewCastInfo newViewCast = ViewCast(angle);

			bool edgeDstThresholdExceeded = Mathf.Abs(minViewCast.dst - newViewCast.dst) > edgeDstThreshold;
			if (newViewCast.hit == minViewCast.hit && !edgeDstThresholdExceeded)
			{
				minAngle = angle;
				minPoint = newViewCast.point;
			}
			else
			{
				maxAngle = angle;
				maxPoint = newViewCast.point;
			}
		}

		return new EdgeInfo(minPoint, maxPoint);
	}


	ViewCastInfo ViewCast(float globalAngle)
	{
		Vector3 dir = DirFromAngle(globalAngle, true);
		RaycastHit hit;

		if (Physics.Raycast(viewMeshFilter.transform.position, dir, out hit, viewRadius, GMS.obstacleVisualMask))
		{
			return new ViewCastInfo(true, hit.point, hit.distance, globalAngle);
		}
		else
		{
			return new ViewCastInfo(false, viewMeshFilter.transform.position + dir * viewRadius, viewRadius, globalAngle);
		}
	}

	public struct ViewCastInfo
	{
		public bool hit;
		public Vector3 point;
		public float dst;
		public float angle;

		public ViewCastInfo(bool _hit, Vector3 _point, float _dst, float _angle)
		{
			hit = _hit;
			point = _point;
			dst = _dst;
			angle = _angle;
		}
	}

	public struct EdgeInfo
	{
		public Vector3 pointA;
		public Vector3 pointB;

		public EdgeInfo(Vector3 _pointA, Vector3 _pointB)
		{
			pointA = _pointA;
			pointB = _pointB;
		}
	}
	//---------------------------------------------------------------


	private void RandomDeath(string hand)
	{
		if (hand == "left")
		{
			int rnd = Random.Range(0, 2);
			if (rnd == 0) anim.SetTrigger("death A");
			else anim.SetTrigger("death D");
		}
		else
		{
			int rnd = Random.Range(0, 3);
			if (rnd == 0) anim.SetTrigger("death A");
			else if (rnd == 1) anim.SetTrigger("death B");
			else anim.SetTrigger("death C");
		}
	}

	public void WalkSound(string param)
	{
		if (param == "right")
		{
			if (haveRightBoot) SoundManager.instance.RandomizeSfx
				(.5f, SoundManager.instance.Z_WalkBootsSounds[0], SoundManager.instance.Z_WalkBootsSounds[2]);
			else SoundManager.instance.RandomizeSfx
				(1, SoundManager.instance.Z_WalkBareSounds[0], SoundManager.instance.Z_WalkBareSounds[2]);
		}
		else
		{
			if (haveLeftBoot)
				SoundManager.instance.RandomizeSfx
			  (.5f, SoundManager.instance.Z_WalkBootsSounds[1], SoundManager.instance.Z_WalkBootsSounds[3]);
			else SoundManager.instance.RandomizeSfx
			  (1, SoundManager.instance.Z_WalkBareSounds[1], SoundManager.instance.Z_WalkBareSounds[3]);
		}
	}

	public void RunSound(string param)
	{
		if (param == "right")
		{
			if (haveRightBoot) SoundManager.instance.RandomizeSfx
				(1f, SoundManager.instance.Z_RunBootsSounds[0], SoundManager.instance.Z_RunBootsSounds[2]);
			else SoundManager.instance.RandomizeSfx
				(1, SoundManager.instance.Z_RunBareSounds[0], SoundManager.instance.Z_RunBareSounds[2]);
		}
		else
		{
			if (haveLeftBoot)
				SoundManager.instance.RandomizeSfx
			  (1f, SoundManager.instance.Z_RunBootsSounds[1], SoundManager.instance.Z_RunBootsSounds[3]);
			else SoundManager.instance.RandomizeSfx
			  (1, SoundManager.instance.Z_RunBareSounds[1], SoundManager.instance.Z_RunBareSounds[3]);
		}
	}

	public void BodyFallSound()
	{
		if (anim.GetCurrentAnimatorStateInfo(0).IsName("Death D"))
			SoundManager.instance.RandomizeSfx(1, SoundManager.instance.bodyFallSoundsHeavy);
		else SoundManager.instance.RandomizeSfx(1, SoundManager.instance.bodyFallSounds);
		if (isDead) StartCoroutine(BloodSpill());
	}

	public void DamageAnimDone()
	{
		if (!knockdown && state == State.attack) anim.SetTrigger("hold");
	}

	public void DeathAnimDone()
	{
		if (isDead)
		{
			gameObject.tag = "zombieBody";
			torso.tag = "zombieBody";
		}
		torso.GetComponent<BoxCollider>().isTrigger = true;
		torso.GetComponent<NavMeshModifierVolume>().enabled = true;
		GMS.CheckRebake();
	}

	public void AttackAnimationHit(string hand)
	{
		animHit = hand;
	}

/*	private void PathList()
	{
		_path.Clear();
		for (int i = 0; i < path.corners.Length; i++) _path.Add(path.corners[i]);
	}*/

	private void StopAllMovingAnims()
	{
		anim.SetBool("isDumbWalking", false);
		anim.SetBool("isWalking", false);
		anim.SetBool("isRunning", false);
	}

	public void StartObstacleBreak()
	{
		print(gameObject + " start breaking " + obstacle);
		StopAgent(true);
		StopAllMovingAnims();
		obstaclePos = obstacle.transform.position;
		state = State.obstacleBreak;
	}

	public void StopAgent(bool isStopped)
	{
		agentIsStopped = isStopped;
		agent.isStopped = isStopped;
	}

	private float SetNMSpeed(float multiplier)
	{
		float s = defaultNMSpeed * multiplier;
		return s * HungerMultiplier();
	}

	public float HungerMultiplier()
	{
		return Mathf.Lerp(.7f, 1f, hunger); 
	}

	public void PatrolOrProwl()
	{
		if (waypoints.Length > 0) StartCoroutine(PatrolCoroutine());
		else if (GMS.zombieList.Count < 7) StartCoroutine(ProwlCoroutine());
	}

	private void StopProwl()
	{
		print(gameObject + " stop prowl");
		StopAgent(true);
		anim.SetBool("isDumbWalking", false);
		StartCoroutine(ProwlCoroutine());
		state = State.idle;
	}

	private IEnumerator BloodSpill()
	{
		yield return new WaitForSeconds(.5f);
		var paddle = Instantiate(AH.bloodPuddlePrefab, headObj.transform.position, Quaternion.identity);
		paddle.transform.SetParent(GameObject.Find("EnviromentStatic/Blood").transform);
		paddle.GetComponent<BloodPaddleScript>().isHuman = false;
	}

	private Vector3 LastKnownTargetPosNavMesh(Vector3 pos)
	{
		NavMeshHit navHit;
		NavMesh.SamplePosition(pos, out navHit, 5f, 1);
		Vector3 result = navHit.position;
		result.y = 0;
		return result;
	}

	public void StopCheckPosZombieOnTheWay(GameObject zombie)
	{
		print(zombie + " on the way.");
		StopAgent(true);
		anim.SetBool("isWalking", false);
		lastKnownTargetPos = Vector3.zero;
		isCheckPos = false;
		state = State.idle;
		PatrolOrProwl();
	}

	private IEnumerator CheckPosBreakerCoroutine()
	{
		float timer = 0;
		while (timer < 1)
		{
			if (state != State.checkPos)
			{
				checkPosBreaker = false;
				yield break;
			}
			timer += Time.deltaTime;
			yield return new WaitForEndOfFrame();
		}
		StopCheckPosZombieOnTheWay(null);
		checkPosBreaker = false;
	}
}

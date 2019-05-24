using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class PlayerController : MonoBehaviour {
    public float speed;
    public Text countText;
    public Text winText;
    public Text timeText;
    private Rigidbody rb;
    public Toggle endlessToggle;
    private int count = 0;
    public int initialPickupNum = 12;
    public string wallTagName = "Wall";
    public bool isMazeMode = false;
    public Toggle MazeToggle;
    public Toggle MuteToggle;
    public GameObject Maze;
    public GameObject ControlsGroup;
    void MazeToggle_ValueChanged(bool isChecked) {
        if (isMazeMode == isChecked) return;
        isMazeMode = isChecked;
        Reinitialize();
    }
    public Vector3 initialPostiton;
    private void Reinitialize() {
        transform.localPosition = initialPostiton;
        foreach (var pickup in pickups) {
            DestroyPickup(pickup);
        }
        pickups.Clear();
        timeSum = 0;
        count = 0;
        SetCountText();
        winText.enabled = false;
        Maze.SetActive(isMazeMode);
        if (isMazeMode) {
            for (int i = 0; i < initialPickupNum; i++) {
                RandomlySpawnPickup();
            }
        } else {
            var angular = 2 * Math.PI / initialPickupNum;
            var spawnRadius = radius / 2;
            for (int i = 0; i < initialPickupNum; i++) {
                SpawnPickup(new Vector3(
                    (float)(spawnRadius * Math.Cos(angular * i)),
                    positionY,
                    (float)(spawnRadius * Math.Sin(angular * i))
                ), rd.Next(0, 5) == 0);
            }
        }
    }
    
    private AudioSource audioSource;
    private bool canVibrate;
    #if UNITY_ANDROID
    private readonly bool nonSense = false;
    private AndroidJavaObject vibrator;
    #endif
    private void Vibrate(long milliseconds) {
        #if UNITY_ANDROID
        vibrator.Call("vibrate", milliseconds);
        #endif
    }
    void Start() {
        canVibrate = SystemInfo.supportsVibration;
        #if UNITY_ANDROID
        if (nonSense) Handheld.Vibrate(); // 这个API不能设置振动时间，仅用来请求权限
        var activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        var activity = activityClass.GetStatic<AndroidJavaObject>("currentActivity");
        if (activity != null) {
            var vibratorServiceTag = activity.GetStatic<AndroidJavaObject>("VIBRATOR_SERVICE");
            vibrator = activity.Call<AndroidJavaObject>("getSystemService", vibratorServiceTag);
        }
        #endif
        initialPostiton = transform.localPosition;
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        endlessToggle.isOn = isEndlessMode;
        endlessToggle.onValueChanged.AddListener(endlessToggle_ValueChanged);
        MazeToggle.isOn = isMazeMode;
        MazeToggle.onValueChanged.AddListener(MazeToggle_ValueChanged);
        Reinitialize();
    }

    //joystick
    public GameObject joystick = null;
    public GameObject thumb = null;
    public Vector2 thumbWeight;
    public float k;
    private float timeSum;
    void FixedUpdate() {
        if (rb == null) {
            rb = GetComponent<Rigidbody>();
            if (rb == null) return;
        }
        if (!rb.useGravity) return;
        float moveHorizontal, moveVertical;
        if (joystick != null && thumb != null) {
            moveHorizontal = (joystick.transform.position.x - thumb.transform.position.x) * thumbWeight.x;
            moveVertical = (joystick.transform.position.y - thumb.transform.position.y) * thumbWeight.y;
        } else {
            moveHorizontal = Input.GetAxis("Horizontal");
            moveVertical = Input.GetAxis("Vertical");
        }
        var movement = new Vector3(moveHorizontal, 0.0f, moveVertical);
        var ratio = 1f - ((float)Math.Min(count / 100, initialPickupNum)) / initialPickupNum;
        rb.AddForce(movement * speed);
        rb.AddForce(rb.velocity * rb.velocity.magnitude * -k * ratio);
        timeSum += Time.deltaTime;
        var minute = ((int)timeSum % 3600) / 60;
        var second = ((int)timeSum) % 60;
        timeText.text = String.Format("Time: {0:D2}:{1:D2}", minute, second);
    }
    public bool isEndlessMode = false;
    void endlessToggle_ValueChanged(bool isChecked) {
        isEndlessMode = isChecked;
        EnsureEndlessMode();
    }
    void EnsureEndlessMode() {
        if (isEndlessMode) {
            winText.enabled = false;
            if (pickups.Count == 0) {
                RandomlySpawnPickup();
            }
        } else {
            if (pickups.Count == 0) {
                winText.enabled = true;
            }
        }
    }
    void DestroyPickup(GameObject pickup) {
        pickup.SetActive(false);
        GameObject.Destroy(pickup, 2.0f);
    }
    public GameObject particlePrefab;
    public GameObject highScoreParticlePrefab;
    public Vector3 particleOffset;
    public GameObject particleContainer;
    public float particleScale = 1;
    void OnTriggerEnter(Collider other) {
        var isHighScore = other.gameObject.CompareTag(highScorePickUpPrefab.tag);
        if (isHighScore || other.gameObject.CompareTag(pickUpPrefab.tag)) {
            if (isHighScore) { // 随机传送
                var offset = radius * precision;
                var floatPrecision = (float)precision;
                do {
                    transform.localPosition = new Vector3(
                        rd.Next(-offset, offset) / floatPrecision,
                        positionY,
                        rd.Next(-offset, offset) / floatPrecision
                    );
                } while (Physics.OverlapSphere(transform.position, positionY * overlapRatio).Length > 1); // 重叠检测
            }
            var position = transform.localPosition + particleOffset;
            var particle = Instantiate(isHighScore ? highScoreParticlePrefab : particlePrefab/*, position, noRotation, particleContainer.transform*/) as GameObject;
            particle.transform.parent = particleContainer.transform;
            particle.transform.localPosition = position;
            particle.transform.localRotation = noRotation;
            particle.transform.localScale = new Vector3(particleScale, particleScale, particleScale);
            GameObject.Destroy(particle, 5.0f);
            DestroyPickup(other.gameObject);
            pickups.Remove(other.gameObject);
            count += isHighScore ? 500 : 100;
            SetCountText();
            EnsureEndlessMode();
        }
    }
    void OnCollisionEnter(Collision other) {
        if (other.gameObject.CompareTag(wallTagName)) {
            var intensity = Math.Min(1.0f, rb.velocity.magnitude / 10.0f);
            audioSource.volume = intensity;
            audioSource.Play();
            if (canVibrate) {
                Vibrate((long)(10L * intensity) + 5L);
            }
        }
    }
    private void SetCountText() {
        countText.text = "Score: " + count.ToString();
    }
    public GameObject pickUpPrefab;
    public GameObject highScorePickUpPrefab;
    public GameObject pickUpContainer;
    private readonly Quaternion noRotation = new Quaternion();
    private readonly HashSet<GameObject> pickups = new HashSet<GameObject>();
    public int radius = 9;
    public int precision = 1000;
    public float positionY = 0.5f;
    private readonly System.Random rd = new System.Random();
    public float overlapRatio;
    public bool SpawnPickup(Vector3 position, bool highScore = false) {
        var prefab = highScore ? highScorePickUpPrefab : pickUpPrefab;
        var pickup = Instantiate(prefab/*, position, noRotation, pickUpContainer.transform*/) as GameObject;
        var scale = pickup.transform.localScale;
        pickup.transform.parent = pickUpContainer.transform;
        pickup.transform.localPosition = position;
        pickup.transform.localRotation = noRotation;
        pickup.transform.localScale = scale;
        if (Physics.OverlapSphere(pickup.transform.position, positionY * overlapRatio).Length > 1) { // 重叠检测
            pickup.SetActive(false);
            GameObject.Destroy(pickup, 1.0f);
            return false;
        }
        pickup.tag = prefab.tag;
        pickups.Add(pickup);
        return true;
    }
    public void RandomlySpawnPickup() {
        var offset = radius * precision;
        var floatPrecision = (float)precision;
        var highScore = rd.Next(0, 5) == 0;
        Vector3 position;
        do {
            position = new Vector3(
                rd.Next(-offset, offset) / floatPrecision,
                positionY,
                rd.Next(-offset, offset) / floatPrecision
            );
        } while (!SpawnPickup(position, highScore)); // 重叠检测
    }
}

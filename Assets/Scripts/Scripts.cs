using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class Player : MonoBehaviour
{
    private static bool hasTransformed;
    private static bool hasFireTransformed;
    private static int coinCount;

    public static void ResetTransformationState()
    {
        hasTransformed = false;
        hasFireTransformed = false;
        coinCount = 0;
    }

    private const int WalkFrameCount = 6;
    private const float WalkFramesPerSecond = 9f;
    private static readonly float[] WalkFrameFootOffsetsFromBottom =
    {
        44f / 724f, 41f / 724f, 44f / 724f,
        44f / 724f, 41f / 724f, 47f / 724f
    };
    private static readonly float[] TransformedFrameFootOffsetsFromBottom =
    {
        53f / 727f, 53f / 727f, 56f / 727f,
        53f / 727f, 53f / 727f, 54f / 727f
    };
    private static readonly float[] FireFrameFootOffsetsFromBottom =
    {
        82f / 724f, 82f / 724f, 82f / 724f,
        80f / 724f, 85f / 724f, 84f / 724f
    };

    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpForce;
    private Vector2 MoveInput;
    private int jumpsUsed;
    private bool loadingNextScene;
    private bool elementDestroyed;
    private SpriteRenderer playerSpriteRenderer;
    private TMP_Text coinCounterText;
    private Sprite[] walkSprites;
    private float originalPixelsPerUnit;
    private float originalSpriteHeightPixels;
    private float colliderBottom;
    private int currentWalkFrame;
    private float walkFrameTimer;

    private void Awake()
    {
        elementDestroyed = SceneManager.GetActiveScene().buildIndex == 2
            ? hasFireTransformed
            : hasTransformed;
        SetImpostersVisible(hasFireTransformed);
        UpdateCoinCounter();
        playerSpriteRenderer = GetComponent<SpriteRenderer>();
        Texture2D walkSheet = Resources.Load<Texture2D>("WalkCycle");
        if (playerSpriteRenderer == null || walkSheet == null)
        {
            return;
        }

        Sprite originalSprite = playerSpriteRenderer.sprite;

        if (originalSprite == null)
        {
            return;
        }

        originalPixelsPerUnit = originalSprite.pixelsPerUnit;
        originalSpriteHeightPixels = originalSprite.rect.height;

        BoxCollider2D playerCollider = GetComponent<BoxCollider2D>();
        colliderBottom = playerCollider != null
            ? playerCollider.offset.y - playerCollider.size.y / 2f
            : -originalSprite.bounds.extents.y;

        if (hasFireTransformed)
        {
            Texture2D fireWalkSheet = Resources.Load<Texture2D>("FireWalkCycle");
            if (fireWalkSheet != null)
            {
                SetWalkSheet(fireWalkSheet, FireFrameFootOffsetsFromBottom);
            }
            else
            {
                SetWalkSheet(walkSheet, WalkFrameFootOffsetsFromBottom);
            }
        }
        else if (hasTransformed)
        {
            Texture2D transformedWalkSheet = Resources.Load<Texture2D>("TransformedWalkCycle");
            if (transformedWalkSheet != null)
            {
                SetWalkSheet(transformedWalkSheet, TransformedFrameFootOffsetsFromBottom);
            }
            else
            {
                SetWalkSheet(walkSheet, WalkFrameFootOffsetsFromBottom);
            }
        }
        else
        {
            SetWalkSheet(walkSheet, WalkFrameFootOffsetsFromBottom);
        }
        playerSpriteRenderer.sprite = walkSprites[0];
    }

    private void SetWalkSheet(Texture2D walkSheet, float[] footOffsetsFromBottom)
    {
        if (walkSprites != null)
        {
            foreach (Sprite oldSprite in walkSprites)
            {
                if (oldSprite != null)
                {
                    Destroy(oldSprite);
                }
            }
        }

        float walkPixelsPerUnit = walkSheet.height * originalPixelsPerUnit / originalSpriteHeightPixels;
        walkSprites = new Sprite[WalkFrameCount];
        for (int i = 0; i < WalkFrameCount; i++)
        {
            int xMin = Mathf.RoundToInt(i * walkSheet.width / (float)WalkFrameCount);
            int xMax = Mathf.RoundToInt((i + 1) * walkSheet.width / (float)WalkFrameCount);

            Rect frame = new Rect(xMin, 0, xMax - xMin, walkSheet.height);
            float footOffsetPixels = walkSheet.height * footOffsetsFromBottom[i];
            float pivotY = (footOffsetPixels - colliderBottom * walkPixelsPerUnit) / walkSheet.height;
            walkSprites[i] = Sprite.Create(walkSheet, frame, new Vector2(0.5f, pivotY), walkPixelsPerUnit);
        }
    }

    private void UpdateCoinCounter()
    {
        GameObject counterObject = null;
        if (coinCounterText == null)
        {
            counterObject = GameObject.Find("CoinCounter");
            if (counterObject != null)
            {
                SetCoinCounterLayout(counterObject);
                coinCounterText = counterObject.GetComponentInChildren<TMP_Text>(true);
            }

            if (coinCounterText == null)
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas == null)
                {
                    GameObject canvasObject = new GameObject(
                        "CoinCounterCanvas",
                        typeof(RectTransform),
                        typeof(Canvas),
                        typeof(CanvasScaler));
                    canvas = canvasObject.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920f, 1080f);
                }

                bool hasCounterBox = counterObject != null && counterObject.GetComponent<RectTransform>() != null;
                if (!hasCounterBox)
                {
                    counterObject = new GameObject(
                        "CoinCounter",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image));
                    counterObject.transform.SetParent(canvas.transform, false);
                    SetCoinCounterLayout(counterObject);

                    Texture2D boxTexture = Resources.Load<Texture2D>("CoinCounterBar");
                    if (boxTexture != null)
                    {
                        Image boxImage = counterObject.GetComponent<Image>();
                        boxImage.sprite = Sprite.Create(
                            boxTexture,
                            new Rect(0, 0, boxTexture.width, boxTexture.height),
                            new Vector2(0.5f, 0.5f),
                            100f);
                        boxImage.preserveAspect = true;
                    }
                    hasCounterBox = true;
                }

                GameObject textObject = new GameObject("CoinCounterValue", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(counterObject.transform, false);
                RectTransform textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0.37f, 0.15f);
                textRect.anchorMax = new Vector2(0.77f, 0.70f);
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                coinCounterText = textObject.GetComponent<TMP_Text>();
                coinCounterText.font = TMP_Settings.defaultFontAsset;
                coinCounterText.fontSize = 16f;
                coinCounterText.color = Color.white;
                coinCounterText.alignment = TextAlignmentOptions.Center;
            }
        }

        if (coinCounterText != null)
        {
            coinCounterText.text = coinCount.ToString();
        }
    }

    private void SetCoinCounterLayout(GameObject counterObject)
    {
        RectTransform counterRect = counterObject.GetComponent<RectTransform>();
        if (counterRect == null)
        {
            return;
        }

        counterRect.anchorMin = new Vector2(1f, 1f);
        counterRect.anchorMax = new Vector2(1f, 1f);
        counterRect.pivot = new Vector2(1f, 1f);
        counterRect.anchoredPosition = new Vector2(-20f, -8f);
        counterRect.sizeDelta = new Vector2(123.75f, 49.005f);
    }

    private void SetImpostersVisible(bool isVisible)
    {
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject rootObject in rootObjects)
        {
            Transform[] transforms = rootObject.GetComponentsInChildren<Transform>(true);
            foreach (Transform objectTransform in transforms)
            {
                if (objectTransform.gameObject.name != "imposter")
                {
                    continue;
                }

                SpriteRenderer[] renderers = objectTransform.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (SpriteRenderer spriteRenderer in renderers)
                {
                    spriteRenderer.enabled = isVisible;
                }
            }
        }
    }

    private void Update()
    {
        if (walkSprites == null || walkSprites.Length == 0 || playerSpriteRenderer == null)
        {
            return;
        }

        if (Mathf.Abs(MoveInput.x) < 0.01f)
        {
            currentWalkFrame = 0;
            walkFrameTimer = 0f;
            playerSpriteRenderer.sprite = walkSprites[0];
            return;
        }

        playerSpriteRenderer.flipX = MoveInput.x < 0f;
        walkFrameTimer += Time.deltaTime;
        if (walkFrameTimer >= 1f / WalkFramesPerSecond)
        {
            walkFrameTimer -= 1f / WalkFramesPerSecond;
            currentWalkFrame = (currentWalkFrame + 1) % walkSprites.Length;
            playerSpriteRenderer.sprite = walkSprites[currentWalkFrame];
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.name == "win" && !loadingNextScene)
        {
            loadingNextScene = true;
            SceneManager.LoadScene("winscene");
            return;
        }

        bool isGround = other.gameObject.name.StartsWith("Ground", System.StringComparison.OrdinalIgnoreCase);
        bool isSecondScene = SceneManager.GetActiveScene().buildIndex == 2;
        bool shouldLoseOnGround = isSecondScene ? !hasFireTransformed : !elementDestroyed;
        if (isGround && shouldLoseOnGround && !loadingNextScene)
        {
            loadingNextScene = true;
            SceneManager.LoadScene("gameover");
            return;
        }

        if (other.gameObject.name == "Square (1)" && !loadingNextScene)
        {
            loadingNextScene = true;
            SceneManager.LoadScene(2);
            return;
        }

        if (other.gameObject.name == "Square (4)" && !loadingNextScene)
        {
            loadingNextScene = true;
            SceneManager.LoadScene("cheatscene");
            return;
        }

        // Reset jump allowance only when landing on a surface below the player.
        foreach (ContactPoint2D contact in other.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                jumpsUsed = 0;
                break;
            }
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void OnMove(InputValue value)
    {
        MoveInput = value.Get<Vector2>();
    }
    public void OnJump(InputValue value)
    {
        if (value.isPressed && jumpsUsed < 2)
        {
            playerRigidbody.linearVelocity = new Vector2(playerRigidbody.linearVelocity.x, jumpForce);
            jumpsUsed++;
        }
    }
    private void FixedUpdate()
    {
        playerRigidbody.linearVelocity = new Vector2(MoveInput.x*moveSpeed, playerRigidbody.linearVelocity.y);

    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.name == "element2")
        {
            hasTransformed = true;
            hasFireTransformed = true;
            elementDestroyed = true;
            Destroy(other.gameObject);

            SetImpostersVisible(true);

            Texture2D fireWalkSheet = Resources.Load<Texture2D>("FireWalkCycle");
            if (fireWalkSheet != null)
            {
                SetWalkSheet(fireWalkSheet, FireFrameFootOffsetsFromBottom);
                currentWalkFrame = 0;
                walkFrameTimer = 0f;
                playerSpriteRenderer.sprite = walkSprites[0];
            }

            return;
        }

        if (other.CompareTag("element"))
        {
            hasTransformed = true;
            elementDestroyed = true;
            Destroy(other.gameObject);

            Texture2D transformedWalkSheet = Resources.Load<Texture2D>("TransformedWalkCycle");
            if (transformedWalkSheet != null)
            {
                SetWalkSheet(transformedWalkSheet, TransformedFrameFootOffsetsFromBottom);
                currentWalkFrame = 0;
                walkFrameTimer = 0f;
                playerSpriteRenderer.sprite = walkSprites[0];
            }

            return;
        }

        if(other.CompareTag ("Coin"))

        {
        coinCount ++;
        UpdateCoinCounter();
        Debug.Log("Coin:" + coinCount);
        Destroy(other.gameObject);
        }
    
    }
   

}

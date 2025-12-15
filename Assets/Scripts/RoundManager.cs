using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoundManager : MonoBehaviour
{
    private static WaitForSeconds _waitForSeconds2 = new WaitForSeconds(2f);
    [Header("Round Settings")]
    [SerializeField] private int maxRounds = 3;
    [SerializeField] private float roundEndDisplayTime = 3f;

    [Header("Goat References")]
    [SerializeField] private GameObject goat1;
    [SerializeField] private GameObject goat2;

    [Header("UI References")]
    [SerializeField] private GameObject roundEndPanel;
    [SerializeField] private GameObject matchEndPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private GameObject[] playerHearts;
    [SerializeField] private GameObject[] playerHeartsGrey;
    [SerializeField] private GameObject[] opponentHearts;
    [SerializeField] private GameObject[] opponentHeartsGrey;
    [SerializeField] private Image roundImage;
    [SerializeField] private Sprite[] roundSprites;

    [Header("Animation Settings")]
    [SerializeField] private RectTransform roundAnnouncementRect;
    [SerializeField] private float animationDuration = 1.5f;
    [SerializeField] private Vector3 magnifiedScale = new Vector3(3f, 3f, 1f);

    [Header("Audio Settings")]
    [SerializeField] private AudioClip roundStartSound;
    [SerializeField] private AudioSource audioSource;

    private int currentRound = 1;
    private bool isRoundEnding = false;
    private GoatController goat1Controller;
    private GoatController goat2Controller;
    private AiGoatScript goat1AI;
    private AiGoatScript goat2AI;

    private void Awake()
    {
        // Gets controller components.
        if (goat1 != null)
        {
            goat1Controller = goat1.GetComponent<GoatController>();
            goat1AI = goat1.GetComponent<AiGoatScript>();
        }
        if (goat2 != null)
        {
            goat2Controller = goat2.GetComponent<GoatController>();
            goat2AI = goat2.GetComponent<AiGoatScript>();
        }

        // Finds the game over panel.
        if (matchEndPanel == null)
        {
            GameObject activePanel = GameObject.FindGameObjectWithTag("GameOverUI");
            if (activePanel != null)
            {
                matchEndPanel = activePanel;
            }
            else
            {
                GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    if (obj.scene.IsValid() && obj.CompareTag("GameOverUI"))
                    {
                        matchEndPanel = obj;
                        break;
                    }
                }
            }
        }

        // Hides panels.
        RoundEndPanel(false);
        MatchEndPanel(false);
    }

    private void Start()
    {
        // Ensures the game is running.
        Time.timeScale = 1f;

        // Initialises lives.
        if (goat1Controller != null)
        {
            goat1Controller.InitializeLives();
        }
        if (goat2Controller != null)
        {
            goat2Controller.InitializeLives();
        }

        UpdateHeartsUI();
        UpdateRoundUI();
    }

    // Called when a goat falls.
    public void OnGoatFell(GameObject fallenGoat)
    {
        if (isRoundEnding) return;

        // Identifies the fallen goat.
        GoatController fallenController = fallenGoat.GetComponent<GoatController>();
        if (fallenController == null) return;

        // Loses a life.
        bool isOutOfLives = fallenController.LoseLife();

        // Updates the UI.
        UpdateHeartsUI();

        // Identifies the other goat.
        GameObject otherGoat = (fallenGoat == goat1) ? goat2 : goat1;
        GoatController otherController = otherGoat != null ? otherGoat.GetComponent<GoatController>() : null;

        // Checks if the match is over.
        if (isOutOfLives)
        {
            string winnerName = otherGoat != null ? otherGoat.name : "Unknown";
            Debug.Log($"Match Over! {winnerName} wins!");
            EndMatch(winnerName);
        }
        else
        {
            string roundWinnerName = otherGoat != null ? otherGoat.name : "Unknown";
            Debug.Log($"Round {currentRound} Over! {roundWinnerName} wins the round!");
            EndRound(roundWinnerName);
        }
    }

    // Ends the round.
    private void EndRound(string roundWinnerName)
    {
        if (isRoundEnding) return;
        isRoundEnding = true;

        // Checks for max rounds.
        if (currentRound >= maxRounds)
        {
            DetermineMatchWinnerByLives(roundWinnerName);
        }
        else
        {
            RoundEndPanel(true);
            StartCoroutine(RestartRoundAfterDelay());
        }
    }

    // Ends the match.
    private void EndMatch(string winnerName)
    {
        if (isRoundEnding) return;
        isRoundEnding = true;

        // Determines the winner.
        bool playerWon = (winnerName == goat1.name);

        RoundEndPanel(false);
        MatchEndPanel(true, playerWon);

        // Freezes the game.
        Time.timeScale = 0f;
    }

    // Restarts the round.
    private IEnumerator RestartRoundAfterDelay()
    {
        currentRound++;

        StartCoroutine(AnimateRoundAnnouncement());

        yield return new WaitForSeconds(roundEndDisplayTime);

        Debug.Log("New Round!");

        RoundEndPanel(false);

        // Resets the arena.
        if (ArenaShrinking.Instance != null)
        {
            ArenaShrinking.Instance.ResetArenaSize();
        }

        // Resets the AI.
        if (goat1AI != null)
        {
            goat1AI.OnEpisodeBegin();
        }
        if (goat2AI != null)
        {
            goat2AI.OnEpisodeBegin();
        }

        isRoundEnding = false;
    }

    private IEnumerator AnimateRoundAnnouncement()
    {
        if (roundAnnouncementRect == null || roundImage == null || roundSprites == null)
        {
            UpdateRoundUI();
            yield break;
        }

        // Gets the next sprite.
        int spriteIndex = Mathf.Clamp(currentRound - 1, 0, roundSprites.Length - 1);
        Sprite nextSprite = roundSprites[spriteIndex];

        // Sets up the image.
        if (roundAnnouncementRect.TryGetComponent<Image>(out var announcementImg)) announcementImg.sprite = nextSprite;

        // Sets up fading.
        if (!roundAnnouncementRect.TryGetComponent<CanvasGroup>(out var canvasGroup))
        {
            canvasGroup = roundAnnouncementRect.gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        roundAnnouncementRect.gameObject.SetActive(true);

        // Plays a sound.
        if (roundStartSound != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(roundStartSound);
            }
            else
            {
                AudioSource.PlayClipAtPoint(roundStartSound, Camera.main.transform.position);
            }
        }

        // Starts at the centre.
        Vector3 startPos = new(Screen.width / 2f, Screen.height / 2f, 0);

        roundAnnouncementRect.position = startPos;
        roundAnnouncementRect.localScale = magnifiedScale;

        // Targets the HUD.
        Vector3 targetPos = roundImage.rectTransform.position;
        Vector3 targetScale = roundImage.rectTransform.localScale;

        // Phase 1: Fades in.
        float fadeInDuration = 0.5f;
        float elapsedFade = 0f;
        while (elapsedFade < fadeInDuration)
        {
            elapsedFade += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsedFade / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Phase 2: Waits.
        yield return new WaitForSeconds(1f);

        // Phase 3: Moves to target.
        float elapsedMove = 0f;
        while (elapsedMove < animationDuration)
        {
            elapsedMove += Time.deltaTime;
            float t = elapsedMove / animationDuration;

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            roundAnnouncementRect.position = Vector3.Lerp(startPos, targetPos, smoothT);
            roundAnnouncementRect.localScale = Vector3.Lerp(magnifiedScale, targetScale, smoothT);

            yield return null;
        }

        roundAnnouncementRect.gameObject.SetActive(false);
        UpdateRoundUI();
    }

    // Determines the match winner.
    private void DetermineMatchWinnerByLives(string lastRoundWinner)
    {
        int goat1Lives = goat1Controller != null ? goat1Controller.CurrentLives : 0;
        int goat2Lives = goat2Controller != null ? goat2Controller.CurrentLives : 0;

        string winnerName;
        if (goat1Lives > goat2Lives)
        {
            winnerName = goat1.name;
            Debug.Log($"Match Over (Max Rounds)! Player wins with {goat1Lives} lives remaining!");
        }
        else if (goat2Lives > goat1Lives)
        {
            winnerName = goat2.name;
            Debug.Log($"Match Over (Max Rounds)! AI wins with {goat2Lives} lives remaining!");
        }
        else
        {
            // Handles ties.
            winnerName = lastRoundWinner;
            Debug.Log($"Match Over (Max Rounds)! Lives tied. Winner determined by last round: {winnerName}");
        }

        EndMatch(winnerName);
    }

    // Restarts the match.
    private IEnumerator RestartMatchAfterDelay()
    {
        yield return new WaitForSeconds(roundEndDisplayTime * 2f);

        // Hides panels.
        RoundEndPanel(false);
        MatchEndPanel(false);

        // Resets the counter.
        currentRound = 1;
        UpdateRoundUI();

        // Resets the arena.
        if (ArenaShrinking.Instance != null)
        {
            ArenaShrinking.Instance.ResetArenaSize();
        }

        // Resets the AI.
        if (goat1AI != null)
        {
            goat1AI.OnEpisodeBegin();
        }
        if (goat2AI != null)
        {
            goat2AI.OnEpisodeBegin();
        }


        // Resets lives.
        if (goat1Controller != null)
        {
            goat1Controller.ResetLives();
        }
        if (goat2Controller != null)
        {
            goat2Controller.ResetLives();
        }

        UpdateHeartsUI();

        isRoundEnding = false;
    }

    // Gets the round number.
    public int GetCurrentRound()
    {
        return currentRound;
    }

    // Checks if the round is ending.
    public bool IsRoundEnding()
    {
        return isRoundEnding;
    }

    // Restarts the match.
    public void RestartMatch()
    {
        // Unfreezes the game.
        Time.timeScale = 1f;

        // Hides panels.
        RoundEndPanel(false);
        MatchEndPanel(false);

        // Resets the counter.
        currentRound = 1;
        UpdateRoundUI();

        // Resets lives.
        if (goat1Controller != null)
        {
            goat1Controller.ResetLives();
        }
        if (goat2Controller != null)
        {
            goat2Controller.ResetLives();
        }
        UpdateHeartsUI();

        // Resets the arena.
        if (ArenaShrinking.Instance != null)
        {
            ArenaShrinking.Instance.ResetArenaSize();
        }

        // Resets the AI.
        if (goat1AI != null)
        {
            goat1AI.OnEpisodeBegin();
        }
        if (goat2AI != null)
        {
            goat2AI.OnEpisodeBegin();
        }

        // Resets the flag.
        isRoundEnding = false;

        Debug.Log("Match restarted!");
    }

    public void RoundEndPanel(bool active)
    {
        if (roundEndPanel != null)
        {
            roundEndPanel.SetActive(active);
        }
    }

    public void MatchEndPanel(bool active, bool playerWon = true)
    {
        if (matchEndPanel != null)
        {
            matchEndPanel.SetActive(active);

            if (active)
            {
                if (winnerText != null)
                {
                    winnerText.text = playerWon ? "Player Wins!" : "Opponent Wins!";
                }

                if (playerWon)
                {
                    Debug.Log("Setting active elements for player win");
                }
                else
                {
                    Debug.Log("Setting active elements for player lose");
                }
            }
        }
    }

    private void UpdateHeartsUI()
    {
        UpdateHeartList(goat1Controller, playerHearts, playerHeartsGrey);
        UpdateHeartList(goat2Controller, opponentHearts, opponentHeartsGrey);
    }

    private void UpdateHeartList(GoatController controller, GameObject[] hearts, GameObject[] greyHearts)
    {
        if (controller != null && hearts != null)
        {
            bool usingOverlay = (greyHearts != null && greyHearts.Length > 0);

            for (int i = 0; i < hearts.Length; i++)
            {
                bool hasLife = i < controller.CurrentLives;

                if (hearts[i] != null)
                {
                    if (usingOverlay && i < greyHearts.Length && greyHearts[i] != null)
                    {
                        // Overlay mode.
                        hearts[i].SetActive(true);

                        // Activates the overlay.
                        greyHearts[i].SetActive(!hasLife);
                    }
                    else
                    {
                        // Standard mode.
                        hearts[i].SetActive(hasLife);
                    }
                }
            }
        }
    }

    private void UpdateRoundUI()
    {
        if (roundImage != null && roundSprites != null && roundSprites.Length > 0)
        {
            // Clamps the index.
            int spriteIndex = Mathf.Clamp(currentRound - 1, 0, roundSprites.Length - 1);

            if (roundSprites[spriteIndex] != null)
            {
                roundImage.sprite = roundSprites[spriteIndex];
                roundImage.gameObject.SetActive(true);
            }
        }
    }
}

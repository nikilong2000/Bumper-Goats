using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoundManager : MonoBehaviour
{
    private static WaitForSeconds _waitForSeconds2 = new WaitForSeconds(2f);
    [Header("Round Settings")]
    [SerializeField] private int maxRounds = 3;
    [SerializeField] private float roundEndDisplayTime = 3f; // Time to show round end screen before restarting

    [Header("Goat References")]
    [SerializeField] private GameObject goat1; // ALWAYS the Player
    [SerializeField] private GameObject goat2; // ALWAYS the AI opponent

    [Header("UI References")]
    [SerializeField] private GameObject roundEndPanel; // Panel that shows round information
    [SerializeField] private GameObject matchEndPanel; // Panel that shows match end information
    [SerializeField] private GameObject[] playerHearts; // Array of heart GameObjects for player
    [SerializeField] private GameObject[] playerHeartsGrey; // Array of grey/used heart GameObjects for player (overlay)
    [SerializeField] private GameObject[] opponentHearts; // Array of heart GameObjects for opponent
    [SerializeField] private GameObject[] opponentHeartsGrey; // Array of grey/used heart GameObjects for opponent (overlay)
    [SerializeField] private Image roundImage; // Image to display current round sprite
    [SerializeField] private Sprite[] roundSprites; // Array of sprites for each round (Round 1, Round 2, etc.)

    [Header("Animation Settings")]
    [SerializeField] private RectTransform roundAnnouncementRect; // The big image that flies
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
        // Get GoatController components
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

        // Hide all panels initially
        RoundEndPanel(false);
        MatchEndPanel(false);
    }

    private void Start()
    {
        // Initialize lives for both goats
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

    /// <summary>
    /// Called when a goat falls off the platform
    /// </summary>
    public void OnGoatFell(GameObject fallenGoat)
    {
        if (isRoundEnding) return; // Prevent multiple calls

        // Determine which goat fell
        GoatController fallenController = fallenGoat.GetComponent<GoatController>();
        if (fallenController == null) return;

        // Lose a life
        bool isOutOfLives = fallenController.LoseLife();

        // Update UI
        UpdateHeartsUI();

        // Determine the other goat
        GameObject otherGoat = (fallenGoat == goat1) ? goat2 : goat1;
        GoatController otherController = otherGoat != null ? otherGoat.GetComponent<GoatController>() : null;

        // Check if match is over (one goat is out of lives)
        if (isOutOfLives)
        {
            // Match is over - one goat has no lives left
            string winnerName = otherGoat != null ? otherGoat.name : "Unknown";
            Debug.Log($"Match Over! {winnerName} wins!");
            EndMatch(winnerName);
        }
        else
        {
            // Round is over, but match continues
            string roundWinnerName = otherGoat != null ? otherGoat.name : "Unknown";
            Debug.Log($"Round {currentRound} Over! {roundWinnerName} wins the round!");
            EndRound(roundWinnerName);
        }
    }

    /// <summary>
    /// End the current round and prepare for the next one
    /// </summary>
    private void EndRound(string roundWinnerName)
    {
        if (isRoundEnding) return;
        isRoundEnding = true;

        // Note: AI agents are notified by FallZoneDetector when goats fall
        // The AI will handle its own rewards and episode ending
        // RoundManager just controls the UI and round/match flow

        // Check if we've reached max rounds
        if (currentRound >= maxRounds)
        {
            // Match is over - max rounds reached
            // Determine winner based on lives remaining (or last round winner if tied)
            DetermineMatchWinnerByLives(roundWinnerName);
        }
        else
        {
            // Show round end panel
            RoundEndPanel(true);

            // Start coroutine to restart the round after delay
            StartCoroutine(RestartRoundAfterDelay());
        }
    }

    /// <summary>
    /// End the match (one goat is out of lives OR max rounds reached)
    /// </summary>
    private void EndMatch(string winnerName)
    {
        if (isRoundEnding) return;
        isRoundEnding = true;

        // Determine which goat won (goat1 is always the player)
        bool playerWon = (winnerName == goat1.name);

        // Note: AI agents will handle their own episode ending via FallZoneDetector
        // The AI will get rewards when goats fall, but RoundManager controls the round/match flow

        // Hide round end panel if it's showing
        RoundEndPanel(false);
        // Show appropriate match end panel based on player win/loss
        MatchEndPanel(true, playerWon);
    }

    /// <summary>
    /// Restart the current round after showing the round end screen
    /// </summary>
    private IEnumerator RestartRoundAfterDelay()
    {
        // Move to next round immediately so we can announce it
        currentRound++;

        // Start the animation immediately so it plays during the wait time
        StartCoroutine(AnimateRoundAnnouncement());

        // Wait for the display time (animation plays during this)
        yield return new WaitForSeconds(roundEndDisplayTime);

        Debug.Log("New Round!");

        // Hide round end panel
        RoundEndPanel(false);

        // Note: We already check max rounds in EndRound() before starting this coroutine
        // So we don't need to check again here - if we reach this point, we're continuing to next round

        // Reset arena size if needed
        if (ArenaShrinking.Instance != null)
        {
            ArenaShrinking.Instance.ResetArenaSize();
        }

        // Reset AI episode if needed (for training)
        // Note: Since AI ends episodes when goats fall, OnEpisodeBegin will be called automatically
        // But we can manually trigger it here to ensure proper reset between rounds
        // This also resets both of the goats
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

        // Get next round sprite
        int spriteIndex = Mathf.Clamp(currentRound - 1, 0, roundSprites.Length - 1);
        Sprite nextSprite = roundSprites[spriteIndex];

        // Setup Announcement Image
        if (roundAnnouncementRect.TryGetComponent<Image>(out var announcementImg)) announcementImg.sprite = nextSprite;

        // Setup CanvasGroup for fading
        if (!roundAnnouncementRect.TryGetComponent<CanvasGroup>(out var canvasGroup))
        {
            canvasGroup = roundAnnouncementRect.gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 0f; // Start invisible
        canvasGroup.blocksRaycasts = false;

        roundAnnouncementRect.gameObject.SetActive(true);

        // Play sound
        if (roundStartSound != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(roundStartSound);
            }
            else
            {
                // Fallback if no AudioSource assigned
                AudioSource.PlayClipAtPoint(roundStartSound, Camera.main.transform.position);
            }
        }

        // Start at center (assuming parent is Canvas center or similar)
        Vector3 startPos = new(Screen.width / 2f, Screen.height / 2f, 0);

        roundAnnouncementRect.position = startPos;
        roundAnnouncementRect.localScale = magnifiedScale;

        // Target is the HUD image
        Vector3 targetPos = roundImage.rectTransform.position;
        Vector3 targetScale = roundImage.rectTransform.localScale; // Usually 1,1,1

        // Phase 1: Fade In
        float fadeInDuration = 0.5f;
        float elapsedFade = 0f;
        while (elapsedFade < fadeInDuration)
        {
            elapsedFade += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsedFade / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Phase 2: Wait
        yield return new WaitForSeconds(1f);

        // Phase 3: Move to Target
        float elapsedMove = 0f;
        while (elapsedMove < animationDuration)
        {
            elapsedMove += Time.deltaTime;
            float t = elapsedMove / animationDuration;

            // Add some easing
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            roundAnnouncementRect.position = Vector3.Lerp(startPos, targetPos, smoothT);
            roundAnnouncementRect.localScale = Vector3.Lerp(magnifiedScale, targetScale, smoothT);

            yield return null;
        }

        // Finish
        roundAnnouncementRect.gameObject.SetActive(false);
        UpdateRoundUI(); // This sets the HUD image to the new sprite
    }

    /// <summary>
    /// Determine match winner based on lives remaining (or last round winner if tied)
    /// </summary>
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
            // Lives are tied - winner is whoever won the last round
            winnerName = lastRoundWinner;
            Debug.Log($"Match Over (Max Rounds)! Lives tied. Winner determined by last round: {winnerName}");
        }

        EndMatch(winnerName);
    }

    /// <summary>
    /// Restart the entire match after showing the match end screen
    /// </summary>
    private IEnumerator RestartMatchAfterDelay()
    {
        yield return new WaitForSeconds(roundEndDisplayTime * 2f); // Show match end screen longer

        // Hide all panels
        RoundEndPanel(false);
        MatchEndPanel(false);

        // Reset round counter
        currentRound = 1;
        UpdateRoundUI();

        // Reset arena size if needed
        if (ArenaShrinking.Instance != null)
        {
            ArenaShrinking.Instance.ResetArenaSize();
        }

        // Reset AI episode if needed (for training)
        // Note: Since AI ends episodes when goats fall, OnEpisodeBegin will be called automatically
        // But we can manually trigger it here to ensure proper reset between rounds
        // This also resets both of the goats
        if (goat1AI != null)
        {
            goat1AI.OnEpisodeBegin();
        }
        if (goat2AI != null)
        {
            goat2AI.OnEpisodeBegin();
        }


        // Reset lives for both goats
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

    /// <summary>
    /// Get the current round number
    /// </summary>
    public int GetCurrentRound()
    {
        return currentRound;
    }

    /// <summary>
    /// Check if a round is currently ending
    /// </summary>
    public bool IsRoundEnding()
    {
        return isRoundEnding;
    }

    /// <summary>
    /// Public method to restart the match (call this from UI button)
    /// </summary>
    public void RestartMatch()
    {
        // Hide all panels
        RoundEndPanel(false);
        MatchEndPanel(false);

        // Reset round counter
        currentRound = 1;
        UpdateRoundUI();

        // Reset lives for both goats
        if (goat1Controller != null)
        {
            goat1Controller.ResetLives();
        }
        if (goat2Controller != null)
        {
            goat2Controller.ResetLives();
        }
        UpdateHeartsUI();

        // Reset arena size if needed
        if (ArenaShrinking.Instance != null)
        {
            ArenaShrinking.Instance.ResetArenaSize();
        }

        // Reset AI episode if needed (for training)
        // This also resets both of the goats
        if (goat1AI != null)
        {
            goat1AI.OnEpisodeBegin();
        }
        if (goat2AI != null)
        {
            goat2AI.OnEpisodeBegin();
        }

        // Reset the round ending flag
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
            if (playerWon && active)
            {
                Debug.Log("Setting active elements for player win");
                // TODO: setting active elements for player win
            }
            else
            {
                Debug.Log("Setting active elements for player lose");
                // TODO: setting active elements for player lose
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
                        // Overlay Mode:
                        // Base heart stays active (so it doesn't disappear)
                        hearts[i].SetActive(true);

                        // Overlay is active when life is lost (to make it look used)
                        greyHearts[i].SetActive(!hasLife);
                    }
                    else
                    {
                        // Standard Mode (Disappear):
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
            // Clamp to ensure we don't go out of bounds if rounds exceed sprites
            int spriteIndex = Mathf.Clamp(currentRound - 1, 0, roundSprites.Length - 1);

            if (roundSprites[spriteIndex] != null)
            {
                roundImage.sprite = roundSprites[spriteIndex];
                roundImage.gameObject.SetActive(true);
            }
        }
    }
}

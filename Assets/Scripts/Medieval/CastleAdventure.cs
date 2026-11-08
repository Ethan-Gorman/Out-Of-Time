using System.Collections;
using UnityEngine;

public class CastleAdventure : MonoBehaviour
{
    public enum QuestStage { Arrival, Village, Guardians, CrystalReward, Returning, Departing }
    [SerializeField] MedievalLevel level;
    [SerializeField] PlayerMovement player;
    [SerializeField] MedievalJump jump;
    [SerializeField] MedievalInteractor interactor;
    [SerializeField] Rigidbody playerBody;
    [SerializeField] SpriteRenderer[] playerSprites;
    [SerializeField] CameraFollow followCamera;
    [SerializeField] TimeMachine machine;
    [SerializeField] Transform crashStart;
    [SerializeField] Transform crashEnd;
    [SerializeField] Transform exitPoint;
    [SerializeField] Transform cinematicCamera;
    [SerializeField] GameObject crashEffect;
    [SerializeField] SkeletonEnemy[] guardians;
    [SerializeField] GameObject rewardChest;
    [SerializeField] CastleRunePuzzle entrancePuzzle;
    [SerializeField] float flightSeconds = 3f;
    [SerializeField] float exitSeconds = 1f;
    [SerializeField] float departureDelay = 1.2f;
    public QuestStage Stage { get; private set; } = QuestStage.Arrival;
    public int GuardiansRemaining { get; private set; } = 3;
    bool battleStarted;
    bool departing;

    IEnumerator Start()
    {
        rewardChest.SetActive(false);
        foreach (var guardian in guardians) if (guardian != null) guardian.gameObject.SetActive(false);
        bool moveEnabled = player.enabled;
        bool jumpEnabled = jump.enabled;
        bool interactEnabled = interactor.enabled;
        bool cameraEnabled = followCamera.enabled;
        bool wasKinematic = playerBody.isKinematic;
        bool clockEnabled = Clock.Instance != null && Clock.Instance.enabled;
        if (Clock.Instance != null) Clock.Instance.enabled = false;
        player.enabled = false;
        jump.enabled = false;
        interactor.enabled = false;
        playerBody.linearVelocity = Vector3.zero;
        playerBody.isKinematic = true;
        foreach (var renderer in playerSprites) renderer.enabled = false;
        followCamera.enabled = false;
        followCamera.transform.SetPositionAndRotation(cinematicCamera.position, cinematicCamera.rotation);
        if (MedievalUI.Instance != null) MedievalUI.Instance.ShowBanner("THE MEDIEVAL ERA");
        float elapsed = 0f;
        while (elapsed < flightSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / flightSeconds);
            machine.transform.position = Vector3.Lerp(crashStart.position, crashEnd.position, t * t);
            machine.transform.rotation = Quaternion.Euler(Mathf.Sin(t * Mathf.PI) * 18f, -15f * (1f - t), Mathf.Sin(t * Mathf.PI * 2f) * 12f);
            yield return null;
        }
        machine.transform.SetPositionAndRotation(crashEnd.position, crashEnd.rotation);
        if (crashEffect != null) crashEffect.SetActive(true);
        if (MedievalUI.Instance != null) MedievalUI.Instance.HideBanner();
        yield return new WaitForSecondsRealtime(0.6f);
        Vector3 start = machine.transform.position + new Vector3(0f, 0.25f, -1.8f);
        playerBody.position = start;
        foreach (var renderer in playerSprites) renderer.enabled = true;
        elapsed = 0f;
        while (elapsed < exitSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            playerBody.position = Vector3.Lerp(start, exitPoint.position, Mathf.SmoothStep(0f, 1f, elapsed / exitSeconds));
            yield return null;
        }
        playerBody.position = exitPoint.position;
        playerBody.isKinematic = wasKinematic;
        player.enabled = moveEnabled;
        jump.enabled = jumpEnabled;
        interactor.enabled = interactEnabled;
        followCamera.enabled = cameraEnabled;
        if (Clock.Instance != null) Clock.Instance.enabled = clockEnabled;
        Stage = QuestStage.Village;
        TellStory();
    }

    public void TellStory()
    {
        if (Stage == QuestStage.Arrival || MedievalUI.Instance == null) return;
        if (level.HasCrystal)
        {
            MedievalUI.Instance.Say("Castle Warden: You have the Time Crystal! Retrace the keep's maze and repair your machine in the village square.");
            return;
        }
        MedievalUI.Instance.Say(
            "Castle Warden: A machine falling from the heavens... You must be a traveller from the future!",
            "Castle Warden: Your engine needs the Time Crystal to get you back. Three guardians protect it in the heart of our keep.",
            "Castle Warden: Solve the rune lock, survive the spikes and swinging guillotines, then claim the crystal chest.");
        MedievalUI.Instance.SetObjective("Speak to the warden / solve the rune lock at the keep entrance");
    }

    public void EnterArena()
    {
        if (battleStarted || Stage == QuestStage.Arrival || !entrancePuzzle.Solved) return;
        battleStarted = true;
        Stage = QuestStage.Guardians;
        foreach (var guardian in guardians) if (guardian != null) guardian.gameObject.SetActive(true);
        if (MedievalUI.Instance != null)
        {
            MedievalUI.Instance.Say("The crystal guardians awaken! Press E near a guardian to strike, or jump onto its head.");
            MedievalUI.Instance.SetObjective("Defeat the crystal guardians: 3 remaining");
        }
    }

    void Update()
    {
        if (!battleStarted || Stage != QuestStage.Guardians) return;
        int remaining = 0;
        foreach (var guardian in guardians) if (guardian != null && !guardian.IsDead) remaining++;
        if (remaining == GuardiansRemaining) return;
        GuardiansRemaining = remaining;
        if (remaining > 0)
        {
            if (MedievalUI.Instance != null) MedievalUI.Instance.SetObjective("Defeat the crystal guardians: " + remaining + " remaining");
            return;
        }
        Stage = QuestStage.CrystalReward;
        rewardChest.SetActive(true);
        if (MedievalUI.Instance != null)
        {
            MedievalUI.Instance.Say("All three guardians are defeated. The Time Crystal chest appears!");
            MedievalUI.Instance.SetObjective("Open the Time Crystal chest [E]");
        }
    }

    public void CrystalClaimed()
    {
        if (Stage != QuestStage.CrystalReward) return;
        Stage = QuestStage.Returning;
        level.CollectCrystal();
    }

    public void DepartAfterRepair()
    {
        if (departing || !level.HasCrystal) return;
        departing = true;
        Stage = QuestStage.Departing;
        StartCoroutine(Depart());
    }
    IEnumerator Depart()
    {
        yield return new WaitForSecondsRealtime(departureDelay);
        machine.Interact(interactor);
    }
}

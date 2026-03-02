using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuSaveSlotController : MonoBehaviour, ISubmitHandler, ISelectHandler
{
    [SerializeField, FirstFoldOutItem("Dependencies")]
    private Animator animator;
    [Tooltip("The parent slot controller of the active slot"), SerializeField, LastFoldoutItem()]
    private MainMenuSaveController saveController;
    [SerializeField, Tooltip("The current player data of the player being built")]
    private PlayerData playerData;

    [Tooltip("The slot ID of the current object"), SerializeField, FirstFoldOutItem("Save Slot Settings")]
    private int saveSlotId = 0;
    [Tooltip("Determines whether the save slot has save data ot not"), SerializeField]
    private bool hasSaveData;
    [Tooltip("The current button overlaying the slot"), SerializeField, LastFoldoutItem()]
    private Button saveSlotButton;

    [Tooltip("The object which displays that the current slot is a no save slot"), SerializeField]
    private GameObject noSaveToggle;

    [SerializeField]
    private NewSaveSlotStateData newSaveSlotState;
    [SerializeField]
    private ExistingSaveSlotStateData existingSaveSlotState;
    [SerializeField]
    private ExistingSaveSlotStateData actsClearedSaveSlotState;
    [SerializeField]
    private ExistingSaveSlotStateData curruptedSaveSlotState;
    [SerializeField]
    private bool isCorrupted = false;

    private void Reset() => this.animator = this.GetComponent<Animator>();

    private void Start()
    {
        if (this.animator == null)
        {
            this.Reset();
        }
    }

    public void OnSubmit(BaseEventData eventData) => this.saveController.OnSaveSlotSubmit(this);

    public void OnSelect(BaseEventData eventData) => this.saveController.OnSaveSlotHighlighted(this);

    private void Update() => this.UpdateSlotDataUI();

    /// <summary>
    /// Updates the save slot data UI - always shows the character select (new save) state
    /// </summary>
    private void UpdateSlotDataUI()
    {
        this.newSaveSlotState.SetDisplayState(this.playerData, true);
        this.existingSaveSlotState.SetDisplayState(this.playerData, false);
        this.actsClearedSaveSlotState.SetDisplayState(this.playerData, false);
        this.curruptedSaveSlotState.SetDisplayState(this.playerData, false);

        // Set the playerdata current scene to the first scene in the stage scene list
        if (GMSceneManager.Instance().GetSceneList().stageScenes.Count > 0)
        {
            this.playerData.SetCurrentScene(GMSceneManager.Instance().GetSceneList().stageScenes[0]);
        }
    }

    /// <summary>
    /// Get a reference to the save slots animator
    /// </summary>
    public Animator GetAnimator() => this.animator;

    /// <summary>
    /// Set the parent save controller of the object
    /// <param name="saveController"> The parent save controller of this slot</param>
    /// </summary>
    public void SetSaveController(MainMenuSaveController saveController) => this.saveController = saveController;

    /// <summary>
    /// Gets the player data for the current save slot
    /// </summary>
    public PlayerData GetPlayerData() => this.playerData;

    /// <summary>
    /// Set the player data for an existing slot
    /// <param name="playerData"> The saved playerdata for an existing slot</param>
    /// </summary>
    public void SetPlayerData(PlayerData playerData) => this.playerData = playerData;

    /// <summary>
    /// Gets the current slot button
    /// </summary>
    public Button GetSlotButton() => this.saveSlotButton;

    /// <summary>
    /// Set the slot id of the current slot item
    /// <param name="saveSlotId"> The slot id value</param>
    /// </summary>
    public void SetSaveSlotID(int saveSlotId) => this.saveSlotId = saveSlotId;

    /// <summary>
    /// Sets the save slot has  save state
    /// <param name="hasSaveData"> The has save value</param>
    /// </summary>
    public void SetHasSaveData(bool hasSaveData) => this.hasSaveData = hasSaveData;

    /// <summary>
    /// Updates the save slot debug mode and toggles the data
    /// </summary>
    public void ToggleDebugMode() => this.playerData.GetPlayerSettings().SetDebugMode(!this.playerData.GetPlayerSettings().GetDebugMode());

    /// <summary>
    /// Gets the slot id of the current object
    /// </summary>
    public int GetSaveSlotID() => this.saveSlotId;

    /// <summary>
    /// Cycles to the next character in the save slot
    /// </summary>
    public void GetNextCharacter()
    {
        int playableCharacters = Enum.GetNames(typeof(PlayableCharacter)).Length;
        int currentCharacterIndex = (int)this.playerData.GetCharacter();

        if (currentCharacterIndex + 1 >= playableCharacters - 1)
        {
            this.playerData.SetCharacter((PlayableCharacter)0);
        }
        else
        {
            this.playerData.SetCharacter((PlayableCharacter)currentCharacterIndex + 1);
        }
    }

    /// <summary>
    /// Cycles to the previous character in the save slot but skips over super sonic
    /// </summary>
    public void GetPreviousCharacter()
    {
        int playableCharacters = Enum.GetNames(typeof(PlayableCharacter)).Length;
        int currentCharacterIndex = (int)this.playerData.GetCharacter();

        if (currentCharacterIndex == 0)
        {
            // -2 cause super sonic should not be selected from the character select sceen
            this.playerData.SetCharacter((PlayableCharacter)(playableCharacters - 2));
        }
        else
        {
            this.playerData.SetCharacter((PlayableCharacter)currentCharacterIndex - 1);
        }
    }

    /// <summary>
    /// Get the <see cref="isCorrupted"/> value
    /// </summary>
    public bool GetIsCorrupted() => this.isCorrupted;

    /// <summary>
    /// Set the <see cref="isCorrupted"/> value
    /// </summary>
    public void SetIsCorrupted(bool isCorrupted) => this.isCorrupted = isCorrupted;
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// Manages the character select menu with a single hidden save slot
/// </summary>
public class MainMenuSaveController : MonoBehaviour
{
    [SerializeField, FirstFoldOutItem("Dependencies")]
    private MainMenuController mainMenuController;
    [Tooltip("The event system of the save system"), SerializeField]
    private MainMenuNodeData nodeData;
    [Tooltip("Manages the main prefab of the save item to be cloned"), SerializeField]
    private GameObject saveSlotPrefab;
    [Tooltip("Manages the main prefab for the no save slot"), SerializeField, LastFoldoutItem()]
    private GameObject noSaveSlotPrefab;

    [Tooltip("The id of the current slot"), SerializeField]
    private int currentSaveSlotId = 0;
    [Tooltip("The save items active on the scene"), SerializeField]
    private List<MainMenuSaveSlotController> activeSaveSlots;

    [Tooltip("The space between each menu item"), SerializeField, FirstFoldOutItem("Spawned Slot Positioning")]
    private float menuItemPadding = 112;
    [Tooltip("The speed in which menu items will be cycled through"), SerializeField]
    private float cycleSpeed = 300;
    [Tooltip("The position of the slot vertically"), SerializeField, LastFoldoutItem()]
    private float verticalPosition = 44;

    private void Awake()
    {
        this.SpawnCharacterSelectSlot();
        this.nodeData.firstSelectedButton = this.activeSaveSlots[0].gameObject;
    }

    /// <summary>
    /// Spawns a single slot that acts as the character select screen
    /// Uses SaveSlot.Slot_0 as a hidden save slot behind the scenes
    /// </summary>
    private void SpawnCharacterSelectSlot()
    {
        SaveSlot hiddenSlot = SaveSlot.Slot_0;
        MainMenuSaveSlotController characterSelectSlot = Instantiate(this.saveSlotPrefab).GetComponent<MainMenuSaveSlotController>();

        characterSelectSlot.transform.SetParent(this.transform);
        characterSelectSlot.transform.localPosition = new Vector2(0, -4);
        characterSelectSlot.name = "Character Select Slot";
        characterSelectSlot.SetSaveController(this);
        characterSelectSlot.SetSaveSlotID(0);

        // Load existing save to restore last-used character, otherwise create fresh data
        PlayerData playerData;

        if (GMSaveSystem.Instance().SaveExists(hiddenSlot))
        {
            try
            {
                playerData = GMSaveSystem.Instance().LoadPlayerData(hiddenSlot);
            }
            catch
            {
                playerData = new PlayerData(hiddenSlot);
            }
        }
        else
        {
            playerData = new PlayerData(hiddenSlot);
        }

        // Always show as "new save" state so the character selector is displayed
        characterSelectSlot.SetHasSaveData(false);
        characterSelectSlot.SetPlayerData(playerData);
        this.activeSaveSlots.Add(characterSelectSlot);
    }

    private void OnDisable()
    {
        for (int x = 0; x < this.activeSaveSlots.Count; x++)
        {
            this.activeSaveSlots[x].transform.localPosition = new Vector2(x * this.menuItemPadding, this.verticalPosition);
        }
    }

    /// <summary>
    /// Gets the current save slot and toggles its debug mode
    /// </summary>
    public void ToggleCurrentSaveSlot() => this.activeSaveSlots[this.currentSaveSlotId].ToggleDebugMode();

    /// <summary>
    /// Switches the character based on the direction passed
    /// <param name="direction">The vertical direction of the players input which signifies what character to cycle too</param>
    /// </summary>
    public void CycleCharacter(int direction)
    {
        if (direction == 1)
        {
            this.activeSaveSlots[this.currentSaveSlotId].GetNextCharacter();
            this.activeSaveSlots[this.currentSaveSlotId].GetAnimator().SetTrigger("Normal");
            this.activeSaveSlots[this.currentSaveSlotId].GetAnimator().SetTrigger("Selected");
        }
        else if (direction == -1)
        {
            this.activeSaveSlots[this.currentSaveSlotId].GetPreviousCharacter();
            this.activeSaveSlots[this.currentSaveSlotId].GetAnimator().SetTrigger("Normal");
            this.activeSaveSlots[this.currentSaveSlotId].GetAnimator().SetTrigger("Selected");
        }
    }

    /// <summary>
    /// Sets the interaction for all the slots
    /// <param name="value"> The new interactable value for the slots</param>
    /// </summary>
    public void SetSlotButtonsInteractable(bool value)
    {
        foreach (MainMenuSaveSlotController saveSlot in this.activeSaveSlots)
        {
            saveSlot.GetSlotButton().interactable = value;
        }
    }

    /// <summary>
    /// Gets the current cycle speed of all the slots
    /// </summary>
    public float GetCycleSpeed() => this.cycleSpeed;

    /// <summary>
    /// Performs the on slot submit action - saves character choice and loads the first stage
    /// <param name="saveSlot"> The save slot to load</param>
    /// </summary>
    public void OnSaveSlotSubmit(MainMenuSaveSlotController saveSlot)
    {
        PlayerData playerData = saveSlot.GetPlayerData();

        // Ensure the scene is set to the first stage
        if (GMSceneManager.Instance().GetSceneList().stageScenes.Count > 0)
        {
            playerData.SetCurrentScene(GMSceneManager.Instance().GetSceneList().stageScenes.First(x => x.GetSceneType() == SceneType.RegularStage));
        }

        // Clear watched cutscenes so they replay each time from the menu
        playerData.GetWatchedActStartCutscenes().Clear();

        // Save the character selection behind the scenes using Slot_0
        GMSaveSystem.Instance().SetSaveSlot(SaveSlot.Slot_0);

        if (GMSaveSystem.Instance().SaveExists(SaveSlot.Slot_0) == false)
        {
            GMSaveSystem.Instance().CreateNewSave(playerData);
        }

        GMSaveSystem.Instance().SetCurrentPlayerData(playerData);
        GMStageManager.Instance().SetOnSaveSlotGameLoad(true);
        GMSaveSystem.Instance().SaveAndOverwriteData();
        GMHistoryManager.Instance().ClearHistory();
        this.mainMenuController.OnLoadGameWithSaveData(GMSaveSystem.Instance().GetCurrentPlayerData());
        this.mainMenuController.OnButtonSelect();
    }

    /// <summary>
    /// Updates button visibility when the character select slot is highlighted
    /// <param name="saveSlot"> The save slot highlighted</param>
    /// </summary>
    public void OnSaveSlotHighlighted(MainMenuSaveSlotController saveSlot)
    {
        this.mainMenuController.OnButtonChange();
        this.currentSaveSlotId = 0;
        this.mainMenuController.GetDeleteButtonUI().SetActive(false);
        this.mainMenuController.GetConfirmButtonUI().SetActive(true);
        this.mainMenuController.GetGameOptionsButtonUI().SetActive(true);
    }
}

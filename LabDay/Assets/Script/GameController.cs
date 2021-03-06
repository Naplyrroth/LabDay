﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//We'll use GameStates to switch beetween Scenes (Overworld, Battle, etc)
public enum GameState { FreeRoam, Battle, Dialog, Cutscene, Menu, Pause } //List every states we'll use
public class GameController : MonoBehaviour
{
    GameState state;//Reference to our GameState

    GameState stateBeforePause;

    [SerializeField] PlayerController playerController;//Reference to the PlayerController script
    [SerializeField] BattleSystem battleSystem;//Reference to the BattleSystem Script 
    [SerializeField] MenuController menuController;//Reference to the MenuSystem Script 
    [SerializeField] Camera worldCamera; //Reference to our Camera

    public static GameController Instance { get; private set; } //Get reference from the game controller anywhere we want

    List<string> defeatedTrainerName = new List<string>();

    private void Awake()
    {
        Instance = this;
        ConditionsDB.Init();
    }

    //On the first frame we check if we enable our Overworld script, or the battle one
    private void Start()
    {
        battleSystem.OnBattleOver += EndBattle;

        DialogManager.Instance.OnShowDialog += () => //Change the state to dialog so the player won't be able to move will a dialog appears
        {
            state = GameState.Dialog;
        };
        DialogManager.Instance.OnCloseDialog += () =>
        {
            if (state == GameState.Dialog)
                state = GameState.FreeRoam;
        };
    }

    //Change our battle state, camera active, and gameobject of the Battle System
    public void StartBattle()
    {
        if (playerController.IntroTallGrass.isPlaying)
        {
            state = GameState.Cutscene;
            StartCoroutine(introWildAppeared());
        }
        else
        {
            state = GameState.Battle;
            battleSystem.gameObject.SetActive(true);
            worldCamera.gameObject.SetActive(false);

            var playerParty = playerController.GetComponent<PokemonParty>(); //Store our party in a var
            var wildPokemon = FindObjectOfType<MapArea>().GetComponent<MapArea>().GetRandomWildPokemon(); //Store a random wild pokemon FROM our map area in a var

            var wildPokemonCopy = new Pokemon(wildPokemon.Base, wildPokemon.Level); //Create a copy of the pokemon in the case the player want to catch it

            battleSystem.StartBattle(playerParty, wildPokemonCopy);   //Call our StartBattle, so every fight are not the same
        }
    }

    IEnumerator introWildAppeared()
    {
        yield return new WaitUntil(() => !playerController.IntroTallGrass.isPlaying);
        StartBattle();
    }

    TrainerController trainer; //Reference the trainer

    public void StartTrainerBattle(TrainerController trainer)
    {
        state = GameState.Battle;
        battleSystem.gameObject.SetActive(true);
        worldCamera.gameObject.SetActive(false);

        this.trainer = trainer; //Set THIS specific trainer as our reference

        var playerParty = playerController.GetComponent<PokemonParty>(); //Store our party in a var
        var trainerParty = trainer.GetComponent<PokemonParty>(); //Store the trainer party in a var

        battleSystem.StartTrainerBattle(playerParty, trainerParty); //Call our StartBattle, so every fight are not the same
    }

    public void OnEnterTrainersView(TrainerController trainer)
    {
        bool battleLost = false;
        foreach (string trainerName in GameController.Instance.DefeatedTrainerName)
        {
            if (trainer.Name == trainerName)
            {
                battleLost = true;
                break;
            }
            else
                battleLost = false;
        }
        if (!battleLost)
        {
            state = GameState.Cutscene;
            StartCoroutine(trainer.TriggerTrainerBattle(playerController));
        }
        else
        {
            trainer.BattleLost();
        }
    }

    //Change our battle state, camera active, and gameobject of the Battle System
    void EndBattle(bool won)
    {
        if (trainer != null)
        {
            if (won) //If it is a trainer battle, won by the player
            {
                trainer.BattleLost(); //Disable the fov, to disable the battle
                defeatedTrainerName.Add(trainer.Name);

                trainer = null;
            }
            else //If we lose the battle
            {
                playerController.LoseBattle();
            }
        }

        state = GameState.FreeRoam;
        battleSystem.gameObject.SetActive(false);
        worldCamera.gameObject.SetActive(true);
    }

    public List<string> DefeatedTrainerName
    {
        get => defeatedTrainerName;
    }

    public void HealPlayerTeam()
    {
        PokemonParty pokemonParty = playerController.GetComponent<PokemonParty>();
        foreach (Pokemon pokemon in pokemonParty.Pokemons)
        {
            pokemon.HP = pokemon.MaxHp;
            pokemon.CureStatus();
            foreach(Move move in pokemon.Moves)
            {
                move.PP = move.Base.Pp;
            }
        }
    }

    void OpenMenu(bool isOpening)
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.I))
        {
            menuController.gameObject.SetActive(isOpening);
            if (isOpening)
            {
                state = GameState.Menu;
            }
            else
            {
                state = GameState.FreeRoam;
            }
        }
    }

    public void PauseGame(bool pause)
    {
        if (pause)
        {
            stateBeforePause = state;
            state = GameState.Pause;
        }
        else
        {
            state = stateBeforePause;
        }
    }

    private void Update()
    {
        switch (state)
        {
            case GameState.FreeRoam: //While we are in the overworld, we use our PlayerController script
                playerController.HandleUpdate();
                OpenMenu(true);
                break;

            case GameState.Battle: //Else if we are in a battle, we'll disable our PlayerController Script
                battleSystem.HandleUpdate();
                break;

            case GameState.Dialog:
                DialogManager.Instance.HandleUpdate();
                break;

            case GameState.Menu:
                menuController.HandleUpdate(playerController);
                OpenMenu(false);
                break;

            default:
                break;
        }
    }
}
﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Here is where we manage our battle system, by calling every needed function, that we create in other classes

public enum BattleState { Start, ActionSelection, MoveSelection, RunningTurn, Busy, PartyScreen, BattleOver} //We will use different state in our BattleSystem, and show what need to be shown in a specific state
public enum BattleActions { Move, SwitchPokemon, UseItem, Run}

public class BattleSystem : MonoBehaviour
{
    [SerializeField] BattleUnit playerUnit;
    [SerializeField] BattleUnit enemyUnit;
    [SerializeField] BattleDialogBox dialogBox;
    [SerializeField] PartyScreen partyScreen;

    public event Action<bool> OnBattleOver; //Add an action happening when the battle ended (Action<bool> is to add a bool to the Action)

    BattleState state;
    BattleState? prevState; //Store the previous state of the battle, we'll mainly use this to switch pokemons (? is for making it a variable)

    //These var will be used to navigate throught selection screens
    int currentAction; //Actually, 0 is Fight, 1 is Bag, 2 is Party, 4 is Run
    int currentMove; //We'll have 4 moves
    int currentMember; //We have 6 pokemons

    PokemonParty playerParty;
    Pokemon wildPokemon;

    //We want to setup everything at the very first frame of the battle
    public void StartBattle(PokemonParty playerParty, Pokemon wildPokemon)
    {
        this.playerParty = playerParty; //this. is to use our variable and not the parameter
        this.wildPokemon = wildPokemon;
        StartCoroutine(SetupBattle()); //We call our SetupBattle function
    }
    public IEnumerator SetupBattle() //We use the data created in the BattleUnit and BattleHud scripts
    {
        playerUnit.Setup(playerParty.GetHealthyPokemon());
        enemyUnit.Setup(wildPokemon);

        partyScreen.Init();

        dialogBox.SetMoveNames(playerUnit.Pokemon.Moves);

        //We return the function Typedialog
        yield return dialogBox.TypeDialog($"A wild {enemyUnit.Pokemon.Base.Name} appeared."); //With the $, a string can show a special variable in it

        //This is the function where the player choose a specific action
        ActionSelection(); 
    }

    void BattleOver(bool won) //Function to know if the battle is over or not
    {
        state = BattleState.BattleOver; //Set the state
        playerParty.Pokemons.ForEach(p => p.OnBattleOver()); //Reset the stats of all our pokemons in a ForEach loop
        enemyUnit.Pokemon.CureStatus(); //Cure status from the enemy pokemon so it won't keep it every time we fight
        OnBattleOver(won); //Calling the event to notify the GameController that the battle is Over
    }

    void ActionSelection()
    {
        state = BattleState.ActionSelection; //Change the state to ActionSelection
        dialogBox.SetDialog("Choose an action"); //Then write a text
        dialogBox.EnableActionSelector(true); //Then allow player to choose an Action
    }

    void OpenPartyScreen()
    {
        state = BattleState.PartyScreen; //Change the battle state to party screen
        partyScreen.SetPartyData(playerParty.Pokemons); //Set the data of our actual pokemons
        partyScreen.gameObject.SetActive(true); //Set active and visible our party screen
    }

    void MoveSelection()
    {
        state = BattleState.MoveSelection; //Change the state to MoveSelection
        dialogBox.EnableActionSelector(false); //Then disable player to choose an action, and allow it to choose a move
        dialogBox.EnableDialogText(false); //Disable the DialogText
        dialogBox.EnableMoveSelector(true); //Enable the MoveSelector
    }

    IEnumerator RunTurns(BattleActions playerAction) //Coroutine to manage the turns
    {
        state = BattleState.RunningTurn; //switch the battle state

        if (playerAction == BattleActions.Move) //If the move Action is selected
        {
            playerUnit.Pokemon.CurrentMove = playerUnit.Pokemon.Moves[currentMove]; //Store the move we'll select
            enemyUnit.Pokemon.CurrentMove = enemyUnit.Pokemon.GetRandomMove(); //Store the move the enemy will use

            //Check who goes first
            bool playerGoesFirst = playerUnit.Pokemon.Speed >= enemyUnit.Pokemon.Speed; //True if the player's pokemon speed is higher

            var firstUnit = (playerGoesFirst) ? playerUnit:enemyUnit; //If the bool is true, the player unit goes first, then the enemy
            var secondUnit = (playerGoesFirst) ? enemyUnit:playerUnit; //Else, we reverse it

            var secondPokemon = secondUnit.Pokemon; //This is in case the player switch as an action

            //We can now call the move in the order
            //First turn
            yield return RunMove(firstUnit, secondUnit, firstUnit.Pokemon.CurrentMove);
            yield return RunAfterTurn(firstUnit); //Call the function happening after turn (poison damage, burn, etc..)
            if (state == BattleState.BattleOver) yield break; //If the battle is over, break the coroutine

            if (secondPokemon.HP > 0)
            {
                //Second Turn
                yield return RunMove(secondUnit, firstUnit, secondUnit.Pokemon.CurrentMove);
                yield return RunAfterTurn(secondUnit); //Call the function happening after turn (poison damage, burn, etc..)
                if (state == BattleState.BattleOver) yield break; //If the battle is over, break the coroutine
            }
        }
        else
        {
            if (playerAction == BattleActions.SwitchPokemon) //If the player decided to switch
            {
                var selectedPokemon = playerParty.Pokemons[currentMember]; //check the selected pokemon
                state = BattleState.Busy; //Set to busy
                yield return SwitchPokemon(selectedPokemon); //Call the coroutine
            }

            //Once the player switched, the enemy get the turn
            var enemyMove = enemyUnit.Pokemon.GetRandomMove();
            yield return RunMove(enemyUnit, playerUnit, enemyMove);
            yield return RunAfterTurn(enemyUnit); //Call the function happening after turn (poison damage, burn, etc..)
            if (state == BattleState.BattleOver) yield break; //If the battle is over, break the coroutine
        }

        if (state != BattleState.BattleOver)
            ActionSelection();

    }
    IEnumerator RunMove(BattleUnit sourceUnit, BattleUnit targetUnit, Move move) //Creating a function with the logic of the moves, to easily change it later and make our code more clear
    {
        bool canRunMove = sourceUnit.Pokemon.OnBeforeMove(); //Store in a boolean the check for paralyze, freeze or burn
        if (!canRunMove)
        {
            yield return ShowStatusChanges(sourceUnit.Pokemon);
            yield return sourceUnit.Hud.UpdateHP();
            yield break; //If the pokemon can not move, we break the coroutine
        }
        yield return ShowStatusChanges(sourceUnit.Pokemon);

        move.PP--; //Redcing PP of the move on use
        if (targetUnit == playerUnit) //If statement to show if the pokemon using a move is the player's one of the enemy
            yield return dialogBox.TypeDialog($"The enemy {sourceUnit.Pokemon.Base.Name} used {move.Base.Name}"); //We write to the player that a pokemon used a move
        else
            yield return dialogBox.TypeDialog($"Your {sourceUnit.Pokemon.Base.Name} used {move.Base.Name}");

        if (CheckIfMoveHits(move, sourceUnit.Pokemon, targetUnit.Pokemon))
        {
            sourceUnit.PlayAttackAnimation(); //Calling the attack animation right after displaying a message
            yield return new WaitForSeconds(0.75f); //Then wait for a second before reducing HP
            targetUnit.PlayHitAnimation();

            if (move.Base.Category == MoveCategory.Status) //If our move is a status move we call it
            {
                yield return RuneMoveEffects(move.Base.Effects, sourceUnit.Pokemon, targetUnit.Pokemon, move.Base.Target);
            }
            else //Else we just call the damages
            {
                var damageDetails = targetUnit.Pokemon.TakeDamage(move, sourceUnit.Pokemon);
                yield return targetUnit.Hud.UpdateHP(); //Calling the function to show damages taken
                yield return ShowDamageDetails(damageDetails);
            }

            if (move.Base.Secondaries != null && move.Base.Secondaries.Count > 0 && targetUnit.Pokemon.HP > 0) //If we CAN call a secondary effects, we call it
            {
                foreach (var secondary in move.Base.Secondaries)//loop throught all the secondaries effects
                {
                    var rnd = UnityEngine.Random.Range(1, 101); //Rnd = RaNDom number
                    if (rnd <= secondary.Chance) //Calculating the stats that a secondary happen
                        yield return RuneMoveEffects(secondary, sourceUnit.Pokemon, targetUnit.Pokemon, secondary.Target);
                }
            }

            //If a pokemon died, we display a message, then check if the battle is over or not
            if (targetUnit.Pokemon.HP <= 0) //Since with status move the pokemon can faint at mostly any moment, we don't check DamageDetails.fainted
            {
                if (targetUnit == enemyUnit)
                    yield return dialogBox.TypeDialog($"{targetUnit.Pokemon.Base.Name} enemy fainted");
                else
                    yield return dialogBox.TypeDialog($"Your {targetUnit.Pokemon.Base.Name} fainted");
                targetUnit.PlayFaintAnimation();
                yield return new WaitForSeconds(2f);

                CheckForBattleOver(targetUnit);
            }
        }

        else
        {
            yield return dialogBox.TypeDialog($"{sourceUnit.Pokemon.Base.Name}'s attack missed");
        }
    }

    IEnumerator RuneMoveEffects(MoveEffects effects, Pokemon source, Pokemon target, MoveTarget moveTarget) //Creating a function of the Effects move, so we'll call it easyly
    {
        if (effects.Boosts != null) //Call for stat boost
        {
            if (moveTarget == MoveTarget.Self)
                source.ApplyBoosts(effects.Boosts);
            else
                target.ApplyBoosts(effects.Boosts);
        }

        //Check from the dictionnary if there are any status condition, and call for status
        if (effects.Status != ConditionID.none)
        {
            target.SetStatus(effects.Status);
        }
        //Same with volatile status
        if (effects.VolatileStatus != ConditionID.none)
        {
            target.SetVolatileStatus(effects.VolatileStatus);
        }

        yield return ShowStatusChanges(source);
        yield return ShowStatusChanges(target);
    }

    IEnumerator RunAfterTurn(BattleUnit sourceUnit) //Call this before the turn is over
    {
        //It have to run only if the battle is NOT over
        if (state == BattleState.BattleOver) yield break;
        yield return new WaitUntil(() => state == BattleState.RunningTurn); //This script only have to happen once the RunningTurn state is over

        //Statuses like burn or poison could hurt the pokemon after the turn is over
        sourceUnit.Pokemon.OnAfterTurn(); //Call the after turn function, to perhaps clear the status
        yield return ShowStatusChanges(sourceUnit.Pokemon);
        yield return sourceUnit.Hud.UpdateHP();

        if (sourceUnit.Pokemon.HP <= 0) //Check again bc the pokemon could have fainted due to poison or burn
        {
            if (sourceUnit == enemyUnit)
                yield return dialogBox.TypeDialog($"{sourceUnit.Pokemon.Base.Name} enemy fainted");
            else
                yield return dialogBox.TypeDialog($"Your {sourceUnit.Pokemon.Base.Name} fainted");
            sourceUnit.PlayFaintAnimation();
            yield return new WaitForSeconds(2f);

            CheckForBattleOver(sourceUnit);
        }
    }
    //Function to check if the move hits
    bool CheckIfMoveHits(Move move, Pokemon source, Pokemon target)
    {
        if (move.Base.AlwaysHits)
            return true;

        float moveAccuracy = move.Base.Accuracy;

        int accuracy = source.StatBoosts[Stat.Accuracy]; //Store in an int the accuracy
        int evasion = target.StatBoosts[Stat.Evasion]; //And the evasion

        var boostValues = new float[] { 1f, 4f/3f, 5f/3f, 2f, 7f/3f, 8f/3f, 3f }; //Store the possible value of boosting

        //Modify accuracy
        if (accuracy > 0)
            moveAccuracy *= boostValues[accuracy];
        else
            moveAccuracy /= boostValues[-accuracy];

        //Modify evasion, by reversing the boost of accuracy of the opponent
        if (accuracy > 0)
            moveAccuracy /= boostValues[evasion];
        else
            moveAccuracy *= boostValues[-evasion];

        return UnityEngine.Random.Range(1, 101) <= moveAccuracy; //Return the value of the actual accuracy
    }

    IEnumerator ShowStatusChanges(Pokemon pokemon) //Check if there are any messages in the Status changes queue, then show all of them in dialogBox
    {
        while (pokemon.StatusChanges.Count > 0) //Means that there is a message
        {
            var message = pokemon.StatusChanges.Dequeue();
            yield return dialogBox.TypeDialog(message);
        }
    }

    void CheckForBattleOver (BattleUnit faintedUnit) //Logic to know if the fainted pokemon is the player's one or the enemy one, and so if the battle is over or not
    {
        if (faintedUnit.IsPlayerUnit)
        {
            var nextPokemon = playerParty.GetHealthyPokemon(); //Store in a var out next pokemon
            if (nextPokemon != null) //we open the party screen when a pokemon of us fainted, and we still have at least one healthy pokemon
                OpenPartyScreen();
            else
                BattleOver(false); //False is when the player lost
        }
        else
            BattleOver(true); //True when the player won
    }

    IEnumerator ShowDamageDetails(DamageDetails damageDetails)
    {
        if (damageDetails.Critical > 1f) //Check the value of Critical to show a message saying we had a critical hit
            yield return dialogBox.TypeDialog("A critical hit!");

        if(damageDetails.TypeEffectiveness > 1)
            yield return dialogBox.TypeDialog("That's super effective!");
        else if (damageDetails.TypeEffectiveness < 1)
            yield return dialogBox.TypeDialog("That was not very effective..");

    }

    public void HandleUpdate()
    {
        if (state == BattleState.ActionSelection)
        {
            HandleActionSelection(); //Function to make the player able to choose an action
        }
        else if (state == BattleState.MoveSelection)
        {
            HandleMoveSelection();
        }
        else if (state == BattleState.PartyScreen)
        {
            HandlePartySelection();
        }
    }

    void HandleActionSelection()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
            ++currentAction;
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
            --currentAction;
        else if (Input.GetKeyDown(KeyCode.DownArrow))
            currentAction += 2;
        else if (Input.GetKeyDown(KeyCode.UpArrow))
            currentAction -= 2;

        currentAction = Mathf.Clamp(currentAction, 0, 3); //Since we have 4 actions we want to loop throught each one of them and not going beyond 3

            dialogBox.UpdateActionSelection(currentAction);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            if (currentAction == 0)
            {
                //Fight*
                MoveSelection();
            }
            else if (currentAction == 1)
            {
                //Bag
            }
            else if (currentAction == 2)
            {
                //Pokemon party
                prevState = state; //If the state was enemy move, this means the player lost a pokemon and can switch, else it mean he decided to switch, so he lost a turn
                OpenPartyScreen();
            }
            else if (currentAction == 3)
            {
                //Run
            }
        }
    }

    //We create a way to move freely between every moves our Creature actually has.
    void HandleMoveSelection()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
            ++currentMove;
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
            --currentMove;
        else if (Input.GetKeyDown(KeyCode.DownArrow))
            currentMove += 2;
        else if (Input.GetKeyDown(KeyCode.UpArrow))
            currentMove -= 2;

        currentMove = Mathf.Clamp(currentMove, 0, playerUnit.Pokemon.Moves.Count - 1); //Since we have ""player unit" number of moves we want to loop throught each one of them and not going beyond

        dialogBox.UpdateMoveSelection(currentMove, playerUnit.Pokemon.Moves[currentMove]);

        //Here we'll make the move happen
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            //First we change the box to dialogText
            dialogBox.EnableMoveSelector(false);
            dialogBox.EnableDialogText(true);
            //Then we start the coroutine
            StartCoroutine(RunTurns(BattleActions.Move));
        }
        else if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            dialogBox.EnableMoveSelector(false);
            dialogBox.EnableDialogText(true);
            ActionSelection();
        }
    }

    void HandlePartySelection()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
            ++currentMember;
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
            --currentMember;
        else if (Input.GetKeyDown(KeyCode.DownArrow))
            currentMember += 3;
        else if (Input.GetKeyDown(KeyCode.UpArrow))
            currentMember -= 3;

        currentMember = Mathf.Clamp(currentMember, 0, playerParty.Pokemons.Count - 1);

        partyScreen.UpdateMemberSelection(currentMember);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            var selectedMember = playerParty.Pokemons[currentMember]; //Creating a var of the actual pokemon we are on
            if (selectedMember.HP <= 0) //Making sure the actual pokemon selected ain't fainted
            {
                partyScreen.SetMessageText("You can't send out a fainted pokemon!");
                return;
            }
            if (selectedMember == playerUnit.Pokemon) //Making sure the actual selected pokemon is not the same as the one in the battle
            {
                partyScreen.SetMessageText("This pokemon is already in the battle.");
                return;
            }

            partyScreen.gameObject.SetActive(false); //Changing the actual view on the screen

            if (prevState == BattleState.ActionSelection) //the player decided to change
            {
                prevState = null; //Reste the prev state before doing anything else
                StartCoroutine(RunTurns(BattleActions.SwitchPokemon)); //Call the coroutine to switch
            }
            else //Here the pokemon fainted, so the player won't lost a turn
            {
                state = BattleState.Busy; //State is changed to busy so player won't mess with the UI
                StartCoroutine(SwitchPokemon(selectedMember)); //Calling coroutine to switch pokemons
            }
            
        }
        else if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            partyScreen.gameObject.SetActive(false);
            ActionSelection();
        }
    }

    IEnumerator SwitchPokemon (Pokemon newPokemon) //Coroutine to make the switch happen
    {
        if (playerUnit.Pokemon.HP > 0) //This will play ONLY if the player change pokemon by choosing the action, if the pokemon fainted and we have to send another one, this will not play
        {
            yield return dialogBox.TypeDialog($"Come back {playerUnit.Pokemon.Base.Name}"); //First we change the message
            playerUnit.PlayFaintAnimation(); //Then we play the faint animation to show that our pokemon came back
            yield return new WaitForSeconds(1f); //Then we wait before it ends
        }

        playerUnit.Setup(newPokemon);
        dialogBox.SetMoveNames(newPokemon.Moves);
        yield return dialogBox.TypeDialog($"Go {newPokemon.Base.Name}!");

        state = BattleState.RunningTurn; //go back to the running turn state
    }
}
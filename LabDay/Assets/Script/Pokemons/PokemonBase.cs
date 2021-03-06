﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// !!! THIS SCRIPT IS THE BASE FROM ALL POKEMON, WITH STATS, TYPES, SPRITES, ETC, NOT THE ACTUAL POKEMONS !!!

[CreateAssetMenu(fileName = "Pokemon", menuName = "Pokemon/Créer un nouveau pokemon")] //We create a menu in Unity to acces this blueprint
public class PokemonBase : ScriptableObject //Changed from "MonoBehavior" to "ScriptableObject" because we'll do some blueprints with theses datas
{
    // Inside we'll create variables to store the data of the pokemons

    // SerializedField is used instead of public, so we can use them in some other classes more easily, and modify them in Unity, or others scripts
    [SerializeField] new string name;

    [TextArea] //This will give us some space to write a description
    [SerializeField] string description;

    [SerializeField] Sprite frontSprite;
    [SerializeField] Sprite backSprite;

    [SerializeField] PokemonType type1;
    [SerializeField] PokemonType type2;

    //Base stats
    [SerializeField] int maxHp;
    [SerializeField] int attack;
    [SerializeField] int defense;
    [SerializeField] int spAttack;
    [SerializeField] int spDefense;
    [SerializeField] int speed;

    [SerializeField] int expYield; //Int to know how much xp a pokemon is gonna get from winning against this pokemon
    [SerializeField] GrowthRate growthRate;

    [SerializeField] int catchRate = 255; //int we'll use in the catching algorithm. The higher the catchRate, the easier it will be caught. Max value is 255

    [SerializeField] List<LearnableMoves> learnableMoves;

    public static int MaxNumberOfMoves { get; set;} = 4;

    public int GetExpForLevel(int level) //Function to get the xp needed to level up
    {
        if (growthRate == GrowthRate.Fast) //Different values for different growth rate
        {
            return 4 * (level * level * level) / 5;
        }
        else if (growthRate == GrowthRate.MediumFast)
        {
            return level * level * level;
        }

        return -1;
    }

    public string Name
    {
        get { return name; } //Set a getter of the name so we could just call pBase.Name in the other scripts.
    }
    public string Description
    {
        get { return description; }
    }
    public int MaxHp
    {
        get { return maxHp; }
    }
    public int Attack
    {
        get { return attack; }
    }
    public int Defense
    {
        get { return defense; }
    }
    public int SpAttack
    {
        get { return spAttack; }
    }
    public int SpDefense
    {
        get { return spDefense; }
    }
    public int Speed
    {
        get { return speed; }
    }
    public PokemonType Type1
    {
        get { return type1; }
    }
    public PokemonType Type2
    {
        get { return type2; }
    }
    public Sprite BackSprite
    {
        get { return backSprite; }
    }
    public Sprite FrontSprite
    {
        get { return frontSprite; }
    }
    public List<LearnableMoves> LearnableMoves
    {
        get { return learnableMoves; }
    }
    public int CatchRate => catchRate; //Just learned a new way of writing a property
    public int ExpYield => expYield;
    public GrowthRate GrowthRate => growthRate;

}

//Here we set the moves a pokemon can learn
[System.Serializable]
public class LearnableMoves
{
    [SerializeField] MoveBase moveBase; //This is a reference to the MoveBase script
    [SerializeField] int level;

    //here we get the Move that will be learnable, and the level at wich it'll be learnable
    public MoveBase Base
    {
        get { return moveBase; } 
    }
    public int Level
    {
        get { return level; }
    }
}

public enum PokemonType //Using an enum to acces all the pokemon types easily
{
    None,
    Insecte,
    Ténebre,
    Dragon,
    Electrique,
    Fée,
    Combat,
    Feu,
    Vol,
    Spectre,
    Plante,
    Sol,
    Glace,
    Normal,
    Poison,
    Psy,
    Roche,
    Acier,
    Eau,
}

public enum GrowthRate //We use these var to know how much xp a pokemon will need to get to next lvl
{
    Fast, MediumFast
}

//We'll use this in a Dictionnary to get the stat at any given level
public enum Stat
{
    ATK,
    DEF,
    SpATK,
    SpDEF,
    VIT,
    //These 2 are not stats, they're just used to boost the moveAccuracy
    PRC,
    ESQ,
    Hp,
}

public class TypeChart //Create the chart to manage types and their effectiveness. It will look like a chart
{
    static float[][] chart = //2D Array, where we wright every type and their weakness, effectiveness
    {
        //                Bug   Drk    Drg    Ele   Fai   Fig   Fir   Fly   Ghs   Gra   Gro   Ice   Nrm Psn   Psy  Rck    Ste   Wtr
        /*BUG*/new float[]{1f,   2f,   0.5f,  1f,   1f,   1f,   0.5f, 0.5f, 0.5f, 2f,   1f,   1f,   1f, 1f,   2f,  0.5f,  0.5f, 1f},
        /*DRK*/new float[]{1f,   0.5f, 1f,    1f,   2f,   0.5f, 1f,   1f,   2f,   1f,   1f,   1f,   1f, 1f,   2f,   1f,   1f,   1f},
        /*DRG*/new float[]{1f,   1f,   2f,    1f,   0.5f, 1f,   1f,   1f,   1f,   1f,   1f,   1f,   1f, 1f,   1f,   1f,   1f,   1f},
        /*ELE*/new float[]{1f,   1f,   0.5f,  0.5f, 1f,   1f,   1f,   2f,   1f,   0.5f, 0f,   1f,   1f, 1f,   1f,   1f,   1f,   2f},
        /*FAI*/new float[]{1f,   2f,   2f,    1f,   1f,   2f,   0.5f, 1f,   1f,   1f,   1f,   1f,   1f, 0.5f, 1f,   1f,   0.5f, 1f},
        /*FIG*/new float[]{0.5f, 2f,   1f,    1f,   0.5f, 1f,   1f,   0.5f, 0f,   1f,   1f,   2f,   2f, 1f,   0.5f, 2f,   2f,   1f},
        /*FIR*/new float[]{2f,   1f,   0.5f,  1f,   1f,   1f,   0.5f, 1f,   1f,   2f,   1f,   2f,   1f, 1f,   1f,   0.5f, 2f,   0.5f},
        /*FLY*/new float[]{2f,   1f,   1f,    0.5f, 1f,   2f,   1f,   1f,   1f,   2f,   1f,   1f,   1f, 1f,   1f,   0.5f, 0.5f, 1f},
        /*GHS*/new float[]{1f,   0.5f, 1f,    1f,   1f,   1f,   1f,   1f,   2f,   1f,   1f,   1f,   0f, 1f,   2f,   1f,   1f,   1f},
        /*GRA*/new float[]{0.5f, 1f,   0.5f,  1f,   1f,   1f,   0.5f, 0.5f, 1f,   0.5f, 2f,   1f,   1f, 0.5f, 1f,   2f,   1f,   2f},
        /*GRO*/new float[]{0.5f, 1f,   1f,    2f,   1f,   1f,   2f,   0f,   1f,   0.5f, 1f,   1f,   1f, 2f,   1f,   2f,   2f,   1f},
        /*ICE*/new float[]{1f,   1f,   2f,    1f,   1f,   0.5f, 0.5f, 2f,   1f,   2f,   2f,   0.5f, 1f, 1f,   1f,   1f,   0.5f, 0.5f},
        /*NRM*/new float[]{1f,   1f,   1f,    1f,   1f,   1f,   1f,   1f,   0f,   1f,   1f,   1f,   1f, 1f,   1f,   0.5f, 0.5f, 1f},
        /*PSN*/new float[]{1f,   1f,   1f,    1f,   2f,   1f,   1f,   1f,   0.5f, 2f,   0.5f, 1f,   1f, 1f,   1f,   0.5f, 0f,   1f},
        /*PSY*/new float[]{1f,   0f,   1f,    1f,   1f,   2f,   1f,   1f,   1f,   1f,   1f,   1f,   1f, 2f,   0.5f, 1f,   1f,   1f},
        /*RCK*/new float[]{2f,   1f,   1f,    1f,   1f,   0.5f, 1f,   2f,   1f,   1f,   0.5f, 2f,   1f, 1f,   1f,   0.5f, 0.5f, 1f},
        /*STE*/new float[]{2f,   1f,   1f,    1f,   2f,   0.5f, 0.5f, 1f,   1f,   1f,   2f,   1f,   1f, 1f,   1f,   2f,   0.5f, 0.5f},
        /*WTR*/new float[]{1f,   1f,   0.5f,  1f,   1f,   1f,   2f,   1f,   1f,   0.5f, 2f,   1f,   1f, 1f,   1f,   2f,   1f,   0.5f}
    };

    //Function to return the effectiveness of a move
    public static float GetEffectiveness(PokemonType attackType, PokemonType defenseType)
    {
        if (attackType == PokemonType.None || defenseType == PokemonType.None)
        {
            return 1;
        }

        //We wrote -1 bc of null type
        int row = (int)attackType - 1; //Get the row
        int col = (int)defenseType - 1; //And the column

        return chart[row][col];

    }
}

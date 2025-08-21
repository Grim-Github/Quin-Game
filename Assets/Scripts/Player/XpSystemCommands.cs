using CommandTerminal;
using UnityEngine;

/// <summary>
/// This file was created by Gemini.
/// It adds console commands for the XpSystem using the CommandTerminal.
/// </summary>
public class XpSystemCommands
{
    static XpSystem GetXpSystem()
    {
        var player = GameObject.FindGameObjectWithTag("GameController");
        if (player == null)
        {
            Terminal.Shell.IssueErrorMessage("XpSystemCommands: Could not find GameObject with 'Player' tag.");
            return null;
        }

        var xpSystem = player.GetComponent<XpSystem>();
        if (xpSystem == null)
        {
            Terminal.Shell.IssueErrorMessage("XpSystemCommands: Could not find XpSystem component on 'Player' GameObject.");
            return null;
        }

        return xpSystem;
    }

    [RegisterCommand(Name = "xp.add", Help = "Adds experience points to the player", MinArgCount = 1, MaxArgCount = 1)]
    static void CommandAddXp(CommandArg[] args)
    {
        int amount = args[0].Int;
        if (Terminal.IssuedError) return;

        var xpSystem = GetXpSystem();
        if (xpSystem != null)
        {
            int levelsGained = xpSystem.AddExperience(amount);
            Terminal.Log($"Added {amount} XP. You gained {levelsGained} level(s).");
        }
    }

    [RegisterCommand(Name = "xp.set_level", Help = "Sets the player's level. Optionally keeps current XP progress.", MinArgCount = 1, MaxArgCount = 2)]
    static void CommandSetLevel(CommandArg[] args)
    {
        int level = args[0].Int;
        bool resetXp = true;

        if (args.Length > 1)
        {
            resetXp = args[1].Bool;
        }

        if (Terminal.IssuedError) return;

        var xpSystem = GetXpSystem();
        if (xpSystem != null)
        {
            xpSystem.SetLevel(level, resetXp);
            Terminal.Log($"Set level to {level}. XP in level was {(resetXp ? "reset" : "kept")}.");
        }
    }

    [RegisterCommand(Name = "xp.status", Help = "Shows the player's current XP status")]
    static void CommandXpStatus(CommandArg[] args)
    {
        var xpSystem = GetXpSystem();
        if (xpSystem != null)
        {
            if (xpSystem.IsMaxLevel)
            {
                Terminal.Log($"Player is at max level: {xpSystem.CurrentLevel}");
            }
            else
            {
                Terminal.Log($"Level: {xpSystem.CurrentLevel}");
                Terminal.Log($"XP: {xpSystem.CurrentXpInLevel} / {xpSystem.XpNeededThisLevel}");
                Terminal.Log($"Progress to next level: {xpSystem.Progress01:P2}");
            }
        }
    }
}

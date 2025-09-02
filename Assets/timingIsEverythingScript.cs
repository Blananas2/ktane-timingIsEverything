using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;

public class timingIsEverythingScript : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombModule Module;
    public KMBombInfo Bomb;

    public KMSelectable ButtonSel;
    public MeshRenderer ButtonMesh;
    public MeshRenderer[] Lights;
    public Material LightGreen;
    public TextMesh Text;
    public TextMesh Exclaim;
    public Material[] ButtonColors;

    bool moduleReady;
    float startTime;
    float? displayedTime;
    float? lastStrike = null;
    float[] chosenTimes = { -1f, -1f, -1f };
    string[] timeStrings = { "---", "---", "---" };
    int submittedStages = 0;
    Coroutine buttonHold;
    bool holding = false;

    bool TwitchPlaysSkipTimeAllowed = true;
    bool TimeModeActive = false;
    bool ZenModeActive = false;
    bool calcHours = false; //set to true if Bomb Timer Modifier requires it

    //Logging
    static int moduleIdCounter = 1;
    int moduleId;
    bool moduleSolved;
    bool tpCorrect;

    void Awake()
    {
        moduleId = moduleIdCounter++;

        Module.OnActivate += ModuleStart;
        ButtonSel.OnInteract += delegate { ButtonPush(); return false; };
        ButtonSel.OnInteractEnded += delegate { ButtonRelease(); };
    }

    void ModuleStart()
    {
        startTime = Mathf.FloorToInt(Bomb.GetTime());

        //this is the Bomb Timer Modifier display hours check, thanks to julie this!
        var btmInfoType = ReflectionHelper.FindType("CustomBombInfo", "KtaneTimerV2");
        if (btmInfoType != null)
        {
            var btmInfo = GetComponent(btmInfoType);
            if (btmInfo)
                calcHours = btmInfo.GetValue<bool>("UseHours");
        }

        if (ZenModeActive)
        {
            Debug.LogFormat("[Timing is Everything #{0}] Current Mode: Zen", moduleId);
            ButtonMesh.material = ButtonColors[2];
            GenerateTimes(3, 0f, 120f * Bomb.GetSolvableModuleNames().Count);
        }
        else if (TimeModeActive)
        {
            Debug.LogFormat("[Timing is Everything #{0}] Current Mode: Time", moduleId);
            ButtonMesh.material = ButtonColors[1];
            GenerateTimes(3, 0f, startTime);
        }
        else
        {
            Debug.LogFormat("[Timing is Everything #{0}] Current Mode: Normal", moduleId);
            GenerateTimes(3, 0f, startTime);
        }
    }

    void GenerateTimes(int number, float minTime, float maxTime)
    {
        //times
        for (int t = 0; t < 3; t++)
        {
            if (3 - number > t)
            {
                chosenTimes[t] = ZenModeActive ? Single.MinValue : Single.MaxValue; //so that when they're sorted, it's moved to a position of a time already dealt with
            }
            else
            {
                if (maxTime <= 60f && !ZenModeActive)
                {
                    chosenTimes[t] = Mathf.FloorToInt(UnityEngine.Random.Range(minTime, maxTime));
                }
                else
                {
                    chosenTimes[t] = Mathf.FloorToInt(UnityEngine.Random.Range(minTime + 20f, maxTime - 21f));
                }
            }
        }

        Array.Sort(chosenTimes);
        if (!ZenModeActive)
            Array.Reverse(chosenTimes);

        if (chosenTimes[0] + 5 < maxTime) //these two adjustments ensure you don't have two times too close together.
            chosenTimes[0] += ZenModeActive ? -5 : 5;
        if (chosenTimes[2] - 5 > 0)
            chosenTimes[2] -= ZenModeActive ? -5 : 5;

        //strings
        for (int t = 0; t < 3; t++)
        {
            if (3 - number > t)
            {
                timeStrings[t] = "---";
            }
            else
            {
                if (chosenTimes[t] >= 3600 && calcHours)
                {
                    timeStrings[t] = Mathf.FloorToInt(chosenTimes[t] / 3600) + ":"
                                    + Mathf.FloorToInt(chosenTimes[t] % 3600 / 60).ToString("00") + ":"
                                    + Mathf.FloorToInt(chosenTimes[t] % 60).ToString("00");
                }
                else
                {
                    timeStrings[t] = Mathf.FloorToInt(chosenTimes[t] / 60).ToString("00")
                                    + ":" + Mathf.FloorToInt(chosenTimes[t] % 60).ToString("00");
                }
            }
        }

        Debug.LogFormat("[Timing is Everything #{0}] Generated times: {1}", moduleId, timeStrings.Join(", "));
        SetTime();
        moduleReady = true;
    }

    void SetTime()
    {
        displayedTime = chosenTimes[submittedStages];
        Text.text = timeStrings[submittedStages];
    }

    // Update is called once per frame
    private void Update()
    {
        if (!moduleReady || moduleSolved || TimeModeActive) { return; }
        if (Mathf.FloorToInt(Bomb.GetTime()) == startTime) { return; }
        /* the above line fixes a bug where in zen mode, the game will *
         * still use the original 'set' time, and since that's greater *
         * than the displayed time, a strike would happen at the start *
         * of the bomb. fortunately, the module won't generate it ever */

        if (ZenModeActive)
        {
            if (Mathf.FloorToInt(Bomb.GetTime()) > displayedTime)
                Strike(true);
        }
        else
        {
            if (Mathf.FloorToInt(Bomb.GetTime()) < displayedTime)
                Strike(true);
        }
    }

    void ButtonPush()
    {
        if (moduleSolved || !moduleReady) { return; }
        buttonHold = StartCoroutine(HoldChecker());
    }

    void ButtonRelease()
    {
        StopAllCoroutines();
        if (!holding)
            ButtonPressed();
        holding = false;
    }

    IEnumerator HoldChecker()
    {
        yield return new WaitForSeconds(1f);
        holding = true;
        ButtonHeld();
    }

    void ButtonPressed()
    {
        ButtonSel.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

        if (Mathf.FloorToInt(Bomb.GetTime()) == displayedTime)
        {
            Debug.LogFormat("[Timing is Everything #{0}] Pressed at time {1}.", moduleId, timeStrings[submittedStages]);
            ProgressStage();
            while (Mathf.FloorToInt(Bomb.GetTime()) == displayedTime)
            {
                Debug.LogFormat("<Timing is Everything #{0}> Time was too small so multiple times were stacked on top of eachother, all relevant stages passed.", moduleId);
                ProgressStage();
            }
        }
        else
        {
            //Double-strike prevention: if you just got a strike from missing a time, but then hit the button on the next second (with a new time), it shouldn't strike you a second time.
            if (Mathf.FloorToInt(Bomb.GetTime()) == lastStrike)
            {
                Debug.LogFormat("<Timing is Everything #{0}> Detected double-strike, second strike prevented.", moduleId);
                return;
            }
            Strike(false);
        }
    }

    void ButtonHeld() //This function recreates the time skip feature of Turn The Key's Component Solver in Tweaks
    {
        if (Bomb.GetModuleNames().Count - Bomb.GetSolvableModuleNames().Count > 0) //No needies, don't skip forward
            return;

        float bombTime = Bomb.GetTime();
        if (!(ZenModeActive ? displayedTime - 75 > bombTime : bombTime > displayedTime + 75)) //Too close to the time already, don't skip forward
            return;

        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, transform);

        int offset = 45 + UnityEngine.Random.Range(0, 31);
        TimeRemaining.FromModule(Module, (float)displayedTime + (ZenModeActive ? -offset : offset));
    }

    void Strike(bool genNew)
    {
        lastStrike = Mathf.FloorToInt(Bomb.GetTime());
        Module.HandleStrike();
        Debug.LogFormat("[Timing is Everything #{0}] {1} time {2}, strike!", moduleId, genNew ? "Missed" : "Pressed before", timeStrings[submittedStages]);
        moduleReady = !genNew;
        if (genNew)
        {
            if (ZenModeActive)
            {
                GenerateTimes(3 - submittedStages, Mathf.FloorToInt(Bomb.GetTime()), Mathf.FloorToInt(Bomb.GetTime()) + 30f * Bomb.GetSolvableModuleNames().Count);
            }
            else
            {
                GenerateTimes(3 - submittedStages, 0f, Mathf.FloorToInt(Bomb.GetTime()));
            }
        }
    }

    void ProgressStage()
    {
        tpCorrect = true;
        Lights[submittedStages].material = LightGreen;
        submittedStages++;
        if (submittedStages == 3)
        {
            Module.HandlePass();
            moduleSolved = true;
            Text.text = null;
            Exclaim.text = "! ! !";
            displayedTime = null;
            Debug.LogFormat("[Timing is Everything #{0}] All stages complete. Module solved.", moduleId);
        }
        else
        {
            SetTime();
        }
    }

    //twitch plays
#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} submit|press at|on <time> [Presses the submit button at the specified time.]";
#pragma warning restore 414
    private IEnumerator ProcessTwitchCommand(string command)
    {
        var m = Regex.Match(command, @"^(?:submit|press)\s*(?:at|on)?\s*([0-9]+:)?([0-9]+):([0-5][0-9])$");
        if (!m.Success || m.Groups[1].Success && int.Parse(m.Groups[2].Value) > 59) //Invalid command or time format with hour while having > 59 minutes (eg. 2:99:00)
            yield break;

        var commandSeconds = (!m.Groups[1].Success ? 0 : int.Parse(m.Groups[1].Value.Replace(":", ""))) * 3600 + int.Parse(m.Groups[2].Value) * 60 + int.Parse(m.Groups[3].Value);

        if (!ZenModeActive) {
            if (Mathf.FloorToInt(Bomb.GetTime()) < commandSeconds)
                yield break;
        } else if (Mathf.FloorToInt(Bomb.GetTime()) > commandSeconds)
            yield break;

        yield return null;

        int timeToSkipTo;
        var music = false;

        if (ZenModeActive)
        {
            timeToSkipTo = commandSeconds - 5;
            if (commandSeconds - Bomb.GetTime() > 15) { yield return "skiptime " + timeToSkipTo; }
            if (commandSeconds - Bomb.GetTime() > 10) { music = true; }
        }
        else
        {
            timeToSkipTo = commandSeconds + 5;
            if (Bomb.GetTime() - commandSeconds > 15) { yield return "skiptime " + timeToSkipTo; }
            if (Bomb.GetTime() - commandSeconds > 10) { music = true; }
        }

        if (music) { yield return "waiting music"; }

        while (Mathf.FloorToInt(Bomb.GetTime()) != commandSeconds)
            yield return "trycancel Button wasn't pressed due to request to cancel.";

        if (music) { yield return "end waiting music"; }

        ButtonSel.OnInteract();
        ButtonSel.OnInteractEnded();

        if (tpCorrect)
        {
            yield return "awardpoints 1";
            tpCorrect = false;
        }
    }
    private void TwitchHandleForcedSolve()
    {
        Debug.LogFormat("[Timing is Everything #{0}] Force solve requested by Twitch Plays.", moduleId);
        while (!moduleSolved) {
            ProgressStage();
        }
    }
}